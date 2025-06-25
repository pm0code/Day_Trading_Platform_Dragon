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
    /// Rule 10: Master Trading Psychology
    /// Control emotions, avoid FOMO/fear, and maintain mental discipline
    /// </summary>
    public class Rule10_MasterPsychology : IGoldenRuleEvaluator
    {
        public int RuleNumber => 10;
        public string RuleName => "Master Trading Psychology";
        public RuleCategory Category => RuleCategory.Psychology;

        private readonly decimal _stressThreshold = 0.015m; // 1.5% daily loss creates stress
        private readonly int _maxStressTrades = 3; // Max trades when stressed
        private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(30); // Cool-down after big loss

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

            // Evaluate psychological state
            var emotionalState = EvaluateEmotionalState(positionContext);
            var stressLevel = CalculateStressLevel(positionContext);
            var fearGreedIndex = CalculateFearGreedIndex(positionContext, marketConditions);
            var isImpulsive = DetectImpulsiveBehavior(positionContext, marketConditions);
            var isFOMO = DetectFOMOPattern(positionContext, marketConditions);

            // Calculate psychological fitness score
            var psychScore = 1m;
            psychScore -= stressLevel * 0.4m;
            psychScore -= Math.Abs(fearGreedIndex - 0.5m) * 0.3m; // Penalty for extreme fear or greed
            if (isImpulsive) psychScore -= 0.2m;
            if (isFOMO) psychScore -= 0.3m;
            psychScore = Math.Max(0, psychScore);

            result.IsPassing = psychScore >= 0.6m && emotionalState != EmotionalState.Tilted;
            result.ComplianceScore = psychScore;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                
                if (emotionalState == EmotionalState.Tilted)
                    reasons.Add("Emotional state compromised - on tilt");
                if (stressLevel > 0.5m)
                    reasons.Add($"High stress level ({stressLevel:P0})");
                if (isImpulsive)
                    reasons.Add("Impulsive trading behavior detected");
                if (isFOMO)
                    reasons.Add("FOMO pattern detected");
                    
                result.Reason = string.Join("; ", reasons);
            }
            else
            {
                result.Reason = "Good psychological state for trading";
            }

            result.Details["EmotionalState"] = emotionalState.ToString();
            result.Details["StressLevel"] = stressLevel;
            result.Details["FearGreedIndex"] = fearGreedIndex;
            result.Details["IsImpulsive"] = isImpulsive;
            result.Details["IsFOMO"] = isFOMO;
            result.Details["PsychologicalScore"] = psychScore;
            result.Details["RequiresCooldown"] = ShouldTakeCooldown(positionContext);

            return result;
        }

        private enum EmotionalState
        {
            Calm,
            Anxious,
            Overconfident,
            Fearful,
            Tilted
        }

        private EmotionalState EvaluateEmotionalState(PositionContext context)
        {
            var lossPercent = Math.Abs(context.DailyPnL) / context.AccountBalance;

            // Tilted: Large loss and still trading aggressively
            if (lossPercent > _stressThreshold && context.DayTradeCount > 10)
                return EmotionalState.Tilted;

            // Overconfident: Big wins and increasing position sizes
            if (context.DailyPnL > context.AccountBalance * 0.02m && 
                context.Quantity * context.CurrentPrice > context.AccountBalance * 0.3m)
                return EmotionalState.Overconfident;

            // Fearful: Small positions after losses
            if (lossPercent > 0.01m && 
                context.Quantity * context.CurrentPrice < context.AccountBalance * 0.05m)
                return EmotionalState.Fearful;

            // Anxious: Rapid trading
            if (context.DayTradeCount > 15)
                return EmotionalState.Anxious;

            return EmotionalState.Calm;
        }

        private decimal CalculateStressLevel(PositionContext context)
        {
            var stress = 0m;

            // Loss-induced stress
            var lossPercent = Math.Abs(Math.Min(0, context.DailyPnL)) / context.AccountBalance;
            stress += lossPercent * 10; // Scale up for visibility

            // Overtrading stress
            if (context.DayTradeCount > 10)
                stress += 0.1m * (context.DayTradeCount - 10);

            // Position size stress
            var positionPercent = (context.Quantity * context.CurrentPrice) / context.AccountBalance;
            if (positionPercent > 0.2m)
                stress += (positionPercent - 0.2m) * 2;

            // Time pressure stress (if holding losing position)
            if (context.UnrealizedPnL < 0 && context.HoldingPeriod > TimeSpan.FromHours(1))
                stress += 0.2m;

            return Math.Min(stress, 1m);
        }

        private decimal CalculateFearGreedIndex(PositionContext context, MarketConditions conditions)
        {
            // 0 = Extreme Fear, 0.5 = Neutral, 1 = Extreme Greed
            var index = 0.5m;

            // Account performance factor
            if (context.DailyPnL > 0)
                index += Math.Min(0.3m, (context.DailyPnL / context.AccountBalance) * 10);
            else
                index -= Math.Min(0.3m, Math.Abs(context.DailyPnL / context.AccountBalance) * 10);

            // Market momentum factor
            index += conditions.Momentum * 0.2m - 0.1m;

            // Volatility factor (high volatility = more fear)
            if (conditions.Volatility > 0.02m)
                index -= 0.1m;

            // Position sizing factor
            var positionPercent = (context.Quantity * context.CurrentPrice) / context.AccountBalance;
            if (positionPercent > 0.15m)
                index += 0.1m; // Greed
            else if (positionPercent < 0.05m && context.DayTradeCount > 0)
                index -= 0.1m; // Fear

            return Math.Max(0, Math.Min(1, index));
        }

        private bool DetectImpulsiveBehavior(PositionContext context, MarketConditions conditions)
        {
            // Rapid entry after loss
            if (context.RealizedPnL < 0 && context.HoldingPeriod < TimeSpan.FromMinutes(2))
                return true;

            // Trading without setup in volatile market
            if (conditions.Volatility > 0.03m && context.DayTradeCount > 10)
                return true;

            // Increasing size dramatically
            if (context.Quantity * context.CurrentPrice > context.AccountBalance * 0.3m &&
                context.DailyPnL < 0)
                return true;

            return false;
        }

        private bool DetectFOMOPattern(PositionContext context, MarketConditions conditions)
        {
            // Chasing extended moves
            if (conditions.Momentum > 0.8m && 
                Math.Abs((conditions.Price - conditions.OpenPrice) / conditions.OpenPrice) > 0.03m)
                return true;

            // Trading after missing initial move
            if (context.DayTradeCount == 0 && // First trade of day
                conditions.Session == MarketSession.RegularHours && // Not market open
                Math.Abs((conditions.Price - conditions.OpenPrice) / conditions.OpenPrice) > 0.02m)
                return true;

            // Overtrading in hot market
            if (conditions.RelativeVolume > 3m && context.DayTradeCount > 5)
                return true;

            return false;
        }

        private bool ShouldTakeCooldown(PositionContext context)
        {
            var lossPercent = Math.Abs(Math.Min(0, context.DailyPnL)) / context.AccountBalance;
            return lossPercent > _stressThreshold || 
                   context.DayTradeCount > 20 ||
                   (context.UnrealizedPnL < -context.AccountBalance * 0.01m);
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            var emotionalState = EvaluateEmotionalState(positionContext);
            var stressLevel = CalculateStressLevel(positionContext);

            // Emotional state guidance
            switch (emotionalState)
            {
                case EmotionalState.Tilted:
                    recommendations.Add("🚨 STOP TRADING - You are on tilt!");
                    recommendations.Add("🧘 Take a 30-minute break minimum");
                    recommendations.Add("📝 Journal about what triggered the tilt");
                    break;
                case EmotionalState.Overconfident:
                    recommendations.Add("⚠️ Overconfidence detected - stay humble");
                    recommendations.Add("📉 Reduce position sizes back to normal");
                    break;
                case EmotionalState.Fearful:
                    recommendations.Add("😰 Fear detected - trust your system");
                    recommendations.Add("📊 Review your trading plan");
                    break;
                case EmotionalState.Anxious:
                    recommendations.Add("😟 Anxiety detected - slow down");
                    recommendations.Add("🎯 Focus on quality over quantity");
                    break;
                case EmotionalState.Calm:
                    recommendations.Add("😌 Good emotional state - stay focused");
                    break;
            }

            if (stressLevel > 0.5m)
            {
                recommendations.Add($"⚠️ High stress level: {stressLevel:P0}");
                recommendations.Add("🧘 Practice breathing exercises");
                recommendations.Add("🚶 Consider a short walk");
            }

            // General psychology tips
            recommendations.Add("✅ Trade the plan, not emotions");
            recommendations.Add("✅ Accept losses as cost of business");
            recommendations.Add("✅ Focus on process, not P&L");
            recommendations.Add("✅ One trade at a time");

            if (ShouldTakeCooldown(positionContext))
            {
                recommendations.Add("🛑 MANDATORY COOLDOWN: Step away for 30 minutes");
            }

            return recommendations;
        }
    }
}