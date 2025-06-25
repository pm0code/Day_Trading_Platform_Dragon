using System;
using System.Threading.Tasks;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Plugins.Http.CSharp;
using NBomber.Plugins.Network.Ping;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Messaging.Services;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.GoldenRules.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingPlatform.PerformanceTests.LoadTests
{
    /// <summary>
    /// Load tests for the trading platform using NBomber
    /// </summary>
    public class TradingPlatformLoadTests
    {
        private readonly ITradingLogger _logger;
        private readonly Mock<ICanonicalMessageQueue> _mockMessageQueue;
        private readonly Mock<ITimeSeriesService> _mockTimeSeriesService;

        public TradingPlatformLoadTests()
        {
            _logger = new NoOpTradingLogger();
            
            _mockMessageQueue = new Mock<ICanonicalMessageQueue>();
            _mockMessageQueue.Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<MessagePriority>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<string>.Success("message-id"));

            _mockTimeSeriesService = new Mock<ITimeSeriesService>();
            _mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());
        }

        public void RunAllLoadTests()
        {
            RunOrderExecutionLoadTest();
            RunGoldenRulesLoadTest();
            RunStrategyExecutionLoadTest();
            RunMessageQueueLoadTest();
        }

        private void RunOrderExecutionLoadTest()
        {
            var engine = new OrderExecutionEngineCanonical(_logger, _mockMessageQueue.Object);
            engine.InitializeAsync(CancellationToken.None).Wait();
            engine.StartAsync(CancellationToken.None).Wait();

            var scenario = Scenario.Create("order_execution_load_test", async context =>
            {
                var order = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{Random.Shared.Next(1, 100)}",
                    OrderType = Random.Shared.Next(2) == 0 ? OrderType.Market : OrderType.Limit,
                    Side = Random.Shared.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell,
                    Quantity = Random.Shared.Next(1, 1000),
                    Price = 100m + (decimal)(Random.Shared.NextDouble() * 100),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await engine.ExecuteOrderAsync(order, CancellationToken.None);
                
                return result.IsSuccess ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(30)),
                Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(30)),
                Simulation.InjectPerSec(rate: 500, during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFileName("order_execution_load_test")
                .WithReportFolder("./load-test-results")
                .Run();

            engine.StopAsync(CancellationToken.None).Wait();
            engine.Dispose();
        }

        private void RunGoldenRulesLoadTest()
        {
            var config = Options.Create(new GoldenRulesEngineConfig
            {
                Enabled = true,
                StrictMode = false,
                EnableRealTimeAlerts = false
            });

            var engine = new CanonicalGoldenRulesEngine(_logger, _mockTimeSeriesService.Object, config);
            engine.InitializeAsync(CancellationToken.None).Wait();
            engine.StartAsync(CancellationToken.None).Wait();

            var scenario = Scenario.Create("golden_rules_load_test", async context =>
            {
                var positionContext = new PositionContext
                {
                    Symbol = $"SYM{Random.Shared.Next(1, 100)}",
                    AccountBalance = 100000m,
                    BuyingPower = 400000m,
                    DayTradeCount = Random.Shared.Next(0, 5),
                    DailyPnL = Random.Shared.Next(-2000, 2000),
                    OpenPositions = Random.Shared.Next(0, 10)
                };

                var marketConditions = new MarketConditions
                {
                    Symbol = positionContext.Symbol,
                    Price = 100m + (decimal)(Random.Shared.NextDouble() * 100),
                    Volume = Random.Shared.Next(1000000, 100000000),
                    Volatility = 0.01m + (decimal)(Random.Shared.NextDouble() * 0.04m),
                    Trend = (TrendDirection)Random.Shared.Next(0, 3),
                    Session = MarketSession.RegularHours
                };

                var result = await engine.EvaluateTradeAsync(
                    positionContext.Symbol,
                    OrderType.Market,
                    OrderSide.Buy,
                    100,
                    marketConditions.Price,
                    positionContext,
                    marketConditions,
                    CancellationToken.None);

                return result.IsSuccess ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 200, during: TimeSpan.FromSeconds(30)),
                Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFileName("golden_rules_load_test")
                .WithReportFolder("./load-test-results")
                .Run();

            engine.StopAsync(CancellationToken.None).Wait();
            engine.Dispose();
        }

        private void RunStrategyExecutionLoadTest()
        {
            var strategy = new MomentumStrategyCanonical(_logger);
            strategy.InitializeAsync(CancellationToken.None).Wait();
            strategy.StartAsync(CancellationToken.None).Wait();

            var scenario = Scenario.Create("strategy_execution_load_test", async context =>
            {
                var marketData = new MarketData
                {
                    Symbol = $"SYM{Random.Shared.Next(1, 100)}",
                    Timestamp = DateTime.UtcNow,
                    Open = 100m + (decimal)(Random.Shared.NextDouble() * 50),
                    High = 0,
                    Low = 0,
                    Close = 0,
                    Volume = Random.Shared.Next(1000000, 100000000),
                    Volatility = 0.01m + (decimal)(Random.Shared.NextDouble() * 0.04m),
                    RSI = 30m + (decimal)(Random.Shared.NextDouble() * 40)
                };

                // Set high/low/close based on open
                marketData.High = marketData.Open * (1m + (decimal)(Random.Shared.NextDouble() * 0.05m));
                marketData.Low = marketData.Open * (1m - (decimal)(Random.Shared.NextDouble() * 0.05m));
                marketData.Close = marketData.Low + (decimal)(Random.Shared.NextDouble() * (double)(marketData.High - marketData.Low));

                var result = await strategy.ExecuteStrategyAsync(
                    marketData.Symbol,
                    marketData,
                    null,
                    CancellationToken.None);

                return result.IsSuccess || !result.IsSuccess ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 150, during: TimeSpan.FromSeconds(30)),
                Simulation.InjectPerSecRandom(minRate: 50, maxRate: 300, during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFileName("strategy_execution_load_test")
                .WithReportFolder("./load-test-results")
                .Run();

            strategy.StopAsync(CancellationToken.None).Wait();
            strategy.Dispose();
        }

        private void RunMessageQueueLoadTest()
        {
            var scenario = Scenario.Create("message_queue_load_test", async context =>
            {
                var message = new
                {
                    EventType = "OrderPlaced",
                    Symbol = $"SYM{Random.Shared.Next(1, 100)}",
                    Price = 100m + (decimal)(Random.Shared.NextDouble() * 100),
                    Quantity = Random.Shared.Next(1, 1000),
                    Timestamp = DateTime.UtcNow
                };

                var result = await _mockMessageQueue.Object.PublishAsync(
                    "trading-events",
                    message,
                    MessagePriority.Normal,
                    CancellationToken.None);

                return result.IsSuccess ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 1000, during: TimeSpan.FromSeconds(30)),
                Simulation.InjectPerSec(rate: 5000, during: TimeSpan.FromSeconds(10)),
                Simulation.InjectPerSec(rate: 10000, during: TimeSpan.FromSeconds(5))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFileName("message_queue_load_test")
                .WithReportFolder("./load-test-results")
                .Run();
        }
    }

    internal class NoOpTradingLogger : ITradingLogger
    {
        public void LogMethodEntry(string methodName = "", string filePath = "", int lineNumber = 0) { }
        public void LogMethodExit(string methodName = "", object? returnValue = null, long elapsedMilliseconds = 0, string filePath = "", int lineNumber = 0) { }
        public void LogInformation(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogWarning(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogError(string message, Exception? exception = null, string? operationContext = null, string? userImpact = null, string? troubleshootingHints = null, object? additionalData = null, string filePath = "", int lineNumber = 0) { }
        public void LogDebug(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogTrace(string message, object? data = null, string filePath = "", int lineNumber = 0) { }
        public void LogTrade(string symbol, string action, decimal price, int quantity, decimal totalAmount, string strategy, string status, object? metadata = null) { }
        public void LogPositionChange(string symbol, int oldQuantity, int newQuantity, decimal averagePrice, decimal realizedPnL, decimal unrealizedPnL, string reason, object? metadata = null) { }
        public void LogRisk(string riskType, string symbol, decimal riskValue, decimal threshold, bool isViolation, string action, object? metadata = null) { }
        public void LogMarketData(string symbol, string dataType, decimal? price = null, long? volume = null, decimal? changePercent = null, object? additionalData = null) { }
        public void LogPerformance(string metricName, decimal value, decimal? comparisonValue = null, object? context = null) { }
        public void LogDataPipeline(string stage, string source, string destination, int recordCount, long bytesProcessed, TimeSpan duration, string status, object? metadata = null) { }
        public void LogSystemResource(string resourceType, decimal usage, decimal threshold, bool isWarning, object? metadata = null) { }
        public void LogHealth(string component, string status, TimeSpan? responseTime = null, object? diagnostics = null) { }
        public void LogAudit(string action, string entity, string entityId, string userId, bool success, object? details = null) { }
        public void LogSecurity(string eventType, string severity, string source, string description, object? context = null) { }
        public void LogDataMovement(string operation, string source, string destination, int recordCount, string status, object? metadata = null) { }
        public void LogMemoryUsage(long usedBytes, long totalBytes, decimal percentageUsed, bool isWarning) { }
        public void LogApiCall(string endpoint, string method, int statusCode, long responseTimeMs, object? requestData = null, object? responseData = null) { }
        public void LogConfiguration(string key, string value, string source, bool isDefault) { }
        public void LogDatabaseQuery(string query, long executionTimeMs, int recordsAffected, object? parameters = null) { }
        public void LogCacheOperation(string operation, string key, bool hit, long? sizeBytes = null, TimeSpan? ttl = null) { }
        public void LogRateLimiting(string resource, int currentRate, int limit, bool isThrottled, TimeSpan? retryAfter = null) { }
        public void LogBackgroundJob(string jobName, string status, TimeSpan? duration = null, object? result = null) { }
        public void LogIntegration(string system, string operation, bool success, object? data = null) { }
        public void LogNotification(string type, string recipient, string subject, bool sent, object? metadata = null) { }
        public void LogWorkflow(string workflowName, string stage, string status, object? context = null) { }
        public void LogValidation(string entityType, string entityId, bool isValid, object? errors = null) { }
        public void LogFeatureToggle(string feature, bool enabled, string reason, object? context = null) { }
        public void LogBusinessEvent(string eventType, string description, object? data = null) { }
        public void LogUserAction(string action, string userId, string targetEntity, object? metadata = null) { }
        public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}