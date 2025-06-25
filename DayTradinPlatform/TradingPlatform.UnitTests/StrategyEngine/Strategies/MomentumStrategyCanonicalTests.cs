using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Builders;
using TradingPlatform.UnitTests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.StrategyEngine.Strategies
{
    public class MomentumStrategyCanonicalTests : CanonicalServiceTestBase<MomentumStrategyCanonical>
    {
        public MomentumStrategyCanonicalTests(ITestOutputHelper output) : base(output)
        {
        }

        protected override MomentumStrategyCanonical CreateService()
        {
            return new MomentumStrategyCanonical(MockLogger.Object);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSucceed()
        {
            await TestInitializationAsync();
        }

        [Fact]
        public async Task StartAsync_AfterInitialization_ShouldSucceed()
        {
            await TestStartAsync();
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldSucceed()
        {
            await TestStopAsync();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenRunning_ShouldReturnHealthy()
        {
            await TestHealthCheckAsync();
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithStrongMomentum_GeneratesBuySignal()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithSymbol("TSLA")
                .WithBasePrice(200m)
                .WithVolatility(0.03m)
                .WithUptrend()
                .WithHighVolume()
                .Build();

            // Act
            var result = await Service.ExecuteStrategyAsync("TSLA", marketData, null, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var signal = result.Value;
            signal.Should().NotBeNull();
            signal.SignalType.Should().Be(SignalType.Buy);
            signal.Confidence.Should().BeGreaterThan(0.6m);
            signal.Metadata.Should().ContainKey("MomentumStrength");
            signal.Metadata.Should().ContainKey("VolumeRatio");
            signal.Metadata.Should().ContainKey("RSI");
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithWeakMomentum_DoesNotGenerateSignal()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithSymbol("AAPL")
                .WithBasePrice(150m)
                .WithVolatility(0.005m) // Low volatility
                .WithLowVolume()
                .Build();

            // Ensure weak momentum
            marketData.Close = marketData.Open * 1.001m; // Only 0.1% move

            // Act
            var result = await Service.ExecuteStrategyAsync("AAPL", marketData, null, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Error!.Message.Should().Contain("No momentum signal");
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithOverboughtRSI_GeneratesSellSignal()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithSymbol("NVDA")
                .WithBasePrice(500m)
                .Build();

            // Set overbought conditions
            marketData.RSI = 85m; // Very overbought
            marketData.Close = marketData.Open * 0.98m; // Downward movement

            // Act
            var result = await Service.ExecuteStrategyAsync("NVDA", marketData, null, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var signal = result.Value;
            signal.SignalType.Should().Be(SignalType.Sell);
            signal.Metadata["RSI"].Should().Be(85m);
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithOversoldRSI_GeneratesBuySignal()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithSymbol("AMD")
                .WithBasePrice(100m)
                .Build();

            // Set oversold conditions
            marketData.RSI = 20m; // Very oversold
            marketData.Close = marketData.Open * 1.02m; // Upward bounce
            marketData.Volume = 50000000; // Good volume

            // Act
            var result = await Service.ExecuteStrategyAsync("AMD", marketData, null, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var signal = result.Value;
            signal.SignalType.Should().Be(SignalType.Buy);
            signal.Metadata["RSI"].Should().Be(20m);
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithCurrentPosition_ConsidersPositionInSignal()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithSymbol("GOOGL")
                .WithBasePrice(150m)
                .WithDowntrend()
                .Build();

            var currentPosition = new PositionInfo
            {
                Symbol = "GOOGL",
                Quantity = 100,
                AveragePrice = 145m,
                UnrealizedPnL = 500m // Profitable position
            };

            // Act
            var result = await Service.ExecuteStrategyAsync("GOOGL", marketData, currentPosition, TestCts.Token);

            // Assert
            if (result.IsSuccess)
            {
                var signal = result.Value;
                // With a profitable position and downtrend, might suggest taking profits
                signal.SignalType.Should().BeOneOf(SignalType.Sell, SignalType.Hold);
            }
        }

        [Theory]
        [InlineData(0.05, 2.0, true)]   // 5% move, 2x volume = strong momentum
        [InlineData(0.02, 1.5, true)]   // 2% move, 1.5x volume = moderate momentum
        [InlineData(0.005, 0.8, false)] // 0.5% move, low volume = no momentum
        [InlineData(0.03, 0.5, false)]  // 3% move but very low volume = no signal
        public async Task ExecuteStrategyAsync_MomentumThresholds_WorkCorrectly(
            decimal priceChangePercent, decimal volumeRatio, bool shouldGenerateSignal)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var basePrice = 100m;
            var marketData = new MarketData
            {
                Symbol = "TEST",
                Timestamp = DateTime.UtcNow,
                Open = basePrice,
                High = basePrice * (1 + priceChangePercent + 0.01m),
                Low = basePrice * 0.99m,
                Close = basePrice * (1 + priceChangePercent),
                Volume = (long)(50000000 * volumeRatio),
                RSI = 50m // Neutral RSI
            };

            // Act
            var result = await Service.ExecuteStrategyAsync("TEST", marketData, null, TestCts.Token);

            // Assert
            if (shouldGenerateSignal)
            {
                result.Should().BeSuccess();
                result.Value.Should().NotBeNull();
            }
            else
            {
                result.Should().BeFailure();
            }
        }

        [Fact]
        public async Task GetRiskLimits_ReturnsConservativeLimits()
        {
            // Arrange
            Service = CreateService();

            // Act
            var limits = Service.GetRiskLimits();

            // Assert
            limits.Should().NotBeNull();
            limits.MaxPositionSize.Should().Be(10000m);
            limits.MaxLeverage.Should().Be(2m);
            limits.MaxOpenPositions.Should().Be(5);
            limits.StopLossPercentage.Should().Be(0.02m); // 2%
            limits.TakeProfitPercentage.Should().Be(0.05m); // 5%
            limits.MaxDailyLoss.Should().Be(0.06m); // 6%
            limits.MaxDrawdown.Should().Be(0.15m); // 15%
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WhenNotRunning_ReturnsError()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            // Not calling StartAsync

            var marketData = new MarketDataBuilder().Build();

            // Act
            var result = await Service.ExecuteStrategyAsync("AAPL", marketData, null, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Error!.Message.Should().Contain("not running");
        }

        [Fact]
        public async Task GetPerformanceMetricsAsync_TracksExecutions()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithUptrend()
                .WithHighVolume()
                .Build();

            // Execute strategy multiple times
            for (int i = 0; i < 5; i++)
            {
                await Service.ExecuteStrategyAsync($"SYM{i}", marketData, null, TestCts.Token);
            }

            // Act
            await TestPerformanceMetricsAsync();
        }

        [Fact]
        public async Task ExecuteStrategyAsync_WithCancellation_HandlesProperly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var marketData = new MarketDataBuilder().Build();

            // Act & Assert
            await TestCancellationAsync(ct => 
                Service.ExecuteStrategyAsync("AAPL", marketData, null, ct));
        }

        [Fact]
        public async Task ConcurrentExecutions_HandleSafely()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var marketData = new MarketDataBuilder()
                .WithUptrend()
                .WithHighVolume()
                .Build();

            // Act & Assert
            await TestConcurrencyAsync(async () =>
            {
                var symbol = $"SYM{Random.Shared.Next(100)}";
                await Service.ExecuteStrategyAsync(symbol, marketData, null, TestCts.Token);
            });
        }
    }
}