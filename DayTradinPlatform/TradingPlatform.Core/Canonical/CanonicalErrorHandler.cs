using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical error handling implementation for consistent error management across the platform
    /// </summary>
    public static class CanonicalErrorHandler
    {
        /// <summary>
        /// Handles an exception with full canonical logging and context
        /// </summary>
        public static void HandleError(
            Exception exception,
            string componentName,
            string operationDescription,
            ErrorSeverity severity = ErrorSeverity.Error,
            string? userImpact = null,
            string? troubleshootingHints = null,
            Dictionary<string, object>? additionalContext = null,
            string? correlationId = null,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            correlationId ??= Guid.NewGuid().ToString();
            
            var errorContext = new Dictionary<string, object>
            {
                ["Component"] = componentName,
                ["Method"] = methodName,
                ["File"] = filePath,
                ["Line"] = lineNumber,
                ["ErrorType"] = exception.GetType().Name,
                ["Severity"] = severity.ToString(),
                ["Timestamp"] = DateTime.UtcNow.ToString("O"),
                ["CorrelationId"] = correlationId
            };

            // Add any additional context
            if (additionalContext != null)
            {
                foreach (var kvp in additionalContext)
                {
                    errorContext[kvp.Key] = kvp.Value;
                }
            }

            // Determine user impact if not provided
            userImpact ??= DetermineUserImpact(exception, severity);
            
            // Determine troubleshooting hints if not provided
            troubleshootingHints ??= DetermineTroubleshootingHints(exception);

            // Log the error using TradingLogOrchestrator
            TradingLogOrchestrator.Instance.LogError(
                $"[{componentName}] {operationDescription} failed: {exception.Message}",
                exception,
                operationDescription,
                userImpact,
                troubleshootingHints,
                errorContext,
                correlationId: correlationId
            );

            // For critical errors, also log to system event log (if available)
            if (severity == ErrorSeverity.Critical)
            {
                LogCriticalError(componentName, exception, errorContext);
            }
        }

        /// <summary>
        /// Wraps an operation with canonical error handling
        /// </summary>
        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation,
            string componentName,
            string operationDescription,
            Func<Exception, T>? fallbackFunc = null,
            string? correlationId = null,
            [CallerMemberName] string methodName = "")
        {
            correlationId ??= Guid.NewGuid().ToString();
            
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                HandleError(
                    ex,
                    componentName,
                    operationDescription,
                    DetermineSeverity(ex),
                    correlationId: correlationId,
                    methodName: methodName);

                if (fallbackFunc != null)
                {
                    TradingLogOrchestrator.Instance.LogWarning(
                        $"[{componentName}] Executing fallback for {operationDescription}",
                        correlationId: correlationId);
                    
                    return fallbackFunc(ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Wraps an operation with canonical error handling (synchronous)
        /// </summary>
        public static T ExecuteWithErrorHandling<T>(
            Func<T> operation,
            string componentName,
            string operationDescription,
            Func<Exception, T>? fallbackFunc = null,
            string? correlationId = null,
            [CallerMemberName] string methodName = "")
        {
            correlationId ??= Guid.NewGuid().ToString();
            
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                HandleError(
                    ex,
                    componentName,
                    operationDescription,
                    DetermineSeverity(ex),
                    correlationId: correlationId,
                    methodName: methodName);

                if (fallbackFunc != null)
                {
                    TradingLogOrchestrator.Instance.LogWarning(
                        $"[{componentName}] Executing fallback for {operationDescription}",
                        correlationId: correlationId);
                    
                    return fallbackFunc(ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Creates a standardized error response
        /// </summary>
        public static ErrorResponse CreateErrorResponse(
            Exception exception,
            string componentName,
            string operationDescription,
            string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString();
            
            return new ErrorResponse
            {
                Success = false,
                ErrorCode = DetermineErrorCode(exception),
                ErrorMessage = exception.Message,
                UserMessage = DetermineUserMessage(exception),
                TechnicalDetails = exception.ToString(),
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Component = componentName,
                Operation = operationDescription,
                Severity = DetermineSeverity(exception).ToString()
            };
        }

        #region Private Helper Methods

        private static ErrorSeverity DetermineSeverity(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                StackOverflowException => ErrorSeverity.Critical,
                AccessViolationException => ErrorSeverity.Critical,
                InvalidOperationException => ErrorSeverity.Error,
                ArgumentException => ErrorSeverity.Warning,
                NotImplementedException => ErrorSeverity.Error,
                TimeoutException => ErrorSeverity.Warning,
                _ => ErrorSeverity.Error
            };
        }

        private static string DetermineUserImpact(Exception exception, ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Critical => "Service is unavailable. Please contact support immediately.",
                ErrorSeverity.Error => "The requested operation could not be completed.",
                ErrorSeverity.Warning => "The operation completed with warnings.",
                _ => "An unexpected error occurred."
            };
        }

        private static string DetermineTroubleshootingHints(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException argNull => $"Ensure the parameter '{argNull.ParamName}' is not null.",
                ArgumentException argEx => $"Check the parameter '{argEx.ParamName}' for valid values.",
                InvalidOperationException => "Ensure the system is in a valid state for this operation.",
                TimeoutException => "The operation timed out. Try again or check network connectivity.",
                UnauthorizedAccessException => "Check your permissions for this operation.",
                NotImplementedException => "This feature is not yet implemented.",
                _ => "Review the error details and logs for more information."
            };
        }

        private static string DetermineErrorCode(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => "ERR_NULL_ARGUMENT",
                ArgumentException => "ERR_INVALID_ARGUMENT",
                InvalidOperationException => "ERR_INVALID_OPERATION",
                TimeoutException => "ERR_TIMEOUT",
                UnauthorizedAccessException => "ERR_UNAUTHORIZED",
                NotImplementedException => "ERR_NOT_IMPLEMENTED",
                OutOfMemoryException => "ERR_OUT_OF_MEMORY",
                _ => "ERR_GENERAL"
            };
        }

        private static string DetermineUserMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentException => "Invalid input provided. Please check your data and try again.",
                TimeoutException => "The operation took too long to complete. Please try again.",
                UnauthorizedAccessException => "You don't have permission to perform this action.",
                NotImplementedException => "This feature is coming soon.",
                _ => "An error occurred while processing your request."
            };
        }

        private static void LogCriticalError(string componentName, Exception exception, Dictionary<string, object> context)
        {
            // In a real implementation, this would write to Windows Event Log or similar
            // For now, we'll use the highest priority logging
            TradingLogOrchestrator.Instance.LogError(
                $"CRITICAL ERROR in {componentName}",
                exception,
                "Critical system error",
                "System stability may be affected",
                "Restart the application and contact support if the issue persists",
                context
            );
        }

        #endregion
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Informational - not really an error
        /// </summary>
        Info,
        
        /// <summary>
        /// Warning - operation completed but with issues
        /// </summary>
        Warning,
        
        /// <summary>
        /// Error - operation failed
        /// </summary>
        Error,
        
        /// <summary>
        /// Critical - system stability affected
        /// </summary>
        Critical
    }

    /// <summary>
    /// Standardized error response structure
    /// </summary>
    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public string TechnicalDetails { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Component { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}