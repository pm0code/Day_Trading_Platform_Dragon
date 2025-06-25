using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TradingPlatform.UnitTests.Framework
{
    /// <summary>
    /// Base class for all canonical unit tests providing common test infrastructure
    /// </summary>
    public abstract class CanonicalTestBase : IDisposable
    {
        protected readonly ITestOutputHelper Output;
        protected readonly Mock<ITradingLogger> MockLogger;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IServiceCollection Services;
        protected readonly CancellationTokenSource TestCts;
        protected readonly Random TestRandom;

        protected CanonicalTestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            MockLogger = new Mock<ITradingLogger>();
            Services = new ServiceCollection();
            TestCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Default test timeout
            TestRandom = new Random(GetStableRandomSeed());

            // Setup default logger behavior
            ConfigureDefaultLogger();

            // Configure services
            ConfigureServices(Services);
            
            // Build service provider
            ServiceProvider = Services.BuildServiceProvider();
        }

        /// <summary>
        /// Gets a stable random seed based on test name for reproducible tests
        /// </summary>
        protected virtual int GetStableRandomSeed()
        {
            var testName = GetType().Name;
            return testName.GetHashCode();
        }

        /// <summary>
        /// Configure default logger mock behavior
        /// </summary>
        protected virtual void ConfigureDefaultLogger()
        {
            MockLogger.Setup(x => x.LogMethodEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, string, int>((method, file, line) => 
                    Output.WriteLine($"[ENTRY] {method} at {file}:{line}"));

            MockLogger.Setup(x => x.LogMethodExit(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, object?, long, string, int>((method, result, elapsed, file, line) => 
                    Output.WriteLine($"[EXIT] {method} returned {result} in {elapsed}ms"));

            MockLogger.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, Exception?, string?, string?, string?, object?, string, int>((message, ex, context, impact, hints, data, file, line) => 
                    Output.WriteLine($"[ERROR] {message} - {ex?.Message ?? "No exception"} at {file}:{line}"));

            MockLogger.Setup(x => x.LogTrade(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
                .Callback<string, string, decimal, int, decimal, string, string, object?>((symbol, action, price, quantity, amount, strategy, status, metadata) => 
                    Output.WriteLine($"[TRADE] {action} {symbol} {quantity}@{price} = ${amount} [{status}]"));

            MockLogger.Setup(x => x.LogPerformance(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal?>(), It.IsAny<object?>()))
                .Callback<string, decimal, decimal?, object?>((metric, value, target, context) => 
                    Output.WriteLine($"[PERF] {metric}: {value} (target: {target ?? 0})"));
        }

        /// <summary>
        /// Override to configure additional services for tests
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton<ITradingLogger>(MockLogger.Object);
        }

        /// <summary>
        /// Get a required service from the container
        /// </summary>
        protected T GetRequiredService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Run an async test with proper timeout and error handling
        /// </summary>
        protected async Task RunTestAsync(Func<Task> testAction, int timeoutSeconds = 30)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await testAction().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Test timed out after {timeoutSeconds} seconds");
            }
        }

        /// <summary>
        /// Verify that a logger method was called with specific parameters
        /// </summary>
        protected void VerifyLog(LogLevel level, string messageContains, Times times)
        {
            switch (level)
            {
                case LogLevel.Error:
                    MockLogger.Verify(x => x.LogError(
                        It.Is<string>(m => m.Contains(messageContains)),
                        It.IsAny<Exception?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()), times);
                    break;
                case LogLevel.Warning:
                    MockLogger.Verify(x => x.LogWarning(
                        It.Is<string>(m => m.Contains(messageContains)),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()), times);
                    break;
                case LogLevel.Information:
                    MockLogger.Verify(x => x.LogInformation(
                        It.Is<string>(m => m.Contains(messageContains)),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()), times);
                    break;
                case LogLevel.Debug:
                    MockLogger.Verify(x => x.LogDebug(
                        It.Is<string>(m => m.Contains(messageContains)),
                        It.IsAny<object?>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()), times);
                    break;
            }
        }

        /// <summary>
        /// Create a test-specific logger that writes to test output
        /// </summary>
        protected ITradingLogger CreateTestLogger()
        {
            return new TestOutputLogger(Output);
        }

        public virtual void Dispose()
        {
            TestCts?.Cancel();
            TestCts?.Dispose();
            (ServiceProvider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Log levels for verification
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// Test logger that writes to xUnit test output
    /// </summary>
    internal class TestOutputLogger : ITradingLogger
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogMethodEntry(string methodName = "", string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[ENTRY] {methodName}");
        }

        public void LogMethodExit(string methodName = "", object? returnValue = null, long elapsedMilliseconds = 0, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[EXIT] {methodName} in {elapsedMilliseconds}ms");
        }

        public void LogInformation(string message, object? data = null, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[INFO] {message}");
        }

        public void LogWarning(string message, object? data = null, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[WARN] {message}");
        }

        public void LogError(string message, Exception? exception = null, string? operationContext = null, string? userImpact = null, string? troubleshootingHints = null, object? additionalData = null, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[ERROR] {message} - {exception?.Message}");
        }

        public void LogDebug(string message, object? data = null, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[DEBUG] {message}");
        }

        public void LogTrace(string message, object? data = null, string filePath = "", int lineNumber = 0)
        {
            _output.WriteLine($"[TRACE] {message}");
        }

        public void LogTrade(string symbol, string action, decimal price, int quantity, decimal totalAmount, string strategy, string status, object? metadata = null)
        {
            _output.WriteLine($"[TRADE] {action} {symbol} {quantity}@{price}");
        }

        public void LogPositionChange(string symbol, int oldQuantity, int newQuantity, decimal averagePrice, decimal realizedPnL, decimal unrealizedPnL, string reason, object? metadata = null)
        {
            _output.WriteLine($"[POSITION] {symbol}: {oldQuantity} -> {newQuantity}");
        }

        public void LogRisk(string riskType, string symbol, decimal riskValue, decimal threshold, bool isViolation, string action, object? metadata = null)
        {
            _output.WriteLine($"[RISK] {riskType} for {symbol}: {riskValue}/{threshold}");
        }

        public void LogMarketData(string symbol, string dataType, decimal? price = null, long? volume = null, decimal? changePercent = null, object? additionalData = null)
        {
            _output.WriteLine($"[MARKET] {symbol} {dataType}: {price}");
        }

        public void LogPerformance(string metricName, decimal value, decimal? comparisonValue = null, object? context = null)
        {
            _output.WriteLine($"[PERF] {metricName}: {value}");
        }

        public void LogDataPipeline(string stage, string source, string destination, int recordCount, long bytesProcessed, TimeSpan duration, string status, object? metadata = null)
        {
            _output.WriteLine($"[PIPELINE] {stage}: {recordCount} records");
        }

        public void LogSystemResource(string resourceType, decimal usage, decimal threshold, bool isWarning, object? metadata = null)
        {
            _output.WriteLine($"[RESOURCE] {resourceType}: {usage}%");
        }

        public void LogHealth(string component, string status, TimeSpan? responseTime = null, object? diagnostics = null)
        {
            _output.WriteLine($"[HEALTH] {component}: {status}");
        }

        public void LogAudit(string action, string entity, string entityId, string userId, bool success, object? details = null)
        {
            _output.WriteLine($"[AUDIT] {action} on {entity}:{entityId}");
        }

        public void LogSecurity(string eventType, string severity, string source, string description, object? context = null)
        {
            _output.WriteLine($"[SECURITY] {eventType}: {description}");
        }

        public void LogDataMovement(string operation, string source, string destination, int recordCount, string status, object? metadata = null)
        {
            _output.WriteLine($"[DATA] {operation}: {recordCount} records from {source} to {destination}");
        }

        public void LogMemoryUsage(long usedBytes, long totalBytes, decimal percentageUsed, bool isWarning)
        {
            _output.WriteLine($"[MEMORY] {percentageUsed}% used");
        }

        public void LogApiCall(string endpoint, string method, int statusCode, long responseTimeMs, object? requestData = null, object? responseData = null)
        {
            _output.WriteLine($"[API] {method} {endpoint} -> {statusCode}");
        }

        public void LogConfiguration(string key, string value, string source, bool isDefault)
        {
            _output.WriteLine($"[CONFIG] {key} = {value}");
        }

        public void LogDatabaseQuery(string query, long executionTimeMs, int recordsAffected, object? parameters = null)
        {
            _output.WriteLine($"[DB] Query executed in {executionTimeMs}ms");
        }

        public void LogCacheOperation(string operation, string key, bool hit, long? sizeBytes = null, TimeSpan? ttl = null)
        {
            _output.WriteLine($"[CACHE] {operation} {key}: {(hit ? "HIT" : "MISS")}");
        }

        public void LogRateLimiting(string resource, int currentRate, int limit, bool isThrottled, TimeSpan? retryAfter = null)
        {
            _output.WriteLine($"[RATE] {resource}: {currentRate}/{limit}");
        }

        public void LogBackgroundJob(string jobName, string status, TimeSpan? duration = null, object? result = null)
        {
            _output.WriteLine($"[JOB] {jobName}: {status}");
        }

        public void LogIntegration(string system, string operation, bool success, object? data = null)
        {
            _output.WriteLine($"[INTEGRATION] {system}.{operation}: {(success ? "SUCCESS" : "FAILURE")}");
        }

        public void LogNotification(string type, string recipient, string subject, bool sent, object? metadata = null)
        {
            _output.WriteLine($"[NOTIFY] {type} to {recipient}: {(sent ? "SENT" : "FAILED")}");
        }

        public void LogWorkflow(string workflowName, string stage, string status, object? context = null)
        {
            _output.WriteLine($"[WORKFLOW] {workflowName}.{stage}: {status}");
        }

        public void LogValidation(string entityType, string entityId, bool isValid, object? errors = null)
        {
            _output.WriteLine($"[VALIDATE] {entityType}:{entityId} = {(isValid ? "VALID" : "INVALID")}");
        }

        public void LogFeatureToggle(string feature, bool enabled, string reason, object? context = null)
        {
            _output.WriteLine($"[FEATURE] {feature}: {(enabled ? "ON" : "OFF")}");
        }

        public void LogBusinessEvent(string eventType, string description, object? data = null)
        {
            _output.WriteLine($"[BUSINESS] {eventType}: {description}");
        }

        public void LogUserAction(string action, string userId, string targetEntity, object? metadata = null)
        {
            _output.WriteLine($"[USER] {userId} -> {action} on {targetEntity}");
        }

        public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}