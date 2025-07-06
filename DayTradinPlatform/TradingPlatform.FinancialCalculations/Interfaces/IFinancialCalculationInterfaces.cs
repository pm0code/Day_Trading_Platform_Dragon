// TradingPlatform.FinancialCalculations.Interfaces.IFinancialCalculationInterfaces
// Core interfaces for financial calculation services

using TradingPlatform.Core.Models;
using TradingPlatform.FinancialCalculations.Models;

namespace TradingPlatform.FinancialCalculations.Interfaces;

/// <summary>
/// Core interface for all financial calculation services
/// </summary>
public interface IFinancialCalculationService
{
    Task<TradingResult<T>> CalculateAsync<T>(string calculationType, object parameters, CancellationToken cancellationToken = default)
        where T : FinancialCalculationResult;
    
    Task<TradingResult<bool>> ValidateInputAsync(string calculationType, object parameters, CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<CalculationAuditEntry>>> GetAuditTrailAsync(string calculationId, CancellationToken cancellationToken = default);
    
    Task<TradingResult<Dictionary<string, CalculationPerformanceStats>>> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Portfolio calculation service interface
/// </summary>
public interface IPortfolioCalculationService : IFinancialCalculationService
{
    Task<TradingResult<PortfolioCalculationResult>> CalculatePortfolioMetricsAsync(
        List<PositionData> positions, 
        Dictionary<string, decimal> currentPrices,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<RiskMetrics>> CalculateRiskMetricsAsync(
        List<PositionData> positions,
        Dictionary<string, List<decimal>> priceHistory,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<VaRResult>> CalculateVaRAsync(
        List<PositionData> positions,
        VaRMethod method,
        double confidenceLevel,
        int historyDays,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<PerformanceAttributionResult>> CalculatePerformanceAttributionAsync(
        string portfolioId,
        string benchmarkId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Technical analysis calculation service interface
/// </summary>
public interface ITechnicalAnalysisService : IFinancialCalculationService
{
    Task<TradingResult<TechnicalIndicatorResult>> CalculateSimpleMovingAverageAsync(
        string symbol,
        List<decimal> prices,
        int period,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<TechnicalIndicatorResult>> CalculateExponentialMovingAverageAsync(
        string symbol,
        List<decimal> prices,
        int period,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<TechnicalIndicatorResult>> CalculateRelativeStrengthIndexAsync(
        string symbol,
        List<decimal> prices,
        int period,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<TechnicalIndicatorResult>> CalculateBollingerBandsAsync(
        string symbol,
        List<decimal> prices,
        int period,
        decimal standardDeviations,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<TechnicalIndicatorResult>> CalculateStochasticOscillatorAsync(
        string symbol,
        List<PriceData> priceData,
        int kPeriod,
        int dPeriod,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<TechnicalIndicatorResult>> CalculateMACDAsync(
        string symbol,
        List<decimal> prices,
        int fastPeriod,
        int slowPeriod,
        int signalPeriod,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Option pricing service interface
/// </summary>
public interface IOptionPricingService : IFinancialCalculationService
{
    Task<TradingResult<OptionPricingResult>> CalculateBlackScholesAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<OptionPricingResult>> CalculateMonteCarloAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int simulations,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<OptionPricingResult>> CalculateBinomialTreeAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int steps,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateImpliedVolatilityAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal marketPrice,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fixed income calculation service interface
/// </summary>
public interface IFixedIncomeService : IFinancialCalculationService
{
    Task<TradingResult<FixedIncomeResult>> CalculateBondMetricsAsync(
        string securityId,
        decimal faceValue,
        decimal couponRate,
        DateTime maturityDate,
        decimal yieldToMaturity,
        int couponFrequency,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateYieldToMaturityAsync(
        string securityId,
        decimal faceValue,
        decimal couponRate,
        DateTime maturityDate,
        decimal currentPrice,
        int couponFrequency,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateDurationAsync(
        string securityId,
        List<CashFlow> cashFlows,
        decimal yieldToMaturity,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateConvexityAsync(
        string securityId,
        List<CashFlow> cashFlows,
        decimal yieldToMaturity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Currency conversion service interface
/// </summary>
public interface ICurrencyConversionService : IFinancialCalculationService
{
    Task<TradingResult<CurrencyConversionResult>> ConvertCurrencyAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        DateTime? valueDate = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        DateTime? valueDate = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<Dictionary<string, decimal>>> GetCurrencyMatrixAsync(
        List<string> currencies,
        DateTime? valueDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance and auditing service interface
/// </summary>
public interface IComplianceAuditor
{
    Task<TradingResult<bool>> InitializeAsync(string serviceName, CancellationToken cancellationToken = default);
    
    Task<TradingResult<string>> StartCalculationAuditAsync(
        string calculationType,
        object parameters,
        string userId,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> CompleteCalculationAuditAsync(
        string auditId,
        object result,
        bool success,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> ValidateRegulatoryComplianceAsync(
        string calculationType,
        object parameters,
        object result,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<CalculationAuditEntry>>> GetAuditReportAsync(
        DateTime startDate,
        DateTime endDate,
        string? calculationType = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Performance monitoring service interface
/// </summary>
public interface IPerformanceMonitor
{
    Task<TradingResult<bool>> StartOperationAsync(string operationName, object? parameters = null);
    
    Task<TradingResult<bool>> CompleteOperationAsync(string operationId, bool success, double latencyMs);
    
    Task<TradingResult<CalculationPerformanceStats>> GetOperationStatsAsync(string operationName);
    
    Task<TradingResult<Dictionary<string, CalculationPerformanceStats>>> GetAllStatsAsync();
    
    Task<TradingResult<bool>> ResetStatsAsync();
}

/// <summary>
/// GPU calculation service interface
/// </summary>
public interface IGpuCalculationService
{
    Task<TradingResult<bool>> InitializeGpuAsync(CancellationToken cancellationToken = default);
    
    Task<TradingResult<T>> ExecuteGpuCalculationAsync<T>(
        string kernelName,
        object parameters,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> IsGpuAvailableAsync();
    
    Task<TradingResult<GpuInfo>> GetGpuInfoAsync();
}

/// <summary>
/// Validation service interface
/// </summary>
public interface ICalculationValidator
{
    Task<TradingResult<bool>> ValidateInputAsync<T>(T input, string calculationType) where T : class;
    
    Task<TradingResult<bool>> ValidateOutputAsync<T>(T output, string calculationType) where T : class;
    
    Task<TradingResult<bool>> ValidateDecimalPrecisionAsync(decimal value, int requiredPrecision);
    
    Task<TradingResult<bool>> ValidateDataQualityAsync(IEnumerable<decimal> data, string dataType);
    
    Task<TradingResult<List<string>>> GetValidationErrorsAsync();
}

/// <summary>
/// Calculation factory interface
/// </summary>
public interface ICalculationFactory
{
    Task<TradingResult<IFinancialCalculationService>> CreateServiceAsync(string serviceType);
    
    Task<TradingResult<T>> CreateCalculatorAsync<T>(string calculatorType) where T : class;
    
    Task<TradingResult<List<string>>> GetAvailableServiceTypesAsync();
}

/// <summary>
/// Cache service interface for calculation results
/// </summary>
public interface ICalculationCache
{
    Task<TradingResult<T?>> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    Task<TradingResult<bool>> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;
    
    Task<TradingResult<bool>> RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> ClearAsync(CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<string>>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for GPU-accelerated decimal mathematics service
/// Provides high-precision financial calculations with automatic GPU acceleration
/// </summary>
public interface IDecimalMathService : IDisposable
{
    /// <summary>
    /// Calculates square root with high precision
    /// </summary>
    Task<TradingResult<DecimalMathResult>> SqrtAsync(decimal value);
    
    /// <summary>
    /// Calculates power with decimal precision
    /// </summary>
    Task<TradingResult<DecimalMathResult>> PowAsync(decimal baseValue, decimal exponent);
    
    /// <summary>
    /// Calculates natural logarithm with decimal precision
    /// </summary>
    Task<TradingResult<DecimalMathResult>> LogAsync(decimal value);
    
    /// <summary>
    /// Batch calculation for large datasets with automatic GPU acceleration
    /// </summary>
    Task<TradingResult<DecimalMathBatchResult>> BatchCalculateAsync(
        decimal[] operand1,
        decimal[] operand2,
        MathOperation operation);
    
    /// <summary>
    /// Financial calculation with automatic precision and rounding
    /// </summary>
    Task<TradingResult<DecimalMathResult>> CalculateFinancialValueAsync(
        decimal principal,
        decimal rate,
        int periods,
        FinancialCalculationType calculationType);
    
    /// <summary>
    /// Portfolio calculations with GPU acceleration for large portfolios
    /// </summary>
    Task<TradingResult<DecimalMathBatchResult>> CalculatePortfolioValuesAsync(
        decimal[] quantities,
        decimal[] prices);
    
    /// <summary>
    /// Risk calculations with high precision requirements
    /// </summary>
    Task<TradingResult<RiskCalculationResult>> CalculateRiskMetricsAsync(
        decimal[] returns,
        decimal confidenceLevel = 0.95m);
}

/// <summary>
/// Mathematical operations supported by the decimal math service
/// </summary>
public enum MathOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    SquareRoot,
    Power,
    Logarithm
}

/// <summary>
/// Financial calculation types supported by the service
/// </summary>
public enum FinancialCalculationType
{
    CompoundInterest,
    PresentValue,
    FutureValue,
    Annuity,
    NetPresentValue,
    InternalRateOfReturn
}

/// <summary>
/// Supporting data structures
/// </summary>
public class PositionData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string Sector { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long TotalMemory { get; set; }
    public long FreeMemory { get; set; }
    public int ComputeUnits { get; set; }
    public bool IsAvailable { get; set; }
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}