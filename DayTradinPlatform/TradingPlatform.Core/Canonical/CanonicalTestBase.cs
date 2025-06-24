using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all unit tests in the trading platform.
    /// Provides standardized test execution, logging, and verification patterns.
    /// </summary>
    public abstract class CanonicalTestBase : IDisposable
    {
        protected readonly ITestOutputHelper _output;
        protected readonly string _testClassName;
        protected readonly string _correlationId;
        protected readonly Stopwatch _testStopwatch;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly List<string> _testArtifacts;

        protected CanonicalTestBase(ITestOutputHelper output, [CallerFilePath] string sourceFilePath = "")
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _testClassName = GetType().Name;
            _correlationId = Guid.NewGuid().ToString();
            _testStopwatch = Stopwatch.StartNew();
            _testArtifacts = new List<string>();

            // Setup services
            var services = new ServiceCollection();
            ConfigureTestServices(services);
            _serviceProvider = services.BuildServiceProvider();

            LogTestStart(sourceFilePath);
        }

        #region Canonical Test Logging

        /// <summary>
        /// Logs test method entry with automatic method name detection
        /// </summary>
        protected void LogTestMethodStart([CallerMemberName] string testMethodName = "")
        {
            var message = $"[TEST START] {_testClassName}.{testMethodName}";
            _output.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
            
            TradingLogOrchestrator.Instance.LogInfo(
                message,
                new 
                { 
                    TestClass = _testClassName,
                    TestMethod = testMethodName,
                    CorrelationId = _correlationId 
                });
        }

        /// <summary>
        /// Logs test method completion
        /// </summary>
        protected void LogTestMethodEnd(
            bool passed = true,
            string? message = null,
            [CallerMemberName] string testMethodName = "")
        {
            _testStopwatch.Stop();
            var status = passed ? "PASSED" : "FAILED";
            var logMessage = $"[TEST {status}] {_testClassName}.{testMethodName} - Duration: {_testStopwatch.ElapsedMilliseconds}ms";
            
            if (!string.IsNullOrEmpty(message))
            {
                logMessage += $" - {message}";
            }

            _output.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {logMessage}");
            
            TradingLogOrchestrator.Instance.LogInfo(
                logMessage,
                new 
                { 
                    TestClass = _testClassName,
                    TestMethod = testMethodName,
                    Status = status,
                    DurationMs = _testStopwatch.ElapsedMilliseconds,
                    CorrelationId = _correlationId,
                    Message = message
                });
        }

        /// <summary>
        /// Logs test step for detailed tracking
        /// </summary>
        protected void LogTestStep(string stepDescription, object? context = null)
        {
            var message = $"[TEST STEP] {stepDescription}";
            _output.WriteLine($"  → {message}");
            
            TradingLogOrchestrator.Instance.LogDebug(
                $"{_testClassName} - {message}",
                context ?? new { Step = stepDescription },
                correlationId: _correlationId);
        }

        /// <summary>
        /// Logs test data for transparency
        /// </summary>
        protected void LogTestData(string dataDescription, object data)
        {
            var message = $"[TEST DATA] {dataDescription}";
            _output.WriteLine($"  ⚡ {message}: {Newtonsoft.Json.JsonConvert.SerializeObject(data)}");
            
            TradingLogOrchestrator.Instance.LogDebug(
                $"{_testClassName} - {message}",
                data,
                correlationId: _correlationId);
        }

        /// <summary>
        /// Logs test assertion
        /// </summary>
        protected void LogAssertion(string assertionDescription, bool passed, object? actual = null, object? expected = null)
        {
            var status = passed ? "✓" : "✗";
            var message = $"[ASSERT {status}] {assertionDescription}";
            _output.WriteLine($"  {message}");
            
            if (!passed)
            {
                _output.WriteLine($"    Expected: {expected}");
                _output.WriteLine($"    Actual: {actual}");
            }

            TradingLogOrchestrator.Instance.LogDebug(
                $"{_testClassName} - {message}",
                new { Assertion = assertionDescription, Passed = passed, Expected = expected, Actual = actual },
                correlationId: _correlationId);
        }

        #endregion

        #region Canonical Test Execution Patterns

        /// <summary>
        /// Executes a test with canonical error handling and logging
        /// </summary>
        protected async Task ExecuteTestAsync(
            Func<Task> testAction,
            [CallerMemberName] string testMethodName = "")
        {
            LogTestMethodStart(testMethodName);
            
            try
            {
                await testAction();
                LogTestMethodEnd(true, testMethodName: testMethodName);
            }
            catch (Exception ex)
            {
                LogTestMethodEnd(false, ex.Message, testMethodName);
                
                TradingLogOrchestrator.Instance.LogError(
                    $"Test failed: {_testClassName}.{testMethodName}",
                    ex,
                    $"Test execution: {testMethodName}",
                    "Test case failed",
                    "Review test implementation and system under test",
                    new { TestClass = _testClassName, TestMethod = testMethodName },
                    correlationId: _correlationId);
                
                throw;
            }
        }

        /// <summary>
        /// Executes a test with expected exception handling
        /// </summary>
        protected async Task ExecuteTestWithExpectedExceptionAsync<TException>(
            Func<Task> testAction,
            string expectedExceptionMessage = "",
            [CallerMemberName] string testMethodName = "") where TException : Exception
        {
            LogTestMethodStart(testMethodName);
            
            try
            {
                await testAction();
                
                // If we get here, the expected exception was not thrown
                LogTestMethodEnd(false, $"Expected {typeof(TException).Name} was not thrown", testMethodName);
                Assert.True(false, $"Expected {typeof(TException).Name} but no exception was thrown");
            }
            catch (TException ex)
            {
                LogTestStep($"Caught expected exception: {typeof(TException).Name}");
                
                if (!string.IsNullOrEmpty(expectedExceptionMessage))
                {
                    Assert.Contains(expectedExceptionMessage, ex.Message);
                }
                
                LogTestMethodEnd(true, $"Successfully caught {typeof(TException).Name}", testMethodName);
            }
            catch (Exception ex)
            {
                LogTestMethodEnd(false, $"Wrong exception type: {ex.GetType().Name}", testMethodName);
                throw;
            }
        }

        /// <summary>
        /// Executes a parameterized test with canonical logging
        /// </summary>
        protected async Task ExecuteParameterizedTestAsync<TParam>(
            TParam parameter,
            Func<TParam, Task> testAction,
            [CallerMemberName] string testMethodName = "")
        {
            LogTestMethodStart(testMethodName);
            LogTestData("Test Parameter", parameter!);
            
            try
            {
                await testAction(parameter);
                LogTestMethodEnd(true, testMethodName: testMethodName);
            }
            catch (Exception ex)
            {
                LogTestMethodEnd(false, ex.Message, testMethodName);
                throw;
            }
        }

        #endregion

        #region Canonical Assertions

        /// <summary>
        /// Asserts with logging
        /// </summary>
        protected void AssertWithLogging<T>(
            T actual,
            T expected,
            string assertionDescription,
            IEqualityComparer<T>? comparer = null)
        {
            bool passed;
            
            if (comparer != null)
            {
                passed = comparer.Equals(actual, expected);
            }
            else
            {
                passed = EqualityComparer<T>.Default.Equals(actual, expected);
            }

            LogAssertion(assertionDescription, passed, actual, expected);
            
            if (!passed)
            {
                Assert.Equal(expected, actual);
            }
        }

        /// <summary>
        /// Asserts condition with logging
        /// </summary>
        protected void AssertConditionWithLogging(
            bool condition,
            string assertionDescription,
            string failureMessage = "")
        {
            LogAssertion(assertionDescription, condition);
            
            if (!condition)
            {
                Assert.True(condition, failureMessage);
            }
        }

        /// <summary>
        /// Asserts collection with logging
        /// </summary>
        protected void AssertCollectionWithLogging<T>(
            IEnumerable<T> actual,
            IEnumerable<T> expected,
            string assertionDescription)
        {
            var actualList = actual.ToList();
            var expectedList = expected.ToList();
            
            var passed = actualList.Count == expectedList.Count && 
                        actualList.SequenceEqual(expectedList);

            LogAssertion(
                $"{assertionDescription} (Count: {actualList.Count} vs {expectedList.Count})",
                passed,
                actualList,
                expectedList);
            
            Assert.Equal(expectedList, actualList);
        }

        #endregion

        #region Canonical Test Helpers

        /// <summary>
        /// Creates test data with logging
        /// </summary>
        protected T CreateTestData<T>(Func<T> factory, string description)
        {
            LogTestStep($"Creating test data: {description}");
            var data = factory();
            LogTestData(description, data!);
            return data;
        }

        /// <summary>
        /// Measures performance of an operation
        /// </summary>
        protected async Task<(T Result, TimeSpan Duration)> MeasurePerformanceAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            TimeSpan? maxDuration = null)
        {
            LogTestStep($"Measuring performance: {operationDescription}");
            
            var stopwatch = Stopwatch.StartNew();
            var result = await operation();
            stopwatch.Stop();
            
            LogTestData($"Performance - {operationDescription}", new 
            { 
                DurationMs = stopwatch.ElapsedMilliseconds,
                DurationFormatted = stopwatch.Elapsed.ToString(@"mm\:ss\.fff")
            });

            if (maxDuration.HasValue)
            {
                AssertConditionWithLogging(
                    stopwatch.Elapsed <= maxDuration.Value,
                    $"Performance assertion: {operationDescription} completes within {maxDuration.Value.TotalMilliseconds}ms",
                    $"Operation took {stopwatch.ElapsedMilliseconds}ms, expected max {maxDuration.Value.TotalMilliseconds}ms");
            }

            return (result, stopwatch.Elapsed);
        }

        /// <summary>
        /// Retries a flaky test operation
        /// </summary>
        protected async Task<T> RetryTestOperationAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int delayMs = 100)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogTestStep($"Attempt {attempt}/{maxRetries}");
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    LogTestStep($"Attempt {attempt} failed: {ex.Message}. Retrying...");
                    await Task.Delay(delayMs * attempt); // Exponential backoff
                }
            }

            // Final attempt without catching
            return await operation();
        }

        #endregion

        #region Setup and Teardown

        /// <summary>
        /// Override to configure test-specific services
        /// </summary>
        protected virtual void ConfigureTestServices(IServiceCollection services)
        {
            // Add default test services
            services.AddSingleton<ITradingLogger, TradingLogger>();
            services.AddMemoryCache();
        }

        /// <summary>
        /// Adds a test artifact for cleanup
        /// </summary>
        protected void RegisterTestArtifact(string artifactPath)
        {
            _testArtifacts.Add(artifactPath);
            LogTestStep($"Registered test artifact: {artifactPath}");
        }

        private void LogTestStart(string sourceFilePath)
        {
            var message = $"[TEST CLASS START] {_testClassName}";
            _output.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
            _output.WriteLine($"  Source: {sourceFilePath}");
            _output.WriteLine($"  Correlation ID: {_correlationId}");
            _output.WriteLine(new string('-', 80));
        }

        public virtual void Dispose()
        {
            // Clean up test artifacts
            foreach (var artifact in _testArtifacts)
            {
                try
                {
                    if (File.Exists(artifact))
                    {
                        File.Delete(artifact);
                        LogTestStep($"Cleaned up test artifact: {artifact}");
                    }
                }
                catch (Exception ex)
                {
                    LogTestStep($"Failed to clean up artifact {artifact}: {ex.Message}");
                }
            }

            // Dispose service provider
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            var message = $"[TEST CLASS END] {_testClassName} - Total Duration: {_testStopwatch.ElapsedMilliseconds}ms";
            _output.WriteLine(new string('-', 80));
            _output.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
        }

        #endregion
    }
}