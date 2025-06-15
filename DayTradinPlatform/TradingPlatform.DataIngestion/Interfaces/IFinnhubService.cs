// File: TradingPlatform.DataIngestion\Interfaces\IFinnhubService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// High-level service interface for Finnhub operations.
    /// Provides business logic layer above the raw provider implementation.
    /// Includes validation, error handling, and business rules for day trading.
    /// </summary>
    public interface IFinnhubService
    {
        // ========== QUOTE OPERATIONS ==========

        /// <summary>
        /// Gets validated quote data for day trading analysis.
        /// Includes automatic validation against trading criteria.
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validated market data with trading assessment</returns>
        Task<ServiceResult<MarketDataWithAssessment>> GetValidatedQuoteAsync(string symbol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets batch quotes with parallel processing and validation.
        /// Optimized for screening multiple symbols efficiently.
        /// </summary>
        /// <param name="symbols">List of symbols to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of validated quotes with success/failure status</returns>
        Task<ServiceResult<List<MarketDataWithAssessment>>> GetValidatedBatchQuotesAsync(List<string> symbols, CancellationToken cancellationToken = default);

        // ========== COMPANY RESEARCH ==========

        /// <summary>
        /// Gets comprehensive company analysis for trading decisions.
        /// Combines profile, financials, and trading suitability assessment.
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete company analysis with trading recommendations</returns>
        Task<ServiceResult<CompanyAnalysis>> GetCompanyAnalysisAsync(string symbol, CancellationToken cancellationToken = default);

        // ========== MARKET INTELLIGENCE ==========

        /// <summary>
        /// Gets market sentiment with confidence scoring.
        /// Includes insider trading analysis and sentiment trends.
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sentiment analysis with confidence metrics</returns>
        Task<ServiceResult<EnhancedSentimentData>> GetMarketSentimentAsync(string symbol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets filtered and categorized news for trading catalysts.
        /// Includes relevance scoring and sentiment analysis.
        /// </summary>
        /// <param name="symbol">Stock symbol (optional for market-wide news)</param>
        /// <param name="category">News category filter</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Filtered news with trading relevance scores</returns>
        Task<ServiceResult<List<TradingNewsItem>>> GetTradingNewsAsync(string? symbol = null, string category = "general", int maxResults = 50, CancellationToken cancellationToken = default);

        // ========== TECHNICAL ANALYSIS ==========

        /// <summary>
        /// Gets technical indicators with day trading interpretation.
        /// Includes volatility analysis and trend identification.
        /// </summary>
        /// <param name="symbol">Stock symbol</param>
        /// <param name="indicators">Requested technical indicators</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Technical analysis with trading signals</returns>
        Task<ServiceResult<TechnicalAnalysis>> GetTechnicalAnalysisAsync(string symbol, List<string> indicators, CancellationToken cancellationToken = default);

        // ========== MARKET STATUS ==========

        /// <summary>
        /// Gets current market status with trading hours information.
        /// Includes pre-market and after-hours status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Comprehensive market status information</returns>
        Task<ServiceResult<MarketStatusInfo>> GetMarketStatusAsync(CancellationToken cancellationToken = default);

        // ========== SERVICE HEALTH ==========

        /// <summary>
        /// Performs comprehensive health check of Finnhub service.
        /// Validates connectivity, authentication, and rate limits.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Service health status with diagnostic information</returns>
        Task<ServiceResult<FinnhubHealthStatus>> GetHealthStatusAsync(CancellationToken cancellationToken = default);
    }

    // ========== SERVICE RESULT TYPES ==========

    /// <summary>
    /// Generic service result wrapper with comprehensive error handling.
    /// </summary>
    /// <typeparam name="T">Result data type</typeparam>
    public class ServiceResult<T>
    {
        public bool IsSuccess { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public TimeSpan ResponseTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ServiceResult<T> Success(T data, TimeSpan responseTime = default)
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                Data = data,
                ResponseTime = responseTime
            };
        }

        public static ServiceResult<T> Failure(string errorMessage, string? errorCode = null)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    // ========== ENHANCED DATA MODELS ==========

    /// <summary>
    /// Market data enhanced with day trading assessment.
    /// </summary>
    public class MarketDataWithAssessment
    {
        public MarketData MarketData { get; set; } = new(null);
        public DayTradingAssessment Assessment { get; set; } = new();
        public ValidationResult ValidationResult { get; set; } = new();
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Comprehensive company analysis for trading decisions.
    /// </summary>
    public class CompanyAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public CompanyProfile Profile { get; set; } = new();
        public CompanyFinancials? Financials { get; set; }
        public TradingSuitabilityAssessment Suitability { get; set; } = new();
        public List<string> TradingRecommendations { get; set; } = new();
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Enhanced sentiment data with confidence metrics.
    /// </summary>
    public class EnhancedSentimentData
    {
        public SentimentData BaseData { get; set; } = new() { Symbol = "", Sentiment = "" };
        public decimal ConfidenceScore { get; set; }
        public List<string> SentimentFactors { get; set; } = new();
        public string TradingImplication { get; set; } = string.Empty;
    }

    /// <summary>
    /// News item enhanced with trading relevance.
    /// </summary>
    public class TradingNewsItem
    {
        public NewsItem BaseNews { get; set; } = new() { Id = "", Title = "", Source = "", Url = "" };
        public decimal RelevanceScore { get; set; }
        public string TradingImpact { get; set; } = string.Empty;
        public List<string> TradingKeywords { get; set; } = new();
    }

    /// <summary>
    /// Technical analysis with trading signals.
    /// </summary>
    public class TechnicalAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public Dictionary<string, decimal> Indicators { get; set; } = new();
        public string TrendDirection { get; set; } = string.Empty;
        public decimal VolatilityScore { get; set; }
        public List<string> TradingSignals { get; set; } = new();
        public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Comprehensive market status information.
    /// </summary>
    public class MarketStatusInfo
    {
        public bool IsOpen { get; set; }
        public string Session { get; set; } = string.Empty;
        public DateTime NextOpenTime { get; set; }
        public DateTime NextCloseTime { get; set; }
        public bool IsPreMarket { get; set; }
        public bool IsAfterHours { get; set; }
        public string TimeZone { get; set; } = "US/Eastern";
    }

    /// <summary>
    /// Finnhub service health status.
    /// </summary>
    public class FinnhubHealthStatus
    {
        public bool IsHealthy { get; set; }
        public bool IsAuthenticated { get; set; }
        public int RemainingQuota { get; set; }
        public DateTime QuotaResetTime { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public List<string> HealthIssues { get; set; } = new();
    }

    /// <summary>
    /// Validation result for API responses.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public string ValidationTimestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
}

// Total Lines: 245
