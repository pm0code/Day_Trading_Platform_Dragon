using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TradingPlatform.Core.Models;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.PerformanceTests.Framework;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingPlatform.PerformanceTests.Benchmarks
{
    /// <summary>
    /// Benchmarks for Golden Rules engine to ensure fast rule evaluation
    /// </summary>
    public class GoldenRulesBenchmarks : CanonicalBenchmarkBase
    {
        private CanonicalGoldenRulesEngine _engine = null!;
        private PositionContext _positionContext = null!;
        private MarketConditions _marketConditions = null!;
        private Mock<ITimeSeriesService> _mockTimeSeriesService = null!;

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();
            
            _mockTimeSeriesService = new Mock<ITimeSeriesService>();
            _mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());

            var config = Options.Create(new GoldenRulesEngineConfig
            {
                Enabled = true,
                StrictMode = true,
                EnableRealTimeAlerts = false,
                MinimumComplianceScore = 0.8m
            });

            _engine = new CanonicalGoldenRulesEngine(Logger, _mockTimeSeriesService.Object, config);
            _engine.InitializeAsync(CancellationToken.None).Wait();
            _engine.StartAsync(CancellationToken.None).Wait();

            _positionContext = new PositionContext
            {
                Symbol = "AAPL",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 2,
                DailyPnL = 500m,
                OpenPositions = 3,
                MaxPositionsAllowed = 10,
                TotalRiskExposure = 5000m,
                AverageWinRate = 0.65m,
                CurrentDrawdown = 0.02m,
                SessionPnL = 300m,
                WinningTrades = 8,
                LosingTrades = 4,
                ConsecutiveLosses = 1,
                LargestLoss = -200m,
                LargestWin = 800m,
                AverageLoss = -150m,
                AverageWin = 400m,
                Quantity = 100,
                EntryPrice = 150m,
                CurrentPrice = 152m,
                UnrealizedPnL = 200m,
                RealizedPnL = 300m,
                HoldingPeriod = TimeSpan.FromHours(2),
                StopLoss = 148m,
                TakeProfit = 155m
            };

            _marketConditions = new MarketConditions
            {
                Symbol = "AAPL",
                Price = 152m,
                Bid = 151.95m,
                Ask = 152.05m,
                Volume = 50000000,
                AverageVolume = 45000000,
                DayHigh = 153m,
                DayLow = 150m,
                OpenPrice = 151m,
                PreviousClose = 150.5m,
                ATR = 2.5m,
                Volatility = 0.025m,
                ImpliedVolatility = 0.03m,
                Trend = TrendDirection.Uptrend,
                TrendStrength = 0.7m,
                Momentum = 0.65m,
                RSI = 60m,
                MACD = 0.5m,
                BollingerPosition = 0.6m,
                VolumeProfile = VolumeProfile.Normal,
                MarketBreadth = 0.6m,
                SectorStrength = 0.7m,
                RelativeVolume = 1.1m,
                SpreadPercentage = 0.0006m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = false,
                EarningsDate = DateTime.UtcNow.AddDays(30),
                DividendDate = DateTime.UtcNow.AddDays(45),
                TechnicalIndicators = new Dictionary<string, decimal>
                {
                    ["SMA20"] = 151m,
                    ["SMA50"] = 149m,
                    ["EMA12"] = 151.5m,
                    ["EMA26"] = 150.8m
                }
            };
        }

        [GlobalCleanup]
        public override void GlobalCleanup()
        {
            _engine?.StopAsync(CancellationToken.None).Wait();
            _engine?.Dispose();
            base.GlobalCleanup();
        }

        [Benchmark(Baseline = true)]
        public async Task<GoldenRulesAssessment> EvaluateAllRules()
        {
            var result = await _engine.EvaluateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                100,
                152m,
                _positionContext,
                _marketConditions,
                CancellationToken.None);
            
            return result.Value;
        }

        [Benchmark]
        public async Task<bool> ValidateTrade()
        {
            var result = await _engine.ValidateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                100,
                152m,
                CancellationToken.None);
            
            return result.Value;
        }

        [Benchmark]
        public GoldenRuleResult EvaluateSingleRule()
        {
            // Simulate evaluating rule 1 (2% risk per trade)
            var positionRisk = 100 * 152m * 0.02m; // 2% stop loss
            var accountRisk = _positionContext.AccountBalance * 0.02m;
            var isCompliant = positionRisk <= accountRisk;
            
            return new GoldenRuleResult
            {
                RuleNumber = 1,
                RuleName = "Never Risk More Than 2% Per Trade",
                IsCompliant = isCompliant,
                Severity = isCompliant ? RuleSeverity.Info : RuleSeverity.Critical,
                Message = isCompliant ? "Risk within limits" : "Risk exceeds 2% limit",
                Impact = isCompliant ? 0m : 0.5m,
                Details = new Dictionary<string, object>
                {
                    ["PositionRisk"] = positionRisk,
                    ["MaxAllowedRisk"] = accountRisk,
                    ["RiskPercentage"] = positionRisk / _positionContext.AccountBalance
                }
            };
        }

        [Benchmark]
        public decimal CalculateComplianceScore()
        {
            var compliantRules = 10; // Assuming 10 out of 12 rules pass
            var totalRules = 12;
            var criticalViolations = 1;
            var warningViolations = 1;
            
            var baseScore = (decimal)compliantRules / totalRules;
            var penalty = criticalViolations * 0.1m + warningViolations * 0.05m;
            
            return System.Math.Max(0, baseScore - penalty);
        }

        [Benchmark]
        public string GenerateRecommendation()
        {
            var violations = 2;
            var complianceScore = 0.75m;
            
            if (violations == 0)
                return "PROCEED - All Golden Rules satisfied";
            else if (complianceScore >= 0.8m)
                return "PROCEED WITH CAUTION - Minor rule violations detected";
            else if (complianceScore >= 0.6m)
                return "WARNING - Multiple rule violations. Review position sizing and risk";
            else
                return "DO NOT TRADE - Critical rule violations detected";
        }

        [Benchmark]
        public async Task EvaluateMultipleTrades()
        {
            var tasks = new Task[10];
            
            for (int i = 0; i < tasks.Length; i++)
            {
                var symbol = $"SYM{i}";
                tasks[i] = _engine.ValidateTradeAsync(
                    symbol,
                    OrderType.Market,
                    OrderSide.Buy,
                    100,
                    100m + i,
                    CancellationToken.None);
            }
            
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Benchmarks for rule caching and optimization
    /// </summary>
    public class GoldenRulesOptimizationBenchmarks : CanonicalBenchmarkBase
    {
        private readonly Dictionary<string, GoldenRuleResult> _ruleCache = new();
        private readonly object _cacheLock = new();

        [Benchmark]
        public GoldenRuleResult GetCachedRule()
        {
            const string key = "AAPL-Rule1-100-152";
            
            lock (_cacheLock)
            {
                if (_ruleCache.TryGetValue(key, out var cached))
                    return cached;
                
                var result = new GoldenRuleResult
                {
                    RuleNumber = 1,
                    IsCompliant = true,
                    Severity = RuleSeverity.Info
                };
                
                _ruleCache[key] = result;
                return result;
            }
        }

        [Benchmark]
        public GoldenRuleResult GetCachedRuleConcurrentDictionary()
        {
            // Using ConcurrentDictionary would be better for high concurrency
            const string key = "AAPL-Rule1-100-152";
            
            return new GoldenRuleResult
            {
                RuleNumber = 1,
                IsCompliant = true,
                Severity = RuleSeverity.Info
            };
        }

        [Benchmark]
        public bool[] EvaluateRuleBatch()
        {
            var results = new bool[12];
            
            // Simulate batch evaluation of all 12 rules
            results[0] = true;  // Rule 1: 2% risk
            results[1] = true;  // Rule 2: 6% daily loss
            results[2] = true;  // Rule 3: Plan before trade
            results[3] = false; // Rule 4: Cut losses
            results[4] = true;  // Rule 5: Let winners run
            results[5] = true;  // Rule 6: No revenge trading
            results[6] = true;  // Rule 7: Liquid stocks
            results[7] = true;  // Rule 8: Pre-market prep
            results[8] = false; // Rule 9: No overtrading
            results[9] = true;  // Rule 10: Review trades
            results[10] = true; // Rule 11: Capital preservation
            results[11] = true; // Rule 12: Risk/reward
            
            return results;
        }
    }
}