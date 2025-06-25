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
    /// Rule 7: Proper Position Sizing
    /// Size positions based on account equity and risk tolerance to ensure long-term survival
    /// </summary>
    public class Rule07_ProperPositionSizing : IGoldenRuleEvaluator
    {
        public int RuleNumber => 7;
        public string RuleName => "Proper Position Sizing";
        public RuleCategory Category => RuleCategory.RiskManagement;

        private readonly decimal _maxPositionSizePercent = 0.25m; // Max 25% of account in one position
        private readonly decimal _maxRiskPerTrade = 0.01m; // Max 1% risk per trade
        private readonly decimal _reducedSizeThreshold = 0.95m; // Reduce size if account < 95% of starting
        private readonly decimal _confidenceMultiplier = 1.5m; // Can increase size for high confidence

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

            // Calculate position value and risk
            var positionValue = quantity * price;
            var positionSizePercent = positionValue / positionContext.AccountBalance;
            
            // Calculate risk based on ATR stop
            var stopDistance = marketConditions.ATR * 1.5m;
            var riskPerShare = stopDistance;
            var totalRisk = quantity * riskPerShare;
            var riskPercent = totalRisk / positionContext.AccountBalance;

            // Calculate optimal position size
            var optimalShares = CalculateOptimalPositionSize(
                positionContext.AccountBalance,
                price,
                stopDistance,
                marketConditions);

            // Evaluate position size
            var isSizeAppropriate = positionSizePercent <= _maxPositionSizePercent;
            var isRiskAppropriate = riskPercent <= _maxRiskPerTrade;

            result.IsPassing = isSizeAppropriate && isRiskAppropriate;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                if (!isSizeAppropriate)
                    reasons.Add($"Position size {positionSizePercent:P1} exceeds max {_maxPositionSizePercent:P0}");
                if (!isRiskAppropriate)
                    reasons.Add($"Risk {riskPercent:P1} exceeds max {_maxRiskPerTrade:P0}");
                
                result.Reason = string.Join("; ", reasons);
                result.ComplianceScore = Math.Min(
                    _maxPositionSizePercent / positionSizePercent,
                    _maxRiskPerTrade / riskPercent);
            }
            else
            {
                result.ComplianceScore = 1m - (decimal)(positionSizePercent / _maxPositionSizePercent * 0.5 + 
                                                       riskPercent / _maxRiskPerTrade * 0.5);
                result.Reason = "Position size and risk within acceptable limits";
            }

            // Add Kelly Criterion calculation
            var kellyPercent = CalculateKellyCriterion(positionContext);

            result.Details["PositionValue"] = positionValue;
            result.Details["PositionSizePercent"] = positionSizePercent;
            result.Details["RiskAmount"] = totalRisk;
            result.Details["RiskPercent"] = riskPercent;
            result.Details["OptimalShares"] = optimalShares;
            result.Details["KellyPercent"] = kellyPercent;
            result.Details["AccountBalance"] = positionContext.AccountBalance;
            result.Details["CurrentDrawdown"] = CalculateDrawdown(positionContext);

            return result;
        }

        private decimal CalculateOptimalPositionSize(
            decimal accountBalance,
            decimal price,
            decimal stopDistance,
            MarketConditions conditions)
        {
            // Base calculation: 1% risk
            var riskAmount = accountBalance * _maxRiskPerTrade;
            var baseShares = Math.Floor(riskAmount / stopDistance);

            // Adjust for market conditions
            decimal adjustment = 1m;

            // Reduce size in volatile markets
            if (conditions.Volatility > 0.03m) // 3% volatility
                adjustment *= 0.75m;

            // Reduce size if account is in drawdown
            var drawdownPercent = 1m - (accountBalance / 100000m); // Assuming 100k starting balance
            if (drawdownPercent > 0.05m) // 5% drawdown
                adjustment *= 0.8m;

            // Reduce size in choppy markets
            if (conditions.Trend == TrendDirection.Sideways)
                adjustment *= 0.8m;

            // Increase size for high-probability setups (would check setup score)
            if (conditions.Momentum > 0.8m && 
                (conditions.Trend == TrendDirection.StrongUptrend || 
                 conditions.Trend == TrendDirection.StrongDowntrend))
                adjustment *= 1.2m;

            return Math.Floor(baseShares * adjustment);
        }

        private decimal CalculateKellyCriterion(PositionContext context)
        {
            // Kelly % = (Win Probability * Avg Win / Avg Loss) - Loss Probability
            // Using simplified assumptions based on account performance
            
            var totalTrades = context.DayTradeCount > 0 ? context.DayTradeCount : 1;
            var winRate = 0.5m; // Assume 50% win rate if no data
            
            if (context.RealizedPnL > 0)
                winRate = 0.6m; // Positive P&L suggests higher win rate
            else if (context.RealizedPnL < 0)
                winRate = 0.4m; // Negative P&L suggests lower win rate

            var avgWinLossRatio = 1.5m; // Assume 1.5:1 reward/risk
            
            var kellyPercent = (winRate * avgWinLossRatio) - (1 - winRate);
            
            // Apply Kelly fraction (typically 25% of full Kelly)
            kellyPercent *= 0.25m;
            
            // Cap at maximum position size
            return Math.Min(Math.Max(kellyPercent, 0), _maxPositionSizePercent);
        }

        private decimal CalculateDrawdown(PositionContext context)
        {
            var startingBalance = 100000m; // Assumed starting balance
            var currentBalance = context.AccountBalance;
            
            if (currentBalance >= startingBalance)
                return 0;
                
            return (startingBalance - currentBalance) / startingBalance;
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            var drawdown = CalculateDrawdown(positionContext);
            var optimalSize = CalculateOptimalPositionSize(
                positionContext.AccountBalance,
                marketConditions.Price,
                marketConditions.ATR * 1.5m,
                marketConditions);

            recommendations.Add($"💰 Account Balance: {positionContext.AccountBalance:C0}");
            recommendations.Add($"📊 Optimal position size: {optimalSize} shares");

            if (drawdown > 0.05m)
            {
                recommendations.Add($"⚠️ Account in {drawdown:P0} drawdown - reduce position sizes");
            }

            if (marketConditions.Volatility > 0.03m)
            {
                recommendations.Add("⚠️ High volatility - reduce position size by 25%");
            }

            if (positionContext.DayTradeCount > 5)
            {
                recommendations.Add("⚠️ Multiple trades today - preserve capital");
            }

            var maxPositionValue = positionContext.AccountBalance * _maxPositionSizePercent;
            var maxRiskAmount = positionContext.AccountBalance * _maxRiskPerTrade;

            recommendations.Add($"✅ Maximum position size: {maxPositionValue:C0} ({_maxPositionSizePercent:P0})");
            recommendations.Add($"✅ Maximum risk per trade: {maxRiskAmount:C0} ({_maxRiskPerTrade:P0})");
            recommendations.Add("✅ Size down in drawdowns, size up when winning");
            recommendations.Add("✅ Never risk more than you can emotionally handle");

            return recommendations;
        }
    }
}