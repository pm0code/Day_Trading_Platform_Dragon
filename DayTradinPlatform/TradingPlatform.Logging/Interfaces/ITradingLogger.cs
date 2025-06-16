using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Logging.Interfaces;

/// <summary>
/// Enterprise-grade trading-specific logger with performance tracking and structured data
/// Every trading operation must be logged with correlation IDs, timing, and context
/// </summary>
public interface ITradingLogger : ILogger
{
    // Trading Operation Logging
    void LogOrderSubmission(string orderId, string symbol, decimal quantity, decimal price, string side, string correlationId);
    void LogOrderExecution(string orderId, string symbol, decimal executedQuantity, decimal executedPrice, TimeSpan latency, string correlationId);
    void LogOrderRejection(string orderId, string symbol, string reason, string correlationId);
    void LogOrderCancellation(string orderId, string symbol, string reason, string correlationId);
    
    // Market Data Logging
    void LogMarketDataReceived(string symbol, decimal price, long volume, DateTime timestamp, TimeSpan latency);
    void LogMarketDataCacheHit(string symbol, TimeSpan retrievalTime);
    void LogMarketDataCacheMiss(string symbol, TimeSpan retrievalTime);
    void LogMarketDataProviderLatency(string provider, string symbol, TimeSpan latency);
    
    // Strategy Execution Logging
    void LogStrategySignal(string strategyName, string symbol, string signal, decimal confidence, string reason, string correlationId);
    void LogStrategyExecution(string strategyName, string symbol, TimeSpan executionTime, bool success, string correlationId);
    void LogStrategyPerformance(string strategyName, decimal pnl, decimal sharpeRatio, int tradesCount, string correlationId);
    
    // Risk Management Logging
    void LogRiskCheck(string riskType, string symbol, decimal value, decimal limit, bool passed, string correlationId);
    void LogRiskAlert(string alertType, string symbol, decimal currentValue, decimal threshold, string severity, string correlationId);
    void LogComplianceCheck(string complianceType, string result, string details, string correlationId);
    
    // Performance Logging
    void LogMethodEntry(string methodName, object? parameters = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogMethodExit(string methodName, TimeSpan duration, object? result = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, object>? tags = null);
    void LogLatencyViolation(string operation, TimeSpan actualLatency, TimeSpan expectedLatency, string correlationId);
    
    // System Health Logging
    void LogSystemMetric(string metricName, double value, string unit);
    void LogHealthCheck(string serviceName, bool healthy, TimeSpan responseTime, string? details = null);
    void LogResourceUsage(string resource, double usage, double capacity, string unit);
    
    // Error and Exception Logging
    void LogTradingError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null);
    void LogCriticalError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null);
    void LogBusinessRuleViolation(string rule, string details, string? correlationId = null);
    
    // Debug and Diagnostic Logging
    void LogDebugTrace(string message, Dictionary<string, object>? context = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogStateTransition(string entity, string fromState, string toState, string reason, string? correlationId = null);
    void LogConfiguration(string component, Dictionary<string, object> configuration);
    
    // Correlation and Context
    IDisposable BeginScope(string operationName, string? correlationId = null);
    IDisposable BeginScope(Dictionary<string, object> properties);
    string GenerateCorrelationId();
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// Performance-aware logger wrapper for high-frequency trading operations
/// Automatically measures and logs method execution times
/// </summary>
public interface IPerformanceLogger
{
    IDisposable MeasureOperation(string operationName, string? correlationId = null);
    void LogOperationComplete(string operationName, TimeSpan duration, bool success, string? correlationId = null);
    void LogThroughput(string operation, int itemsProcessed, TimeSpan duration, string? correlationId = null);
    void LogLatencyPercentile(string operation, TimeSpan p50, TimeSpan p95, TimeSpan p99, string? correlationId = null);
}

/// <summary>
/// Trading-specific structured logging context
/// Ensures all trading operations include required metadata
/// </summary>
public record TradingLogContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public string? Symbol { get; init; }
    public string? OrderId { get; init; }
    public string? StrategyName { get; init; }
    public string? AccountId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? Environment { get; init; }
    public string? Version { get; init; }
    public Dictionary<string, object>? AdditionalProperties { get; init; }
}

/// <summary>
/// Performance metrics for ultra-low latency trading
/// All operations must track timing and throughput
/// </summary>
public record PerformanceMetrics
{
    public string OperationName { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? ItemsProcessed { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}