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
    /// Rule 5: Trade with the Trend
    /// "The trend is your friend" - always trade in the direction of the dominant market trend
    /// </summary>
    public class Rule05_TradeWithTrend : IGoldenRuleEvaluator
    {
        public int RuleNumber => 5;
        public string RuleName => "Trade with the Trend";
        public RuleCategory Category => RuleCategory.MarketAnalysis;

        private readonly decimal _trendStrengthThreshold = 0.6m; // Minimum trend strength
        private readonly int _againstTrendPenalty = 3; // Multiplier for against-trend risk

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

            // Determine if trade aligns with trend
            var isAlignedWithTrend = IsTrendAligned(side, marketConditions.Trend);
            var trendStrength = CalculateTrendStrength(marketConditions);

            // Calculate compliance score based on trend alignment and strength
            decimal complianceScore;
            string reason;

            if (isAlignedWithTrend)
            {
                if (marketConditions.Trend == TrendDirection.StrongUptrend || 
                    marketConditions.Trend == TrendDirection.StrongDowntrend)
                {
                    complianceScore = 1m;
                    reason = "Trade perfectly aligned with strong trend";
                }
                else if (marketConditions.Trend == TrendDirection.Uptrend || 
                         marketConditions.Trend == TrendDirection.Downtrend)
                {
                    complianceScore = 0.8m;
                    reason = "Trade aligned with moderate trend";
                }
                else // Sideways
                {
                    complianceScore = 0.5m;
                    reason = "No clear trend - trade with caution";
                    result.Severity = RuleSeverity.Warning;
                }
            }
            else
            {
                // Trading against the trend
                if (marketConditions.Trend == TrendDirection.StrongUptrend || 
                    marketConditions.Trend == TrendDirection.StrongDowntrend)
                {
                    complianceScore = 0m;
                    reason = "DANGER: Trading against strong trend!";
                    result.Severity = RuleSeverity.Blocking;
                }
                else if (marketConditions.Trend == TrendDirection.Uptrend || 
                         marketConditions.Trend == TrendDirection.Downtrend)
                {
                    complianceScore = 0.2m;
                    reason = "Warning: Trading against trend";
                }
                else // Sideways
                {
                    complianceScore = 0.6m;
                    reason = "Sideways market - direction uncertain";
                }
            }

            result.IsPassing = complianceScore >= 0.5m;
            result.ComplianceScore = complianceScore;
            result.Reason = reason;

            // Add details
            result.Details["Trend"] = marketConditions.Trend.ToString();
            result.Details["TrendStrength"] = trendStrength;
            result.Details["IsAligned"] = isAlignedWithTrend;
            result.Details["TradeSide"] = side.ToString();
            result.Details["MovingAverageAlignment"] = CheckMovingAverageAlignment(price, marketConditions);

            return result;
        }

        private bool IsTrendAligned(OrderSide side, TrendDirection trend)
        {
            return side switch
            {
                OrderSide.Buy => trend == TrendDirection.Uptrend || 
                                trend == TrendDirection.StrongUptrend,
                OrderSide.Sell => trend == TrendDirection.Downtrend || 
                                 trend == TrendDirection.StrongDowntrend,
                _ => false
            };
        }

        private decimal CalculateTrendStrength(MarketConditions conditions)
        {
            // Calculate trend strength based on various factors
            var strength = 0m;

            // Price action strength
            var priceChange = (conditions.Price - conditions.OpenPrice) / conditions.OpenPrice;
            strength += Math.Min(Math.Abs(priceChange) * 10, 0.3m);

            // Volume confirmation
            if (conditions.RelativeVolume > 1.5m)
                strength += 0.3m;
            else if (conditions.RelativeVolume > 1m)
                strength += 0.2m;

            // Momentum
            strength += conditions.Momentum * 0.4m;

            return Math.Min(strength, 1m);
        }

        private bool CheckMovingAverageAlignment(decimal price, MarketConditions conditions)
        {
            // Check if price is above/below key moving averages
            if (conditions.TechnicalIndicators.ContainsKey("MA20") && 
                conditions.TechnicalIndicators.ContainsKey("MA50"))
            {
                var ma20 = conditions.TechnicalIndicators["MA20"];
                var ma50 = conditions.TechnicalIndicators["MA50"];
                
                // Bullish: Price > MA20 > MA50
                // Bearish: Price < MA20 < MA50
                return (price > ma20 && ma20 > ma50) || (price < ma20 && ma20 < ma50);
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

            // Trend analysis
            recommendations.Add($"📈 Current trend: {marketConditions.Trend}");
            
            switch (marketConditions.Trend)
            {
                case TrendDirection.StrongUptrend:
                    recommendations.Add("🟢 Strong uptrend - focus on long positions only");
                    recommendations.Add("✅ Look for pullbacks to support for entry");
                    break;
                case TrendDirection.Uptrend:
                    recommendations.Add("🟢 Uptrend - prefer long positions");
                    recommendations.Add("⚠️ Be cautious with shorts");
                    break;
                case TrendDirection.Sideways:
                    recommendations.Add("➡️ Sideways market - range trading strategies");
                    recommendations.Add("📊 Wait for breakout confirmation");
                    break;
                case TrendDirection.Downtrend:
                    recommendations.Add("🔴 Downtrend - prefer short positions");
                    recommendations.Add("⚠️ Be cautious with longs");
                    break;
                case TrendDirection.StrongDowntrend:
                    recommendations.Add("🔴 Strong downtrend - focus on short positions only");
                    recommendations.Add("✅ Look for rallies to resistance for entry");
                    break;
            }

            if (marketConditions.Momentum < 0.3m)
            {
                recommendations.Add("⚠️ Weak momentum - wait for stronger confirmation");
            }

            recommendations.Add("✅ Always trade in direction of higher timeframe trend");
            recommendations.Add("✅ Use multiple timeframes to confirm trend direction");
            recommendations.Add("✅ Don't fight the trend - it's stronger than you");

            return recommendations;
        }
    }
}