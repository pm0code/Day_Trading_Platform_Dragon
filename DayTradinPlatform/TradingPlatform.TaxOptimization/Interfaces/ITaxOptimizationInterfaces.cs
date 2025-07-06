using TradingPlatform.TaxOptimization.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.TaxOptimization.Interfaces;

/// <summary>
/// Core tax optimization service interface
/// </summary>
public interface ITaxOptimizationService
{
    Task<TradingResult<TaxOptimizationMetrics>> GetOptimizationMetricsAsync();
    Task<TradingResult<List<OptimizationRecommendation>>> GetRecommendationsAsync();
    Task<TradingResult<bool>> ExecuteOptimizationAsync(string recommendationId);
    Task<TradingResult<TaxReport>> GenerateTaxReportAsync(int taxYear);
    Task<TradingResult<bool>> ConfigureStrategyAsync(TaxOptimizationStrategy strategy);
}

/// <summary>
/// Tax loss harvesting engine interface
/// </summary>
public interface ITaxLossHarvestingEngine
{
    Task<TradingResult<List<TaxLossHarvestingOpportunity>>> IdentifyHarvestingOpportunitiesAsync();
    Task<TradingResult<decimal>> EstimateTaxSavingsAsync(TaxLossHarvestingOpportunity opportunity);
    Task<TradingResult<bool>> ExecuteHarvestingAsync(string opportunityId);
    Task<TradingResult<HarvestingPriority>> DeterminePriorityAsync(TaxLossHarvestingOpportunity opportunity);
    Task<TradingResult<List<string>>> GetAlternativeInvestmentsAsync(string symbol);
}

/// <summary>
/// Cost basis optimization service interface
/// </summary>
public interface ICostBasisOptimizer
{
    Task<TradingResult<CostBasisMethod>> DetermineOptimalMethodAsync(string symbol, decimal quantity);
    Task<TradingResult<List<TaxLot>>> SelectOptimalLotsAsync(string symbol, decimal quantity, CostBasisMethod method);
    Task<TradingResult<decimal>> CalculateOptimalCostBasisAsync(List<TaxLot> lots);
    Task<TradingResult<bool>> UpdateCostBasisMethodAsync(string symbol, CostBasisMethod method);
}

/// <summary>
/// Wash sale rule compliance interface
/// </summary>
public interface IWashSaleDetector
{
    Task<TradingResult<WashSaleAnalysis>> AnalyzeTransactionAsync(string symbol, DateTime saleDate);
    Task<TradingResult<bool>> IsWashSaleViolationAsync(string symbol, DateTime saleDate, DateTime? repurchaseDate);
    Task<TradingResult<List<string>>> GetSafeAlternativesAsync(string symbol);
    Task<TradingResult<int>> CalculateSafeRepurchaseDaysAsync(string symbol, DateTime saleDate);
    Task<TradingResult<bool>> MonitorWashSaleRiskAsync();
}

/// <summary>
/// Section 1256 contract management interface
/// </summary>
public interface ISection1256Manager
{
    Task<TradingResult<Section1256Analysis>> AnalyzeContractAsync(string symbol);
    Task<TradingResult<bool>> IsSection1256ContractAsync(string symbol);
    Task<TradingResult<decimal>> CalculateMarkToMarketGainLossAsync(string symbol);
    Task<TradingResult<bool>> ShouldElectMarkToMarketAsync(string symbol);
    Task<TradingResult<decimal>> EstimateTaxSavingsAsync(Section1256Analysis analysis);
}

/// <summary>
/// Mark-to-Market election advisor interface
/// </summary>
public interface IMarkToMarketAdvisor
{
    Task<TradingResult<MarkToMarketElectionAnalysis>> AnalyzeElectionBenefitsAsync();
    Task<TradingResult<bool>> ShouldElectMarkToMarketAsync();
    Task<TradingResult<List<string>>> GetElectionRequirementsAsync();
    Task<TradingResult<DateTime>> DetermineOptimalElectionDateAsync();
    Task<TradingResult<decimal>> EstimateTaxSavingsAsync();
}

/// <summary>
/// Capital gains optimization interface
/// </summary>
public interface ICapitalGainsOptimizer
{
    Task<TradingResult<CapitalGainsOptimization>> OptimizeCapitalGainsAsync();
    Task<TradingResult<List<OptimizationRecommendation>>> GetShortTermOptimizationsAsync();
    Task<TradingResult<List<OptimizationRecommendation>>> GetLongTermOptimizationsAsync();
    Task<TradingResult<decimal>> CalculateOptimalTimingAsync(string symbol, decimal quantity);
    Task<TradingResult<bool>> ShouldDeferGainsAsync(string symbol);
}

/// <summary>
/// Tax lot management interface
/// </summary>
public interface ITaxLotManager
{
    Task<TradingResult<List<TaxLot>>> GetTaxLotsAsync(string symbol);
    Task<TradingResult<TaxLot>> CreateTaxLotAsync(string symbol, decimal quantity, decimal costBasis, DateTime acquisitionDate);
    Task<TradingResult<bool>> UpdateTaxLotAsync(TaxLot taxLot);
    Task<TradingResult<bool>> RealizeTaxLotAsync(string lotId, decimal quantity, decimal salePrice, DateTime saleDate);
    Task<TradingResult<decimal>> CalculateUnrealizedGainLossAsync(string lotId, decimal currentPrice);
}

/// <summary>
/// Tax reporting service interface
/// </summary>
public interface ITaxReportingService
{
    Task<TradingResult<TaxReport>> GenerateAnnualReportAsync(int taxYear);
    Task<TradingResult<List<RealizedGainLoss>>> GetRealizedTransactionsAsync(int taxYear);
    Task<TradingResult<TaxYearSummary>> GetTaxYearSummaryAsync(int taxYear);
    Task<TradingResult<byte[]>> ExportForm8949DataAsync(int taxYear);
    Task<TradingResult<byte[]>> ExportScheduleDDataAsync(int taxYear);
}

/// <summary>
/// Real-time tax monitoring interface
/// </summary>
public interface ITaxMonitoringService
{
    Task<TradingResult<TaxOptimizationMetrics>> GetRealTimeMetricsAsync();
    Task<TradingResult<List<string>>> GetActiveAlertsAsync();
    Task<TradingResult<bool>> SetupMonitoringAsync(List<string> symbols);
    Task<TradingResult<bool>> TriggerAlertAsync(string alertType, string message);
    Task<TradingResult<List<TaxEfficientTradingSuggestion>>> GetTradingSuggestionsAsync();
}

/// <summary>
/// Tax strategy advisor interface
/// </summary>
public interface ITaxStrategyAdvisor
{
    Task<TradingResult<TaxOptimizationStrategy>> RecommendStrategyAsync();
    Task<TradingResult<List<TaxEfficientTradingSuggestion>>> GetTradingSuggestionsAsync();
    Task<TradingResult<decimal>> EstimateYearEndTaxLiabilityAsync();
    Task<TradingResult<List<OptimizationRecommendation>>> GetYearEndOptimizationsAsync();
    Task<TradingResult<bool>> ShouldAccelerateGainsAsync();
    Task<TradingResult<bool>> ShouldDeferGainsAsync();
}

/// <summary>
/// Tax calculation engine interface
/// </summary>
public interface ITaxCalculationEngine
{
    Task<TradingResult<decimal>> CalculateShortTermTaxAsync(decimal gains);
    Task<TradingResult<decimal>> CalculateLongTermTaxAsync(decimal gains);
    Task<TradingResult<decimal>> CalculateNetInvestmentIncomeTaxAsync(decimal gains);
    Task<TradingResult<decimal>> CalculateTotalTaxLiabilityAsync(CapitalGainsOptimization optimization);
    Task<TradingResult<decimal>> EstimateTaxSavingsAsync(OptimizationRecommendation recommendation);
}