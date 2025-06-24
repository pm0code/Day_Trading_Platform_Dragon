using System;
using System.Threading;
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
    /// Comprehensive unit tests for CanonicalServiceBase following canonical test patterns
    /// </summary>
    public class CanonicalServiceBaseTests : CanonicalTestBase
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly TestServiceImplementation _testService;

        public CanonicalServiceBaseTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ITradingLogger>();
            _testService = new TestServiceImplementation(_mockLogger.Object);
        }

        #region Service Lifecycle Tests

        [Fact]
        public async Task Service_Should_StartWithCreatedState()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Verifying initial service state");

                var service = new TestServiceImplementation(_mockLogger.Object);
                
                AssertWithLogging(
                    service.GetCurrentState(),
                    ServiceState.Created,
                    "Service should start in Created state"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task InitializeAsync_Should_TransitionToInitialized()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing service initialization");

                // Act
                var result = await _testService.InitializeAsync();

                // Assert
                AssertWithLogging(result, true, "Initialization should succeed");
                AssertWithLogging(
                    _testService.GetCurrentState(),
                    ServiceState.Initialized,
                    "Service should be in Initialized state"
                );
                AssertConditionWithLogging(
                    _testService.OnInitializeAsyncCalled,
                    "OnInitializeAsync should be called"
                );
            });
        }

        [Fact]
        public async Task InitializeAsync_Should_HandleInitializationFailure()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    LogTestStep("Testing initialization failure handling");

                    var failingService = new TestServiceImplementation(_mockLogger.Object)
                    {
                        ShouldFailOnInitialize = true
                    };

                    await failingService.InitializeAsync();
                },
                "Initialization failed"
            );
        }

        [Fact]
        public async Task StartAsync_Should_RequireInitializedState()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing start without initialization");

                // Act - try to start without initializing
                var result = await _testService.StartAsync();

                // Assert
                AssertWithLogging(result, false, "Start should fail without initialization");
                AssertWithLogging(
                    _testService.GetCurrentState(),
                    ServiceState.Created,
                    "State should remain unchanged"
                );
            });
        }

        [Fact]
        public async Task StartAsync_Should_TransitionToRunning()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing service startup after initialization");

                // Arrange
                await _testService.InitializeAsync();

                // Act
                var result = await _testService.StartAsync();

                // Assert
                AssertWithLogging(result, true, "Start should succeed");
                AssertWithLogging(
                    _testService.GetCurrentState(),
                    ServiceState.Running,
                    "Service should be in Running state"
                );
                AssertConditionWithLogging(
                    _testService.OnStartAsyncCalled,
                    "OnStartAsync should be called"
                );
            });
        }

        [Fact]
        public async Task StartAsync_Should_HandleStartupFailure()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    LogTestStep("Testing startup failure handling");

                    var failingService = new TestServiceImplementation(_mockLogger.Object)
                    {
                        ShouldFailOnStart = true
                    };

                    await failingService.InitializeAsync();
                    await failingService.StartAsync();
                },
                "Startup failed"
            );
        }

        [Fact]
        public async Task StopAsync_Should_RequireRunningState()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing stop without running");

                // Act - try to stop without starting
                var result = await _testService.StopAsync();

                // Assert
                AssertWithLogging(result, false, "Stop should fail when not running");
            });
        }

        [Fact]
        public async Task StopAsync_Should_TransitionToStopped()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing service shutdown");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();

                // Act
                var result = await _testService.StopAsync();

                // Assert
                AssertWithLogging(result, true, "Stop should succeed");
                AssertWithLogging(
                    _testService.GetCurrentState(),
                    ServiceState.Stopped,
                    "Service should be in Stopped state"
                );
                AssertConditionWithLogging(
                    _testService.OnStopAsyncCalled,
                    "OnStopAsync should be called"
                );
            });
        }

        [Fact]
        public async Task Service_Should_RestartAfterStop()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing service restart capability");

                // Arrange - full lifecycle
                await _testService.InitializeAsync();
                await _testService.StartAsync();
                await _testService.StopAsync();

                // Act - restart
                var result = await _testService.StartAsync();

                // Assert
                AssertWithLogging(result, true, "Restart should succeed");
                AssertWithLogging(
                    _testService.GetCurrentState(),
                    ServiceState.Running,
                    "Service should be running again"
                );
            });
        }

        #endregion

        #region Health Check Tests

        [Fact]
        public async Task CheckHealthAsync_Should_ReturnHealthyWhenRunning()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing health check for running service");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();

                // Act
                var health = await _testService.CheckHealthAsync();

                // Assert
                AssertWithLogging(health.IsHealthy, true, "Service should be healthy");
                AssertWithLogging(
                    health.CurrentState,
                    ServiceState.Running,
                    "Health should report Running state"
                );
                AssertConditionWithLogging(
                    health.UptimeSeconds > 0,
                    "Uptime should be tracked"
                );
            });
        }

        [Fact]
        public async Task CheckHealthAsync_Should_ReturnUnhealthyWhenNotRunning()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing health check for non-running service");

                // Act - check health without starting
                var health = await _testService.CheckHealthAsync();

                // Assert
                AssertWithLogging(health.IsHealthy, false, "Service should not be healthy");
                AssertConditionWithLogging(
                    health.HealthMessage!.Contains("Created"),
                    "Health message should indicate state"
                );
            });
        }

        [Fact]
        public async Task CheckHealthAsync_Should_UseCustomHealthCheck()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing custom health check implementation");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();
                _testService.CustomHealthStatus = false;
                _testService.CustomHealthMessage = "Test unhealthy condition";

                // Act
                var health = await _testService.CheckHealthAsync();

                // Assert
                AssertWithLogging(health.IsHealthy, false, "Custom health should be respected");
                AssertWithLogging(
                    health.HealthMessage,
                    "Test unhealthy condition",
                    "Custom message should be used"
                );
            });
        }

        [Fact]
        public async Task CheckHealthAsync_Should_HandleHealthCheckFailure()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing health check failure handling");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();
                _testService.ShouldFailHealthCheck = true;

                // Act
                var health = await _testService.CheckHealthAsync();

                // Assert
                AssertWithLogging(health.IsHealthy, false, "Health check should fail gracefully");
                AssertConditionWithLogging(
                    health.HealthMessage!.Contains("Health check failed"),
                    "Error message should be included"
                );
            });
        }

        #endregion

        #region Metrics Tests

        [Fact]
        public async Task UpdateMetric_Should_StoreMetricValue()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing metric update functionality");

                // Act
                _testService.TestUpdateMetric("TestMetric", 42);
                var metrics = _testService.GetMetrics();

                // Assert
                AssertConditionWithLogging(
                    metrics.ContainsKey("TestMetric"),
                    "Metric should be stored"
                );
                AssertWithLogging(
                    metrics["TestMetric"],
                    42,
                    "Metric value should be correct"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task IncrementCounter_Should_IncrementValue()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing counter increment functionality");

                // Act
                _testService.TestIncrementCounter("TestCounter");
                _testService.TestIncrementCounter("TestCounter");
                _testService.TestIncrementCounter("TestCounter", 5);
                var metrics = _testService.GetMetrics();

                // Assert
                AssertWithLogging(
                    metrics["TestCounter"],
                    7L,
                    "Counter should be incremented correctly (1+1+5)"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task GetMetrics_Should_IncludeStandardMetrics()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing standard metrics inclusion");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();

                // Act
                var metrics = _testService.GetMetrics();

                // Assert
                AssertConditionWithLogging(
                    metrics.ContainsKey("ServiceName"),
                    "Should include ServiceName"
                );
                AssertConditionWithLogging(
                    metrics.ContainsKey("CurrentState"),
                    "Should include CurrentState"
                );
                AssertConditionWithLogging(
                    metrics.ContainsKey("UptimeSeconds"),
                    "Should include UptimeSeconds"
                );
                AssertConditionWithLogging(
                    metrics.ContainsKey("CorrelationId"),
                    "Should include CorrelationId"
                );
            });
        }

        #endregion

        #region Service Operation Tests

        [Fact]
        public async Task ExecuteServiceOperationAsync_Should_TrackMetrics()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing service operation metric tracking");

                // Arrange
                await _testService.InitializeAsync();
                await _testService.StartAsync();

                // Act
                var result = await _testService.TestServiceOperation();
                var metrics = _testService.GetMetrics();

                // Assert
                AssertWithLogging(result, "operation result", "Operation should return result");
                AssertConditionWithLogging(
                    metrics.ContainsKey("TestOperationCount"),
                    "Operation count should be tracked"
                );
                AssertConditionWithLogging(
                    metrics.ContainsKey("TestOperationLastDurationMs"),
                    "Operation duration should be tracked"
                );
            });
        }

        [Fact]
        public async Task ExecuteServiceOperationAsync_Should_TrackErrors()
        {
            await ExecuteTestWithExpectedExceptionAsync<InvalidOperationException>(
                async () =>
                {
                    LogTestStep("Testing service operation error tracking");

                    await _testService.InitializeAsync();
                    await _testService.StartAsync();
                    
                    await _testService.TestServiceOperationWithError();
                },
                "Test operation error"
            );

            // Verify error was tracked
            var metrics = _testService.GetMetrics();
            AssertConditionWithLogging(
                metrics.ContainsKey("TestOperationErrorCount") && (long)metrics["TestOperationErrorCount"] > 0,
                "Error count should be tracked"
            );
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task Dispose_Should_StopRunningService()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing disposal of running service");

                // Arrange
                var service = new TestServiceImplementation(_mockLogger.Object);
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                service.Dispose();

                // Assert
                AssertWithLogging(
                    service.GetCurrentState(),
                    ServiceState.Stopped,
                    "Service should be stopped on disposal"
                );
                AssertConditionWithLogging(
                    service.DisposeCalled,
                    "Dispose should be called"
                );
            });
        }

        [Fact]
        public async Task Dispose_Should_BeIdempotent()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing multiple disposal calls");

                // Arrange
                var service = new TestServiceImplementation(_mockLogger.Object);
                await service.InitializeAsync();

                // Act - dispose multiple times
                service.Dispose();
                service.Dispose();
                service.Dispose();

                // Assert - should not throw
                AssertConditionWithLogging(true, "Multiple dispose calls should not throw");
            });
        }

        #endregion

        #region Test Helper Class

        /// <summary>
        /// Test implementation of CanonicalServiceBase for testing purposes
        /// </summary>
        private class TestServiceImplementation : CanonicalServiceBase
        {
            public bool OnInitializeAsyncCalled { get; private set; }
            public bool OnStartAsyncCalled { get; private set; }
            public bool OnStopAsyncCalled { get; private set; }
            public bool DisposeCalled { get; private set; }
            
            public bool ShouldFailOnInitialize { get; set; }
            public bool ShouldFailOnStart { get; set; }
            public bool ShouldFailHealthCheck { get; set; }
            
            public bool CustomHealthStatus { get; set; } = true;
            public string CustomHealthMessage { get; set; } = "Healthy";

            public TestServiceImplementation(ITradingLogger logger) 
                : base(logger, "TestService")
            {
            }

            public ServiceState GetCurrentState()
            {
                var metrics = GetMetrics();
                return Enum.Parse<ServiceState>(metrics["CurrentState"].ToString()!);
            }

            public void TestUpdateMetric(string name, object value)
            {
                UpdateMetric(name, value);
            }

            public void TestIncrementCounter(string name, long incrementBy = 1)
            {
                IncrementCounter(name, incrementBy);
            }

            public async Task<string> TestServiceOperation()
            {
                return await ExecuteServiceOperationAsync(
                    async () =>
                    {
                        await Task.Delay(10);
                        return "operation result";
                    },
                    "TestOperation"
                );
            }

            public async Task<string> TestServiceOperationWithError()
            {
                return await ExecuteServiceOperationAsync<string>(
                    async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Test operation error");
                    },
                    "TestOperation"
                );
            }

            protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                OnInitializeAsyncCalled = true;
                
                if (ShouldFailOnInitialize)
                {
                    throw new InvalidOperationException("Initialization failed");
                }
                
                await Task.Delay(10, cancellationToken);
            }

            protected override async Task OnStartAsync(CancellationToken cancellationToken)
            {
                OnStartAsyncCalled = true;
                
                if (ShouldFailOnStart)
                {
                    throw new InvalidOperationException("Startup failed");
                }
                
                await Task.Delay(10, cancellationToken);
            }

            protected override async Task OnStopAsync(CancellationToken cancellationToken)
            {
                OnStopAsyncCalled = true;
                await Task.Delay(10, cancellationToken);
            }

            protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
                OnCheckHealthAsync(CancellationToken cancellationToken)
            {
                if (ShouldFailHealthCheck)
                {
                    throw new InvalidOperationException("Health check error");
                }
                
                var details = new Dictionary<string, object>
                {
                    ["CustomCheck"] = "Performed",
                    ["TestValue"] = 42
                };
                
                return await Task.FromResult((CustomHealthStatus, CustomHealthMessage, details));
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCalled = true;
                base.Dispose(disposing);
            }
        }

        #endregion
    }
}