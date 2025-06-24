using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Moq;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Testing;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Tests.Canonical
{
    /// <summary>
    /// Comprehensive unit tests for CanonicalErrorHandler following canonical test patterns
    /// </summary>
    public class CanonicalErrorHandlerTests : CanonicalTestBase
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly CanonicalErrorHandler _errorHandler;

        public CanonicalErrorHandlerTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ITradingLogger>();
            _errorHandler = new CanonicalErrorHandler(_mockLogger.Object);
        }

        #region HandleError Tests

        [Fact]
        public async Task HandleError_Should_ReturnErrorResultForException()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing error handling for exception");

                // Arrange
                var exception = new InvalidOperationException("Test error");
                var operationContext = "Test operation";

                // Act
                var result = _errorHandler.HandleError<string>(
                    exception,
                    operationContext,
                    "User impact",
                    "Troubleshooting hints"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Result should indicate failure");
                AssertWithLogging(
                    result.Error?.ErrorCode.Contains("InvalidOperationException_Medium") ?? false,
                    true,
                    "Default severity should be Medium (encoded in error code)"
                );
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Test error") ?? false,
                    "Error message should be included"
                );
                AssertWithLogging(
                    result.Error != null,
                    true,
                    "Error should not be null"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task HandleError_Should_DetermineSeverityByExceptionType()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing severity determination by exception type");

                // Test Critical severity
                var criticalEx = new OutOfMemoryException();
                var criticalResult = _errorHandler.HandleError<string>(
                    criticalEx,
                    "Critical op",
                    "System failure",
                    "Restart required"
                );
                AssertWithLogging(
                    criticalResult.Error?.ErrorCode.Contains("OutOfMemoryException_Critical") ?? false,
                    true,
                    "OutOfMemoryException should be Critical (in error code)"
                );

                // Test High severity
                var highEx = new UnauthorizedAccessException();
                var highResult = _errorHandler.HandleError<string>(
                    highEx,
                    "High op",
                    "Access denied",
                    "Check permissions"
                );
                AssertWithLogging(
                    highResult.Error?.ErrorCode.Contains("UnauthorizedAccessException_High") ?? false,
                    true,
                    "UnauthorizedAccessException should be High (in error code)"
                );

                // Test Low severity
                var lowEx = new ArgumentException();
                var lowResult = _errorHandler.HandleError<string>(
                    lowEx,
                    "Low op",
                    "Invalid input",
                    "Check parameters"
                );
                AssertWithLogging(
                    lowResult.Error?.ErrorCode.Contains("ArgumentException_Low") ?? false,
                    true,
                    "ArgumentException should be Low (in error code)"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task HandleError_Should_PreserveStackTrace()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing stack trace preservation");

                // Arrange
                Exception exception;
                try
                {
                    throw new InvalidOperationException("Test with stack");
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                // Act
                var result = _errorHandler.HandleError<string>(
                    exception,
                    "Stack test",
                    "No impact",
                    "Debug info"
                );

                // Assert
                AssertConditionWithLogging(
                    !string.IsNullOrEmpty(result.Error?.Exception?.StackTrace),
                    "Stack trace should be preserved through Exception property"
                );
                AssertConditionWithLogging(
                    result.Error?.Exception?.StackTrace?.Contains("HandleError_Should_PreserveStackTrace") ?? false,
                    "Stack trace should contain test method name"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task HandleError_Should_IncludeTimestamp()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing timestamp inclusion");

                // Arrange
                var beforeTime = DateTime.UtcNow;
                var exception = new Exception("Timestamp test");

                // Act
                var result = _errorHandler.HandleError<string>(
                    exception,
                    "Timestamp operation",
                    "No impact",
                    "Check time"
                );

                var afterTime = DateTime.UtcNow;

                // Assert
                AssertConditionWithLogging(
                    result.Error?.Timestamp >= beforeTime,
                    "Timestamp should be after test start"
                );
                AssertConditionWithLogging(
                    result.Error?.Timestamp <= afterTime,
                    "Timestamp should be before test end"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region HandleErrorAsync Tests

        [Fact]
        public async Task HandleErrorAsync_Should_WorkAsynchronously()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing async error handling");

                // Arrange
                var exception = new InvalidOperationException("Async error");

                // Act
                var result = await _errorHandler.HandleErrorAsync<string>(
                    exception,
                    "Async operation",
                    "User affected",
                    "Try again"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Async result should indicate failure");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Async error") ?? false,
                    "Error message should be preserved in async"
                );
            });
        }

        #endregion

        #region HandleAggregateError Tests

        [Fact]
        public async Task HandleAggregateError_Should_ProcessMultipleExceptions()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing aggregate error handling");

                // Arrange
                var innerExceptions = new List<Exception>
                {
                    new InvalidOperationException("Error 1"),
                    new ArgumentException("Error 2"),
                    new NotImplementedException("Error 3")
                };
                var aggregateEx = new AggregateException(innerExceptions);

                // Act
                var result = _errorHandler.HandleAggregateError<string>(
                    aggregateEx,
                    "Aggregate operation",
                    "Multiple failures",
                    "Check all errors"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Result should indicate failure");
                // Verify aggregate exception is preserved
                AssertConditionWithLogging(
                    result.Error?.Exception is AggregateException,
                    "Exception should be AggregateException"
                );
                
                var aggEx = result.Error?.Exception as AggregateException;
                AssertWithLogging(
                    aggEx?.InnerExceptions.Count ?? 0,
                    3,
                    "Should have 3 inner exceptions"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task HandleAggregateError_Should_DetermineMaxSeverity()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing aggregate error severity determination");

                // Arrange - mix of severities
                var innerExceptions = new List<Exception>
                {
                    new ArgumentException("Low severity"),
                    new OutOfMemoryException("Critical severity"),
                    new InvalidOperationException("Medium severity")
                };
                var aggregateEx = new AggregateException(innerExceptions);

                // Act
                var result = _errorHandler.HandleAggregateError<string>(
                    aggregateEx,
                    "Mixed severity operation",
                    "System critical",
                    "Immediate action required"
                );

                // Assert
                AssertWithLogging(
                    result.Error?.ErrorCode.Contains("AggregateError_Critical") ?? false,
                    true,
                    "Aggregate severity should be the highest (Critical) in error code"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region WrapException Tests

        [Fact]
        public async Task WrapException_Should_AddContext()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing exception wrapping with context");

                // Arrange
                var innerException = new InvalidOperationException("Inner error");
                var additionalContext = "Extra information about the failure";

                // Act
                var wrappedException = _errorHandler.WrapException(
                    innerException,
                    additionalContext,
                    "Wrapped operation",
                    "User sees wrapped error",
                    "Check inner exception"
                );

                // Assert
                AssertConditionWithLogging(
                    wrappedException.Message.Contains(additionalContext),
                    "Wrapped exception should contain additional context"
                );
                AssertWithLogging(
                    wrappedException.InnerException,
                    innerException,
                    "Inner exception should be preserved"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task WrapException_Should_PreserveOriginalException()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing original exception preservation in wrapping");

                // Arrange
                var originalMessage = "Original error message";
                var originalException = new ArgumentNullException("param", originalMessage);

                // Act
                var wrapped = _errorHandler.WrapException(
                    originalException,
                    "Additional context",
                    "Operation",
                    "Impact",
                    "Hints"
                );

                // Assert
                AssertNotNull(wrapped.InnerException, "Inner exception should exist");
                AssertWithLogging(
                    wrapped.InnerException?.GetType(),
                    typeof(ArgumentNullException),
                    "Original exception type should be preserved"
                );
                AssertConditionWithLogging(
                    wrapped.InnerException?.Message.Contains(originalMessage) ?? false,
                    "Original message should be preserved"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region TryExecute Tests

        [Fact]
        public async Task TryExecute_Should_ReturnSuccessForSuccessfulOperation()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing TryExecute with successful operation");

                // Arrange
                var expectedResult = "Success value";

                // Act
                var result = _errorHandler.TryExecute(
                    () => expectedResult,
                    "Successful operation"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Should indicate success");
                AssertWithLogging(result.Value, expectedResult, "Should return the value");
                AssertNull(result.Error, "Should have no error");

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task TryExecute_Should_CatchAndHandleExceptions()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing TryExecute exception handling");

                // Act
                var result = _errorHandler.TryExecute<string>(
                    () => throw new InvalidOperationException("Expected failure"),
                    "Failing operation",
                    "Operation failed",
                    "Retry later"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Should indicate failure");
                AssertNull(result.Value, "Should have no value");
                AssertNotNull(result.Error, "Should have error details");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Expected failure") ?? false,
                    "Error message should be preserved"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region TryExecuteAsync Tests

        [Fact]
        public async Task TryExecuteAsync_Should_HandleAsyncOperations()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing TryExecuteAsync with async operation");

                // Arrange
                var expectedResult = "Async success";

                // Act
                var result = await _errorHandler.TryExecuteAsync(
                    async () =>
                    {
                        await Task.Delay(10);
                        return expectedResult;
                    },
                    "Async operation"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Async should succeed");
                AssertWithLogging(result.Value, expectedResult, "Should return async value");
            });
        }

        [Fact]
        public async Task TryExecuteAsync_Should_HandleAsyncExceptions()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing TryExecuteAsync exception handling");

                // Act
                var result = await _errorHandler.TryExecuteAsync<string>(
                    async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Async failure");
                    },
                    "Async failing operation",
                    "Async operation failed",
                    "Check async configuration"
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Should indicate async failure");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Async failure") ?? false,
                    "Async error message should be preserved"
                );
            });
        }

        #endregion

        #region Error Logging Tests

        [Fact]
        public async Task HandleError_Should_LogErrors()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing error logging");

                // Arrange
                var loggedErrors = new List<string>();
                _mockLogger
                    .Setup(x => x.LogError(
                        It.IsAny<string>(), 
                        It.IsAny<Exception?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()))
                    .Callback<string, Exception?, string?, string?, string?, object?, string, string, int>(
                        (msg, ex, op, impact, hints, data, member, file, line) => loggedErrors.Add(msg));

                var exception = new InvalidOperationException("Log test error");

                // Act
                _errorHandler.HandleError<string>(
                    exception,
                    "Logging operation",
                    "No impact",
                    "Check logs"
                );

                // Assert
                _mockLogger.Verify(
                    x => x.LogError(
                        It.Is<string>(msg => msg.Contains("Log test error")),
                        It.IsAny<Exception?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()
                    ),
                    Times.AtLeastOnce,
                    "Error should be logged"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Retry Policy Tests

        [Fact]
        public async Task TryExecuteWithRetry_Should_RetryOnTransientFailure()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing retry on transient failures");

                // Arrange
                var attempts = 0;
                var expectedResult = "Success after retry";

                // Act
                var result = await _errorHandler.TryExecuteWithRetryAsync(
                    async () =>
                    {
                        attempts++;
                        if (attempts < 3)
                        {
                            throw new InvalidOperationException($"Transient failure {attempts}");
                        }
                        await Task.Delay(10);
                        return expectedResult;
                    },
                    "Retry operation",
                    maxRetries: 3
                );

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Should succeed after retries");
                AssertWithLogging(result.Value, expectedResult, "Should return value after retry");
                AssertWithLogging(attempts, 3, "Should have made 3 attempts");
            });
        }

        [Fact]
        public async Task TryExecuteWithRetry_Should_FailAfterMaxRetries()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing failure after max retries");

                // Arrange
                var attempts = 0;

                // Act
                var result = await _errorHandler.TryExecuteWithRetryAsync<string>(
                    async () =>
                    {
                        attempts++;
                        await Task.Delay(10);
                        throw new InvalidOperationException($"Permanent failure {attempts}");
                    },
                    "Permanent failure operation",
                    maxRetries: 2
                );

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Should fail after max retries");
                AssertWithLogging(attempts, 3, "Should have made 3 attempts (initial + 2 retries)");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Permanent failure") ?? false,
                    "Final error should be preserved"
                );
            });
        }

        #endregion
    }
}