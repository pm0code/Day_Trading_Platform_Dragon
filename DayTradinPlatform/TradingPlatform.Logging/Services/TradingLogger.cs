// TradingPlatform.Logging.Services.TradingLogger - CANONICAL DELEGATION TO TradingLogOrchestrator
// ZERO Microsoft.Extensions.Logging dependencies - Delegates to unified LogOrchestrator
// ALL LOGGING MUST GO THROUGH TradingLogOrchestrator.Instance

using System.Runtime.CompilerServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Logging.Interfaces;

namespace TradingPlatform.Logging.Services;

/// <summary>
/// CANONICAL LOGGING WRAPPER - Delegates all calls to TradingLogOrchestrator.Instance
/// This ensures ALL logging throughout the platform goes through the single LogOrchestrator
/// CRITICAL: Use TradingLogOrchestrator.Instance directly for best performance
/// </summary>
public class TradingLogger : ITradingOperationsLogger
{
    private readonly TradingLogOrchestrator _orchestrator;
    private readonly string _serviceName;

    public TradingLogger(string serviceName = "TradingPlatform")
    {
        _serviceName = serviceName;
        _orchestrator = TradingLogOrchestrator.Instance;

        // Log the delegation setup
        _orchestrator.LogInfo("TradingLogger initialized - delegating to TradingLogOrchestrator",
            new { ServiceName = serviceName, DelegationType = "Canonical" });
    }

    #region ITradingLogger Interface - Delegated to TradingLogOrchestrator

    public void LogInfo(string message,
                        object? additionalData = null,
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string sourceFilePath = "",
                        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _orchestrator.LogInfo(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogDebug(string message,
                         object? additionalData = null,
                         [CallerMemberName] string memberName = "",
                         [CallerFilePath] string sourceFilePath = "",
                         [CallerLineNumber] int sourceLineNumber = 0)
    {
        _orchestrator.LogDebug(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogWarning(string message,
                           string? impact = null,
                           string? recommendedAction = null,
                           object? additionalData = null,
                           [CallerMemberName] string memberName = "",
                           [CallerFilePath] string sourceFilePath = "",
                           [CallerLineNumber] int sourceLineNumber = 0)
    {
        _orchestrator.LogWarning(message, impact, recommendedAction, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogError(string message,
                         Exception? exception = null,
                         string? operationContext = null,
                         string? userImpact = null,
                         string? troubleshootingHints = null,
                         object? additionalData = null,
                         [CallerMemberName] string memberName = "",
                         [CallerFilePath] string sourceFilePath = "",
                         [CallerLineNumber] int sourceLineNumber = 0)
    {
        _orchestrator.LogError(message, exception, operationContext, userImpact, troubleshootingHints, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogTrade(string symbol,
                         string action,
                         decimal quantity,
                         decimal price,
                         string? orderId = null,
                         string? strategy = null,
                         TimeSpan? executionTime = null,
                         object? marketConditions = null,
                         object? riskMetrics = null,
                         [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogTrade(symbol, action, quantity, price, orderId, strategy, executionTime, marketConditions, riskMetrics, memberName);
    }

    public void LogPositionChange(string symbol,
                                  decimal oldPosition,
                                  decimal newPosition,
                                  string reason,
                                  decimal? pnlImpact = null,
                                  object? riskImpact = null,
                                  [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogPositionChange(symbol, oldPosition, newPosition, reason, pnlImpact, riskImpact, memberName);
    }

    public void LogPerformance(string operation,
                               TimeSpan duration,
                               bool success = true,
                               double? throughput = null,
                               object? resourceUsage = null,
                               object? businessMetrics = null,
                               TimeSpan? comparisonTarget = null,
                               [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogPerformance(operation, duration, success, throughput, resourceUsage, businessMetrics, comparisonTarget, memberName);
    }

    public void LogHealth(string component,
                          string status,
                          object? metrics = null,
                          string[]? alerts = null,
                          string[]? recommendedActions = null,
                          [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogHealth(component, status, metrics, alerts, recommendedActions, memberName);
    }

    public void LogRisk(string riskType,
                        string severity,
                        string description,
                        decimal? currentExposure = null,
                        decimal? riskLimit = null,
                        string[]? mitigationActions = null,
                        string? regulatoryImplications = null,
                        [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogRisk(riskType, severity, description, currentExposure, riskLimit, mitigationActions, regulatoryImplications, memberName);
    }

    public void LogDataPipeline(string pipeline,
                                string stage,
                                int recordsProcessed,
                                object? dataQuality = null,
                                object? latencyMetrics = null,
                                string[]? errors = null,
                                [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogDataPipeline(pipeline, stage, recordsProcessed, dataQuality, latencyMetrics, errors, memberName);
    }

    public void LogMarketData(string symbol,
                              string dataType,
                              string source,
                              TimeSpan? latency = null,
                              string? quality = null,
                              object? volume = null,
                              [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogMarketData(symbol, dataType, source, latency, quality, volume, memberName);
    }

    #endregion

    #region ITradingLogger Interface - Delegated to TradingLogOrchestrator

    public void LogOrderSubmission(string orderId, string symbol, decimal quantity, decimal price, string side, string correlationId)
    {
        _orchestrator.LogTrade(symbol, $"SUBMIT_{side}", quantity, price, orderId, "OrderSubmission", null, null, null);
    }

    public void LogOrderExecution(string orderId, string symbol, decimal executedQuantity, decimal executedPrice, TimeSpan latency, string correlationId)
    {
        _orchestrator.LogTrade(symbol, "EXECUTED", executedQuantity, executedPrice, orderId, "OrderExecution", latency, null, null);
    }

    public void LogOrderRejection(string orderId, string symbol, string reason, string correlationId)
    {
        _orchestrator.LogWarning($"Order rejected: {orderId} for {symbol}",
            impact: "Trading operation failed",
            recommendedAction: "Review order parameters and retry",
            additionalData: new { OrderId = orderId, Symbol = symbol, Reason = reason, CorrelationId = correlationId });
    }

    public void LogOrderCancellation(string orderId, string symbol, string reason, string correlationId)
    {
        _orchestrator.LogInfo($"Order cancelled: {orderId} for {symbol} - {reason}",
            new { OrderId = orderId, Symbol = symbol, Reason = reason, CorrelationId = correlationId });
    }

    public void LogMarketDataReceived(string symbol, decimal price, long volume, DateTime timestamp, TimeSpan latency)
    {
        _orchestrator.LogMarketData(symbol, "PRICE_UPDATE", "Market", latency, "LIVE", new { Price = price, Volume = volume, Timestamp = timestamp });
    }

    public void LogMarketDataCacheHit(string symbol, TimeSpan retrievalTime)
    {
        _orchestrator.LogPerformance($"MarketDataCache.Hit.{symbol}", retrievalTime, true);
    }

    public void LogMarketDataCacheMiss(string symbol, TimeSpan retrievalTime)
    {
        _orchestrator.LogPerformance($"MarketDataCache.Miss.{symbol}", retrievalTime, false);
    }

    public void LogMarketDataProviderLatency(string provider, string symbol, TimeSpan latency)
    {
        _orchestrator.LogMarketData(symbol, "PROVIDER_LATENCY", provider, latency, "MEASURED", null);
    }

    public void LogStrategySignal(string strategyName, string symbol, string signal, decimal confidence, string reason, string correlationId)
    {
        _orchestrator.LogInfo($"Strategy signal: {strategyName} generated {signal} for {symbol}",
            new { StrategyName = strategyName, Symbol = symbol, Signal = signal, Confidence = confidence, Reason = reason, CorrelationId = correlationId });
    }

    public void LogStrategyExecution(string strategyName, string symbol, TimeSpan executionTime, bool success, string correlationId)
    {
        _orchestrator.LogPerformance($"Strategy.{strategyName}.{symbol}", executionTime, success);
    }

    public void LogStrategyPerformance(string strategyName, decimal pnl, decimal sharpeRatio, int tradesCount, string correlationId)
    {
        _orchestrator.LogInfo($"Strategy performance: {strategyName}",
            new { StrategyName = strategyName, PnL = pnl, SharpeRatio = sharpeRatio, TradesCount = tradesCount, CorrelationId = correlationId });
    }

    public void LogRiskCheck(string riskType, string symbol, decimal value, decimal limit, bool passed, string correlationId)
    {
        var severity = passed ? "LOW" : "HIGH";
        _orchestrator.LogRisk(riskType, severity, $"Risk check for {symbol}: {value} vs limit {limit}", value, limit);
    }

    public void LogRiskAlert(string alertType, string symbol, decimal currentValue, decimal threshold, string severity, string correlationId)
    {
        _orchestrator.LogRisk(alertType, severity, $"Risk alert for {symbol}", currentValue, threshold);
    }

    public void LogComplianceCheck(string complianceType, string result, string details, string correlationId)
    {
        var passed = result.ToUpper() == "PASSED";
        if (passed)
        {
            _orchestrator.LogInfo($"Compliance check passed: {complianceType} - {details}");
        }
        else
        {
            _orchestrator.LogWarning($"Compliance check failed: {complianceType} - {details}",
                impact: "Regulatory compliance violation",
                recommendedAction: "Review compliance requirements immediately");
        }
    }

    public void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, object>? tags = null)
    {
        _orchestrator.LogPerformance(metricName, TimeSpan.FromMilliseconds(value), true, null, null, tags);
    }

    public void LogLatencyViolation(string operation, TimeSpan actualLatency, TimeSpan expectedLatency, string correlationId)
    {
        _orchestrator.LogWarning($"Latency violation: {operation}",
            impact: $"Performance degradation: {actualLatency.TotalMicroseconds}μs vs {expectedLatency.TotalMicroseconds}μs target",
            recommendedAction: "Investigate performance bottleneck",
            additionalData: new { Operation = operation, ActualLatency = actualLatency, ExpectedLatency = expectedLatency, CorrelationId = correlationId });
    }

    public void LogSystemMetric(string metricName, double value, string unit)
    {
        _orchestrator.LogHealth("SystemMetrics", "MONITORED", new { MetricName = metricName, Value = value, Unit = unit });
    }

    public void LogHealthCheck(string serviceName, bool healthy, TimeSpan responseTime, string? details = null)
    {
        var status = healthy ? "HEALTHY" : "UNHEALTHY";
        _orchestrator.LogHealth(serviceName, status, new { ResponseTime = responseTime },
            healthy ? null : new[] { "Service health check failed" },
            healthy ? null : new[] { "Investigate service status", "Check dependencies" });
    }

    public void LogResourceUsage(string resource, double usage, double capacity, string unit)
    {
        var utilizationPercent = (usage / capacity) * 100;
        var status = utilizationPercent > 90 ? "CRITICAL" : utilizationPercent > 75 ? "WARNING" : "HEALTHY";
        _orchestrator.LogHealth($"Resource.{resource}", status,
            new { Usage = usage, Capacity = capacity, Unit = unit, UtilizationPercent = utilizationPercent });
    }

    public void LogTradingError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null)
    {
        _orchestrator.LogError($"Trading error in {operation}", exception, operation, "Trading operations impacted",
            "Check system status and retry operation", context);
    }

    public void LogCriticalError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null)
    {
        _orchestrator.LogError($"CRITICAL ERROR in {operation}", exception, operation, "System functionality severely impacted",
            "Immediate investigation required - escalate to operations team", context);
    }

    public void LogBusinessRuleViolation(string rule, string details, string? correlationId = null)
    {
        _orchestrator.LogWarning($"Business rule violation: {rule}",
            impact: "Business logic constraint violated",
            recommendedAction: "Review business rules and data integrity",
            additionalData: new { Rule = rule, Details = details, CorrelationId = correlationId });
    }

    public void LogDebugTrace(string message, Dictionary<string, object>? context = null, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogInfo($"DEBUG: {message}", context, callerName);
    }

    public void LogStateTransition(string entity, string fromState, string toState, string reason, string? correlationId = null)
    {
        _orchestrator.LogInfo($"State transition: {entity} {fromState} → {toState}",
            new { Entity = entity, FromState = fromState, ToState = toState, Reason = reason, CorrelationId = correlationId });
    }

    public void LogConfiguration(string component, Dictionary<string, object> configuration)
    {
        _orchestrator.LogInfo($"Configuration: {component}", configuration);
    }

    // Additional comprehensive debugging methods
    public void LogClassInstantiation(string className, object? constructorParams = null, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogClassInstantiation(className, constructorParams, callerName);
    }

    public void LogVariableChange(string variableName, object? oldValue, object? newValue, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
    {
        _orchestrator.LogVariableChange(variableName, oldValue, newValue, callerName, lineNumber);
    }

    public void LogDataMovement(string source, string destination, object? data, string operation, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogDataMovement(source, destination, data, operation, callerName);
    }

    public void LogDatabaseOperation(string operation, string table, object? parameters, TimeSpan? duration = null, bool success = true, string? errorMessage = null)
    {
        if (success)
        {
            _orchestrator.LogPerformance($"Database.{operation}.{table}", duration ?? TimeSpan.Zero, true, null, parameters);
        }
        else
        {
            _orchestrator.LogError($"Database operation failed: {operation} on {table}",
                operationContext: $"Database {operation}",
                userImpact: "Data operation failed",
                troubleshootingHints: errorMessage ?? "Check database connectivity and permissions",
                additionalData: new { Operation = operation, Table = table, Parameters = parameters });
        }
    }

    public void LogApiCall(string endpoint, string method, object? request, object? response, TimeSpan? duration = null, int? statusCode = null, string? errorMessage = null)
    {
        var success = statusCode < 400;
        _orchestrator.LogPerformance($"Api.{method}.{endpoint}", duration ?? TimeSpan.Zero, success, null,
            new { Request = request, Response = response, StatusCode = statusCode, ErrorMessage = errorMessage });
    }

    public void LogCacheOperation(string operation, string key, bool hit, TimeSpan? duration = null, object? data = null)
    {
        _orchestrator.LogPerformance($"Cache.{operation}.{(hit ? "Hit" : "Miss")}", duration ?? TimeSpan.Zero, hit, null,
            new { Key = key, Data = data });
    }

    public void LogThreadOperation(string operation, int threadId, string threadName, object? context = null)
    {
        _orchestrator.LogInfo($"Thread operation: {operation}",
            new { Operation = operation, ThreadId = threadId, ThreadName = threadName, Context = context });
    }

    public void LogMemoryUsage(string component, long bytesUsed, long bytesAllocated, string? details = null)
    {
        var utilizationPercent = bytesAllocated > 0 ? (double)bytesUsed / bytesAllocated * 100 : 0;
        _orchestrator.LogHealth($"Memory.{component}", utilizationPercent > 90 ? "HIGH_USAGE" : "NORMAL",
            new { BytesUsed = bytesUsed, BytesAllocated = bytesAllocated, UtilizationPercent = utilizationPercent, Details = details });
    }

    public void LogFileOperation(string operation, string filePath, long? fileSize = null, bool success = true, string? errorMessage = null)
    {
        if (success)
        {
            _orchestrator.LogInfo($"File operation: {operation} on {filePath}",
                new { Operation = operation, FilePath = filePath, FileSize = fileSize });
        }
        else
        {
            _orchestrator.LogError($"File operation failed: {operation} on {filePath}",
                operationContext: $"File {operation}",
                troubleshootingHints: errorMessage ?? "Check file permissions and disk space",
                additionalData: new { Operation = operation, FilePath = filePath, FileSize = fileSize });
        }
    }

    public void LogNetworkOperation(string operation, string endpoint, long? bytesTransferred = null, TimeSpan? latency = null, bool success = true, string? errorMessage = null)
    {
        if (success)
        {
            _orchestrator.LogPerformance($"Network.{operation}", latency ?? TimeSpan.Zero, true, null,
                new { Endpoint = endpoint, BytesTransferred = bytesTransferred });
        }
        else
        {
            _orchestrator.LogError($"Network operation failed: {operation} to {endpoint}",
                operationContext: $"Network {operation}",
                troubleshootingHints: errorMessage ?? "Check network connectivity",
                additionalData: new { Operation = operation, Endpoint = endpoint, BytesTransferred = bytesTransferred });
        }
    }

    public void LogSecurityEvent(string eventType, string details, string? userId = null, string? ipAddress = null, string? riskLevel = null)
    {
        var severity = riskLevel?.ToUpper() switch
        {
            "HIGH" => "HIGH",
            "CRITICAL" => "CRITICAL",
            _ => "MEDIUM"
        };

        _orchestrator.LogRisk("SecurityEvent", severity, $"{eventType}: {details}", null, null, null,
            "Security event requires investigation");
    }

    public void LogExceptionDetails(Exception exception, string context, Dictionary<string, object>? additionalData = null, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
    {
        _orchestrator.LogExceptionDetails(exception, context, additionalData, callerName, lineNumber);
    }

    public void LogStackTrace(string reason, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogStackTrace(reason, callerName);
    }

    public void LogSystemResource(string resourceType, double currentValue, double maxValue, string unit, string? threshold = null)
    {
        var utilizationPercent = (currentValue / maxValue) * 100;
        var status = utilizationPercent > 90 ? "CRITICAL" : utilizationPercent > 75 ? "WARNING" : "HEALTHY";
        _orchestrator.LogHealth($"SystemResource.{resourceType}", status,
            new { CurrentValue = currentValue, MaxValue = maxValue, Unit = unit, UtilizationPercent = utilizationPercent, Threshold = threshold });
    }

    public void LogApplicationEvent(string eventType, string description, object? metadata = null)
    {
        _orchestrator.LogInfo($"Application event: {eventType} - {description}", metadata);
    }

    public void LogUserAction(string userId, string action, object? parameters = null, string? sessionId = null)
    {
        _orchestrator.LogInfo($"User action: {action} by {userId}",
            new { UserId = userId, Action = action, Parameters = parameters, SessionId = sessionId });
    }

    public void LogComponentLifecycle(string component, string state, string? reason = null, object? configuration = null)
    {
        _orchestrator.LogInfo($"Component lifecycle: {component} → {state}",
            new { Component = component, State = state, Reason = reason, Configuration = configuration });
    }

    public void LogMessageQueueOperation(string operation, string queue, string? messageId = null, object? message = null, bool success = true, string? errorMessage = null)
    {
        if (success)
        {
            _orchestrator.LogInfo($"Message queue operation: {operation} on {queue}",
                new { Operation = operation, Queue = queue, MessageId = messageId, Message = message });
        }
        else
        {
            _orchestrator.LogError($"Message queue operation failed: {operation} on {queue}",
                operationContext: $"Message queue {operation}",
                troubleshootingHints: errorMessage ?? "Check message queue connectivity and permissions",
                additionalData: new { Operation = operation, Queue = queue, MessageId = messageId });
        }
    }

    public void LogScheduledTask(string taskName, DateTime scheduledTime, DateTime? actualTime = null, bool success = true, TimeSpan? duration = null, string? errorMessage = null)
    {
        if (success)
        {
            _orchestrator.LogPerformance($"ScheduledTask.{taskName}", duration ?? TimeSpan.Zero, true, null,
                new { TaskName = taskName, ScheduledTime = scheduledTime, ActualTime = actualTime });
        }
        else
        {
            _orchestrator.LogError($"Scheduled task failed: {taskName}",
                operationContext: "Scheduled task execution",
                troubleshootingHints: errorMessage ?? "Check task configuration and dependencies",
                additionalData: new { TaskName = taskName, ScheduledTime = scheduledTime, ActualTime = actualTime });
        }
    }

    public void LogValidation(string validationType, object? input, bool passed, string[]? errors = null, [CallerMemberName] string callerName = "")
    {
        if (passed)
        {
            _orchestrator.LogInfo($"Validation passed: {validationType}", new { Input = input }, callerName);
        }
        else
        {
            _orchestrator.LogWarning($"Validation failed: {validationType}",
                impact: "Data validation constraint violated",
                recommendedAction: "Review input data and validation rules",
                additionalData: new { Input = input, Errors = errors }, callerName);
        }
    }

    public void LogConfigurationChange(string component, string setting, object? oldValue, object? newValue, string? changedBy = null)
    {
        _orchestrator.LogInfo($"Configuration change: {component}.{setting}",
            new { Component = component, Setting = setting, OldValue = oldValue, NewValue = newValue, ChangedBy = changedBy });
    }

    public void LogAlert(string alertType, string severity, string message, object? context = null, string[]? recommendedActions = null)
    {
        _orchestrator.LogRisk(alertType, severity, message, null, null, recommendedActions, "Alert requires attention");
    }

    // Correlation and Context
    public IDisposable BeginScope(string operationName, string? correlationId = null)
    {
        return new LogScope(); // Simplified scope for delegation
    }

    public IDisposable BeginScope(Dictionary<string, object> properties)
    {
        return new LogScope(); // Simplified scope for delegation
    }

    public string GenerateCorrelationId()
    {
        return _orchestrator.GenerateCorrelationId();
    }

    public void SetCorrelationId(string correlationId)
    {
        // Correlation ID is handled by the orchestrator
    }


    // ITradingLogger.LogMethodEntry implementation
    public void LogMethodEntry(object? parameters = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        _orchestrator.LogMethodEntry(parameters, memberName, sourceFilePath);
    }

    // ITradingLogger.LogMethodEntry implementation
    public void LogMethodEntry(string methodName, object? parameters = null, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogMethodEntry(parameters, methodName, "");
    }

    // ITradingLogger.LogMethodExit implementation
    public void LogMethodExit(object? result = null, TimeSpan? executionTime = null, bool success = true, [CallerMemberName] string memberName = "")
    {
        _orchestrator.LogMethodExit(result, executionTime, success, memberName);
    }

    // ITradingLogger.LogMethodExit implementation
    public void LogMethodExit(string methodName, TimeSpan duration, object? result = null, [CallerMemberName] string callerName = "")
    {
        _orchestrator.LogMethodExit(result, duration, true, methodName);
    }
    #endregion
}

/// <summary>
/// Simplified scope for delegation pattern
/// </summary>
internal class LogScope : IDisposable
{





    public void Dispose()
    {
        // No-op for delegation pattern
    }
}


