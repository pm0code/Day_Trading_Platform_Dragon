// File: TradingPlatform.Core\Models\CompanyFinancials.cs

using System;
using System.Collections.Generic;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Represents company financial metrics for fundamental analysis in day trading.
    /// Focuses on key metrics that impact short-term price movements and volatility.
    /// </summary>
    public class CompanyFinancials
    {
        /// <summary>
        /// Stock ticker symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Reporting period for these financials.
        /// </summary>
        public DateTime ReportingPeriod { get; set; }

        /// <summary>
        /// Period type (Q1, Q2, Q3, Q4, Annual).
        /// </summary>
        public string PeriodType { get; set; } = string.Empty;

        // ========== INCOME STATEMENT METRICS ==========

        /// <summary>
        /// Total revenue for the period.
        /// </summary>
        public decimal Revenue { get; set; }

        /// <summary>
        /// Net income for the period.
        /// </summary>
        public decimal NetIncome { get; set; }

        /// <summary>
        /// Earnings before interest, taxes, depreciation, and amortization.
        /// </summary>
        public decimal EBITDA { get; set; }

        /// <summary>
        /// Earnings per share (basic).
        /// </summary>
        public decimal EPS { get; set; }

        /// <summary>
        /// Diluted earnings per share.
        /// </summary>
        public decimal EPSDiluted { get; set; }

        // ========== BALANCE SHEET METRICS ==========

        /// <summary>
        /// Total assets.
        /// </summary>
        public decimal TotalAssets { get; set; }

        /// <summary>
        /// Total liabilities.
        /// </summary>
        public decimal TotalLiabilities { get; set; }

        /// <summary>
        /// Total shareholders' equity.
        /// </summary>
        public decimal ShareholdersEquity { get; set; }

        /// <summary>
        /// Total debt (short-term + long-term).
        /// </summary>
        public decimal TotalDebt { get; set; }

        /// <summary>
        /// Cash and cash equivalents.
        /// </summary>
        public decimal Cash { get; set; }

        // ========== CASH FLOW METRICS ==========

        /// <summary>
        /// Operating cash flow.
        /// </summary>
        public decimal OperatingCashFlow { get; set; }

        /// <summary>
        /// Free cash flow (Operating CF - Capital Expenditures).
        /// </summary>
        public decimal FreeCashFlow { get; set; }

        /// <summary>
        /// Capital expenditures.
        /// </summary>
        public decimal CapitalExpenditures { get; set; }

        // ========== CALCULATED RATIOS ==========

        /// <summary>
        /// Return on equity (Net Income / Shareholders Equity).
        /// </summary>
        public decimal ROE => ShareholdersEquity != 0 ? (NetIncome / ShareholdersEquity) * 100 : 0;

        /// <summary>
        /// Return on assets (Net Income / Total Assets).
        /// </summary>
        public decimal ROA => TotalAssets != 0 ? (NetIncome / TotalAssets) * 100 : 0;

        /// <summary>
        /// Debt-to-equity ratio (Total Debt / Shareholders Equity).
        /// </summary>
        public decimal DebtToEquity => ShareholdersEquity != 0 ? TotalDebt / ShareholdersEquity : 0;

        /// <summary>
        /// Current ratio proxy (using cash vs total debt).
        /// </summary>
        public decimal LiquidityRatio => TotalDebt != 0 ? Cash / TotalDebt : 0;

        /// <summary>
        /// Revenue growth rate (requires previous period data).
        /// </summary>
        public decimal RevenueGrowthRate { get; set; }

        /// <summary>
        /// EPS growth rate (requires previous period data).
        /// </summary>
        public decimal EPSGrowthRate { get; set; }

        // ========== DAY TRADING RELEVANCE METHODS ==========

        /// <summary>
        /// Determines if financial health supports day trading volatility.
        /// Strong financials can indicate sustainable price movements.
        /// </summary>
        /// <returns>True if financials support active trading</returns>
        public bool SupportsActiveTradingVolatility()
        {
            // Strong revenue base for sustainability
            if (Revenue < 100_000_000m) return false; // $100M minimum

            // Positive profitability trend
            if (NetIncome <= 0) return false;

            // Reasonable debt levels
            if (DebtToEquity > 3.0m) return false; // Max 3:1 debt to equity

            // Adequate liquidity
            if (LiquidityRatio < 0.25m) return false; // Minimum 25% cash coverage

            return true;
        }

        /// <summary>
        /// Calculates earnings quality score for volatility prediction.
        /// Higher scores indicate more reliable earnings that may drive price action.
        /// </summary>
        /// <returns>Score from 0-100 indicating earnings quality</returns>
        public int GetEarningsQualityScore()
        {
            int score = 0;

            // Revenue consistency (higher revenue = higher score)
            if (Revenue > 1_000_000_000m) score += 25;      // $1B+
            else if (Revenue > 500_000_000m) score += 20;   // $500M+
            else if (Revenue > 100_000_000m) score += 15;   // $100M+
            else if (Revenue > 50_000_000m) score += 10;    // $50M+

            // Profitability strength
            if (NetIncome > 0 && NetIncome / Revenue > 0.1m) score += 25; // >10% net margin
            else if (NetIncome > 0 && NetIncome / Revenue > 0.05m) score += 15; // >5% net margin
            else if (NetIncome > 0) score += 10; // Profitable

            // Cash flow health
            if (FreeCashFlow > 0 && FreeCashFlow > NetIncome * 0.8m) score += 25; // Strong FCF
            else if (FreeCashFlow > 0) score += 15; // Positive FCF

            // Financial stability
            if (DebtToEquity < 1.0m) score += 15; // Conservative debt
            else if (DebtToEquity < 2.0m) score += 10; // Moderate debt

            if (LiquidityRatio > 0.5m) score += 10; // Strong liquidity

            return Math.Min(score, 100);
        }

        /// <summary>
        /// Validates financial data completeness and consistency.
        /// </summary>
        /// <returns>True if financial data is valid for analysis</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Symbol) &&
                   ReportingPeriod > DateTime.MinValue &&
                   Revenue >= 0 &&
                   TotalAssets >= 0 &&
                   Math.Abs(TotalAssets - (TotalLiabilities + ShareholdersEquity)) / Math.Max(TotalAssets, 1) < 0.01m; // Balance sheet equation check
        }

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return $"CompanyFinancials[{Symbol}]: {ReportingPeriod:yyyy-MM-dd} " +
                   $"Rev:${Revenue:N0} NI:${NetIncome:N0} (ROE:{ROE:F1}%, Quality:{GetEarningsQualityScore()})";
        }
    }
}

// Total Lines: 218
