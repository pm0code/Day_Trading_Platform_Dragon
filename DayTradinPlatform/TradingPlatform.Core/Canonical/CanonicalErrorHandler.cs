using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical error handler providing consistent error handling patterns across the platform
    /// </summary>
    public class CanonicalErrorHandler
    {
        private readonly ITradingLogger _logger;

        public CanonicalErrorHandler(ITradingLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles an error and returns a result with error information
        /// </summary>
        public TradingResult<T> HandleError<T>(
            Exception exception,
            string operationContext,
            string userImpact,
            string troubleshootingHints,
            ErrorSeverity? severity = null)
        {
            var determinedSeverity = severity ?? DetermineSeverity(exception);
            
            _logger.LogError(
                $"Operation failed: {exception.Message}",
                exception,
                operationContext,
                userImpact,
                troubleshootingHints,
                new
                {
                    Severity = determinedSeverity,
                    ExceptionType = exception.GetType().Name
                });

            var errorCode = $"{exception.GetType().Name}_{determinedSeverity}";
            
            return TradingResult<T>.Failure(
                errorCode,
                exception.Message,
                exception);
        }

        /// <summary>
        /// Handles an error asynchronously and returns a result with error information
        /// </summary>
        public async Task<TradingResult<T>> HandleErrorAsync<T>(
            Exception exception,
            string operationContext,
            string userImpact,
            string troubleshootingHints,
            ErrorSeverity? severity = null)
        {
            return await Task.FromResult(HandleError<T>(
                exception,
                operationContext,
                userImpact,
                troubleshootingHints,
                severity));
        }

        /// <summary>
        /// Handles an aggregate exception with multiple inner errors
        /// </summary>
        public TradingResult<T> HandleAggregateError<T>(
            AggregateException aggregateException,
            string operationContext,
            string userImpact,
            string troubleshootingHints)
        {
            var innerExceptions = aggregateException.InnerExceptions;
            var maxSeverity = innerExceptions.Any() 
                ? innerExceptions.Max(DetermineSeverity) 
                : ErrorSeverity.Medium;

            _logger.LogError(
                $"Aggregate operation failed with {innerExceptions.Count} errors",
                aggregateException,
                operationContext,
                userImpact,
                troubleshootingHints,
                new
                {
                    ErrorCount = innerExceptions.Count,
                    MaxSeverity = maxSeverity,
                    InnerErrors = innerExceptions.Select(ex => new
                    {
                        Type = ex.GetType().Name,
                        Message = ex.Message,
                        Severity = DetermineSeverity(ex)
                    })
                });

            var errorCode = $"AggregateError_{maxSeverity}";
            
            return TradingResult<T>.Failure(
                errorCode,
                aggregateException.Message,
                aggregateException);
        }

        /// <summary>
        /// Wraps an exception with additional context
        /// </summary>
        public TradingException WrapException(
            Exception innerException,
            string additionalContext,
            string operationContext,
            string userImpact,
            string troubleshootingHints)
        {
            var message = $"{additionalContext}: {innerException.Message}";
            
            _logger.LogError(
                message,
                innerException,
                operationContext,
                userImpact,
                troubleshootingHints,
                new { AdditionalContext = additionalContext });

            return new TradingException(
                message,
                operationContext,
                userImpact,
                troubleshootingHints,
                innerException);
        }

        /// <summary>
        /// Tries to execute an operation and returns a result
        /// </summary>
        public TradingResult<T> TryExecute<T>(
            Func<T> operation,
            string operationContext,
            string userImpact = "Operation could not be completed",
            string troubleshootingHints = "Check logs for details")
        {
            try
            {
                var result = operation();
                return TradingResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                return HandleError<T>(ex, operationContext, userImpact, troubleshootingHints);
            }
        }

        /// <summary>
        /// Tries to execute an async operation and returns a result
        /// </summary>
        public async Task<TradingResult<T>> TryExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationContext,
            string userImpact = "Operation could not be completed",
            string troubleshootingHints = "Check logs for details")
        {
            try
            {
                var result = await operation();
                return TradingResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                return HandleError<T>(ex, operationContext, userImpact, troubleshootingHints);
            }
        }

        /// <summary>
        /// Tries to execute an operation with retry logic
        /// </summary>
        public async Task<TradingResult<T>> TryExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationContext,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            string userImpact = "Operation could not be completed after retries",
            string troubleshootingHints = "Check logs for details")
        {
            var delay = initialDelay ?? TimeSpan.FromSeconds(1);
            var lastException = default(Exception);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();
                    return TradingResult<T>.Success(result);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning(
                            $"Attempt {attempt}/{maxRetries} failed for {operationContext}. Retrying in {delay.TotalSeconds}s",
                            $"Temporary failure in {operationContext}",
                            "System will retry automatically",
                            new { Attempt = attempt, MaxRetries = maxRetries, Exception = ex.Message });
                        
                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
                    }
                }
            }

            return HandleError<T>(
                lastException ?? new InvalidOperationException("Operation failed"),
                operationContext,
                userImpact,
                troubleshootingHints);
        }

        /// <summary>
        /// Determines error severity based on exception type
        /// </summary>
        private ErrorSeverity DetermineSeverity(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                StackOverflowException => ErrorSeverity.Critical,
                AccessViolationException => ErrorSeverity.Critical,
                
                UnauthorizedAccessException => ErrorSeverity.High,
                TimeoutException => ErrorSeverity.High,
                AggregateException ae when ae.InnerExceptions.Any(e => DetermineSeverity(e) == ErrorSeverity.Critical) => ErrorSeverity.Critical,
                AggregateException ae when ae.InnerExceptions.Any(e => DetermineSeverity(e) == ErrorSeverity.High) => ErrorSeverity.High,
                
                InvalidOperationException => ErrorSeverity.Medium,
                NotSupportedException => ErrorSeverity.Medium,
                NotImplementedException => ErrorSeverity.Medium,
                
                ArgumentNullException => ErrorSeverity.Low,
                ArgumentOutOfRangeException => ErrorSeverity.Low,
                ArgumentException => ErrorSeverity.Low,
                
                _ => ErrorSeverity.Medium
            };
        }
    }

    /// <summary>
    /// Error severity levels for categorization
    /// </summary>
    public enum ErrorSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Custom exception for trading platform operations
    /// </summary>
    public class TradingException : Exception
    {
        public string OperationContext { get; }
        public string UserImpact { get; }
        public string TroubleshootingHints { get; }

        public TradingException(
            string message,
            string operationContext,
            string userImpact,
            string troubleshootingHints,
            Exception? innerException = null)
            : base(message, innerException)
        {
            OperationContext = operationContext;
            UserImpact = userImpact;
            TroubleshootingHints = troubleshootingHints;
        }
    }
}