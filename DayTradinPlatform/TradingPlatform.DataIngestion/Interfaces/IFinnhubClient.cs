// File: TradingPlatform.DataIngestion\Interfaces\IFinnhubClient.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Low-level HTTP client interface for Finnhub API communication.
    /// Provides direct API access with minimal processing for testing and flexibility.
    /// Implements proper HttpClient patterns for dependency injection and mocking.
    /// </summary>
    public interface IFinnhubClient
    {
        // ========== RAW API ENDPOINTS ==========

        /// <summary>
        /// Makes direct call to Finnhub quote endpoint (/quote).
        /// Returns raw JSON response for maximum flexibility.
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw API response</returns>
        Task<FinnhubApiResponse<FinnhubQuoteResponse>> GetQuoteRawAsync(string symbol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub candle endpoint (/stock/candle).
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="resolution">Time resolution (1, 5, 15, 30, 60, D, W, M)</param>
        /// <param name="from">Start timestamp (Unix)</param>
        /// <param name="to">End timestamp (Unix)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw candle data response</returns>
        Task<FinnhubApiResponse<TradingPlatform.DataIngestion.Models.FinnhubCandleResponse>> GetCandleRawAsync(string symbol, string resolution, long from, long to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub company profile endpoint (/stock/profile2).
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw company profile response</returns>
        Task<FinnhubApiResponse<Dictionary<string, object>>> GetCompanyProfileRawAsync(string symbol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub news endpoint (/news or /company-news).
        /// </summary>
        /// <param name="category">News category or symbol for company news</param>
        /// <param name="isCompanyNews">True for company-specific news</param>
        /// <param name="from">Start date (for company news)</param>
        /// <param name="to">End date (for company news)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw news response</returns>
        Task<FinnhubApiResponse<List<FinnhubNewsItem>>> GetNewsRawAsync(string category, bool isCompanyNews = false, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub insider sentiment endpoint (/stock/insider-sentiment).
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="from">Start date</param>
        /// <param name="to">End date</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw insider sentiment response</returns>
        Task<FinnhubApiResponse<FinnhubSentimentResponse>> GetInsiderSentimentRawAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub market status endpoint (/stock/market-status).
        /// </summary>
        /// <param name="exchange">Exchange identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw market status response</returns>
        Task<FinnhubApiResponse<FinnhubMarketStatus>> GetMarketStatusRawAsync(string exchange = "US", CancellationToken cancellationToken = default);

        /// <summary>
        /// Makes direct call to Finnhub symbols endpoint (/stock/symbol).
        /// </summary>
        /// <param name="exchange">Exchange identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw symbols response</returns>
        Task<FinnhubApiResponse<List<FinnhubSymbol>>> GetSymbolsRawAsync(string exchange = "US", CancellationToken cancellationToken = default);

        // ========== CLIENT UTILITIES ==========

        /// <summary>
        /// Tests API connectivity and authentication.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Connection test result</returns>
        Task<FinnhubApiResponse<bool>> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current rate limit status.
        /// </summary>
        /// <returns>Rate limit information</returns>
        Task<FinnhubRateLimitStatus> GetRateLimitStatusAsync();
    }

    // ========== CLIENT RESPONSE TYPES ==========

    /// <summary>
    /// Wrapper for raw Finnhub API responses with comprehensive metadata.
    /// Enables detailed testing and error analysis.
    /// </summary>
    /// <typeparam name="T">Response data type</typeparam>
    public class FinnhubApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public T? Data { get; set; }
        public int StatusCode { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public TimeSpan ResponseTime { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public string RequestUrl { get; set; } = string.Empty;
        public bool WasRateLimited { get; set; }
        public string? RawResponse { get; set; } // For debugging

        public static FinnhubApiResponse<T> Success(T data, int statusCode, TimeSpan responseTime, Dictionary<string, string>? headers = null)
        {
            return new FinnhubApiResponse<T>
            {
                IsSuccess = true,
                Data = data,
                StatusCode = statusCode,
                ResponseTime = responseTime,
                Headers = headers ?? new(),
                RequestTimestamp = DateTime.UtcNow
            };
        }

        public static FinnhubApiResponse<T> Failure(int statusCode, string errorMessage, TimeSpan responseTime)
        {
            return new FinnhubApiResponse<T>
            {
                IsSuccess = false,
                StatusCode = statusCode,
                ErrorMessage = errorMessage,
                ResponseTime = responseTime,
                RequestTimestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Rate limit status information.
    /// </summary>
    public class FinnhubRateLimitStatus
    {
        public int RequestsRemaining { get; set; }
        public DateTime ResetTime { get; set; }
        public int RequestsPerMinute { get; set; }
        public bool IsLimited { get; set; }
        public TimeSpan SuggestedDelay { get; set; }
    }
}

// Total Lines: 158
