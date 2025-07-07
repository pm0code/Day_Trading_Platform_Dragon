using System;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Enhanced base class for all canonical implementations with MCP standards compliance.
    /// Uses TradingLogOrchestratorEnhanced for SCREAMING_SNAKE_CASE event codes and operation tracking.
    /// </summary>
    public abstract class CanonicalBaseEnhanced : CanonicalBase
    {
        /// <summary>
        /// Enhanced logger instance with MCP standards
        /// </summary>
        protected new TradingLogOrchestratorEnhanced Logger { get; }
        
        /// <summary>
        /// Child logger for component isolation (optional)
        /// </summary>
        protected IChildLogger? ChildLogger { get; private set; }

        protected CanonicalBaseEnhanced(
            string componentName,
            bool createChildLogger = false) 
            : base(TradingLogOrchestratorEnhanced.Instance, componentName)
        {
            Logger = TradingLogOrchestratorEnhanced.Instance;
            
            if (createChildLogger)
            {
                ChildLogger = Logger.CreateChildLogger(componentName, 
                    new Dictionary<string, string> 
                    { 
                        ["CorrelationId"] = CorrelationId,
                        ["ComponentType"] = GetType().Name
                    });
            }
            
            // Log initialization with event code
            Logger.LogEventCode(
                TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                $"{componentName} initialized with enhanced logging",
                new { ComponentName = componentName, HasChildLogger = createChildLogger });
        }

        #region Enhanced Operation Tracking

        /// <summary>
        /// Tracks an operation with automatic timing and event codes
        /// </summary>
        protected async Task<T> TrackOperationAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            return await Logger.TrackOperationAsync(
                operationName, 
                operation, 
                parameters, 
                memberName, 
                sourceFilePath, 
                sourceLineNumber);
        }

        /// <summary>
        /// Tracks an operation with automatic timing and event codes (synchronous)
        /// </summary>
        protected T TrackOperation<T>(
            string operationName,
            Func<T> operation,
            object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            return Logger.TrackOperation(
                operationName, 
                operation, 
                parameters, 
                memberName, 
                sourceFilePath, 
                sourceLineNumber);
        }

        /// <summary>
        /// Starts a manual operation tracking
        /// </summary>
        protected string StartOperation(
            string operationName,
            object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            return Logger.StartOperation(operationName, parameters, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Completes a manually tracked operation
        /// </summary>
        protected void CompleteOperation(
            string operationId,
            object? result = null,
            [CallerMemberName] string memberName = "")
        {
            Logger.CompleteOperation(operationId, result, memberName);
        }

        /// <summary>
        /// Fails a manually tracked operation
        /// </summary>
        protected void FailOperation(
            string operationId,
            Exception exception,
            string? errorContext = null,
            [CallerMemberName] string memberName = "")
        {
            Logger.FailOperation(operationId, exception, errorContext, memberName);
        }

        #endregion

        #region Event Code Logging

        /// <summary>
        /// Logs with SCREAMING_SNAKE_CASE event code
        /// </summary>
        protected void LogEvent(
            string eventCode,
            string message,
            object? data = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (ChildLogger != null)
            {
                ChildLogger.LogEvent(eventCode, message, data, level, memberName, sourceFilePath, sourceLineNumber);
            }
            else
            {
                Logger.LogEventCode(eventCode, message, data, level, memberName, sourceFilePath, sourceLineNumber);
            }
        }

        /// <summary>
        /// Logs a trade event with event code
        /// </summary>
        protected void LogTradeEvent(
            string eventCode,
            string symbol,
            string action,
            decimal quantity,
            decimal price,
            string? orderId = null,
            string? strategy = null,
            TimeSpan? executionTime = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "")
        {
            Logger.LogTradeEvent(eventCode, symbol, action, quantity, price, orderId, strategy, executionTime, additionalData, memberName);
        }

        #endregion

        #region Enhanced Performance Tracking

        /// <summary>
        /// Tracks performance with automatic event logging for threshold violations
        /// </summary>
        protected new async Task<T> TrackPerformanceAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            TimeSpan? warningThreshold = null,
            TimeSpan? criticalThreshold = null,
            [CallerMemberName] string methodName = "")
        {
            var operationId = StartOperation(operationName, new { WarningThreshold = warningThreshold, CriticalThreshold = criticalThreshold }, methodName);
            
            try
            {
                var result = await operation();
                CompleteOperation(operationId, result, methodName);
                
                // Check if we need to log performance events based on duration
                if (_activeOperations.TryGetValue(operationId, out var context))
                {
                    var duration = DateTime.UtcNow - context.StartTime;
                    
                    if (criticalThreshold.HasValue && duration > criticalThreshold.Value)
                    {
                        LogEvent(
                            TradingLogOrchestratorEnhanced.LATENCY_SPIKE,
                            $"Critical latency threshold exceeded for {operationName}",
                            new { Duration = duration, Threshold = criticalThreshold },
                            LogLevel.Error);
                    }
                    else if (warningThreshold.HasValue && duration > warningThreshold.Value)
                    {
                        LogEvent(
                            TradingLogOrchestratorEnhanced.PERFORMANCE_DEGRADATION,
                            $"Warning latency threshold exceeded for {operationName}",
                            new { Duration = duration, Threshold = warningThreshold },
                            LogLevel.Warning);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                FailOperation(operationId, ex);
                throw;
            }
        }

        #endregion

        #region Risk and Health Event Logging

        /// <summary>
        /// Logs a risk event
        /// </summary>
        protected void LogRiskEvent(
            string message,
            decimal currentValue,
            decimal limitValue,
            string riskType,
            object? additionalData = null,
            [CallerMemberName] string memberName = "")
        {
            var eventCode = currentValue >= limitValue 
                ? TradingLogOrchestratorEnhanced.RISK_LIMIT_BREACH 
                : TradingLogOrchestratorEnhanced.RISK_WARNING;
            
            LogEvent(
                eventCode,
                message,
                new
                {
                    RiskType = riskType,
                    CurrentValue = currentValue,
                    LimitValue = limitValue,
                    BreachAmount = currentValue - limitValue,
                    BreachPercentage = ((currentValue - limitValue) / limitValue) * 100,
                    AdditionalData = additionalData
                },
                currentValue >= limitValue ? LogLevel.Error : LogLevel.Warning,
                memberName);
        }

        /// <summary>
        /// Logs a health check result
        /// </summary>
        protected void LogHealthCheck(
            bool passed,
            string checkName,
            string message,
            object? details = null,
            [CallerMemberName] string memberName = "")
        {
            var eventCode = passed 
                ? TradingLogOrchestratorEnhanced.HEALTH_CHECK_PASSED 
                : TradingLogOrchestratorEnhanced.HEALTH_CHECK_FAILED;
            
            LogEvent(
                eventCode,
                $"Health check '{checkName}': {message}",
                new
                {
                    CheckName = checkName,
                    Passed = passed,
                    Details = details
                },
                passed ? LogLevel.Info : LogLevel.Error,
                memberName);
        }

        #endregion

        #region IDisposable Enhancement

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Log component shutdown
                Logger.LogEventCode(
                    TradingLogOrchestratorEnhanced.COMPONENT_FAILED,
                    $"{ComponentName} shutting down",
                    new { ComponentName, CorrelationId });
                
                // Dispose child logger if created
                ChildLogger?.Dispose();
            }
        }

        #endregion

        // Store active operations for performance threshold checking
        private readonly Dictionary<string, (DateTime StartTime, string OperationName)> _activeOperations = new();
    }
}