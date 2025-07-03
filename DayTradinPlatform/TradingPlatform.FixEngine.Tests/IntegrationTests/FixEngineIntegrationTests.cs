using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.FixEngine.Compliance;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Performance;
using TradingPlatform.FixEngine.Services;

namespace TradingPlatform.FixEngine.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests for complete FIX engine workflow.
    /// Tests end-to-end scenarios with all components integrated.
    /// </summary>
    public class FixEngineIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IFixEngineService _fixEngine;
        private readonly TestFixServer _testServer;
        
        public FixEngineIntegrationTests()
        {
            // Setup DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            
            _fixEngine = _serviceProvider.GetRequiredService<IFixEngineService>();
            _testServer = new TestFixServer();
        }
        
        private void ConfigureServices(IServiceCollection services)
        {
            // Core services
            services.AddSingleton<ITradingLogger, TestTradingLogger>();
            services.AddSingleton<ISecureConfiguration, TestSecureConfiguration>();
            
            // FIX engine components
            services.AddSingleton<FixMessagePool>();
            services.AddSingleton<IFixMessageParser, FixMessageParser>();
            services.AddSingleton<IFixSessionManager, FixSessionManager>();
            services.AddSingleton<IFixOrderManager, FixOrderManager>();
            services.AddSingleton<IFixMarketDataManager, TestMarketDataManager>();
            services.AddSingleton<IFixMessageProcessor, TestMessageProcessor>();
            services.AddSingleton<IFixPerformanceMonitor, TestPerformanceMonitor>();
            
            // Compliance
            services.AddSingleton<ComplianceConfiguration>(sp => new ComplianceConfiguration
            {
                MaxOrderSize = 10000,
                MaxNotionalValue = 1000000,
                RequireAlgorithmId = true
            });
            services.AddSingleton<IComplianceRuleEngine, TestComplianceRuleEngine>();
            services.AddSingleton<IAuditLogger, TestAuditLogger>();
            services.AddSingleton<IFixComplianceService, FixComplianceService>();
            
            // Performance
            services.AddSingleton<FixPerformanceOptimizer>(sp => 
                new FixPerformanceOptimizer(
                    sp.GetRequiredService<ITradingLogger>(),
                    new[] { 0, 1, 2, 3 }));
            services.AddSingleton<MemoryOptimizer>();
            
            // Main engine
            services.Configure<FixEngineOptions>(options =>
            {
                options.RequireActiveSession = true;
                options.MessageBatchSize = 100;
                options.MaxConcurrentProcessors = 4;
            });
            services.AddSingleton<IFixEngineService, FixEngineService>();
        }
        
        [Fact]
        public async Task FullOrderLifecycle_ShouldCompleteSuccessfully()
        {
            // Arrange
            await _testServer.StartAsync();
            
            // Initialize engine
            var initResult = await _fixEngine.InitializeAsync();
            initResult.IsSuccess.Should().BeTrue();
            
            // Start engine
            var startResult = await _fixEngine.StartAsync();
            startResult.IsSuccess.Should().BeTrue();
            
            // Create test session
            var sessionManager = _serviceProvider.GetRequiredService<IFixSessionManager>();
            var sessionConfig = new FixSessionConfig
            {
                SessionId = "TEST_SESSION",
                SenderCompId = "CLIENT",
                TargetCompId = "EXCHANGE",
                Host = "localhost",
                Port = _testServer.Port,
                UseTls = false, // For test only
                HeartbeatInterval = 30
            };
            
            var sessionResult = await sessionManager.CreateSessionAsync(sessionConfig);
            sessionResult.IsSuccess.Should().BeTrue();
            
            // Act - Submit order
            var orderRequest = new OrderRequest
            {
                SessionId = "TEST_SESSION",
                Symbol = "AAPL",
                Quantity = 100,
                Price = 150.50m,
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy,
                TimeInForce = TimeInForce.Day,
                AlgorithmId = "ALGO123",
                TradingCapacity = TradingCapacity.Principal
            };
            
            var progress = new Progress<OrderExecutionProgress>(p =>
            {
                Console.WriteLine($"Order progress: {p.Status} - {p.PercentComplete}%");
            });
            
            var orderResult = await _fixEngine.SendOrderAsync(orderRequest, progress);
            
            // Assert
            orderResult.IsSuccess.Should().BeTrue();
            orderResult.Value.Should().NotBeNull();
            orderResult.Value!.Symbol.Should().Be("AAPL");
            orderResult.Value.Quantity.Should().Be(100);
            orderResult.Value.Price.Should().Be(150.50m);
            orderResult.Value.Status.Should().Be(OrderStatus.PendingNew);
            
            // Wait for execution
            await Task.Delay(100);
            
            // Check order status
            var orderManager = _serviceProvider.GetRequiredService<IFixOrderManager>();
            var statusResult = await orderManager.GetOrderStatusAsync(orderResult.Value.ClOrdId);
            statusResult.IsSuccess.Should().BeTrue();
            
            // Clean up
            await _fixEngine.StopAsync();
        }
        
        [Fact]
        public async Task ComplianceViolation_ShouldRejectOrder()
        {
            // Arrange
            await InitializeEngine();
            
            // Act - Submit oversized order
            var orderRequest = new OrderRequest
            {
                SessionId = "TEST_SESSION",
                Symbol = "AAPL",
                Quantity = 100000, // Exceeds max order size
                Price = 150.50m,
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy
            };
            
            var orderResult = await _fixEngine.SendOrderAsync(orderRequest);
            
            // Assert
            orderResult.IsSuccess.Should().BeFalse();
            orderResult.ErrorCode.Should().Be("COMPLIANCE_FAILED");
            orderResult.ErrorMessage.Should().Contain("exceeds maximum");
        }
        
        [Fact]
        public async Task OrderCancellation_ShouldSucceed()
        {
            // Arrange
            await InitializeEngine();
            
            // Submit order
            var orderRequest = new OrderRequest
            {
                SessionId = "TEST_SESSION",
                Symbol = "MSFT",
                Quantity = 50,
                Price = 350.00m,
                OrderType = OrderType.Limit,
                Side = OrderSide.Sell,
                AlgorithmId = "ALGO456",
                TradingCapacity = TradingCapacity.Principal
            };
            
            var orderResult = await _fixEngine.SendOrderAsync(orderRequest);
            orderResult.IsSuccess.Should().BeTrue();
            
            // Act - Cancel order
            var cancelResult = await _fixEngine.CancelOrderAsync(
                orderResult.Value!.ClOrdId,
                "TEST_SESSION");
            
            // Assert
            cancelResult.IsSuccess.Should().BeTrue();
            
            // Verify status changed
            await Task.Delay(50);
            var orderManager = _serviceProvider.GetRequiredService<IFixOrderManager>();
            var statusResult = await orderManager.GetOrderStatusAsync(orderResult.Value.ClOrdId);
            statusResult.Value!.Status.Should().BeOneOf(
                OrderStatus.PendingCancel, 
                OrderStatus.Canceled);
        }
        
        [Fact]
        public async Task MarketDataSubscription_ShouldReceiveUpdates()
        {
            // Arrange
            await InitializeEngine();
            var marketDataReceived = false;
            var marketDataManager = (TestMarketDataManager)_serviceProvider
                .GetRequiredService<IFixMarketDataManager>();
            
            marketDataManager.OnMarketDataUpdate = (symbol, price) =>
            {
                marketDataReceived = true;
                symbol.Should().Be("GOOGL");
                price.Should().BeGreaterThan(0);
            };
            
            // Act
            var subscribeResult = await _fixEngine.SubscribeMarketDataAsync(
                new[] { "GOOGL" },
                "TEST_SESSION");
            
            // Assert
            subscribeResult.IsSuccess.Should().BeTrue();
            
            // Wait for market data
            await Task.Delay(200);
            marketDataReceived.Should().BeTrue();
        }
        
        [Fact]
        public async Task PerformanceMetrics_ShouldMeetTargets()
        {
            // Arrange
            await InitializeEngine();
            var performanceOptimizer = _serviceProvider
                .GetRequiredService<FixPerformanceOptimizer>();
            
            performanceOptimizer.WarmUp();
            
            // Act - Submit multiple orders and measure
            var latencies = new List<long>();
            
            for (int i = 0; i < 100; i++)
            {
                var start = performanceOptimizer.GetHighResolutionTimestamp();
                
                var orderRequest = new OrderRequest
                {
                    SessionId = "TEST_SESSION",
                    Symbol = $"TEST{i:D3}",
                    Quantity = 100,
                    Price = 100.00m + i,
                    OrderType = OrderType.Limit,
                    Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell,
                    AlgorithmId = "PERF_TEST",
                    TradingCapacity = TradingCapacity.Principal
                };
                
                var result = await _fixEngine.SendOrderAsync(orderRequest);
                
                var end = performanceOptimizer.GetHighResolutionTimestamp();
                
                if (result.IsSuccess)
                {
                    latencies.Add(end - start);
                }
            }
            
            // Assert - Check latency percentiles
            latencies.Sort();
            var p50 = latencies[latencies.Count / 2];
            var p99 = latencies[(int)(latencies.Count * 0.99)];
            
            // Convert to microseconds
            p50 = p50 / 1000;
            p99 = p99 / 1000;
            
            Console.WriteLine($"Order submission latency - P50: {p50}μs, P99: {p99}μs");
            
            p99.Should().BeLessThan(50000, "P99 latency should be under 50ms");
        }
        
        [Fact]
        public async Task MemoryStability_ShouldMaintainLowGC()
        {
            // Arrange
            await InitializeEngine();
            var memoryOptimizer = _serviceProvider.GetRequiredService<MemoryOptimizer>();
            
            memoryOptimizer.OptimizeGarbageCollector();
            memoryOptimizer.PreAllocateBuffers(1000, 4096);
            
            var statsBefore = memoryOptimizer.GetMemoryStats();
            
            // Act - High frequency operations
            var tasks = new List<Task>();
            
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var orderRequest = new OrderRequest
                        {
                            SessionId = "TEST_SESSION",
                            Symbol = "MEM_TEST",
                            Quantity = 10,
                            Price = 50.00m,
                            OrderType = OrderType.Market,
                            Side = OrderSide.Buy,
                            AlgorithmId = "MEM_TEST",
                            TradingCapacity = TradingCapacity.Principal
                        };
                        
                        await _fixEngine.SendOrderAsync(orderRequest);
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            
            var statsAfter = memoryOptimizer.GetMemoryStats();
            
            // Assert
            var gen0Increase = statsAfter.Gen0Collections - statsBefore.Gen0Collections;
            var gen2Increase = statsAfter.Gen2Collections - statsBefore.Gen2Collections;
            
            Console.WriteLine($"GC Collections - Gen0: {gen0Increase}, Gen2: {gen2Increase}");
            
            gen2Increase.Should().Be(0, "No Gen2 collections should occur");
            gen0Increase.Should().BeLessThan(10, "Minimal Gen0 collections");
        }
        
        private async Task InitializeEngine()
        {
            await _testServer.StartAsync();
            
            var initResult = await _fixEngine.InitializeAsync();
            initResult.IsSuccess.Should().BeTrue();
            
            var startResult = await _fixEngine.StartAsync();
            startResult.IsSuccess.Should().BeTrue();
            
            // Create test session
            var sessionManager = _serviceProvider.GetRequiredService<IFixSessionManager>();
            var sessionConfig = new FixSessionConfig
            {
                SessionId = "TEST_SESSION",
                SenderCompId = "CLIENT",
                TargetCompId = "EXCHANGE",
                Host = "localhost",
                Port = _testServer.Port,
                UseTls = false,
                HeartbeatInterval = 30
            };
            
            await sessionManager.CreateSessionAsync(sessionConfig);
            await sessionManager.StartAllSessionsAsync();
        }
        
        public void Dispose()
        {
            _fixEngine?.StopAsync().Wait();
            _testServer?.Dispose();
            _serviceProvider?.Dispose();
        }
    }
    
    // Test implementations would go here...
    // Including: TestFixServer, TestTradingLogger, TestSecureConfiguration,
    // TestMarketDataManager, TestMessageProcessor, TestPerformanceMonitor,
    // TestComplianceRuleEngine, TestAuditLogger
    
    internal class TestFixServer : IDisposable
    {
        public int Port { get; } = 9876;
        
        public Task StartAsync()
        {
            // Mock FIX server implementation
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            // Cleanup
        }
    }
    
    internal class TestTradingLogger : ITradingLogger
    {
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
        public void LogError(Exception ex, string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        // Other interface methods...
    }
}