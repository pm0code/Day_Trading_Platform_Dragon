using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingPlatform.Database.Models;

/// <summary>
/// Order execution record for regulatory compliance and performance analysis
/// Optimized for TimescaleDB with microsecond-precision timestamps
/// </summary>
[Table("executions")]
public class ExecutionRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
    
    /// <summary>
    /// Execution timestamp - hypertable partition key
    /// </summary>
    [Column("execution_time")]
    [Required]
    public DateTime ExecutionTime { get; set; }
    
    /// <summary>
    /// Hardware timestamp for precise latency measurement
    /// </summary>
    [Column("hardware_timestamp_ns")]
    [Required]
    public long HardwareTimestampNs { get; set; }
    
    /// <summary>
    /// Client order ID from FIX message
    /// </summary>
    [Column("client_order_id")]
    [Required]
    [MaxLength(50)]
    public string ClientOrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Exchange order ID
    /// </summary>
    [Column("exchange_order_id")]
    [MaxLength(50)]
    public string? ExchangeOrderId { get; set; }
    
    /// <summary>
    /// Execution ID from venue
    /// </summary>
    [Column("execution_id")]
    [Required]
    [MaxLength(50)]
    public string ExecutionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Stock symbol
    /// </summary>
    [Column("symbol")]
    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Trading venue
    /// </summary>
    [Column("venue")]
    [Required]
    [MaxLength(10)]
    public string Venue { get; set; } = string.Empty;
    
    /// <summary>
    /// Order side (BUY/SELL)
    /// </summary>
    [Column("side")]
    [Required]
    [MaxLength(4)]
    public string Side { get; set; } = string.Empty;
    
    /// <summary>
    /// Executed quantity
    /// </summary>
    [Column("executed_quantity")]
    [Required]
    public long ExecutedQuantity { get; set; }
    
    /// <summary>
    /// Execution price with financial precision
    /// </summary>
    [Column("execution_price", TypeName = "decimal(18,8)")]
    [Required]
    public decimal ExecutionPrice { get; set; }
    
    /// <summary>
    /// Order type (MARKET, LIMIT, STOP, etc.)
    /// </summary>
    [Column("order_type")]
    [Required]
    [MaxLength(20)]
    public string OrderType { get; set; } = string.Empty;
    
    /// <summary>
    /// Time in force (DAY, IOC, FOK, etc.)
    /// </summary>
    [Column("time_in_force")]
    [MaxLength(10)]
    public string? TimeInForce { get; set; }
    
    /// <summary>
    /// Execution type (NEW, PARTIAL_FILL, FILL, CANCELED, etc.)
    /// </summary>
    [Column("execution_type")]
    [Required]
    [MaxLength(20)]
    public string ExecutionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Order status after execution
    /// </summary>
    [Column("order_status")]
    [Required]
    [MaxLength(20)]
    public string OrderStatus { get; set; } = string.Empty;
    
    /// <summary>
    /// Cumulative executed quantity for this order
    /// </summary>
    [Column("cumulative_quantity")]
    [Required]
    public long CumulativeQuantity { get; set; }
    
    /// <summary>
    /// Remaining quantity to be executed
    /// </summary>
    [Column("remaining_quantity")]
    [Required]
    public long RemainingQuantity { get; set; }
    
    /// <summary>
    /// Average fill price for cumulative executions
    /// </summary>
    [Column("average_price", TypeName = "decimal(18,8)")]
    public decimal? AveragePrice { get; set; }
    
    /// <summary>
    /// Commission/fees for this execution
    /// </summary>
    [Column("commission", TypeName = "decimal(18,8)")]
    public decimal? Commission { get; set; }
    
    /// <summary>
    /// Trading account
    /// </summary>
    [Column("account")]
    [Required]
    [MaxLength(50)]
    public string Account { get; set; } = string.Empty;
    
    /// <summary>
    /// Original order timestamp for latency calculation
    /// </summary>
    [Column("order_timestamp")]
    public DateTime? OrderTimestamp { get; set; }
    
    /// <summary>
    /// Order-to-execution latency in nanoseconds
    /// </summary>
    [Column("execution_latency_ns")]
    public long? ExecutionLatencyNs { get; set; }
    
    /// <summary>
    /// Regulatory transaction identifier
    /// </summary>
    [Column("regulatory_transaction_id")]
    [MaxLength(100)]
    public string? RegulatoryTransactionId { get; set; }
    
    /// <summary>
    /// Liquidity indicator (A=Added, R=Removed, etc.)
    /// </summary>
    [Column("liquidity_indicator")]
    [MaxLength(1)]
    public string? LiquidityIndicator { get; set; }
    
    /// <summary>
    /// Contra firm identifier for regulatory reporting
    /// </summary>
    [Column("contra_firm")]
    [MaxLength(20)]
    public string? ContraFirm { get; set; }
    
    /// <summary>
    /// Additional execution details as JSON
    /// </summary>
    [Column("execution_details", TypeName = "jsonb")]
    public string? ExecutionDetails { get; set; }
    
    /// <summary>
    /// Record insertion timestamp
    /// </summary>
    [Column("inserted_at")]
    public DateTime InsertedAt { get; set; } = DateTime.UtcNow;
}