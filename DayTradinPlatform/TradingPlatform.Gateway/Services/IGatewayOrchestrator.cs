using System.Net.WebSockets;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Central orchestration service for on-premise trading workstation
/// Coordinates communication between local microservices via Redis Streams
/// </summary>
public interface IGatewayOrchestrator
{
    // Market Data Operations
    Task<MarketData?> GetMarketDataAsync(string symbol);
    Task SubscribeToMarketDataAsync(string[] symbols);
    Task UnsubscribeFromMarketDataAsync(string[] symbols);

    // Order Management Operations
    Task<OrderResponse> SubmitOrderAsync(OrderRequest request);
    Task<OrderStatus?> GetOrderStatusAsync(string orderId);
    Task<OrderResponse> CancelOrderAsync(string orderId);

    // Strategy Management Operations
    Task<StrategyInfo[]> GetActiveStrategiesAsync();
    Task StartStrategyAsync(string strategyId);
    Task StopStrategyAsync(string strategyId);
    Task<StrategyPerformance> GetStrategyPerformanceAsync(string strategyId);

    // Risk Management Operations
    Task<RiskStatus> GetRiskStatusAsync();
    Task<RiskLimits> GetRiskLimitsAsync();
    Task UpdateRiskLimitsAsync(RiskLimits limits);

    // Performance Monitoring
    Task<PerformanceMetrics> GetPerformanceMetricsAsync();
    Task<LatencyMetrics> GetLatencyMetricsAsync();

    // Real-time WebSocket Communication
    Task HandleWebSocketConnectionAsync(WebSocket webSocket);

    // Health and Diagnostics
    Task<SystemHealth> GetSystemHealthAsync();
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