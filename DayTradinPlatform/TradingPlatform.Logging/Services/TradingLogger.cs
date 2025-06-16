using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using TradingPlatform.Logging.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TradingPlatform.Logging.Services;

/// <summary>
/// Enterprise-grade trading logger with comprehensive instrumentation
/// CRITICAL: Every trading operation MUST be logged with full context and timing
/// </summary>
public class TradingLogger : ITradingLogger
{
    private readonly ILogger _logger;
    private readonly Serilog.ILogger _serilogLogger;
    private readonly string _serviceName;
    private readonly AsyncLocal<string?> _correlationId = new();

    public TradingLogger(ILogger<TradingLogger> logger, string serviceName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serilogLogger = Serilog.Log.ForContext<TradingLogger>();
        _serviceName = serviceName;
    }

    #region Trading Operation Logging

    public void LogOrderSubmission(string orderId, string symbol, decimal quantity, decimal price, string side, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "OrderSubmission",
            ["OrderId"] = orderId,
            ["Symbol"] = symbol,
            ["Quantity"] = quantity,
            ["Price"] = price,
            ["Side"] = side,
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "ORDER_SUBMISSION: Order {OrderId} submitted for {Symbol} - {Side} {Quantity} @ {Price} [CorrelationId: {CorrelationId}]",
            orderId, symbol, side, quantity, price, correlationId);

        LogPerformanceMetric("orders.submitted", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["side"] = side,
            ["service"] = _serviceName
        });
    }

    public void LogOrderExecution(string orderId, string symbol, decimal executedQuantity, decimal executedPrice, TimeSpan latency, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "OrderExecution",
            ["OrderId"] = orderId,
            ["Symbol"] = symbol,
            ["ExecutedQuantity"] = executedQuantity,
            ["ExecutedPrice"] = executedPrice,
            ["LatencyMs"] = latency.TotalMilliseconds,
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "ORDER_EXECUTION: Order {OrderId} executed for {Symbol} - {ExecutedQuantity} @ {ExecutedPrice} in {LatencyMs}ms [CorrelationId: {CorrelationId}]",
            orderId, symbol, executedQuantity, executedPrice, latency.TotalMilliseconds, correlationId);

        // Check for latency violations (target <100μs for critical path)
        if (latency.TotalMicroseconds > 100)
        {
            LogLatencyViolation("OrderExecution", latency, TimeSpan.FromMicroseconds(100), correlationId);
        }

        LogPerformanceMetric("orders.executed", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["latency_ms"] = latency.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogOrderRejection(string orderId, string symbol, string reason, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "OrderRejection",
            ["OrderId"] = orderId,
            ["Symbol"] = symbol,
            ["Reason"] = reason,
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["Service"] = _serviceName
        });

        _serilogLogger.Warning(
            "ORDER_REJECTION: Order {OrderId} rejected for {Symbol} - Reason: {Reason} [CorrelationId: {CorrelationId}]",
            orderId, symbol, reason, correlationId);

        LogPerformanceMetric("orders.rejected", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["reason"] = reason,
            ["service"] = _serviceName
        });
    }

    public void LogOrderCancellation(string orderId, string symbol, string reason, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "OrderCancellation",
            ["OrderId"] = orderId,
            ["Symbol"] = symbol,
            ["Reason"] = reason,
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "ORDER_CANCELLATION: Order {OrderId} cancelled for {Symbol} - Reason: {Reason} [CorrelationId: {CorrelationId}]",
            orderId, symbol, reason, correlationId);

        LogPerformanceMetric("orders.cancelled", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["reason"] = reason,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region Market Data Logging

    public void LogMarketDataReceived(string symbol, decimal price, long volume, DateTime timestamp, TimeSpan latency)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "MarketDataReceived",
            ["Symbol"] = symbol,
            ["Price"] = price,
            ["Volume"] = volume,
            ["DataTimestamp"] = timestamp,
            ["LatencyMs"] = latency.TotalMilliseconds,
            ["Service"] = _serviceName
        });

        _serilogLogger.Debug(
            "MARKET_DATA: {Symbol} - Price: {Price}, Volume: {Volume}, Latency: {LatencyMs}ms",
            symbol, price, volume, latency.TotalMilliseconds);

        // Alert on high latency (target <50μs for market data)
        if (latency.TotalMicroseconds > 50)
        {
            LogLatencyViolation("MarketDataReceived", latency, TimeSpan.FromMicroseconds(50), GenerateCorrelationId());
        }

        LogPerformanceMetric("market_data.received", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["latency_ms"] = latency.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogMarketDataCacheHit(string symbol, TimeSpan retrievalTime)
    {
        _serilogLogger.Debug(
            "CACHE_HIT: {Symbol} retrieved from cache in {RetrievalTimeMs}ms",
            symbol, retrievalTime.TotalMilliseconds);

        LogPerformanceMetric("market_data.cache_hit", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["retrieval_time_ms"] = retrievalTime.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogMarketDataCacheMiss(string symbol, TimeSpan retrievalTime)
    {
        _serilogLogger.Debug(
            "CACHE_MISS: {Symbol} not in cache, fetched in {RetrievalTimeMs}ms",
            symbol, retrievalTime.TotalMilliseconds);

        LogPerformanceMetric("market_data.cache_miss", 1, "count", new Dictionary<string, object>
        {
            ["symbol"] = symbol,
            ["retrieval_time_ms"] = retrievalTime.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogMarketDataProviderLatency(string provider, string symbol, TimeSpan latency)
    {
        _serilogLogger.Information(
            "PROVIDER_LATENCY: {Provider} - {Symbol} responded in {LatencyMs}ms",
            provider, symbol, latency.TotalMilliseconds);

        LogPerformanceMetric("market_data.provider_latency", latency.TotalMilliseconds, "ms", new Dictionary<string, object>
        {
            ["provider"] = provider,
            ["symbol"] = symbol,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region Strategy Execution Logging

    public void LogStrategySignal(string strategyName, string symbol, string signal, decimal confidence, string reason, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "StrategySignal",
            ["StrategyName"] = strategyName,
            ["Symbol"] = symbol,
            ["Signal"] = signal,
            ["Confidence"] = confidence,
            ["Reason"] = reason,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "STRATEGY_SIGNAL: {StrategyName} generated {Signal} signal for {Symbol} with {Confidence}% confidence - {Reason} [CorrelationId: {CorrelationId}]",
            strategyName, signal, symbol, confidence, reason, correlationId);

        LogPerformanceMetric("strategy.signals", 1, "count", new Dictionary<string, object>
        {
            ["strategy"] = strategyName,
            ["symbol"] = symbol,
            ["signal"] = signal,
            ["confidence"] = (double)confidence,
            ["service"] = _serviceName
        });
    }

    public void LogStrategyExecution(string strategyName, string symbol, TimeSpan executionTime, bool success, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "StrategyExecution",
            ["StrategyName"] = strategyName,
            ["Symbol"] = symbol,
            ["ExecutionTimeMs"] = executionTime.TotalMilliseconds,
            ["Success"] = success,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        var logLevel = success ? LogLevel.Information : LogLevel.Warning;
        var status = success ? "SUCCESS" : "FAILED";

        _logger.Log(logLevel,
            "STRATEGY_EXECUTION: {StrategyName} execution {Status} for {Symbol} in {ExecutionTimeMs}ms [CorrelationId: {CorrelationId}]",
            strategyName, status, symbol, executionTime.TotalMilliseconds, correlationId);

        // Check for execution time violations (target <45ms)
        if (executionTime.TotalMilliseconds > 45)
        {
            LogLatencyViolation("StrategyExecution", executionTime, TimeSpan.FromMilliseconds(45), correlationId);
        }

        LogPerformanceMetric("strategy.executions", 1, "count", new Dictionary<string, object>
        {
            ["strategy"] = strategyName,
            ["symbol"] = symbol,
            ["success"] = success,
            ["execution_time_ms"] = executionTime.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogStrategyPerformance(string strategyName, decimal pnl, decimal sharpeRatio, int tradesCount, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "StrategyPerformance",
            ["StrategyName"] = strategyName,
            ["PnL"] = pnl,
            ["SharpeRatio"] = sharpeRatio,
            ["TradesCount"] = tradesCount,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "STRATEGY_PERFORMANCE: {StrategyName} - PnL: {PnL}, Sharpe: {SharpeRatio}, Trades: {TradesCount} [CorrelationId: {CorrelationId}]",
            strategyName, pnl, sharpeRatio, tradesCount, correlationId);

        LogPerformanceMetric("strategy.pnl", (double)pnl, "currency", new Dictionary<string, object>
        {
            ["strategy"] = strategyName,
            ["service"] = _serviceName
        });

        LogPerformanceMetric("strategy.sharpe_ratio", (double)sharpeRatio, "ratio", new Dictionary<string, object>
        {
            ["strategy"] = strategyName,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region Risk Management Logging

    public void LogRiskCheck(string riskType, string symbol, decimal value, decimal limit, bool passed, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "RiskCheck",
            ["RiskType"] = riskType,
            ["Symbol"] = symbol,
            ["Value"] = value,
            ["Limit"] = limit,
            ["Passed"] = passed,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        var logLevel = passed ? LogLevel.Debug : LogLevel.Warning;
        var status = passed ? "PASSED" : "FAILED";

        _logger.Log(logLevel,
            "RISK_CHECK: {RiskType} {Status} for {Symbol} - Value: {Value}, Limit: {Limit} [CorrelationId: {CorrelationId}]",
            riskType, status, symbol, value, limit, correlationId);

        LogPerformanceMetric("risk.checks", 1, "count", new Dictionary<string, object>
        {
            ["risk_type"] = riskType,
            ["symbol"] = symbol,
            ["passed"] = passed,
            ["service"] = _serviceName
        });
    }

    public void LogRiskAlert(string alertType, string symbol, decimal currentValue, decimal threshold, string severity, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "RiskAlert",
            ["AlertType"] = alertType,
            ["Symbol"] = symbol,
            ["CurrentValue"] = currentValue,
            ["Threshold"] = threshold,
            ["Severity"] = severity,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        var logLevel = severity.ToUpper() switch
        {
            "CRITICAL" => LogLevel.Critical,
            "HIGH" => LogLevel.Error,
            "MEDIUM" => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "RISK_ALERT: {AlertType} alert for {Symbol} - Current: {CurrentValue}, Threshold: {Threshold}, Severity: {Severity} [CorrelationId: {CorrelationId}]",
            alertType, symbol, currentValue, threshold, severity, correlationId);

        LogPerformanceMetric("risk.alerts", 1, "count", new Dictionary<string, object>
        {
            ["alert_type"] = alertType,
            ["symbol"] = symbol,
            ["severity"] = severity,
            ["service"] = _serviceName
        });
    }

    public void LogComplianceCheck(string complianceType, string result, string details, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "ComplianceCheck",
            ["ComplianceType"] = complianceType,
            ["Result"] = result,
            ["Details"] = details,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        var logLevel = result.ToUpper() == "PASSED" ? LogLevel.Information : LogLevel.Warning;

        _logger.Log(logLevel,
            "COMPLIANCE_CHECK: {ComplianceType} {Result} - {Details} [CorrelationId: {CorrelationId}]",
            complianceType, result, details, correlationId);

        LogPerformanceMetric("compliance.checks", 1, "count", new Dictionary<string, object>
        {
            ["compliance_type"] = complianceType,
            ["result"] = result,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region Performance Logging

    public void LogMethodEntry(string methodName, object? parameters = null, [CallerMemberName] string callerName = "")
    {
        _serilogLogger.Debug(
            "METHOD_ENTRY: {MethodName} called from {CallerName} with parameters: {@Parameters}",
            methodName, callerName, parameters);
    }

    public void LogMethodExit(string methodName, TimeSpan duration, object? result = null, [CallerMemberName] string callerName = "")
    {
        _serilogLogger.Debug(
            "METHOD_EXIT: {MethodName} completed in {DurationMs}ms, Result: {@Result}",
            methodName, duration.TotalMilliseconds, result);

        LogPerformanceMetric("method.duration", duration.TotalMilliseconds, "ms", new Dictionary<string, object>
        {
            ["method"] = methodName,
            ["caller"] = callerName,
            ["service"] = _serviceName
        });
    }

    public void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, object>? tags = null)
    {
        var enrichedTags = new Dictionary<string, object>(tags ?? new Dictionary<string, object>())
        {
            ["metric_name"] = metricName,
            ["value"] = value,
            ["unit"] = unit,
            ["timestamp"] = DateTime.UtcNow,
            ["service"] = _serviceName
        };

        using var scope = BeginScope(enrichedTags);

        _serilogLogger.Information(
            "PERFORMANCE_METRIC: {MetricName} = {Value} {Unit} {@Tags}",
            metricName, value, unit, enrichedTags);
    }

    public void LogLatencyViolation(string operation, TimeSpan actualLatency, TimeSpan expectedLatency, string correlationId)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "LatencyViolation",
            ["Operation"] = operation,
            ["ActualLatencyMs"] = actualLatency.TotalMilliseconds,
            ["ExpectedLatencyMs"] = expectedLatency.TotalMilliseconds,
            ["ViolationRatio"] = actualLatency.TotalMilliseconds / expectedLatency.TotalMilliseconds,
            ["CorrelationId"] = correlationId,
            ["Service"] = _serviceName
        });

        _serilogLogger.Warning(
            "LATENCY_VIOLATION: {Operation} took {ActualLatencyMs}ms, expected <{ExpectedLatencyMs}ms [CorrelationId: {CorrelationId}]",
            operation, actualLatency.TotalMilliseconds, expectedLatency.TotalMilliseconds, correlationId);

        LogPerformanceMetric("latency.violations", 1, "count", new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["actual_latency_ms"] = actualLatency.TotalMilliseconds,
            ["expected_latency_ms"] = expectedLatency.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region System Health Logging

    public void LogSystemMetric(string metricName, double value, string unit)
    {
        _serilogLogger.Information(
            "SYSTEM_METRIC: {MetricName} = {Value} {Unit}",
            metricName, value, unit);

        LogPerformanceMetric($"system.{metricName}", value, unit, new Dictionary<string, object>
        {
            ["metric_type"] = "system",
            ["service"] = _serviceName
        });
    }

    public void LogHealthCheck(string serviceName, bool healthy, TimeSpan responseTime, string? details = null)
    {
        var logLevel = healthy ? LogLevel.Debug : LogLevel.Warning;
        var status = healthy ? "HEALTHY" : "UNHEALTHY";

        _logger.Log(logLevel,
            "HEALTH_CHECK: {ServiceName} is {Status} - Response time: {ResponseTimeMs}ms, Details: {Details}",
            serviceName, status, responseTime.TotalMilliseconds, details);

        LogPerformanceMetric("health.check", healthy ? 1 : 0, "boolean", new Dictionary<string, object>
        {
            ["target_service"] = serviceName,
            ["response_time_ms"] = responseTime.TotalMilliseconds,
            ["service"] = _serviceName
        });
    }

    public void LogResourceUsage(string resource, double usage, double capacity, string unit)
    {
        var utilizationPercent = (usage / capacity) * 100;

        _serilogLogger.Information(
            "RESOURCE_USAGE: {Resource} = {Usage}/{Capacity} {Unit} ({UtilizationPercent:F1}%)",
            resource, usage, capacity, unit, utilizationPercent);

        LogPerformanceMetric($"resource.{resource}.usage", usage, unit);
        LogPerformanceMetric($"resource.{resource}.utilization", utilizationPercent, "percent");
    }

    #endregion

    #region Error and Exception Logging

    public void LogTradingError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null)
    {
        using var scope = BeginScope(new Dictionary<string, object>(context ?? new Dictionary<string, object>())
        {
            ["OperationType"] = "TradingError",
            ["Operation"] = operation,
            ["ExceptionType"] = exception.GetType().Name,
            ["CorrelationId"] = correlationId ?? GenerateCorrelationId(),
            ["Service"] = _serviceName
        });

        _serilogLogger.Error(exception,
            "TRADING_ERROR: {Operation} failed - {ExceptionMessage} [CorrelationId: {CorrelationId}]",
            operation, exception.Message, correlationId);

        LogPerformanceMetric("errors.trading", 1, "count", new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["exception_type"] = exception.GetType().Name,
            ["service"] = _serviceName
        });
    }

    public void LogCriticalError(string operation, Exception exception, string? correlationId = null, Dictionary<string, object>? context = null)
    {
        using var scope = BeginScope(new Dictionary<string, object>(context ?? new Dictionary<string, object>())
        {
            ["OperationType"] = "CriticalError",
            ["Operation"] = operation,
            ["ExceptionType"] = exception.GetType().Name,
            ["CorrelationId"] = correlationId ?? GenerateCorrelationId(),
            ["Service"] = _serviceName
        });

        _serilogLogger.Fatal(exception,
            "CRITICAL_ERROR: {Operation} failed critically - {ExceptionMessage} [CorrelationId: {CorrelationId}]",
            operation, exception.Message, correlationId);

        LogPerformanceMetric("errors.critical", 1, "count", new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["exception_type"] = exception.GetType().Name,
            ["service"] = _serviceName
        });
    }

    public void LogBusinessRuleViolation(string rule, string details, string? correlationId = null)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "BusinessRuleViolation",
            ["Rule"] = rule,
            ["Details"] = details,
            ["CorrelationId"] = correlationId ?? GenerateCorrelationId(),
            ["Service"] = _serviceName
        });

        _serilogLogger.Warning(
            "BUSINESS_RULE_VIOLATION: {Rule} violated - {Details} [CorrelationId: {CorrelationId}]",
            rule, details, correlationId);

        LogPerformanceMetric("business_rules.violations", 1, "count", new Dictionary<string, object>
        {
            ["rule"] = rule,
            ["service"] = _serviceName
        });
    }

    #endregion

    #region Debug and Diagnostic Logging

    public void LogDebugTrace(string message, Dictionary<string, object>? context = null, [CallerMemberName] string callerName = "")
    {
        using var scope = BeginScope(context ?? new Dictionary<string, object>());

        _serilogLogger.Debug(
            "DEBUG_TRACE: {Message} [Caller: {CallerName}] {@Context}",
            message, callerName, context);
    }

    public void LogStateTransition(string entity, string fromState, string toState, string reason, string? correlationId = null)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "StateTransition",
            ["Entity"] = entity,
            ["FromState"] = fromState,
            ["ToState"] = toState,
            ["Reason"] = reason,
            ["CorrelationId"] = correlationId ?? GenerateCorrelationId(),
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "STATE_TRANSITION: {Entity} changed from {FromState} to {ToState} - {Reason} [CorrelationId: {CorrelationId}]",
            entity, fromState, toState, reason, correlationId);

        LogPerformanceMetric("state.transitions", 1, "count", new Dictionary<string, object>
        {
            ["entity"] = entity,
            ["from_state"] = fromState,
            ["to_state"] = toState,
            ["service"] = _serviceName
        });
    }

    public void LogConfiguration(string component, Dictionary<string, object> configuration)
    {
        using var scope = BeginScope(new Dictionary<string, object>
        {
            ["OperationType"] = "Configuration",
            ["Component"] = component,
            ["Service"] = _serviceName
        });

        _serilogLogger.Information(
            "CONFIGURATION: {Component} configured with {@Configuration}",
            component, configuration);
    }

    #endregion

    #region Correlation and Context

    public IDisposable BeginScope(string operationName, string? correlationId = null)
    {
        var scopeCorrelationId = correlationId ?? GenerateCorrelationId();
        SetCorrelationId(scopeCorrelationId);

        return BeginScope(new Dictionary<string, object>
        {
            ["OperationName"] = operationName,
            ["CorrelationId"] = scopeCorrelationId,
            ["Service"] = _serviceName,
            ["StartTime"] = DateTime.UtcNow
        });
    }

    public IDisposable BeginScope(Dictionary<string, object> properties)
    {
        var enrichedProperties = new Dictionary<string, object>(properties)
        {
            ["Service"] = _serviceName,
            ["Timestamp"] = DateTime.UtcNow
        };

        return LogContext.PushProperty("Scope", enrichedProperties);
    }

    public string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..8]; // Short correlation ID for performance
    }

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    #endregion

    #region ILogger Implementation

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    #endregion
}