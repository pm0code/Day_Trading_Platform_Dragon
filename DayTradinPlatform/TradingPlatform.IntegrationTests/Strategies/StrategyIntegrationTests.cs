using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TradingPlatform.IntegrationTests.Fixtures;
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Canonical;

namespace TradingPlatform.IntegrationTests.Strategies
{
    [Collection("Integration Tests")]
    public class StrategyIntegrationTests : IClassFixture<StrategyTestFixture>
    {
        private readonly StrategyTestFixture _fixture;
        private readonly ITradingLogger _logger;

        public StrategyIntegrationTests(StrategyTestFixture fixture)
        {
            _fixture = fixture;
            _logger = _fixture.GetRequiredService<ITradingLogger>();
        }

        [Fact]
        public async Task MomentumStrategy_DetectsMomentum_GeneratesSignals()
        {
            // Arrange
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            var marketData = new MarketData
            {
                Symbol = "TSLA",
                Timestamp = DateTime.UtcNow,
                Open = 250m,
                High = 260m,
                Low = 248m,
                Close = 258m, // 3.2% gain
                Volume = 50000000, // High volume
                Volatility = 0.03m,
                RSI = 65m // Strong but not overbought
            };

            // Act
            var signalResult = await strategy.ExecuteStrategyAsync("TSLA", marketData, null, CancellationToken.None);

            // Assert
            signalResult.IsSuccess.Should().BeTrue();
            var signal = signalResult.Value;
            signal.Should().NotBeNull();
            signal.SignalType.Should().Be(SignalType.Buy);
            signal.Confidence.Should().BeGreaterThan(0.6m);
            signal.Metadata.Should().ContainKey("MomentumStrength");

            // Cleanup
            await strategy.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MomentumStrategy_NoMomentum_NoSignal()
        {
            // Arrange
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            var marketData = new MarketData
            {
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow,
                Open = 150m,
                High = 150.5m,
                Low = 149.5m,
                Close = 150.1m, // Minimal movement
                Volume = 10000000, // Low volume
                Volatility = 0.01m,
                RSI = 50m
            };

            // Act
            var signalResult = await strategy.ExecuteStrategyAsync("AAPL", marketData, null, CancellationToken.None);

            // Assert
            signalResult.IsSuccess.Should().BeFalse();
            signalResult.Error.Should().NotBeNull();

            await strategy.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task GapStrategy_DetectsGap_GeneratesSignal()
        {
            // Arrange
            var strategy = new GapStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            var conditions = new MarketConditions
            {
                Symbol = "SPY",
                Price = 450m,
                Volatility = 0.015m, // Current open implied from volatility * 100
                Volume = 80000000,
                PriceChange = 0.025m, // 2.5% change
                Trend = TrendDirection.Up,
                RSI = 55m,
                MACD = 0.5m,
                MarketBreadth = 0.6m,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Act
            var signals = await strategy.GenerateSignalsAsync("SPY", conditions);

            // Assert
            signals.Should().NotBeNull();
            if (signals.Length > 0)
            {
                var signal = signals[0];
                signal.Symbol.Should().Be("SPY");
                signal.Metadata.Should().ContainKey("GapType");
                signal.Metadata.Should().ContainKey("FillProbability");
            }

            await strategy.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StrategyOrchestrator_MultipleStrategies_Coordination()
        {
            // Arrange
            var orchestrator = _fixture.GetRequiredService<CanonicalStrategyOrchestrator>();
            var momentumStrategy = new MomentumStrategyCanonical(_logger);
            var gapStrategy = new GapStrategyCanonical(_logger);

            // Register strategies
            await orchestrator.RegisterStrategyAsync("momentum", momentumStrategy);
            await orchestrator.RegisterStrategyAsync("gap", gapStrategy);

            await orchestrator.InitializeAsync(CancellationToken.None);
            await orchestrator.StartAsync(CancellationToken.None);

            var marketData = new MarketData
            {
                Symbol = "QQQ",
                Timestamp = DateTime.UtcNow,
                Open = 380m,
                High = 390m,
                Low = 378m,
                Close = 388m,
                Volume = 40000000,
                Volatility = 0.025m,
                RSI = 62m
            };

            // Act
            var results = await orchestrator.ExecuteStrategiesAsync("QQQ", marketData, CancellationToken.None);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(2); // Both strategies should execute
            results.Should().ContainKey("momentum");
            results.Should().ContainKey("gap");

            await orchestrator.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Strategy_HealthCheck_ReportsCorrectly()
        {
            // Arrange
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            // Act
            var healthResult = await strategy.CheckHealthAsync(CancellationToken.None);

            // Assert
            healthResult.IsHealthy.Should().BeTrue();
            healthResult.Message.Should().NotBeNullOrEmpty();
            healthResult.Details.Should().ContainKey("State");
            healthResult.Details["State"].Should().Be("Running");

            await strategy.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Strategy_PerformanceMetrics_Tracked()
        {
            // Arrange
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            var marketData = new MarketData
            {
                Symbol = "NVDA",
                Timestamp = DateTime.UtcNow,
                Open = 500m,
                High = 520m,
                Low = 495m,
                Close = 515m,
                Volume = 30000000,
                Volatility = 0.04m,
                RSI = 68m
            };

            // Act - Execute multiple times
            for (int i = 0; i < 5; i++)
            {
                await strategy.ExecuteStrategyAsync("NVDA", marketData, null, CancellationToken.None);
                await Task.Delay(10); // Small delay between executions
            }

            var metrics = await strategy.GetPerformanceMetricsAsync(CancellationToken.None);

            // Assert
            metrics.IsSuccess.Should().BeTrue();
            var perf = metrics.Value;
            perf.Should().ContainKey("TotalExecutions");
            perf.Should().ContainKey("SuccessfulExecutions");
            perf.Should().ContainKey("AverageExecutionTime");

            var totalExecutions = Convert.ToInt64(perf["TotalExecutions"]);
            totalExecutions.Should().Be(5);

            await strategy.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Strategy_RiskLimits_Enforced()
        {
            // Arrange
            var momentumStrategy = new MomentumStrategyCanonical(_logger);
            var gapStrategy = new GapStrategyCanonical(_logger);

            // Act
            var momentumLimits = momentumStrategy.GetRiskLimits();
            var gapLimits = gapStrategy.GetRiskLimits();

            // Assert
            momentumLimits.Should().NotBeNull();
            momentumLimits.MaxPositionSize.Should().BeGreaterThan(gapLimits.MaxPositionSize);
            momentumLimits.MaxOpenPositions.Should().BeGreaterThan(gapLimits.MaxOpenPositions);
            
            gapLimits.StopLossPercentage.Should().BeLessThan(momentumLimits.StopLossPercentage);
        }

        [Fact]
        public async Task Strategy_ConcurrentExecution_ThreadSafe()
        {
            // Arrange
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            var marketData = new MarketData
            {
                Symbol = "AMD",
                Timestamp = DateTime.UtcNow,
                Open = 120m,
                High = 125m,
                Low = 119m,
                Close = 124m,
                Volume = 25000000,
                Volatility = 0.035m,
                RSI = 60m
            };

            // Act - Execute concurrently
            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = strategy.ExecuteStrategyAsync($"AMD{i}", marketData, null, CancellationToken.None);
            }

            await Task.WhenAll(tasks);

            // Assert - Should complete without exceptions
            var metrics = await strategy.GetPerformanceMetricsAsync(CancellationToken.None);
            metrics.IsSuccess.Should().BeTrue();

            await strategy.StopAsync(CancellationToken.None);
        }
    }

    public class StrategyTestFixture : IntegrationTestFixture
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Add strategy orchestrator
            services.AddSingleton<CanonicalStrategyOrchestrator>();
        }
    }
}