// File: TradingPlatform.DataIngestion\Interfaces\IFinnhubProvider.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using System.Reactive;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Finnhub-specific data provider interface.
    /// Reflects actual Finnhub API capabilities including real-time quotes,
    /// company fundamentals, sentiment analysis, and market news.
    /// All operations use TradingResult pattern for consistent error handling.
    /// Optimized for premium $50/month plan with enhanced features.
    /// </summary>
    public interface IFinnhubProvider : IMarketDataProvider
    {
        // ========== FINNHUB-SPECIFIC QUOTE METHODS ==========

        /// <summary>
        /// Gets real-time quote using Finnhub's /quote endpoint.
        /// </summary>
        Task<TradingResult<MarketData?>> GetQuoteAsync(string symbol);

        /// <summary>
        /// Gets batch quotes for multiple symbols (sequential calls due to Finnhub limitations).
        /// </summary>
        Task<TradingResult<List<MarketData>?>> GetBatchQuotesAsync(List<string> symbols);

        // ========== FINNHUB-SPECIFIC CANDLE DATA ==========

        /// <summary>
        /// Gets OHLCV candle data using Finnhub's resolution system.
        /// Resolutions: 1, 5, 15, 30, 60, D, W, M
        /// </summary>
        Task<TradingResult<MarketData?>> GetCandleDataAsync(string symbol, string resolution, DateTime from, DateTime to);

        // ========== FINNHUB-SPECIFIC COMPANY DATA ==========

        /// <summary>
        /// Gets company profile using Finnhub's /stock/profile2 endpoint.
        /// </summary>
        Task<TradingResult<CompanyProfile>> GetCompanyProfileAsync(string symbol);

        /// <summary>
        /// Gets company financials using Finnhub's /stock/financials-reported endpoint.
        /// </summary>
        Task<TradingResult<CompanyFinancials>> GetCompanyFinancialsAsync(string symbol);

        // ========== FINNHUB-SPECIFIC MARKET FEATURES ==========

        /// <summary>
        /// Gets market status using Finnhub's /stock/market-status endpoint.
        /// </summary>
        Task<TradingResult<bool>> IsMarketOpenAsync();

        /// <summary>
        /// Gets available stock symbols for exchange using Finnhub's /stock/symbol endpoint.
        /// </summary>
        Task<TradingResult<List<string>?>> GetStockSymbolsAsync(string exchange = "US");

        // ========== FINNHUB-SPECIFIC SENTIMENT & NEWS ==========

        /// <summary>
        /// Gets insider sentiment using Finnhub's /stock/insider-sentiment endpoint.
        /// </summary>
        Task<TradingResult<SentimentData>> GetInsiderSentimentAsync(string symbol);

        /// <summary>
        /// Gets market news using Finnhub's /news endpoint.
        /// Categories: general, forex, crypto, merger
        /// </summary>
        Task<TradingResult<List<NewsItem>>> GetMarketNewsAsync(string category = "general");

        /// <summary>
        /// Gets company news using Finnhub's /company-news endpoint.
        /// </summary>
        Task<TradingResult<List<NewsItem>>> GetCompanyNewsAsync(string symbol, DateTime from, DateTime to);

        // ========== FINNHUB-SPECIFIC TECHNICAL INDICATORS ==========

        /// <summary>
        /// Gets technical indicators using Finnhub's /indicator endpoint.
        /// </summary>
        Task<TradingResult<Dictionary<string, decimal>>> GetTechnicalIndicatorsAsync(string symbol, string indicator);

        // ========== FINNHUB-SPECIFIC PREMIUM FEATURES ==========

        /// <summary>
        /// Subscribes to real-time quote updates via WebSocket (Premium feature).
        /// </summary>
        Task<TradingResult<IObservable<MarketData>>> SubscribeToQuoteUpdatesAsync(string symbol);
    }
}

// Total Lines: 85
