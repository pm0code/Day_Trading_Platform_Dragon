using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;

namespace TradingPlatform.GoldenRules.Rules
{
    /// <summary>
    /// Rule 2: Maintain Trading Discipline
    /// Follow your trading system religiously and avoid deviating based on emotions or hunches
    /// </summary>
    public class Rule02_TradingDiscipline : IGoldenRuleEvaluator
    {
        public int RuleNumber => 2;
        public string RuleName => "Maintain Trading Discipline";
        public RuleCategory Category => RuleCategory.Discipline;

        private readonly Dictionary<string, TradingSystemCriteria> _systemCriteria = new();

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

            // Check if trade meets systematic criteria
            var meetsSystemCriteria = EvaluateSystemCriteria(symbol, side, marketConditions);
            
            // Check for emotional trading patterns
            var emotionalScore = EvaluateEmotionalDiscipline(positionContext);
            
            // Check for revenge trading
            var revengeTrading = DetectRevengeTradingPattern(positionContext);
            
            // Check for overtrading
            var overtrading = positionContext.DayTradeCount > 10; // More than 10 trades per day

            result.IsPassing = meetsSystemCriteria && emotionalScore > 0.7m && !revengeTrading && !overtrading;
            result.ComplianceScore = (meetsSystemCriteria ? 0.4m : 0) + (emotionalScore * 0.3m) + 
                                   (revengeTrading ? 0 : 0.2m) + (overtrading ? 0 : 0.1m);

            result.Details["MeetsSystemCriteria"] = meetsSystemCriteria;
            result.Details["EmotionalScore"] = emotionalScore;
            result.Details["RevengeTrading"] = revengeTrading;
            result.Details["Overtrading"] = overtrading;
            result.Details["DayTradeCount"] = positionContext.DayTradeCount;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                if (!meetsSystemCriteria) reasons.Add("Trade doesn't meet system criteria");
                if (emotionalScore <= 0.7m) reasons.Add("Low emotional discipline score");
                if (revengeTrading) reasons.Add("Revenge trading pattern detected");
                if (overtrading) reasons.Add("Overtrading detected");
                result.Reason = string.Join("; ", reasons);
            }
            else
            {
                result.Reason = "Trade follows systematic approach with good discipline";
            }

            return result;
        }

        private bool EvaluateSystemCriteria(string symbol, OrderSide side, MarketConditions conditions)
        {
            // Check basic system criteria (simplified - would use actual strategy rules)
            var criteria = new TradingSystemCriteria
            {
                RequiresTrendAlignment = true,
                RequiresVolumeConfirmation = true,
                RequiresTechnicalSetup = true
            };

            var trendAligned = (side == OrderSide.Buy && conditions.Trend >= TrendDirection.Uptrend) ||
                              (side == OrderSide.Sell && conditions.Trend <= TrendDirection.Downtrend);
            
            var volumeConfirmed = conditions.RelativeVolume > 1.2m; // 20% above average
            
            var technicalSetup = conditions.TechnicalIndicators.ContainsKey("RSI") && 
                                (side == OrderSide.Buy ? conditions.TechnicalIndicators["RSI"] < 70 : 
                                                        conditions.TechnicalIndicators["RSI"] > 30);

            return (!criteria.RequiresTrendAlignment || trendAligned) &&
                   (!criteria.RequiresVolumeConfirmation || volumeConfirmed) &&
                   (!criteria.RequiresTechnicalSetup || technicalSetup);
        }

        private decimal EvaluateEmotionalDiscipline(PositionContext context)
        {
            var score = 1m;

            // Penalize for trading after losses
            if (context.DailyPnL < 0)
            {
                var lossRatio = Math.Abs(context.DailyPnL) / context.AccountBalance;
                score -= lossRatio * 2; // Heavy penalty for trading after losses
            }

            // Penalize for rapid-fire trading
            if (context.DayTradeCount > 5)
            {
                score -= 0.1m * (context.DayTradeCount - 5);
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private bool DetectRevengeTradingPattern(PositionContext context)
        {
            // Revenge trading: Increasing position sizes after losses
            if (context.DailyPnL < 0 && context.Quantity > context.AccountBalance * 0.02m)
            {
                return true;
            }

            // Trading immediately after a loss
            if (context.RealizedPnL < 0 && context.HoldingPeriod < TimeSpan.FromMinutes(5))
            {
                return true;
            }

            return false;
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            if (positionContext.DayTradeCount > 10)
            {
                recommendations.Add("⚠️ Overtrading alert: You've made {positionContext.DayTradeCount} trades today. Take a break!");
            }

            if (positionContext.DailyPnL < 0)
            {
                recommendations.Add("🛑 Trading after losses detected. Step back and review your plan");
            }

            if (marketConditions.Trend == TrendDirection.Sideways)
            {
                recommendations.Add("📊 Choppy market conditions. Wait for clear trend before trading");
            }

            if (!marketConditions.TechnicalIndicators.Any())
            {
                recommendations.Add("📈 No technical confirmation. Wait for setup to develop");
            }

            recommendations.Add("✅ Follow your written trading plan for every trade");
            recommendations.Add("✅ Journal each trade with entry/exit reasons");

            return recommendations;
        }

        private class TradingSystemCriteria
        {
            public bool RequiresTrendAlignment { get; set; }
            public bool RequiresVolumeConfirmation { get; set; }
            public bool RequiresTechnicalSetup { get; set; }
        }
    }
}