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
    /// Rule 1: Capital Preservation Above All
    /// Never risk more than 1% of your trading capital on any single trade
    /// </summary>
    public class Rule01_CapitalPreservation : IGoldenRuleEvaluator
    {
        public int RuleNumber => 1;
        public string RuleName => "Capital Preservation Above All";
        public RuleCategory Category => RuleCategory.RiskManagement;

        private readonly decimal _defaultMaxRiskPercentage = 0.01m; // 1%
        private readonly decimal _beginnerMaxRiskPercentage = 0.001m; // 0.1% for beginners

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
            await Task.Yield(); // Async for consistency

            var result = new RuleEvaluationResult
            {
                RuleNumber = RuleNumber,
                RuleName = RuleName,
                Severity = RuleSeverity.Blocking
            };

            // Calculate position value
            var positionValue = quantity * price;
            var accountBalance = positionContext.AccountBalance;

            if (accountBalance <= 0)
            {
                result.IsPassing = false;
                result.ComplianceScore = 0;
                result.Reason = "Invalid account balance";
                return result;
            }

            // Calculate risk amount (using ATR for stop loss estimation)
            var estimatedStopLoss = marketConditions.ATR * 2; // 2x ATR stop loss
            var riskAmount = quantity * estimatedStopLoss;
            var riskPercentage = riskAmount / accountBalance;

            // Determine max risk based on experience (simplified - would check actual trader profile)
            var maxRiskPercentage = positionContext.DayTradeCount < 100 
                ? _beginnerMaxRiskPercentage 
                : _defaultMaxRiskPercentage;

            result.IsPassing = riskPercentage <= maxRiskPercentage;
            result.ComplianceScore = result.IsPassing ? 1m : Math.Max(0, 1m - (decimal)(riskPercentage / maxRiskPercentage - 1));

            result.Details["PositionValue"] = positionValue;
            result.Details["RiskAmount"] = riskAmount;
            result.Details["RiskPercentage"] = riskPercentage;
            result.Details["MaxAllowedRisk"] = maxRiskPercentage;
            result.Details["AccountBalance"] = accountBalance;

            if (!result.IsPassing)
            {
                result.Reason = $"Risk {riskPercentage:P2} exceeds maximum allowed {maxRiskPercentage:P2}";
            }
            else
            {
                result.Reason = $"Risk {riskPercentage:P2} is within acceptable limits";
            }

            return result;
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            // Check current risk exposure
            var totalPositionValue = 0m;
            foreach (var position in positionContext.OpenPositions)
            {
                totalPositionValue += position.Value;
            }

            var exposureRatio = totalPositionValue / positionContext.AccountBalance;

            if (exposureRatio > 0.5m)
            {
                recommendations.Add("⚠️ High exposure: Consider reducing position sizes to preserve capital");
            }

            if (positionContext.DailyPnL < 0 && Math.Abs(positionContext.DailyPnL) > positionContext.AccountBalance * 0.02m)
            {
                recommendations.Add("🛑 Daily loss exceeds 2%: Stop trading and review your approach");
            }

            if (marketConditions.Volatility > 0.03m) // High volatility
            {
                recommendations.Add("📊 High volatility detected: Reduce position sizes by 50%");
            }

            if (positionContext.DayTradeCount < 100)
            {
                recommendations.Add("🎓 Beginner mode: Keep risk at 0.1% per trade until 100 successful trades");
            }

            recommendations.Add($"✅ Maximum position size: ${positionContext.AccountBalance * 0.01m / marketConditions.ATR:F2}");

            return recommendations;
        }
    }
}