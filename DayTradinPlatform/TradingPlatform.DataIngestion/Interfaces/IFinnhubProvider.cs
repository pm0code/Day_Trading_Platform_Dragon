// File: TradingPlatform.DataIngestion\Interfaces\IFinnhubProvider.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Finnhub-specific data provider interface.
    /// Reflects actual Finnhub API capabilities including real-time quotes,
    /// company fundamentals, sentiment analysis, and market news.
    /// </summary>
    public interface IFinnhubProvider : IMarketDataProvider
    {
        // ========== FINNHUB-SPECIFIC QUOTE METHODS ==========

        /// <summary>
        /// Gets real-time quote using Finnhub's /quote endpoint.
        /// </summary>
        Task<MarketData?> GetQuoteAsync(string symbol);

        /// <summary>
        /// Gets batch quotes for multiple symbols (sequential calls due to Finnhub limitations).
        /// </summary>
        Task<List<MarketData>?> GetBatchQuotesAsync(List<string> symbols);

        // ========== FINNHUB-SPECIFIC CANDLE DATA ==========

        /// <summary>
        /// Gets OHLCV candle data using Finnhub's resolution system.
        /// Resolutions: 1, 5, 15, 30, 60, D, W, M
        /// </summary>
        Task<MarketData?> GetCandleDataAsync(string symbol, string resolution, DateTime from, DateTime to);

        // ========== FINNHUB-SPECIFIC COMPANY DATA ==========

        /// <summary>
        /// Gets company profile using Finnhub's /stock/profile2 endpoint.
        /// </summary>
        Task<CompanyProfile> GetCompanyProfileAsync(string symbol);

        /// <summary>
        /// Gets company financials using Finnhub's /stock/financials-reported endpoint.
        /// </summary>
        Task<CompanyFinancials> GetCompanyFinancialsAsync(string symbol);

        // ========== FINNHUB-SPECIFIC MARKET FEATURES ==========

        /// <summary>
        /// Gets market status using Finnhub's /stock/market-status endpoint.
        /// </summary>
        Task<bool> IsMarketOpenAsync();

        /// <summary>
        /// Gets available stock symbols for exchange using Finnhub's /stock/symbol endpoint.
        /// </summary>
        Task<List<string>?> GetStockSymbolsAsync(string exchange = "US");

        // ========== FINNHUB-SPECIFIC SENTIMENT & NEWS ==========

        /// <summary>
        /// Gets insider sentiment using Finnhub's /stock/insider-sentiment endpoint.
        /// </summary>
        Task<SentimentData> GetInsiderSentimentAsync(string symbol);

        /// <summary>
        /// Gets market news using Finnhub's /news endpoint.
        /// Categories: general, forex, crypto, merger
        /// </summary>
        Task<List<NewsItem>> GetMarketNewsAsync(string category = "general");

        /// <summary>
        /// Gets company news using Finnhub's /company-news endpoint.
        /// </summary>
        Task<List<NewsItem>> GetCompanyNewsAsync(string symbol, DateTime from, DateTime to);

        // ========== FINNHUB-SPECIFIC TECHNICAL INDICATORS ==========

        /// <summary>
        /// Gets technical indicators using Finnhub's /indicator endpoint.
        /// </summary>
        Task<Dictionary<string, decimal>> GetTechnicalIndicatorsAsync(string symbol, string indicator);
    }
}

// Total Lines: 85
