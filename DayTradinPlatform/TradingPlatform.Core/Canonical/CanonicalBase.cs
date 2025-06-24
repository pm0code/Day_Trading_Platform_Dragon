using System;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Base class for all canonical implementations in the trading platform.
    /// Provides standardized logging, error handling, and progress reporting.
    /// </summary>
    public abstract class CanonicalBase
    {
        /// <summary>
        /// The logger instance for this component
        /// </summary>
        protected ITradingLogger Logger { get; }
        
        /// <summary>
        /// The component name for logging context
        /// </summary>
        protected string ComponentName { get; }
        
        /// <summary>
        /// The correlation ID for this instance
        /// </summary>
        protected string CorrelationId { get; }

        protected CanonicalBase(
            ITradingLogger logger,
            string componentName)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ComponentName = componentName;
            CorrelationId = Guid.NewGuid().ToString();
            
            LogMethodEntry();
        }

        #region Canonical Logging Methods

        /// <summary>
        /// Logs method entry with automatic method name detection
        /// </summary>
        protected void LogMethodEntry(
            object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            Logger.LogMethodEntry(parameters, memberName, sourceFilePath);
        }

        /// <summary>
        /// Logs method exit with automatic method name detection
        /// </summary>
        protected void LogMethodExit(
            object? result = null,
            TimeSpan? duration = null,
            bool success = true,
            [CallerMemberName] string memberName = "")
        {
            Logger.LogMethodExit(result, duration, success, memberName);
        }

        /// <summary>
        /// Logs informational message with context
        /// </summary>
        protected void LogInfo(
            string message,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogInfo(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs debug message with context
        /// </summary>
        protected void LogDebug(
            string message,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogDebug(message, additionalData, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs warning message with context
        /// </summary>
        protected void LogWarning(
            string message,
            string? impact = null,
            string? troubleshooting = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogWarning(message, impact, troubleshooting, additionalData, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs error with full canonical context
        /// </summary>
        protected void LogError(
            string message,
            Exception? exception = null,
            string? operationContext = null,
            string? userImpact = null,
            string? troubleshootingHints = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Logger.LogError(message, exception, operationContext, userImpact, troubleshootingHints, additionalData, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs progress for long-running operations
        /// </summary>
        protected void LogProgress(
            string operation,
            int currentStep,
            int totalSteps,
            string? currentStepDescription = null,
            TimeSpan? estimatedTimeRemaining = null,
            [CallerMemberName] string methodName = "")
        {
            var percentComplete = totalSteps > 0 ? (currentStep * 100.0 / totalSteps) : 0;
            
            var progressInfo = new
            {
                Operation = operation,
                CurrentStep = currentStep,
                TotalSteps = totalSteps,
                PercentComplete = percentComplete,
                Description = currentStepDescription,
                EstimatedTimeRemaining = estimatedTimeRemaining?.ToString(@"mm\:ss"),
                MethodName = methodName
            };

            LogInfo($"Progress: {operation} - {percentComplete:F1}% complete", progressInfo);
        }

        #endregion

        #region Canonical Error Handling

        /// <summary>
        /// Executes an operation with canonical error handling and logging
        /// </summary>
        protected async Task<T> ExecuteWithLoggingAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            string userImpactOnFailure,
            string troubleshootingHints,
            [CallerMemberName] string methodName = "")
        {
            LogMethodEntry(methodName);
            
            try
            {
                LogDebug($"Starting: {operationDescription}");
                var result = await operation();
                LogDebug($"Completed: {operationDescription}");
                return result;
            }
            catch (Exception ex)
            {
                LogError(
                    $"Failed: {operationDescription}",
                    ex,
                    operationDescription,
                    userImpactOnFailure,
                    troubleshootingHints,
                    new { MethodName = methodName });
                throw;
            }
            finally
            {
                LogMethodExit(methodName);
            }
        }

        /// <summary>
        /// Executes an operation with canonical error handling and logging (non-async)
        /// </summary>
        protected T ExecuteWithLogging<T>(
            Func<T> operation,
            string operationDescription,
            string userImpactOnFailure,
            string troubleshootingHints,
            [CallerMemberName] string methodName = "")
        {
            LogMethodEntry(methodName);
            
            try
            {
                LogDebug($"Starting: {operationDescription}");
                var result = operation();
                LogDebug($"Completed: {operationDescription}");
                return result;
            }
            catch (Exception ex)
            {
                LogError(
                    $"Failed: {operationDescription}",
                    ex,
                    operationDescription,
                    userImpactOnFailure,
                    troubleshootingHints,
                    new { MethodName = methodName });
                throw;
            }
            finally
            {
                LogMethodExit(methodName);
            }
        }

        /// <summary>
        /// Executes an operation with retry logic and canonical logging
        /// </summary>
        protected async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            int maxRetries = 3,
            TimeSpan? retryDelay = null,
            [CallerMemberName] string methodName = "")
        {
            var delay = retryDelay ?? TimeSpan.FromSeconds(1);
            var attempt = 0;
            
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    LogDebug($"Attempt {attempt}/{maxRetries}: {operationDescription}");
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    LogWarning(
                        $"Attempt {attempt} failed: {operationDescription}. Retrying in {delay.TotalSeconds}s",
                        additionalData: new { Exception = ex.Message, Attempt = attempt });
                    
                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
                }
            }
            
            // Final attempt without catching
            return await operation();
        }

        #endregion

        #region Canonical Validation

        /// <summary>
        /// Validates a parameter and logs if invalid
        /// </summary>
        protected void ValidateParameter<T>(
            T parameter,
            string parameterName,
            Func<T, bool> validationFunc,
            string validationErrorMessage,
            [CallerMemberName] string methodName = "")
        {
            if (!validationFunc(parameter))
            {
                var message = $"Invalid parameter '{parameterName}': {validationErrorMessage}";
                LogError(
                    message,
                    null,
                    $"{methodName} validation",
                    "Operation cannot proceed with invalid parameters",
                    $"Ensure {parameterName} meets requirements: {validationErrorMessage}",
                    new { ParameterName = parameterName, ParameterValue = parameter });
                
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Validates that a string parameter is not null or empty
        /// </summary>
        protected void ValidateNotNullOrEmpty(
            string parameter,
            string parameterName,
            [CallerMemberName] string methodName = "")
        {
            ValidateParameter(
                parameter,
                parameterName,
                p => !string.IsNullOrWhiteSpace(p),
                "Value cannot be null or empty",
                methodName);
        }

        /// <summary>
        /// Validates that an object parameter is not null
        /// </summary>
        protected void ValidateNotNull<T>(
            T parameter,
            string parameterName,
            [CallerMemberName] string methodName = "") where T : class
        {
            ValidateParameter(
                parameter,
                parameterName,
                p => p != null,
                "Value cannot be null",
                methodName);
        }

        #endregion

        #region Canonical Performance Tracking

        /// <summary>
        /// Tracks performance of an operation
        /// </summary>
        protected async Task<T> TrackPerformanceAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            TimeSpan? warningThreshold = null,
            [CallerMemberName] string methodName = "")
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var result = await operation();
                stopwatch.Stop();
                
                var performanceData = new
                {
                    Operation = operationName,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    ElapsedFormatted = stopwatch.Elapsed.ToString(@"mm\:ss\.fff")
                };
                
                if (warningThreshold.HasValue && stopwatch.Elapsed > warningThreshold.Value)
                {
                    LogWarning(
                        $"Performance warning: {operationName} took {stopwatch.ElapsedMilliseconds}ms (threshold: {warningThreshold.Value.TotalMilliseconds}ms)",
                        additionalData: performanceData);
                }
                else
                {
                    LogDebug($"Performance: {operationName} completed in {stopwatch.ElapsedMilliseconds}ms", performanceData);
                }
                
                return result;
            }
            catch
            {
                stopwatch.Stop();
                LogError(
                    $"Performance tracking: {operationName} failed after {stopwatch.ElapsedMilliseconds}ms",
                    additionalData: new { ElapsedMs = stopwatch.ElapsedMilliseconds });
                throw;
            }
        }

        #endregion
    }
}