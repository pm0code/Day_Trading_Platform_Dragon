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
    /// Rule 9: Continuous Learning
    /// Review trades, learn from mistakes, and adapt strategies based on market conditions
    /// </summary>
    public class Rule09_ContinuousLearning : IGoldenRuleEvaluator
    {
        public int RuleNumber => 9;
        public string RuleName => "Continuous Learning";
        public RuleCategory Category => RuleCategory.Psychology;

        private readonly int _minTradesForPattern = 5; // Need 5 trades to identify pattern
        private readonly decimal _lossPatternThreshold = 0.6m; // 60% losses indicates problem
        private readonly int _recentTradesWindow = 20; // Analyze last 20 trades

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
                Severity = RuleSeverity.Warning // Learning issues are warnings, not blocks
            };

            // Analyze trading patterns and performance
            var learningScore = EvaluateLearningBehavior(positionContext);
            var adaptationScore = EvaluateMarketAdaptation(positionContext, marketConditions);
            var mistakeRepetition = DetectRepeatedMistakes(positionContext, symbol);

            // Calculate overall compliance
            var overallScore = (learningScore + adaptationScore) / 2m;
            
            if (mistakeRepetition)
            {
                overallScore *= 0.5m; // Heavy penalty for repeating mistakes
            }

            result.IsPassing = overallScore >= 0.6m;
            result.ComplianceScore = overallScore;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                
                if (learningScore < 0.6m)
                    reasons.Add("Not learning from recent trades");
                if (adaptationScore < 0.6m)
                    reasons.Add("Poor adaptation to market conditions");
                if (mistakeRepetition)
                    reasons.Add("Repeating same mistakes");
                    
                result.Reason = string.Join("; ", reasons);
            }
            else
            {
                result.Reason = "Demonstrating good learning and adaptation";
            }

            result.Details["LearningScore"] = learningScore;
            result.Details["AdaptationScore"] = adaptationScore;
            result.Details["RepeatingMistakes"] = mistakeRepetition;
            result.Details["TradesToday"] = positionContext.DayTradeCount;
            result.Details["DailyPnL"] = positionContext.DailyPnL;
            result.Details["RequiresReview"] = ShouldReviewTrades(positionContext);

            return result;
        }

        private decimal EvaluateLearningBehavior(PositionContext context)
        {
            var score = 1m;

            // Check if trader is adjusting after losses
            if (context.DailyPnL < 0)
            {
                // Good: Reducing activity after losses
                if (context.DayTradeCount < 5)
                    score = 0.9m;
                // Bad: Overtrading after losses
                else if (context.DayTradeCount > 10)
                    score = 0.3m;
                else
                    score = 0.6m;
            }

            // Check if maintaining discipline when winning
            if (context.DailyPnL > 0)
            {
                // Good: Not getting overconfident
                if (context.DayTradeCount <= 8)
                    score = Math.Min(score, 1m);
                // Bad: Overtrading when winning
                else
                    score *= 0.7m;
            }

            return score;
        }

        private decimal EvaluateMarketAdaptation(PositionContext context, MarketConditions conditions)
        {
            var score = 0.8m; // Start with good score

            // Check adaptation to market conditions
            switch (conditions.Trend)
            {
                case TrendDirection.Sideways:
                    // Should reduce trading in choppy markets
                    if (context.DayTradeCount > 5)
                        score = 0.5m;
                    break;
                    
                case TrendDirection.StrongUptrend:
                case TrendDirection.StrongDowntrend:
                    // Should be more active in trending markets
                    if (context.DayTradeCount < 3 && context.HoldingPeriod.TotalHours > 2)
                        score = 0.6m;
                    break;
            }

            // Check volatility adaptation
            if (conditions.Volatility > 0.03m) // High volatility
            {
                // Should be more cautious
                if (context.Quantity > context.AccountBalance * 0.02m)
                    score *= 0.7m;
            }

            return score;
        }

        private bool DetectRepeatedMistakes(PositionContext context, string symbol)
        {
            // Simple pattern detection for repeated mistakes
            
            // Mistake 1: Trading same losing symbol repeatedly
            if (context.RealizedPnL < 0 && context.UnrealizedPnL < 0)
            {
                // If losing on this symbol and still trading it
                return true;
            }

            // Mistake 2: Not respecting stop losses (position too large after losses)
            if (context.DailyPnL < -context.AccountBalance * 0.01m && 
                context.Quantity * context.CurrentPrice > context.AccountBalance * 0.2m)
            {
                return true;
            }

            // Mistake 3: Revenge trading pattern
            if (context.DayTradeCount > 10 && context.DailyPnL < 0)
            {
                return true;
            }

            return false;
        }

        private bool ShouldReviewTrades(PositionContext context)
        {
            // Recommend trade review in certain conditions
            return context.DailyPnL < -context.AccountBalance * 0.015m || // Lost > 1.5%
                   context.DayTradeCount > 15 || // Too many trades
                   (context.DayTradeCount > 5 && context.DailyPnL < 0); // Multiple losing trades
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            // Performance review recommendations
            if (positionContext.DailyPnL < 0)
            {
                recommendations.Add($"📉 Down {Math.Abs(positionContext.DailyPnL):C0} today");
                recommendations.Add("📝 Review losing trades and identify common patterns");
                
                if (positionContext.DayTradeCount > 10)
                {
                    recommendations.Add("⚠️ Overtrading detected - quality over quantity!");
                }
            }
            else if (positionContext.DailyPnL > 0)
            {
                recommendations.Add($"📈 Up {positionContext.DailyPnL:C0} today");
                recommendations.Add("✅ Document what worked well today");
            }

            // Market adaptation recommendations
            switch (marketConditions.Trend)
            {
                case TrendDirection.Sideways:
                    recommendations.Add("➡️ Choppy market - consider sitting out");
                    break;
                case TrendDirection.StrongUptrend:
                case TrendDirection.StrongDowntrend:
                    recommendations.Add("📊 Strong trend - stick with trend-following setups");
                    break;
            }

            // Learning activities
            recommendations.Add("✅ Maintain detailed trade journal with entry/exit reasons");
            recommendations.Add("✅ Review trades weekly to identify patterns");
            recommendations.Add("✅ Study one new trading concept each week");
            recommendations.Add("✅ Join trading communities for peer learning");

            if (ShouldReviewTrades(positionContext))
            {
                recommendations.Add("🚨 MANDATORY: Review all trades before continuing");
            }

            return recommendations;
        }
    }
}