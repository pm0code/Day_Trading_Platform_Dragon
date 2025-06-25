using System;
using System.Collections.Generic;

namespace TradingPlatform.GoldenRules.Models
{
    /// <summary>
    /// Represents one of the 12 Golden Rules of Day Trading
    /// </summary>
    public class GoldenRule
    {
        public int RuleNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RuleCategory Category { get; set; }
        public RuleSeverity Severity { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Categories of Golden Rules
    /// </summary>
    public enum RuleCategory
    {
        RiskManagement,
        Psychology,
        TechnicalAnalysis,
        TradeManagement,
        MarketStructure,
        Discipline
    }

    /// <summary>
    /// Severity levels for rule violations
    /// </summary>
    public enum RuleSeverity
    {
        Info,
        Warning,
        Critical,
        Blocking // Prevents trade execution
    }

    /// <summary>
    /// Rule evaluation result
    /// </summary>
    public class RuleEvaluationResult
    {
        public int RuleNumber { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public bool IsPassing { get; set; }
        public decimal ComplianceScore { get; set; }
        public string Reason { get; set; } = string.Empty;
        public RuleSeverity Severity { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Overall Golden Rules assessment
    /// </summary>
    public class GoldenRulesAssessment
    {
        public string Symbol { get; set; } = string.Empty;
        public List<RuleEvaluationResult> RuleResults { get; set; } = new();
        public bool OverallCompliance { get; set; }
        public decimal ConfidenceScore { get; set; }
        public int PassingRules { get; set; }
        public int FailingRules { get; set; }
        public int BlockingViolations { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public DateTime AssessmentTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> MarketContext { get; set; } = new();
    }

    /// <summary>
    /// Trading session compliance report
    /// </summary>
    public class GoldenRulesSessionReport
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public int TotalTradesEvaluated { get; set; }
        public int TradesExecuted { get; set; }
        public int TradesBlocked { get; set; }
        public Dictionary<int, RuleComplianceStats> RuleStats { get; set; } = new();
        public decimal OverallComplianceRate { get; set; }
        public decimal SessionPnL { get; set; }
        public List<RuleViolation> Violations { get; set; } = new();
    }

    /// <summary>
    /// Rule compliance statistics
    /// </summary>
    public class RuleComplianceStats
    {
        public int RuleNumber { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public int EvaluationCount { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public decimal ComplianceRate { get; set; }
        public decimal AverageScore { get; set; }
        public List<DateTime> ViolationTimes { get; set; } = new();
    }

    /// <summary>
    /// Rule violation record
    /// </summary>
    public class RuleViolation
    {
        public string ViolationId { get; set; } = Guid.NewGuid().ToString();
        public int RuleNumber { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public RuleSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal PotentialLoss { get; set; }
        public DateTime ViolationTime { get; set; } = DateTime.UtcNow;
        public string CorrectiveAction { get; set; } = string.Empty;
        public bool WasOverridden { get; set; }
        public string? OverrideReason { get; set; }
    }

    /// <summary>
    /// Market conditions for rule evaluation
    /// </summary>
    public class MarketConditions
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal Volume { get; set; }
        public decimal DayHigh { get; set; }
        public decimal DayLow { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal ATR { get; set; } // Average True Range
        public decimal Volatility { get; set; }
        public TrendDirection Trend { get; set; }
        public decimal RelativeVolume { get; set; }
        public bool IsNewsEvent { get; set; }
        public MarketSession Session { get; set; }
        public Dictionary<string, decimal> TechnicalIndicators { get; set; } = new();
    }

    /// <summary>
    /// Market trend direction
    /// </summary>
    public enum TrendDirection
    {
        StrongUptrend,
        Uptrend,
        Sideways,
        Downtrend,
        StrongDowntrend
    }

    /// <summary>
    /// Market session types
    /// </summary>
    public enum MarketSession
    {
        PreMarket,
        MarketOpen,
        RegularHours,
        PowerHour,
        MarketClose,
        AfterHours
    }

    /// <summary>
    /// Position context for rule evaluation
    /// </summary>
    public class PositionContext
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public TimeSpan HoldingPeriod { get; set; }
        public int DayTradeCount { get; set; }
        public decimal AccountBalance { get; set; }
        public decimal BuyingPower { get; set; }
        public decimal DailyPnL { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public Dictionary<string, decimal> OpenPositions { get; set; } = new();
    }

    /// <summary>
    /// Rule configuration
    /// </summary>
    public class GoldenRuleConfiguration
    {
        public int RuleNumber { get; set; }
        public bool Enabled { get; set; } = true;
        public RuleSeverity Severity { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> ExemptSymbols { get; set; } = new();
        public TimeSpan? ActiveTimeWindow { get; set; }
        public bool RequiresApproval { get; set; }
    }

    /// <summary>
    /// Golden Rules engine configuration
    /// </summary>
    public class GoldenRulesEngineConfig
    {
        public bool Enabled { get; set; } = true;
        public bool StrictMode { get; set; } = true; // Blocks all violations
        public decimal MinimumComplianceScore { get; set; } = 0.8m;
        public int MaxDailyViolations { get; set; } = 3;
        public bool EnableRealTimeAlerts { get; set; } = true;
        public bool EnableSessionReporting { get; set; } = true;
        public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromHours(1);
        public List<GoldenRuleConfiguration> RuleConfigs { get; set; } = new();
    }
}