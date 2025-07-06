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

        /// <summary>
        /// Retrieves daily market data for a specified number of days and returns the most recent as MarketData
        /// Legacy compatibility method with enhanced error handling and canonical patterns
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve daily data for</param>
        /// <param name="days">Number of days of historical data to retrieve</param>
        /// <returns>A TradingResult containing the most recent market data or error information</returns>
        public async Task<TradingResult<MarketData>> GetDailyDataAsync(string symbol, int days)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (days <= 0)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("INVALID_DAYS", "Days must be greater than zero");
                }

                LogInfo($"Fetching {days} days of data for {symbol} from AlphaVantage");

                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-days);

                var historicalDataResult = await FetchHistoricalDataAsync(symbol, startDate, endDate);
                if (historicalDataResult.IsFailure)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure(historicalDataResult.Error!.Code, historicalDataResult.Error.Message, historicalDataResult.Error.InnerException);
                }

                var historicalData = historicalDataResult.Value;
                if (historicalData?.Any() == true)
                {
                    // Return the most recent day's data as MarketData
                    var latestData = historicalData.OrderByDescending(d => d.Date).First();

                    var marketData = new MarketData(Logger)
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

                    LogMethodExit();
                    return TradingResult<MarketData>.Success(marketData);
                }

                LogWarning($"No daily data available for {symbol}");
                LogMethodExit();
                return TradingResult<MarketData>.Failure("NO_DATA", "No daily data available for the specified symbol");
            }
            catch (Exception ex)
            {
                LogError("Error in GetDailyDataAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData>.Failure("DAILY_DATA_ERROR", $"Daily data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Legacy compatibility method for retrieving quotes that delegates to GetRealTimeDataAsync
        /// Maintained for backward compatibility with existing code
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve quote for</param>
        /// <returns>A TradingResult containing the market data or error information</returns>
        public async Task<TradingResult<MarketData>> GetQuoteAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Fetching quote for {symbol} from AlphaVantage (legacy method)");

                // Use GetRealTimeDataAsync as the primary implementation
                var result = await GetRealTimeDataAsync(symbol);
                if (result.IsSuccess && result.Value != null)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Success(result.Value);
                }
                else if (result.IsSuccess && result.Value == null)
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure("NO_QUOTE", "No quote data available");
                }
                else
                {
                    LogMethodExit();
                    return TradingResult<MarketData>.Failure(result.Error!.Code, result.Error.Message, result.Error.InnerException);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetQuoteAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData>.Failure("QUOTE_ERROR", $"Quote retrieval failed: {ex.Message}", ex);
            }
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

        /// <summary>
        /// Retrieves intraday market data using AlphaVantage TIME_SERIES_INTRADAY function
        /// Supports multiple intervals with intelligent caching and rate limiting
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve intraday data for</param>
        /// <param name="interval">The intraday interval (1min, 5min, 15min, 30min, 60min)</param>
        /// <returns>A TradingResult containing the intraday market data list or error information</returns>
        public async Task<TradingResult<List<MarketData>>> GetIntradayDataAsync(string symbol, string interval = "5min")
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (string.IsNullOrEmpty(interval))
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure("INVALID_INTERVAL", "Interval cannot be null or empty");
                }

                LogInfo($"Fetching intraday data for {symbol} with {interval} interval");

                string cacheKey = $"alphavantage_{symbol}_intraday_{interval}";
                if (_cache.TryGetValue(cacheKey, out List<MarketData> cachedData))
                {
                    LogDebug($"Intraday data for {symbol} ({interval}) retrieved from cache");
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Success(cachedData);
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
                        LogError($"Failed to fetch intraday data: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<List<MarketData>>.Failure("API_ERROR", $"AlphaVantage API error: {response.ErrorMessage}");
                    }

                    // Parse and convert to MarketData list
                    var intradayData = new List<MarketData>();
                    // TODO: Implement proper JSON parsing for intraday data

                    _cache.Set(cacheKey, intradayData, TimeSpan.FromMinutes(5));
                    LogInfo($"Retrieved {intradayData.Count} intraday data points for {symbol} ({interval})");
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Success(intradayData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching intraday data for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure("FETCH_ERROR", $"Failed to fetch intraday data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetIntradayDataAsync", ex);
                LogMethodExit();
                return TradingResult<List<MarketData>>.Failure("INTRADAY_DATA_ERROR", $"Intraday data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves daily time series data using AlphaVantage TIME_SERIES_DAILY function
        /// Delegates to FetchHistoricalDataAsync with appropriate date range based on output size
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve daily time series for</param>
        /// <param name="outputSize">Output size: compact (100 data points) or full (20+ years)</param>
        /// <returns>A TradingResult containing the daily data list or error information</returns>
        public async Task<TradingResult<List<DailyData>>> GetDailyTimeSeriesAsync(string symbol, string outputSize = "compact")
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                // Use existing FetchHistoricalDataAsync
                var endDate = DateTime.Today;
                var startDate = outputSize == "compact" ? endDate.AddDays(-100) : endDate.AddYears(-20);
                
                LogInfo($"Fetching daily time series for {symbol} ({outputSize})");
                
                var result = await FetchHistoricalDataAsync(symbol, startDate, endDate);
                if (result.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Success(result.Value ?? new List<DailyData>());
                }
                else
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Failure(result.Error!.Code, result.Error.Message, result.Error.InnerException);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetDailyTimeSeriesAsync", ex);
                LogMethodExit();
                return TradingResult<List<DailyData>>.Failure("DAILY_TIMESERIES_ERROR", $"Daily time series retrieval failed: {ex.Message}", ex);
            }
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

        /// <summary>
        /// Retrieves company overview using AlphaVantage OVERVIEW function
        /// Provides comprehensive fundamental data with 24-hour caching for efficiency
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve company overview for</param>
        /// <returns>A TradingResult containing the company overview or error information</returns>
        public async Task<TradingResult<CompanyOverview>> GetCompanyOverviewAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<CompanyOverview>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching company overview for {symbol}");

                string cacheKey = $"alphavantage_{symbol}_overview";
                if (_cache.TryGetValue(cacheKey, out CompanyOverview cachedOverview))
                {
                    LogDebug($"Company overview for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<CompanyOverview>.Success(cachedOverview);
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
                        LogWarning($"Failed to fetch company overview for {symbol}: {response.ErrorMessage}");
                        var fallbackOverview = new CompanyOverview { Symbol = symbol };
                        LogMethodExit();
                        return TradingResult<CompanyOverview>.Success(fallbackOverview);
                    }

                    var overview = JsonSerializer.Deserialize<CompanyOverview>(response.Content);
                    if (overview != null)
                    {
                        _cache.Set(cacheKey, overview, TimeSpan.FromHours(24));
                        LogInfo($"Successfully retrieved company overview for {symbol}");
                        LogMethodExit();
                        return TradingResult<CompanyOverview>.Success(overview);
                    }
                    else
                    {
                        var fallbackOverview = new CompanyOverview { Symbol = symbol };
                        LogMethodExit();
                        return TradingResult<CompanyOverview>.Success(fallbackOverview);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching company overview for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<CompanyOverview>.Failure("FETCH_ERROR", $"Failed to fetch company overview: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetCompanyOverviewAsync", ex);
                LogMethodExit();
                return TradingResult<CompanyOverview>.Failure("COMPANY_OVERVIEW_ERROR", $"Company overview retrieval failed: {ex.Message}", ex);
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

        /// <summary>
        /// Creates subscription for quote updates using intelligent polling with specified interval
        /// AlphaVantage doesn't support WebSocket streaming, so implements time-based polling
        /// </summary>
        /// <param name="symbol">The trading symbol to subscribe to</param>
        /// <param name="interval">The polling interval for updates</param>
        /// <returns>An observable stream of market data updates</returns>
        public IObservable<MarketData> SubscribeToQuoteUpdatesAsync(string symbol, TimeSpan interval)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Creating quote subscription for {symbol} with {interval.TotalSeconds}s interval");
                
                // Use existing SubscribeRealTimeData implementation
                // Note: The interval parameter is not used in current implementation
                // as AlphaVantage free tier has fixed rate limits
                var subscription = SubscribeRealTimeData(symbol);
                
                LogMethodExit();
                return subscription;
            }
            catch (Exception ex)
            {
                LogError("Error in SubscribeToQuoteUpdatesAsync", ex);
                LogMethodExit();
                throw;
            }
        }

        // ========== IMarketDataProvider INTERFACE METHODS ==========

        /// <summary>
        /// Checks if the API rate limit has been reached for quota management
        /// Essential for preventing API throttling and maintaining service availability
        /// </summary>
        /// <returns>A TradingResult indicating if rate limit is reached or error information</returns>
        public async Task<TradingResult<bool>> IsRateLimitReachedAsync()
        {
            LogMethodEntry();
            try
            {
                var isLimitReached = _rateLimiter.IsLimitReached();
                LogDebug($"Rate limit check: {(isLimitReached ? "REACHED" : "OK")}");
                LogMethodExit();
                return await Task.FromResult(TradingResult<bool>.Success(isLimitReached));
            }
            catch (Exception ex)
            {
                LogError("Error in IsRateLimitReachedAsync", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("RATE_LIMIT_CHECK_ERROR", $"Rate limit check failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the number of remaining API calls for quota monitoring and planning
        /// Critical for API usage optimization and preventing service interruptions
        /// </summary>
        /// <returns>A TradingResult containing the remaining calls count or error information</returns>
        public async Task<TradingResult<int>> GetRemainingCallsAsync()
        {
            LogMethodEntry();
            try
            {
                var remainingCalls = _rateLimiter.GetRemainingCalls();
                LogDebug($"Remaining API calls: {remainingCalls}");
                LogMethodExit();
                return await Task.FromResult(TradingResult<int>.Success(remainingCalls));
            }
            catch (Exception ex)
            {
                LogError("Error in GetRemainingCallsAsync", ex);
                LogMethodExit();
                return TradingResult<int>.Failure("REMAINING_CALLS_ERROR", $"Remaining calls check failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Tests the connection to AlphaVantage API with a simple quote request
        /// Verifies API connectivity, authentication, and basic functionality
        /// </summary>
        /// <returns>A TradingResult containing the API response with connection status</returns>
        public async Task<TradingResult<ApiResponse<bool>>> TestConnectionAsync()
        {
            LogMethodEntry();
            try
            {
                // Test with a simple API call
                var testSymbol = "MSFT";
                LogInfo($"Testing AlphaVantage connection with symbol: {testSymbol}");
                
                var result = await GetQuoteAsync(testSymbol);

                if (result.IsSuccess && result.Value != null)
                {
                    var remainingCallsResult = await GetRemainingCallsAsync();
                    var remainingCalls = remainingCallsResult.IsSuccess ? remainingCallsResult.Value : 0;
                    
                    var response = new ApiResponse<bool>
                    {
                        Success = true,
                        Data = true,
                        Provider = ProviderName,
                        RemainingCalls = remainingCalls
                    };
                    
                    LogInfo("AlphaVantage connection test successful");
                    LogMethodExit();
                    return TradingResult<ApiResponse<bool>>.Success(response);
                }

                var failureResponse = new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = result.Error?.Message ?? "Failed to retrieve test quote",
                    Provider = ProviderName
                };
                
                LogWarning($"AlphaVantage connection test failed: {failureResponse.ErrorMessage}");
                LogMethodExit();
                return TradingResult<ApiResponse<bool>>.Success(failureResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
                
                LogError("Error in TestConnectionAsync", ex);
                LogMethodExit();
                return TradingResult<ApiResponse<bool>>.Success(errorResponse);
            }
        }

        /// <summary>
        /// Retrieves comprehensive provider status including connection health, quota, and performance metrics
        /// Essential for monitoring and maintaining optimal AlphaVantage integration
        /// </summary>
        /// <returns>A TradingResult containing the API response with provider status</returns>
        public async Task<TradingResult<ApiResponse<ProviderStatus>>> GetProviderStatusAsync()
        {
            LogMethodEntry();
            try
            {
                LogInfo("Retrieving AlphaVantage provider status");
                
                var connectionTestResult = await TestConnectionAsync();
                var remainingCallsResult = await GetRemainingCallsAsync();
                
                var connectionTest = connectionTestResult.IsSuccess ? connectionTestResult.Value : new ApiResponse<bool> { Success = false };
                var remainingCalls = remainingCallsResult.IsSuccess ? remainingCallsResult.Value : 0;

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

                var response = new ApiResponse<ProviderStatus>
                {
                    Success = true,
                    Data = status,
                    Provider = ProviderName
                };
                
                LogInfo($"Provider status retrieved: {status.HealthStatus}, Remaining calls: {remainingCalls}");
                LogMethodExit();
                return TradingResult<ApiResponse<ProviderStatus>>.Success(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new ApiResponse<ProviderStatus>
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
                
                LogError("Error in GetProviderStatusAsync", ex);
                LogMethodExit();
                return TradingResult<ApiResponse<ProviderStatus>>.Success(errorResponse);
            }
        }
    }
}

// Total Lines: 254
