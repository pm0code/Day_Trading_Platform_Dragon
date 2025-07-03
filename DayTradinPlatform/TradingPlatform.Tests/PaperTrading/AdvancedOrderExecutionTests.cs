using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.PaperTrading.Services;

namespace TradingPlatform.Tests.PaperTrading
{
    /// <summary>
    /// Comprehensive unit tests for advanced order execution (TWAP, VWAP, Iceberg)
    /// </summary>
    public class AdvancedOrderExecutionTests
    {
        private readonly Mock<IOrderExecutionEngineExtended> _mockExecutionEngine;
        private readonly Mock<IMarketDataService> _mockMarketDataService;
        private readonly Mock<IVolumeAnalysisService> _mockVolumeAnalysisService;
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly AdvancedOrderConfiguration _configuration;
        private readonly AdvancedOrderExecutionService _service;

        public AdvancedOrderExecutionTests()
        {
            _mockExecutionEngine = new Mock<IOrderExecutionEngineExtended>();
            _mockMarketDataService = new Mock<IMarketDataService>();
            _mockVolumeAnalysisService = new Mock<IVolumeAnalysisService>();
            _mockLogger = new Mock<ITradingLogger>();
            
            _configuration = new AdvancedOrderConfiguration
            {
                MinOrderSize = 100m,
                MaxMarketParticipation = 0.25m,
                MinSliceInterval = TimeSpan.FromSeconds(30),
                MaxSliceInterval = TimeSpan.FromMinutes(5)
            };

            _service = new AdvancedOrderExecutionService(
                _mockExecutionEngine.Object,
                _mockMarketDataService.Object,
                _mockVolumeAnalysisService.Object,
                _configuration,
                _mockLogger.Object);
        }

        #region TWAP Order Tests

        [Fact]
        public async Task SubmitTwapOrder_ValidOrder_ShouldSucceed()
        {
            // Arrange
            var order = new TwapOrder(
                OrderId: "TWAP001",
                Symbol: "AAPL",
                Side: OrderSide.Buy,
                TotalQuantity: 10000m,
                StartTime: DateTime.UtcNow.AddMinutes(1),
                EndTime: DateTime.UtcNow.AddHours(2),
                NumberOfSlices: 10,
                RandomizeSliceSize: true,
                RandomizeSliceTiming: true,
                MinSliceSize: 500m,
                MaxSliceSize: 1500m,
                MinInterval: TimeSpan.FromMinutes(5),
                MaxInterval: TimeSpan.FromMinutes(15),
                LimitPrice: 150.00m,
                ClientOrderId: "CLIENT001"
            );

            // Act
            var result = await _service.SubmitTwapOrderAsync(order);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("TWAP001", result.OrderId);
            Assert.Equal(AdvancedOrderType.TWAP, result.Type);
            Assert.Contains("successfully", result.Message);
        }

        [Fact]
        public async Task SubmitTwapOrder_InvalidTimeWindow_ShouldFail()
        {
            // Arrange
            var order = new TwapOrder(
                OrderId: "TWAP002",
                Symbol: "AAPL",
                Side: OrderSide.Buy,
                TotalQuantity: 10000m,
                StartTime: DateTime.UtcNow.AddHours(2), // Start after end
                EndTime: DateTime.UtcNow.AddHours(1),   // End before start
                NumberOfSlices: 10,
                RandomizeSliceSize: false,
                RandomizeSliceTiming: false,
                MinSliceSize: null,
                MaxSliceSize: null,
                MinInterval: null,
                MaxInterval: null,
                LimitPrice: null
            );

            // Act
            var result = await _service.SubmitTwapOrderAsync(order);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.OrderId);
            Assert.NotNull(result.Validation);
            Assert.Contains("Start time must be before end time", result.Validation.Errors);
        }

        [Fact]
        public async Task TwapOrder_CalculateProperties_ShouldBeCorrect()
        {
            // Arrange
            var order = new TwapOrder(
                OrderId: "TWAP003",
                Symbol: "MSFT",
                Side: OrderSide.Sell,
                TotalQuantity: 5000m,
                StartTime: DateTime.UtcNow,
                EndTime: DateTime.UtcNow.AddHours(1),
                NumberOfSlices: 5,
                RandomizeSliceSize: false,
                RandomizeSliceTiming: false,
                MinSliceSize: null,
                MaxSliceSize: null,
                MinInterval: null,
                MaxInterval: null,
                LimitPrice: 300m
            );

            // Act & Assert
            Assert.Equal(TimeSpan.FromHours(1), order.Duration);
            Assert.Equal(1000m, order.AverageSliceSize); // 5000 / 5
            Assert.Equal(TimeSpan.FromMinutes(15), order.AverageInterval); // 60 min / 4 intervals
        }

        #endregion

        #region VWAP Order Tests

        [Fact]
        public async Task SubmitVwapOrder_ValidOrder_ShouldSucceed()
        {
            // Arrange
            var order = new VwapOrder(
                OrderId: "VWAP001",
                Symbol: "GOOGL",
                Side: OrderSide.Buy,
                TotalQuantity: 15000m,
                StartTime: DateTime.UtcNow.AddMinutes(1),
                EndTime: DateTime.UtcNow.AddHours(4),
                ParticipationRate: 0.10m, // 10%
                UseHistoricalVolume: true,
                HistoricalDays: 20,
                LimitPrice: 2500m,
                MaxParticipationRate: 0.25m,
                MinSliceSize: 100m,
                MaxSliceSize: 1000m,
                ClientOrderId: "CLIENT002"
            );

            // Mock volume profile
            var volumeProfile = GenerateMockVolumeProfile();
            _mockVolumeAnalysisService
                .Setup(x => x.GetHistoricalVolumeProfileAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(volumeProfile);

            // Act
            var result = await _service.SubmitVwapOrderAsync(order);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("VWAP001", result.OrderId);
            Assert.Equal(AdvancedOrderType.VWAP, result.Type);
        }

        [Fact]
        public async Task SubmitVwapOrder_InvalidParticipationRate_ShouldFail()
        {
            // Arrange
            var order = new VwapOrder(
                OrderId: "VWAP002",
                Symbol: "GOOGL",
                Side: OrderSide.Sell,
                TotalQuantity: 15000m,
                StartTime: DateTime.UtcNow,
                EndTime: DateTime.UtcNow.AddHours(4),
                ParticipationRate: 0.35m, // 35% - too high
                UseHistoricalVolume: false,
                HistoricalDays: 0,
                LimitPrice: null,
                MaxParticipationRate: 0.25m,
                MinSliceSize: 100m,
                MaxSliceSize: 1000m
            );

            // Act
            var result = await _service.SubmitVwapOrderAsync(order);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Validation);
            Assert.Contains("Participation rate", result.Validation.Errors.First());
        }

        [Fact]
        public void VwapOrder_IsValidParticipationRate_ShouldValidateCorrectly()
        {
            // Arrange & Act & Assert
            var validOrder = new VwapOrder(
                "V1", "AAPL", OrderSide.Buy, 1000m,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
                0.10m, false, 0, null, 0.25m, 100m, 500m);
            Assert.True(validOrder.IsValidParticipationRate);

            var invalidOrder = new VwapOrder(
                "V2", "AAPL", OrderSide.Buy, 1000m,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
                0.35m, false, 0, null, 0.25m, 100m, 500m);
            Assert.False(invalidOrder.IsValidParticipationRate);
        }

        #endregion

        #region Iceberg Order Tests

        [Fact]
        public async Task SubmitIcebergOrder_ValidOrder_ShouldSucceed()
        {
            // Arrange
            var order = new IcebergOrder(
                OrderId: "ICE001",
                Symbol: "TSLA",
                Side: OrderSide.Buy,
                TotalQuantity: 50000m,
                VisibleQuantity: 1000m, // Show only 1000 shares
                UnderlyingType: OrderType.Limit,
                LimitPrice: 800m,
                RandomizeVisibleQuantity: true,
                MinVisibleQuantity: 500m,
                MaxVisibleQuantity: 1500m,
                TimeInForce: TimeInForce.Day,
                ClientOrderId: "CLIENT003"
            );

            // Act
            var result = await _service.SubmitIcebergOrderAsync(order);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("ICE001", result.OrderId);
            Assert.Equal(AdvancedOrderType.Iceberg, result.Type);
        }

        [Fact]
        public async Task SubmitIcebergOrder_VisibleQuantityTooLarge_ShouldFail()
        {
            // Arrange
            var order = new IcebergOrder(
                OrderId: "ICE002",
                Symbol: "TSLA",
                Side: OrderSide.Sell,
                TotalQuantity: 10000m,
                VisibleQuantity: 3000m, // 30% - too much
                UnderlyingType: OrderType.Market,
                LimitPrice: null,
                RandomizeVisibleQuantity: false,
                MinVisibleQuantity: null,
                MaxVisibleQuantity: null
            );

            // Act
            var result = await _service.SubmitIcebergOrderAsync(order);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Validation);
            Assert.Contains("Visible quantity", result.Validation.Errors.First());
        }

        [Fact]
        public void IcebergOrder_CalculateProperties_ShouldBeCorrect()
        {
            // Arrange
            var order = new IcebergOrder(
                OrderId: "ICE003",
                Symbol: "NVDA",
                Side: OrderSide.Buy,
                TotalQuantity: 10000m,
                VisibleQuantity: 500m,
                UnderlyingType: OrderType.Limit,
                LimitPrice: 400m,
                RandomizeVisibleQuantity: false,
                MinVisibleQuantity: null,
                MaxVisibleQuantity: null
            );

            // Act & Assert
            Assert.Equal(20, order.EstimatedRefills); // 10000 / 500
            Assert.True(order.IsValidVisibleQuantity); // 500 < 10000 and 500/10000 = 5% < 20%
        }

        #endregion

        #region Order Management Tests

        [Fact]
        public async Task GetOrderStatus_ExistingOrder_ShouldReturnStatus()
        {
            // Arrange
            var twapOrder = new TwapOrder(
                "TWAP004", "AAPL", OrderSide.Buy, 5000m,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
                5, false, false, null, null, null, null, 150m);

            await _service.SubmitTwapOrderAsync(twapOrder);

            // Act
            var status = await _service.GetOrderStatusAsync("TWAP004");

            // Assert
            Assert.NotNull(status);
            Assert.Equal("TWAP004", status.OrderId);
            Assert.Equal(AdvancedOrderType.TWAP, status.Type);
            Assert.Equal(5000m, status.TotalQuantity);
        }

        [Fact]
        public async Task CancelOrder_ActiveOrder_ShouldSucceed()
        {
            // Arrange
            var order = new IcebergOrder(
                "ICE004", "AMZN", OrderSide.Buy, 2000m,
                200m, OrderType.Limit, 3000m, false, null, null);

            await _service.SubmitIcebergOrderAsync(order);

            // Act
            var result = await _service.CancelOrderAsync("ICE004");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CancelOrder_NonExistentOrder_ShouldReturnFalse()
        {
            // Act
            var result = await _service.CancelOrderAsync("NONEXISTENT");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Advanced Configuration Tests

        [Fact]
        public void AdvancedOrderConfiguration_DefaultValues_ShouldBeReasonable()
        {
            // Arrange
            var config = new AdvancedOrderConfiguration();

            // Assert
            Assert.Equal(0.25m, config.MaxMarketParticipation);
            Assert.Equal(TimeSpan.FromSeconds(30), config.MinSliceInterval);
            Assert.Equal(TimeSpan.FromMinutes(5), config.MaxSliceInterval);
            Assert.Equal(0.2m, config.SliceSizeRandomization);
            Assert.Equal(0.15m, config.SliceTimingRandomization);
            Assert.True(config.EnableSmartRouting);
            Assert.True(config.EnableAntiGaming);
            Assert.Equal(0.005m, config.MaxSlippageTolerance);
            Assert.Equal(100m, config.MinOrderSize);
            Assert.True(config.UseAdaptiveAlgorithms);
        }

        #endregion

        #region Execution Statistics Tests

        [Fact]
        public void ExecutionStatistics_ShouldTrackMetricsCorrectly()
        {
            // Arrange
            var stats = new ExecutionStatistics(
                AverageSlippage: 0.0005m,
                TotalSlippage: 0.005m,
                BenchmarkPrice: 150.00m,
                PerformanceVsBenchmark: -0.0002m,
                AverageExecutionTime: TimeSpan.FromMilliseconds(250),
                MarketImpact: 0.001m,
                TotalCommission: 50m,
                SuccessfulSlices: 9,
                FailedSlices: 1
            );

            // Assert
            Assert.Equal(0.0005m, stats.AverageSlippage);
            Assert.Equal(0.005m, stats.TotalSlippage);
            Assert.Equal(150.00m, stats.BenchmarkPrice);
            Assert.Equal(-0.0002m, stats.PerformanceVsBenchmark);
            Assert.Equal(TimeSpan.FromMilliseconds(250), stats.AverageExecutionTime);
            Assert.Equal(0.001m, stats.MarketImpact);
            Assert.Equal(50m, stats.TotalCommission);
            Assert.Equal(9, stats.SuccessfulSlices);
            Assert.Equal(1, stats.FailedSlices);
        }

        #endregion

        #region Helper Methods

        private List<VolumeProfile> GenerateMockVolumeProfile()
        {
            var profiles = new List<VolumeProfile>();
            var baseTime = DateTime.Today.AddHours(9.5); // 9:30 AM
            
            for (int i = 0; i < 13; i++) // 13 half-hour periods
            {
                var time = baseTime.AddMinutes(i * 30);
                var volumePercentage = GetTypicalVolumePercentage(time.Hour, time.Minute);
                
                profiles.Add(new VolumeProfile(
                    Time: time,
                    Volume: 1000000m * volumePercentage,
                    Price: 150m + (_random.Next(-100, 100) / 100m),
                    VolumePercentage: volumePercentage
                ));
            }
            
            return profiles;
        }

        private decimal GetTypicalVolumePercentage(int hour, int minute)
        {
            // U-shaped volume profile (high at open/close)
            if (hour == 9 && minute < 45) return 0.15m;
            if (hour == 15 && minute > 30) return 0.12m;
            if (hour >= 11 && hour <= 13) return 0.05m; // Lunch hour
            return 0.07m; // Normal trading hours
        }

        private readonly Random _random = new Random();

        #endregion
    }

    /// <summary>
    /// Integration tests for advanced order execution
    /// </summary>
    public class AdvancedOrderExecutionIntegrationTests
    {
        [Fact]
        public async Task TwapOrder_FullExecution_ShouldCompleteSuccessfully()
        {
            // This would be an integration test with real dependencies
            // Testing the full execution flow of a TWAP order
            await Task.CompletedTask;
        }

        [Fact]
        public async Task VwapOrder_WithRealVolumeData_ShouldFollowVolumeProfile()
        {
            // This would test VWAP execution against real volume data
            await Task.CompletedTask;
        }

        [Fact]
        public async Task IcebergOrder_MarketImpact_ShouldBeMinimal()
        {
            // This would test that iceberg orders minimize market impact
            await Task.CompletedTask;
        }
    }
}