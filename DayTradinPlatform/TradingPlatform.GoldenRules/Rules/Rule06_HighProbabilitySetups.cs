using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;

namespace TradingPlatform.GoldenRules.Rules
{
    /// <summary>
    /// Rule 6: Trade High-Probability Setups Only
    /// Focus on A+ quality trades that meet multiple confluence factors
    /// </summary>
    public class Rule06_HighProbabilitySetups : IGoldenRuleEvaluator
    {
        public int RuleNumber => 6;
        public string RuleName => "High-Probability Setups Only";
        public RuleCategory Category => RuleCategory.Strategy;

        private readonly int _minConfluenceFactors = 3; // Need at least 3 factors
        private readonly decimal _minSetupScore = 0.7m; // 70% minimum setup quality

        public async Task<RuleEvaluationResult> EvaluateAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var result = new RuleEvaluationResult
            {
                RuleNumber = RuleNumber,
                RuleName = RuleName,
                Severity = RuleSeverity.Critical
            };

            // Evaluate confluence factors
            var confluenceFactors = EvaluateConfluenceFactors(side, price, marketConditions);
            var setupScore = CalculateSetupScore(confluenceFactors);
            var activeFactors = confluenceFactors.Where(f => f.Value).Count();

            result.IsPassing = activeFactors >= _minConfluenceFactors && setupScore >= _minSetupScore;
            result.ComplianceScore = setupScore;

            if (!result.IsPassing)
            {
                if (activeFactors < _minConfluenceFactors)
                {
                    result.Reason = $"Insufficient confluence factors ({activeFactors}/{_minConfluenceFactors} required)";
                }
                else
                {
                    result.Reason = $"Setup quality too low ({setupScore:P0} < {_minSetupScore:P0} required)";
                }
            }
            else
            {
                result.Reason = $"High-probability setup with {activeFactors} confluence factors";
            }

            // Add details
            result.Details["ConfluenceFactors"] = activeFactors;
            result.Details["SetupScore"] = setupScore;
            result.Details["ActiveFactors"] = string.Join(", ", confluenceFactors.Where(f => f.Value).Select(f => f.Key));
            
            foreach (var factor in confluenceFactors)
            {
                result.Details[$"Factor_{factor.Key}"] = factor.Value;
            }

            return result;
        }

        private Dictionary<string, bool> EvaluateConfluenceFactors(
            OrderSide side, 
            decimal price, 
            MarketConditions conditions)
        {
            var factors = new Dictionary<string, bool>();

            // 1. Trend Alignment
            factors["TrendAlignment"] = 
                (side == OrderSide.Buy && (conditions.Trend == TrendDirection.Uptrend || conditions.Trend == TrendDirection.StrongUptrend)) ||
                (side == OrderSide.Sell && (conditions.Trend == TrendDirection.Downtrend || conditions.Trend == TrendDirection.StrongDowntrend));

            // 2. Volume Confirmation
            factors["VolumeConfirmation"] = conditions.RelativeVolume > 1.5m;

            // 3. Support/Resistance Level
            factors["KeyLevel"] = CheckKeyLevelProximity(price, conditions);

            // 4. Technical Indicator Confluence
            factors["TechnicalConfluence"] = CheckTechnicalIndicatorConfluence(side, conditions);

            // 5. Market Session
            factors["OptimalSession"] = conditions.Session == MarketSession.MarketOpen || 
                                      conditions.Session == MarketSession.RegularHours ||
                                      conditions.Session == MarketSession.PowerHour;

            // 6. Momentum
            factors["StrongMomentum"] = conditions.Momentum > 0.6m;

            // 7. Low Volatility (for breakouts)
            factors["VolatilitySetup"] = conditions.Volatility < 0.02m || conditions.Volatility > 0.04m;

            // 8. Gap Setup (if applicable)
            var gapPercentage = Math.Abs((conditions.OpenPrice - conditions.PreviousClose) / conditions.PreviousClose);
            factors["GapSetup"] = gapPercentage > 0.01m; // 1% gap

            // 9. News Catalyst
            factors["NewsCatalyst"] = conditions.HasNewsCatalyst;

            // 10. Risk/Reward Favorable
            var atr = conditions.ATR;
            var potentialReward = atr * 2;
            var potentialRisk = atr * 1;
            factors["FavorableRiskReward"] = potentialReward / potentialRisk >= 2;

            return factors;
        }

        private decimal CalculateSetupScore(Dictionary<string, bool> factors)
        {
            // Weight different factors based on importance
            var weights = new Dictionary<string, decimal>
            {
                ["TrendAlignment"] = 0.20m,
                ["VolumeConfirmation"] = 0.15m,
                ["KeyLevel"] = 0.15m,
                ["TechnicalConfluence"] = 0.15m,
                ["OptimalSession"] = 0.10m,
                ["StrongMomentum"] = 0.10m,
                ["VolatilitySetup"] = 0.05m,
                ["GapSetup"] = 0.05m,
                ["NewsCatalyst"] = 0.03m,
                ["FavorableRiskReward"] = 0.02m
            };

            decimal score = 0m;
            foreach (var factor in factors)
            {
                if (factor.Value && weights.ContainsKey(factor.Key))
                {
                    score += weights[factor.Key];
                }
            }

            return score;
        }

        private bool CheckKeyLevelProximity(decimal price, MarketConditions conditions)
        {
            // Check if price is near important levels
            var levels = new List<decimal>
            {
                conditions.DayHigh,
                conditions.DayLow,
                conditions.PreviousClose,
                conditions.OpenPrice
            };

            // Add pivot points if available
            if (conditions.TechnicalIndicators.ContainsKey("R1"))
                levels.Add(conditions.TechnicalIndicators["R1"]);
            if (conditions.TechnicalIndicators.ContainsKey("S1"))
                levels.Add(conditions.TechnicalIndicators["S1"]);
            if (conditions.TechnicalIndicators.ContainsKey("PP"))
                levels.Add(conditions.TechnicalIndicators["PP"]);

            // Check if price is within 0.5% of any key level
            return levels.Any(level => Math.Abs((price - level) / level) < 0.005m);
        }

        private bool CheckTechnicalIndicatorConfluence(OrderSide side, MarketConditions conditions)
        {
            var bullishSignals = 0;
            var bearishSignals = 0;

            // RSI
            if (conditions.TechnicalIndicators.ContainsKey("RSI"))
            {
                var rsi = conditions.TechnicalIndicators["RSI"];
                if (rsi < 30) bullishSignals++;
                else if (rsi > 70) bearishSignals++;
            }

            // MACD
            if (conditions.TechnicalIndicators.ContainsKey("MACD") && 
                conditions.TechnicalIndicators.ContainsKey("MACD_Signal"))
            {
                var macd = conditions.TechnicalIndicators["MACD"];
                var signal = conditions.TechnicalIndicators["MACD_Signal"];
                if (macd > signal) bullishSignals++;
                else bearishSignals++;
            }

            // Moving Averages
            if (conditions.TechnicalIndicators.ContainsKey("MA20") && 
                conditions.TechnicalIndicators.ContainsKey("MA50"))
            {
                var ma20 = conditions.TechnicalIndicators["MA20"];
                var ma50 = conditions.TechnicalIndicators["MA50"];
                if (conditions.Price > ma20 && ma20 > ma50) bullishSignals++;
                else if (conditions.Price < ma20 && ma20 < ma50) bearishSignals++;
            }

            return (side == OrderSide.Buy && bullishSignals >= 2) ||
                   (side == OrderSide.Sell && bearishSignals >= 2);
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            // Evaluate current market quality
            var confluenceFactors = EvaluateConfluenceFactors(OrderSide.Buy, marketConditions.Price, marketConditions);
            var activeFactors = confluenceFactors.Where(f => f.Value).Count();
            var setupScore = CalculateSetupScore(confluenceFactors);

            recommendations.Add($"📊 Current setup quality: {setupScore:P0} ({activeFactors} factors)");

            if (setupScore < _minSetupScore)
            {
                recommendations.Add($"⚠️ Setup quality below threshold ({_minSetupScore:P0})");
                recommendations.Add("🚫 Wait for better setup to develop");
            }
            else
            {
                recommendations.Add("✅ High-probability setup detected");
            }

            // List active factors
            var active = confluenceFactors.Where(f => f.Value).Select(f => f.Key).ToList();
            if (active.Any())
            {
                recommendations.Add($"✅ Active factors: {string.Join(", ", active)}");
            }

            // List missing factors
            var missing = confluenceFactors.Where(f => !f.Value).Select(f => f.Key).Take(3).ToList();
            if (missing.Any())
            {
                recommendations.Add($"❌ Missing factors: {string.Join(", ", missing)}");
            }

            recommendations.Add("✅ Only trade A+ setups with multiple confirmations");
            recommendations.Add("✅ Quality over quantity - wait for the best opportunities");
            recommendations.Add("✅ When in doubt, stay out");

            return recommendations;
        }
    }
}