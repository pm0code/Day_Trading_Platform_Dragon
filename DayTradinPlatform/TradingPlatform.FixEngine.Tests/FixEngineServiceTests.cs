using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Services;

namespace TradingPlatform.FixEngine.Tests
{
    /// <summary>
    /// Comprehensive unit tests for FixEngineService.
    /// Ensures compliance with mandatory testing standards (80%+ coverage).
    /// </summary>
    public class FixEngineServiceTests
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly Mock<IFixSessionManager> _mockSessionManager;
        private readonly Mock<IFixOrderManager> _mockOrderManager;
        private readonly Mock<IFixMarketDataManager> _mockMarketDataManager;
        private readonly Mock<IFixMessageProcessor> _mockMessageProcessor;
        private readonly Mock<FixMessagePool> _mockMessagePool;
        private readonly Mock<IFixPerformanceMonitor> _mockPerformanceMonitor;
        private readonly IOptions<FixEngineOptions> _options;
        private readonly FixEngineService _service;
        
        public FixEngineServiceTests()
        {
            _mockLogger = new Mock<ITradingLogger>();
            _mockSessionManager = new Mock<IFixSessionManager>();
            _mockOrderManager = new Mock<IFixOrderManager>();
            _mockMarketDataManager = new Mock<IFixMarketDataManager>();
            _mockMessageProcessor = new Mock<IFixMessageProcessor>();
            _mockMessagePool = new Mock<FixMessagePool>(_mockLogger.Object, 100, 4096);
            _mockPerformanceMonitor = new Mock<IFixPerformanceMonitor>();
            _options = Options.Create(new FixEngineOptions());
            
            // Setup default mock behaviors
            _mockPerformanceMonitor.Setup(x => x.StartActivity(It.IsAny<string>()))
                .Returns(Mock.Of<IDisposable>());
            
            _mockMessagePool.Setup(x => x.GetStats())
                .Returns(new FixMessagePoolStats { UtilizationPercent = 50 });
            
            _service = new FixEngineService(
                _mockLogger.Object,
                _mockSessionManager.Object,
                _mockOrderManager.Object,
                _mockMarketDataManager.Object,
                _mockMessageProcessor.Object,
                _mockMessagePool.Object,
                _mockPerformanceMonitor.Object,
                _options);
        }
        
        [Fact]
        public async Task InitializeAsync_Success_ReturnsSuccess()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.InitializeAsync())
                .ReturnsAsync(TradingResult.Success());
            _mockOrderManager.Setup(x => x.InitializeAsync())
                .ReturnsAsync(TradingResult.Success());
            _mockMarketDataManager.Setup(x => x.InitializeAsync())
                .ReturnsAsync(TradingResult.Success());
            
            // Act
            var result = await _service.InitializeAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            
            // Verify all components initialized
            _mockSessionManager.Verify(x => x.InitializeAsync(), Times.Once);
            _mockOrderManager.Verify(x => x.InitializeAsync(), Times.Once);
            _mockMarketDataManager.Verify(x => x.InitializeAsync(), Times.Once);
        }
        
        [Fact]
        public async Task InitializeAsync_SessionManagerFails_ReturnsFailure()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.InitializeAsync())
                .ReturnsAsync(TradingResult.Failure("Session init failed", "SESSION_ERROR"));
            
            // Act
            var result = await _service.InitializeAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("SESSION_INIT_FAILED");
            result.ErrorMessage.Should().Contain("session manager");
            
            // Verify other components not initialized
            _mockOrderManager.Verify(x => x.InitializeAsync(), Times.Never);
            _mockMarketDataManager.Verify(x => x.InitializeAsync(), Times.Never);
        }
        
        [Fact]
        public async Task SendOrderAsync_ValidRequest_ReturnsOrder()
        {
            // Arrange
            var request = new OrderRequest
            {
                SessionId = "TEST_SESSION",
                Symbol = "AAPL",
                Quantity = 100,
                Price = 150.50m,
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy
            };
            
            var expectedOrder = new FixOrder
            {
                ClOrdId = "CLO123456",
                Symbol = "AAPL",
                Quantity = 100,
                Price = 150.50m,
                Status = OrderStatus.PendingNew
            };
            
            var session = new FixSession { SessionId = "TEST_SESSION", IsConnected = true };
            
            _mockSessionManager.Setup(x => x.GetActiveSessionAsync("TEST_SESSION"))
                .ReturnsAsync(TradingResult<FixSession>.Success(session));
            
            _mockOrderManager.Setup(x => x.CreateAndSendOrderAsync(
                    It.IsAny<FixSession>(),
                    It.IsAny<OrderRequest>(),
                    It.IsAny<IProgress<OrderExecutionProgress>>()))
                .ReturnsAsync(TradingResult<FixOrder>.Success(expectedOrder));
            
            // Act
            var result = await _service.SendOrderAsync(request);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.ClOrdId.Should().Be("CLO123456");
            result.Value.Symbol.Should().Be("AAPL");
            result.Value.Quantity.Should().Be(100);
            result.Value.Price.Should().Be(150.50m);
            
            // Verify metrics recorded
            _mockPerformanceMonitor.Verify(x => x.RecordMetric(
                It.Is<string>(s => s.Contains("OrdersSent")),
                1,
                It.IsAny<(string, string)[]>()), Times.Once);
        }
        
        [Theory]
        [InlineData(null, "Order request cannot be null", "NULL_REQUEST")]
        [InlineData("", "Symbol is required", "MISSING_SYMBOL")]
        [InlineData("AAPL", "Quantity must be positive", "INVALID_QUANTITY")]
        public async Task SendOrderAsync_InvalidRequest_ReturnsFailure(
            string? symbol, string expectedError, string expectedCode)
        {
            // Arrange
            var request = symbol == null ? null : new OrderRequest
            {
                Symbol = symbol,
                Quantity = symbol == "AAPL" ? 0 : 100,
                Price = 150.50m,
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy
            };
            
            // Act
            var result = await _service.SendOrderAsync(request!);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(expectedError);
            result.ErrorCode.Should().Be(expectedCode);
        }
        
        [Fact]
        public async Task SendOrderAsync_NoActiveSession_ReturnsFailure()
        {
            // Arrange
            var request = new OrderRequest
            {
                SessionId = "INVALID_SESSION",
                Symbol = "AAPL",
                Quantity = 100,
                Price = 150.50m
            };
            
            _mockSessionManager.Setup(x => x.GetActiveSessionAsync("INVALID_SESSION"))
                .ReturnsAsync(TradingResult<FixSession>.Failure("No session", "NO_SESSION"));
            
            // Act
            var result = await _service.SendOrderAsync(request);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("NO_ACTIVE_SESSION");
        }
        
        [Fact]
        public async Task CancelOrderAsync_ValidClOrdId_ReturnsSuccess()
        {
            // Arrange
            const string clOrdId = "CLO123456";
            _mockOrderManager.Setup(x => x.CancelOrderAsync(clOrdId, null))
                .ReturnsAsync(TradingResult.Success());
            
            // Act
            var result = await _service.CancelOrderAsync(clOrdId);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            
            // Verify metrics
            _mockPerformanceMonitor.Verify(x => x.RecordMetric(
                It.Is<string>(s => s.Contains("OrdersCanceled")),
                1,
                It.IsAny<(string, string)[]>()), Times.Once);
        }
        
        [Fact]
        public async Task CancelOrderAsync_EmptyClOrdId_ReturnsFailure()
        {
            // Act
            var result = await _service.CancelOrderAsync("");
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("MISSING_CLORDID");
            result.ErrorMessage.Should().Be("Client order ID is required");
        }
        
        [Fact]
        public async Task SubscribeMarketDataAsync_ValidSymbols_ReturnsSuccess()
        {
            // Arrange
            var symbols = new[] { "AAPL", "GOOGL", "MSFT" };
            _mockMarketDataManager.Setup(x => x.SubscribeAsync(symbols, null))
                .ReturnsAsync(TradingResult.Success());
            
            // Act
            var result = await _service.SubscribeMarketDataAsync(symbols);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            
            // Verify metrics
            _mockPerformanceMonitor.Verify(x => x.RecordMetric(
                It.Is<string>(s => s.Contains("MarketDataSubscriptions")),
                3,
                It.IsAny<(string, string)[]>()), Times.Once);
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData(new string[0])]
        public async Task SubscribeMarketDataAsync_NoSymbols_ReturnsFailure(string[]? symbols)
        {
            // Act
            var result = await _service.SubscribeMarketDataAsync(symbols!);
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("NO_SYMBOLS");
            result.ErrorMessage.Should().Be("At least one symbol is required");
        }
        
        [Fact]
        public async Task StartAsync_NotRunning_StartsSuccessfully()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.StartAllSessionsAsync())
                .ReturnsAsync(TradingResult.Success());
            
            // Act
            var result = await _service.StartAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            
            _mockSessionManager.Verify(x => x.StartAllSessionsAsync(), Times.Once);
        }
        
        [Fact]
        public async Task StartAsync_AlreadyRunning_ReturnsSuccess()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.StartAllSessionsAsync())
                .ReturnsAsync(TradingResult.Success());
            await _service.StartAsync();
            
            // Act
            var result = await _service.StartAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Contain("already running");
            
            // Verify start not called again
            _mockSessionManager.Verify(x => x.StartAllSessionsAsync(), Times.Once);
        }
        
        [Fact]
        public async Task StopAsync_Running_StopsSuccessfully()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.StartAllSessionsAsync())
                .ReturnsAsync(TradingResult.Success());
            _mockSessionManager.Setup(x => x.StopAllSessionsAsync())
                .ReturnsAsync(TradingResult.Success());
            
            await _service.StartAsync();
            
            // Act
            var result = await _service.StopAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            
            _mockSessionManager.Verify(x => x.StopAllSessionsAsync(), Times.Once);
        }
        
        [Fact]
        public async Task CheckHealthAsync_EngineRunning_ReturnsHealthy()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.StartAllSessionsAsync())
                .ReturnsAsync(TradingResult.Success());
            _mockSessionManager.Setup(x => x.GetActiveSessionCountAsync())
                .ReturnsAsync(TradingResult<int>.Success(2));
            
            await _service.StartAsync();
            
            // Act
            var result = await _service.CheckHealthAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsHealthy.Should().BeTrue();
            result.Description.Should().Contain("2 active sessions");
        }
        
        [Fact]
        public async Task CheckHealthAsync_NotRunning_ReturnsUnhealthy()
        {
            // Arrange
            _mockSessionManager.Setup(x => x.GetActiveSessionCountAsync())
                .ReturnsAsync(TradingResult<int>.Success(0));
            
            // Act
            var result = await _service.CheckHealthAsync();
            
            // Assert
            result.Should().NotBeNull();
            result.IsHealthy.Should().BeFalse();
            result.Description.Should().Contain("not running");
        }
    }
}