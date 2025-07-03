using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TradingPlatform.Tests.Core.Canonical
{
    /// <summary>
    /// Base class for end-to-end tests
    /// Extends IntegrationTestBase with E2E-specific functionality
    /// </summary>
    public abstract class E2ETestBase : IntegrationTestBase
    {
        protected E2ETestBase(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Add common E2E services
            ConfigureE2EServices(services);
            
            // Allow derived classes to add additional services
            ConfigureAdditionalServices(services);
        }
        
        /// <summary>
        /// Configure common E2E services
        /// </summary>
        private void ConfigureE2EServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });
            
            // Add common test services
            services.AddSingleton(MockLogger.Object);
            
            // Add memory cache for testing
            services.AddMemoryCache();
            
            // Add HTTP client factory for integration tests
            services.AddHttpClient();
        }
        
        /// <summary>
        /// Override to add test-specific services
        /// </summary>
        protected virtual void ConfigureAdditionalServices(IServiceCollection services)
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Helper to wait for eventual consistency in distributed scenarios
        /// </summary>
        protected async Task WaitForEventualConsistencyAsync(
            Func<Task<bool>> condition, 
            int maxWaitMs = 5000, 
            int pollIntervalMs = 100)
        {
            var startTime = DateTime.UtcNow;
            
            while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(maxWaitMs))
            {
                if (await condition())
                {
                    return;
                }
                
                await Task.Delay(pollIntervalMs);
            }
            
            throw new TimeoutException($"Condition not met within {maxWaitMs}ms");
        }
        
        /// <summary>
        /// Helper to retry operations that might fail due to timing
        /// </summary>
        protected async Task<T> RetryAsync<T>(
            Func<Task<T>> operation, 
            int maxRetries = 3, 
            int delayMs = 100)
        {
            Exception lastException = null;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs * (i + 1)); // Exponential backoff
                    }
                }
            }
            
            throw new AggregateException($"Operation failed after {maxRetries} retries", lastException);
        }
        
        /// <summary>
        /// Helper to measure operation performance
        /// </summary>
        protected async Task<(T Result, long ElapsedMs)> MeasurePerformanceAsync<T>(Func<Task<T>> operation)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await operation();
            stopwatch.Stop();
            
            Output.WriteLine($"Operation completed in {stopwatch.ElapsedMilliseconds}ms");
            return (result, stopwatch.ElapsedMilliseconds);
        }
        
        /// <summary>
        /// Helper to run operations in parallel and measure throughput
        /// </summary>
        protected async Task<double> MeasureThroughputAsync(
            Func<int, Task> operation, 
            int operationCount, 
            int parallelism = 10)
        {
            var semaphore = new System.Threading.SemaphoreSlim(parallelism);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var tasks = new Task[operationCount];
            for (int i = 0; i < operationCount; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await operation(index);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            var throughput = operationCount / (stopwatch.ElapsedMilliseconds / 1000.0);
            Output.WriteLine($"Throughput: {throughput:F2} operations/second");
            
            return throughput;
        }
    }
}