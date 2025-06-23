// File: TradingPlatform.DataIngestion\Interfaces\IMarketDataAggregator.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Defines the contract for aggregating market data from multiple providers.
    /// Implementations must ensure data consistency and handle potential discrepancies
    /// between providers. All calculations must adhere to FinancialCalculationStandards.md
    /// and use decimal arithmetic for financial values.
    /// 
    /// This interface provides type-safe aggregation with proper async patterns,
    /// circuit breaker functionality, and comprehensive error handling.
    /// </summary>
    public interface IMarketDataAggregator
    {
        // ========== TYPE-SAFE AGGREGATION METHODS ==========

        /// <summary>
        /// Aggregates market data from a single API response with type safety.
        /// </summary>
        /// <typeparam name="T">The type of the API response data</typeparam>
        /// <param name="data">The API response data to aggregate</param>
        /// <param name="providerName">The name of the data provider</param>
        /// <returns>A task containing the aggregated MarketData object, or null if aggregation fails</returns>
        Task<MarketData> AggregateAsync<T>(T data, string providerName) where T : class;

        /// <summary>
        /// Aggregates market data from multiple API responses with type safety.
        /// </summary>
        /// <typeparam name="T">The type of the API response data</typeparam>
        /// <param name="dataList">A list of API response data to aggregate</param>
        /// <param name="providerName">The name of the data provider</param>
        /// <returns>A task containing a list of aggregated MarketData objects</returns>
        Task<List<MarketData>> AggregateBatchAsync<T>(List<T> dataList, string providerName) where T : class;

        /// <summary>
        /// Aggregates market data from multiple providers for the same symbol.
        /// Implements conflict resolution and data validation between providers.
        /// </summary>
        /// <param name="symbol">The stock symbol to aggregate data for</param>
        /// <param name="primaryData">Data from the primary provider</param>
        /// <param name="fallbackData">Data from the fallback provider (optional)</param>
        /// <returns>A task containing the best available aggregated MarketData</returns>
        Task<MarketData> AggregateMultiProviderAsync(string symbol, MarketData primaryData, MarketData fallbackData = null);

        /// <summary>
        /// Gets market data for a single symbol with intelligent provider selection.
        /// </summary>
        /// <param name="symbol">The stock symbol</param>
        /// <returns>Market data from the best available provider</returns>
        Task<MarketData> GetMarketDataAsync(string symbol);

        /// <summary>
        /// Gets market data for multiple symbols with intelligent provider selection.
        /// </summary>
        /// <param name="symbols">List of stock symbols</param>
        /// <returns>List of market data from available providers</returns>
        Task<List<MarketData>> GetBatchMarketDataAsync(List<string> symbols);

        // ========== PROVIDER MANAGEMENT METHODS ==========

        /// <summary>
        /// Checks if a specific provider is currently available (not in circuit breaker state).
        /// </summary>
        /// <param name="providerName">The name of the provider to check</param>
        /// <returns>True if the provider is available, false if in circuit breaker state</returns>
        bool IsProviderAvailable(string providerName);

        /// <summary>
        /// Gets the priority order of providers for data aggregation.
        /// </summary>
        /// <returns>An ordered list of provider names by priority</returns>
        List<string> GetProviderPriority();

        /// <summary>
        /// Records a provider failure for circuit breaker management.
        /// </summary>
        /// <param name="providerName">The name of the provider that failed</param>
        /// <param name="exception">The exception that occurred</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task RecordProviderFailureAsync(string providerName, Exception exception);

        /// <summary>
        /// Records a provider success for circuit breaker management.
        /// </summary>
        /// <param name="providerName">The name of the provider that succeeded</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task RecordProviderSuccessAsync(string providerName);

        // ========== DATA VALIDATION AND QUALITY METHODS ==========

        /// <summary>
        /// Validates market data for consistency and accuracy.
        /// Implements financial data validation rules (price > 0, volume >= 0, etc.).
        /// </summary>
        /// <param name="marketData">The market data to validate</param>
        /// <returns>True if the data passes validation, false otherwise</returns>
        bool ValidateMarketData(MarketData marketData);

        /// <summary>
        /// Compares market data from different providers and identifies discrepancies.
        /// </summary>
        /// <param name="primaryData">Data from the primary provider</param>
        /// <param name="fallbackData">Data from the fallback provider</param>
        /// <returns>A data quality report indicating any discrepancies</returns>
        DataQualityReport CompareProviderData(MarketData primaryData, MarketData fallbackData);

        /// <summary>
        /// Applies financial calculation standards to ensure decimal precision.
        /// </summary>
        /// <param name="marketData">The market data to normalize</param>
        /// <returns>Market data with standardized decimal precision</returns>
        MarketData NormalizeFinancialData(MarketData marketData);

        // ========== CACHING AND PERFORMANCE METHODS ==========

        /// <summary>
        /// Gets cached aggregated data if available and still valid.
        /// </summary>
        /// <param name="symbol">The stock symbol</param>
        /// <param name="maxAge">Maximum age of cached data to accept</param>
        /// <returns>Cached MarketData if available and valid, null otherwise</returns>
        Task<MarketData> GetCachedDataAsync(string symbol, TimeSpan maxAge);

        /// <summary>
        /// Caches aggregated market data for performance optimization.
        /// </summary>
        /// <param name="symbol">The stock symbol</param>
        /// <param name="marketData">The market data to cache</param>
        /// <param name="expiration">Cache expiration time</param>
        /// <returns>A task representing the asynchronous caching operation</returns>
        Task SetCachedDataAsync(string symbol, MarketData marketData, TimeSpan expiration);

        // ========== STATISTICS AND MONITORING METHODS ==========

        /// <summary>
        /// Gets aggregation statistics for monitoring and performance analysis.
        /// </summary>
        /// <returns>Statistics about aggregation performance and provider usage</returns>
        AggregationStatistics GetAggregationStatistics();

        /// <summary>
        /// Gets the current health status of all providers.
        /// </summary>
        /// <returns>A dictionary of provider names and their health status</returns>
        Dictionary<string, ProviderHealthStatus> GetProviderHealthStatus();

        // ========== EVENT HANDLING ==========

        /// <summary>
        /// Event raised when a provider fails and enters circuit breaker state.
        /// </summary>
        event EventHandler<ProviderFailureEventArgs> ProviderFailure;

        /// <summary>
        /// Event raised when aggregated data quality issues are detected.
        /// </summary>
        event EventHandler<DataQualityEventArgs> DataQualityIssue;
    }

    // ========== SUPPORTING TYPES ==========

    /// <summary>
    /// Represents a data quality report comparing multiple provider data sources.
    /// </summary>
    public class DataQualityReport
    {
        public bool HasDiscrepancies { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public decimal PriceVariancePercentage { get; set; }
        public long VolumeVariance { get; set; }
        public TimeSpan TimestampDifference { get; set; }
        public string RecommendedProvider { get; set; }
    }

    /// <summary>
    /// Represents aggregation performance statistics.
    /// </summary>
    public class AggregationStatistics
    {
        public DateTime StartTime { get; set; }
        public long TotalAggregations { get; set; }
        public long SuccessfulAggregations { get; set; }
        public long FailedAggregations { get; set; }
        public Dictionary<string, long> ProviderUsageCount { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, TimeSpan> AverageResponseTime { get; set; } = new Dictionary<string, TimeSpan>();
        public decimal SuccessRate => TotalAggregations > 0 ? (decimal)SuccessfulAggregations / TotalAggregations * 100 : 0;
    }

    /// <summary>
    /// Represents the health status of a data provider.
    /// </summary>
    public enum ProviderHealthStatus
    {
        Healthy,
        Degraded,
        CircuitBreakerOpen,
        Unavailable,
        Unknown
    }

    /// <summary>
    /// Event arguments for provider failure events.
    /// </summary>
    public class ProviderFailureEventArgs : EventArgs
    {
        public string ProviderName { get; set; }
        public Exception Exception { get; set; }
        public DateTime FailureTime { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    /// <summary>
    /// Event arguments for data quality issue events.
    /// </summary>
    public class DataQualityEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public DataQualityReport QualityReport { get; set; }
        public DateTime DetectedTime { get; set; }
        public string PrimaryProvider { get; set; }
        public string FallbackProvider { get; set; }
    }
}

// Total Lines: 187
