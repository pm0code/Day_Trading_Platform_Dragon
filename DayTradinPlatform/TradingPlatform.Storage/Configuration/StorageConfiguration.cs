using System;
using System.Collections.Generic;

namespace TradingPlatform.Storage.Configuration;

/// <summary>
/// Storage configuration for individual day trader platform
/// Implements tiered storage strategy with NVMe for hot/warm data and NAS for cold archives
/// </summary>
public class StorageConfiguration
{
    /// <summary>
    /// Hot tier configuration (NVMe SSD) - Real-time data
    /// </summary>
    public HotTierConfig HotTier { get; set; } = new();

    /// <summary>
    /// Warm tier configuration (NVMe SSD) - Recent historical data
    /// </summary>
    public WarmTierConfig WarmTier { get; set; } = new();

    /// <summary>
    /// Cold tier configuration (NAS) - Long-term archives
    /// </summary>
    public ColdTierConfig ColdTier { get; set; } = new();

    /// <summary>
    /// Data retention policies
    /// </summary>
    public RetentionPolicyConfig RetentionPolicies { get; set; } = new();

    /// <summary>
    /// Compression settings
    /// </summary>
    public CompressionConfig Compression { get; set; } = new();

    /// <summary>
    /// Performance optimization settings
    /// </summary>
    public PerformanceConfig Performance { get; set; } = new();
}

public class HotTierConfig
{
    /// <summary>
    /// Path to NVMe storage for hot data
    /// </summary>
    public string BasePath { get; set; } = @"C:\TradingData\Hot";

    /// <summary>
    /// Maximum size in GB before data moves to warm tier
    /// </summary>
    public int MaxSizeGB { get; set; } = 100;

    /// <summary>
    /// Data age in days before moving to warm tier
    /// </summary>
    public int MaxAgeInDays { get; set; } = 30;

    /// <summary>
    /// Paths for specific data types
    /// </summary>
    public Dictionary<string, string> DataPaths { get; set; } = new()
    {
        ["MarketData"] = "MarketData",
        ["OrderBook"] = "OrderBook",
        ["Executions"] = "Executions",
        ["Positions"] = "Positions",
        ["AIModels"] = "AIModels",
        ["SystemDB"] = "Databases"
    };

    /// <summary>
    /// Write buffer size for high-frequency data
    /// </summary>
    public int WriteBufferSizeMB { get; set; } = 64;

    /// <summary>
    /// Enable write-through caching
    /// </summary>
    public bool EnableWriteThrough { get; set; } = true;
}

public class WarmTierConfig
{
    /// <summary>
    /// Path to NVMe storage for warm data
    /// </summary>
    public string BasePath { get; set; } = @"C:\TradingData\Warm";

    /// <summary>
    /// Maximum size in GB before data moves to cold tier
    /// </summary>
    public int MaxSizeGB { get; set; } = 1000;

    /// <summary>
    /// Data age in days before moving to cold tier
    /// </summary>
    public int MaxAgeInDays { get; set; } = 365;

    /// <summary>
    /// Enable compression for warm data
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Compression level (1-9, higher = better compression)
    /// </summary>
    public int CompressionLevel { get; set; } = 6;

    /// <summary>
    /// Index frequently accessed data
    /// </summary>
    public bool EnableIndexing { get; set; } = true;
}

public class ColdTierConfig
{
    /// <summary>
    /// Path to NAS storage for cold archives
    /// </summary>
    public string BasePath { get; set; } = @"\\NAS\TradingArchives";

    /// <summary>
    /// Archive format
    /// </summary>
    public string ArchiveFormat { get; set; } = "parquet";

    /// <summary>
    /// Enable deduplication for cold storage
    /// </summary>
    public bool EnableDeduplication { get; set; } = true;

    /// <summary>
    /// Maximum archive file size in GB
    /// </summary>
    public int MaxArchiveFileSizeGB { get; set; } = 10;

    /// <summary>
    /// Number of parallel archive operations
    /// </summary>
    public int ParallelArchiveOperations { get; set; } = 2;

    /// <summary>
    /// Archive scheduling (cron expression)
    /// </summary>
    public string ArchiveSchedule { get; set; } = "0 2 * * *"; // 2 AM daily
}

public class RetentionPolicyConfig
{
    /// <summary>
    /// Retention policies by data type
    /// </summary>
    public Dictionary<string, DataRetentionPolicy> Policies { get; set; } = new()
    {
        ["TickData"] = new DataRetentionPolicy
        {
            HotRetentionDays = 7,
            WarmRetentionDays = 30,
            ColdRetentionYears = 2,
            CompressAfterDays = 7,
            AggregateAfterDays = 365
        },
        ["OHLCV_1Min"] = new DataRetentionPolicy
        {
            HotRetentionDays = 30,
            WarmRetentionDays = 365,
            ColdRetentionYears = 2,
            CompressAfterDays = 30
        },
        ["OHLCV_Daily"] = new DataRetentionPolicy
        {
            HotRetentionDays = 365,
            WarmRetentionDays = 1825, // 5 years
            ColdRetentionYears = -1, // Indefinite
            CompressAfterDays = 365
        },
        ["OrderBook"] = new DataRetentionPolicy
        {
            HotRetentionDays = 7,
            WarmRetentionDays = 30,
            ColdRetentionYears = 0, // Don't archive
            CompressAfterDays = 1,
            DepthLevelsToRetain = 10
        },
        ["TradeHistory"] = new DataRetentionPolicy
        {
            HotRetentionDays = 90,
            WarmRetentionDays = 730, // 2 years
            ColdRetentionYears = 7, // Tax purposes
            CompressAfterDays = 90
        },
        ["AIModelData"] = new DataRetentionPolicy
        {
            HotRetentionDays = 30,
            WarmRetentionDays = 180,
            ColdRetentionYears = 1,
            CompressAfterDays = 30
        }
    };
}

public class DataRetentionPolicy
{
    public int HotRetentionDays { get; set; }
    public int WarmRetentionDays { get; set; }
    public int ColdRetentionYears { get; set; }
    public int CompressAfterDays { get; set; }
    public int? AggregateAfterDays { get; set; }
    public int? DepthLevelsToRetain { get; set; }
}

public class CompressionConfig
{
    /// <summary>
    /// Compression algorithm
    /// </summary>
    public string Algorithm { get; set; } = "zstd"; // Zstandard

    /// <summary>
    /// Enable parallel compression
    /// </summary>
    public bool EnableParallelCompression { get; set; } = true;

    /// <summary>
    /// Number of compression threads
    /// </summary>
    public int CompressionThreads { get; set; } = 4;

    /// <summary>
    /// Minimum file size to compress (MB)
    /// </summary>
    public int MinFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Compression ratios by data type
    /// </summary>
    public Dictionary<string, double> ExpectedCompressionRatios { get; set; } = new()
    {
        ["TickData"] = 0.25, // 75% reduction
        ["OHLCV"] = 0.3,    // 70% reduction
        ["OrderBook"] = 0.2, // 80% reduction
        ["JSON"] = 0.15,     // 85% reduction
        ["CSV"] = 0.25       // 75% reduction
    };
}

public class PerformanceConfig
{
    /// <summary>
    /// Enable memory-mapped files for large datasets
    /// </summary>
    public bool EnableMemoryMappedFiles { get; set; } = true;

    /// <summary>
    /// Read-ahead buffer size in MB
    /// </summary>
    public int ReadAheadBufferMB { get; set; } = 32;

    /// <summary>
    /// Write buffer size in MB
    /// </summary>
    public int WriteBufferMB { get; set; } = 64;

    /// <summary>
    /// Enable async I/O operations
    /// </summary>
    public bool EnableAsyncIO { get; set; } = true;

    /// <summary>
    /// Maximum concurrent read operations
    /// </summary>
    public int MaxConcurrentReads { get; set; } = 10;

    /// <summary>
    /// Maximum concurrent write operations
    /// </summary>
    public int MaxConcurrentWrites { get; set; } = 5;

    /// <summary>
    /// Enable direct I/O for bypassing OS cache
    /// </summary>
    public bool EnableDirectIO { get; set; } = false;

    /// <summary>
    /// Pre-allocate file space for better performance
    /// </summary>
    public bool PreAllocateSpace { get; set; } = true;
}