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
    /// Rule 8: Control Daily Losses
    /// Set maximum daily loss limits to prevent catastrophic drawdowns
    /// </summary>
    public class Rule08_ControlDailyLosses : IGoldenRuleEvaluator
    {
        public int RuleNumber => 8;
        public string RuleName => "Control Daily Losses";
        public RuleCategory Category => RuleCategory.RiskManagement;

        private readonly decimal _maxDailyLossPercentage = 0.02m; // 2% max daily loss
        private readonly decimal _warningLossPercentage = 0.015m; // 1.5% warning level
        private readonly int _maxConsecutiveLosses = 3; // Stop after 3 consecutive losses

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
                Severity = RuleSeverity.Blocking
            };

            // Calculate current daily loss percentage
            var dailyLossPercentage = Math.Abs(positionContext.DailyPnL) / positionContext.AccountBalance;
            var isLoss = positionContext.DailyPnL < 0;

            // Check if already at max loss
            if (isLoss && dailyLossPercentage >= _maxDailyLossPercentage)
            {
                result.IsPassing = false;
                result.ComplianceScore = 0;
                result.Reason = $"Daily loss limit {_maxDailyLossPercentage:P0} reached. Trading halted for the day";
                result.Details["DailyLoss"] = positionContext.DailyPnL;
                result.Details["DailyLossPercentage"] = dailyLossPercentage;
                result.Details["TradingHalted"] = true;
                return result;
            }

            // Calculate potential loss from new trade
            var potentialLoss = quantity * marketConditions.ATR * 2; // Estimate 2x ATR loss
            var potentialTotalLoss = Math.Abs(positionContext.DailyPnL) + potentialLoss;
            var potentialLossPercentage = potentialTotalLoss / positionContext.AccountBalance;

            // Check if trade would exceed daily limit
            if (potentialLossPercentage > _maxDailyLossPercentage)
            {
                result.IsPassing = false;
                result.ComplianceScore = 0.3m; // Some credit for being under limit currently
                result.Reason = "Trade would potentially exceed daily loss limit";
                result.Details["PotentialLoss"] = potentialLoss;
                result.Details["PotentialTotalLossPercentage"] = potentialLossPercentage;
            }
            else if (isLoss && dailyLossPercentage >= _warningLossPercentage)
            {
                result.IsPassing = true; // Allow but warn
                result.ComplianceScore = 0.6m;
                result.Reason = $"Warning: Approaching daily loss limit ({dailyLossPercentage:P1} of {_maxDailyLossPercentage:P0})";
                result.Severity = RuleSeverity.Warning;
            }
            else
            {
                result.IsPassing = true;
                result.ComplianceScore = 1m - (decimal)(dailyLossPercentage / _maxDailyLossPercentage);
                result.Reason = "Daily loss within acceptable limits";
            }

            result.Details["CurrentDailyPnL"] = positionContext.DailyPnL;
            result.Details["DailyLossPercentage"] = dailyLossPercentage;
            result.Details["MaxDailyLossPercentage"] = _maxDailyLossPercentage;
            result.Details["RemainingLossCapacity"] = Math.Max(0, _maxDailyLossPercentage - dailyLossPercentage);

            return result;
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();
            var dailyLossPercentage = Math.Abs(positionContext.DailyPnL) / positionContext.AccountBalance;

            if (positionContext.DailyPnL < 0)
            {
                if (dailyLossPercentage >= _maxDailyLossPercentage)
                {
                    recommendations.Add("🛑 STOP TRADING: Daily loss limit reached. Resume tomorrow");
                    recommendations.Add("📝 Review today's trades and identify what went wrong");
                }
                else if (dailyLossPercentage >= _warningLossPercentage)
                {
                    recommendations.Add($"⚠️ WARNING: Down {dailyLossPercentage:P1} for the day");
                    recommendations.Add("🎯 Take only A+ setups for remainder of day");
                    recommendations.Add("📉 Reduce position sizes by 50%");
                }
                else if (dailyLossPercentage >= 0.01m)
                {
                    recommendations.Add("⚠️ Down 1% for the day. Trade cautiously");
                    recommendations.Add("✅ Focus on high-probability setups only");
                }
            }

            recommendations.Add($"✅ Daily loss limit: {_maxDailyLossPercentage:P0} ({positionContext.AccountBalance * _maxDailyLossPercentage:C0})");
            recommendations.Add($"✅ Current P&L: {positionContext.DailyPnL:C2} ({dailyLossPercentage:P2})");
            
            var remainingCapacity = Math.Max(0, (_maxDailyLossPercentage - dailyLossPercentage) * positionContext.AccountBalance);
            recommendations.Add($"✅ Remaining loss capacity: {remainingCapacity:C0}");

            return recommendations;
        }
    }
}