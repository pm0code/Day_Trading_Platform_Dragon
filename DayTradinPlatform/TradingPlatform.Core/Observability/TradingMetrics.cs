// File: TradingPlatform.Core\Observability\TradingMetrics.cs

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Prometheus;

namespace TradingPlatform.Core.Observability;

/// <summary>
/// Comprehensive trading metrics collection using both OpenTelemetry and Prometheus
/// Provides microsecond-precision metrics for ultra-low latency trading operations
/// </summary>
public class TradingMetrics : ITradingMetrics, IDisposable
{
    private readonly Meter _meter;

    // OpenTelemetry Instruments
    private readonly Counter<long> _ordersProcessedCounter;
    private readonly Histogram<double> _orderLatencyHistogram;
    private readonly Counter<long> _marketDataTicksCounter;
    private readonly Histogram<double> _marketDataLatencyHistogram;
    private readonly Counter<long> _riskViolationsCounter;
    private readonly Histogram<double> _fixMessageLatencyHistogram;
    private readonly UpDownCounter<long> _activePositionsGauge;
    private readonly Histogram<double> _tradingPnlHistogram;

    // Prometheus Metrics (for Grafana integration)
    private static readonly Counter PrometheusOrdersProcessed = Metrics
        .CreateCounter("trading_orders_processed_total", "Total number of orders processed", new[] { "symbol", "side", "venue" });

    private static readonly Histogram PrometheusOrderLatency = Metrics
        .CreateHistogram("trading_order_latency_microseconds", "Order execution latency in microseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 10.0, 25.0, 50.0, 75.0, 100.0, 150.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0, 10000.0 }
            });

    private static readonly Counter PrometheusMarketDataTicks = Metrics
        .CreateCounter("trading_market_data_ticks_total", "Total market data ticks processed", new[] { "symbol", "data_type" });

    private static readonly Histogram PrometheusMarketDataLatency = Metrics
        .CreateHistogram("trading_market_data_latency_microseconds", "Market data processing latency in microseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 1.0, 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0, 2500.0 }
            });

    private static readonly Counter PrometheusRiskViolations = Metrics
        .CreateCounter("trading_risk_violations_total", "Total risk violations detected", new[] { "violation_type", "severity", "account" });

    private static readonly Histogram PrometheusFixMessageLatency = Metrics
        .CreateHistogram("trading_fix_message_latency_microseconds", "FIX message processing latency in microseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0 }
            });

    private static readonly Gauge PrometheusActivePositions = Metrics
        .CreateGauge("trading_active_positions", "Number of active trading positions", new[] { "account", "symbol" });

    private static readonly Histogram PrometheusTradingPnl = Metrics
        .CreateHistogram("trading_pnl_dollars", "Trading profit and loss in dollars",
            new HistogramConfiguration
            {
                Buckets = new[] { -10000.0, -5000.0, -1000.0, -500.0, -100.0, -50.0, -10.0, 0.0, 10.0, 50.0, 100.0, 500.0, 1000.0, 5000.0, 10000.0 }
            });

    public TradingMetrics()
    {
        _meter = new Meter("TradingPlatform.Trading", "1.0.0");

        // Initialize OpenTelemetry instruments
        _ordersProcessedCounter = _meter.CreateCounter<long>(
            "trading.orders.processed",
            "orders",
            "Total number of orders processed");

        _orderLatencyHistogram = _meter.CreateHistogram<double>(
            "trading.orders.latency",
            "microseconds",
            "Order execution latency in microseconds");

        _marketDataTicksCounter = _meter.CreateCounter<long>(
            "trading.market_data.ticks",
            "ticks",
            "Total market data ticks processed");

        _marketDataLatencyHistogram = _meter.CreateHistogram<double>(
            "trading.market_data.latency",
            "microseconds",
            "Market data processing latency in microseconds");

        _riskViolationsCounter = _meter.CreateCounter<long>(
            "trading.risk.violations",
            "violations",
            "Total risk violations detected");

        _fixMessageLatencyHistogram = _meter.CreateHistogram<double>(
            "trading.fix.message_latency",
            "microseconds",
            "FIX message processing latency in microseconds");

        _activePositionsGauge = _meter.CreateUpDownCounter<long>(
            "trading.positions.active",
            "positions",
            "Number of active trading positions");

        _tradingPnlHistogram = _meter.CreateHistogram<double>(
            "trading.pnl.realized",
            "dollars",
            "Trading profit and loss in dollars");
    }

    /// <summary>
    /// Records order execution metrics with comprehensive context
    /// </summary>
    public void RecordOrderExecution(TimeSpan latency, string symbol, decimal quantity)
    {
        var latencyMicroseconds = latency.TotalMicroseconds;

        // OpenTelemetry metrics
        _ordersProcessedCounter.Add(1, new KeyValuePair<string, object?>("symbol", symbol));
        _orderLatencyHistogram.Record(latencyMicroseconds,
            new KeyValuePair<string, object?>("symbol", symbol),
            new KeyValuePair<string, object?>("quantity_range", GetQuantityRange(quantity)));

        // Prometheus metrics (for Grafana)
        PrometheusOrdersProcessed.WithLabels(symbol, GetSide(quantity), "AUTO").Inc();
        PrometheusOrderLatency.Observe(latencyMicroseconds);

        // Create detailed activity for tracing
        using var activity = OpenTelemetryInstrumentation.TradingActivitySource.StartActivity("OrderExecution");
        activity?.SetTag("trading.order.symbol", symbol);
        activity?.SetTag("trading.order.quantity", quantity.ToString());
        activity?.SetTag("trading.order.latency_microseconds", latencyMicroseconds.ToString("F2"));
        activity?.SetTag("trading.order.timestamp", DateTimeOffset.UtcNow.ToString("O"));

        // Flag latency violations
        if (latencyMicroseconds > 100)
        {
            activity?.SetTag("trading.latency.violation", true);
            activity?.SetTag("trading.latency.target", "100μs");
            activity?.SetTag("trading.latency.actual", $"{latencyMicroseconds:F2}μs");
        }
    }

    /// <summary>
    /// Records market data tick processing metrics
    /// </summary>
    public void RecordMarketDataTick(string symbol, TimeSpan processingLatency)
    {
        var latencyMicroseconds = processingLatency.TotalMicroseconds;

        // OpenTelemetry metrics
        _marketDataTicksCounter.Add(1, new KeyValuePair<string, object?>("symbol", symbol));
        _marketDataLatencyHistogram.Record(latencyMicroseconds,
            new KeyValuePair<string, object?>("symbol", symbol),
            new KeyValuePair<string, object?>("data_type", "level1"));

        // Prometheus metrics
        PrometheusMarketDataTicks.WithLabels(symbol, "level1").Inc();
        PrometheusMarketDataLatency.Observe(latencyMicroseconds);

        // Create activity for market data processing
        using var activity = OpenTelemetryInstrumentation.MarketDataActivitySource.StartActivity("MarketDataTick");
        activity?.SetTag("trading.market_data.symbol", symbol);
        activity?.SetTag("trading.market_data.latency_microseconds", latencyMicroseconds.ToString("F2"));
        activity?.SetTag("trading.market_data.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Records risk violation events with severity classification
    /// </summary>
    public void RecordRiskViolation(string violationType, string severity)
    {
        // OpenTelemetry metrics
        _riskViolationsCounter.Add(1,
            new KeyValuePair<string, object?>("violation_type", violationType),
            new KeyValuePair<string, object?>("severity", severity));

        // Prometheus metrics
        PrometheusRiskViolations.WithLabels(violationType, severity, "default").Inc();

        // Create critical activity for risk violations
        using var activity = OpenTelemetryInstrumentation.RiskActivitySource.StartActivity("RiskViolation");
        activity?.SetTag("trading.risk.violation_type", violationType);
        activity?.SetTag("trading.risk.severity", severity);
        activity?.SetTag("trading.risk.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        activity?.SetTag("trading.risk.requires_action", severity == "critical");

        // Mark as error for critical violations
        if (severity == "critical")
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"Critical risk violation: {violationType}");
        }
    }

    /// <summary>
    /// Records FIX message processing performance
    /// </summary>
    public void RecordFixMessageProcessing(string messageType, TimeSpan processingTime)
    {
        var latencyMicroseconds = processingTime.TotalMicroseconds;

        // OpenTelemetry metrics
        _fixMessageLatencyHistogram.Record(latencyMicroseconds,
            new KeyValuePair<string, object?>("message_type", messageType));

        // Prometheus metrics
        PrometheusFixMessageLatency.Observe(latencyMicroseconds);

        // Create FIX engine activity
        using var activity = OpenTelemetryInstrumentation.FixEngineActivitySource.StartActivity("FixMessageProcessing");
        activity?.SetTag("trading.fix.message_type", messageType);
        activity?.SetTag("trading.fix.latency_microseconds", latencyMicroseconds.ToString("F2"));
        activity?.SetTag("trading.fix.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Updates active positions count
    /// </summary>
    public void UpdateActivePositions(string account, string symbol, long positionChange)
    {
        _activePositionsGauge.Add(positionChange,
            new KeyValuePair<string, object?>("account", account),
            new KeyValuePair<string, object?>("symbol", symbol));

        PrometheusActivePositions.WithLabels(account, symbol).Set(positionChange);
    }

    /// <summary>
    /// Records realized P&L from trading operations
    /// </summary>
    public void RecordRealizedPnL(decimal pnlAmount, string symbol, string account)
    {
        var pnlDouble = (double)pnlAmount;

        _tradingPnlHistogram.Record(pnlDouble,
            new KeyValuePair<string, object?>("symbol", symbol),
            new KeyValuePair<string, object?>("account", account));

        PrometheusTradingPnl.Observe(pnlDouble);

        // Create P&L activity
        using var activity = OpenTelemetryInstrumentation.TradingActivitySource.StartActivity("RealizedPnL");
        activity?.SetTag("trading.pnl.amount", pnlAmount.ToString());
        activity?.SetTag("trading.pnl.symbol", symbol);
        activity?.SetTag("trading.pnl.account", account);
        activity?.SetTag("trading.pnl.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    private static string GetQuantityRange(decimal quantity)
    {
        return quantity switch
        {
            < 100 => "small",
            < 1000 => "medium",
            < 10000 => "large",
            _ => "institutional"
        };
    }

    private static string GetSide(decimal quantity) => quantity > 0 ? "buy" : "sell";

    public void Dispose()
    {
        _meter?.Dispose();
    }
}