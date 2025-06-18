using TradingPlatform.Core.Interfaces;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Strategies;

/// <summary>
/// Implementation of the 12 Golden Rules of Day Trading strategy
/// Based on comprehensive trading discipline and risk management principles
/// </summary>
public class GoldenRulesStrategy : IGoldenRulesStrategy
{
    private readonly ILogger _logger;
    private readonly GoldenRule[] _goldenRules;

    public string StrategyName => "Golden Rules Strategy";
    public string Description => "Rule-based trading strategy implementing the 12 Golden Rules of Day Trading for disciplined execution";

    public GoldenRulesStrategy(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _goldenRules = InitializeGoldenRules();
    }

    public async Task<TradingSignal[]> GenerateSignalsAsync(string symbol, MarketConditions conditions)
    {
        try
        {
            _logger.LogDebug("Evaluating Golden Rules for {Symbol}", symbol);

            // Evaluate Golden Rules compliance
            var assessment = await EvaluateGoldenRulesAsync(symbol, conditions);
            
            if (!assessment.OverallCompliance || assessment.ConfidenceScore < 0.7m)
            {
                _logger.LogDebug("Golden Rules assessment failed for {Symbol}: Compliance={Compliance}, Confidence={Confidence}", 
                    symbol, assessment.OverallCompliance, assessment.ConfidenceScore);
                return Array.Empty<TradingSignal>();
            }

            var signals = new List<TradingSignal>();

            // Generate buy signal if conditions are favorable
            if (ShouldGenerateBuySignal(conditions, assessment))
            {
                var buySignal = new TradingSignal(
                    Guid.NewGuid().ToString(),
                    "golden-rules-momentum",
                    symbol,
                    SignalType.Buy,
                    conditions.Volatility * 100, // Mock price based on volatility
                    CalculatePositionSize(conditions),
                    assessment.ConfidenceScore,
                    $"Golden Rules Buy: {assessment.PassingRules}/{assessment.TotalRules} rules passed",
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["RulesCompliance"] = assessment.OverallCompliance,
                        ["PassingRules"] = assessment.PassingRules,
                        ["ViolatedRules"] = assessment.ViolatedRules
                    });

                signals.Add(buySignal);
            }

            // Generate sell signal if risk management rules are triggered
            if (ShouldGenerateSellSignal(conditions, assessment))
            {
                var sellSignal = new TradingSignal(
                    Guid.NewGuid().ToString(),
                    "golden-rules-momentum",
                    symbol,
                    SignalType.Sell,
                    conditions.Volatility * 100, // Mock price based on volatility
                    CalculatePositionSize(conditions),
                    assessment.ConfidenceScore,
                    $"Golden Rules Sell: Risk management triggered",
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["RiskTrigger"] = true,
                        ["RulesCompliance"] = assessment.OverallCompliance
                    });

                signals.Add(sellSignal);
            }

            _logger.LogInformation("Generated {SignalCount} Golden Rules signals for {Symbol}", 
                signals.Count, symbol);

            return signals.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Golden Rules signals for {Symbol}", symbol);
            return Array.Empty<TradingSignal>();
        }
    }

    public async Task<GoldenRulesAssessment> EvaluateGoldenRulesAsync(string symbol, MarketConditions conditions)
    {
        try
        {
            var evaluations = new List<RuleEvaluation>();

            // Evaluate each Golden Rule
            foreach (var rule in _goldenRules)
            {
                var isCompliant = await EvaluateIndividualRuleAsync(rule, symbol, conditions);
                evaluations.Add(new RuleEvaluation(rule.Number, rule.Name, isCompliant, rule.Weight));
            }

            var passingRules = evaluations.Count(e => e.IsCompliant);
            var totalRules = evaluations.Count;
            var violatedRules = evaluations.Where(e => !e.IsCompliant).Select(e => e.RuleName).ToArray();

            // Calculate weighted confidence score
            var totalWeight = evaluations.Sum(e => e.Weight);
            var passedWeight = evaluations.Where(e => e.IsCompliant).Sum(e => e.Weight);
            var confidenceScore = totalWeight > 0 ? passedWeight / totalWeight : 0.0m;

            // Overall compliance requires 80% of rules to pass
            var overallCompliance = (decimal)passingRules / totalRules >= 0.8m;

            var recommendation = GenerateRecommendation(overallCompliance, confidenceScore, violatedRules);

            return new GoldenRulesAssessment(
                overallCompliance,
                passingRules,
                totalRules,
                violatedRules,
                confidenceScore,
                recommendation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating Golden Rules for {Symbol}", symbol);
            return new GoldenRulesAssessment(false, 0, _goldenRules.Length, 
                new[] { "Evaluation Error" }, 0.0m, "Do not trade - evaluation failed");
        }
    }

    public async Task<GoldenRuleStatus[]> GetRuleStatusAsync()
    {
        await Task.CompletedTask;
        
        return _goldenRules.Select(rule => new GoldenRuleStatus(
            rule.Number,
            rule.Name,
            rule.Description,
            true, // Mock compliance status
            "Active",
            rule.Weight)).ToArray();
    }

    public bool CanTrade(string symbol)
    {
        // Golden Rules strategy can trade most liquid symbols
        var allowedSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "SPY", "QQQ" };
        return allowedSymbols.Contains(symbol.ToUpperInvariant());
    }

    public RiskLimits GetRiskLimits()
    {
        return new RiskLimits(
            MaxPositionSize: 10000.0m,    // $10k max position
            MaxDailyLoss: -500.0m,        // $500 max daily loss
            MaxPortfolioRisk: 0.02m,      // 2% portfolio risk
            MaxOpenPositions: 3,          // Max 3 concurrent positions
            StopLossPercentage: 0.02m);   // 2% stop loss
    }

    // Private implementation methods
    private async Task<bool> EvaluateIndividualRuleAsync(GoldenRule rule, string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        // Implementation of each Golden Rule evaluation
        return rule.Number switch
        {
            1 => EvaluateRule1_CapitalPreservation(conditions),
            2 => EvaluateRule2_TradingDiscipline(conditions),
            3 => EvaluateRule3_LossManagement(conditions),
            4 => EvaluateRule4_SystematicApproach(conditions),
            5 => EvaluateRule5_RiskManagement(conditions),
            6 => EvaluateRule6_MarketTiming(conditions),
            7 => EvaluateRule7_EmotionalControl(conditions),
            8 => EvaluateRule8_TechnicalAnalysis(conditions),
            9 => EvaluateRule9_VolumeConfirmation(conditions),
            10 => EvaluateRule10_TrendFollowing(conditions),
            11 => EvaluateRule11_ProfitTaking(conditions),
            12 => EvaluateRule12_ContinuousLearning(conditions),
            _ => false
        };
    }

    private bool ShouldGenerateBuySignal(MarketConditions conditions, GoldenRulesAssessment assessment)
    {
        return assessment.OverallCompliance &&
               assessment.ConfidenceScore >= 0.8m &&
               conditions.Trend == TrendDirection.Up &&
               conditions.Volume > 1000000 && // Minimum volume requirement
               conditions.RSI < 70; // Not overbought
    }

    private bool ShouldGenerateSellSignal(MarketConditions conditions, GoldenRulesAssessment assessment)
    {
        return conditions.RSI > 80 || // Overbought condition
               conditions.PriceChange < -0.02m || // 2% loss trigger
               !assessment.OverallCompliance; // Rules violation
    }

    private int CalculatePositionSize(MarketConditions conditions)
    {
        // Calculate position size based on volatility and risk limits
        var baseSize = 100;
        var volatilityAdjustment = conditions.Volatility > 0.03m ? 0.5 : 1.0;
        return (int)(baseSize * volatilityAdjustment);
    }

    // Golden Rules evaluation methods
    private bool EvaluateRule1_CapitalPreservation(MarketConditions conditions) => 
        conditions.Volatility < 0.05m; // Low volatility for capital preservation

    private bool EvaluateRule2_TradingDiscipline(MarketConditions conditions) => 
        true; // Always enforce discipline

    private bool EvaluateRule3_LossManagement(MarketConditions conditions) => 
        Math.Abs(conditions.PriceChange) < 0.05m; // Limit exposure to high price movements

    private bool EvaluateRule4_SystematicApproach(MarketConditions conditions) => 
        conditions.Volume > 500000; // Ensure adequate liquidity

    private bool EvaluateRule5_RiskManagement(MarketConditions conditions) => 
        conditions.Volatility < 0.04m; // Risk control through volatility

    private bool EvaluateRule6_MarketTiming(MarketConditions conditions) => 
        conditions.Trend != TrendDirection.Unknown; // Clear trend identification

    private bool EvaluateRule7_EmotionalControl(MarketConditions conditions) => 
        true; // Systematic approach eliminates emotions

    private bool EvaluateRule8_TechnicalAnalysis(MarketConditions conditions) => 
        conditions.RSI > 30 && conditions.RSI < 70; // RSI in normal range

    private bool EvaluateRule9_VolumeConfirmation(MarketConditions conditions) => 
        conditions.Volume > 1000000; // Volume confirmation

    private bool EvaluateRule10_TrendFollowing(MarketConditions conditions) => 
        conditions.Trend == TrendDirection.Up; // Follow uptrend

    private bool EvaluateRule11_ProfitTaking(MarketConditions conditions) => 
        conditions.RSI < 80; // Not in extreme overbought territory

    private bool EvaluateRule12_ContinuousLearning(MarketConditions conditions) => 
        true; // Always learning and adapting

    private string GenerateRecommendation(bool overallCompliance, decimal confidenceScore, string[] violatedRules)
    {
        if (!overallCompliance)
        {
            return $"Do not trade - Rule violations: {string.Join(", ", violatedRules)}";
        }

        return confidenceScore switch
        {
            >= 0.9m => "Strong buy signal - Excellent Golden Rules compliance",
            >= 0.8m => "Buy signal - Good Golden Rules compliance",
            >= 0.7m => "Weak buy signal - Adequate compliance",
            _ => "Hold - Insufficient compliance"
        };
    }

    private GoldenRule[] InitializeGoldenRules()
    {
        return new[]
        {
            new GoldenRule(1, "Capital Preservation", "Preserve capital above all else", 1.0m),
            new GoldenRule(2, "Trading Discipline", "Follow systematic trading rules", 0.9m),
            new GoldenRule(3, "Loss Management", "Cut losses quickly and systematically", 1.0m),
            new GoldenRule(4, "Systematic Approach", "Use systematic, rule-based decisions", 0.8m),
            new GoldenRule(5, "Risk Management", "Never risk more than you can afford to lose", 1.0m),
            new GoldenRule(6, "Market Timing", "Trade with proper market timing", 0.7m),
            new GoldenRule(7, "Emotional Control", "Maintain emotional discipline", 0.9m),
            new GoldenRule(8, "Technical Analysis", "Use proper technical analysis", 0.6m),
            new GoldenRule(9, "Volume Confirmation", "Confirm moves with volume", 0.7m),
            new GoldenRule(10, "Trend Following", "Trade with the trend", 0.8m),
            new GoldenRule(11, "Profit Taking", "Take profits systematically", 0.8m),
            new GoldenRule(12, "Continuous Learning", "Continuously improve and adapt", 0.5m)
        };
    }
}

// Supporting data structures
internal record GoldenRule(int Number, string Name, string Description, decimal Weight);
internal record RuleEvaluation(int RuleNumber, string RuleName, bool IsCompliant, decimal Weight);