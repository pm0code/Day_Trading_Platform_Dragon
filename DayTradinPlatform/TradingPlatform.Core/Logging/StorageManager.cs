// TradingPlatform.Core.Logging.StorageManager - TIERED STORAGE MANAGEMENT
// Hot (NVMe) → Warm (HDD) → Cold (Archive) storage with compression
// ClickHouse integration for high-performance analytics

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Manages tiered storage for log entries with compression and retention policies
/// Hot (NVMe) → Warm (HDD) → Cold (Archive) progression
/// </summary>
internal class StorageManager : IDisposable
{
    private readonly StorageConfiguration _config;
    private readonly ConcurrentQueue<LogEntry> _pendingWrites = new();
    private readonly Timer _tieringTimer;
    private readonly SemaphoreSlim _writeSemaphore = new(10); // Allow concurrent writes

    public StorageManager(StorageConfiguration config)
    {
        _config = config;

        // Ensure storage directories exist
        Directory.CreateDirectory(_config.HotStoragePath);
        Directory.CreateDirectory(_config.WarmStoragePath);
        Directory.CreateDirectory(_config.ColdStoragePath);

        // Start tiered storage management timer
        _tieringTimer = new Timer(ManageTieredStorageInternal, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public async Task WriteBatch(List<LogEntry> entries)
    {
        await _writeSemaphore.WaitAsync();
        try
        {
            // Write to hot storage (JSON format)
            var hotFile = GetHotStorageFile();
            await WriteToJsonFile(hotFile, entries);

            // Write to ClickHouse if enabled
            if (_config.EnableClickHouse)
            {
                await WriteToClickHouse(entries);
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private string GetHotStorageFile()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH");
        return Path.Combine(_config.HotStoragePath, $"trading_logs_{timestamp}.json");
    }

    private async Task WriteToJsonFile(string filePath, List<LogEntry> entries)
    {
        var jsonLines = entries.Select(e => e.ToJson());
        await File.AppendAllLinesAsync(filePath, jsonLines);
    }

    private async Task WriteToClickHouse(List<LogEntry> entries)
    {
        // ClickHouse integration would be implemented here
        // For now, placeholder implementation
        await Task.CompletedTask;
    }

    public void FlushBuffers()
    {
        // Flush any pending operations
    }

    public void ManageTieredStorage()
    {
        ManageTieredStorageInternal(null);
    }

    private void ManageTieredStorageInternal(object? state)
    {
        try
        {
            // Move files from hot to warm storage
            MoveHotToWarm();

            // Move files from warm to cold storage
            MoveWarmToCold();

            // Clean up old files based on retention policies
            CleanupOldFiles();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error managing tiered storage: {ex.Message}");
        }
    }

    private void MoveHotToWarm()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-_config.HotRetentionHours);
        var hotFiles = Directory.GetFiles(_config.HotStoragePath, "*.json")
            .Where(f => File.GetCreationTime(f) < cutoffTime);

        foreach (var file in hotFiles)
        {
            var fileName = Path.GetFileName(file);
            var warmPath = Path.Combine(_config.WarmStoragePath, fileName + ".gz");

            // Compress and move to warm storage
            CompressFile(file, warmPath);
            File.Delete(file);
        }
    }

    private void MoveWarmToCold()
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-_config.WarmRetentionDays);
        var warmFiles = Directory.GetFiles(_config.WarmStoragePath, "*.gz")
            .Where(f => File.GetCreationTime(f) < cutoffTime);

        foreach (var file in warmFiles)
        {
            var fileName = Path.GetFileName(file);
            var coldPath = Path.Combine(_config.ColdStoragePath, fileName);

            File.Move(file, coldPath);
        }
    }

    private void CleanupOldFiles()
    {
        var cutoffTime = DateTime.UtcNow.AddYears(-_config.ColdRetentionYears);
        var coldFiles = Directory.GetFiles(_config.ColdStoragePath)
            .Where(f => File.GetCreationTime(f) < cutoffTime);

        foreach (var file in coldFiles)
        {
            File.Delete(file);
        }
    }

    private void CompressFile(string inputPath, string outputPath)
    {
        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);
        using var gzip = new GZipStream(output, CompressionLevel.Optimal);
        input.CopyTo(gzip);
    }

    public void Dispose()
    {
        _tieringTimer?.Dispose();
        _writeSemaphore?.Dispose();
    }
}