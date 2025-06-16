using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Strategies;

/// <summary>
/// Base interface for all trading strategies
/// Defines common contract for signal generation and strategy execution
/// </summary>
public interface IStrategyBase
{
    /// <summary>
    /// Generate trading signals based on market conditions
    /// </summary>
    Task<TradingSignal[]> GenerateSignalsAsync(string symbol, MarketConditions conditions);

    /// <summary>
    /// Strategy name identifier
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Strategy description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validate if strategy can trade this symbol
    /// </summary>
    bool CanTrade(string symbol);

    /// <summary>
    /// Get strategy-specific risk parameters
    /// </summary>
    RiskLimits GetRiskLimits();
}

/// <summary>
/// Golden Rules trading strategy interface
/// Implements the 12 Golden Rules of Day Trading
/// </summary>
public interface IGoldenRulesStrategy : IStrategyBase
{
    /// <summary>
    /// Evaluate Golden Rules compliance for current market conditions
    /// </summary>
    Task<GoldenRulesAssessment> EvaluateGoldenRulesAsync(string symbol, MarketConditions conditions);

    /// <summary>
    /// Get current Golden Rules status
    /// </summary>
    Task<GoldenRuleStatus[]> GetRuleStatusAsync();
}

/// <summary>
/// Momentum trading strategy interface
/// </summary>
public interface IMomentumStrategy : IStrategyBase
{
    /// <summary>
    /// Detect momentum breakouts
    /// </summary>
    Task<MomentumSignal[]> DetectMomentumAsync(string symbol, MarketConditions conditions);

    /// <summary>
    /// Calculate momentum strength
    /// </summary>
    Task<decimal> CalculateMomentumStrengthAsync(string symbol, MarketConditions conditions);
}

/// <summary>
/// Gap trading strategy interface
/// </summary>
public interface IGapStrategy : IStrategyBase
{
    /// <summary>
    /// Detect gap patterns
    /// </summary>
    Task<GapPattern[]> DetectGapsAsync(string symbol, MarketConditions conditions);

    /// <summary>
    /// Assess gap fill probability
    /// </summary>
    Task<decimal> AssessGapFillProbabilityAsync(string symbol, GapPattern gap);
}

// Supporting data models for strategies

/// <summary>
/// Golden Rules assessment result
/// </summary>
public record GoldenRulesAssessment(
    bool OverallCompliance,
    int PassingRules,
    int TotalRules,
    string[] ViolatedRules,
    decimal ConfidenceScore,
    string Recommendation);

/// <summary>
/// Individual Golden Rule status
/// </summary>
public record GoldenRuleStatus(
    int RuleNumber,
    string RuleName,
    string Description,
    bool IsCompliant,
    string Status,
    decimal Weight);

/// <summary>
/// Momentum signal information
/// </summary>
public record MomentumSignal(
    string Symbol,
    decimal Strength,
    TrendDirection Direction,
    decimal BreakoutLevel,
    decimal VolumeConfirmation,
    DateTimeOffset Timestamp);

/// <summary>
/// Gap pattern detection result
/// </summary>
public record GapPattern(
    string Symbol,
    GapType GapType,
    decimal GapSize,
    decimal GapPercentage,
    decimal OpenPrice,
    decimal PreviousClose,
    bool HasVolumeConfirmation,
    DateTimeOffset Timestamp);

/// <summary>
/// Gap types for classification
/// </summary>
public enum GapType
{
    GapUp,
    GapDown,
    ExhaustionGap,
    BreakoutGap,
    CommonGap
}