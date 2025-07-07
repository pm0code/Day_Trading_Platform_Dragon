namespace TradingPlatform.PaperTrading.Models;

/// <summary>
/// Comprehensive metrics for paper trading service performance monitoring
/// </summary>
public class PaperTradingMetrics
{
    /// <summary>
    /// Total number of orders submitted to the paper trading system
    /// </summary>
    public long TotalOrdersSubmitted { get; init; }
    
    /// <summary>
    /// Total number of orders that have been fully filled
    /// </summary>
    public long TotalOrdersFilled { get; init; }
    
    /// <summary>
    /// Total number of orders that have been cancelled
    /// </summary>
    public long TotalOrdersCancelled { get; init; }
    
    /// <summary>
    /// Number of currently active orders (new or partially filled)
    /// </summary>
    public int ActiveOrders { get; init; }
    
    /// <summary>
    /// Number of orders pending execution in the queue
    /// </summary>
    public int PendingOrders { get; init; }
    
    /// <summary>
    /// Timestamp when these metrics were captured
    /// </summary>
    public DateTime Timestamp { get; init; }
}