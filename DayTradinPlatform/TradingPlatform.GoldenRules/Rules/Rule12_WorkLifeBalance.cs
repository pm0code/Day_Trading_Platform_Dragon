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
    /// Rule 12: Maintain Work-Life Balance
    /// Trading is a marathon, not a sprint - ensure sustainable practices
    /// </summary>
    public class Rule12_WorkLifeBalance : IGoldenRuleEvaluator
    {
        public int RuleNumber => 12;
        public string RuleName => "Work-Life Balance";
        public RuleCategory Category => RuleCategory.Discipline;

        private readonly int _maxDailyScreenHours = 6; // Maximum 6 hours active trading
        private readonly int _maxTradesPerHour = 5; // Avoid excessive trading
        private readonly int _mandatoryBreakAfterHours = 2; // Break after 2 hours
        private readonly decimal _profitTargetForDay = 0.02m; // 2% daily target

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

            // Evaluate work-life balance factors
            var tradingIntensity = EvaluateTradingIntensity(positionContext);
            var sessionDuration = EstimateSessionDuration(positionContext);
            var needsBreak = CheckIfNeedsBreak(positionContext, sessionDuration);
            var hasMetDailyGoals = CheckDailyGoals(positionContext);
            var sustainabilityScore = CalculateSustainabilityScore(positionContext, sessionDuration);

            result.IsPassing = sustainabilityScore >= 0.6m && !needsBreak;
            result.ComplianceScore = sustainabilityScore;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                
                if (needsBreak)
                    reasons.Add("Break required - continuous trading detected");
                if (tradingIntensity > 0.8m)
                    reasons.Add("Trading intensity too high");
                if (sessionDuration.TotalHours > _maxDailyScreenHours)
                    reasons.Add($"Trading session too long ({sessionDuration.TotalHours:F1} hours)");
                if (sustainabilityScore < 0.6m)
                    reasons.Add("Unsustainable trading pattern");
                    
                result.Reason = string.Join("; ", reasons);
            }
            else
            {
                if (hasMetDailyGoals)
                {
                    result.Reason = "Daily goals met - consider stopping for the day";
                    result.Severity = RuleSeverity.Info;
                }
                else
                {
                    result.Reason = "Trading pattern is sustainable";
                }
            }

            result.Details["TradingIntensity"] = tradingIntensity;
            result.Details["SessionHours"] = sessionDuration.TotalHours;
            result.Details["NeedsBreak"] = needsBreak;
            result.Details["DailyGoalsMet"] = hasMetDailyGoals;
            result.Details["SustainabilityScore"] = sustainabilityScore;
            result.Details["TradesPerHour"] = CalculateTradesPerHour(positionContext, sessionDuration);
            result.Details["ScreenTimeTodayHours"] = sessionDuration.TotalHours;

            return result;
        }

        private decimal EvaluateTradingIntensity(PositionContext context)
        {
            // Base intensity on number of trades
            var tradeIntensity = Math.Min(1m, context.DayTradeCount / 20m);
            
            // Increase intensity if trading while losing
            if (context.DailyPnL < 0 && context.DayTradeCount > 10)
            {
                tradeIntensity = Math.Min(1m, tradeIntensity + 0.3m);
            }
            
            // Consider position size stress
            var positionStress = (context.Quantity * context.CurrentPrice) / context.AccountBalance;
            if (positionStress > 0.2m)
            {
                tradeIntensity = Math.Min(1m, tradeIntensity + 0.2m);
            }
            
            return tradeIntensity;
        }

        private TimeSpan EstimateSessionDuration(PositionContext context)
        {
            // Estimate based on trade count and typical trade frequency
            // Assume average 10 minutes between trades
            var estimatedMinutes = context.DayTradeCount * 10;
            
            // Minimum session time based on market hours
            var now = DateTime.UtcNow;
            var marketOpen = DateTime.Today.AddHours(13.5); // 9:30 AM ET in UTC
            
            if (now > marketOpen)
            {
                var actualDuration = now - marketOpen;
                return actualDuration > TimeSpan.FromMinutes(estimatedMinutes) 
                    ? actualDuration 
                    : TimeSpan.FromMinutes(estimatedMinutes);
            }
            
            return TimeSpan.FromMinutes(estimatedMinutes);
        }

        private bool CheckIfNeedsBreak(PositionContext context, TimeSpan sessionDuration)
        {
            // Need break after continuous trading
            if (sessionDuration.TotalHours >= _mandatoryBreakAfterHours)
            {
                // Check if trading intensity is high
                var tradesPerHour = context.DayTradeCount / Math.Max(1, sessionDuration.TotalHours);
                if (tradesPerHour > _maxTradesPerHour)
                    return true;
            }
            
            // Need break if stressed (losing and overtrading)
            if (context.DailyPnL < -context.AccountBalance * 0.01m && context.DayTradeCount > 15)
                return true;
            
            // Need break if session too long
            if (sessionDuration.TotalHours > _maxDailyScreenHours)
                return true;
                
            return false;
        }

        private bool CheckDailyGoals(PositionContext context)
        {
            // Check if profit target met
            var profitPercent = context.DailyPnL / context.AccountBalance;
            if (profitPercent >= _profitTargetForDay)
                return true;
                
            // Also consider if made good progress with reasonable trade count
            if (profitPercent > 0.01m && context.DayTradeCount <= 10)
                return true;
                
            return false;
        }

        private decimal CalculateSustainabilityScore(PositionContext context, TimeSpan sessionDuration)
        {
            var score = 1m;
            
            // Penalize long sessions
            if (sessionDuration.TotalHours > 4)
                score -= 0.1m * (decimal)(sessionDuration.TotalHours - 4);
                
            // Penalize excessive trading
            if (context.DayTradeCount > 20)
                score -= 0.02m * (context.DayTradeCount - 20);
                
            // Penalize trading while losing
            if (context.DailyPnL < 0 && context.DayTradeCount > 10)
                score -= 0.3m;
                
            // Reward meeting goals efficiently
            if (CheckDailyGoals(context) && context.DayTradeCount < 10)
                score += 0.2m;
                
            // Penalize high-stress trading
            var stressLevel = (context.Quantity * context.CurrentPrice) / context.AccountBalance;
            if (stressLevel > 0.25m)
                score -= 0.2m;
                
            return Math.Max(0, Math.Min(1, score));
        }

        private decimal CalculateTradesPerHour(PositionContext context, TimeSpan duration)
        {
            if (duration.TotalHours < 0.1)
                return 0;
                
            return context.DayTradeCount / (decimal)duration.TotalHours;
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();
            
            var sessionDuration = EstimateSessionDuration(positionContext);
            var hasMetGoals = CheckDailyGoals(positionContext);
            var profitPercent = positionContext.DailyPnL / positionContext.AccountBalance;

            // Session duration feedback
            recommendations.Add($"⏱️ Trading session: {sessionDuration.TotalHours:F1} hours");
            recommendations.Add($"📊 Trades today: {positionContext.DayTradeCount}");
            recommendations.Add($"💰 Daily P&L: {positionContext.DailyPnL:C2} ({profitPercent:P2})");

            // Goal-based recommendations
            if (hasMetGoals)
            {
                recommendations.Add("🎯 Daily goal achieved! Consider stopping");
                recommendations.Add("✅ Preserve your gains - don't give them back");
            }
            else if (profitPercent > 0.01m)
            {
                recommendations.Add("📈 Good progress - stay disciplined");
            }

            // Break recommendations
            if (sessionDuration.TotalHours >= _mandatoryBreakAfterHours)
            {
                recommendations.Add($"⚠️ You've been trading for {sessionDuration.TotalHours:F1} hours");
                recommendations.Add("☕ Take a 15-minute break");
            }

            // Intensity warnings
            if (positionContext.DayTradeCount > 20)
            {
                recommendations.Add("🚨 Overtrading detected - quality over quantity");
            }

            // Balance tips
            recommendations.Add("✅ Set daily profit targets and stick to them");
            recommendations.Add("✅ Schedule regular breaks every 2 hours");
            recommendations.Add("✅ Have a life outside trading");
            recommendations.Add("✅ Exercise and maintain physical health");
            
            // End of day recommendation
            if (sessionDuration.TotalHours > 5)
            {
                recommendations.Add("🏁 Consider wrapping up for the day");
                recommendations.Add("📝 Review trades and prepare for tomorrow");
            }

            return recommendations;
        }
    }
}