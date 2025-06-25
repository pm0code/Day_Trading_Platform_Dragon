using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.Core.Canonical
{
    public class CanonicalServiceBaseTests : CanonicalTestBase
    {
        private readonly TestCanonicalService _service;

        public CanonicalServiceBaseTests(ITestOutputHelper output) : base(output)
        {
            _service = new TestCanonicalService(MockLogger.Object, "TestService");
        }

        [Fact]
        public async Task InitializeAsync_WhenNotInitialized_Success()
        {
            // Act
            var result = await _service.InitializeAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Initialized);
            _service.InitializeCallCount.Should().Be(1);
            VerifyLog(LogLevel.Information, "Initializing TestService", Times.Once());
            VerifyLog(LogLevel.Information, "TestService initialized successfully", Times.Once());
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ReturnsSuccess()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);

            // Act
            var result = await _service.InitializeAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Initialized);
            _service.InitializeCallCount.Should().Be(1); // Should not call OnInitializeAsync again
        }

        [Fact]
        public async Task StartAsync_WhenInitialized_Success()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);

            // Act
            var result = await _service.StartAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Running);
            _service.StartCallCount.Should().Be(1);
            VerifyLog(LogLevel.Information, "Starting TestService", Times.Once());
            VerifyLog(LogLevel.Information, "TestService started successfully", Times.Once());
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_Fails()
        {
            // Act
            var result = await _service.StartAsync(TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Cannot start service in Created state");
            _service.State.Should().Be(ServiceState.Created);
            _service.StartCallCount.Should().Be(0);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ReturnsSuccess()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);

            // Act
            var result = await _service.StartAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Running);
            _service.StartCallCount.Should().Be(1); // Should not call OnStartAsync again
        }

        [Fact]
        public async Task StopAsync_WhenRunning_Success()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);

            // Act
            var result = await _service.StopAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Stopped);
            _service.StopCallCount.Should().Be(1);
            VerifyLog(LogLevel.Information, "Stopping TestService", Times.Once());
            VerifyLog(LogLevel.Information, "TestService stopped successfully", Times.Once());
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ReturnsSuccess()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);

            // Act
            var result = await _service.StopAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Stopped);
            _service.StopCallCount.Should().Be(0); // Should not call OnStopAsync
        }

        [Fact]
        public async Task CheckHealthAsync_WhenRunning_ReturnsHealthy()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);

            // Act
            var result = await _service.CheckHealthAsync(TestCts.Token);

            // Assert
            result.IsHealthy.Should().BeTrue();
            result.Message.Should().Contain("TestService is healthy");
            result.Details.Should().ContainKey("State").WhoseValue.Should().Be("Running");
            result.Details.Should().ContainKey("UptimeSeconds");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenStopped_ReturnsUnhealthy()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);
            await _service.StopAsync(TestCts.Token);

            // Act
            var result = await _service.CheckHealthAsync(TestCts.Token);

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.Message.Should().Contain("TestService is not running");
            result.Details.Should().ContainKey("State").WhoseValue.Should().Be("Stopped");
        }

        [Fact]
        public async Task ExecuteServiceOperationAsync_WhenRunning_ExecutesOperation()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);
            var operationCalled = false;

            // Act
            var result = await _service.TestExecuteOperation(
                () =>
                {
                    operationCalled = true;
                    return Task.FromResult(TradingResult<string>.Success("Operation completed"));
                });

            // Assert
            result.Should().BeSuccess();
            result.Should().HaveValue("Operation completed");
            operationCalled.Should().BeTrue();
            VerifyLog(LogLevel.Debug, "TestOperation", Times.Once());
        }

        [Fact]
        public async Task ExecuteServiceOperationAsync_WhenNotRunning_Fails()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);

            // Act
            var result = await _service.TestExecuteOperation(
                () => Task.FromResult(TradingResult<string>.Success("Should not execute")));

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Service TestService is not running");
        }

        [Fact]
        public async Task ExecuteServiceOperationAsync_WhenOperationThrows_ReturnsFailure()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);
            var exception = new InvalidOperationException("Operation failed");

            // Act
            var result = await _service.TestExecuteOperation(
                () => throw exception);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Operation failed");
            VerifyLog(LogLevel.Error, "TestOperation failed", Times.Once());
        }

        [Fact]
        public async Task GetPerformanceMetricsAsync_ReturnsMetrics()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);
            await Task.Delay(100); // Let some time pass

            // Act
            var result = await _service.GetPerformanceMetricsAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var metrics = result.Value;
            metrics.Should().ContainKey("State").WhoseValue.Should().Be("Running");
            metrics.Should().ContainKey("UptimeSeconds");
            metrics.Should().ContainKey("InitializedAt");
            metrics.Should().ContainKey("StartedAt");
            
            var uptime = Convert.ToDouble(metrics["UptimeSeconds"]);
            uptime.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task OnInitializeAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            _service.ShouldDelayInInitialize = true;

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _service.InitializeAsync(cts.Token));
            
            _service.State.Should().Be(ServiceState.Created);
        }

        [Fact]
        public async Task Dispose_DisposesResources()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);

            // Act
            _service.Dispose();

            // Assert
            _service.IsDisposed.Should().BeTrue();
            _service.State.Should().Be(ServiceState.Disposed);
        }

        [Fact]
        public async Task ServiceLifecycle_CompleteFlow()
        {
            // Test complete lifecycle: Create -> Initialize -> Start -> Stop -> Dispose
            
            // Initial state
            _service.State.Should().Be(ServiceState.Created);

            // Initialize
            var initResult = await _service.InitializeAsync(TestCts.Token);
            initResult.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Initialized);

            // Start
            var startResult = await _service.StartAsync(TestCts.Token);
            startResult.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Running);

            // Check health while running
            var healthResult = await _service.CheckHealthAsync(TestCts.Token);
            healthResult.IsHealthy.Should().BeTrue();

            // Stop
            var stopResult = await _service.StopAsync(TestCts.Token);
            stopResult.Should().BeSuccess();
            _service.State.Should().Be(ServiceState.Stopped);

            // Dispose
            _service.Dispose();
            _service.State.Should().Be(ServiceState.Disposed);
        }

        [Fact]
        public async Task ConcurrentOperations_HandledSafely()
        {
            // Arrange
            await _service.InitializeAsync(TestCts.Token);
            await _service.StartAsync(TestCts.Token);

            // Act - Execute multiple operations concurrently
            var tasks = new Task<TradingResult<int>>[100];
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = _service.TestExecuteOperation(() => 
                    Task.FromResult(TradingResult<int>.Success(index)));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(r => r.Should().BeSuccess());
            var values = results.Select(r => r.Value).OrderBy(v => v).ToArray();
            values.Should().BeEquivalentTo(Enumerable.Range(0, 100));
        }
    }

    // Test implementation of CanonicalServiceBase
    internal class TestCanonicalService : CanonicalServiceBase
    {
        public int InitializeCallCount { get; private set; }
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool ShouldDelayInInitialize { get; set; }

        public TestCanonicalService(ITradingLogger logger, string serviceName) 
            : base(logger, serviceName)
        {
        }

        protected override async Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCallCount++;
            
            if (ShouldDelayInInitialize)
            {
                await Task.Delay(1000, cancellationToken);
            }
            
            return TradingResult.Success();
        }

        protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            StartCallCount++;
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            StopCallCount++;
            return Task.FromResult(TradingResult.Success());
        }

        public Task<TradingResult<T>> TestExecuteOperation<T>(Func<Task<TradingResult<T>>> operation)
        {
            return ExecuteServiceOperationAsync(operation, "TestOperation");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}