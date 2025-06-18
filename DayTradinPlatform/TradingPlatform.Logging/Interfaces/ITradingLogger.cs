using System.Diagnostics;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.Logging.Interfaces;

/// <summary>
/// Enterprise-grade trading-specific logger with comprehensive debugging and error tracing
/// CRITICAL: Every trading operation, method, class, and data movement MUST be logged with full context
/// All logs saved to /logs directory with timestamps and complete traceability for debugging
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
    
    // COMPREHENSIVE DEBUGGING AND ERROR TRACING - NEW METHODS
    void LogClassInstantiation(string className, object? constructorParams = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogVariableChange(string variableName, object? oldValue, object? newValue, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0);
    void LogDataMovement(string source, string destination, object? data, string operation, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogDatabaseOperation(string operation, string table, object? parameters, TimeSpan? duration = null, bool success = true, string? errorMessage = null);
    void LogApiCall(string endpoint, string method, object? request, object? response, TimeSpan? duration = null, int? statusCode = null, string? errorMessage = null);
    void LogCacheOperation(string operation, string key, bool hit, TimeSpan? duration = null, object? data = null);
    void LogThreadOperation(string operation, int threadId, string threadName, object? context = null);
    void LogMemoryUsage(string component, long bytesUsed, long bytesAllocated, string? details = null);
    void LogFileOperation(string operation, string filePath, long? fileSize = null, bool success = true, string? errorMessage = null);
    void LogNetworkOperation(string operation, string endpoint, long? bytesTransferred = null, TimeSpan? latency = null, bool success = true, string? errorMessage = null);
    void LogSecurityEvent(string eventType, string details, string? userId = null, string? ipAddress = null, string? riskLevel = null);
    void LogExceptionDetails(Exception exception, string context, Dictionary<string, object>? additionalData = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0);
    void LogStackTrace(string reason, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogSystemResource(string resourceType, double currentValue, double maxValue, string unit, string? threshold = null);
    void LogApplicationEvent(string eventType, string description, object? metadata = null);
    void LogUserAction(string userId, string action, object? parameters = null, string? sessionId = null);
    void LogComponentLifecycle(string component, string state, string? reason = null, object? configuration = null);
    void LogMessageQueueOperation(string operation, string queue, string? messageId = null, object? message = null, bool success = true, string? errorMessage = null);
    void LogScheduledTask(string taskName, DateTime scheduledTime, DateTime? actualTime = null, bool success = true, TimeSpan? duration = null, string? errorMessage = null);
    void LogValidation(string validationType, object? input, bool passed, string[]? errors = null, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "");
    void LogConfigurationChange(string component, string setting, object? oldValue, object? newValue, string? changedBy = null);
    void LogAlert(string alertType, string severity, string message, object? context = null, string[]? recommendedActions = null);
    
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