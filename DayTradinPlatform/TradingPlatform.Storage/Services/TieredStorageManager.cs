using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Storage.Configuration;
using TradingPlatform.Storage.Interfaces;

namespace TradingPlatform.Storage.Services;

/// <summary>
/// Manages tiered storage lifecycle for trading data
/// Automatically moves data between hot (NVMe), warm (NVMe), and cold (NAS) tiers
/// </summary>
public class TieredStorageManager : CanonicalServiceBase, ITieredStorageManager
{
    private readonly StorageConfiguration _config;
    private readonly IStorageMetricsCollector _metricsCollector;
    private readonly ICompressionService _compressionService;
    private readonly IArchiveService _archiveService;
    private readonly ConcurrentDictionary<string, StorageTierInfo> _tierInfo;
    private readonly SemaphoreSlim _migrationSemaphore;
    private readonly Timer _tieringTimer;
    private readonly Timer _cleanupTimer;

    public TieredStorageManager(
        ITradingLogger logger,
        StorageConfiguration config,
        IStorageMetricsCollector metricsCollector,
        ICompressionService compressionService,
        IArchiveService archiveService) : base(logger, "TieredStorageManager")
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        
        _tierInfo = new ConcurrentDictionary<string, StorageTierInfo>();
        _migrationSemaphore = new SemaphoreSlim(1, 1);
        
        // Schedule tiering operations every hour
        _tieringTimer = new Timer(async _ => await PerformTieringAsync(), null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        
        // Schedule cleanup operations daily at 3 AM
        _cleanupTimer = new Timer(async _ => await PerformCleanupAsync(), null,
            CalculateNextCleanupTime(), TimeSpan.FromDays(1));
    }

    /// <summary>
    /// Stores data in the appropriate tier based on type and characteristics
    /// </summary>
    public async Task<string> StoreDataAsync(string dataType, string identifier, Stream data, 
        Dictionary<string, string>? metadata = null)
    {
        LogMethodEntry();

        try
        {
            // Determine initial tier based on data type
            var tier = DetermineInitialTier(dataType);
            var path = GenerateStoragePath(tier, dataType, identifier);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write data with appropriate optimizations
            await WriteDataAsync(path, data, tier);
            
            // Update tier information
            var tierInfo = new StorageTierInfo
            {
                DataType = dataType,
                Identifier = identifier,
                CurrentTier = tier,
                StoragePath = path,
                Size = data.Length,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
            
            _tierInfo[GenerateKey(dataType, identifier)] = tierInfo;
            
            // Record metrics
            await _metricsCollector.RecordStorageOperationAsync("write", tier, dataType, data.Length);
            
            LogInfo($"Stored {dataType}/{identifier} in {tier} tier at {path} ({data.Length} bytes)");
            
            return path;
        }
        catch (Exception ex)
        {
            LogError($"Failed to store {dataType}/{identifier}", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Retrieves data from any tier with automatic caching
    /// </summary>
    public async Task<Stream?> RetrieveDataAsync(string dataType, string identifier)
    {
        LogMethodEntry();

        try
        {
            var key = GenerateKey(dataType, identifier);
            
            if (!_tierInfo.TryGetValue(key, out var tierInfo))
            {
                LogWarning($"Data not found: {dataType}/{identifier}");
                return null;
            }

            // Update access time
            tierInfo.LastAccessedAt = DateTime.UtcNow;
            
            // Check if file exists
            if (!File.Exists(tierInfo.StoragePath))
            {
                LogError($"File not found at expected path: {tierInfo.StoragePath}");
                return null;
            }

            // Read data based on tier
            var data = await ReadDataAsync(tierInfo.StoragePath, tierInfo.CurrentTier);
            
            // Record metrics
            await _metricsCollector.RecordStorageOperationAsync("read", tierInfo.CurrentTier, 
                dataType, tierInfo.Size);
            
            // Consider promoting frequently accessed cold data
            if (tierInfo.CurrentTier == StorageTier.Cold && 
                await ShouldPromoteData(tierInfo))
            {
                _ = Task.Run(() => PromoteDataAsync(tierInfo));
            }
            
            return data;
        }
        catch (Exception ex)
        {
            LogError($"Failed to retrieve {dataType}/{identifier}", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Gets current storage metrics for monitoring
    /// </summary>
    public async Task<StorageMetrics> GetStorageMetricsAsync()
    {
        LogMethodEntry();

        try
        {
            var metrics = new StorageMetrics
            {
                Timestamp = DateTime.UtcNow,
                HotTier = await GetTierMetricsAsync(StorageTier.Hot),
                WarmTier = await GetTierMetricsAsync(StorageTier.Warm),
                ColdTier = await GetTierMetricsAsync(StorageTier.Cold),
                TotalDataPoints = _tierInfo.Count
            };

            // Calculate totals
            metrics.TotalSizeGB = (metrics.HotTier.UsedSizeGB + 
                                  metrics.WarmTier.UsedSizeGB + 
                                  metrics.ColdTier.UsedSizeGB);
            
            metrics.CompressionRatio = CalculateOverallCompressionRatio();

            return metrics;
        }
        catch (Exception ex)
        {
            LogError("Failed to get storage metrics", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Performs automatic data tiering based on policies
    /// </summary>
    private async Task PerformTieringAsync()
    {
        LogMethodEntry();

        if (!await _migrationSemaphore.WaitAsync(0))
        {
            LogInfo("Tiering operation already in progress, skipping");
            return;
        }

        try
        {
            LogInfo("Starting automated tiering operation");
            
            var migrationTasks = new List<Task>();
            var now = DateTime.UtcNow;

            // Check each data point for tiering eligibility
            foreach (var kvp in _tierInfo)
            {
                var tierInfo = kvp.Value;
                var policy = GetRetentionPolicy(tierInfo.DataType);
                
                if (policy == null) continue;

                // Hot to Warm migration
                if (tierInfo.CurrentTier == StorageTier.Hot)
                {
                    var age = (now - tierInfo.CreatedAt).TotalDays;
                    if (age > policy.HotRetentionDays || 
                        await IsHotTierFull())
                    {
                        migrationTasks.Add(MigrateToWarmAsync(tierInfo));
                    }
                }
                // Warm to Cold migration
                else if (tierInfo.CurrentTier == StorageTier.Warm)
                {
                    var age = (now - tierInfo.CreatedAt).TotalDays;
                    if (age > policy.WarmRetentionDays || 
                        await IsWarmTierFull())
                    {
                        migrationTasks.Add(MigrateToColdAsync(tierInfo));
                    }
                }
            }

            // Execute migrations with controlled parallelism
            await Task.WhenAll(migrationTasks.Take(5)); // Max 5 concurrent migrations
            
            LogInfo($"Tiering operation completed. Migrated {migrationTasks.Count} items");
        }
        catch (Exception ex)
        {
            LogError("Tiering operation failed", ex);
        }
        finally
        {
            _migrationSemaphore.Release();
            LogMethodExit();
        }
    }

    /// <summary>
    /// Migrates data from hot to warm tier with compression
    /// </summary>
    private async Task MigrateToWarmAsync(StorageTierInfo tierInfo)
    {
        LogMethodEntry();

        try
        {
            var sourceFile = tierInfo.StoragePath;
            var targetPath = GenerateStoragePath(StorageTier.Warm, tierInfo.DataType, tierInfo.Identifier);
            
            // Ensure target directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            // Read source data
            using var sourceData = await ReadDataAsync(sourceFile, StorageTier.Hot);
            if (sourceData == null) return;

            // Compress if enabled
            if (_config.WarmTier.EnableCompression)
            {
                using var compressedData = await _compressionService.CompressAsync(
                    sourceData, _config.Compression.Algorithm, _config.WarmTier.CompressionLevel);
                
                await WriteDataAsync(targetPath + ".zst", compressedData, StorageTier.Warm);
                
                // Update tier info
                tierInfo.StoragePath = targetPath + ".zst";
                tierInfo.IsCompressed = true;
                tierInfo.CompressedSize = compressedData.Length;
            }
            else
            {
                sourceData.Position = 0;
                await WriteDataAsync(targetPath, sourceData, StorageTier.Warm);
                tierInfo.StoragePath = targetPath;
            }

            // Update tier info
            tierInfo.CurrentTier = StorageTier.Warm;
            tierInfo.MigratedAt = DateTime.UtcNow;

            // Delete source file
            File.Delete(sourceFile);
            
            LogInfo($"Migrated {tierInfo.DataType}/{tierInfo.Identifier} from Hot to Warm tier");
        }
        catch (Exception ex)
        {
            LogError($"Failed to migrate {tierInfo.Identifier} to warm tier", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Migrates data from warm to cold tier (NAS) with archival
    /// </summary>
    private async Task MigrateToColdAsync(StorageTierInfo tierInfo)
    {
        LogMethodEntry();

        try
        {
            var sourceFile = tierInfo.StoragePath;
            
            // Create archive with metadata
            var archiveMetadata = new Dictionary<string, string>(tierInfo.Metadata)
            {
                ["original_size"] = tierInfo.Size.ToString(),
                ["created_at"] = tierInfo.CreatedAt.ToString("O"),
                ["data_type"] = tierInfo.DataType,
                ["identifier"] = tierInfo.Identifier
            };

            // Archive to NAS
            var archivePath = await _archiveService.ArchiveDataAsync(
                sourceFile, tierInfo.DataType, archiveMetadata);

            // Update tier info
            tierInfo.CurrentTier = StorageTier.Cold;
            tierInfo.StoragePath = archivePath;
            tierInfo.MigratedAt = DateTime.UtcNow;
            tierInfo.IsArchived = true;

            // Delete source file
            File.Delete(sourceFile);
            
            LogInfo($"Migrated {tierInfo.DataType}/{tierInfo.Identifier} from Warm to Cold tier (NAS)");
        }
        catch (Exception ex)
        {
            LogError($"Failed to migrate {tierInfo.Identifier} to cold tier", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Performs cleanup of expired data based on retention policies
    /// </summary>
    private async Task PerformCleanupAsync()
    {
        LogMethodEntry();

        try
        {
            LogInfo("Starting storage cleanup operation");
            
            var deletionCount = 0;
            var now = DateTime.UtcNow;

            foreach (var kvp in _tierInfo.ToList())
            {
                var tierInfo = kvp.Value;
                var policy = GetRetentionPolicy(tierInfo.DataType);
                
                if (policy == null) continue;

                var age = (now - tierInfo.CreatedAt).TotalDays;
                var shouldDelete = false;

                // Check retention based on tier
                switch (tierInfo.CurrentTier)
                {
                    case StorageTier.Cold:
                        shouldDelete = policy.ColdRetentionYears > 0 && 
                                     age > (policy.ColdRetentionYears * 365);
                        break;
                }

                if (shouldDelete)
                {
                    try
                    {
                        if (File.Exists(tierInfo.StoragePath))
                        {
                            File.Delete(tierInfo.StoragePath);
                        }
                        
                        _tierInfo.TryRemove(kvp.Key, out _);
                        deletionCount++;
                        
                        LogInfo($"Deleted expired data: {tierInfo.DataType}/{tierInfo.Identifier}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to delete {tierInfo.StoragePath}", ex);
                    }
                }
            }

            LogInfo($"Cleanup completed. Deleted {deletionCount} expired items");
        }
        catch (Exception ex)
        {
            LogError("Cleanup operation failed", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Helper methods
    private StorageTier DetermineInitialTier(string dataType)
    {
        LogMethodEntry();
        try
        {
            // Real-time data always starts in hot tier
            var tier = dataType.ToLower() switch
            {
                "tickdata" => StorageTier.Hot,
                "orderbook" => StorageTier.Hot,
                "executions" => StorageTier.Hot,
                "positions" => StorageTier.Hot,
                "marketdata" => StorageTier.Hot,
                _ => StorageTier.Warm
            };
            
            LogMethodExit();
            return tier;
        }
        catch (Exception ex)
        {
            LogError($"Failed to determine initial tier for {dataType}", ex);
            LogMethodExit();
            throw;
        }
    }

    private string GenerateStoragePath(StorageTier tier, string dataType, string identifier)
    {
        LogMethodEntry();
        try
        {
            var basePath = tier switch
            {
                StorageTier.Hot => _config.HotTier.BasePath,
                StorageTier.Warm => _config.WarmTier.BasePath,
                StorageTier.Cold => _config.ColdTier.BasePath,
                _ => throw new ArgumentException($"Unknown tier: {tier}")
            };

            var dataPath = tier == StorageTier.Hot && _config.HotTier.DataPaths.ContainsKey(dataType)
                ? _config.HotTier.DataPaths[dataType]
                : dataType;

            var date = DateTime.UtcNow;
            var path = Path.Combine(basePath, dataPath, 
                $"{date:yyyy/MM/dd}", $"{identifier}_{date:HHmmss}.dat");
            
            LogMethodExit();
            return path;
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate storage path for tier={tier}, dataType={dataType}, identifier={identifier}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task WriteDataAsync(string path, Stream data, StorageTier tier)
    {
        LogMethodEntry();
        try
        {
            var bufferSize = tier == StorageTier.Hot 
                ? _config.HotTier.WriteBufferSizeMB * 1024 * 1024
                : _config.Performance.WriteBufferMB * 1024 * 1024;

            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, 
                FileShare.None, bufferSize, _config.Performance.EnableAsyncIO);
            
            await data.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write data to {path}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<Stream?> ReadDataAsync(string path, StorageTier tier)
    {
        LogMethodEntry();
        try
        {
            if (!File.Exists(path))
            {
                LogMethodExit();
                return null;
            }

            var bufferSize = _config.Performance.ReadAheadBufferMB * 1024 * 1024;
            
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, 
                FileShare.Read, bufferSize, _config.Performance.EnableAsyncIO);

            // Decompress if needed
            if (path.EndsWith(".zst") || path.EndsWith(".gz"))
            {
                var decompressedStream = await _compressionService.DecompressAsync(fileStream);
                LogMethodExit();
                return decompressedStream;
            }

            LogMethodExit();
            return fileStream;
        }
        catch (Exception ex)
        {
            LogError($"Failed to read data from {path}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<TierMetrics> GetTierMetricsAsync(StorageTier tier)
    {
        LogMethodEntry();
        try
        {
            var items = _tierInfo.Values.Where(t => t.CurrentTier == tier).ToList();
            var totalSize = items.Sum(t => t.IsCompressed ? t.CompressedSize : t.Size);
            
            var metrics = new TierMetrics
            {
                Tier = tier,
                ItemCount = items.Count,
                UsedSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0),
                OldestDataAge = items.Any() 
                    ? (DateTime.UtcNow - items.Min(t => t.CreatedAt)).TotalDays 
                    : 0,
                CompressionRatio = CalculateTierCompressionRatio(items)
            };

            // Get capacity based on tier
            metrics.CapacityGB = tier switch
            {
                StorageTier.Hot => _config.HotTier.MaxSizeGB,
                StorageTier.Warm => _config.WarmTier.MaxSizeGB,
                StorageTier.Cold => 4000, // 4TB NAS assumed
                _ => 0
            };

            metrics.UtilizationPercent = metrics.CapacityGB > 0 
                ? (metrics.UsedSizeGB / metrics.CapacityGB) * 100 
                : 0;

            LogMethodExit();
            return metrics;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get tier metrics for {tier}", ex);
            LogMethodExit();
            throw;
        }
    }

    private DataRetentionPolicy? GetRetentionPolicy(string dataType)
    {
        LogMethodEntry();
        try
        {
            var policy = _config.RetentionPolicies.Policies.TryGetValue(dataType, out var p) 
                ? p 
                : null;
            
            LogMethodExit();
            return policy;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get retention policy for {dataType}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<bool> IsHotTierFull()
    {
        LogMethodEntry();
        try
        {
            var metrics = await GetTierMetricsAsync(StorageTier.Hot);
            var isFull = metrics.UtilizationPercent > 85; // 85% threshold
            
            LogMethodExit();
            return isFull;
        }
        catch (Exception ex)
        {
            LogError("Failed to check if hot tier is full", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<bool> IsWarmTierFull()
    {
        LogMethodEntry();
        try
        {
            var metrics = await GetTierMetricsAsync(StorageTier.Warm);
            var isFull = metrics.UtilizationPercent > 90; // 90% threshold
            
            LogMethodExit();
            return isFull;
        }
        catch (Exception ex)
        {
            LogError("Failed to check if warm tier is full", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<bool> ShouldPromoteData(StorageTierInfo tierInfo)
    {
        LogMethodEntry();
        try
        {
            // Promote if accessed frequently (more than 5 times in last 7 days)
            // This is simplified - in production, track access patterns
            var daysSinceCreation = (DateTime.UtcNow - tierInfo.CreatedAt).TotalDays;
            var daysSinceAccess = (DateTime.UtcNow - tierInfo.LastAccessedAt).TotalDays;
            
            var shouldPromote = daysSinceAccess < 7 && daysSinceCreation > 30;
            
            LogMethodExit();
            return shouldPromote;
        }
        catch (Exception ex)
        {
            LogError($"Failed to check if should promote data for {tierInfo.Identifier}", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task PromoteDataAsync(StorageTierInfo tierInfo)
    {
        LogMethodEntry();
        try
        {
            // Implement data promotion from cold to warm tier
            // This is an optimization for frequently accessed archived data
            LogInfo($"Promoting frequently accessed cold data: {tierInfo.DataType}/{tierInfo.Identifier}");
            // Implementation would restore from archive to warm tier
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to promote data for {tierInfo.Identifier}", ex);
            LogMethodExit();
            throw;
        }
    }

    private double CalculateOverallCompressionRatio()
    {
        LogMethodEntry();
        try
        {
            var compressedItems = _tierInfo.Values.Where(t => t.IsCompressed).ToList();
            if (!compressedItems.Any())
            {
                LogMethodExit();
                return 1.0;
            }

            var originalSize = compressedItems.Sum(t => t.Size);
            var compressedSize = compressedItems.Sum(t => t.CompressedSize);
            
            var ratio = originalSize > 0 ? (double)compressedSize / originalSize : 1.0;
            
            LogMethodExit();
            return ratio;
        }
        catch (Exception ex)
        {
            LogError("Failed to calculate overall compression ratio", ex);
            LogMethodExit();
            throw;
        }
    }

    private double CalculateTierCompressionRatio(List<StorageTierInfo> items)
    {
        LogMethodEntry();
        try
        {
            var compressedItems = items.Where(t => t.IsCompressed).ToList();
            if (!compressedItems.Any())
            {
                LogMethodExit();
                return 1.0;
            }

            var originalSize = compressedItems.Sum(t => t.Size);
            var compressedSize = compressedItems.Sum(t => t.CompressedSize);
            
            var ratio = originalSize > 0 ? (double)compressedSize / originalSize : 1.0;
            
            LogMethodExit();
            return ratio;
        }
        catch (Exception ex)
        {
            LogError("Failed to calculate tier compression ratio", ex);
            LogMethodExit();
            throw;
        }
    }

    private string GenerateKey(string dataType, string identifier)
    {
        LogMethodEntry();
        try
        {
            var key = $"{dataType}:{identifier}";
            
            LogMethodExit();
            return key;
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate key for dataType={dataType}, identifier={identifier}", ex);
            LogMethodExit();
            throw;
        }
    }

    private TimeSpan CalculateNextCleanupTime()
    {
        LogMethodEntry();
        try
        {
            var now = DateTime.Now;
            var threAm = now.Date.AddHours(3);
            if (now > threAm)
                threAm = threAm.AddDays(1);
            
            var timeSpan = threAm - now;
            
            LogMethodExit();
            return timeSpan;
        }
        catch (Exception ex)
        {
            LogError("Failed to calculate next cleanup time", ex);
            LogMethodExit();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tieringTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _migrationSemaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// Supporting classes
public enum StorageTier
{
    Hot,
    Warm,
    Cold
}

public class StorageTierInfo
{
    public string DataType { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public StorageTier CurrentTier { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime? MigratedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class StorageMetrics
{
    public DateTime Timestamp { get; set; }
    public TierMetrics HotTier { get; set; } = new();
    public TierMetrics WarmTier { get; set; } = new();
    public TierMetrics ColdTier { get; set; } = new();
    public double TotalSizeGB { get; set; }
    public int TotalDataPoints { get; set; }
    public double CompressionRatio { get; set; }
}

public class TierMetrics
{
    public StorageTier Tier { get; set; }
    public int ItemCount { get; set; }
    public double UsedSizeGB { get; set; }
    public double CapacityGB { get; set; }
    public double UtilizationPercent { get; set; }
    public double OldestDataAge { get; set; }
    public double CompressionRatio { get; set; }
}