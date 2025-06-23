using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingPlatform.Database.Models;

/// <summary>
/// System performance metrics for latency monitoring and optimization
/// TimescaleDB optimized for high-frequency performance data collection
/// </summary>
[Table("performance_metrics")]
public class PerformanceMetric
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// Metric collection timestamp - hypertable partition key
    /// </summary>
    [Column("timestamp")]
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Hardware timestamp in nanoseconds
    /// </summary>
    [Column("hardware_timestamp_ns")]
    [Required]
    public long HardwareTimestampNs { get; set; }

    /// <summary>
    /// Metric category (FIX_ENGINE, ORDER_ROUTER, MARKET_DATA, etc.)
    /// </summary>
    [Column("category")]
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific operation name
    /// </summary>
    [Column("operation")]
    [Required]
    [MaxLength(100)]
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Latency measurement in nanoseconds
    /// </summary>
    [Column("latency_ns")]
    public long? LatencyNs { get; set; }

    /// <summary>
    /// Throughput (operations per second)
    /// </summary>
    [Column("throughput")]
    public long? Throughput { get; set; }

    /// <summary>
    /// CPU utilization percentage
    /// </summary>
    [Column("cpu_utilization", TypeName = "decimal(5,2)")]
    public decimal? CpuUtilization { get; set; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    [Column("memory_bytes")]
    public long? MemoryBytes { get; set; }

    /// <summary>
    /// Network I/O bytes
    /// </summary>
    [Column("network_io_bytes")]
    public long? NetworkIoBytes { get; set; }

    /// <summary>
    /// Success count for operations
    /// </summary>
    [Column("success_count")]
    public long? SuccessCount { get; set; }

    /// <summary>
    /// Error count for operations
    /// </summary>
    [Column("error_count")]
    public long? ErrorCount { get; set; }

    /// <summary>
    /// Queue depth/backlog
    /// </summary>
    [Column("queue_depth")]
    public long? QueueDepth { get; set; }

    /// <summary>
    /// Component or service identifier
    /// </summary>
    [Column("component")]
    [MaxLength(50)]
    public string? Component { get; set; }

    /// <summary>
    /// Associated symbol (for symbol-specific metrics)
    /// </summary>
    [Column("symbol")]
    [MaxLength(10)]
    public string? Symbol { get; set; }

    /// <summary>
    /// Associated venue (for venue-specific metrics)
    /// </summary>
    [Column("venue")]
    [MaxLength(10)]
    public string? Venue { get; set; }

    /// <summary>
    /// Additional metric data as JSON
    /// </summary>
    [Column("metric_data", TypeName = "jsonb")]
    public string? MetricData { get; set; }

    /// <summary>
    /// Severity level (INFO, WARN, ERROR)
    /// </summary>
    [Column("severity")]
    [MaxLength(10)]
    public string? Severity { get; set; }

    /// <summary>
    /// Record insertion timestamp
    /// </summary>
    [Column("inserted_at")]
    public DateTime InsertedAt { get; set; } = DateTime.UtcNow;
}