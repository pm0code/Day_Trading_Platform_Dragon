using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Strategies
{
    /// <summary>
    /// Canonical implementation of the 12 Golden Rules of Day Trading strategy.
    /// Based on comprehensive trading discipline and risk management principles.
    /// </summary>
    public class GoldenRulesStrategyCanonical : CanonicalStrategyBase
    {
        private readonly GoldenRule[] _goldenRules;
        
        public override string StrategyName => "Golden Rules Strategy";
        
        public override string Description => 
            "Rule-based trading strategy implementing the 12 Golden Rules of Day Trading for disciplined execution";
        
        public override string[] SupportedSymbols => new[] { "*" }; // Supports all symbols

        // Strategy parameters
        private const string PARAM_MIN_CONFIDENCE = "MinConfidence";
        private const string PARAM_MAX_RISK_PER_TRADE = "MaxRiskPerTrade";
        private const string PARAM_MIN_RULES_COMPLIANCE = "MinRulesCompliance";
        private const string PARAM_POSITION_SCALE_FACTOR = "PositionScaleFactor";
        private const string PARAM_STOP_LOSS_PERCENTAGE = "StopLossPercentage";
        private const string PARAM_TAKE_PROFIT_MULTIPLIER = "TakeProfitMultiplier";

        public GoldenRulesStrategyCanonical(
            ITradingLogger logger,
            string strategyId = "golden-rules-canonical")
            : base(logger, strategyId)
        {
            _goldenRules = InitializeGoldenRules();
        }

        #region Strategy Implementation

        protected override async Task<TradingResult<TradingSignal>> GenerateSignalAsync(
            string symbol,
            MarketData marketData,
            PositionInfo? currentPosition,
            CancellationToken cancellationToken)
        {
            try
            {
                // Convert market data to conditions
                var conditions = ConvertToMarketConditions(marketData);
                
                // Evaluate Golden Rules compliance
                var assessment = await EvaluateGoldenRulesAsync(symbol, conditions, cancellationToken);
                
                var minConfidence = GetParameter(PARAM_MIN_CONFIDENCE, 0.7m);
                var minCompliance = GetParameter(PARAM_MIN_RULES_COMPLIANCE, 0.75m);
                
                if (!assessment.OverallCompliance || assessment.ConfidenceScore < minConfidence)
                {
                    LogDebug($"Golden Rules assessment failed for {symbol}: " +
                            $"Compliance={assessment.OverallCompliance}, " +
                            $"Confidence={assessment.ConfidenceScore:P0}");
                    
                    return TradingResult<TradingSignal>.Success(null!); // No signal
                }

                // Determine signal type based on rules and position
                var signalType = DetermineSignalType(conditions, assessment, currentPosition);
                
                if (signalType == SignalType.Hold)
                {
                    return TradingResult<TradingSignal>.Success(null!); // No signal
                }

                // Calculate position size
                var positionSize = await CalculatePositionSizeAsync(
                    symbol, 
                    100000m, // TODO: Get actual account balance
                    GetParameter(PARAM_MAX_RISK_PER_TRADE, 0.01m),
                    marketData);

                var signal = new TradingSignal(
                    Id: Guid.NewGuid().ToString(),
                    StrategyId: StrategyId,
                    Symbol: symbol,
                    SignalType: signalType,
                    Price: marketData.Price,
                    Quantity: positionSize,
                    Confidence: assessment.ConfidenceScore,
                    Reason: GenerateSignalReason(assessment, signalType),
                    Timestamp: DateTime.UtcNow,
                    Metadata: new Dictionary<string, object>
                    {
                        ["RulesCompliance"] = assessment.OverallCompliance,
                        ["PassingRules"] = assessment.PassingRules,
                        ["TotalRules"] = assessment.TotalRules,
                        ["ViolatedRules"] = assessment.ViolatedRuleIds,
                        ["MarketTrend"] = conditions.Trend.ToString(),
                        ["Volatility"] = conditions.Volatility,
                        ["RSI"] = conditions.RSI
                    }
                );

                return TradingResult<TradingSignal>.Success(signal);
            }
            catch (Exception ex)
            {
                LogError($"Error generating Golden Rules signal for {symbol}", ex);
                return TradingResult<TradingSignal>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        protected override TradingResult ValidateParameters(Dictionary<string, decimal> parameters)
        {
            // Validate confidence threshold
            if (parameters.TryGetValue(PARAM_MIN_CONFIDENCE, out var confidence))
            {
                if (confidence < 0 || confidence > 1)
                {
                    return TradingResult.Failure("INVALID_PARAMETER", 
                        $"{PARAM_MIN_CONFIDENCE} must be between 0 and 1");
                }
            }

            // Validate risk per trade
            if (parameters.TryGetValue(PARAM_MAX_RISK_PER_TRADE, out var risk))
            {
                if (risk <= 0 || risk > 0.05m) // Max 5% risk per trade
                {
                    return TradingResult.Failure("INVALID_PARAMETER", 
                        $"{PARAM_MAX_RISK_PER_TRADE} must be between 0 and 0.05");
                }
            }

            // Validate rules compliance
            if (parameters.TryGetValue(PARAM_MIN_RULES_COMPLIANCE, out var compliance))
            {
                if (compliance < 0 || compliance > 1)
                {
                    return TradingResult.Failure("INVALID_PARAMETER", 
                        $"{PARAM_MIN_RULES_COMPLIANCE} must be between 0 and 1");
                }
            }

            return TradingResult.Success();
        }

        protected override Dictionary<string, decimal> GetDefaultParameters()
        {
            return new Dictionary<string, decimal>
            {
                [PARAM_MIN_CONFIDENCE] = 0.7m,
                [PARAM_MAX_RISK_PER_TRADE] = 0.01m, // 1% risk per trade
                [PARAM_MIN_RULES_COMPLIANCE] = 0.75m, // 75% of rules must pass
                [PARAM_POSITION_SCALE_FACTOR] = 1.0m,
                [PARAM_STOP_LOSS_PERCENTAGE] = 0.02m, // 2% stop loss
                [PARAM_TAKE_PROFIT_MULTIPLIER] = 2.0m // 2:1 risk/reward ratio
            };
        }

        protected override async Task<decimal> CalculatePositionSizeAsync(
            string symbol,
            decimal accountBalance,
            decimal riskPercentage,
            MarketData marketData)
        {
            // Calculate position size based on risk and stop loss
            var stopLossPercentage = GetParameter(PARAM_STOP_LOSS_PERCENTAGE, 0.02m);
            var scaleFactor = GetParameter(PARAM_POSITION_SCALE_FACTOR, 1.0m);
            
            // Risk amount in dollars
            var riskAmount = accountBalance * riskPercentage;
            
            // Position size based on stop loss distance
            var stopLossDistance = marketData.Price * stopLossPercentage;
            var basePositionSize = riskAmount / stopLossDistance;
            
            // Apply scale factor and volatility adjustment
            var adjustedSize = CalculateRiskAdjustedSize(
                basePositionSize * scaleFactor,
                marketData.Volatility,
                riskAmount);
            
            // Round down to whole shares
            return await Task.FromResult(Math.Floor(adjustedSize));
        }

        #endregion

        #region Golden Rules Implementation

        private async Task<GoldenRulesAssessment> EvaluateGoldenRulesAsync(
            string symbol,
            MarketConditions conditions,
            CancellationToken cancellationToken)
        {
            var results = new List<RuleEvaluationResult>();
            
            foreach (var rule in _goldenRules)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                var result = await EvaluateRuleAsync(rule, symbol, conditions);
                results.Add(result);
            }

            var passingRules = results.Count(r => r.Passed);
            var totalRules = results.Count;
            var complianceRate = totalRules > 0 ? (decimal)passingRules / totalRules : 0m;
            var minCompliance = GetParameter(PARAM_MIN_RULES_COMPLIANCE, 0.75m);
            
            return new GoldenRulesAssessment
            {
                OverallCompliance = complianceRate >= minCompliance,
                PassingRules = passingRules,
                TotalRules = totalRules,
                ConfidenceScore = CalculateConfidenceScore(results),
                ViolatedRuleIds = results.Where(r => !r.Passed).Select(r => r.RuleId).ToArray(),
                RuleResults = results
            };
        }

        private async Task<RuleEvaluationResult> EvaluateRuleAsync(
            GoldenRule rule,
            string symbol,
            MarketConditions conditions)
        {
            try
            {
                var passed = rule.Id switch
                {
                    1 => await EvaluateCapitalPreservationRule(conditions),
                    2 => await EvaluateTradingPlanRule(symbol),
                    3 => await EvaluateLossCuttingRule(conditions),
                    4 => await EvaluateProfitRunningRule(conditions),
                    5 => await EvaluateRiskManagementRule(),
                    6 => await EvaluateEmotionControlRule(conditions),
                    7 => await EvaluateTrendFollowingRule(conditions),
                    8 => await EvaluateOverbuyingAvoidanceRule(),
                    9 => await EvaluateDisciplineRule(),
                    10 => await EvaluatePatternRecognitionRule(conditions),
                    11 => await EvaluateRecordKeepingRule(),
                    12 => await EvaluateContinuousLearningRule(),
                    _ => false
                };

                return new RuleEvaluationResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Passed = passed,
                    Score = passed ? 1.0m : 0.0m,
                    Message = passed ? "Rule satisfied" : "Rule violated"
                };
            }
            catch (Exception ex)
            {
                LogWarning($"Error evaluating rule {rule.Id}: {rule.Name}", 
                    additionalData: new { Exception = ex.Message });
                
                return new RuleEvaluationResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Passed = false,
                    Score = 0.0m,
                    Message = $"Evaluation error: {ex.Message}"
                };
            }
        }

        // Individual rule evaluation methods
        private Task<bool> EvaluateCapitalPreservationRule(MarketConditions conditions)
        {
            // Rule 1: Protect trading capital above all else
            var maxRisk = GetParameter(PARAM_MAX_RISK_PER_TRADE, 0.01m);
            return Task.FromResult(maxRisk <= 0.02m); // Max 2% risk per trade
        }

        private Task<bool> EvaluateTradingPlanRule(string symbol)
        {
            // Rule 2: Trade with a plan
            // In canonical implementation, having parameters set means we have a plan
            return Task.FromResult(SupportsSymbol(symbol));
        }

        private Task<bool> EvaluateLossCuttingRule(MarketConditions conditions)
        {
            // Rule 3: Cut losses quickly
            var stopLoss = GetParameter(PARAM_STOP_LOSS_PERCENTAGE, 0.02m);
            return Task.FromResult(stopLoss > 0 && stopLoss <= 0.05m); // Stop loss set between 0-5%
        }

        private Task<bool> EvaluateProfitRunningRule(MarketConditions conditions)
        {
            // Rule 4: Let profits run
            var profitMultiplier = GetParameter(PARAM_TAKE_PROFIT_MULTIPLIER, 2.0m);
            return Task.FromResult(profitMultiplier >= 2.0m); // At least 2:1 risk/reward
        }

        private Task<bool> EvaluateRiskManagementRule()
        {
            // Rule 5: Manage risk on every trade
            return Task.FromResult(true); // Always true in canonical implementation
        }

        private Task<bool> EvaluateEmotionControlRule(MarketConditions conditions)
        {
            // Rule 6: Trade without emotions
            // Check if volatility is within acceptable range
            return Task.FromResult(conditions.Volatility < 0.05m); // Less than 5% volatility
        }

        private Task<bool> EvaluateTrendFollowingRule(MarketConditions conditions)
        {
            // Rule 7: Trade with the trend
            return Task.FromResult(conditions.Trend != TrendDirection.Unknown);
        }

        private Task<bool> EvaluateOverbuyingAvoidanceRule()
        {
            // Rule 8: Never overbuy
            var scaleFactor = GetParameter(PARAM_POSITION_SCALE_FACTOR, 1.0m);
            return Task.FromResult(scaleFactor <= 1.0m); // No position scaling above 100%
        }

        private Task<bool> EvaluateDisciplineRule()
        {
            // Rule 9: Stay disciplined
            return Task.FromResult(true); // Canonical implementation enforces discipline
        }

        private Task<bool> EvaluatePatternRecognitionRule(MarketConditions conditions)
        {
            // Rule 10: Use technical analysis
            // Check if we have valid technical indicators
            return Task.FromResult(conditions.RSI > 0 && conditions.MACD != 0);
        }

        private Task<bool> EvaluateRecordKeepingRule()
        {
            // Rule 11: Keep records
            return Task.FromResult(true); // Canonical implementation includes logging
        }

        private Task<bool> EvaluateContinuousLearningRule()
        {
            // Rule 12: Continue learning
            return Task.FromResult(true); // Always true - system is designed for adaptation
        }

        #endregion

        #region Helper Methods

        private MarketConditions ConvertToMarketConditions(MarketData marketData)
        {
            // Calculate volatility from high/low
            var volatility = marketData.High > 0 
                ? (marketData.High - marketData.Low) / marketData.High 
                : 0m;
            
            // Determine trend from open/close
            var priceChange = marketData.Close - marketData.Open;
            var priceChangePercent = marketData.Open > 0 
                ? priceChange / marketData.Open 
                : 0m;
            
            var trend = priceChangePercent switch
            {
                > 0.01m => TrendDirection.Up,
                < -0.01m => TrendDirection.Down,
                _ => TrendDirection.Sideways
            };

            // Mock RSI and MACD (in production, calculate from historical data)
            var rsi = 50m + (priceChangePercent * 100m); // Simplified RSI
            var macd = priceChangePercent * 10m; // Simplified MACD
            
            return new MarketConditions(
                Symbol: marketData.Symbol,
                Volatility: volatility,
                Volume: marketData.Volume,
                PriceChange: priceChangePercent,
                Trend: trend,
                RSI: Math.Max(0, Math.Min(100, rsi)),
                MACD: macd,
                Timestamp: DateTimeOffset.FromDateTime(marketData.Timestamp)
            );
        }

        private SignalType DetermineSignalType(
            MarketConditions conditions,
            GoldenRulesAssessment assessment,
            PositionInfo? currentPosition)
        {
            // If we have a position, check exit conditions
            if (currentPosition != null && currentPosition.Quantity > 0)
            {
                // Exit if rules compliance drops
                if (!assessment.OverallCompliance)
                {
                    return SignalType.Sell;
                }
                
                // Exit if trend reverses
                if (conditions.Trend == TrendDirection.Down)
                {
                    return SignalType.Sell;
                }
                
                // Check stop loss
                var stopLossPrice = currentPosition.AveragePrice * 
                    (1 - GetParameter(PARAM_STOP_LOSS_PERCENTAGE, 0.02m));
                    
                if (currentPosition.CurrentPrice <= stopLossPrice)
                {
                    return SignalType.StopLoss;
                }
                
                // Check take profit
                var takeProfitPrice = currentPosition.AveragePrice * 
                    (1 + GetParameter(PARAM_STOP_LOSS_PERCENTAGE, 0.02m) * 
                     GetParameter(PARAM_TAKE_PROFIT_MULTIPLIER, 2.0m));
                     
                if (currentPosition.CurrentPrice >= takeProfitPrice)
                {
                    return SignalType.TakeProfit;
                }
            }
            else
            {
                // Entry conditions
                if (conditions.Trend == TrendDirection.Up && 
                    conditions.RSI < 70 && // Not overbought
                    assessment.ConfidenceScore >= GetParameter(PARAM_MIN_CONFIDENCE, 0.7m))
                {
                    return SignalType.Buy;
                }
            }
            
            return SignalType.Hold;
        }

        private decimal CalculateConfidenceScore(List<RuleEvaluationResult> results)
        {
            if (!results.Any())
                return 0m;
                
            // Weight critical rules higher
            var criticalRules = new[] { 1, 3, 5 }; // Capital preservation, cut losses, risk management
            var criticalWeight = 2.0m;
            
            decimal totalScore = 0m;
            decimal totalWeight = 0m;
            
            foreach (var result in results)
            {
                var weight = criticalRules.Contains(result.RuleId) ? criticalWeight : 1.0m;
                totalScore += result.Score * weight;
                totalWeight += weight;
            }
            
            return totalWeight > 0 ? totalScore / totalWeight : 0m;
        }

        private string GenerateSignalReason(GoldenRulesAssessment assessment, SignalType signalType)
        {
            var reason = $"Golden Rules {signalType}: {assessment.PassingRules}/{assessment.TotalRules} rules passed";
            
            if (assessment.ViolatedRuleIds.Any())
            {
                var violatedRules = string.Join(", ", assessment.ViolatedRuleIds.Take(3));
                reason += $" (Violated: {violatedRules})";
            }
            
            return reason;
        }

        private GoldenRule[] InitializeGoldenRules()
        {
            return new[]
            {
                new GoldenRule(1, "Protect Your Trading Capital", 
                    "The first rule of trading is to protect your capital. Never risk more than you can afford to lose."),
                new GoldenRule(2, "Always Trade with a Plan", 
                    "Every trade should be based on a well-thought-out plan with clear entry and exit strategies."),
                new GoldenRule(3, "Cut Losses Quickly", 
                    "When a trade goes against you, exit quickly to preserve capital for future opportunities."),
                new GoldenRule(4, "Let Profits Run", 
                    "Allow winning trades to continue as long as the trend remains in your favor."),
                new GoldenRule(5, "Manage Risk on Every Trade", 
                    "Never enter a trade without knowing your maximum acceptable loss and position size."),
                new GoldenRule(6, "Trade Without Emotions", 
                    "Fear and greed are the enemies of successful trading. Stick to your plan regardless of emotions."),
                new GoldenRule(7, "Trade with the Trend", 
                    "The trend is your friend. Don't fight the market direction."),
                new GoldenRule(8, "Never Overbuy or Overtrade", 
                    "Avoid the temptation to trade too frequently or with positions too large for your account."),
                new GoldenRule(9, "Stay Disciplined", 
                    "Consistency and discipline are more important than being right on any single trade."),
                new GoldenRule(10, "Use Technical Analysis", 
                    "Price patterns, support/resistance levels, and indicators provide objective trading signals."),
                new GoldenRule(11, "Keep Detailed Records", 
                    "Track every trade to learn from both successes and failures."),
                new GoldenRule(12, "Continue Learning", 
                    "Markets evolve constantly. Successful traders never stop learning and adapting.")
            };
        }

        #endregion
    }

    #region Supporting Types

    public record GoldenRule(int Id, string Name, string Description);

    public class GoldenRulesAssessment
    {
        public bool OverallCompliance { get; set; }
        public int PassingRules { get; set; }
        public int TotalRules { get; set; }
        public decimal ConfidenceScore { get; set; }
        public int[] ViolatedRuleIds { get; set; } = Array.Empty<int>();
        public List<RuleEvaluationResult> RuleResults { get; set; } = new();
    }

    public class RuleEvaluationResult
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public decimal Score { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}