using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingPlatform.Database.Models;

/// <summary>
/// Time-series market data record optimized for TimescaleDB storage
/// Designed for microsecond-precision market data with high-frequency inserts
/// </summary>
[Table("market_data")]
public class MarketDataRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// Hypertable partition key - optimized for time-series queries
    /// </summary>
    [Column("timestamp")]
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Hardware timestamp in nanoseconds for ultra-precise latency measurement
    /// </summary>
    [Column("hardware_timestamp_ns")]
    public long HardwareTimestampNs { get; set; }

    /// <summary>
    /// Stock symbol (US equity markets)
    /// </summary>
    [Column("symbol")]
    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Market venue (NYSE, NASDAQ, BATS, IEX, etc.)
    /// </summary>
    [Column("venue")]
    [Required]
    [MaxLength(10)]
    public string Venue { get; set; } = string.Empty;

    /// <summary>
    /// Market Identifier Code
    /// </summary>
    [Column("mic")]
    [MaxLength(10)]
    public string? Mic { get; set; }

    /// <summary>
    /// Bid price with financial precision
    /// </summary>
    [Column("bid_price", TypeName = "decimal(18,8)")]
    public decimal? BidPrice { get; set; }

    /// <summary>
    /// Bid size (shares)
    /// </summary>
    [Column("bid_size")]
    public long? BidSize { get; set; }

    /// <summary>
    /// Ask price with financial precision
    /// </summary>
    [Column("ask_price", TypeName = "decimal(18,8)")]
    public decimal? AskPrice { get; set; }

    /// <summary>
    /// Ask size (shares)
    /// </summary>
    [Column("ask_size")]
    public long? AskSize { get; set; }

    /// <summary>
    /// Last trade price with financial precision
    /// </summary>
    [Column("last_price", TypeName = "decimal(18,8)")]
    public decimal? LastPrice { get; set; }

    /// <summary>
    /// Last trade size (shares)
    /// </summary>
    [Column("last_size")]
    public long? LastSize { get; set; }

    /// <summary>
    /// Cumulative daily volume
    /// </summary>
    [Column("volume")]
    public long? Volume { get; set; }

    /// <summary>
    /// Volume-weighted average price
    /// </summary>
    [Column("vwap", TypeName = "decimal(18,8)")]
    public decimal? Vwap { get; set; }

    /// <summary>
    /// Market data type (L1, L2, Trades, NBBO)
    /// </summary>
    [Column("data_type")]
    [Required]
    [MaxLength(20)]
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Sequence number for ordering within the same timestamp
    /// </summary>
    [Column("sequence_number")]
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Market condition flags (halt, opening, closing, etc.)
    /// </summary>
    [Column("market_conditions")]
    [MaxLength(50)]
    public string? MarketConditions { get; set; }

    /// <summary>
    /// Processing latency in nanoseconds (from receipt to storage)
    /// </summary>
    [Column("processing_latency_ns")]
    public long? ProcessingLatencyNs { get; set; }

    /// <summary>
    /// Record insertion timestamp for audit purposes
    /// </summary>
    [Column("inserted_at")]
    public DateTime InsertedAt { get; set; } = DateTime.UtcNow;
}