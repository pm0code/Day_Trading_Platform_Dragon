using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.Framework
{
    /// <summary>
    /// Base class for testing canonical services
    /// </summary>
    public abstract class CanonicalServiceTestBase<TService> : CanonicalTestBase
        where TService : CanonicalServiceBase
    {
        protected TService Service { get; set; } = default!;

        protected CanonicalServiceTestBase(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Create the service instance to test
        /// </summary>
        protected abstract TService CreateService();

        /// <summary>
        /// Standard test for service initialization
        /// </summary>
        protected async Task TestInitializationAsync()
        {
            // Arrange
            Service = CreateService();

            // Act
            var result = await Service.InitializeAsync(TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();
            Service.State.Should().Be(ServiceState.Initialized);
            
            VerifyLog(LogLevel.Information, "initialized", Moq.Times.AtLeastOnce());
        }

        /// <summary>
        /// Standard test for service start
        /// </summary>
        protected async Task TestStartAsync()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);

            // Act
            var result = await Service.StartAsync(TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();
            Service.State.Should().Be(ServiceState.Running);
            
            VerifyLog(LogLevel.Information, "started", Moq.Times.AtLeastOnce());
        }

        /// <summary>
        /// Standard test for service stop
        /// </summary>
        protected async Task TestStopAsync()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act
            var result = await Service.StopAsync(TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();
            Service.State.Should().Be(ServiceState.Stopped);
            
            VerifyLog(LogLevel.Information, "stopped", Moq.Times.AtLeastOnce());
        }

        /// <summary>
        /// Standard test for health check
        /// </summary>
        protected async Task TestHealthCheckAsync()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act
            var result = await Service.CheckHealthAsync(TestCts.Token);

            // Assert
            result.IsHealthy.Should().BeTrue();
            result.Details.Should().ContainKey("State");
            result.Details["State"].Should().Be("Running");
        }

        /// <summary>
        /// Standard test for error handling in service operations
        /// </summary>
        protected async Task TestErrorHandlingAsync(Func<Task<TradingResult>> operation, string expectedError)
        {
            // Arrange
            Service = CreateService();
            
            // Act
            var result = await operation();

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().NotBeNull();
            result.Error!.Message.Should().Contain(expectedError);
            
            VerifyLog(LogLevel.Error, expectedError, Moq.Times.AtLeastOnce());
        }

        /// <summary>
        /// Standard test for cancellation handling
        /// </summary>
        protected async Task TestCancellationAsync(Func<CancellationToken, Task> operation)
        {
            // Arrange
            Service = CreateService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => operation(cts.Token));
        }

        /// <summary>
        /// Standard test for concurrent operations
        /// </summary>
        protected async Task TestConcurrencyAsync(Func<Task> operation, int concurrentOperations = 10)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var tasks = new Task[concurrentOperations];

            // Act
            for (int i = 0; i < concurrentOperations; i++)
            {
                tasks[i] = operation();
            }

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Standard test for performance metrics
        /// </summary>
        protected async Task TestPerformanceMetricsAsync()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act
            var result = await Service.GetPerformanceMetricsAsync(TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var metrics = result.Value;
            metrics.Should().ContainKey("State");
            metrics.Should().ContainKey("UptimeSeconds");
        }

        public override void Dispose()
        {
            Service?.Dispose();
            base.Dispose();
        }
    }
}