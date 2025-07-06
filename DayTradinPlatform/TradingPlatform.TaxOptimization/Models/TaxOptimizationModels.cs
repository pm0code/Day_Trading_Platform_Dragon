using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.TaxOptimization.Models;

/// <summary>
/// Tax lot tracking for precise cost basis calculation and tax optimization
/// </summary>
public class TaxLot
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostBasis { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public string LotId { get; set; } = string.Empty;
    public TaxLotStatus Status { get; set; } = TaxLotStatus.Open;
    public string? Notes { get; set; }
    
    // Tax-specific properties
    public bool IsShortTerm => (DateTime.UtcNow - AcquisitionDate).TotalDays <= 365;
    public bool IsLongTerm => !IsShortTerm;
    public decimal UnrealizedGainLoss { get; set; }
    public decimal? RealizedGainLoss { get; set; }
    public DateTime? DispositionDate { get; set; }
}

/// <summary>
/// Tax lot status tracking
/// </summary>
public enum TaxLotStatus
{
    Open,
    PartiallyRealized,
    FullyRealized,
    WashSaleDeferred
}

/// <summary>
/// Cost basis calculation methods for tax optimization
/// </summary>
public enum CostBasisMethod
{
    FIFO,           // First In, First Out
    LIFO,           // Last In, First Out
    SpecificID,     // Specific Identification (optimal for tax loss harvesting)
    AverageCost,    // Average Cost (mutual funds)
    HighestCost,    // Highest Cost First (tax optimization)
    LowestCost      // Lowest Cost First (tax deferral)
}

/// <summary>
/// Tax optimization strategy configuration
/// </summary>
public class TaxOptimizationStrategy
{
    public string StrategyName { get; set; } = string.Empty;
    public CostBasisMethod PreferredCostBasisMethod { get; set; } = CostBasisMethod.SpecificID;
    public bool EnableTaxLossHarvesting { get; set; } = true;
    public bool EnableWashSaleAvoidance { get; set; } = true;
    public bool EnableMarkToMarketElection { get; set; } = false;
    public bool EnableSection1256Treatment { get; set; } = false;
    public decimal MinTaxLossThreshold { get; set; } = 100m; // Minimum loss for harvesting
    public decimal MaxDailyHarvestingAmount { get; set; } = 10000m;
    public bool EnableShortTermToLongTermConversion { get; set; } = true;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Tax loss harvesting opportunity
/// </summary>
public class TaxLossHarvestingOpportunity
{
    public string Symbol { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public decimal UnrealizedLoss { get; set; }
    public decimal Quantity { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public bool IsShortTerm { get; set; }
    public decimal TaxSavings { get; set; }
    public HarvestingPriority Priority { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime RecommendationExpiry { get; set; }
}

/// <summary>
/// Tax loss harvesting priority levels
/// </summary>
public enum HarvestingPriority
{
    Critical,    // Large losses, year-end approaching
    High,        // Significant losses, good tax savings
    Medium,      // Moderate losses, decent savings
    Low,         // Small losses, minimal savings
    Deferred     // Should wait for better timing
}

/// <summary>
/// Wash sale rule violation detection and avoidance
/// </summary>
public class WashSaleAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public DateTime? RepurchaseDate { get; set; }
    public bool IsWashSaleViolation { get; set; }
    public decimal AffectedLoss { get; set; }
    public decimal DeferredLoss { get; set; }
    public int DaysToSafeRepurchase { get; set; }
    public List<string> RecommendedAlternatives { get; set; } = new();
    public string ViolationDetails { get; set; } = string.Empty;
}

/// <summary>
/// Section 1256 contract analysis for futures and options
/// </summary>
public class Section1256Analysis
{
    public string Symbol { get; set; } = string.Empty;
    public bool IsSection1256Contract { get; set; }
    public decimal Mark60Percent { get; set; } // 60% long-term capital gains
    public decimal Mark40Percent { get; set; } // 40% short-term capital gains
    public decimal TotalMarkToMarketGainLoss { get; set; }
    public decimal TaxSavingsVsOrdinary { get; set; }
    public bool ShouldElectMarkToMarket { get; set; }
    public string RecommendedTreatment { get; set; } = string.Empty;
}

/// <summary>
/// Mark-to-Market election analysis for traders
/// </summary>
public class MarkToMarketElectionAnalysis
{
    public decimal ProjectedTotalTradingIncome { get; set; }
    public decimal ProjectedOrdinaryTaxRate { get; set; }
    public decimal ProjectedCapitalGainsTaxRate { get; set; }
    public decimal TaxSavingsWithElection { get; set; }
    public decimal AdditionalDeductionsAvailable { get; set; }
    public bool RecommendElection { get; set; }
    public List<string> RequiredConditions { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public DateTime OptimalElectionDate { get; set; }
}

/// <summary>
/// Capital gains optimization analysis
/// </summary>
public class CapitalGainsOptimization
{
    public decimal ShortTermGains { get; set; }
    public decimal ShortTermLosses { get; set; }
    public decimal LongTermGains { get; set; }
    public decimal LongTermLosses { get; set; }
    public decimal NetShortTerm { get; set; }
    public decimal NetLongTerm { get; set; }
    public decimal OverallNetGainLoss { get; set; }
    public decimal EstimatedTaxLiability { get; set; }
    public List<OptimizationRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Tax optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string Action { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal EstimatedTaxSavings { get; set; }
    public RecommendationPriority Priority { get; set; }
    public DateTime Deadline { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public List<string> Risks { get; set; } = new();
    public List<string> Prerequisites { get; set; } = new();
}

/// <summary>
/// Recommendation priority levels
/// </summary>
public enum RecommendationPriority
{
    Urgent,      // Must act immediately
    High,        // Should act soon
    Medium,      // Act when convenient
    Low,         // Consider if beneficial
    Monitor      // Watch for changes
}

/// <summary>
/// Tax report generation models
/// </summary>
public class TaxReport
{
    public int TaxYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public string TraderId { get; set; } = string.Empty;
    public TaxYearSummary Summary { get; set; } = new();
    public List<RealizedGainLoss> RealizedTransactions { get; set; } = new();
    public List<TaxLossCarryforward> Carryforwards { get; set; } = new();
    public List<WashSaleAdjustment> WashSaleAdjustments { get; set; } = new();
    public Dictionary<string, decimal> Form8949Totals { get; set; } = new();
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Tax year summary
/// </summary>
public class TaxYearSummary
{
    public decimal TotalShortTermGains { get; set; }
    public decimal TotalShortTermLosses { get; set; }
    public decimal TotalLongTermGains { get; set; }
    public decimal TotalLongTermLosses { get; set; }
    public decimal NetShortTerm { get; set; }
    public decimal NetLongTerm { get; set; }
    public decimal OverallNetGainLoss { get; set; }
    public decimal EstimatedTaxOwed { get; set; }
    public decimal TaxLossesHarvested { get; set; }
    public decimal TaxSavingsRealized { get; set; }
    public int TotalTrades { get; set; }
    public Dictionary<string, decimal> MonthlyBreakdown { get; set; } = new();
}

/// <summary>
/// Realized gain/loss transaction
/// </summary>
public class RealizedGainLoss
{
    public string Symbol { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public DateTime AcquisitionDate { get; set; }
    public DateTime DispositionDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal CostBasis { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal GainLoss { get; set; }
    public bool IsShortTerm { get; set; }
    public bool IsWashSale { get; set; }
    public string TransactionId { get; set; } = string.Empty;
}

/// <summary>
/// Tax loss carryforward tracking
/// </summary>
public class TaxLossCarryforward
{
    public int OriginatingYear { get; set; }
    public decimal ShortTermLossCarryforward { get; set; }
    public decimal LongTermLossCarryforward { get; set; }
    public decimal TotalCarryforward { get; set; }
    public decimal UsedThisYear { get; set; }
    public decimal RemainingCarryforward { get; set; }
    public bool HasExpiration { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

/// <summary>
/// Wash sale adjustment tracking
/// </summary>
public class WashSaleAdjustment
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime LossDate { get; set; }
    public DateTime RepurchaseDate { get; set; }
    public decimal DeferredLoss { get; set; }
    public decimal AdjustedCostBasis { get; set; }
    public string AffectedLotId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Real-time tax optimization monitoring
/// </summary>
public class TaxOptimizationMetrics
{
    public DateTime LastUpdated { get; set; }
    public decimal YearToDateTaxSavings { get; set; }
    public decimal PotentialTaxSavings { get; set; }
    public int OpportunitiesIdentified { get; set; }
    public int ActionsRecommended { get; set; }
    public int ActionsCompleted { get; set; }
    public decimal EfficiencyRatio { get; set; }
    public List<string> ActiveAlerts { get; set; } = new();
    public Dictionary<string, decimal> MonthlyPerformance { get; set; } = new();
}

/// <summary>
/// Tax-efficient trading suggestions
/// </summary>
public class TaxEfficientTradingSuggestion
{
    public string Symbol { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // BUY, SELL, HOLD, HARVEST_LOSS
    public decimal Quantity { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public decimal TaxImpact { get; set; }
    public decimal EstimatedSavings { get; set; }
    public TradingWindow OptimalTiming { get; set; } = new();
    public List<string> Considerations { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
}

/// <summary>
/// Optimal trading window for tax efficiency
/// </summary>
public class TradingWindow
{
    public DateTime OptimalStartTime { get; set; }
    public DateTime OptimalEndTime { get; set; }
    public DateTime Deadline { get; set; }
    public string WindowType { get; set; } = string.Empty; // HARVEST, REALIZE, DEFER
    public List<string> Constraints { get; set; } = new();
}

/// <summary>
/// Tax configuration settings
/// </summary>
public class TaxConfiguration
{
    public decimal OrdinaryIncomeRate { get; set; } = 0.37m; // 37% top rate
    public decimal ShortTermCapitalGainsRate { get; set; } = 0.37m; // Same as ordinary
    public decimal LongTermCapitalGainsRate { get; set; } = 0.20m; // 20% top rate
    public decimal NetInvestmentIncomeRate { get; set; } = 0.038m; // 3.8% NIIT
    public decimal StateIncomeTaxRate { get; set; } = 0.0m; // State-specific
    public decimal MinimumLossHarvestAmount { get; set; } = 100m;
    public bool EnableAggressiveOptimization { get; set; } = true;
    public bool EnableAutomaticHarvesting { get; set; } = false;
    public int WashSaleAvoidanceDays { get; set; } = 31; // 30 days + 1 for safety
    public Dictionary<string, decimal> CustomRates { get; set; } = new();
}