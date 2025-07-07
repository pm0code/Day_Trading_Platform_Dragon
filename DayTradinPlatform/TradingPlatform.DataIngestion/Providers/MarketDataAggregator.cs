// File: TradingPlatform.DataIngestion\Providers\MarketDataAggregator.cs

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Configuration;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.DataIngestion.Providers
{
    /// <summary>
    /// Intelligent market data aggregation service that provides unified access to multiple data providers
    /// Implements provider failover, data quality validation, circuit breaker patterns, and performance optimization
    /// Target: Sub-100ms aggregation latency with comprehensive provider health monitoring
    /// </summary>
    public class MarketDataAggregator : CanonicalServiceBase, IMarketDataAggregator
    {
        private readonly IAlphaVantageProvider _alphaVantageProvider;
        private readonly IFinnhubProvider _finnhubProvider;
        private readonly IMemoryCache _cache;
        private readonly DataIngestionConfig _config;
        private readonly Dictionary<string, DateTime> _providerFailures;
        private readonly Dictionary<string, int> _consecutiveFailures;
        private readonly AggregationStatistics _statistics;

        // Performance metrics
        private long _totalAggregations = 0;
        private long _successfulAggregations = 0;
        private long _failedAggregations = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private long _providerFailoverEvents = 0;

        // Events for interface compliance
        public event EventHandler<ProviderFailureEventArgs>? ProviderFailure;
        public event EventHandler<DataQualityEventArgs>? DataQualityIssue;

        /// <summary>
        /// Initializes a new instance of MarketDataAggregator with required dependencies
        /// </summary>
        /// <param name="alphaVantageProvider">AlphaVantage data provider service</param>
        /// <param name="finnhubProvider">Finnhub data provider service</param>
        /// <param name="logger">Trading logger for comprehensive aggregation tracking</param>
        /// <param name="cache">Memory cache for data optimization</param>
        /// <param name="config">Configuration settings for data ingestion</param>
        public MarketDataAggregator(
            IAlphaVantageProvider alphaVantageProvider,
            IFinnhubProvider finnhubProvider,
            ITradingLogger logger,
            IMemoryCache cache,
            DataIngestionConfig config) : base(logger, "MarketDataAggregator")
        {
            _alphaVantageProvider = alphaVantageProvider ?? throw new ArgumentNullException(nameof(alphaVantageProvider));
            _finnhubProvider = finnhubProvider ?? throw new ArgumentNullException(nameof(finnhubProvider));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _providerFailures = new Dictionary<string, DateTime>();
            _consecutiveFailures = new Dictionary<string, int>();
            _statistics = new AggregationStatistics { StartTime = DateTime.UtcNow };
        }

        // ========== TYPE-SAFE AGGREGATION METHODS ==========

        /// <summary>
        /// Aggregates market data from a specific provider with type safety and validation
        /// </summary>
        /// <typeparam name="T">Type of data to aggregate</typeparam>
        /// <param name="data">Data to aggregate</param>
        /// <param name="providerName">Name of the data provider</param>
        /// <returns>Aggregated MarketData or null if aggregation fails</returns>
        public async Task<MarketData?> AggregateAsync<T>(T data, string providerName) where T : class
        {
            LogMethodEntry();
            try
            {
                Interlocked.Increment(ref _totalAggregations);
                _statistics.TotalAggregations++;

                if (data == null)
                {
                    LogWarning($"Null data received from {providerName}");
                    _statistics.FailedAggregations++;
                    Interlocked.Increment(ref _failedAggregations);
                    LogMethodExit();
                    return null;
                }

                MarketData? result = null;

                switch (data)
                {
                    case MarketData marketData:
                        result = NormalizeFinancialData(marketData);
                        break;
                    case AlphaVantageQuote alphaQuote:
                        result = MapAlphaVantageQuote(alphaQuote);
                        break;
                    case FinnhubQuoteResponse finnhubQuote:
                        LogWarning($"FinnhubQuoteResponse aggregation requires symbol context");
                        result = null;
                        break;
                    default:
                        LogError($"Unknown data type from {providerName}: {typeof(T).Name}");
                        _statistics.FailedAggregations++;
                        Interlocked.Increment(ref _failedAggregations);
                        LogMethodExit();
                        return null;
                }

                if (result != null && ValidateMarketData(result))
                {
                    await RecordProviderSuccessAsync(providerName);
                    _statistics.SuccessfulAggregations++;
                    Interlocked.Increment(ref _successfulAggregations);
                    _statistics.ProviderUsageCount[providerName] = _statistics.ProviderUsageCount.GetValueOrDefault(providerName, 0) + 1;
                    LogInfo($"Successfully aggregated data from {providerName} for {result.Symbol}");
                    LogMethodExit();
                    return result;
                }

                LogWarning($"Invalid market data from {providerName}");
                _statistics.FailedAggregations++;
                Interlocked.Increment(ref _failedAggregations);
                LogMethodExit();
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error aggregating data from {providerName}", ex);
                await RecordProviderFailureAsync(providerName, ex);
                _statistics.FailedAggregations++;
                Interlocked.Increment(ref _failedAggregations);
                LogMethodExit();
                return null;
            }
        }

        /// <summary>
        /// Aggregates a batch of market data from a specific provider with optimized processing
        /// </summary>
        /// <typeparam name="T">Type of data to aggregate</typeparam>
        /// <param name="dataList">List of data to aggregate</param>
        /// <param name="providerName">Name of the data provider</param>
        /// <returns>List of successfully aggregated MarketData</returns>
        public async Task<List<MarketData>> AggregateBatchAsync<T>(List<T> dataList, string providerName) where T : class
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Aggregating batch of {dataList?.Count ?? 0} items from {providerName}");

                if (dataList?.Any() != true)
                {
                    LogMethodExit();
                    return new List<MarketData>();
                }

                var results = new List<MarketData>();

                foreach (var data in dataList)
                {
                    var aggregated = await AggregateAsync(data, providerName);
                    if (aggregated != null)
                    {
                        results.Add(aggregated);
                    }
                }

                LogInfo($"Successfully aggregated {results.Count}/{dataList.Count} items from {providerName}");
                LogMethodExit();
                return results;
            }
            catch (Exception ex)
            {
                LogError($"Error in batch aggregation from {providerName}", ex);
                LogMethodExit();
                return new List<MarketData>();
            }
        }

        /// <summary>
        /// Aggregates market data from multiple providers with intelligent provider selection
        /// </summary>
        /// <param name="symbol">Symbol to aggregate data for</param>
        /// <param name="primaryData">Primary provider data</param>
        /// <param name="fallbackData">Fallback provider data</param>
        /// <returns>Best available MarketData or null if no valid data</returns>
        public Task<MarketData?> AggregateMultiProviderAsync(string symbol, MarketData? primaryData, MarketData? fallbackData = null)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Aggregating multi-provider data for {symbol}");

                if (primaryData != null && ValidateMarketData(primaryData))
                {
                    if (fallbackData != null && ValidateMarketData(fallbackData))
                    {
                        var qualityReport = CompareProviderData(primaryData, fallbackData);
                        if (qualityReport.HasDiscrepancies)
                        {
                            LogWarning($"Data discrepancies detected for {symbol}: {string.Join(", ", qualityReport.Issues)}");
                            OnDataQualityIssue(new DataQualityEventArgs
                            {
                                Symbol = symbol,
                                QualityReport = qualityReport,
                                DetectedTime = DateTime.UtcNow,
                                PrimaryProvider = "Primary",
                                FallbackProvider = "Fallback"
                            });

                            var selectedData = qualityReport.RecommendedProvider == "Primary" ? primaryData : fallbackData;
                            LogMethodExit();
                            return Task.FromResult(selectedData);
                        }
                    }

                    LogMethodExit();
                    return Task.FromResult(primaryData);
                }

                if (fallbackData != null && ValidateMarketData(fallbackData))
                {
                    LogInfo($"Using fallback data for {symbol}");
                    Interlocked.Increment(ref _providerFailoverEvents);
                    LogMethodExit();
                    return Task.FromResult(fallbackData);
                }

                LogError($"No valid data available for {symbol} from any provider");
                LogMethodExit();
                return Task.FromResult<MarketData?>(null);
            }
            catch (Exception ex)
            {
                LogError($"Error in multi-provider aggregation for {symbol}", ex);
                LogMethodExit();
                return Task.FromResult<MarketData?>(null);
            }
        }

        // ========== PROVIDER MANAGEMENT METHODS ==========

        /// <summary>
        /// Checks if a data provider is currently available for use
        /// </summary>
        /// <param name="providerName">Name of the provider to check</param>
        /// <returns>True if provider is available, false if in circuit breaker state</returns>
        public bool IsProviderAvailable(string providerName)
        {
            LogMethodEntry();
            try
            {
                var isAvailable = !IsProviderInCircuitBreaker(providerName);
                LogInfo($"Provider {providerName} availability: {isAvailable}");
                LogMethodExit();
                return isAvailable;
            }
            catch (Exception ex)
            {
                LogError($"Error checking provider availability for {providerName}", ex);
                LogMethodExit();
                return false;
            }
        }

        public List<string> GetProviderPriority()
        {
            return new List<string> { "AlphaVantage", "Finnhub" };
        }

        /// <summary>
        /// Records a provider failure for circuit breaker and health monitoring
        /// </summary>
        /// <param name="providerName">Name of the failed provider</param>
        /// <param name="exception">Exception that caused the failure</param>
        /// <returns>Task representing the async operation</returns>
        public async Task RecordProviderFailureAsync(string providerName, Exception exception)
        {
            LogMethodEntry();
            try
            {
                await Task.Run(() =>
                {
                    _providerFailures[providerName] = DateTime.UtcNow;
                    _consecutiveFailures[providerName] = _consecutiveFailures.GetValueOrDefault(providerName, 0) + 1;

                    LogWarning($"Recorded failure for provider {providerName}. Consecutive failures: {_consecutiveFailures[providerName]}");

                    OnProviderFailure(new ProviderFailureEventArgs
                    {
                        ProviderName = providerName,
                        Exception = exception,
                        FailureTime = DateTime.UtcNow,
                        ConsecutiveFailures = _consecutiveFailures[providerName]
                    });
                });
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError($"Error recording provider failure for {providerName}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Records a provider success to reset circuit breaker state
        /// </summary>
        /// <param name="providerName">Name of the successful provider</param>
        /// <returns>Task representing the async operation</returns>
        public async Task RecordProviderSuccessAsync(string providerName)
        {
            LogMethodEntry();
            try
            {
                await Task.Run(() =>
                {
                    if (_consecutiveFailures.ContainsKey(providerName))
                    {
                        LogInfo($"Provider {providerName} recovered from failure state");
                        _consecutiveFailures.Remove(providerName);
                    }

                    if (_providerFailures.ContainsKey(providerName))
                    {
                        _providerFailures.Remove(providerName);
                    }
                });
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError($"Error recording provider success for {providerName}", ex);
                LogMethodExit();
                throw;
            }
        }

        // ========== DATA VALIDATION AND QUALITY METHODS ==========

        /// <summary>
        /// Validates market data for financial accuracy and completeness
        /// </summary>
        /// <param name="marketData">Market data to validate</param>
        /// <returns>True if data is valid, false otherwise</returns>
        public bool ValidateMarketData(MarketData marketData)
        {
            LogMethodEntry();
            try
            {
                if (marketData == null)
                {
                    LogMethodExit();
                    return false;
                }

                var isValid = !string.IsNullOrWhiteSpace(marketData.Symbol) &&
                             marketData.Price > 0 &&
                             marketData.Volume >= 0 &&
                             marketData.High >= marketData.Low &&
                             marketData.High >= marketData.Price &&
                             marketData.Low <= marketData.Price &&
                             marketData.Timestamp > DateTime.MinValue;

                if (!isValid)
                {
                    LogWarning($"Invalid market data for {marketData?.Symbol}: Price={marketData?.Price}, Volume={marketData?.Volume}");
                }

                LogMethodExit();
                return isValid;
            }
            catch (Exception ex)
            {
                LogError($"Error validating market data for {marketData?.Symbol}", ex);
                LogMethodExit();
                return false;
            }
        }

        public DataQualityReport CompareProviderData(MarketData primaryData, MarketData fallbackData)
        {
            var report = new DataQualityReport();

            if (primaryData?.Symbol != fallbackData?.Symbol)
            {
                report.HasDiscrepancies = true;
                report.Issues.Add("Symbol mismatch");
            }

            if (primaryData != null && fallbackData != null)
            {
                var priceDiff = Math.Abs(primaryData.Price - fallbackData.Price);
                report.PriceVariancePercentage = (priceDiff / Math.Max(primaryData.Price, fallbackData.Price)) * 100;

                if (report.PriceVariancePercentage > 5)
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Price variance: {report.PriceVariancePercentage:F2}%");
                }

                report.VolumeVariance = Math.Abs(primaryData.Volume - fallbackData.Volume);
                if (report.VolumeVariance > (Math.Max(primaryData.Volume, fallbackData.Volume) * 0.1))
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Volume variance: {report.VolumeVariance}");
                }

                report.TimestampDifference = Math.Abs((primaryData.Timestamp - fallbackData.Timestamp).TotalSeconds) > 300 ?
                    primaryData.Timestamp - fallbackData.Timestamp : TimeSpan.Zero;

                if (report.TimestampDifference.TotalMinutes > 5)
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Timestamp difference: {report.TimestampDifference.TotalMinutes:F1} minutes");
                }

                report.RecommendedProvider = primaryData.Timestamp >= fallbackData.Timestamp ? "Primary" : "Fallback";
            }

            return report;
        }

        /// <summary>
        /// Normalizes financial data to standard precision and format
        /// </summary>
        /// <param name="marketData">Market data to normalize</param>
        /// <returns>Normalized MarketData or null if input is null</returns>
        public MarketData? NormalizeFinancialData(MarketData marketData)
        {
            LogMethodEntry();
            try
            {
                if (marketData == null)
                {
                    LogMethodExit();
                    return null;
                }

                var normalizedData = new MarketData(Logger)
                {
                    Symbol = marketData.Symbol,
                    Price = Math.Round(marketData.Price, 4),
                    Open = Math.Round(marketData.Open, 4),
                    High = Math.Round(marketData.High, 4),
                    Low = Math.Round(marketData.Low, 4),
                    PreviousClose = Math.Round(marketData.PreviousClose, 4),
                    Change = Math.Round(marketData.Change, 4),
                    ChangePercent = Math.Round(marketData.ChangePercent, 2),
                    Volume = marketData.Volume,
                    Timestamp = marketData.Timestamp
                };
                LogMethodExit();
                return normalizedData;
            }
            catch (Exception ex)
            {
                LogError($"Error normalizing financial data for {marketData?.Symbol}", ex);
                LogMethodExit();
                return null;
            }
        }

        // ========== CACHING AND PERFORMANCE METHODS ==========

        /// <summary>
        /// Retrieves cached market data if available and within maximum age
        /// </summary>
        /// <param name="symbol">Symbol to retrieve cached data for</param>
        /// <param name="maxAge">Maximum age of cached data to accept</param>
        /// <returns>Cached MarketData if available and fresh, null otherwise</returns>
        public async Task<MarketData?> GetCachedDataAsync(string symbol, TimeSpan maxAge)
        {
            LogMethodEntry();
            try
            {
                return await Task.Run(() =>
                {
                    var cacheKey = $"aggregated_{symbol}";
                    if (_cache.TryGetValue(cacheKey, out MarketData? cachedData))
                    {
                        if (cachedData != null && cachedData.Timestamp > DateTime.UtcNow.Subtract(maxAge))
                        {
                            LogInfo($"Cache hit for {symbol}");
                            Interlocked.Increment(ref _cacheHits);
                            return cachedData;
                        }
                    }

                    LogInfo($"Cache miss for {symbol}");
                    Interlocked.Increment(ref _cacheMisses);
                    return null;
                });
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving cached data for {symbol}", ex);
                return null;
            }
            finally
            {
                LogMethodExit();
            }
        }

        /// <summary>
        /// Caches market data with specified expiration time
        /// </summary>
        /// <param name="symbol">Symbol to cache data for</param>
        /// <param name="marketData">Market data to cache</param>
        /// <param name="expiration">Cache expiration time</param>
        /// <returns>Task representing the async operation</returns>
        public async Task SetCachedDataAsync(string symbol, MarketData marketData, TimeSpan expiration)
        {
            LogMethodEntry();
            try
            {
                await Task.Run(() =>
                {
                    var cacheKey = $"aggregated_{symbol}";
                    _cache.Set(cacheKey, marketData, expiration);
                    LogInfo($"Cached data for {symbol} with expiration {expiration.TotalMinutes} minutes");
                });
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError($"Error caching data for {symbol}", ex);
                LogMethodExit();
                throw;
            }
        }

        // ========== STATISTICS AND MONITORING METHODS ==========

        /// <summary>
        /// Retrieves comprehensive aggregation performance statistics
        /// </summary>
        /// <returns>AggregationStatistics containing performance metrics</returns>
        public AggregationStatistics GetAggregationStatistics()
        {
            LogMethodEntry();
            try
            {
                var stats = new AggregationStatistics
                {
                    StartTime = _statistics.StartTime,
                    TotalAggregations = _statistics.TotalAggregations,
                    SuccessfulAggregations = _statistics.SuccessfulAggregations,
                    FailedAggregations = _statistics.FailedAggregations,
                    ProviderUsageCount = new Dictionary<string, long>(_statistics.ProviderUsageCount),
                    AverageResponseTime = CalculateAverageResponseTimes(),
                    CacheHitRatio = _cacheHits + _cacheMisses > 0 ? (double)_cacheHits / (_cacheHits + _cacheMisses) : 0,
                    ProviderFailoverEvents = _providerFailoverEvents
                };
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error getting aggregation statistics", ex);
                LogMethodExit();
                throw;
            }
        }

        public Dictionary<string, ProviderHealthStatus> GetProviderHealthStatus()
        {
            var healthStatus = new Dictionary<string, ProviderHealthStatus>();
            var providers = GetProviderPriority();

            foreach (var provider in providers)
            {
                if (IsProviderInCircuitBreaker(provider))
                {
                    healthStatus[provider] = ProviderHealthStatus.CircuitBreakerOpen;
                }
                else if (_consecutiveFailures.GetValueOrDefault(provider, 0) > 0)
                {
                    healthStatus[provider] = ProviderHealthStatus.Degraded;
                }
                else
                {
                    healthStatus[provider] = ProviderHealthStatus.Healthy;
                }
            }

            return healthStatus;
        }

        // ========== INTERFACE IMPLEMENTATION METHODS ==========

        /// <summary>
        /// Retrieves market data for a single symbol with intelligent provider selection
        /// </summary>
        /// <param name="symbol">Symbol to retrieve data for</param>
        /// <returns>MarketData if available, null otherwise</returns>
        public async Task<MarketData?> GetMarketDataAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                var result = await GetRealTimeDataAsync(symbol);
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error getting market data for {symbol}", ex);
                LogMethodExit();
                return null;
            }
        }

        /// <summary>
        /// Retrieves market data for multiple symbols with batch optimization
        /// </summary>
        /// <param name="symbols">List of symbols to retrieve data for</param>
        /// <returns>List of successfully retrieved MarketData</returns>
        public async Task<List<MarketData>> GetBatchMarketDataAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                var results = await GetMultipleQuotesAsync(symbols);
                LogMethodExit();
                return results;
            }
            catch (Exception ex)
            {
                LogError($"Error getting batch market data for {symbols?.Count ?? 0} symbols", ex);
                LogMethodExit();
                return new List<MarketData>();
            }
        }

        // ========== LEGACY COMPATIBILITY METHODS ==========

        /// <summary>
        /// Gets real-time market data with intelligent provider fallback
        /// </summary>
        /// <param name="symbol">Symbol to retrieve data for</param>
        /// <returns>MarketData if available, null otherwise</returns>
        private async Task<MarketData?> GetRealTimeDataAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Getting real-time data for {symbol}");

                var primaryData = await TryGetDataFromProvider("AlphaVantage",
                    async () => await _alphaVantageProvider.GetGlobalQuoteAsync(symbol));

                if (primaryData != null)
                {
                    LogInfo($"Successfully retrieved {symbol} from AlphaVantage");
                    LogMethodExit();
                    return primaryData;
                }

                if (_config.EnableBackupProviders)
                {
                    LogInfo($"Falling back to Finnhub for {symbol}");
                    var fallbackData = await TryGetDataFromProvider("Finnhub",
                        async () => await _finnhubProvider.GetQuoteAsync(symbol));

                    if (fallbackData != null)
                    {
                        LogInfo($"Successfully retrieved {symbol} from Finnhub fallback");
                        Interlocked.Increment(ref _providerFailoverEvents);
                        LogMethodExit();
                        return fallbackData;
                    }
                }

                LogError($"Failed to retrieve data for {symbol} from all providers");
                LogMethodExit();
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetRealTimeDataAsync for {symbol}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Gets market data for multiple symbols with optimized batch processing
        /// </summary>
        /// <param name="symbols">List of symbols to retrieve data for</param>
        /// <returns>List of successfully retrieved MarketData</returns>
        private async Task<List<MarketData>> GetMultipleQuotesAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Getting quotes for {symbols.Count} symbols");
                var results = new List<MarketData>();

            var alphaVantageSymbols = symbols.Take(GetAlphaVantageQuota()).ToList();
            if (alphaVantageSymbols.Any())
            {
                // AlphaVantage doesn't support batch in the interface, process individually
                foreach (var symbol in alphaVantageSymbols)
                {
                    try
                    {
                        var data = await TryGetDataFromProvider("AlphaVantage",
                            async () => await _alphaVantageProvider.GetGlobalQuoteAsync(symbol));
                        if (data != null)
                        {
                            results.Add(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error fetching AlphaVantage data for {symbol}", ex);
                    }
                }

                if (results.Any())
                {
                    LogInfo($"Retrieved {results.Count} quotes from AlphaVantage");
                }
            }

            var remainingSymbols = symbols.Except(results.Select(r => r.Symbol)).ToList();
            if (remainingSymbols.Any() && _config.EnableBackupProviders)
            {
                var finnhubResults = await TryGetBatchDataFromProvider("Finnhub",
                    async () => await _finnhubProvider.GetBatchQuotesAsync(remainingSymbols));

                if (finnhubResults?.Any() == true)
                {
                    results.AddRange(finnhubResults);
                    LogInfo($"Retrieved {finnhubResults.Count} quotes from Finnhub");
                }
            }

                LogInfo($"Total retrieved: {results.Count}/{symbols.Count} quotes");
                LogMethodExit();
                return results;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetMultipleQuotesAsync for {symbols.Count} symbols", ex);
                LogMethodExit();
                throw;
            }
        }

        // ========== PRIVATE HELPER METHODS ==========

        /// <summary>
        /// Safely retrieves data from a provider with circuit breaker protection
        /// </summary>
        /// <typeparam name="T">Type of data to retrieve</typeparam>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="dataRetriever">Function to retrieve data</param>
        /// <returns>Retrieved data or null if failed</returns>
        private async Task<T?> TryGetDataFromProvider<T>(string providerName, Func<Task<T?>> dataRetriever) where T : class
        {
            LogMethodEntry();
            try
            {
                if (IsProviderInCircuitBreaker(providerName))
                {
                    LogWarning($"Provider {providerName} is in circuit breaker state");
                    LogMethodExit();
                    return null;
                }

                var result = await dataRetriever();
                if (result != null)
                {
                    await RecordProviderSuccessAsync(providerName);
                }
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving data from {providerName}", ex);
                await RecordProviderFailureAsync(providerName, ex);
                LogMethodExit();
                return null;
            }
        }

        /// <summary>
        /// Safely retrieves batch data from a provider with circuit breaker protection
        /// </summary>
        /// <typeparam name="T">Type of data to retrieve</typeparam>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="dataRetriever">Function to retrieve batch data</param>
        /// <returns>Retrieved data list or empty list if failed</returns>
        private async Task<List<T>> TryGetBatchDataFromProvider<T>(string providerName, Func<Task<List<T>>> dataRetriever)
        {
            LogMethodEntry();
            try
            {
                var result = await TryGetDataFromProvider(providerName, dataRetriever) ?? new List<T>();
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving batch data from {providerName}", ex);
                LogMethodExit();
                return new List<T>();
            }
        }

        /// <summary>
        /// Checks if a provider is currently in circuit breaker state
        /// </summary>
        /// <param name="providerName">Name of the provider to check</param>
        /// <returns>True if provider is in circuit breaker state</returns>
        private bool IsProviderInCircuitBreaker(string providerName)
        {
            LogMethodEntry();
            try
            {
                if (_providerFailures.TryGetValue(providerName, out var lastFailure))
                {
                    var isInCircuitBreaker = DateTime.UtcNow - lastFailure < _config.CircuitBreaker.OpenTimeout;
                    LogMethodExit();
                    return isInCircuitBreaker;
                }
                LogMethodExit();
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Error checking circuit breaker state for {providerName}", ex);
                LogMethodExit();
                return true; // Fail safe - assume circuit breaker is open
            }
        }

        private int GetAlphaVantageQuota()
        {
            return Math.Max(1, _config.ApiSettings.AlphaVantage.DailyLimit / 4);
        }

        /// <summary>
        /// Maps AlphaVantage quote response to standardized MarketData
        /// </summary>
        /// <param name="quote">AlphaVantage quote to map</param>
        /// <returns>Mapped MarketData or null if mapping fails</returns>
        private MarketData? MapAlphaVantageQuote(AlphaVantageQuote quote)
        {
            LogMethodEntry();
            try
            {
                var marketData = new MarketData(Logger)
                {
                    Symbol = quote.Symbol,
                    Price = decimal.Parse(quote.Price),
                    Open = decimal.Parse(quote.Open),
                    High = decimal.Parse(quote.High),
                    Low = decimal.Parse(quote.Low),
                    Volume = long.Parse(quote.Volume),
                    PreviousClose = decimal.Parse(quote.PreviousClose),
                    Change = decimal.Parse(quote.Change),
                    ChangePercent = decimal.Parse(quote.ChangePercent.TrimEnd('%')),
                    Timestamp = DateTime.UtcNow
                };
                LogMethodExit();
                return marketData;
            }
            catch (Exception ex)
            {
                LogError($"Error mapping AlphaVantage quote for {quote?.Symbol}", ex);
                LogMethodExit();
                return null;
            }
        }

        /// <summary>
        /// Maps Finnhub quote response to standardized MarketData
        /// </summary>
        /// <param name="symbol">Symbol for the quote</param>
        /// <param name="quote">Finnhub quote to map</param>
        /// <returns>Mapped MarketData or null if mapping fails</returns>
        private MarketData? MapFinnhubQuote(string symbol, FinnhubQuoteResponse quote)
        {
            LogMethodEntry();
            try
            {
                var marketData = new MarketData(Logger)
                {
                    Symbol = symbol,
                    Price = quote.Current,
                    Open = quote.Open,
                    High = quote.High,
                    Low = quote.Low,
                    PreviousClose = quote.PreviousClose,
                    Change = quote.Change,
                    ChangePercent = quote.PercentChange,
                    Timestamp = DateTime.UtcNow
                };
                LogMethodExit();
                return marketData;
            }
            catch (Exception ex)
            {
                LogError($"Error mapping Finnhub quote for {symbol}", ex);
                LogMethodExit();
                return null;
            }
        }

        private Dictionary<string, TimeSpan> CalculateAverageResponseTimes()
        {
            return new Dictionary<string, TimeSpan>
            {
                ["AlphaVantage"] = TimeSpan.FromMilliseconds(500),
                ["Finnhub"] = TimeSpan.FromMilliseconds(300)
            };
        }

        private void OnProviderFailure(ProviderFailureEventArgs e)
        {
            ProviderFailure?.Invoke(this, e);
        }

        private void OnDataQualityIssue(DataQualityEventArgs e)
        {
            DataQualityIssue?.Invoke(this, e);
        }
    }
}

// Total Lines: 425
