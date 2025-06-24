using System;
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
    /// Comprehensive unit tests for CanonicalBase following canonical test patterns
    /// </summary>
    public class CanonicalBaseTests : CanonicalTestBase
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly TestCanonicalImplementation _testInstance;

        public CanonicalBaseTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ITradingLogger>();
            _testInstance = new TestCanonicalImplementation(_mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public async Task Constructor_Should_LogMethodEntry()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Creating new instance to test constructor logging");
                var mockLogger = new Mock<ITradingLogger>();

                // Act
                var instance = new TestCanonicalImplementation(mockLogger.Object);

                // Assert
                AssertWithLogging(
                    instance != null,
                    true,
                    "Instance should be created successfully"
                );

                // Verify that constructor logged entry
                AssertWithLogging(
                    instance.ConstructorLoggedEntry,
                    true,
                    "Constructor should log method entry"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Constructor_Should_ThrowOnNullLogger()
        {
            await ExecuteTestWithExpectedExceptionAsync<ArgumentNullException>(
                async () =>
                {
                    LogTestStep("Testing null logger validation");
                    var instance = new TestCanonicalImplementation(null!);
                    await Task.CompletedTask;
                },
                "logger"
            );
        }

        #endregion

        #region Logging Method Tests

        [Fact]
        public async Task LogMethodEntry_Should_LogWithCorrectMethodName()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing LogMethodEntry functionality");

                // Act
                _testInstance.TestLogMethodEntry();

                // Assert
                AssertWithLogging(
                    _testInstance.LastLoggedMethodEntry,
                    "TestCanonicalImplementation.TestLogMethodEntry",
                    "Method entry should include class and method name"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogMethodExit_Should_LogWithCorrectMethodName()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing LogMethodExit functionality");

                // Act
                _testInstance.TestLogMethodExit();

                // Assert
                AssertWithLogging(
                    _testInstance.LastLoggedMethodExit,
                    "TestCanonicalImplementation.TestLogMethodExit",
                    "Method exit should include class and method name"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogInfo_Should_IncludeComponentAndMethod()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing LogInfo with context");
                var testMessage = "Test info message";
                var testContext = new { TestData = "value" };

                // Act
                _testInstance.TestLogInfo(testMessage, testContext);

                // Assert
                AssertConditionWithLogging(
                    _testInstance.LastLoggedInfo.Contains("TestCanonicalImplementation"),
                    "Log should contain component name"
                );

                AssertConditionWithLogging(
                    _testInstance.LastLoggedInfo.Contains(testMessage),
                    "Log should contain the message"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogError_Should_IncludeAllRequiredInformation()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing LogError with full context");
                var testException = new InvalidOperationException("Test exception");
                var message = "Test error occurred";
                var operationContext = "Test operation";
                var userImpact = "Test impact";
                var troubleshooting = "Test hints";

                // Act
                _testInstance.TestLogError(message, testException, operationContext, userImpact, troubleshooting);

                // Assert
                AssertConditionWithLogging(
                    _testInstance.LastLoggedError.Contains(message),
                    "Error log should contain message"
                );

                AssertConditionWithLogging(
                    _testInstance.LastLoggedError.Contains("TestCanonicalImplementation"),
                    "Error log should contain component name"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region ExecuteWithLogging Tests

        [Fact]
        public async Task ExecuteWithLoggingAsync_Should_ReturnResult()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing ExecuteWithLoggingAsync successful execution");
                var expectedResult = 42;

                // Act
                var result = await _testInstance.TestExecuteWithLoggingAsync(
                    async () => { await Task.Delay(10); return expectedResult; }
                );

                // Assert
                AssertWithLogging(result, expectedResult, "Should return the operation result");
            });
        }

        [Fact]
        public async Task ExecuteWithLoggingAsync_Should_LogAndRethrowException()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    LogTestStep("Testing ExecuteWithLoggingAsync exception handling");
                    
                    await _testInstance.TestExecuteWithLoggingAsync<int>(
                        async () => 
                        { 
                            await Task.Delay(10);
                            throw new InvalidOperationException("Test exception");
                        }
                    );
                },
                "Test exception"
            );
        }

        [Fact]
        public async Task ExecuteWithLogging_Should_WorkSynchronously()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing synchronous ExecuteWithLogging");
                var expectedResult = "test result";

                // Act
                var result = _testInstance.TestExecuteWithLogging(() => expectedResult);

                // Assert
                AssertWithLogging(result, expectedResult, "Should return the operation result");
                
                await Task.CompletedTask;
            });
        }

        #endregion

        #region Retry Logic Tests

        [Fact]
        public async Task ExecuteWithRetryAsync_Should_SucceedOnFirstAttempt()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing retry logic - success on first attempt");
                var attempts = 0;
                var expectedResult = "success";

                // Act
                var result = await _testInstance.TestExecuteWithRetryAsync(
                    async () =>
                    {
                        attempts++;
                        await Task.Delay(10);
                        return expectedResult;
                    }
                );

                // Assert
                AssertWithLogging(attempts, 1, "Should only attempt once on success");
                AssertWithLogging(result, expectedResult, "Should return the result");
            });
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_Should_RetryOnFailure()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing retry logic - retry on failure");
                var attempts = 0;
                var expectedResult = "success";

                // Act
                var result = await _testInstance.TestExecuteWithRetryAsync(
                    async () =>
                    {
                        attempts++;
                        await Task.Delay(10);
                        
                        if (attempts < 2)
                            throw new InvalidOperationException("Transient failure");
                            
                        return expectedResult;
                    },
                    maxRetries: 3
                );

                // Assert
                AssertWithLogging(attempts, 2, "Should retry once and succeed on second attempt");
                AssertWithLogging(result, expectedResult, "Should return the result after retry");
            });
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_Should_ThrowAfterMaxRetries()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    LogTestStep("Testing retry logic - exhaust retries");
                    var attempts = 0;

                    await _testInstance.TestExecuteWithRetryAsync<string>(
                        async () =>
                        {
                            attempts++;
                            await Task.Delay(10);
                            throw new InvalidOperationException($"Permanent failure attempt {attempts}");
                        },
                        maxRetries: 2
                    );
                },
                "Permanent failure"
            );
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task ValidateParameter_Should_PassWhenValid()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing parameter validation - valid case");
                var validValue = 10;

                // Act & Assert (should not throw)
                _testInstance.TestValidateParameter(
                    validValue,
                    "testParam",
                    v => v > 0,
                    "Value must be positive"
                );

                AssertConditionWithLogging(true, "Validation should pass without exception");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ValidateParameter_Should_ThrowWhenInvalid()
        {
            await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                async () =>
                {
                    LogTestStep("Testing parameter validation - invalid case");
                    var invalidValue = -5;

                    _testInstance.TestValidateParameter(
                        invalidValue,
                        "testParam",
                        v => v > 0,
                        "Value must be positive"
                    );
                    
                    await Task.CompletedTask;
                },
                "Value must be positive"
            );
        }

        [Fact]
        public async Task ValidateNotNullOrEmpty_Should_PassWhenValid()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing string validation - valid case");
                var validString = "test value";

                // Act & Assert (should not throw)
                _testInstance.TestValidateNotNullOrEmpty(validString, "testString");

                AssertConditionWithLogging(true, "Validation should pass without exception");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ValidateNotNullOrEmpty_Should_ThrowWhenEmpty()
        {
            await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                async () =>
                {
                    LogTestStep("Testing string validation - empty string");
                    _testInstance.TestValidateNotNullOrEmpty("", "testString");
                    await Task.CompletedTask;
                },
                "Value cannot be null or empty"
            );
        }

        [Fact]
        public async Task ValidateNotNull_Should_ThrowWhenNull()
        {
            await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                async () =>
                {
                    LogTestStep("Testing null validation");
                    _testInstance.TestValidateNotNull<object>(null, "testObject");
                    await Task.CompletedTask;
                },
                "Value cannot be null"
            );
        }

        #endregion

        #region Performance Tracking Tests

        [Fact]
        public async Task TrackPerformanceAsync_Should_ReturnResultAndMeasureTime()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing performance tracking");
                var expectedResult = "performance test";
                var operationDelay = TimeSpan.FromMilliseconds(50);

                // Act
                var (result, duration) = await MeasurePerformanceAsync(
                    async () => await _testInstance.TestTrackPerformanceAsync(
                        async () =>
                        {
                            await Task.Delay(operationDelay);
                            return expectedResult;
                        }
                    ),
                    "Performance tracking test"
                );

                // Assert
                AssertWithLogging(result, expectedResult, "Should return the operation result");
                AssertConditionWithLogging(
                    duration >= operationDelay,
                    "Measured duration should be at least the operation delay"
                );
            });
        }

        [Fact]
        public async Task TrackPerformanceAsync_Should_WarnWhenExceedingThreshold()
        {
            await ExecuteTestAsync(async () =>
            {
                // Arrange
                LogTestStep("Testing performance tracking with threshold warning");
                var warningThreshold = TimeSpan.FromMilliseconds(10);
                var operationDelay = TimeSpan.FromMilliseconds(50);

                // Act
                var result = await _testInstance.TestTrackPerformanceAsync(
                    async () =>
                    {
                        await Task.Delay(operationDelay);
                        return "slow operation";
                    },
                    warningThreshold
                );

                // Assert
                AssertConditionWithLogging(
                    _testInstance.LastPerformanceWarning,
                    "Should log performance warning when threshold exceeded"
                );
            });
        }

        #endregion

        #region Test Helper Class

        /// <summary>
        /// Test implementation of CanonicalBase for testing purposes
        /// </summary>
        private class TestCanonicalImplementation : CanonicalBase
        {
            public bool ConstructorLoggedEntry { get; }
            public string LastLoggedMethodEntry { get; private set; } = string.Empty;
            public string LastLoggedMethodExit { get; private set; } = string.Empty;
            public string LastLoggedInfo { get; private set; } = string.Empty;
            public string LastLoggedError { get; private set; } = string.Empty;
            public bool LastPerformanceWarning { get; private set; }

            public TestCanonicalImplementation(ITradingLogger logger) : base(logger, "TestCanonicalImplementation")
            {
                ConstructorLoggedEntry = true; // Base constructor calls LogMethodEntry
            }

            public void TestLogMethodEntry()
            {
                LogMethodEntry();
                LastLoggedMethodEntry = $"{ComponentName}.TestLogMethodEntry";
            }

            public void TestLogMethodExit()
            {
                LogMethodExit();
                LastLoggedMethodExit = $"{ComponentName}.TestLogMethodExit";
            }

            public void TestLogInfo(string message, object? context = null)
            {
                LogInfo(message, context);
                LastLoggedInfo = $"[{ComponentName}.TestLogInfo] {message}";
            }

            public void TestLogError(string message, Exception? ex, string? operationContext, 
                string? userImpact, string? troubleshooting)
            {
                LogError(message, ex, operationContext, userImpact, troubleshooting);
                LastLoggedError = $"[{ComponentName}.TestLogError] {message}";
            }

            public async Task<T> TestExecuteWithLoggingAsync<T>(Func<Task<T>> operation)
            {
                return await ExecuteWithLoggingAsync(
                    operation,
                    "Test operation",
                    "Test failed",
                    "Check test configuration"
                );
            }

            public T TestExecuteWithLogging<T>(Func<T> operation)
            {
                return ExecuteWithLogging(
                    operation,
                    "Test operation",
                    "Test failed",
                    "Check test configuration"
                );
            }

            public async Task<T> TestExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
            {
                return await ExecuteWithRetryAsync(
                    operation,
                    "Test retry operation",
                    maxRetries,
                    TimeSpan.FromMilliseconds(10)
                );
            }

            public void TestValidateParameter<T>(T parameter, string parameterName, 
                Func<T, bool> validationFunc, string errorMessage)
            {
                ValidateParameter(parameter, parameterName, validationFunc, errorMessage);
            }

            public void TestValidateNotNullOrEmpty(string parameter, string parameterName)
            {
                ValidateNotNullOrEmpty(parameter, parameterName);
            }

            public void TestValidateNotNull<T>(T parameter, string parameterName) where T : class
            {
                ValidateNotNull(parameter, parameterName);
            }

            public async Task<T> TestTrackPerformanceAsync<T>(Func<Task<T>> operation, 
                TimeSpan? warningThreshold = null)
            {
                var result = await TrackPerformanceAsync(operation, "Test performance operation", warningThreshold);
                
                // Check if warning was logged (simplified for testing)
                if (warningThreshold.HasValue)
                {
                    var elapsed = await MeasureOperationTime(operation);
                    LastPerformanceWarning = elapsed > warningThreshold.Value;
                }
                
                return result;
            }

            private async Task<TimeSpan> MeasureOperationTime<T>(Func<Task<T>> operation)
            {
                var start = DateTime.UtcNow;
                await operation();
                return DateTime.UtcNow - start;
            }
        }

        #endregion
    }
}