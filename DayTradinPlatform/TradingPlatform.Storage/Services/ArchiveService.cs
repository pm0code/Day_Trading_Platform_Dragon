using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Storage.Configuration;
using TradingPlatform.Storage.Interfaces;

namespace TradingPlatform.Storage.Services;

/// <summary>
/// Manages archival of trading data to NAS storage
/// Handles Parquet format conversion, deduplication, and archive organization
/// </summary>
public class ArchiveService : CanonicalServiceBase, IArchiveService
{
    private readonly StorageConfiguration _config;
    private readonly ICompressionService _compressionService;
    private readonly SemaphoreSlim _archiveSemaphore;

    public ArchiveService(
        ITradingLogger logger,
        StorageConfiguration config,
        ICompressionService compressionService) : base(logger, "ArchiveService")
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _archiveSemaphore = new SemaphoreSlim(_config.ColdTier.ParallelArchiveOperations);
    }

    /// <summary>
    /// Archives data to NAS storage with metadata
    /// </summary>
    public async Task<string> ArchiveDataAsync(string sourcePath, string dataType, 
        Dictionary<string, string> metadata)
    {
        LogMethodEntry();

        await _archiveSemaphore.WaitAsync();

        try
        {
            // Generate archive path
            var archivePath = GenerateArchivePath(dataType, metadata);
            
            // Ensure archive directory exists
            var archiveDir = Path.GetDirectoryName(archivePath);
            if (!string.IsNullOrEmpty(archiveDir))
            {
                Directory.CreateDirectory(archiveDir);
            }

            // Check if we need to convert to Parquet
            if (_config.ColdTier.ArchiveFormat.ToLower() == "parquet")
            {
                archivePath = await ConvertToParquetAsync(sourcePath, archivePath, dataType, metadata);
            }
            else
            {
                // Direct copy with compression
                await CopyToArchiveAsync(sourcePath, archivePath);
            }

            // Write metadata sidecar file
            await WriteMetadataAsync(archivePath, metadata);

            // Apply deduplication if enabled
            if (_config.ColdTier.EnableDeduplication)
            {
                await ApplyDeduplicationAsync(archivePath, dataType);
            }

            LogInfo($"Archived {sourcePath} to {archivePath} ({new FileInfo(archivePath).Length / 1024.0 / 1024.0:F2} MB)");

            return archivePath;
        }
        catch (Exception ex)
        {
            LogError($"Failed to archive {sourcePath}", ex);
            throw;
        }
        finally
        {
            _archiveSemaphore.Release();
            LogMethodExit();
        }
    }

    /// <summary>
    /// Restores data from archive to specified location
    /// </summary>
    public async Task<string> RestoreFromArchiveAsync(string archivePath, string targetPath)
    {
        LogMethodEntry();

        try
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"Archive not found: {archivePath}");
            }

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Check if we need to convert from Parquet
            if (archivePath.EndsWith(".parquet"))
            {
                targetPath = await ConvertFromParquetAsync(archivePath, targetPath);
            }
            else
            {
                // Direct copy with decompression
                await RestoreFromArchiveAsyncInternal(archivePath, targetPath);
            }

            LogInfo($"Restored {archivePath} to {targetPath}");

            return targetPath;
        }
        catch (Exception ex)
        {
            LogError($"Failed to restore {archivePath}", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Lists archives matching criteria
    /// </summary>
    public async Task<List<ArchiveInfo>> ListArchivesAsync(string? dataType = null, 
        DateTime? startDate = null, DateTime? endDate = null)
    {
        LogMethodEntry();

        try
        {
            var archives = new List<ArchiveInfo>();
            var searchPath = Path.Combine(_config.ColdTier.BasePath, dataType ?? "*");

            // Search for archive files
            var files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".parquet") || f.EndsWith(".zst") || f.EndsWith(".gz"));

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    var metadataPath = file + ".metadata";
                    var metadata = File.Exists(metadataPath) 
                        ? await ReadMetadataAsync(metadataPath) 
                        : new Dictionary<string, string>();

                    // Apply date filters
                    if (startDate.HasValue || endDate.HasValue)
                    {
                        if (metadata.TryGetValue("created_at", out var createdStr) && 
                            DateTime.TryParse(createdStr, out var created))
                        {
                            if (startDate.HasValue && created < startDate.Value) continue;
                            if (endDate.HasValue && created > endDate.Value) continue;
                        }
                        else
                        {
                            // Use file creation time as fallback
                            if (startDate.HasValue && info.CreationTimeUtc < startDate.Value) continue;
                            if (endDate.HasValue && info.CreationTimeUtc > endDate.Value) continue;
                        }
                    }

                    archives.Add(new ArchiveInfo
                    {
                        Path = file,
                        DataType = metadata.GetValueOrDefault("data_type", "unknown"),
                        Size = info.Length,
                        CreatedAt = info.CreationTimeUtc,
                        Metadata = metadata
                    });
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to read archive info for {file}: {ex.Message}");
                }
            }

            LogInfo($"Found {archives.Count} archives matching criteria");

            return archives.OrderByDescending(a => a.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            LogError("Failed to list archives", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Cleans up old archives based on retention policy
    /// </summary>
    public async Task<int> CleanupArchivesAsync()
    {
        LogMethodEntry();

        try
        {
            var deletedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var policy in _config.RetentionPolicies.Policies)
            {
                if (policy.Value.ColdRetentionYears <= 0) continue;

                var archives = await ListArchivesAsync(policy.Key);
                var cutoffDate = now.AddYears(-policy.Value.ColdRetentionYears);

                foreach (var archive in archives.Where(a => a.CreatedAt < cutoffDate))
                {
                    try
                    {
                        File.Delete(archive.Path);
                        
                        // Delete metadata file if exists
                        var metadataPath = archive.Path + ".metadata";
                        if (File.Exists(metadataPath))
                        {
                            File.Delete(metadataPath);
                        }

                        deletedCount++;
                        LogInfo($"Deleted expired archive: {archive.Path}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to delete archive: {archive.Path}", ex);
                    }
                }
            }

            LogInfo($"Archive cleanup completed. Deleted {deletedCount} expired archives");

            return deletedCount;
        }
        catch (Exception ex)
        {
            LogError("Archive cleanup failed", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Private helper methods
    private string GenerateArchivePath(string dataType, Dictionary<string, string> metadata)
    {
        LogMethodEntry();
        try
        {
            var date = DateTime.UtcNow;
            
            // Extract date from metadata if available
            if (metadata.TryGetValue("created_at", out var createdStr) && 
                DateTime.TryParse(createdStr, out var created))
            {
                date = created;
            }

            var identifier = metadata.GetValueOrDefault("identifier", Guid.NewGuid().ToString("N"));
            var extension = _config.ColdTier.ArchiveFormat.ToLower() == "parquet" ? "parquet" : "zst";

            var path = Path.Combine(
                _config.ColdTier.BasePath,
                dataType,
                $"{date:yyyy/MM}",
                $"{dataType}_{date:yyyyMMdd}_{identifier}.{extension}"
            );
            
            LogMethodExit();
            return path;
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate archive path for {dataType}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<string> ConvertToParquetAsync(string sourcePath, string targetPath, 
        string dataType, Dictionary<string, string> metadata)
    {
        LogMethodEntry();
        try
        {
            // In production, use Parquet.NET or Apache Arrow for actual conversion
            // For now, compress the file as a placeholder
            
            var parquetPath = Path.ChangeExtension(targetPath, ".parquet");
            
            // Simulate Parquet conversion with compression
            using (var sourceStream = File.OpenRead(sourcePath))
            using (var compressedStream = await _compressionService.CompressAsync(sourceStream, "zstd", 9))
            using (var targetStream = File.Create(parquetPath))
            {
                await compressedStream.CopyToAsync(targetStream);
            }

            LogMethodExit();
            return parquetPath;
        }
        catch (Exception ex)
        {
            LogError($"Failed to convert {sourcePath} to Parquet format", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<string> ConvertFromParquetAsync(string archivePath, string targetPath)
    {
        LogMethodEntry();
        try
        {
            // In production, use Parquet.NET or Apache Arrow for actual conversion
            // For now, decompress the file as a placeholder
            
            using (var sourceStream = File.OpenRead(archivePath))
            using (var decompressedStream = await _compressionService.DecompressAsync(sourceStream))
            using (var targetStream = File.Create(targetPath))
            {
                await decompressedStream.CopyToAsync(targetStream);
            }

            LogMethodExit();
            return targetPath;
        }
        catch (Exception ex)
        {
            LogError($"Failed to convert from Parquet format: {archivePath}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task CopyToArchiveAsync(string sourcePath, string targetPath)
    {
        LogMethodEntry();
        try
        {
            // Copy with compression
            using (var sourceStream = File.OpenRead(sourcePath))
            using (var compressedStream = await _compressionService.CompressAsync(sourceStream, "zstd", 9))
            using (var targetStream = File.Create(targetPath + ".zst"))
            {
                await compressedStream.CopyToAsync(targetStream);
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to copy {sourcePath} to archive {targetPath}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task RestoreFromArchiveAsyncInternal(string archivePath, string targetPath)
    {
        LogMethodEntry();
        try
        {
            // Copy with decompression
            using (var sourceStream = File.OpenRead(archivePath))
            using (var decompressedStream = await _compressionService.DecompressAsync(sourceStream))
            using (var targetStream = File.Create(targetPath))
            {
                await decompressedStream.CopyToAsync(targetStream);
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to restore from archive {archivePath} to {targetPath}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task WriteMetadataAsync(string archivePath, Dictionary<string, string> metadata)
    {
        LogMethodEntry();
        try
        {
            var metadataPath = archivePath + ".metadata";
            var lines = metadata.Select(kvp => $"{kvp.Key}={kvp.Value}");
            await File.WriteAllLinesAsync(metadataPath, lines);
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write metadata for {archivePath}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<Dictionary<string, string>> ReadMetadataAsync(string metadataPath)
    {
        LogMethodEntry();
        try
        {
            var metadata = new Dictionary<string, string>();
            
            if (!File.Exists(metadataPath))
            {
                LogMethodExit();
                return metadata;
            }

            var lines = await File.ReadAllLinesAsync(metadataPath);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    metadata[parts[0]] = parts[1];
                }
            }

            LogMethodExit();
            return metadata;
        }
        catch (Exception ex)
        {
            LogError($"Failed to read metadata from {metadataPath}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task ApplyDeduplicationAsync(string archivePath, string dataType)
    {
        LogMethodEntry();
        try
        {
            // Simplified deduplication - in production, use content-based hashing
            // For now, just log that deduplication would be applied
            LogInfo($"Deduplication would be applied to {archivePath} (type: {dataType})");
            await Task.CompletedTask;
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply deduplication to {archivePath}", ex);
            LogMethodExit();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _archiveSemaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// Supporting classes
public class ArchiveInfo
{
    public string Path { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}