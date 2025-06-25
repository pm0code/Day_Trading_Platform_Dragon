using System;
using System.Collections.Generic;

namespace TradingPlatform.TimeSeries.Models
{
    /// <summary>
    /// Base class for all time-series data points
    /// </summary>
    public abstract class TimeSeriesPoint
    {
        /// <summary>
        /// Timestamp with microsecond precision
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Data source identifier
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Additional tags for filtering and grouping
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Measurement name for InfluxDB
        /// </summary>
        public abstract string Measurement { get; }
    }

    /// <summary>
    /// Market data point for time-series storage
    /// </summary>
    public class MarketDataPoint : TimeSeriesPoint
    {
        public override string Measurement => "market_data";

        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal BidSize { get; set; }
        public decimal AskSize { get; set; }
        public decimal Volume { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal VWAP { get; set; }
        public int TradeCount { get; set; }
        public string DataType { get; set; } = "quote"; // quote, trade, bar
    }

    /// <summary>
    /// Order execution data point
    /// </summary>
    public class OrderExecutionPoint : TimeSeriesPoint
    {
        public override string Measurement => "order_execution";

        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty; // BUY, SELL
        public string OrderType { get; set; } = string.Empty; // MARKET, LIMIT, STOP
        public string Status { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal ExecutedQuantity { get; set; }
        public decimal ExecutedPrice { get; set; }
        public decimal Commission { get; set; }
        public long LatencyMicroseconds { get; set; }
        public string Venue { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Position tracking data point
    /// </summary>
    public class PositionPoint : TimeSeriesPoint
    {
        public override string Measurement => "positions";

        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal MarketValue { get; set; }
        public decimal CostBasis { get; set; }
        public string Account { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Trading signal data point
    /// </summary>
    public class SignalPoint : TimeSeriesPoint
    {
        public override string Measurement => "signals";

        public string SignalId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string SignalType { get; set; } = string.Empty; // BUY, SELL, HOLD
        public decimal Confidence { get; set; }
        public decimal TargetPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public string Strategy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Dictionary<string, decimal> Indicators { get; set; } = new();
    }

    /// <summary>
    /// Risk metrics data point
    /// </summary>
    public class RiskMetricsPoint : TimeSeriesPoint
    {
        public override string Measurement => "risk_metrics";

        public string Portfolio { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
        public decimal DailyPnL { get; set; }
        public decimal DrawdownPercent { get; set; }
        public decimal VaR95 { get; set; }
        public decimal VaR99 { get; set; }
        public decimal ExpectedShortfall { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxPositionSize { get; set; }
        public decimal MarginUsed { get; set; }
        public decimal BuyingPower { get; set; }
        public int ActivePositions { get; set; }
        public Dictionary<string, decimal> RiskBySymbol { get; set; } = new();
    }

    /// <summary>
    /// System performance metrics
    /// </summary>
    public class PerformanceMetricsPoint : TimeSeriesPoint
    {
        public override string Measurement => "system_performance";

        public string Component { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public long LatencyNanoseconds { get; set; }
        public long MemoryBytes { get; set; }
        public double CpuPercent { get; set; }
        public long ThreadCount { get; set; }
        public long GcGen0 { get; set; }
        public long GcGen1 { get; set; }
        public long GcGen2 { get; set; }
        public long MessagesProcessed { get; set; }
        public double Throughput { get; set; }
        public long ErrorCount { get; set; }
        public Dictionary<string, long> CustomMetrics { get; set; } = new();
    }

    /// <summary>
    /// Market depth/Level 2 data point
    /// </summary>
    public class MarketDepthPoint : TimeSeriesPoint
    {
        public override string Measurement => "market_depth";

        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Side { get; set; } = string.Empty; // BID, ASK
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public int OrderCount { get; set; }
        public decimal CumulativeSize { get; set; }
        public decimal ImbalanceRatio { get; set; }
    }

    /// <summary>
    /// Aggregated trading statistics
    /// </summary>
    public class TradingStatsPoint : TimeSeriesPoint
    {
        public override string Measurement => "trading_stats";

        public string Period { get; set; } = string.Empty; // 1min, 5min, 1hour, 1day
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public decimal WinRate { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal TotalCommission { get; set; }
        public Dictionary<string, decimal> PnLByStrategy { get; set; } = new();
    }

    /// <summary>
    /// Alert and event data point
    /// </summary>
    public class AlertPoint : TimeSeriesPoint
    {
        public override string Measurement => "alerts";

        public string AlertId { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // INFO, WARNING, CRITICAL
        public string Symbol { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public Dictionary<string, string> Context { get; set; } = new();
        public bool Acknowledged { get; set; }
        public string AcknowledgedBy { get; set; } = string.Empty;
    }
}