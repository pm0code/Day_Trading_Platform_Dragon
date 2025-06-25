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
    /// Rule 3: Cut Losses Quickly
    /// Always use stop-losses and exit losing trades immediately when predetermined exit criteria are met
    /// </summary>
    public class Rule03_CutLossesQuickly : IGoldenRuleEvaluator
    {
        public int RuleNumber => 3;
        public string RuleName => "Cut Losses Quickly";
        public RuleCategory Category => RuleCategory.RiskManagement;

        private readonly decimal _maxLossPercentage = 0.02m; // 2% max loss per position
        private readonly decimal _timeStopMinutes = 30; // Exit if no profit after 30 minutes

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

            // For new positions, ensure stop loss is planned
            if (positionContext.Quantity == 0)
            {
                var stopLossDistance = marketConditions.ATR * 1.5m; // 1.5x ATR stop
                var stopLossPercentage = stopLossDistance / price;

                result.IsPassing = stopLossPercentage <= _maxLossPercentage;
                result.ComplianceScore = result.IsPassing ? 1m : Math.Max(0, 1m - (decimal)(stopLossPercentage / _maxLossPercentage - 1));

                result.Details["PlannedStopLoss"] = price - (side == OrderSide.Buy ? stopLossDistance : -stopLossDistance);
                result.Details["StopLossPercentage"] = stopLossPercentage;
                result.Details["MaxAllowedLoss"] = _maxLossPercentage;

                result.Reason = result.IsPassing 
                    ? $"Stop loss {stopLossPercentage:P2} is within acceptable range"
                    : $"Stop loss {stopLossPercentage:P2} exceeds maximum {_maxLossPercentage:P2}";
            }
            // For existing positions, check if stop loss should be triggered
            else
            {
                var currentLoss = positionContext.UnrealizedPnL / (positionContext.Quantity * positionContext.EntryPrice);
                var shouldExit = false;
                var exitReasons = new List<string>();

                // Check percentage loss
                if (currentLoss < -_maxLossPercentage)
                {
                    shouldExit = true;
                    exitReasons.Add($"Loss {currentLoss:P2} exceeds maximum");
                }

                // Check time stop
                if (positionContext.HoldingPeriod.TotalMinutes > _timeStopMinutes && positionContext.UnrealizedPnL <= 0)
                {
                    shouldExit = true;
                    exitReasons.Add($"Position held {positionContext.HoldingPeriod.TotalMinutes:F0} minutes without profit");
                }

                // Check momentum loss
                if (side == OrderSide.Buy && marketConditions.Trend <= TrendDirection.Downtrend)
                {
                    shouldExit = true;
                    exitReasons.Add("Trend has reversed against position");
                }

                result.IsPassing = !shouldExit;
                result.ComplianceScore = shouldExit ? 0 : 1;
                result.Details["CurrentLoss"] = currentLoss;
                result.Details["HoldingPeriod"] = positionContext.HoldingPeriod.TotalMinutes;
                result.Details["ShouldExit"] = shouldExit;

                result.Reason = shouldExit 
                    ? $"Exit required: {string.Join("; ", exitReasons)}"
                    : "Position within acceptable loss parameters";
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

            if (positionContext.Quantity > 0)
            {
                var currentLoss = positionContext.UnrealizedPnL / (positionContext.Quantity * positionContext.EntryPrice);
                
                if (currentLoss < -0.01m)
                {
                    recommendations.Add($"⚠️ Position down {currentLoss:P2}. Consider tightening stop loss");
                }

                if (positionContext.HoldingPeriod.TotalMinutes > 15 && positionContext.UnrealizedPnL <= 0)
                {
                    recommendations.Add("⏱️ Position not working after 15 minutes. Prepare to exit");
                }
            }

            recommendations.Add($"✅ Always set stop loss at entry, maximum {_maxLossPercentage:P0} risk");
            recommendations.Add($"✅ Use {marketConditions.ATR * 1.5m:C2} as stop distance (1.5x ATR)");
            recommendations.Add("✅ Never move stop loss further away from entry");
            recommendations.Add("✅ Honor your stops without hesitation");

            return recommendations;
        }
    }
}