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
    /// Rule 4: Let Winners Run
    /// Allow profitable positions to maximize gains by not exiting prematurely
    /// </summary>
    public class Rule04_LetWinnersRun : IGoldenRuleEvaluator
    {
        public int RuleNumber => 4;
        public string RuleName => "Let Winners Run";
        public RuleCategory Category => RuleCategory.ProfitManagement;

        private readonly decimal _minProfitToRiskRatio = 2m; // Minimum 2:1 reward/risk
        private readonly decimal _trailingStopPercentage = 0.005m; // 0.5% trailing stop
        private readonly decimal _quickExitThreshold = 0.002m; // Don't exit if profit < 0.2%

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
                Severity = RuleSeverity.Warning
            };

            // For new positions, ensure profit target is appropriate
            if (positionContext.Quantity == 0)
            {
                var stopLossDistance = marketConditions.ATR * 1.5m;
                var minProfitTarget = stopLossDistance * _minProfitToRiskRatio;
                var profitTargetPercentage = minProfitTarget / price;

                result.IsPassing = true;
                result.ComplianceScore = 1m;
                result.Reason = $"Ensure profit target of at least {profitTargetPercentage:P2} (2:1 R/R)";

                result.Details["MinProfitTarget"] = price + (side == OrderSide.Buy ? minProfitTarget : -minProfitTarget);
                result.Details["RiskRewardRatio"] = _minProfitToRiskRatio;
            }
            // For existing winning positions
            else if (positionContext.UnrealizedPnL > 0)
            {
                var profitPercentage = positionContext.UnrealizedPnL / (positionContext.Quantity * positionContext.EntryPrice);
                var shouldKeepRunning = true;
                var reasons = new List<string>();

                // Check if profit is too small to exit
                if (profitPercentage < _quickExitThreshold)
                {
                    shouldKeepRunning = true;
                    reasons.Add($"Profit {profitPercentage:P2} too small to exit");
                }
                // Check if trend is still favorable
                else if ((side == OrderSide.Buy && marketConditions.Trend >= TrendDirection.Uptrend) ||
                         (side == OrderSide.Sell && marketConditions.Trend <= TrendDirection.Downtrend))
                {
                    shouldKeepRunning = true;
                    reasons.Add("Trend still favorable - let winner run");
                }
                // Check momentum
                else if (marketConditions.Momentum > 0.6m)
                {
                    shouldKeepRunning = true;
                    reasons.Add("Strong momentum - continue holding");
                }

                // Calculate trailing stop
                var trailingStopPrice = side == OrderSide.Buy
                    ? positionContext.CurrentPrice * (1 - _trailingStopPercentage)
                    : positionContext.CurrentPrice * (1 + _trailingStopPercentage);

                result.IsPassing = shouldKeepRunning;
                result.ComplianceScore = shouldKeepRunning ? 1m : 0.7m;
                result.Reason = shouldKeepRunning 
                    ? string.Join("; ", reasons)
                    : "Consider taking profits - conditions deteriorating";

                result.Details["ProfitPercentage"] = profitPercentage;
                result.Details["TrailingStopPrice"] = trailingStopPrice;
                result.Details["CurrentTrend"] = marketConditions.Trend.ToString();
                result.Details["Momentum"] = marketConditions.Momentum;
            }
            // For losing positions
            else
            {
                result.IsPassing = true;
                result.ComplianceScore = 1m;
                result.Reason = "Rule applies to winning positions only";
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

            if (positionContext.UnrealizedPnL > 0)
            {
                var profitPercentage = positionContext.UnrealizedPnL / (positionContext.Quantity * positionContext.EntryPrice);
                
                recommendations.Add($"💰 Current profit: {profitPercentage:P2} ({positionContext.UnrealizedPnL:C2})");
                
                if (profitPercentage > 0.02m) // 2% profit
                {
                    recommendations.Add("✅ Consider moving stop to breakeven to protect profits");
                }
                
                if (profitPercentage > 0.05m) // 5% profit
                {
                    recommendations.Add("🎯 Exceptional gain! Use trailing stop to lock in profits");
                }

                if (marketConditions.Momentum > 0.8m)
                {
                    recommendations.Add("🚀 Strong momentum - let position run with trailing stop");
                }
            }

            recommendations.Add($"✅ Target minimum {_minProfitToRiskRatio}:1 reward-to-risk ratio");
            recommendations.Add("✅ Use trailing stops to protect profits while allowing upside");
            recommendations.Add("✅ Don't exit winners prematurely - ride the trend");
            recommendations.Add("✅ Scale out partially to lock in gains if needed");

            return recommendations;
        }
    }
}