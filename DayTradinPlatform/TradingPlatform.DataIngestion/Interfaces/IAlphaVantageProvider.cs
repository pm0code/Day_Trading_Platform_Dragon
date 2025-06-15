// File: TradingPlatform.DataIngestion\Interfaces\IAlphaVantageProvider.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// AlphaVantage-specific data provider interface.
    /// Reflects actual AlphaVantage API capabilities including global quotes,
    /// time series data, fundamentals, and streaming subscriptions.
    /// </summary>
    public interface IAlphaVantageProvider : IMarketDataProvider
    {
        // ========== ALPHAVANTAGE-SPECIFIC QUOTE METHODS ==========

        /// <summary>
        /// Gets global quote using AlphaVantage's GLOBAL_QUOTE function.
        /// </summary>
        Task<MarketData> GetGlobalQuoteAsync(string symbol);

        /// <summary>
        /// Gets intraday quotes using AlphaVantage's TIME_SERIES_INTRADAY function.
        /// Intervals: 1min, 5min, 15min, 30min, 60min
        /// </summary>
        Task<List<MarketData>> GetIntradayDataAsync(string symbol, string interval = "5min");

        // ========== ALPHAVANTAGE-SPECIFIC TIME SERIES ==========

        /// <summary>
        /// Gets daily time series using AlphaVantage's TIME_SERIES_DAILY function.
        /// Output sizes: compact (100 data points), full (20+ years)
        /// </summary>
        Task<List<DailyData>> GetDailyTimeSeriesAsync(string symbol, string outputSize = "compact");

        /// <summary>
        /// Gets daily adjusted time series with dividend/split adjustments.
        /// </summary>
        Task<List<DailyData>> GetDailyAdjustedTimeSeriesAsync(string symbol, string outputSize = "compact");

        /// <summary>
        /// Gets weekly time series using AlphaVantage's TIME_SERIES_WEEKLY function.
        /// </summary>
        Task<List<DailyData>> GetWeeklyTimeSeriesAsync(string symbol);

        /// <summary>
        /// Gets monthly time series using AlphaVantage's TIME_SERIES_MONTHLY function.
        /// </summary>
        Task<List<DailyData>> GetMonthlyTimeSeriesAsync(string symbol);

        // ========== ALPHAVANTAGE-SPECIFIC FUNDAMENTALS ==========

        /// <summary>
        /// Gets company overview using AlphaVantage's OVERVIEW function.
        /// </summary>
        Task<CompanyOverview> GetCompanyOverviewAsync(string symbol);

        /// <summary>
        /// Gets earnings data using AlphaVantage's EARNINGS function.
        /// </summary>
        Task<EarningsData> GetEarningsAsync(string symbol);

        /// <summary>
        /// Gets income statement using AlphaVantage's INCOME_STATEMENT function.
        /// </summary>
        Task<IncomeStatement> GetIncomeStatementAsync(string symbol);

        // ========== ALPHAVANTAGE-SPECIFIC SEARCH & DISCOVERY ==========

        /// <summary>
        /// Searches for symbols using AlphaVantage's SYMBOL_SEARCH function.
        /// </summary>
        Task<List<SymbolSearchResult>> SearchSymbolsAsync(string keywords);

        /// <summary>
        /// Gets market status (AlphaVantage doesn't have dedicated endpoint, uses time-based logic).
        /// </summary>
        Task<bool> IsMarketOpenAsync();

        // ========== ALPHAVANTAGE-SPECIFIC STREAMING (POLLING-BASED) ==========

        /// <summary>
        /// Creates polling-based subscription for real-time updates.
        /// AlphaVantage doesn't support WebSocket, so implements intelligent polling.
        /// </summary>
        IObservable<MarketData> SubscribeToQuoteUpdatesAsync(string symbol, TimeSpan interval);
    }
}

// Total Lines: 92
