using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;
using Polly.Contrib.Simmy.Outcomes;
using Polly.Contrib.Simmy.Behavior;
using TradingPlatform.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace TradingPlatform.ChaosTests.Framework
{
    /// <summary>
    /// Base class for chaos engineering tests using Simmy
    /// </summary>
    public abstract class ChaosTestBase : IntegrationTestFixture
    {
        protected readonly ITestOutputHelper Output;
        protected readonly Random ChaosRandom;

        protected ChaosTestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            ChaosRandom = new Random(42); // Fixed seed for reproducibility
        }

        /// <summary>
        /// Create a chaos policy that injects random exceptions
        /// </summary>
        protected IAsyncPolicy<T> CreateExceptionChaosPolicy<T>(
            double injectionRate,
            Func<Context, CancellationToken, Exception> exceptionFactory,
            bool enabled = true)
        {
            var chaosPolicy = MonkeyPolicy.InjectExceptionAsync<T>(
                configureOptions: options =>
                {
                    options.InjectionRate = injectionRate;
                    options.Enabled = enabled;
                    options.ExceptionFactory = exceptionFactory;
                });

            return chaosPolicy;
        }

        /// <summary>
        /// Create a chaos policy that injects latency
        /// </summary>
        protected IAsyncPolicy<T> CreateLatencyChaosPolicy<T>(
            double injectionRate,
            TimeSpan latency,
            bool enabled = true)
        {
            var chaosPolicy = MonkeyPolicy.InjectLatencyAsync<T>(
                configureOptions: options =>
                {
                    options.InjectionRate = injectionRate;
                    options.Enabled = enabled;
                    options.Latency = latency;
                });

            return chaosPolicy;
        }

        /// <summary>
        /// Create a chaos policy that returns specific results
        /// </summary>
        protected IAsyncPolicy<T> CreateResultChaosPolicy<T>(
            double injectionRate,
            Func<Context, CancellationToken, T> resultFactory,
            bool enabled = true)
        {
            var chaosPolicy = MonkeyPolicy.InjectResultAsync<T>(
                configureOptions: options =>
                {
                    options.InjectionRate = injectionRate;
                    options.Enabled = enabled;
                    options.Result = resultFactory;
                });

            return chaosPolicy;
        }

        /// <summary>
        /// Create a chaos policy that injects custom behavior
        /// </summary>
        protected IAsyncPolicy CreateBehaviorChaosPolicy(
            double injectionRate,
            Func<Context, CancellationToken, Task> behavior,
            bool enabled = true)
        {
            var chaosPolicy = MonkeyPolicy.InjectBehaviourAsync(
                configureOptions: options =>
                {
                    options.InjectionRate = injectionRate;
                    options.Enabled = enabled;
                    options.Behaviour = behavior;
                });

            return chaosPolicy;
        }

        /// <summary>
        /// Create a resilience policy with retry and circuit breaker
        /// </summary>
        protected IAsyncPolicy<T> CreateResiliencePolicy<T>(
            int retryCount = 3,
            int circuitBreakerThreshold = 5,
            TimeSpan circuitBreakerDuration = default)
        {
            if (circuitBreakerDuration == default)
                circuitBreakerDuration = TimeSpan.FromSeconds(30);

            var retryPolicy = Policy<T>
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Output.WriteLine($"Retry {retryCount} after {timespan}ms");
                    });

            var circuitBreakerPolicy = Policy<T>
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    circuitBreakerThreshold,
                    circuitBreakerDuration,
                    onBreak: (result, duration) =>
                    {
                        Output.WriteLine($"Circuit breaker opened for {duration}");
                    },
                    onReset: () =>
                    {
                        Output.WriteLine("Circuit breaker reset");
                    });

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        /// <summary>
        /// Simulate network partition by blocking connections
        /// </summary>
        protected async Task SimulateNetworkPartition(TimeSpan duration)
        {
            Output.WriteLine($"Simulating network partition for {duration}");
            
            // In a real scenario, this would manipulate network rules
            // For testing, we simulate by stopping containers
            if (RedisContainer != null)
            {
                await RedisContainer.StopAsync();
                await Task.Delay(duration);
                await RedisContainer.StartAsync();
            }
        }

        /// <summary>
        /// Simulate resource exhaustion
        /// </summary>
        protected async Task SimulateResourceExhaustion(
            int cpuStressThreads = 4,
            int memoryMB = 1000,
            TimeSpan duration = default)
        {
            if (duration == default)
                duration = TimeSpan.FromSeconds(10);

            Output.WriteLine($"Simulating resource exhaustion: {cpuStressThreads} CPU threads, {memoryMB}MB memory for {duration}");

            var cts = new CancellationTokenSource(duration);
            var tasks = new Task[cpuStressThreads];

            // CPU stress
            for (int i = 0; i < cpuStressThreads; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        // Busy loop to consume CPU
                        _ = Guid.NewGuid().ToString();
                    }
                });
            }

            // Memory stress
            var memoryHog = new byte[memoryMB * 1024 * 1024];
            ChaosRandom.NextBytes(memoryHog);

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Clean up
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Simulate clock skew
        /// </summary>
        protected DateTimeOffset SimulateClockSkew(TimeSpan skew)
        {
            Output.WriteLine($"Simulating clock skew of {skew}");
            return DateTimeOffset.UtcNow.Add(skew);
        }

        /// <summary>
        /// Create chaos scenarios for different failure modes
        /// </summary>
        protected ChaosScenario[] GetStandardChaosScenarios()
        {
            return new[]
            {
                new ChaosScenario
                {
                    Name = "Transient Network Errors",
                    InjectionRate = 0.1,
                    Description = "10% of requests fail with network errors"
                },
                new ChaosScenario
                {
                    Name = "High Latency",
                    InjectionRate = 0.2,
                    Description = "20% of requests have 1-5 second latency"
                },
                new ChaosScenario
                {
                    Name = "Service Unavailable",
                    InjectionRate = 0.05,
                    Description = "5% of requests fail with service unavailable"
                },
                new ChaosScenario
                {
                    Name = "Data Corruption",
                    InjectionRate = 0.01,
                    Description = "1% of responses contain corrupted data"
                },
                new ChaosScenario
                {
                    Name = "Resource Exhaustion",
                    InjectionRate = 0.02,
                    Description = "2% chance of resource exhaustion"
                }
            };
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            // Add chaos-specific services
            services.AddSingleton<ITestOutputHelper>(Output);
        }
    }

    public class ChaosScenario
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double InjectionRate { get; set; }
        public TimeSpan? Duration { get; set; }
        public Exception? Exception { get; set; }
    }
}