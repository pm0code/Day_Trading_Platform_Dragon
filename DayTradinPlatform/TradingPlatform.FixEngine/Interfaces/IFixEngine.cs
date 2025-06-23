using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;

namespace TradingPlatform.FixEngine.Interfaces;

/// <summary>
/// Core interface for high-performance FIX engine operations
/// Focused on US equity markets with sub-millisecond execution targets
/// </summary>
public interface IFixEngine : IDisposable
{
    /// <summary>
    /// Initializes FIX engine with US market venue configurations
    /// </summary>
    Task<bool> InitializeAsync(FixEngineConfig config);

    /// <summary>
    /// Submits new order to optimal US venue with smart routing
    /// </summary>
    Task<string> SubmitOrderAsync(OrderRequest request);

    /// <summary>
    /// Cancels existing order across US venues
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, string symbol);

    /// <summary>
    /// Modifies existing order (cancel/replace)
    /// </summary>
    Task<bool> ModifyOrderAsync(string orderId, OrderRequest newRequest);

    /// <summary>
    /// Requests market data subscription for US symbols
    /// </summary>
    Task<bool> SubscribeMarketDataAsync(string[] symbols, MarketDataType dataType);

    /// <summary>
    /// Unsubscribes from market data
    /// </summary>
    Task<bool> UnsubscribeMarketDataAsync(string[] symbols);

    /// <summary>
    /// Gets current session status for all US venues
    /// </summary>
    Dictionary<string, bool> GetVenueStatuses();

    /// <summary>
    /// Event fired when order execution report is received
    /// </summary>
    event EventHandler<ExecutionReport> ExecutionReceived;

    /// <summary>
    /// Event fired when market data is received
    /// </summary>
    event EventHandler<MarketDataSnapshot> MarketDataReceived;

    /// <summary>
    /// Event fired when venue connection status changes
    /// </summary>
    event EventHandler<VenueStatusUpdate> VenueStatusChanged;
}

/// <summary>
/// Configuration for FIX engine targeting US equity markets
/// </summary>
public record FixEngineConfig
{
    public required string SenderCompId { get; init; }
    public required Dictionary<string, VenueConnectionConfig> VenueConfigs { get; init; }
    public int HeartbeatInterval { get; init; } = 30;
    public int ReconnectAttempts { get; init; } = 3;
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
    public bool EnableLatencyMonitoring { get; init; } = true;
    public bool EnablePerformanceOptimizations { get; init; } = true;
}

public record VenueConnectionConfig
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string TargetCompId { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool EnableTls { get; init; } = true;
    public int Priority { get; init; } = 1; // 1 = highest priority for routing
}

/// <summary>
/// Market data types supported for US equity markets
/// </summary>
public enum MarketDataType
{
    Level1,      // Best bid/offer
    Level2,      // Market depth
    Trades,      // Time and sales
    NBBO,        // National Best Bid Offer
    Imbalances   // Opening/closing imbalances
}

/// <summary>
/// Execution report for order lifecycle tracking
/// </summary>
public record ExecutionReport
{
    public required string OrderId { get; init; }
    public required string Symbol { get; init; }
    public required string Side { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal Price { get; init; }
    public required decimal FilledQuantity { get; init; }
    public required decimal AvgFillPrice { get; init; }
    public required string OrderStatus { get; init; } // NEW/PARTIALLY_FILLED/FILLED/CANCELED/REJECTED
    public required string ExecType { get; init; }
    public required DateTime TransactionTime { get; init; }
    public required string Venue { get; init; }
    public string? RejectReason { get; init; }
    public long LatencyNanoseconds { get; init; }
}

/// <summary>
/// Market data snapshot for US equity symbols
/// </summary>
public record MarketDataSnapshot
{
    public required string Symbol { get; init; }
    public required DateTime Timestamp { get; init; }
    public decimal? BidPrice { get; init; }
    public decimal? BidSize { get; init; }
    public decimal? AskPrice { get; init; }
    public decimal? AskSize { get; init; }
    public decimal? LastPrice { get; init; }
    public decimal? LastSize { get; init; }
    public decimal? Volume { get; init; }
    public string? Venue { get; init; }
    public MarketDataType DataType { get; init; }
}

/// <summary>
/// Venue status update for monitoring US market connectivity
/// </summary>
public record VenueStatusUpdate
{
    public required string Venue { get; init; }
    public required bool IsConnected { get; init; }
    public required DateTime Timestamp { get; init; }
    public string? StatusMessage { get; init; }
    public long? LatencyMicroseconds { get; init; }
}