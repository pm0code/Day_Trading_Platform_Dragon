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
    /// Rule 11: Understand Market Structure
    /// Know market hours, sessions, and optimal trading times for your strategy
    /// </summary>
    public class Rule11_UnderstandMarketStructure : IGoldenRuleEvaluator
    {
        public int RuleNumber => 11;
        public string RuleName => "Understand Market Structure";
        public RuleCategory Category => RuleCategory.MarketAnalysis;

        private readonly Dictionary<MarketSession, decimal> _sessionQualityScores = new()
        {
            [MarketSession.PreMarket] = 0.6m,
            [MarketSession.MarketOpen] = 0.9m,
            [MarketSession.RegularHours] = 0.8m,
            [MarketSession.PowerHour] = 0.85m,
            [MarketSession.MarketClose] = 0.7m,
            [MarketSession.AfterHours] = 0.4m
        };

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

            // Evaluate market structure understanding
            var sessionScore = EvaluateSessionAppropriateness(marketConditions.Session, marketConditions);
            var liquidityScore = EvaluateLiquidityConditions(marketConditions);
            var structureScore = EvaluateMarketStructure(marketConditions);
            var timingScore = EvaluateEntryTiming(marketConditions, positionContext);

            // Calculate overall score
            var overallScore = (sessionScore * 0.3m) + 
                              (liquidityScore * 0.3m) + 
                              (structureScore * 0.2m) + 
                              (timingScore * 0.2m);

            result.IsPassing = overallScore >= 0.6m;
            result.ComplianceScore = overallScore;

            if (!result.IsPassing)
            {
                var reasons = new List<string>();
                
                if (sessionScore < 0.6m)
                    reasons.Add($"Poor session for trading ({marketConditions.Session})");
                if (liquidityScore < 0.6m)
                    reasons.Add("Insufficient liquidity");
                if (structureScore < 0.6m)
                    reasons.Add("Unfavorable market structure");
                if (timingScore < 0.6m)
                    reasons.Add("Poor entry timing");
                    
                result.Reason = string.Join("; ", reasons);
            }
            else
            {
                result.Reason = "Good understanding of market structure and timing";
            }

            // Add structure details
            result.Details["Session"] = marketConditions.Session.ToString();
            result.Details["SessionScore"] = sessionScore;
            result.Details["LiquidityScore"] = liquidityScore;
            result.Details["StructureScore"] = structureScore;
            result.Details["TimingScore"] = timingScore;
            result.Details["Spread"] = marketConditions.Ask - marketConditions.Bid;
            result.Details["RelativeVolume"] = marketConditions.RelativeVolume;
            result.Details["MarketPhase"] = DetermineMarketPhase(marketConditions);

            return result;
        }

        private decimal EvaluateSessionAppropriateness(MarketSession session, MarketConditions conditions)
        {
            var baseScore = _sessionQualityScores.GetValueOrDefault(session, 0.5m);

            // Adjust for specific conditions
            switch (session)
            {
                case MarketSession.PreMarket:
                    // Good for gap plays, bad for most others
                    if (Math.Abs((conditions.OpenPrice - conditions.PreviousClose) / conditions.PreviousClose) > 0.01m)
                        baseScore += 0.2m;
                    else
                        baseScore -= 0.1m;
                    break;

                case MarketSession.MarketOpen:
                    // Excellent for volatility strategies
                    if (conditions.Volatility > 0.02m)
                        baseScore += 0.1m;
                    break;

                case MarketSession.PowerHour:
                    // Good for trend continuation
                    if (conditions.Trend != TrendDirection.Sideways)
                        baseScore += 0.1m;
                    break;

                case MarketSession.AfterHours:
                    // Generally poor unless news-driven
                    if (!conditions.HasNewsCatalyst)
                        baseScore -= 0.2m;
                    break;
            }

            return Math.Max(0, Math.Min(1, baseScore));
        }

        private decimal EvaluateLiquidityConditions(MarketConditions conditions)
        {
            var score = 1m;

            // Check spread
            var spreadPercent = (conditions.Ask - conditions.Bid) / conditions.Price;
            if (spreadPercent > 0.001m) // 0.1% spread
                score -= 0.3m;
            else if (spreadPercent > 0.0005m) // 0.05% spread
                score -= 0.1m;

            // Check volume
            if (conditions.RelativeVolume < 0.8m)
                score -= 0.3m;
            else if (conditions.RelativeVolume < 1m)
                score -= 0.1m;

            // Check if volume is extremely high (potential pump)
            if (conditions.RelativeVolume > 5m)
                score -= 0.2m;

            return Math.Max(0, score);
        }

        private decimal EvaluateMarketStructure(MarketConditions conditions)
        {
            var score = 0.7m; // Base score

            // Support/Resistance structure
            var priceRange = conditions.DayHigh - conditions.DayLow;
            var currentPosition = (conditions.Price - conditions.DayLow) / priceRange;

            // Good: Trading near support/resistance
            if (currentPosition < 0.2m || currentPosition > 0.8m)
                score += 0.2m;
            // Bad: Trading in middle of range
            else if (currentPosition > 0.4m && currentPosition < 0.6m)
                score -= 0.2m;

            // Trend structure
            if (conditions.Trend == TrendDirection.StrongUptrend || 
                conditions.Trend == TrendDirection.StrongDowntrend)
            {
                score += 0.1m; // Clear structure
            }
            else if (conditions.Trend == TrendDirection.Sideways)
            {
                score -= 0.1m; // Choppy structure
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private decimal EvaluateEntryTiming(MarketConditions conditions, PositionContext context)
        {
            var score = 0.8m;

            // Avoid first/last 5 minutes
            var now = DateTime.UtcNow;
            var easternTime = TimeZoneInfo.ConvertTimeFromUtc(now, 
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            var time = easternTime.TimeOfDay;

            // First 5 minutes (9:30-9:35)
            if (time >= new TimeSpan(9, 30, 0) && time < new TimeSpan(9, 35, 0))
            {
                score -= 0.3m; // Very volatile
            }
            // Last 5 minutes (3:55-4:00)
            else if (time >= new TimeSpan(15, 55, 0) && time < new TimeSpan(16, 0, 0))
            {
                score -= 0.2m; // Closing volatility
            }

            // Lunch hour (12:00-1:00) - typically slow
            if (time >= new TimeSpan(12, 0, 0) && time < new TimeSpan(13, 0, 0))
            {
                score -= 0.2m;
            }

            // Check for news timing
            if (conditions.HasNewsCatalyst)
            {
                // Good: Trading with news momentum
                score += 0.1m;
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private string DetermineMarketPhase(MarketConditions conditions)
        {
            var priceChange = (conditions.Price - conditions.OpenPrice) / conditions.OpenPrice;

            if (conditions.Session == MarketSession.MarketOpen)
                return "Opening Range";
            else if (conditions.Session == MarketSession.PowerHour)
                return "Closing Momentum";
            else if (Math.Abs(priceChange) < 0.005m && conditions.Volatility < 0.01m)
                return "Consolidation";
            else if (conditions.Trend == TrendDirection.StrongUptrend || conditions.Trend == TrendDirection.StrongDowntrend)
                return "Trending";
            else
                return "Ranging";
        }

        public async Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var recommendations = new List<string>();

            // Session-specific recommendations
            recommendations.Add($"🕐 Current session: {marketConditions.Session}");
            
            switch (marketConditions.Session)
            {
                case MarketSession.PreMarket:
                    recommendations.Add("📈 Pre-market: Focus on gap plays and news");
                    recommendations.Add("⚠️ Lower liquidity - use limit orders");
                    break;
                case MarketSession.MarketOpen:
                    recommendations.Add("🔥 Market open: High volatility period");
                    recommendations.Add("✅ Wait for opening range to establish (first 30 min)");
                    break;
                case MarketSession.RegularHours:
                    recommendations.Add("📊 Regular hours: Best liquidity");
                    recommendations.Add("✅ Follow established trends");
                    break;
                case MarketSession.PowerHour:
                    recommendations.Add("⚡ Power hour: Increased activity");
                    recommendations.Add("✅ Look for continuation or reversal setups");
                    break;
                case MarketSession.AfterHours:
                    recommendations.Add("🌙 After hours: Limited liquidity");
                    recommendations.Add("⚠️ Wider spreads - trade only with news");
                    break;
            }

            // Liquidity analysis
            var spread = marketConditions.Ask - marketConditions.Bid;
            var spreadPercent = spread / marketConditions.Price;
            recommendations.Add($"💧 Spread: {spread:C4} ({spreadPercent:P2})");
            
            if (spreadPercent > 0.001m)
            {
                recommendations.Add("⚠️ Wide spread - consider liquidity risk");
            }

            // Market phase
            var phase = DetermineMarketPhase(marketConditions);
            recommendations.Add($"📊 Market phase: {phase}");

            // General structure tips
            recommendations.Add("✅ Know key support/resistance levels");
            recommendations.Add("✅ Trade with market rhythm, not against it");
            recommendations.Add("✅ Avoid lunch hour (12-1 PM ET) doldrums");
            recommendations.Add("✅ Best opportunities: First and last hour");

            return recommendations;
        }
    }
}