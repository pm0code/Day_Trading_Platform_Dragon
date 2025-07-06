// File: TradingPlatform.DataIngestion\Providers\AlphaVantageProvider.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using System.Text.Json;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;
using AlphaVantageGlobalQuoteResponse = TradingPlatform.Core.Models.AlphaVantageGlobalQuoteResponse;
using AlphaVantageQuote = TradingPlatform.Core.Models.AlphaVantageQuote;

namespace TradingPlatform.DataIngestion.Providers
{
    /// <summary>
    /// High-performance AlphaVantage data provider for external market data integration
    /// Implements comprehensive rate limiting, caching, and error handling with canonical patterns
    /// All operations use TradingResult pattern for consistent error handling and observability
    /// Maintains sub-second response times through intelligent caching and request optimization
    /// </summary>
    public class AlphaVantageProvider : CanonicalServiceBase, IAlphaVantageProvider
    {
        private readonly RestClient _client;
        private readonly IMemoryCache _cache;
        private readonly IRateLimiter _rateLimiter;
        private readonly IConfigurationService _config;
        private const int CACHE_MINUTES = 5; // Default cache duration

        public string ProviderName => "AlphaVantage";

        /// <summary>
        /// Initializes a new instance of the AlphaVantageProvider with comprehensive dependencies and canonical patterns
        /// </summary>
        /// <param name="logger">Trading logger for comprehensive AlphaVantage operation tracking</param>
        /// <param name="cache">Memory cache for high-performance data caching</param>
        /// <param name="rateLimiter">Rate limiter for API quota management</param>
        /// <param name="config">Configuration service for AlphaVantage API settings</param>
        public AlphaVantageProvider(ITradingLogger logger,
            IMemoryCache cache,
            IRateLimiter rateLimiter,
            IConfigurationService config) : base(logger, "AlphaVantageProvider")
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _client = new RestClient(_config.AlphaVantageBaseUrl);
        }

        /// <summary>
        /// Retrieves real-time market data for a symbol using AlphaVantage GLOBAL_QUOTE function
        /// Implements intelligent caching and rate limiting for optimal performance and API quota management
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve real-time data for</param>
        /// <returns>A TradingResult containing the market data or error information</returns>
        public async Task<TradingResult<MarketData?>> GetRealTimeDataAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching real-time data for {symbol} from AlphaVantage");

                // 1. Check Cache
                string cacheKey = $"alphavantage_{symbol}_realtime";
                if (_cache.TryGetValue(cacheKey, out MarketData? cachedData) &&
                    cachedData != null &&
                    cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-CACHE_MINUTES))
                {
                    LogDebug($"Real-time data for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(cachedData);
                }

                // 2. Rate Limiting
                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    // 3. Construct API Request (using GLOBAL_QUOTE function)
                    var request = new RestRequest()
                        .AddParameter("function", "GLOBAL_QUOTE")
                        .AddParameter("symbol", symbol)
                        .AddParameter("apikey", _config.AlphaVantageApiKey);

                    // 4. Execute API Request
                    RestResponse response = await _client.ExecuteAsync(request);

                    // 5. Error Handling
                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogError($"Error fetching real-time data from AlphaVantage for {symbol}: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("API_ERROR", $"AlphaVantage API error: {response.ErrorMessage}");
                    }

                    // 6. Parse JSON Response using external model
                    var jsonResponse = JsonSerializer.Deserialize<TradingPlatform.Core.Models.AlphaVantageGlobalQuoteResponse>(response.Content);
                    if (jsonResponse?.GlobalQuote == null)
                    {
                        LogError($"Failed to deserialize AlphaVantage response for {symbol}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("DESERIALIZATION_ERROR", "Failed to deserialize AlphaVantage response");
                    }

                    // 7. Map to MarketData (using decimal parsing)
                    var marketDataResult = MapToMarketData(jsonResponse.GlobalQuote);
                    if (marketDataResult.IsFailure)
                    {
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure(marketDataResult.Error!.Code, marketDataResult.Error.Message, marketDataResult.Error.InnerException);
                    }

                    // 8. Cache the Result
                    _cache.Set(cacheKey, marketDataResult.Value, TimeSpan.FromMinutes(CACHE_MINUTES));
                    LogInfo($"Successfully retrieved real-time data for {symbol} from AlphaVantage");
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(marketDataResult.Value);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching real-time data for {symbol} from AlphaVantage", ex);
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("FETCH_ERROR", $"Failed to fetch real-time data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetRealTimeDataAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("REALTIME_DATA_ERROR", $"Real-time data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches historical daily data for a symbol using AlphaVantage TIME_SERIES_DAILY function
        /// Implements intelligent caching and date range filtering for optimal performance
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve historical data for</param>
        /// <param name="startDate">The start date for historical data retrieval</param>
        /// <param name="endDate">The end date for historical data retrieval</param>
        /// <returns>A TradingResult containing the historical data list or error information</returns>
        public async Task<TradingResult<List<DailyData>?>> FetchHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (startDate > endDate)
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Failure("INVALID_DATE_RANGE", "Start date cannot be after end date");
                }

                LogInfo($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                string cacheKey = $"alphavantage_{symbol}_historical_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
                if (_cache.TryGetValue(cacheKey, out List<DailyData> cachedData))
                {
                    LogDebug($"Historical data for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Success(cachedData);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest()
                        .AddParameter("function", "TIME_SERIES_DAILY")
                        .AddParameter("symbol", symbol)
                        .AddParameter("outputsize", "full")
                        .AddParameter("apikey", _config.AlphaVantageApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogError($"Failed to fetch historical data for {symbol}: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<List<DailyData>?>.Failure("API_ERROR", $"AlphaVantage API error: {response.ErrorMessage}");
                    }

                    // Use external model for deserialization
                    var jsonResponse = JsonSerializer.Deserialize<TradingPlatform.Core.Models.AlphaVantageTimeSeriesResponse>(response.Content);
                    if (jsonResponse?.TimeSeries == null)
                    {
                        LogWarning($"No historical data available for {symbol}");
                        var emptyList = new List<DailyData>();
                        LogMethodExit();
                        return TradingResult<List<DailyData>?>.Success(emptyList);
                    }

                    // Convert to DailyData using the new model's method
                    var historicalData = jsonResponse.ToDailyData()
                        .Where(d => d.Date >= startDate && d.Date <= endDate)
                        .OrderBy(d => d.Date)
                        .ToList();

                    // Cache for 1 hour as historical data doesn't change frequently
                    _cache.Set(cacheKey, historicalData, TimeSpan.FromHours(1));
                    LogInfo($"Retrieved {historicalData.Count} historical records for {symbol}");
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Success(historicalData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching historical data for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Failure("FETCH_ERROR", $"Failed to fetch historical data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in FetchHistoricalDataAsync", ex);
                LogMethodExit();
                return TradingResult<List<DailyData>?>.Failure("HISTORICAL_DATA_ERROR", $"Historical data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves batch real-time data for multiple symbols with intelligent rate limiting and error isolation
        /// AlphaVantage doesn't support batch requests, so processes individually with API quota management
        /// </summary>
        /// <param name="symbols">List of trading symbols to retrieve real-time data for</param>
        /// <returns>A TradingResult containing the market data list or error information</returns>
        public async Task<TradingResult<List<MarketData>?>> GetBatchRealTimeDataAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                if (symbols == null || symbols.Count == 0)
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>?>.Failure("INVALID_SYMBOLS", "Symbols list cannot be null or empty");
                }

                LogInfo($"Fetching batch real-time data for {symbols.Count} symbols from AlphaVantage");
                var results = new List<MarketData>();
                var failures = new List<string>();

                try
                {
                    // AlphaVantage doesn't support batch requests, so we process individually
                    foreach (var symbol in symbols)
                    {
                        var marketDataResult = await GetRealTimeDataAsync(symbol);
                        if (marketDataResult.IsSuccess && marketDataResult.Value != null)
                        {
                            results.Add(marketDataResult.Value);
                        }
                        else
                        {
                            failures.Add(symbol);
                            LogWarning($"Failed to retrieve data for {symbol}: {marketDataResult.Error?.Message}");
                        }

                        // Add delay to respect API rate limits (5 requests per minute for free tier)
                        await Task.Delay(TimeSpan.FromSeconds(12));
                    }

                    if (failures.Any())
                    {
                        LogWarning($"Batch request completed with {failures.Count} failures: {string.Join(", ", failures)}");
                    }

                    LogInfo($"Successfully retrieved {results.Count}/{symbols.Count} real-time quotes from AlphaVantage");
                    LogMethodExit();
                    return TradingResult<List<MarketData>?>.Success(results);
                }
                catch (Exception ex)
                {
                    LogError("Exception in batch real-time data retrieval", ex);
                    LogMethodExit();
                    return TradingResult<List<MarketData>?>.Failure("BATCH_FETCH_ERROR", $"Failed to fetch batch data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetBatchRealTimeDataAsync", ex);
                LogMethodExit();
                return TradingResult<List<MarketData>?>.Failure("BATCH_REALTIME_ERROR", $"Batch real-time data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates real-time subscription for market data using intelligent polling
        /// AlphaVantage doesn't support WebSocket streaming, so implements polling-based subscription with rate limiting
        /// </summary>
        /// <param name="symbol">The trading symbol to subscribe to</param>
        /// <returns>An observable stream of market data updates</returns>
        public IObservable<MarketData> SubscribeRealTimeData(string symbol)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Setting up real-time subscription for {symbol} using AlphaVantage polling");

                // AlphaVantage doesn't support WebSocket streaming, so we implement polling-based subscription
                return System.Reactive.Linq.Observable.Create<MarketData>(observer =>
                {
                    var cancellationTokenSource = new System.Threading.CancellationTokenSource();

                    _ = Task.Run(async () =>
                    {
                        while (!cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            try
                            {
                                var dataResult = await GetRealTimeDataAsync(symbol);
                                if (dataResult.IsSuccess && dataResult.Value != null)
                                {
                                    observer.OnNext(dataResult.Value);
                                }
                                else if (dataResult.IsFailure)
                                {
                                    LogWarning($"Failed to get real-time data for subscription {symbol}: {dataResult.Error?.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"Error in real-time subscription for {symbol}", ex);
                                observer.OnError(ex);
                                break;
                            }

                            // Poll every 60 seconds for free tier compliance
                            await Task.Delay(TimeSpan.FromSeconds(60), cancellationTokenSource.Token);
                        }
                    });

                    LogMethodExit();
                    return cancellationTokenSource;
                });
            }
            catch (Exception ex)
            {
                LogError("Error in SubscribeRealTimeData", ex);
                LogMethodExit();
                throw;
            }
        }

        // ========== LEGACY COMPATIBILITY METHODS (Previously NotImplementedException) ==========

        /// <summary>
        /// Legacy compatibility method that delegates to GetRealTimeDataAsync
        /// Maintained for backward compatibility with existing code
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve market data for</param>
        /// <returns>A TradingResult containing the market data or error information</returns>
        public async Task<TradingResult<MarketData?>> FetchMarketDataAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Fetching market data for {symbol} from AlphaVantage (legacy method)");

                // Use GetRealTimeDataAsync as the primary implementation
                var result = await GetRealTimeDataAsync(symbol);
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                LogError("Error in FetchMarketDataAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("FETCH_MARKET_DATA_ERROR", $"Market data fetch failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Legacy compatibility method that delegates to GetBatchRealTimeDataAsync
        /// Maintained for backward compatibility with existing code
        /// </summary>
        /// <param name="symbols">List of trading symbols to retrieve batch quotes for</param>
        /// <returns>A TradingResult containing the market data list or error information</returns>
        public async Task<TradingResult<List<MarketData>>> GetBatchQuotesAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Fetching batch quotes for {symbols?.Count ?? 0} symbols from AlphaVantage (legacy method)");

                // Use GetBatchRealTimeDataAsync as the primary implementation
                var result = await GetBatchRealTimeDataAsync(symbols);
                if (result.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Success(result.Value ?? new List<MarketData>());
                }
                else
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure(result.Error!.Code, result.Error.Message, result.Error.InnerException);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetBatchQuotesAsync", ex);
                LogMethodExit();
                return TradingResult<List<MarketData>>.Failure("BATCH_QUOTES_ERROR", $"Batch quotes retrieval failed: {ex.Message}", ex);
            }
        }

        public async Task<MarketData> GetDailyDataAsync(string symbol, int days)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching {days} days of data for {symbol} from AlphaVantage");

            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-days);

            var historicalData = await FetchHistoricalDataAsync(symbol, startDate, endDate);
            if (historicalData?.Any() == true)
            {
                // Return the most recent day's data as MarketData
                var latestData = historicalData.OrderByDescending(d => d.Date).First();

                return new MarketData(_logger)
                {
                    Symbol = latestData.Symbol,
                    Price = latestData.Close,
                    Open = latestData.Open,
                    High = latestData.High,
                    Low = latestData.Low,
                    Volume = latestData.Volume,
                    PreviousClose = historicalData.Count > 1 ? historicalData.OrderByDescending(d => d.Date).Skip(1).First().Close : latestData.Close,
                    Change = latestData.Close - (historicalData.Count > 1 ? historicalData.OrderByDescending(d => d.Date).Skip(1).First().Close : latestData.Close),
                    ChangePercent = historicalData.Count > 1 ? ((latestData.Close - historicalData.OrderByDescending(d => d.Date).Skip(1).First().Close) / historicalData.OrderByDescending(d => d.Date).Skip(1).First().Close) * 100 : 0,
                    Timestamp = DateTime.UtcNow
                };
            }

            return null;
        }

        public async Task<MarketData> GetQuoteAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching quote for {symbol} from AlphaVantage");

            // Use GetRealTimeDataAsync as the primary implementation
            return await GetRealTimeDataAsync(symbol);
        }

        // ========== PRIVATE HELPER METHODS ==========

        /// <summary>
        /// Maps AlphaVantage quote data to MarketData with comprehensive error handling and financial precision
        /// </summary>
        private TradingResult<MarketData> MapToMarketData(TradingPlatform.Core.Models.AlphaVantageQuote quote)
        {
            LogMethodEntry();
            try
            {
                if (quote == null)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("NULL_QUOTE", "Quote data is null");
                }

                try
                {
                    var marketData = new MarketData(Logger)
                    {
                        Symbol = quote.Symbol,
                        Price = quote.PriceAsDecimal,
                        Open = decimal.TryParse(quote.Open, out var openVal) ? openVal : 0m,
                        High = decimal.TryParse(quote.High, out var highVal) ? highVal : 0m,
                        Low = decimal.TryParse(quote.Low, out var lowVal) ? lowVal : 0m,
                        Volume = quote.VolumeAsLong,
                        PreviousClose = decimal.TryParse(quote.PreviousClose, out var prevVal) ? prevVal : 0m,
                        Change = quote.ChangeAsDecimal,
                        ChangePercent = quote.ChangePercentAsDecimal,
                        Timestamp = DateTime.UtcNow
                    };

                    LogMethodExit();
                    return TradingResult<MarketData>.Success(marketData);
                }
                catch (Exception ex)
                {
                    LogError($"Error mapping AlphaVantage quote data for {quote?.Symbol}", ex);
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("MAPPING_ERROR", $"Failed to map quote data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in MapToMarketData", ex);
                LogMethodExit();
                return TradingResult<MarketData>.Failure("MAPPING_ERROR", $"Quote mapping failed: {ex.Message}", ex);
            }
        }

        // ========== IAlphaVantageProvider SPECIFIC METHODS ==========

        /// <summary>
        /// Gets global quote using AlphaVantage's GLOBAL_QUOTE function with canonical patterns
        /// Delegates to GetRealTimeDataAsync for consistent implementation
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve global quote for</param>
        /// <returns>A TradingResult containing the market data or error information</returns>
        public async Task<TradingResult<MarketData>> GetGlobalQuoteAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                // This is already implemented as GetRealTimeDataAsync
                var result = await GetRealTimeDataAsync(symbol);
                if (result.IsSuccess && result.Value != null)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Success(result.Value);
                }
                else if (result.IsSuccess && result.Value == null)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("NO_DATA", "No global quote data available");
                }
                else
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure(result.Error!.Code, result.Error.Message, result.Error.InnerException);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetGlobalQuoteAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData>.Failure("GLOBAL_QUOTE_ERROR", $"Global quote retrieval failed: {ex.Message}", ex);
            }
        }

        public async Task<List<MarketData>> GetIntradayDataAsync(string symbol, string interval = "5min")
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching intraday data for {symbol} with {interval} interval");

            string cacheKey = $"alphavantage_{symbol}_intraday_{interval}";
            if (_cache.TryGetValue(cacheKey, out List<MarketData> cachedData))
            {
                return cachedData;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "TIME_SERIES_INTRADAY")
                    .AddParameter("symbol", symbol)
                    .AddParameter("interval", interval)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    TradingLogOrchestrator.Instance.LogError($"Failed to fetch intraday data: {response.ErrorMessage}");
                    return new List<MarketData>();
                }

                // Parse and convert to MarketData list
                var intradayData = new List<MarketData>();
                // TODO: Implement proper JSON parsing for intraday data

                _cache.Set(cacheKey, intradayData, TimeSpan.FromMinutes(5));
                return intradayData;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching intraday data for {symbol}", ex);
                return new List<MarketData>();
            }
        }

        public async Task<List<DailyData>> GetDailyTimeSeriesAsync(string symbol, string outputSize = "compact")
        {
            // Use existing FetchHistoricalDataAsync
            var endDate = DateTime.Today;
            var startDate = outputSize == "compact" ? endDate.AddDays(-100) : endDate.AddYears(-20);
            return await FetchHistoricalDataAsync(symbol, startDate, endDate);
        }

        public async Task<List<DailyData>> GetDailyAdjustedTimeSeriesAsync(string symbol, string outputSize = "compact")
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching daily adjusted time series for {symbol}");

            string cacheKey = $"alphavantage_{symbol}_daily_adjusted_{outputSize}";
            if (_cache.TryGetValue(cacheKey, out List<DailyData> cachedData))
            {
                return cachedData;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "TIME_SERIES_DAILY_ADJUSTED")
                    .AddParameter("symbol", symbol)
                    .AddParameter("outputsize", outputSize)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new List<DailyData>();
                }

                // TODO: Implement proper JSON parsing for adjusted daily data
                var adjustedData = new List<DailyData>();

                _cache.Set(cacheKey, adjustedData, TimeSpan.FromHours(1));
                return adjustedData;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching adjusted daily data for {symbol}", ex);
                return new List<DailyData>();
            }
        }

        public async Task<List<DailyData>> GetWeeklyTimeSeriesAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching weekly time series for {symbol}");

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "TIME_SERIES_WEEKLY")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new List<DailyData>();
                }

                // TODO: Implement proper JSON parsing for weekly data
                return new List<DailyData>();
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching weekly data for {symbol}", ex);
                return new List<DailyData>();
            }
        }

        public async Task<List<DailyData>> GetMonthlyTimeSeriesAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching monthly time series for {symbol}");

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "TIME_SERIES_MONTHLY")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new List<DailyData>();
                }

                // TODO: Implement proper JSON parsing for monthly data
                return new List<DailyData>();
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching monthly data for {symbol}", ex);
                return new List<DailyData>();
            }
        }

        public async Task<CompanyOverview> GetCompanyOverviewAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching company overview for {symbol}");

            string cacheKey = $"alphavantage_{symbol}_overview";
            if (_cache.TryGetValue(cacheKey, out CompanyOverview cachedOverview))
            {
                return cachedOverview;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "OVERVIEW")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return new CompanyOverview { Symbol = symbol };
                }

                var overview = JsonSerializer.Deserialize<CompanyOverview>(response.Content);
                if (overview != null)
                {
                    _cache.Set(cacheKey, overview, TimeSpan.FromHours(24));
                }

                return overview ?? new CompanyOverview { Symbol = symbol };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching company overview for {symbol}", ex);
                return new CompanyOverview { Symbol = symbol };
            }
        }

        public async Task<EarningsData> GetEarningsAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching earnings data for {symbol}");

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "EARNINGS")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new EarningsData { Symbol = symbol };
                }

                // TODO: Implement proper JSON parsing for earnings data
                return new EarningsData { Symbol = symbol };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching earnings for {symbol}", ex);
                return new EarningsData { Symbol = symbol };
            }
        }

        public async Task<IncomeStatement> GetIncomeStatementAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching income statement for {symbol}");

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "INCOME_STATEMENT")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new IncomeStatement { Symbol = symbol };
                }

                // TODO: Implement proper JSON parsing for income statement
                return new IncomeStatement { Symbol = symbol };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception fetching income statement for {symbol}", ex);
                return new IncomeStatement { Symbol = symbol };
            }
        }

        public async Task<List<SymbolSearchResult>> SearchSymbolsAsync(string keywords)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Searching symbols with keywords: {keywords}");

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "SYMBOL_SEARCH")
                    .AddParameter("keywords", keywords)
                    .AddParameter("apikey", _config.AlphaVantageApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    return new List<SymbolSearchResult>();
                }

                // TODO: Implement proper JSON parsing for symbol search
                return new List<SymbolSearchResult>();
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception searching symbols", ex);
                return new List<SymbolSearchResult>();
            }
        }

        /// <summary>
        /// Determines if the market is currently open using time-based logic
        /// AlphaVantage doesn't have a dedicated market status endpoint, so uses EST timezone calculations
        /// </summary>
        /// <returns>A TradingResult indicating if the market is open or error information</returns>
        public async Task<TradingResult<bool>> IsMarketOpenAsync()
        {
            LogMethodEntry();
            try
            {
                // AlphaVantage doesn't have a dedicated market status endpoint
                // Use time-based logic
                var now = DateTime.Now;
                var estTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                var isWeekday = estTime.DayOfWeek >= DayOfWeek.Monday && estTime.DayOfWeek <= DayOfWeek.Friday;
                var isMarketHours = estTime.TimeOfDay >= TimeSpan.FromHours(9.5) && estTime.TimeOfDay <= TimeSpan.FromHours(16);

                var isOpen = isWeekday && isMarketHours;
                LogDebug($"Market status check: {(isOpen ? "OPEN" : "CLOSED")} (EST: {estTime:yyyy-MM-dd HH:mm:ss})");
                
                LogMethodExit();
                return await Task.FromResult(TradingResult<bool>.Success(isOpen));
            }
            catch (Exception ex)
            {
                LogError("Error in IsMarketOpenAsync", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("MARKET_STATUS_ERROR", $"Failed to determine market status: {ex.Message}", ex);
            }
        }

        public IObservable<MarketData> SubscribeToQuoteUpdatesAsync(string symbol, TimeSpan interval)
        {
            // Use existing SubscribeRealTimeData implementation
            return SubscribeRealTimeData(symbol);
        }

        // ========== IMarketDataProvider INTERFACE METHODS ==========

        public async Task<bool> IsRateLimitReachedAsync()
        {
            return await Task.FromResult(_rateLimiter.IsLimitReached());
        }

        public async Task<int> GetRemainingCallsAsync()
        {
            return await Task.FromResult(_rateLimiter.GetRemainingCalls());
        }

        public async Task<ApiResponse<bool>> TestConnectionAsync()
        {
            try
            {
                // Test with a simple API call
                var testSymbol = "MSFT";
                var result = await GetQuoteAsync(testSymbol);

                if (result != null)
                {
                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Data = true,
                        Provider = ProviderName,
                        RemainingCalls = await GetRemainingCallsAsync()
                    };
                }

                return new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = "Failed to retrieve test quote",
                    Provider = ProviderName
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
            }
        }

        public async Task<ApiResponse<ProviderStatus>> GetProviderStatusAsync()
        {
            try
            {
                var connectionTest = await TestConnectionAsync();
                var remainingCalls = await GetRemainingCallsAsync();

                var status = new ProviderStatus
                {
                    ProviderName = ProviderName,
                    IsConnected = connectionTest.Success,
                    IsAuthenticated = connectionTest.Success,
                    RemainingQuota = remainingCalls,
                    QuotaResetTime = _rateLimiter.GetResetTime(),
                    SubscriptionTier = "Free",
                    ResponseTimeMs = 0, // TODO: Implement response time tracking
                    LastSuccessfulCall = DateTime.UtcNow,
                    HealthStatus = connectionTest.Success ? "Healthy" : "Unhealthy"
                };

                return new ApiResponse<ProviderStatus>
                {
                    Success = true,
                    Data = status,
                    Provider = ProviderName
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ProviderStatus>
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
            }
        }
    }
}

// Total Lines: 254
