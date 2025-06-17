using System;
using System.Collections.Generic;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Company overview data from AlphaVantage API
    /// </summary>
    public class CompanyOverview
    {
        public string Symbol { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CIK { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string FiscalYearEnd { get; set; } = string.Empty;
        public string LatestQuarter { get; set; } = string.Empty;
        public decimal MarketCapitalization { get; set; }
        public decimal EBITDA { get; set; }
        public decimal PERatio { get; set; }
        public decimal PEGRatio { get; set; }
        public decimal BookValue { get; set; }
        public decimal DividendPerShare { get; set; }
        public decimal DividendYield { get; set; }
        public decimal EPS { get; set; }
        public decimal RevenuePerShareTTM { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal OperatingMarginTTM { get; set; }
        public decimal ReturnOnAssetsTTM { get; set; }
        public decimal ReturnOnEquityTTM { get; set; }
        public decimal RevenueTTM { get; set; }
        public decimal GrossProfitTTM { get; set; }
        public decimal DilutedEPSTTM { get; set; }
        public decimal QuarterlyEarningsGrowthYOY { get; set; }
        public decimal QuarterlyRevenueGrowthYOY { get; set; }
        public decimal AnalystTargetPrice { get; set; }
        public decimal TrailingPE { get; set; }
        public decimal ForwardPE { get; set; }
        public decimal PriceToSalesRatioTTM { get; set; }
        public decimal PriceToBookRatio { get; set; }
        public decimal EVToRevenue { get; set; }
        public decimal EVToEBITDA { get; set; }
        public decimal Beta { get; set; }
        public decimal Week52High { get; set; }
        public decimal Week52Low { get; set; }
        public decimal Day50MovingAverage { get; set; }
        public decimal Day200MovingAverage { get; set; }
        public long SharesOutstanding { get; set; }
        public DateTime DividendDate { get; set; }
        public DateTime ExDividendDate { get; set; }
    }

    /// <summary>
    /// Earnings data from AlphaVantage API
    /// </summary>
    public class EarningsData
    {
        public string Symbol { get; set; } = string.Empty;
        public List<AnnualEarnings> AnnualEarnings { get; set; } = new();
        public List<QuarterlyEarnings> QuarterlyEarnings { get; set; } = new();
    }

    public class AnnualEarnings
    {
        public int FiscalDateEnding { get; set; }
        public decimal ReportedEPS { get; set; }
    }

    public class QuarterlyEarnings
    {
        public string FiscalDateEnding { get; set; } = string.Empty;
        public DateTime ReportedDate { get; set; }
        public decimal ReportedEPS { get; set; }
        public decimal EstimatedEPS { get; set; }
        public decimal Surprise { get; set; }
        public decimal SurprisePercentage { get; set; }
    }

    /// <summary>
    /// Income statement data from AlphaVantage API
    /// </summary>
    public class IncomeStatement
    {
        public string Symbol { get; set; } = string.Empty;
        public List<AnnualReport> AnnualReports { get; set; } = new();
        public List<QuarterlyReport> QuarterlyReports { get; set; } = new();
    }

    public class AnnualReport
    {
        public string FiscalDateEnding { get; set; } = string.Empty;
        public DateTime ReportedCurrency { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CostOfRevenue { get; set; }
        public decimal CostofGoodsAndServicesSold { get; set; }
        public decimal OperatingIncome { get; set; }
        public decimal SellingGeneralAndAdministrative { get; set; }
        public decimal ResearchAndDevelopment { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal InvestmentIncomeNet { get; set; }
        public decimal NetInterestIncome { get; set; }
        public decimal InterestIncome { get; set; }
        public decimal InterestExpense { get; set; }
        public decimal NonInterestIncome { get; set; }
        public decimal OtherNonOperatingIncome { get; set; }
        public decimal Depreciation { get; set; }
        public decimal DepreciationAndAmortization { get; set; }
        public decimal IncomeBeforeTax { get; set; }
        public decimal IncomeTaxExpense { get; set; }
        public decimal InterestAndDebtExpense { get; set; }
        public decimal NetIncomeFromContinuingOperations { get; set; }
        public decimal ComprehensiveIncomeNetOfTax { get; set; }
        public decimal EBIT { get; set; }
        public decimal EBITDA { get; set; }
        public decimal NetIncome { get; set; }
    }

    public class QuarterlyReport : AnnualReport
    {
        // Inherits all properties from AnnualReport
    }

    /// <summary>
    /// Symbol search result from AlphaVantage API
    /// </summary>
    public class SymbolSearchResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string MarketOpen { get; set; } = string.Empty;
        public string MarketClose { get; set; } = string.Empty;
        public string Timezone { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal MatchScore { get; set; }
    }

    /// <summary>
    /// Trading suitability assessment for Finnhub service
    /// </summary>
    public class TradingSuitabilityAssessment
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal LiquidityScore { get; set; }
        public decimal VolatilityScore { get; set; }
        public decimal TradingVolumeScore { get; set; }
        public decimal SpreadScore { get; set; }
        public decimal OverallSuitabilityScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string RecommendedPositionSize { get; set; } = string.Empty;
        public List<string> TradingWarnings { get; set; } = new();
        public DateTime AssessmentDate { get; set; }
        public string MarketCondition { get; set; } = string.Empty;
    }
}