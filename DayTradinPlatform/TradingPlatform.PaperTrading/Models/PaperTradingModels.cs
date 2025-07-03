namespace TradingPlatform.PaperTrading.Models;

public record OrderRequest(
    string Symbol,
    OrderType Type,
    OrderSide Side,
    decimal Quantity,
    decimal? LimitPrice = null,
    decimal? StopPrice = null,
    TimeInForce TimeInForce = TimeInForce.Day,
    string? ClientOrderId = null
);

public record Order(
    string OrderId,
    string Symbol,
    OrderType Type,
    OrderSide Side,
    decimal Quantity,
    decimal? LimitPrice,
    decimal? StopPrice,
    OrderStatus Status,
    TimeInForce TimeInForce,
    decimal FilledQuantity,
    decimal RemainingQuantity,
    decimal AveragePrice,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? ClientOrderId = null
);

public record OrderResult(
    bool IsSuccess,
    string? OrderId,
    string Message,
    OrderStatus Status,
    DateTime Timestamp
);

public record Execution(
    string ExecutionId,
    string OrderId,
    string Symbol,
    OrderSide Side,
    decimal Quantity,
    decimal Price,
    decimal Commission,
    DateTime ExecutionTime,
    string VenueId,
    decimal Slippage
);

public record Position(
    string Symbol,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    DateTime FirstTradeDate,
    DateTime LastUpdated
);

public record Portfolio(
    decimal TotalValue,
    decimal CashBalance,
    decimal TotalEquity,
    decimal DayPnL,
    decimal TotalPnL,
    decimal BuyingPower,
    IEnumerable<Position> Positions,
    DateTime LastUpdated
);

public record PerformanceMetrics(
    decimal TotalReturn,
    decimal DailyReturn,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal WinRate,
    decimal AverageWin,
    decimal AverageLoss,
    decimal ProfitFactor,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

public record ExecutionAnalytics(
    decimal AverageSlippage,
    decimal TotalCommissions,
    TimeSpan AverageExecutionTime,
    decimal FillRate,
    IEnumerable<SlippageMetric> SlippageBySymbol,
    IEnumerable<VenueMetric> VenueMetrics,
    DateTime AnalysisPeriodStart,
    DateTime AnalysisPeriodEnd
);

public record SlippageMetric(
    string Symbol,
    decimal AverageSlippage,
    decimal MaxSlippage,
    decimal MinSlippage,
    int TradeCount
);

public record VenueMetric(
    string VenueId,
    TimeSpan AverageLatency,
    decimal FillRate,
    int OrderCount
);

public record OrderBookLevel(
    decimal Price,
    decimal Size,
    int OrderCount
);

public record OrderBook(
    string Symbol,
    IEnumerable<OrderBookLevel> Bids,
    IEnumerable<OrderBookLevel> Asks,
    DateTime Timestamp
);

public record MarketImpact(
    decimal PriceImpact,
    decimal TemporaryImpact,
    decimal PermanentImpact,
    TimeSpan Duration
);

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit,
    TrailingStop,
    TWAP,     // Time-Weighted Average Price
    VWAP,     // Volume-Weighted Average Price
    Iceberg   // Hidden quantity order
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
    Expired,
    PendingCancel,
    PendingReplace
}

public enum TimeInForce
{
    Day,
    GTC, // Good Till Cancelled
    IOC, // Immediate Or Cancel
    FOK  // Fill Or Kill
}

public enum ExecutionVenue
{
    SimulatedMarket,
    NASDAQ,
    NYSE,
    ARCA,
    BATS,
    IEX
}