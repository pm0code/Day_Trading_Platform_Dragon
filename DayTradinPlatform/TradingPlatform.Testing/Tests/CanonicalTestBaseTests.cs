using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Testing;

namespace TradingPlatform.Tests.Canonical
{
    /// <summary>
    /// Comprehensive unit tests for CanonicalTestBase to ensure the test framework itself
    /// follows canonical patterns and provides reliable test infrastructure
    /// </summary>
    public class CanonicalTestBaseTests : CanonicalTestBase
    {
        public CanonicalTestBaseTests(ITestOutputHelper output) : base(output)
        {
        }

        #region ExecuteTestAsync Tests

        [Fact]
        public async Task ExecuteTestAsync_Should_ExecuteAndLogTest()
        {
            // This test verifies that ExecuteTestAsync properly executes tests
            var testExecuted = false;
            
            await ExecuteTestAsync(async () =>
            {
                testExecuted = true;
                await Task.Delay(10);
            });

            Assert.True(testExecuted, "Test should have been executed");
        }

        [Fact]
        public async Task ExecuteTestAsync_Should_PropagateExceptions()
        {
            // This test verifies that exceptions are properly propagated
            var exceptionMessage = "Test exception";
            
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await ExecuteTestAsync(async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException(exceptionMessage);
                });
            });
        }

        #endregion

        #region ExecuteTestWithExpectedExceptionAsync Tests

        [Fact]
        public async Task ExecuteTestWithExpectedExceptionAsync_Should_CatchExpectedException()
        {
            await ExecuteTestAsync(async () =>
            {
                // This should complete successfully as the exception is expected
                await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                    async () =>
                    {
                        await Task.Delay(10);
                        throw new ArgumentException("Expected exception");
                    },
                    "Expected"
                );
            });
        }

        [Fact]
        public async Task ExecuteTestWithExpectedExceptionAsync_Should_FailOnWrongExceptionType()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                    async () =>
                    {
                        throw new InvalidOperationException("Wrong exception type");
                    },
                    "Expected"
                );
            });
        }

        [Fact]
        public async Task ExecuteTestWithExpectedExceptionAsync_Should_FailWhenNoException()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestWithExpectedExceptionAsync<ArgumentException>(
                    async () =>
                    {
                        // No exception thrown
                        await Task.Delay(10);
                    },
                    "Expected"
                );
            });
        }

        #endregion

        #region Assertion Helper Tests

        [Fact]
        public async Task AssertWithLogging_Should_PassOnEqualValues()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing AssertWithLogging with equal values");
                
                AssertWithLogging(42, 42, "Values should be equal");
                AssertWithLogging("test", "test", "Strings should be equal");
                AssertWithLogging(true, true, "Booleans should be equal");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task AssertWithLogging_Should_FailOnDifferentValues()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestAsync(async () =>
                {
                    AssertWithLogging(42, 43, "This should fail");
                    await Task.CompletedTask;
                });
            });
        }

        [Fact]
        public async Task AssertConditionWithLogging_Should_PassOnTrueCondition()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing AssertConditionWithLogging with true condition");
                
                AssertConditionWithLogging(1 + 1 == 2, "Math should work");
                AssertConditionWithLogging("test".Length == 4, "String length should be correct");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task AssertConditionWithLogging_Should_FailOnFalseCondition()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestAsync(async () =>
                {
                    AssertConditionWithLogging(1 + 1 == 3, "This should fail");
                    await Task.CompletedTask;
                });
            });
        }

        [Fact]
        public async Task AssertNotNull_Should_PassOnNonNullValue()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing AssertNotNull with non-null value");
                
                AssertNotNull("not null", "String should not be null");
                AssertNotNull(new object(), "Object should not be null");
                // Note: AssertNotNull only works with reference types, not value types like int
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task AssertNotNull_Should_FailOnNullValue()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestAsync(async () =>
                {
                    string? nullString = null;
                    AssertNotNull(nullString, "This should fail");
                    await Task.CompletedTask;
                });
            });
        }

        [Fact]
        public async Task AssertNull_Should_PassOnNullValue()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing AssertNull with null value");
                
                string? nullString = null;
                object? nullObject = null;
                
                AssertNull(nullString, "String should be null");
                AssertNull(nullObject, "Object should be null");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task AssertNull_Should_FailOnNonNullValue()
        {
            await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(async () =>
            {
                await ExecuteTestAsync(async () =>
                {
                    AssertNull("not null", "This should fail");
                    await Task.CompletedTask;
                });
            });
        }

        #endregion

        #region Performance Measurement Tests

        [Fact]
        public async Task MeasurePerformanceAsync_Should_MeasureExecutionTime()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing performance measurement");
                
                var delayTime = TimeSpan.FromMilliseconds(50);
                
                var (result, duration) = await MeasurePerformanceAsync(
                    async () =>
                    {
                        await Task.Delay(delayTime);
                        return "test result";
                    },
                    "Test operation"
                );
                
                AssertWithLogging(result, "test result", "Should return operation result");
                AssertConditionWithLogging(
                    duration >= delayTime,
                    $"Duration ({duration.TotalMilliseconds}ms) should be at least delay time ({delayTime.TotalMilliseconds}ms)"
                );
                AssertConditionWithLogging(
                    duration < delayTime + TimeSpan.FromMilliseconds(500),
                    "Duration should not be too much longer than delay"
                );
            });
        }

        [Fact]
        public async Task MeasurePerformanceAsync_Should_PropagateExceptions()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    await MeasurePerformanceAsync<string>(
                        async () =>
                        {
                            await Task.Delay(10);
                            throw new InvalidOperationException("Performance test exception");
                        },
                        "Failing operation"
                    );
                },
                "Performance test exception"
            );
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task LogTestStep_Should_NotThrow()
        {
            await ExecuteTestAsync(async () =>
            {
                // Should not throw
                LogTestStep("Step 1: Initialize");
                LogTestStep("Step 2: Execute");
                LogTestStep("Step 3: Verify");
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogTestInfo_Should_NotThrow()
        {
            await ExecuteTestAsync(async () =>
            {
                // Should not throw
                LogTestInfo("Test information");
                LogTestInfo("Test with context", new { Value = 42, Name = "Test" });
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogTestWarning_Should_NotThrow()
        {
            await ExecuteTestAsync(async () =>
            {
                // Should not throw
                LogTestWarning("Test warning");
                LogTestWarning("Warning with context", "Minor impact", new { Issue = "Minor", Code = 100 });
                
                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task LogTestError_Should_NotThrow()
        {
            await ExecuteTestAsync(async () =>
            {
                // Should not throw
                LogTestError("Test error");
                LogTestError("Error with exception", new Exception("Test exception"));
                
                await Task.CompletedTask;
            });
        }

        #endregion

        #region Test Context Tests

        [Fact]
        public async Task TestContext_Should_BeAvailable()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing test context availability");
                
                AssertNotNull(TestContext, "TestContext should be available");
                AssertNotNull(TestContext.TestName, "Test name should be available");
                AssertConditionWithLogging(
                    TestContext.TestName.Contains("TestContext_Should_BeAvailable"),
                    "Test name should match current test"
                );
                
                await Task.CompletedTask;
            });
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task CanonicalTestBase_Should_SupportComplexTestScenarios()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing complex scenario support");
                
                // Test 1: Performance measurement with assertions
                var (result, duration) = await MeasurePerformanceAsync(
                    async () =>
                    {
                        await Task.Delay(25);
                        return 42;
                    },
                    "Complex operation"
                );
                
                AssertWithLogging(result, 42, "Should return correct result");
                AssertConditionWithLogging(duration.TotalMilliseconds > 20, "Should take expected time");
                
                // Test 2: Exception handling
                await ExecuteTestWithExpectedExceptionAsync<ArgumentNullException>(
                    async () =>
                    {
                        string? nullValue = null;
                        if (nullValue == null)
                            throw new ArgumentNullException(nameof(nullValue));
                        await Task.CompletedTask;
                    },
                    "nullValue"
                );
                
                // Test 3: Multiple assertions
                AssertWithLogging(1 + 1, 2, "Math test 1");
                AssertWithLogging(2 * 3, 6, "Math test 2");
                AssertConditionWithLogging(10 > 5, "Comparison test");
                
                LogTestInfo("Complex test scenario completed successfully");
            });
        }

        #endregion

        #region Canonical Pattern Compliance Tests

        [Fact]
        public async Task CanonicalTestBase_Should_ProvideComprehensiveLogging()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Verifying comprehensive logging support");
                
                // All logging methods should be available and functional
                LogTestStep("Step logging works");
                LogTestInfo("Info logging works");
                LogTestWarning("Warning logging works");
                LogTestError("Error logging works", new Exception("Test"));
                
                // Performance tracking should be available
                var (_, duration) = await MeasurePerformanceAsync(
                    async () => { await Task.Delay(10); return true; },
                    "Performance tracking"
                );
                
                AssertConditionWithLogging(
                    duration.TotalMilliseconds > 0,
                    "Performance should be tracked"
                );
            });
        }

        [Fact]
        public async Task CanonicalTestBase_Should_ProvideStructuredTestExecution()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Verifying structured test execution");
                
                var testPhases = new[] { "Arrange", "Act", "Assert" };
                
                foreach (var phase in testPhases)
                {
                    LogTestStep($"Phase: {phase}");
                    await Task.Delay(5); // Simulate work
                }
                
                AssertConditionWithLogging(true, "All phases executed");
            });
        }

        #endregion
    }
}