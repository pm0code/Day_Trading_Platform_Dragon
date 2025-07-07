// TradingPlatform.Core.Logging.TradingLogOrchestratorEnhanced - ENHANCED WITH MCP STANDARDS
// SCREAMING_SNAKE_CASE event codes, operation tracking, child logger support
// Backwards compatible with existing TradingLogOrchestrator

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Enhanced TradingLogOrchestrator with MCP standards compliance
/// - SCREAMING_SNAKE_CASE event codes for categorization
/// - Operation tracking (start/complete/failed) with timing
/// - Child logger support for component isolation
/// - Composition-based design instead of inheritance
/// </summary>
public sealed class TradingLogOrchestratorEnhanced : ITradingLogger, IDisposable
{
    #region Event Code Constants (SCREAMING_SNAKE_CASE)
    
    // Trading Events
    public const string TRADE_EXECUTED = "TRADE_EXECUTED";
    public const string TRADE_REJECTED = "TRADE_REJECTED";
    public const string TRADE_CANCELLED = "TRADE_CANCELLED";
    public const string ORDER_PLACED = "ORDER_PLACED";
    public const string ORDER_FILLED = "ORDER_FILLED";
    public const string ORDER_PARTIAL_FILL = "ORDER_PARTIAL_FILL";
    
    // Market Data Events
    public const string MARKET_DATA_RECEIVED = "MARKET_DATA_RECEIVED";
    public const string MARKET_DATA_STALE = "MARKET_DATA_STALE";
    public const string MARKET_DATA_ERROR = "MARKET_DATA_ERROR";
    public const string QUOTE_UPDATE = "QUOTE_UPDATE";
    public const string TICK_RECEIVED = "TICK_RECEIVED";
    
    // Risk Events
    public const string RISK_LIMIT_BREACH = "RISK_LIMIT_BREACH";
    public const string RISK_WARNING = "RISK_WARNING";
    public const string POSITION_LIMIT_REACHED = "POSITION_LIMIT_REACHED";
    public const string LOSS_LIMIT_APPROACHING = "LOSS_LIMIT_APPROACHING";
    
    // System Events
    public const string SYSTEM_STARTUP = "SYSTEM_STARTUP";
    public const string SYSTEM_SHUTDOWN = "SYSTEM_SHUTDOWN";
    public const string COMPONENT_INITIALIZED = "COMPONENT_INITIALIZED";
    public const string COMPONENT_FAILED = "COMPONENT_FAILED";
    public const string HEALTH_CHECK_PASSED = "HEALTH_CHECK_PASSED";
    public const string HEALTH_CHECK_FAILED = "HEALTH_CHECK_FAILED";
    
    // Performance Events
    public const string PERFORMANCE_DEGRADATION = "PERFORMANCE_DEGRADATION";
    public const string LATENCY_SPIKE = "LATENCY_SPIKE";
    public const string THROUGHPUT_DROP = "THROUGHPUT_DROP";
    public const string MEMORY_PRESSURE = "MEMORY_PRESSURE";
    
    // Data Pipeline Events
    public const string DATA_INGESTION_START = "DATA_INGESTION_START";
    public const string DATA_INGESTION_COMPLETE = "DATA_INGESTION_COMPLETE";
    public const string DATA_VALIDATION_FAILED = "DATA_VALIDATION_FAILED";
    public const string DATA_TRANSFORMATION_ERROR = "DATA_TRANSFORMATION_ERROR";
    
    // Operation Status
    public const string OPERATION_STARTED = "OPERATION_STARTED";
    public const string OPERATION_COMPLETED = "OPERATION_COMPLETED";
    public const string OPERATION_FAILED = "OPERATION_FAILED";
    public const string OPERATION_TIMEOUT = "OPERATION_TIMEOUT";
    public const string OPERATION_CANCELLED = "OPERATION_CANCELLED";
    
    #endregion
    
    #region Operation Tracking Infrastructure
    
    private readonly ConcurrentDictionary<string, OperationContext> _activeOperations = new();
    private readonly ConcurrentDictionary<string, ChildLoggerContext> _childLoggers = new();
    private long _operationCounter = 0;
    
    #endregion
    
    private readonly TradingLogOrchestrator _baseOrchestrator;
    
    /// <summary>
    /// Enhanced singleton instance
    /// </summary>
    private static readonly Lazy<TradingLogOrchestratorEnhanced> _enhancedInstance = 
        new(() => new TradingLogOrchestratorEnhanced());
    
    public static TradingLogOrchestratorEnhanced Instance => _enhancedInstance.Value;
    
    private TradingLogOrchestratorEnhanced(string serviceName = "TradingPlatform")
    {
        _baseOrchestrator = TradingLogOrchestrator.Instance;
        
        // Log enhanced orchestrator initialization
        LogEventCode(SYSTEM_STARTUP, "Enhanced TradingLogOrchestrator initialized with MCP standards", 
            new { Features = new[] { "SCREAMING_SNAKE_CASE", "Operation Tracking", "Child Loggers" } });
    }
    
    #region Event Code Logging Methods
    
    /// <summary>
    /// Log with SCREAMING_SNAKE_CASE event code
    /// </summary>
    public void LogEventCode(string eventCode, string message, object? data = null,
        LogLevel level = LogLevel.Info,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var eventData = new
        {
            EventCode = eventCode,
            EventTimestamp = DateTime.UtcNow,
            Data = data
        };
        
        var enhancedMessage = $"[{eventCode}] {message}";
        
        switch (level)
        {
            case LogLevel.Debug:
                _baseOrchestrator.LogDebug(enhancedMessage, eventData, memberName, sourceFilePath, sourceLineNumber);
                break;
            case LogLevel.Info:
                _baseOrchestrator.LogInfo(enhancedMessage, eventData, memberName, sourceFilePath, sourceLineNumber);
                break;
            case LogLevel.Warning:
                _baseOrchestrator.LogWarning(enhancedMessage, additionalData: eventData, memberName: memberName, 
                    sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
                break;
            case LogLevel.Error:
                _baseOrchestrator.LogError(enhancedMessage, additionalData: eventData, memberName: memberName, 
                    sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
                break;
            case LogLevel.Critical:
                _baseOrchestrator.LogError($"[CRITICAL] {enhancedMessage}", additionalData: eventData, memberName: memberName, 
                    sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
                break;
        }
    }
    
    /// <summary>
    /// Log trade event with event code
    /// </summary>
    public void LogTradeEvent(string eventCode, string symbol, string action, decimal quantity, decimal price,
        string? orderId = null, string? strategy = null, TimeSpan? executionTime = null,
        object? additionalData = null,
        [CallerMemberName] string memberName = "")
    {
        var tradeEventData = new
        {
            EventCode = eventCode,
            Symbol = symbol,
            Action = action,
            Quantity = quantity,
            Price = price,
            OrderId = orderId,
            Strategy = strategy,
            ExecutionTime = executionTime,
            AdditionalData = additionalData
        };
        
        _baseOrchestrator.LogTrade(symbol, action, quantity, price, orderId, strategy, executionTime, 
            marketConditions: tradeEventData, memberName: memberName);
    }
    
    #endregion
    
    #region Operation Tracking Methods
    
    /// <summary>
    /// Start tracking an operation with automatic timing
    /// </summary>
    public string StartOperation(string operationName, object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var operationId = $"{operationName}_{Interlocked.Increment(ref _operationCounter)}_{Guid.NewGuid():N}".Substring(0, 32);
        var context = new OperationContext
        {
            OperationId = operationId,
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            StartTimestamp = Stopwatch.GetTimestamp(),
            Parameters = parameters,
            CallerMemberName = memberName,
            CallerFilePath = sourceFilePath,
            CallerLineNumber = sourceLineNumber,
            ThreadId = Environment.CurrentManagedThreadId
        };
        
        _activeOperations[operationId] = context;
        
        LogEventCode(OPERATION_STARTED, $"Operation '{operationName}' started", 
            new
            {
                OperationId = operationId,
                OperationName = operationName,
                Parameters = parameters,
                ThreadId = context.ThreadId
            }, 
            LogLevel.Debug, memberName, sourceFilePath, sourceLineNumber);
        
        return operationId;
    }
    
    /// <summary>
    /// Complete an operation successfully
    /// </summary>
    public void CompleteOperation(string operationId, object? result = null,
        [CallerMemberName] string memberName = "")
    {
        if (_activeOperations.TryRemove(operationId, out var context))
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = TimeSpan.FromTicks((endTimestamp - context.StartTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            
            LogEventCode(OPERATION_COMPLETED, $"Operation '{context.OperationName}' completed successfully",
                new
                {
                    OperationId = operationId,
                    OperationName = context.OperationName,
                    DurationMs = duration.TotalMilliseconds,
                    DurationMicroseconds = duration.TotalMicroseconds,
                    Result = result,
                    ThreadId = Environment.CurrentManagedThreadId
                },
                LogLevel.Debug, memberName);
            
            // Log performance if operation took significant time
            if (duration.TotalMicroseconds > 100)
            {
                _baseOrchestrator.LogPerformance(context.OperationName, duration, true, 
                    businessMetrics: new { OperationId = operationId, Result = result },
                    memberName: memberName);
            }
        }
    }
    
    /// <summary>
    /// Fail an operation with error details
    /// </summary>
    public void FailOperation(string operationId, Exception exception, string? errorContext = null,
        [CallerMemberName] string memberName = "")
    {
        if (_activeOperations.TryRemove(operationId, out var context))
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = TimeSpan.FromTicks((endTimestamp - context.StartTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            
            LogEventCode(OPERATION_FAILED, $"Operation '{context.OperationName}' failed",
                new
                {
                    OperationId = operationId,
                    OperationName = context.OperationName,
                    DurationMs = duration.TotalMilliseconds,
                    ErrorType = exception.GetType().Name,
                    ErrorMessage = exception.Message,
                    ErrorContext = errorContext,
                    ThreadId = Environment.CurrentManagedThreadId
                },
                LogLevel.Error, memberName);
            
            _baseOrchestrator.LogError($"Operation '{context.OperationName}' failed after {duration.TotalMilliseconds}ms",
                exception, errorContext, memberName: memberName);
        }
    }
    
    /// <summary>
    /// Cancel an operation
    /// </summary>
    public void CancelOperation(string operationId, string reason,
        [CallerMemberName] string memberName = "")
    {
        if (_activeOperations.TryRemove(operationId, out var context))
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = TimeSpan.FromTicks((endTimestamp - context.StartTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            
            LogEventCode(OPERATION_CANCELLED, $"Operation '{context.OperationName}' cancelled",
                new
                {
                    OperationId = operationId,
                    OperationName = context.OperationName,
                    DurationMs = duration.TotalMilliseconds,
                    Reason = reason,
                    ThreadId = Environment.CurrentManagedThreadId
                },
                LogLevel.Warning, memberName);
        }
    }
    
    /// <summary>
    /// Execute operation with automatic tracking
    /// </summary>
    public async Task<T> TrackOperationAsync<T>(string operationName, Func<Task<T>> operation,
        object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var operationId = StartOperation(operationName, parameters, memberName, sourceFilePath, sourceLineNumber);
        
        try
        {
            var result = await operation();
            CompleteOperation(operationId, result, memberName);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            CancelOperation(operationId, ex.Message, memberName);
            throw;
        }
        catch (Exception ex)
        {
            FailOperation(operationId, ex, memberName: memberName);
            throw;
        }
    }
    
    /// <summary>
    /// Execute operation with automatic tracking (synchronous)
    /// </summary>
    public T TrackOperation<T>(string operationName, Func<T> operation,
        object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var operationId = StartOperation(operationName, parameters, memberName, sourceFilePath, sourceLineNumber);
        
        try
        {
            var result = operation();
            CompleteOperation(operationId, result, memberName);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            CancelOperation(operationId, ex.Message, memberName);
            throw;
        }
        catch (Exception ex)
        {
            FailOperation(operationId, ex, memberName: memberName);
            throw;
        }
    }
    
    #endregion
    
    #region Child Logger Support
    
    /// <summary>
    /// Create a child logger for a specific component
    /// </summary>
    public IChildLogger CreateChildLogger(string componentName, Dictionary<string, string>? metadata = null)
    {
        var childContext = new ChildLoggerContext
        {
            ComponentName = componentName,
            ParentCorrelationId = GenerateCorrelationId(),
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
        
        _childLoggers[componentName] = childContext;
        
        LogEventCode(COMPONENT_INITIALIZED, $"Child logger created for component '{componentName}'",
            new { ComponentName = componentName, Metadata = metadata });
        
        return new ChildLogger(this, childContext);
    }
    
    /// <summary>
    /// Remove child logger
    /// </summary>
    public void RemoveChildLogger(string componentName)
    {
        if (_childLoggers.TryRemove(componentName, out _))
        {
            _baseOrchestrator.LogDebug($"Child logger removed for component '{componentName}'");
        }
    }
    
    #endregion
    
    #region Supporting Classes
    
    /// <summary>
    /// Operation context for tracking
    /// </summary>
    private class OperationContext
    {
        public string OperationId { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public long StartTimestamp { get; set; }
        public object? Parameters { get; set; }
        public string CallerMemberName { get; set; } = string.Empty;
        public string CallerFilePath { get; set; } = string.Empty;
        public int CallerLineNumber { get; set; }
        public int ThreadId { get; set; }
    }
    
    /// <summary>
    /// Child logger context
    /// </summary>
    private class ChildLoggerContext
    {
        public string ComponentName { get; set; } = string.Empty;
        public string ParentCorrelationId { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
    
    /// <summary>
    /// Child logger implementation
    /// </summary>
    private class ChildLogger : IChildLogger
    {
        private readonly TradingLogOrchestratorEnhanced _parent;
        private readonly ChildLoggerContext _context;
        
        public ChildLogger(TradingLogOrchestratorEnhanced parent, ChildLoggerContext context)
        {
            _parent = parent;
            _context = context;
        }
        
        public void LogInfo(string message, object? data = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var childData = EnrichWithChildContext(data);
            _parent._baseOrchestrator.LogInfo($"[{_context.ComponentName}] {message}", childData, memberName, sourceFilePath, sourceLineNumber);
        }
        
        public void LogDebug(string message, object? data = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var childData = EnrichWithChildContext(data);
            _parent._baseOrchestrator.LogDebug($"[{_context.ComponentName}] {message}", childData, memberName, sourceFilePath, sourceLineNumber);
        }
        
        public void LogWarning(string message, object? data = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var childData = EnrichWithChildContext(data);
            _parent._baseOrchestrator.LogWarning($"[{_context.ComponentName}] {message}", additionalData: childData, 
                memberName: memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }
        
        public void LogError(string message, Exception? exception = null, object? data = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var childData = EnrichWithChildContext(data);
            _parent._baseOrchestrator.LogError($"[{_context.ComponentName}] {message}", exception, additionalData: childData,
                memberName: memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }
        
        public void LogEvent(string eventCode, string message, object? data = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var childData = EnrichWithChildContext(data);
            _parent.LogEventCode(eventCode, $"[{_context.ComponentName}] {message}", childData, level,
                memberName, sourceFilePath, sourceLineNumber);
        }
        
        public string StartOperation(string operationName, object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var enrichedParams = EnrichWithChildContext(parameters);
            return _parent.StartOperation($"{_context.ComponentName}.{operationName}", enrichedParams,
                memberName, sourceFilePath, sourceLineNumber);
        }
        
        public void CompleteOperation(string operationId, object? result = null,
            [CallerMemberName] string memberName = "")
        {
            _parent.CompleteOperation(operationId, result, memberName);
        }
        
        public void FailOperation(string operationId, Exception exception, string? errorContext = null,
            [CallerMemberName] string memberName = "")
        {
            _parent.FailOperation(operationId, exception, $"[{_context.ComponentName}] {errorContext}", memberName);
        }
        
        private object EnrichWithChildContext(object? data)
        {
            return new
            {
                Component = _context.ComponentName,
                ParentCorrelationId = _context.ParentCorrelationId,
                Metadata = _context.Metadata,
                Data = data
            };
        }
        
        public void Dispose()
        {
            _parent.RemoveChildLogger(_context.ComponentName);
        }
    }
    
    #endregion
    
    #region ITradingLogger Implementation
    
    public void LogDebug(string message, object? additionalData = null, 
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _baseOrchestrator.LogDebug(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }
    
    public void LogInfo(string message, object? additionalData = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _baseOrchestrator.LogInfo(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }
    
    public void LogWarning(string message, string? impact = null, string? recommendedAction = null, object? additionalData = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _baseOrchestrator.LogWarning(message, additionalData: additionalData, memberName: memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
    }
    
    public void LogError(string message, Exception? exception = null, string? operationContext = null, string? userImpact = null, string? troubleshootingHints = null, object? additionalData = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _baseOrchestrator.LogError(message, exception, operationContext, additionalData, memberName, sourceFilePath, sourceLineNumber);
    }
    
    public void LogTrade(string symbol, string action, decimal quantity, decimal price, string? orderId = null, 
        string? strategy = null, TimeSpan? executionTime = null, object? marketConditions = null, object? riskMetrics = null,
        [CallerMemberName] string memberName = "")
    {
        _baseOrchestrator.LogTrade(symbol, action, quantity, price, orderId, strategy, executionTime, marketConditions, memberName);
    }
    
    public void LogPositionChange(string symbol, decimal oldPosition, decimal newPosition, string reason, decimal? pnlImpact = null, object? riskImpact = null,
        [CallerMemberName] string memberName = "")
    {
        LogEventCode(TRADE_EXECUTED, $"Position change for {symbol}: {oldPosition} -> {newPosition}, reason: {reason}",
            new { Symbol = symbol, OldPosition = oldPosition, NewPosition = newPosition, Reason = reason, PnlImpact = pnlImpact, RiskImpact = riskImpact },
            LogLevel.Info, memberName);
    }
    
    public void LogPerformance(string operation, TimeSpan duration, bool success = true, double? throughput = null, object? resourceUsage = null, object? businessMetrics = null, TimeSpan? comparisonTarget = null,
        [CallerMemberName] string memberName = "")
    {
        _baseOrchestrator.LogPerformance(operation, duration, success, businessMetrics, memberName);
    }
    
    public void LogHealth(string component, string status, object? metrics = null, string[]? alerts = null, string[]? recommendedActions = null,
        [CallerMemberName] string memberName = "")
    {
        LogEventCode(HEALTH_CHECK_PASSED, $"Health check for {component}: {status}",
            new { Component = component, Status = status, Metrics = metrics, Alerts = alerts, RecommendedActions = recommendedActions },
            LogLevel.Info, memberName);
    }
    
    public void LogRisk(string riskType, string severity, string description, decimal? currentExposure = null, decimal? riskLimit = null, string[]? mitigationActions = null, string? regulatoryImplications = null,
        [CallerMemberName] string memberName = "")
    {
        LogEventCode(RISK_WARNING, $"Risk event - {riskType}: {description}",
            new { RiskType = riskType, Severity = severity, Description = description, CurrentExposure = currentExposure, RiskLimit = riskLimit, MitigationActions = mitigationActions, RegulatoryImplications = regulatoryImplications },
            LogLevel.Warning, memberName);
    }
    
    public void LogDataPipeline(string pipeline, string stage, int recordsProcessed, object? dataQuality = null, object? latencyMetrics = null, string[]? errors = null,
        [CallerMemberName] string memberName = "")
    {
        LogEventCode(DATA_INGESTION_COMPLETE, $"Data pipeline {pipeline} at stage {stage}: processed {recordsProcessed} records",
            new { Pipeline = pipeline, Stage = stage, RecordsProcessed = recordsProcessed, DataQuality = dataQuality, LatencyMetrics = latencyMetrics, Errors = errors },
            LogLevel.Info, memberName);
    }
    
    public void LogMarketData(string symbol, string dataType, string source, TimeSpan? latency = null, string? quality = null, object? volume = null,
        [CallerMemberName] string memberName = "")
    {
        LogEventCode(MARKET_DATA_RECEIVED, $"Market data received for {symbol}: {dataType} from {source}",
            new { Symbol = symbol, DataType = dataType, Source = source, Latency = latency, Quality = quality, Volume = volume },
            LogLevel.Debug, memberName);
    }
    
    public void LogMethodEntry(object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "")
    {
        _baseOrchestrator.LogDebug($"→ Entering {memberName}", parameters, memberName, sourceFilePath, 0);
    }
    
    public void LogMethodExit(object? result = null, TimeSpan? executionTime = null, bool success = true,
        [CallerMemberName] string memberName = "")
    {
        var data = new { Result = result, ExecutionTime = executionTime, Success = success };
        _baseOrchestrator.LogDebug($"← Exiting {memberName}", data, memberName);
    }
    
    public string GenerateCorrelationId()
    {
        return _baseOrchestrator.GenerateCorrelationId();
    }
    
    public void SetCorrelationId(string correlationId)
    {
        _baseOrchestrator.SetCorrelationId(correlationId);
    }
    
    public void Dispose()
    {
        _baseOrchestrator?.Dispose();
    }
    
    #endregion
}

/// <summary>
/// Child logger interface
/// </summary>
public interface IChildLogger : IDisposable
{
    void LogInfo(string message, object? data = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    void LogDebug(string message, object? data = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    void LogWarning(string message, object? data = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    void LogError(string message, Exception? exception = null, object? data = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    void LogEvent(string eventCode, string message, object? data = null,
        LogLevel level = LogLevel.Info,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    string StartOperation(string operationName, object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
        
    void CompleteOperation(string operationId, object? result = null,
        [CallerMemberName] string memberName = "");
        
    void FailOperation(string operationId, Exception exception, string? errorContext = null,
        [CallerMemberName] string memberName = "");
}