using System.Net.WebSockets;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Central orchestration service for on-premise trading workstation
/// Coordinates communication between local microservices via Redis Streams
/// All operations use TradingResult pattern for consistent error handling
/// </summary>
public interface IGatewayOrchestrator
{
    // Market Data Operations
    Task<TradingResult<MarketData?>> GetMarketDataAsync(string symbol);
    Task<TradingResult<bool>> SubscribeToMarketDataAsync(string[] symbols);
    Task<TradingResult<bool>> UnsubscribeFromMarketDataAsync(string[] symbols);

    // Order Management Operations
    Task<TradingResult<OrderResponse>> SubmitOrderAsync(OrderRequest request);
    Task<TradingResult<OrderStatus?>> GetOrderStatusAsync(string orderId);
    Task<TradingResult<OrderResponse>> CancelOrderAsync(string orderId);

    // Strategy Management Operations
    Task<TradingResult<StrategyInfo[]>> GetActiveStrategiesAsync();
    Task<TradingResult<bool>> StartStrategyAsync(string strategyId);
    Task<TradingResult<bool>> StopStrategyAsync(string strategyId);
    Task<TradingResult<StrategyPerformance>> GetStrategyPerformanceAsync(string strategyId);

    // Risk Management Operations
    Task<TradingResult<RiskStatus>> GetRiskStatusAsync();
    Task<TradingResult<RiskLimits>> GetRiskLimitsAsync();
    Task<TradingResult<bool>> UpdateRiskLimitsAsync(RiskLimits limits);

    // Performance Monitoring
    Task<TradingResult<PerformanceMetrics>> GetPerformanceMetricsAsync();
    Task<TradingResult<LatencyMetrics>> GetLatencyMetricsAsync();

    // Real-time WebSocket Communication
    Task<TradingResult<bool>> HandleWebSocketConnectionAsync(WebSocket webSocket);

    // Health and Diagnostics
    Task<TradingResult<SystemHealth>> GetSystemHealthAsync();
}

// Response DTOs
public record OrderResponse(string OrderId, string Status, string Message, DateTimeOffset Timestamp);

public record OrderStatus(
    string OrderId,
    string Symbol,
    string OrderType,
    string Side,
    decimal Quantity,
    decimal? Price,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExecutedAt,
    decimal? ExecutedPrice,
    decimal ExecutedQuantity);

public record StrategyInfo(
    string StrategyId,
    string Name,
    string Status,
    DateTimeOffset StartTime,
    decimal PnL,
    int TradeCount);

public record StrategyPerformance(
    string StrategyId,
    decimal TotalPnL,
    decimal DailyPnL,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    decimal WinRate,
    decimal SharpeRatio,
    decimal MaxDrawdown);

public record RiskStatus(
    decimal TotalExposure,
    decimal DailyPnL,
    decimal MaxDailyLoss,
    decimal RemainingDayTrades,
    bool PDTRestricted,
    string[] ActiveAlerts);

public record RiskLimits(
    decimal MaxPositionSize,
    decimal MaxDailyLoss,
    decimal MaxTotalExposure,
    int MaxDayTrades,
    decimal StopLossPercentage);

public record PerformanceMetrics(
    TimeSpan AverageOrderLatency,
    TimeSpan AverageMarketDataLatency,
    long OrdersPerSecond,
    long MarketDataUpdatesPerSecond,
    double CpuUsagePercent,
    double MemoryUsageMB,
    int ActiveConnections);

public record LatencyMetrics(
    TimeSpan OrderToWireLatency,
    TimeSpan MarketDataProcessingLatency,
    TimeSpan StrategyExecutionLatency,
    TimeSpan RiskCheckLatency,
    DateTimeOffset LastMeasurement);

public record SystemHealth(
    bool IsHealthy,
    string[] Services,
    Dictionary<string, bool> ServiceHealth,
    TimeSpan Uptime,
    string Version);