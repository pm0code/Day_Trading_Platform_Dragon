using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.PerformanceTests.Framework
{
    /// <summary>
    /// Base class for all performance benchmarks with canonical configuration
    /// </summary>
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    [MinColumn, MaxColumn, MeanColumn, MedianColumn]
    [RankColumn]
    public abstract class CanonicalBenchmarkBase
    {
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        protected ITradingLogger Logger { get; private set; } = null!;

        [GlobalSetup]
        public virtual void GlobalSetup()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            Logger = ServiceProvider.GetRequiredService<ITradingLogger>();
        }

        [GlobalCleanup]
        public virtual void GlobalCleanup()
        {
            (ServiceProvider as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Configure services for benchmarks
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Add no-op logger for benchmarks
            services.AddSingleton<ITradingLogger, NoOpTradingLogger>();
        }

        /// <summary>
        /// Get a required service from the container
        /// </summary>
        protected T GetRequiredService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }
    }

    /// <summary>
    /// Configuration for ultra-low latency benchmarks
    /// </summary>
    public class UltraLowLatencyConfig : ManualConfig
    {
        public UltraLowLatencyConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(false)
                .WithGcForce(false)
                .WithIterationCount(100)
                .WithWarmupCount(10)
                .WithLaunchCount(3)
                .WithInvocationCount(1000)
                .WithUnrollFactor(16));
        }
    }

    /// <summary>
    /// No-op logger implementation for performance tests
    /// </summary>
    internal class NoOpTradingLogger : ITradingLogger
    {
        public void LogMethodEntry(string methodName = "", string filePath = "", int lineNumber = 0) { }
        public void LogMethodExit(string methodName = "", object? returnValue = null, long elapsedMilliseconds = 0, string filePath = "", int lineNumber = 0) { }
        public void LogInformation(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogWarning(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogError(string message, Exception? exception = null, string? operationContext = null, string? userImpact = null, string? troubleshootingHints = null, object? additionalData = null, string filePath = "", int lineNumber = 0) { }
        public void LogDebug(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogTrace(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogTrade(string symbol, string action, decimal price, int quantity, decimal totalAmount, string strategy, string status, object? metadata = null) { }
        public void LogPositionChange(string symbol, int oldQuantity, int newQuantity, decimal averagePrice, decimal realizedPnL, decimal unrealizedPnL, string reason, object? metadata = null) { }
        public void LogRisk(string riskType, string symbol, decimal riskValue, decimal threshold, bool isViolation, string action, object? metadata = null) { }
        public void LogMarketData(string symbol, string dataType, decimal? price = null, long? volume = null, decimal? changePercent = null, object? additionalData = null) { }
        public void LogPerformance(string metricName, decimal value, decimal? comparisonValue = null, object? context = null) { }
        public void LogDataPipeline(string stage, string source, string destination, int recordCount, long bytesProcessed, TimeSpan duration, string status, object? metadata = null) { }
        public void LogSystemResource(string resourceType, decimal usage, decimal threshold, bool isWarning, object? metadata = null) { }
        public void LogHealth(string component, string status, TimeSpan? responseTime = null, object? diagnostics = null) { }
        public void LogAudit(string action, string entity, string entityId, string userId, bool success, object? details = null) { }
        public void LogSecurity(string eventType, string severity, string source, string description, object? context = null) { }
        public void LogDataMovement(string operation, string source, string destination, int recordCount, string status, object? metadata = null) { }
        public void LogMemoryUsage(long usedBytes, long totalBytes, decimal percentageUsed, bool isWarning) { }
        public void LogApiCall(string endpoint, string method, int statusCode, long responseTimeMs, object? requestData = null, object? responseData = null) { }
        public void LogConfiguration(string key, string value, string source, bool isDefault) { }
        public void LogDatabaseQuery(string query, long executionTimeMs, int recordsAffected, object? parameters = null) { }
        public void LogCacheOperation(string operation, string key, bool hit, long? sizeBytes = null, TimeSpan? ttl = null) { }
        public void LogRateLimiting(string resource, int currentRate, int limit, bool isThrottled, TimeSpan? retryAfter = null) { }
        public void LogBackgroundJob(string jobName, string status, TimeSpan? duration = null, object? result = null) { }
        public void LogIntegration(string system, string operation, bool success, object? data = null) { }
        public void LogNotification(string type, string recipient, string subject, bool sent, object? metadata = null) { }
        public void LogWorkflow(string workflowName, string stage, string status, object? context = null) { }
        public void LogValidation(string entityType, string entityId, bool isValid, object? errors = null) { }
        public void LogFeatureToggle(string feature, bool enabled, string reason, object? context = null) { }
        public void LogBusinessEvent(string eventType, string description, object? data = null) { }
        public void LogUserAction(string action, string userId, string targetEntity, object? metadata = null) { }
        public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}