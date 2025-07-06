// TradingPlatform.FinancialCalculations.Models.FinancialModels
// Core model classes for financial calculations with GPU acceleration support

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TradingPlatform.FinancialCalculations.Models;

/// <summary>
/// Base class for all financial calculation results
/// </summary>
public abstract class FinancialCalculationResult
{
    public string CalculationId { get; set; } = Guid.NewGuid().ToString();
    public string CalculationType { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string ServiceName { get; set; } = string.Empty;
    public bool UsedGpuAcceleration { get; set; }
    public double CalculationTimeMs { get; set; }
    public int DecimalPrecision { get; set; } = 4;
    public string ComplianceHash { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Portfolio calculation result
/// </summary>
public class PortfolioCalculationResult : FinancialCalculationResult
{
    public decimal TotalValue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal DayPnL { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal CashBalance { get; set; }
    public List<PositionResult> Positions { get; set; } = new();
    public RiskMetrics RiskMetrics { get; set; } = new();
}

/// <summary>
/// Individual position result
/// </summary>
public class PositionResult
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal UnrealizedPnLPercent { get; set; }
    public decimal DayPnL { get; set; }
    public decimal DayPnLPercent { get; set; }
    public decimal PositionWeight { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Risk metrics calculation result
/// </summary>
public class RiskMetrics
{
    public decimal PortfolioValue { get; set; }
    public decimal PortfolioVolatility { get; set; }
    public decimal PortfolioBeta { get; set; }
    public decimal Sharpe { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal VaR95 { get; set; }
    public decimal VaR99 { get; set; }
    public decimal ExpectedShortfall { get; set; }
    public decimal ConcentrationRisk { get; set; }
    public decimal LeverageRatio { get; set; }
    public Dictionary<string, decimal> SectorExposure { get; set; } = new();
    public Dictionary<string, decimal> CountryExposure { get; set; } = new();
}

/// <summary>
/// Technical indicator calculation result
/// </summary>
public class TechnicalIndicatorResult : FinancialCalculationResult
{
    public string Symbol { get; set; } = string.Empty;
    public string IndicatorType { get; set; } = string.Empty;
    public int Period { get; set; }
    public List<DataPoint> Values { get; set; } = new();
    public Dictionary<string, decimal> Parameters { get; set; } = new();
}

/// <summary>
/// Data point for time series calculations
/// </summary>
public class DataPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
    public decimal Volume { get; set; }
    public Dictionary<string, decimal> AdditionalData { get; set; } = new();
}

/// <summary>
/// Option pricing calculation result
/// </summary>
public class OptionPricingResult : FinancialCalculationResult
{
    public string Symbol { get; set; } = string.Empty;
    public OptionType OptionType { get; set; }
    public decimal Strike { get; set; }
    public decimal SpotPrice { get; set; }
    public decimal TimeToExpiry { get; set; }
    public decimal RiskFreeRate { get; set; }
    public decimal Volatility { get; set; }
    public decimal DividendYield { get; set; }
    
    // Option Greeks
    public decimal TheoreticalPrice { get; set; }
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
    public decimal Rho { get; set; }
    public decimal ImpliedVolatility { get; set; }
    
    // Monte Carlo specific
    public int MonteCarloSimulations { get; set; }
    public decimal StandardError { get; set; }
    public decimal ConfidenceInterval95Low { get; set; }
    public decimal ConfidenceInterval95High { get; set; }
}

/// <summary>
/// Fixed income calculation result
/// </summary>
public class FixedIncomeResult : FinancialCalculationResult
{
    public string SecurityId { get; set; } = string.Empty;
    public decimal FaceValue { get; set; }
    public decimal CouponRate { get; set; }
    public decimal YieldToMaturity { get; set; }
    public decimal Duration { get; set; }
    public decimal ModifiedDuration { get; set; }
    public decimal Convexity { get; set; }
    public decimal PresentValue { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal CleanPrice { get; set; }
    public decimal DirtyPrice { get; set; }
    public DateTime MaturityDate { get; set; }
    public List<CashFlow> CashFlows { get; set; } = new();
}

/// <summary>
/// Cash flow for fixed income securities
/// </summary>
public class CashFlow
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountedValue { get; set; }
    public CashFlowType Type { get; set; }
}

/// <summary>
/// Value at Risk calculation result
/// </summary>
public class VaRResult : FinancialCalculationResult
{
    public VaRMethod Method { get; set; }
    public decimal ConfidenceLevel { get; set; }
    public int HoldingPeriod { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal VaRAmount { get; set; }
    public decimal VaRPercent { get; set; }
    public decimal ExpectedShortfall { get; set; }
    public List<decimal> SimulatedReturns { get; set; } = new();
    public Dictionary<string, decimal> ComponentVaR { get; set; } = new();
}

/// <summary>
/// Performance attribution result
/// </summary>
public class PerformanceAttributionResult : FinancialCalculationResult
{
    public string PortfolioName { get; set; } = string.Empty;
    public string BenchmarkName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PortfolioReturn { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal ActiveReturn { get; set; }
    public decimal TrackingError { get; set; }
    public decimal InformationRatio { get; set; }
    public List<SectorAttribution> SectorAttributions { get; set; } = new();
    public List<SecurityAttribution> SecurityAttributions { get; set; } = new();
}

/// <summary>
/// Sector attribution breakdown
/// </summary>
public class SectorAttribution
{
    public string SectorName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; }
    public decimal BenchmarkWeight { get; set; }
    public decimal PortfolioReturn { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal AllocationEffect { get; set; }
    public decimal SelectionEffect { get; set; }
    public decimal InteractionEffect { get; set; }
    public decimal TotalEffect { get; set; }
}

/// <summary>
/// Security attribution breakdown
/// </summary>
public class SecurityAttribution
{
    public string Symbol { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; }
    public decimal BenchmarkWeight { get; set; }
    public decimal SecurityReturn { get; set; }
    public decimal Contribution { get; set; }
}

/// <summary>
/// Calculation result for caching
/// </summary>
public class CalculationResult
{
    public string Key { get; set; } = string.Empty;
    public object Value { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public TimeSpan? CacheExpiry { get; set; }
}

/// <summary>
/// Audit trail entry for compliance
/// </summary>
public class CalculationAuditEntry
{
    public string Id { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public string? Result { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public string? Error { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ComplianceHash { get; set; } = string.Empty;
    public bool UsedGpuAcceleration { get; set; }
    public string ExecutionMethod { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Performance statistics for calculation operations
/// </summary>
public class CalculationPerformanceStats
{
    public string OperationName { get; set; } = string.Empty;
    public long TotalCalculations { get; set; }
    public double AverageLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public long GpuUsageCount { get; set; }
    public long CpuUsageCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Market data input for calculations
/// </summary>
public class MarketDataInput
{
    public string Symbol { get; set; } = string.Empty;
    public List<PriceData> PriceHistory { get; set; } = new();
    public decimal CurrentPrice { get; set; }
    public decimal Volume { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, decimal> AdditionalData { get; set; } = new();
}

/// <summary>
/// Price data for historical analysis
/// </summary>
public class PriceData
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal AdjustedClose { get; set; }
}

/// <summary>
/// Multi-currency calculation support
/// </summary>
public class CurrencyConversionResult
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public DateTime ConversionDate { get; set; }
    public string DataSource { get; set; } = string.Empty;
}

#region Enums

/// <summary>
/// Option type enumeration
/// </summary>
public enum OptionType
{
    Call,
    Put
}

/// <summary>
/// Cash flow type enumeration
/// </summary>
public enum CashFlowType
{
    Coupon,
    Principal,
    Dividend,
    Interest
}

/// <summary>
/// VaR calculation method
/// </summary>
public enum VaRMethod
{
    HistoricalSimulation,
    ParametricNormal,
    MonteCarlo,
    CornishFisher
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Calculation status enumeration
/// </summary>
public enum CalculationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Rounding mode for regulatory compliance
/// </summary>
public enum RegulatoryRoundingMode
{
    BankersRounding,
    HalfUp,
    HalfDown,
    Truncate,
    RoundUp,
    RoundDown
}

/// <summary>
/// Result from decimal mathematics calculations
/// </summary>
public class DecimalMathResult : FinancialCalculationResult
{
    public decimal Value { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public int InputCount { get; set; } = 1;
    public RegulatoryRoundingMode RoundingMode { get; set; } = RegulatoryRoundingMode.BankersRounding;
}

/// <summary>
/// Result from batch decimal mathematics calculations
/// </summary>
public class DecimalMathBatchResult : FinancialCalculationResult
{
    public decimal[] Values { get; set; } = Array.Empty<decimal>();
    public int Count { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public RegulatoryRoundingMode RoundingMode { get; set; } = RegulatoryRoundingMode.BankersRounding;
    public TimeSpan GpuKernelTime { get; set; }
    public TimeSpan DataTransferTime { get; set; }
}

/// <summary>
/// Comprehensive risk calculation result
/// </summary>
public class RiskCalculationResult : FinancialCalculationResult
{
    public decimal VaR95 { get; set; }
    public decimal VaR99 { get; set; }
    public decimal Volatility { get; set; }
    public decimal ExpectedShortfall { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal ConcentrationRisk { get; set; }
    public decimal LeverageRatio { get; set; }
    public decimal ConfidenceLevel { get; set; }
    public int SampleSize { get; set; }
    public string CalculationMethod { get; set; } = string.Empty;
    public Dictionary<string, decimal> AdditionalMetrics { get; set; } = new();
}

#endregion