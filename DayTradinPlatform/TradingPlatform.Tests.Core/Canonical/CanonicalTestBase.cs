using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Tests.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all unit tests in the trading platform
    /// Provides common test infrastructure and helper methods
    /// </summary>
    public abstract class CanonicalTestBase<TService> : IDisposable
        where TService : class
    {
        protected readonly ITestOutputHelper Output;
        protected readonly Mock<ITradingLogger> MockLogger;
        protected readonly IServiceCollection Services;
        protected readonly IServiceProvider ServiceProvider;
        protected TService SystemUnderTest = null!;
        
        // Common test data
        protected readonly DateTime TestTimestamp = new DateTime(2025, 1, 30, 10, 0, 0, DateTimeKind.Utc);
        protected readonly string TestSymbol = "AAPL";
        protected readonly decimal TestPrice = 150.25m;
        
        protected CanonicalTestBase(ITestOutputHelper output)
        {
            Output = output;
            MockLogger = new Mock<ITradingLogger>();
            Services = new ServiceCollection();
            
            // Setup common services
            SetupMockLogger();
            ConfigureCommonServices();
            ConfigureServices(Services);
            
            ServiceProvider = Services.BuildServiceProvider();
            
            // Create system under test
            SystemUnderTest = CreateSystemUnderTest();
        }
        
        /// <summary>
        /// Configure service-specific dependencies
        /// </summary>
        protected abstract void ConfigureServices(IServiceCollection services);
        
        /// <summary>
        /// Create the system under test
        /// </summary>
        protected abstract TService CreateSystemUnderTest();
        
        /// <summary>
        /// Configure common services used by all tests
        /// </summary>
        protected virtual void ConfigureCommonServices()
        {
            Services.AddSingleton(MockLogger.Object);
            Services.AddSingleton<ITradingLogger>(MockLogger.Object);
            Services.AddMemoryCache();
            Services.AddLogging();
        }
        
        /// <summary>
        /// Setup mock logger with default behavior
        /// </summary>
        protected virtual void SetupMockLogger()
        {
            MockLogger.Setup(x => x.LogInfo(It.IsAny<string>()))
                .Callback<string>(msg => Output.WriteLine($"[INFO] {msg}"));
                
            MockLogger.Setup(x => x.LogWarning(It.IsAny<string>()))
                .Callback<string>(msg => Output.WriteLine($"[WARN] {msg}"));
                
            MockLogger.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()))
                .Callback<string, Exception>((msg, ex) => Output.WriteLine($"[ERROR] {msg} - {ex?.Message}"));
                
            MockLogger.Setup(x => x.LogDebug(It.IsAny<string>()))
                .Callback<string>(msg => Output.WriteLine($"[DEBUG] {msg}"));
        }
        
        #region Common Test Helpers
        
        /// <summary>
        /// Assert that an action does not throw any exceptions
        /// </summary>
        protected void AssertNoExceptions(Action action)
        {
            var exception = Record.Exception(action);
            Assert.Null(exception);
        }
        
        /// <summary>
        /// Assert that an async action does not throw any exceptions
        /// </summary>
        protected async Task AssertNoExceptionsAsync(Func<Task> action)
        {
            var exception = await Record.ExceptionAsync(action);
            Assert.Null(exception);
        }
        
        /// <summary>
        /// Assert financial values are equal within precision tolerance
        /// </summary>
        protected void AssertFinancialPrecision(decimal expected, decimal actual, int decimalPlaces = 8)
        {
            var tolerance = (decimal)Math.Pow(10, -decimalPlaces);
            Assert.True(Math.Abs(expected - actual) < tolerance,
                $"Expected {expected} but got {actual} (tolerance: {tolerance})");
        }
        
        /// <summary>
        /// Assert that a result is successful
        /// </summary>
        protected void AssertSuccess<T>(TradingResult<T> result)
        {
            Assert.True(result.IsSuccess, $"Expected success but got: {result.Error?.Message}");
            Assert.NotNull(result.Value);
        }
        
        /// <summary>
        /// Assert that a result failed with expected error
        /// </summary>
        protected void AssertFailure<T>(TradingResult<T> result, string expectedErrorSubstring = null)
        {
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
            
            if (!string.IsNullOrEmpty(expectedErrorSubstring))
            {
                Assert.Contains(expectedErrorSubstring, result.Error.Message);
            }
        }
        
        /// <summary>
        /// Verify logger was called with specific level
        /// </summary>
        protected void VerifyLoggerCalled(string level, Times times)
        {
            switch (level.ToLower())
            {
                case "info":
                    MockLogger.Verify(x => x.LogInfo(It.IsAny<string>()), times);
                    break;
                case "warning":
                    MockLogger.Verify(x => x.LogWarning(It.IsAny<string>()), times);
                    break;
                case "error":
                    MockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), times);
                    break;
                case "debug":
                    MockLogger.Verify(x => x.LogDebug(It.IsAny<string>()), times);
                    break;
            }
        }
        
        /// <summary>
        /// Verify no errors were logged
        /// </summary>
        protected void VerifyNoErrors()
        {
            MockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never());
            MockLogger.Verify(x => x.LogCritical(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never());
        }
        
        #endregion
        
        #region Performance Testing Helpers
        
        /// <summary>
        /// Measure execution time of an action
        /// </summary>
        protected long MeasureExecutionTime(Action action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        
        /// <summary>
        /// Measure execution time of an async action
        /// </summary>
        protected async Task<long> MeasureExecutionTimeAsync(Func<Task> action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        
        /// <summary>
        /// Assert operation completes within time limit
        /// </summary>
        protected async Task AssertCompletesWithinAsync(int milliseconds, Func<Task> action)
        {
            var elapsed = await MeasureExecutionTimeAsync(action);
            Assert.True(elapsed <= milliseconds, 
                $"Operation took {elapsed}ms, exceeding limit of {milliseconds}ms");
        }
        
        #endregion
        
        #region Test Data Helpers
        
        /// <summary>
        /// Create a test exception with standard message
        /// </summary>
        protected Exception CreateTestException(string message = "Test exception")
        {
            return new InvalidOperationException(message);
        }
        
        /// <summary>
        /// Create random decimal within range
        /// </summary>
        protected decimal CreateRandomDecimal(decimal min = 0, decimal max = 1000)
        {
            var random = new Random();
            var range = max - min;
            return min + (decimal)random.NextDouble() * range;
        }
        
        #endregion
        
        public virtual void Dispose()
        {
            Output.WriteLine($"Disposing test for {typeof(TService).Name}");
            (ServiceProvider as IDisposable)?.Dispose();
            (SystemUnderTest as IDisposable)?.Dispose();
        }
    }
    
    /// <summary>
    /// Trading result wrapper for testing
    /// </summary>
    public class TradingResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Value { get; set; }
        public Exception Error { get; set; }
        
        public static TradingResult<T> Success(T value) => new()
        {
            IsSuccess = true,
            Value = value
        };
        
        public static TradingResult<T> Failure(Exception error) => new()
        {
            IsSuccess = false,
            Error = error
        };
    }
}