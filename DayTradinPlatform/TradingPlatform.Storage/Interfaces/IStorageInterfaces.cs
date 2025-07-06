using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TradingPlatform.Storage.Services;

namespace TradingPlatform.Storage.Interfaces;

/// <summary>
/// Interface for tiered storage management
/// </summary>
public interface ITieredStorageManager : IDisposable
{
    /// <summary>
    /// Stores data in the appropriate tier
    /// </summary>
    Task<string> StoreDataAsync(string dataType, string identifier, Stream data, 
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Retrieves data from any tier
    /// </summary>
    Task<Stream?> RetrieveDataAsync(string dataType, string identifier);

    /// <summary>
    /// Gets current storage metrics
    /// </summary>
    Task<StorageMetrics> GetStorageMetricsAsync();
}

/// <summary>
/// Interface for compression operations
/// </summary>
public interface ICompressionService : IDisposable
{
    /// <summary>
    /// Compresses data stream
    /// </summary>
    Task<Stream> CompressAsync(Stream input, string algorithm = "zstd", int level = 6);

    /// <summary>
    /// Decompresses data stream
    /// </summary>
    Task<Stream> DecompressAsync(Stream input);

    /// <summary>
    /// Compresses file
    /// </summary>
    Task<string> CompressFileAsync(string filePath, string algorithm = "zstd", 
        int level = 6, bool deleteOriginal = false);

    /// <summary>
    /// Estimates compression ratio
    /// </summary>
    double EstimateCompressionRatio(string dataType, string algorithm = "zstd");
}

/// <summary>
/// Interface for archive operations
/// </summary>
public interface IArchiveService : IDisposable
{
    /// <summary>
    /// Archives data to cold storage
    /// </summary>
    Task<string> ArchiveDataAsync(string sourcePath, string dataType, 
        Dictionary<string, string> metadata);

    /// <summary>
    /// Restores data from archive
    /// </summary>
    Task<string> RestoreFromArchiveAsync(string archivePath, string targetPath);

    /// <summary>
    /// Lists available archives
    /// </summary>
    Task<List<ArchiveInfo>> ListArchivesAsync(string? dataType = null, 
        DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Cleans up expired archives
    /// </summary>
    Task<int> CleanupArchivesAsync();
}

/// <summary>
/// Interface for storage metrics collection
/// </summary>
public interface IStorageMetricsCollector : IDisposable
{
    /// <summary>
    /// Records storage operation metrics
    /// </summary>
    Task RecordStorageOperationAsync(string operation, StorageTier tier, 
        string dataType, long sizeBytes);

    /// <summary>
    /// Gets aggregated metrics
    /// </summary>
    Task<Dictionary<string, object>> GetAggregatedMetricsAsync(TimeSpan period);
}

/// <summary>
/// Interface for high-speed data writer
/// </summary>
public interface IHighSpeedDataWriter : IDisposable
{
    /// <summary>
    /// Writes market data with minimal latency
    /// </summary>
    Task WriteMarketDataAsync(string symbol, object data);

    /// <summary>
    /// Writes order execution data
    /// </summary>
    Task WriteExecutionDataAsync(string orderId, object executionData);

    /// <summary>
    /// Flushes pending writes
    /// </summary>
    Task FlushAsync();
}

/// <summary>
/// Interface for data persistence to TimeSeries DB
/// </summary>
public interface ITimeSeriesPersistence : IDisposable
{
    /// <summary>
    /// Writes time series data point
    /// </summary>
    Task WriteAsync<T>(T dataPoint) where T : class;

    /// <summary>
    /// Writes batch of time series data
    /// </summary>
    Task WriteBatchAsync<T>(IEnumerable<T> dataPoints) where T : class;

    /// <summary>
    /// Queries time series data
    /// </summary>
    Task<List<T>> QueryAsync<T>(string measurement, DateTime start, DateTime end, 
        Dictionary<string, string>? tags = null) where T : class;
}