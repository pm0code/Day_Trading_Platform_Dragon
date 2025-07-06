using System;
using System.Collections.Generic;

namespace TradingPlatform.CostManagement.Models
{
    /// <summary>
    /// Comprehensive cost dashboard with interactive controls for data source management
    /// </summary>
    public class CostDashboard
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalCost { get; set; }
        public decimal BudgetRemaining { get; set; }
        public decimal ProjectedMonthlyCost { get; set; }
        public Dictionary<string, DataSourceCostSummary> DataSourceCosts { get; set; } = new();
        public List<CostTrendPoint> CostTrends { get; set; } = new();
        public Dictionary<string, UsageMetrics> UsageMetrics { get; set; } = new();
        public List<CostRecommendation> Recommendations { get; set; } = new();
        public EfficiencyMetrics EfficiencyMetrics { get; set; } = new();
        public List<CostAlert> ActiveAlerts { get; set; } = new();
        
        // Interactive controls for each data source
        public Dictionary<string, DataSourceControls> DataSourceControls { get; set; } = new();
        
        // Budget tracking
        public BudgetSummary BudgetSummary { get; set; } = new();
        
        // ROI summary
        public Dictionary<string, ROISummary> ROISummaries { get; set; } = new();
    }

    /// <summary>
    /// Interactive controls for managing individual data sources
    /// </summary>
    public class DataSourceControls
    {
        public string DataSource { get; set; } = string.Empty;
        public DataSourceStatus CurrentStatus { get; set; }
        public List<DataSourceAction> AvailableActions { get; set; } = new();
        public bool CanSuspend { get; set; }
        public bool CanStop { get; set; }
        public bool CanRestart { get; set; }
        public bool CanUpgrade { get; set; }
        public bool CanDowngrade { get; set; }
        
        // Current tier/plan information
        public string CurrentTier { get; set; } = string.Empty;
        public decimal CurrentMonthlyCost { get; set; }
        public string NextTierUp { get; set; } = string.Empty;
        public decimal NextTierUpCost { get; set; }
        public string NextTierDown { get; set; } = string.Empty;
        public decimal NextTierDownCost { get; set; }
        
        // Usage warnings
        public List<string> Warnings { get; set; } = new();
        public bool IsApproachingLimit { get; set; }
        public bool HasExceededBudget { get; set; }
        
        // Auto-management settings
        public AutoManagementSettings AutoSettings { get; set; } = new();
    }

    /// <summary>
    /// Available actions for data source management
    /// </summary>
    public class DataSourceAction
    {
        public ActionType Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ButtonClass { get; set; } = string.Empty; // CSS class for styling
        public string Icon { get; set; } = string.Empty;
        public bool RequiresConfirmation { get; set; }
        public string ConfirmationMessage { get; set; } = string.Empty;
        public decimal EstimatedCostImpact { get; set; }
        public TimeSpan? EstimatedTimeImpact { get; set; }
        public bool IsDestructive { get; set; }
        public List<string> Prerequisites { get; set; } = new();
    }

    /// <summary>
    /// Auto-management settings for data sources
    /// </summary>
    public class AutoManagementSettings
    {
        public bool EnableAutoSuspend { get; set; }
        public decimal AutoSuspendThreshold { get; set; } // Budget percentage
        public bool EnableAutoUpgrade { get; set; }
        public decimal AutoUpgradeROIThreshold { get; set; }
        public bool EnableAutoDowngrade { get; set; }
        public decimal AutoDowngradeUtilizationThreshold { get; set; }
        public bool EnableRateLimitManagement { get; set; }
        public bool SendCostAlerts { get; set; }
        public List<string> AlertEmails { get; set; } = new();
    }

    /// <summary>
    /// Data source cost summary with ROI metrics
    /// </summary>
    public class DataSourceCostSummary
    {
        public string DataSource { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public decimal MonthlyRecurring { get; set; }
        public decimal VariableCosts { get; set; }
        public decimal AverageDailyCost { get; set; }
        public decimal ProjectedMonthlyCost { get; set; }
        
        // Usage metrics
        public long TotalAPICalls { get; set; }
        public decimal CostPerAPICall { get; set; }
        public decimal UtilizationRate { get; set; }
        
        // ROI metrics
        public decimal ROIPercentage { get; set; }
        public decimal RevenueAttribution { get; set; }
        public TimeSpan? PaybackPeriod { get; set; }
        
        // Efficiency indicators
        public EfficiencyRating EfficiencyRating { get; set; }
        public List<string> EfficiencyNotes { get; set; } = new();
        
        // Trend indicators
        public decimal CostTrend { get; set; } // Percentage change
        public decimal UsageTrend { get; set; }
        public decimal ROITrend { get; set; }
        
        // Status
        public DataSourceStatus Status { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Budget summary with alerts and projections
    /// </summary>
    public class BudgetSummary
    {
        public decimal TotalMonthlyBudget { get; set; }
        public decimal SpentThisMonth { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal ProjectedMonthlySpend { get; set; }
        public decimal BudgetUtilization { get; set; } // Percentage
        
        public bool IsOverBudget { get; set; }
        public bool IsApproachingBudget { get; set; }
        public decimal OverBudgetAmount { get; set; }
        
        // Per data source budget breakdown
        public Dictionary<string, DataSourceBudget> DataSourceBudgets { get; set; } = new();
        
        // Budget alerts
        public List<BudgetAlert> BudgetAlerts { get; set; } = new();
    }

    /// <summary>
    /// ROI analysis with interactive decision support
    /// </summary>
    public class ROIAnalysis
    {
        public string DataSource { get; set; } = string.Empty;
        public TimeSpan Period { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Financial metrics
        public decimal TotalCost { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal DirectRevenue { get; set; }
        public decimal IndirectRevenue { get; set; }
        public decimal NetProfit { get; set; }
        
        // ROI metrics
        public decimal ROIPercentage { get; set; }
        public TimeSpan? Payback { get; set; }
        public decimal NPV { get; set; }
        public decimal IRR { get; set; }
        
        // Efficiency metrics
        public decimal CostPerAPI { get; set; }
        public decimal CostPerSignal { get; set; }
        public decimal CostPerTrade { get; set; }
        public decimal SignalAccuracy { get; set; }
        public decimal SignalValue { get; set; }
        public decimal UtilizationRate { get; set; }
        
        // Risk metrics
        public decimal ValueAtRisk { get; set; }
        public decimal ConcentrationRisk { get; set; }
        
        // Qualitative assessment
        public QualitativeFactors QualitativeFactors { get; set; } = new();
        
        // Decision support
        public ROIRecommendation Recommendation { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public decimal ConfidenceScore { get; set; }
        
        // Scenario analysis
        public Dictionary<string, ROIScenario> Scenarios { get; set; } = new();
        
        // Sensitivity analysis
        public SensitivityAnalysis SensitivityAnalysis { get; set; } = new();
    }

    /// <summary>
    /// ROI scenario for what-if analysis
    /// </summary>
    public class ROIScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Probability { get; set; }
        public decimal AdjustedROI { get; set; }
        public decimal AdjustedRevenue { get; set; }
        public decimal AdjustedCost { get; set; }
        public List<string> Assumptions { get; set; } = new();
    }

    /// <summary>
    /// Sensitivity analysis for key variables
    /// </summary>
    public class SensitivityAnalysis
    {
        public Dictionary<string, decimal> CostSensitivity { get; set; } = new(); // Variable -> ROI impact
        public Dictionary<string, decimal> RevenueSensitivity { get; set; } = new();
        public Dictionary<string, decimal> UsageSensitivity { get; set; } = new();
        public string MostSensitiveVariable { get; set; } = string.Empty;
        public decimal BreakevenUsage { get; set; }
        public decimal BreakevenAccuracy { get; set; }
    }

    /// <summary>
    /// Qualitative factors for ROI assessment
    /// </summary>
    public class QualitativeFactors
    {
        public DataReliabilityRating Reliability { get; set; }
        public DataQualityRating Quality { get; set; }
        public string VendorStability { get; set; } = string.Empty;
        public string CompetitiveDifferentiation { get; set; } = string.Empty;
        public string StrategicValue { get; set; } = string.Empty;
        public List<string> RiskFactors { get; set; } = new();
        public List<string> OpportunityFactors { get; set; } = new();
        public decimal OverallQualitativeScore { get; set; }
    }

    /// <summary>
    /// Cost recommendation with actionable insights
    /// </summary>
    public class CostRecommendation
    {
        public RecommendationType Type { get; set; }
        public string DataSource { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RecommendationPriority Priority { get; set; }
        public decimal EstimatedSavings { get; set; }
        public decimal EstimatedRevenueImpact { get; set; }
        public TimeSpan ImplementationTime { get; set; }
        public List<string> ActionSteps { get; set; } = new();
        public List<string> Risks { get; set; } = new();
        public decimal ConfidenceLevel { get; set; }
        
        // Actionable buttons
        public List<RecommendationAction> Actions { get; set; } = new();
    }

    /// <summary>
    /// Actionable recommendation with button controls
    /// </summary>
    public class RecommendationAction
    {
        public string ActionId { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string ButtonClass { get; set; } = "btn-primary";
        public string Icon { get; set; } = string.Empty;
        public bool RequiresConfirmation { get; set; }
        public string ConfirmationText { get; set; } = string.Empty;
        public bool IsDestructive { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Cost alert with interactive responses
    /// </summary>
    public class CostAlert
    {
        public AlertType Type { get; set; }
        public string DataSource { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal ThresholdValue { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsAcknowledged { get; set; }
        public string AcknowledgedBy { get; set; } = string.Empty;
        
        // Interactive responses
        public List<AlertAction> SuggestedActions { get; set; } = new();
        public bool CanDismiss { get; set; }
        public bool CanSnooze { get; set; }
        public List<TimeSpan> SnoozeOptions { get; set; } = new();
    }

    /// <summary>
    /// Alert action for interactive response
    /// </summary>
    public class AlertAction
    {
        public string ActionId { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ButtonClass { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public decimal EstimatedImpact { get; set; }
        public bool IsQuickFix { get; set; }
        public bool RequiresApproval { get; set; }
    }

    // Supporting classes and enums
    public class DataSourceBudget
    {
        public decimal MonthlyLimit { get; set; }
        public decimal AlertThreshold { get; set; }
        public decimal HardLimit { get; set; }
        public decimal CurrentSpend { get; set; }
        public bool IsOverBudget => CurrentSpend > MonthlyLimit;
        public bool IsApproachingLimit => CurrentSpend > AlertThreshold;
    }

    public class BudgetAlert
    {
        public string DataSource { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ROISummary
    {
        public decimal ROIPercentage { get; set; }
        public ROIRecommendation Recommendation { get; set; }
        public string Summary { get; set; } = string.Empty;
        public EfficiencyRating Rating { get; set; }
    }

    public class UsageMetrics
    {
        public long TotalRequests { get; set; }
        public decimal AverageLatency { get; set; }
        public decimal ErrorRate { get; set; }
        public decimal UtilizationRate { get; set; }
        public Dictionary<string, long> RequestsByType { get; set; } = new();
    }

    public class EfficiencyMetrics
    {
        public decimal OverallEfficiencyScore { get; set; }
        public decimal CostEfficiencyScore { get; set; }
        public decimal RevenueEfficiencyScore { get; set; }
        public Dictionary<string, decimal> DataSourceEfficiency { get; set; } = new();
        public List<string> TopEfficiencyGains { get; set; } = new();
    }

    public class CostTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<string, decimal> DataSourceCosts { get; set; } = new();
    }

    // Configuration and settings
    public class CostTrackingConfiguration
    {
        public Dictionary<string, DataSourceBudget> DataSourceBudgets { get; set; } = new();
        public decimal MinimumROIThreshold { get; set; } = 0.15m; // 15%
        public decimal DiscountRate { get; set; } = 0.10m; // 10% for NPV calculations
        public TimeSpan AlertCheckInterval { get; set; } = TimeSpan.FromMinutes(15);
        public bool EnableAutoManagement { get; set; } = true;
        public string DefaultCurrency { get; set; } = "USD";
        public List<string> AlertRecipients { get; set; } = new();
    }

    // Enums
    public enum DataSourceStatus
    {
        Active,
        Suspended,
        Stopped,
        Limited,
        OverBudget,
        Error
    }

    public enum ActionType
    {
        Continue,
        Suspend,
        Stop,
        Restart,
        Upgrade,
        Downgrade,
        SetBudget,
        Configure,
        Optimize,
        Review
    }

    public enum AlertType
    {
        BudgetThreshold,
        LowROI,
        HighCost,
        LowUtilization,
        RateLimit,
        DataQuality,
        VendorIssue
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public enum EfficiencyRating
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Critical
    }

    public enum ROIRecommendation
    {
        StronglyRecommend,
        Recommend,
        Neutral,
        Caution,
        Discontinue
    }

    public enum RecommendationType
    {
        CostReduction,
        TierOptimization,
        UsageOptimization,
        ROIImprovement,
        RiskMitigation,
        PerformanceImprovement
    }

    public enum RecommendationPriority
    {
        Critical,
        High,
        Medium,
        Low
    }

    public enum DataReliabilityRating
    {
        Excellent = 5,
        Good = 4,
        Fair = 3,
        Poor = 2,
        Unacceptable = 1
    }

    public enum DataQualityRating
    {
        Excellent = 5,
        Good = 4,
        Fair = 3,
        Poor = 2,
        Unacceptable = 1
    }

    // Additional supporting classes
    public class UsageEvent
    {
        public UsageType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public long DataVolume { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class CostEvent
    {
        public DateTime Timestamp { get; set; }
        public decimal Amount { get; set; }
        public CostType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class DataSourceUsage
    {
        public string DataSource { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LastAPICall { get; set; }
        public long TotalAPICalls { get; set; }
        public long SignalsGenerated { get; set; }
        public long TradesInfluenced { get; set; }
        public long DataRefreshes { get; set; }
        public long DataVolume { get; set; }
    }

    public class RevenueAttribution
    {
        public string DataSource { get; set; } = string.Empty;
        public decimal DirectRevenue { get; set; }
        public decimal IndirectRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public List<decimal> CashFlows { get; set; } = new();
    }

    public class DataSourcePricing
    {
        public PricingModel PricingModel { get; set; }
        public decimal MonthlyFee { get; set; }
        public decimal CostPerUnit { get; set; }
        public string Unit { get; set; } = string.Empty;
        public PricingTier FreeTier { get; set; } = new();
        public PricingTier[] PaidTiers { get; set; } = Array.Empty<PricingTier>();
        public long RateLimit { get; set; } // Per minute
        public long IncludedUnits { get; set; }
    }

    public class PricingTier
    {
        public long Limit { get; set; }
        public decimal Cost { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public enum UsageType
    {
        SignalGeneration,
        TradeExecution,
        DataRefresh,
        Analysis,
        Monitoring
    }

    public enum CostType
    {
        Subscription,
        Usage,
        Overage,
        Setup,
        Support
    }

    public enum PricingModel
    {
        Free,
        Subscription,
        PayPerUse,
        Tiered,
        Hybrid
    }
}

namespace TradingPlatform.CostManagement.Interfaces
{
    /// <summary>
    /// Interface for data source cost tracking and ROI analysis
    /// </summary>
    public interface IDataSourceCostTracker
    {
        Task<CostDashboard> GetCostDashboardAsync(TimeSpan period);
        Task<ROIAnalysis> CalculateROIAsync(string dataSource, TimeSpan period);
        Task RecordUsageAsync(string dataSource, UsageEvent usageEvent);
        Task RecordCostEventAsync(string dataSource, CostEvent costEvent);
        Task<List<CostAlert>> GetActiveAlertsAsync();
        Task<bool> ExecuteDataSourceActionAsync(string dataSource, ActionType action, Dictionary<string, object>? parameters = null);
        Task<List<ROIScenario>> GenerateROIScenariosAsync(string dataSource);
        Task<SensitivityAnalysis> PerformSensitivityAnalysisAsync(string dataSource);
    }
}