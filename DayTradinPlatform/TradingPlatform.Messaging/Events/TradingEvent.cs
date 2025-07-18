using System;

namespace TradingPlatform.Messaging.Events;

/// <summary>
/// Base class for all trading system events with microsecond precision timing
/// Implements event sourcing pattern for complete audit trail
/// </summary>
public abstract record TradingEvent
{
    /// <summary>
    /// Unique event identifier for tracking and correlation
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Microsecond precision timestamp for latency measurement
    /// Critical for order-to-wire timing validation (< 100μs target)
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Event type for routing and processing optimization
    /// </summary>
    public abstract string EventType { get; }

    /// <summary>
    /// Source service that generated the event
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracking related events across services
    /// Essential for distributed tracing and debugging
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Event version for schema evolution support
    /// </summary>
    public int Version { get; init; } = 1;
}

/// <summary>
/// Market data event for real-time price updates
/// High-frequency event requiring sub-millisecond processing
/// </summary>
public record MarketDataEvent : TradingEvent
{
    public override string EventType => "MarketData";

    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public decimal Bid { get; init; }
    public decimal Ask { get; init; }
    public DateTimeOffset MarketTimestamp { get; init; }
    public string Exchange { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
}

/// <summary>
/// Order execution event for paper trading simulation
/// Critical path event requiring immediate processing
/// </summary>
public record OrderEvent : TradingEvent
{
    public override string EventType => "Order";

    public string OrderId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty; // Market, Limit, Stop
    public string Side { get; init; } = string.Empty; // Buy, Sell
    public decimal Quantity { get; init; }
    public decimal? Price { get; init; }
    public string Status { get; init; } = string.Empty; // New, Filled, Cancelled, Rejected
    public DateTimeOffset ExecutionTime { get; init; }
}

/// <summary>
/// Alert event for real-time notifications
/// Target delivery latency < 500ms per EDD requirements
/// </summary>
public record AlertEvent : TradingEvent
{
    public override string EventType => "Alert";

    public string AlertType { get; init; } = string.Empty; // Price, Volume, News, Risk
    public string Symbol { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty; // Info, Warning, Critical
    public bool RequiresAcknowledgment { get; init; }
}

/// <summary>
/// Risk management event for position monitoring
/// Real-time risk calculation and limit enforcement
/// </summary>
public record RiskEvent : TradingEvent
{
    public override string EventType => "Risk";

    public string RiskType { get; init; } = string.Empty; // Position, Drawdown, PDT
    public string Symbol { get; init; } = string.Empty;
    public decimal CurrentExposure { get; init; }
    public decimal RiskLimit { get; init; }
    public decimal UtilizationPercent { get; init; }
    public bool LimitBreached { get; init; }
    public string Action { get; init; } = string.Empty; // Monitor, Warn, Block
}

/// <summary>
/// Strategy signal event for automated trading decisions
/// Generated by rule-based engine following 12 Golden Rules
/// </summary>
public record StrategyEvent : TradingEvent
{
    public override string EventType => "Strategy";

    public string StrategyName { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Signal { get; init; } = string.Empty; // Buy, Sell, Hold
    public decimal Confidence { get; init; }
    public string[] TriggeredRules { get; init; } = Array.Empty<string>();
    public decimal SuggestedPrice { get; init; }
    public decimal SuggestedQuantity { get; init; }
}

/// <summary>
/// Market data request event for on-demand data fetching
/// </summary>
public record MarketDataRequestEvent : TradingEvent
{
    public override string EventType => "MarketDataRequest";

    public string Symbol { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty; // RealTime, Historical
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string Interval { get; init; } = string.Empty;
    public string RequestId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Market data subscription event for managing real-time feeds
/// </summary>
public record MarketDataSubscriptionEvent : TradingEvent
{
    public override string EventType => "MarketDataSubscription";

    public string[] Symbols { get; init; } = Array.Empty<string>();
    public string Action { get; init; } = string.Empty; // Subscribe, Unsubscribe
}