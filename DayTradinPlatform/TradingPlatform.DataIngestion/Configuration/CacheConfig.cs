// File: TradingPlatform.DataIngestion\Configuration\CacheConfig.cs

using System;

namespace TradingPlatform.DataIngestion.Configuration
{
    /// <summary>
    /// Configuration settings for data caching throughout the trading platform.
    /// Controls cache durations, sizes, and behaviors for optimal performance
    /// while ensuring data freshness for trading decisions.
    /// 
    /// All cache durations are configurable to balance performance with data accuracy.
    /// Real-time trading data requires shorter cache durations than historical data.
    /// </summary>
    public class CacheConfig
    {
        // ========== REAL-TIME DATA CACHE SETTINGS ==========

        /// <summary>
        /// Cache duration for real-time quote data in seconds.
        /// Default: 30 seconds for balance between performance and freshness.
        /// Day trading requires relatively fresh data for accurate decisions.
        /// </summary>
        public int RealTimeCacheSeconds { get; set; } = 30;

        /// <summary>
        /// Cache duration for individual stock quotes in seconds.
        /// Default: 15 seconds for high-frequency trading scenarios.
        /// Should be shorter than RealTimeCacheSeconds for single-stock focus.
        /// </summary>
        public int QuoteCacheSeconds { get; set; } = 15;

        /// <summary>
        /// Cache duration for batch quote requests in seconds.
        /// Default: 45 seconds since batch data is typically used for screening.
        /// Longer duration acceptable for portfolio-wide analysis.
        /// </summary>
        public int BatchQuoteCacheSeconds { get; set; } = 45;

        /// <summary>
        /// Cache duration for market data aggregation results in seconds.
        /// Default: 60 seconds for processed/calculated market data.
        /// Aggregated data changes less frequently than raw quotes.
        /// </summary>
        public int AggregatedDataCacheSeconds { get; set; } = 60;

        // ========== HISTORICAL DATA CACHE SETTINGS ==========

        /// <summary>
        /// Cache duration for historical daily data in minutes.
        /// Default: 60 minutes since historical data is static once day closes.
        /// Longer cache acceptable as historical data doesn't change intraday.
        /// </summary>
        public int HistoricalDataCacheMinutes { get; set; } = 60;

        /// <summary>
        /// Cache duration for candlestick/OHLCV data in minutes.
        /// Default: 30 minutes for technical analysis data.
        /// Balance between performance and capturing latest completed candles.
        /// </summary>
        public int CandleDataCacheMinutes { get; set; } = 30;

        /// <summary>
        /// Cache duration for technical indicators in minutes.
        /// Default: 15 minutes since indicators are calculated from market data.
        /// Should refresh frequently enough to capture trend changes.
        /// </summary>
        public int TechnicalIndicatorCacheMinutes { get; set; } = 15;

        // ========== REFERENCE DATA CACHE SETTINGS ==========

        /// <summary>
        /// Cache duration for stock symbol lists in hours.
        /// Default: 24 hours since symbol lists change infrequently.
        /// Can cache for extended periods as new listings are rare.
        /// </summary>
        public int SymbolListCacheHours { get; set; } = 24;

        /// <summary>
        /// Cache duration for market status information in minutes.
        /// Default: 5 minutes for market open/close status.
        /// Needs frequent updates around market open/close times.
        /// </summary>
        public int MarketStatusCacheMinutes { get; set; } = 5;

        /// <summary>
        /// Cache duration for earnings calendar data in hours.
        /// Default: 12 hours since earnings dates are scheduled in advance.
        /// Daily refresh sufficient for earnings planning.
        /// </summary>
        public int EarningsCalendarCacheHours { get; set; } = 12;

        // ========== NEWS AND SENTIMENT CACHE SETTINGS ==========

        /// <summary>
        /// Cache duration for news articles in minutes.
        /// Default: 10 minutes for breaking news and market-moving events.
        /// News impacts trading decisions, so must be relatively fresh.
        /// </summary>
        public int NewsCacheMinutes { get; set; } = 10;

        /// <summary>
        /// Cache duration for sentiment analysis data in minutes.
        /// Default: 30 minutes since sentiment trends change gradually.
        /// Longer than news cache as sentiment is derived/calculated data.
        /// </summary>
        public int SentimentCacheMinutes { get; set; } = 30;

        /// <summary>
        /// Cache duration for insider trading sentiment in hours.
        /// Default: 6 hours since insider data is reported with delays.
        /// Regulatory filings have natural delays, so longer cache acceptable.
        /// </summary>
        public int InsiderSentimentCacheHours { get; set; } = 6;

        // ========== CACHE SIZE AND PERFORMANCE SETTINGS ==========

        /// <summary>
        /// Maximum number of symbols to keep in memory cache.
        /// Default: 1000 symbols for active trading scenarios.
        /// Balances memory usage with performance for large watchlists.
        /// </summary>
        public int MaxCachedSymbols { get; set; } = 1000;

        /// <summary>
        /// Maximum cache size in megabytes.
        /// Default: 256 MB for comprehensive data caching.
        /// Prevents excessive memory usage while maintaining performance.
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 256;

        /// <summary>
        /// Cache cleanup interval in minutes.
        /// Default: 15 minutes for regular cache maintenance.
        /// Removes expired entries and manages memory usage.
        /// </summary>
        public int CacheCleanupIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// Enable persistent cache to disk for faster startup.
        /// Default: true for improved application startup performance.
        /// Stores frequently accessed data between application restarts.
        /// </summary>
        public bool EnablePersistentCache { get; set; } = true;

        // ========== CACHE BEHAVIOR SETTINGS ==========

        /// <summary>
        /// Enable cache warming on application startup.
        /// Default: true to pre-load frequently accessed data.
        /// Improves initial user experience by pre-fetching common data.
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;

        /// <summary>
        /// Enable automatic cache refresh for critical data.
        /// Default: true to proactively refresh expiring cache entries.
        /// Prevents cache misses during active trading periods.
        /// </summary>
        public bool EnableAutoRefresh { get; set; } = true;

        /// <summary>
        /// Cache compression to reduce memory usage.
        /// Default: false to prioritize speed over memory efficiency.
        /// Can be enabled if memory usage becomes a concern.
        /// </summary>
        public bool EnableCacheCompression { get; set; } = false;

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Gets the appropriate cache duration for a specific data type.
        /// Provides centralized cache duration logic for consistency.
        /// </summary>
        /// <param name="dataType">Type of data being cached</param>
        /// <returns>Cache duration as TimeSpan</returns>
        public TimeSpan GetCacheDuration(CacheDataType dataType)
        {
            return dataType switch
            {
                CacheDataType.RealTimeQuote => TimeSpan.FromSeconds(RealTimeCacheSeconds),
                CacheDataType.Quote => TimeSpan.FromSeconds(QuoteCacheSeconds),
                CacheDataType.BatchQuote => TimeSpan.FromSeconds(BatchQuoteCacheSeconds),
                CacheDataType.AggregatedData => TimeSpan.FromSeconds(AggregatedDataCacheSeconds),
                CacheDataType.HistoricalData => TimeSpan.FromMinutes(HistoricalDataCacheMinutes),
                CacheDataType.CandleData => TimeSpan.FromMinutes(CandleDataCacheMinutes),
                CacheDataType.TechnicalIndicator => TimeSpan.FromMinutes(TechnicalIndicatorCacheMinutes),
                CacheDataType.SymbolList => TimeSpan.FromHours(SymbolListCacheHours),
                CacheDataType.MarketStatus => TimeSpan.FromMinutes(MarketStatusCacheMinutes),
                CacheDataType.EarningsCalendar => TimeSpan.FromHours(EarningsCalendarCacheHours),
                CacheDataType.News => TimeSpan.FromMinutes(NewsCacheMinutes),
                CacheDataType.Sentiment => TimeSpan.FromMinutes(SentimentCacheMinutes),
                CacheDataType.InsiderSentiment => TimeSpan.FromHours(InsiderSentimentCacheHours),
                _ => TimeSpan.FromMinutes(30) // Default fallback
            };
        }

        /// <summary>
        /// Validates cache configuration values for consistency and reasonableness.
        /// Ensures cache settings don't conflict with trading requirements.
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool ValidateConfiguration()
        {
            // Real-time data should have shorter cache than historical data
            if (QuoteCacheSeconds > HistoricalDataCacheMinutes * 60)
                return false;

            // Cache sizes should be reasonable
            if (MaxCacheSizeMB < 1 || MaxCacheSizeMB > 2048)
                return false;

            if (MaxCachedSymbols < 10 || MaxCachedSymbols > 10000)
                return false;

            // Cache durations should be positive
            if (RealTimeCacheSeconds <= 0 || QuoteCacheSeconds <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// Creates a configuration optimized for high-frequency trading.
        /// Reduces cache durations for maximum data freshness.
        /// </summary>
        /// <returns>CacheConfig optimized for high-frequency scenarios</returns>
        public static CacheConfig CreateHighFrequencyConfig()
        {
            return new CacheConfig
            {
                RealTimeCacheSeconds = 10,
                QuoteCacheSeconds = 5,
                BatchQuoteCacheSeconds = 15,
                AggregatedDataCacheSeconds = 20,
                NewsCacheMinutes = 5,
                SentimentCacheMinutes = 15,
                MaxCachedSymbols = 500,
                EnableAutoRefresh = true,
                EnableCacheWarming = true
            };
        }

        /// <summary>
        /// Creates a configuration optimized for swing trading.
        /// Uses longer cache durations for less frequent data updates.
        /// </summary>
        /// <returns>CacheConfig optimized for swing trading scenarios</returns>
        public static CacheConfig CreateSwingTradingConfig()
        {
            return new CacheConfig
            {
                RealTimeCacheSeconds = 120,
                QuoteCacheSeconds = 60,
                BatchQuoteCacheSeconds = 180,
                AggregatedDataCacheSeconds = 300,
                NewsCacheMinutes = 30,
                SentimentCacheMinutes = 60,
                MaxCachedSymbols = 2000,
                EnableAutoRefresh = true,
                EnableCacheWarming = true
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            return $"CacheConfig: Quote={QuoteCacheSeconds}s, RealTime={RealTimeCacheSeconds}s, " +
                   $"MaxSymbols={MaxCachedSymbols}, MaxSize={MaxCacheSizeMB}MB";
        }
    }

    /// <summary>
    /// Enumeration of different types of cached data.
    /// Used for determining appropriate cache durations and behaviors.
    /// </summary>
    public enum CacheDataType
    {
        /// <summary>Real-time streaming quote data</summary>
        RealTimeQuote,
        /// <summary>Individual stock quote data</summary>
        Quote,
        /// <summary>Batch quote request results</summary>
        BatchQuote,
        /// <summary>Aggregated and processed market data</summary>
        AggregatedData,
        /// <summary>Historical daily price data</summary>
        HistoricalData,
        /// <summary>Candlestick/OHLCV chart data</summary>
        CandleData,
        /// <summary>Technical analysis indicators</summary>
        TechnicalIndicator,
        /// <summary>Stock symbol listings</summary>
        SymbolList,
        /// <summary>Market open/close status</summary>
        MarketStatus,
        /// <summary>Earnings announcement calendar</summary>
        EarningsCalendar,
        /// <summary>News articles and press releases</summary>
        News,
        /// <summary>Market sentiment analysis</summary>
        Sentiment,
        /// <summary>Insider trading sentiment data</summary>
        InsiderSentiment
    }
}

// Total Lines: 268
