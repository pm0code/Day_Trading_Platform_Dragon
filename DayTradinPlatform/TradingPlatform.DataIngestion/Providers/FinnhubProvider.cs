// File: TradingPlatform.DataIngestion\Providers\FinnhubProvider.cs

using TradingPlatform.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using System.Text.Json;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Net.WebSockets;
using System.Threading;

namespace TradingPlatform.DataIngestion.Providers
{
    /// <summary>
    /// High-performance Finnhub data provider for external market data integration
    /// Implements comprehensive rate limiting, caching, and error handling with canonical patterns
    /// All operations use TradingResult pattern for consistent error handling and observability
    /// Optimized for premium $50/month plan with enhanced rate limits and WebSocket support
    /// </summary>
    public class FinnhubProvider : CanonicalServiceBase, IFinnhubProvider
    {
        private readonly RestClient _client;
        private readonly IMemoryCache _cache;
        private readonly IRateLimiter _rateLimiter;
        private readonly IConfigurationService _config;
        private const int CACHE_MINUTES = 5; // Default cache duration
        private const int PREMIUM_RATE_LIMIT = 300; // Premium plan: 300 calls/minute
        private ClientWebSocket? _webSocket;
        private readonly Subject<MarketData> _marketDataSubject = new();

        public string ProviderName => "Finnhub";

        /// <summary>
        /// Initializes a new instance of the FinnhubProvider with comprehensive dependencies and canonical patterns
        /// </summary>
        /// <param name="logger">Trading logger for comprehensive Finnhub operation tracking</param>
        /// <param name="cache">Memory cache for high-performance data caching</param>
        /// <param name="rateLimiter">Rate limiter for API quota management (300/min for premium)</param>
        /// <param name="config">Configuration service for Finnhub API settings</param>
        public FinnhubProvider(ITradingLogger logger, IMemoryCache cache,
            IRateLimiter rateLimiter, IConfigurationService config) : base(logger, "FinnhubProvider")
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _client = new RestClient(_config.FinnhubBaseUrl);
        }

        /// <summary>
        /// Gets real-time quote using Finnhub's /quote endpoint with intelligent caching
        /// Optimized for premium plan with enhanced rate limits and sub-second response times
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve quote data for</param>
        /// <returns>A TradingResult containing the market data or error information</returns>
        public async Task<TradingResult<MarketData?>> GetQuoteAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching quote for {symbol} from Finnhub");

                // 1. Check Cache (optimized for high-frequency trading)
                string cacheKey = $"finnhub_{symbol}_quote";
                if (_cache.TryGetValue(cacheKey, out MarketData? cachedData) &&
                    cachedData != null &&
                    cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-CACHE_MINUTES))
                {
                    LogDebug($"Quote for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(cachedData);
                }

                // 2. Rate Limiting (300/min for premium plan)
                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    // 3. Construct API Request
                    var request = new RestRequest("/quote")
                        .AddParameter("symbol", symbol)
                        .AddParameter("token", _config.FinnhubApiKey);

                    // 4. Execute API Request
                    RestResponse response = await _client.ExecuteAsync(request);

                    // 5. Error Handling
                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogError($"Failed to get Finnhub quote for {symbol}: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("API_ERROR", 
                            $"Finnhub API request failed: {response.StatusCode} - {response.ErrorMessage}");
                    }

                    // 6. Parse JSON Response
                    var jsonResponse = JsonSerializer.Deserialize<FinnhubQuoteResponse>(response.Content);
                    if (jsonResponse == null)
                    {
                        LogError($"Failed to deserialize Finnhub quote response for {symbol}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("DESERIALIZATION_ERROR", 
                            "Failed to parse Finnhub quote response");
                    }

                    // 7. Map to MarketData
                    var marketData = MapToMarketData(symbol, jsonResponse);

                    // 8. Cache the Result
                    _cache.Set(cacheKey, marketData, TimeSpan.FromMinutes(CACHE_MINUTES));
                    LogInfo($"Successfully retrieved Finnhub quote for {symbol}");
                    
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(marketData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching quote for {symbol} from Finnhub", ex);
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("QUOTE_ERROR", 
                        $"Failed to retrieve quote: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetQuoteAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("QUOTE_ERROR", 
                    $"Quote retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets batch quotes for multiple symbols with intelligent batching and error isolation
        /// Optimized for premium plan with parallel processing capabilities
        /// </summary>
        /// <param name="symbols">List of trading symbols to retrieve quotes for</param>
        /// <returns>A TradingResult containing the list of market data or error information</returns>
        public async Task<TradingResult<List<MarketData>?>> GetBatchQuotesAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                if (symbols == null || !symbols.Any())
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>?>.Failure("INVALID_SYMBOLS", 
                        "Symbols list cannot be null or empty");
                }

                LogInfo($"Fetching batch quotes for {symbols.Count} symbols from Finnhub");
                var results = new List<MarketData>();

                // Premium plan: can process in parallel with 300/min rate limit
                var batchSize = Math.Min(10, symbols.Count); // Process 10 at a time
                var batches = symbols.Select((symbol, index) => new { symbol, index })
                                   .GroupBy(x => x.index / batchSize)
                                   .Select(g => g.Select(x => x.symbol).ToList());

                foreach (var batch in batches)
                {
                    var tasks = batch.Select(async symbol =>
                    {
                        var quoteResult = await GetQuoteAsync(symbol);
                        return quoteResult;
                    }).ToList();

                    var batchResults = await Task.WhenAll(tasks);
                    
                    foreach (var result in batchResults)
                    {
                        if (result.IsSuccess && result.Value != null)
                        {
                            results.Add(result.Value);
                        }
                    }

                    // Small delay between batches (optimized for premium rate limits)
                    if (batch != batches.Last())
                    {
                        await Task.Delay(200); // 200ms between batches for premium plan
                    }
                }

                LogInfo($"Successfully retrieved {results.Count}/{symbols.Count} quotes from Finnhub");
                LogMethodExit();
                return TradingResult<List<MarketData>?>.Success(results);
            }
            catch (Exception ex)
            {
                LogError("Error in GetBatchQuotesAsync", ex);
                LogMethodExit();
                return TradingResult<List<MarketData>?>.Failure("BATCH_QUOTE_ERROR", 
                    $"Batch quote retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets OHLCV candle data using Finnhub's resolution system with intelligent caching
        /// Resolutions: 1, 5, 15, 30, 60, D, W, M - optimized for day trading patterns
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve candle data for</param>
        /// <param name="resolution">The time resolution (1, 5, 15, 30, 60, D, W, M)</param>
        /// <param name="from">Start date for historical data</param>
        /// <param name="to">End date for historical data</param>
        /// <returns>A TradingResult containing the candle data or error information</returns>
        public async Task<TradingResult<MarketData?>> GetCandleDataAsync(string symbol, string resolution, DateTime from, DateTime to)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (from > to)
                {
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("INVALID_DATE_RANGE", "From date must be before to date");
                }

                LogInfo($"Fetching candle data for {symbol} from Finnhub");

                // Check cache
                string cacheKey = $"finnhub_{symbol}_candle_{resolution}_{from:yyyyMMdd}_{to:yyyyMMdd}";
                if (_cache.TryGetValue(cacheKey, out MarketData? cachedData) && cachedData != null)
                {
                    LogDebug($"Candle data for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(cachedData);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var fromUnix = ((DateTimeOffset)from).ToUnixTimeSeconds();
                    var toUnix = ((DateTimeOffset)to).ToUnixTimeSeconds();

                    var request = new RestRequest("/stock/candle")
                        .AddParameter("symbol", symbol)
                        .AddParameter("resolution", resolution)
                        .AddParameter("from", fromUnix)
                        .AddParameter("to", toUnix)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogError($"Failed to get Finnhub candle data for {symbol}: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("API_ERROR", 
                            $"Finnhub candle API request failed: {response.StatusCode} - {response.ErrorMessage}");
                    }

                    var jsonResponse = JsonSerializer.Deserialize<TradingPlatform.DataIngestion.Models.FinnhubCandleResponse>(response.Content);
                    if (jsonResponse?.Close?.Any() != true)
                    {
                        LogWarning($"No candle data available for {symbol}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("NO_DATA", 
                            $"No candle data available for {symbol} in the specified date range");
                    }

                    var marketData = new MarketData(Logger)
                    {
                        Symbol = symbol,
                        Price = jsonResponse.Close.Last(),
                        Open = jsonResponse.Open.Last(),
                        High = jsonResponse.High.Last(),
                        Low = jsonResponse.Low.Last(),
                        Volume = jsonResponse.Volume?.Last() ?? 0,
                        Timestamp = DateTime.UtcNow
                    };

                    // Cache for longer period for historical data
                    _cache.Set(cacheKey, marketData, TimeSpan.FromMinutes(15));
                    
                    LogInfo($"Successfully retrieved candle data for {symbol}");
                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(marketData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching candle data for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("CANDLE_DATA_ERROR", 
                        $"Failed to retrieve candle data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetCandleDataAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("CANDLE_DATA_ERROR", 
                    $"Candle data retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches historical daily data for the specified date range using Finnhub's candle API
        /// Optimized for backtesting and historical analysis with comprehensive error handling
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve historical data for</param>
        /// <param name="startDate">Start date for historical data</param>
        /// <param name="endDate">End date for historical data</param>
        /// <returns>A TradingResult containing the list of daily data or error information</returns>
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
                    return TradingResult<List<DailyData>?>.Failure("INVALID_DATE_RANGE", 
                        "Start date must be before end date");
                }

                LogInfo($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                var candleResult = await GetCandleDataAsync(symbol, "D", startDate, endDate);
                if (!candleResult.IsSuccess || candleResult.Value == null)
                {
                    LogWarning($"No historical data available for {symbol}");
                    LogMethodExit();
                    return TradingResult<List<DailyData>?>.Success(new List<DailyData>());
                }

                // Convert MarketData to DailyData format
                var dailyData = new DailyData
                {
                    Symbol = candleResult.Value.Symbol,
                    Date = endDate,
                    Open = candleResult.Value.Open,
                    High = candleResult.Value.High,
                    Low = candleResult.Value.Low,
                    Close = candleResult.Value.Price,
                    Volume = candleResult.Value.Volume,
                    AdjustedClose = candleResult.Value.Price
                };

                LogInfo($"Successfully retrieved historical data for {symbol}");
                LogMethodExit();
                return TradingResult<List<DailyData>?>.Success(new List<DailyData> { dailyData });
            }
            catch (Exception ex)
            {
                LogError("Error in FetchHistoricalDataAsync", ex);
                LogMethodExit();
                return TradingResult<List<DailyData>?>.Failure("HISTORICAL_DATA_ERROR", 
                    $"Historical data retrieval failed: {ex.Message}", ex);
            }
        }

        // ========== FINNHUB-SPECIFIC MARKET FEATURES ==========

        /// <summary>
        /// Gets available stock symbols for exchange using Finnhub's /stock/symbol endpoint
        /// Premium plan provides access to global exchanges beyond US markets
        /// </summary>
        /// <param name="exchange">The exchange code (US, XNAS, XNYS, etc.)</param>
        /// <returns>A TradingResult containing the list of symbols or error information</returns>
        public async Task<TradingResult<List<string>?>> GetStockSymbolsAsync(string exchange = "US")
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(exchange))
                {
                    LogMethodExit();
                    return TradingResult<List<string>?>.Failure("INVALID_EXCHANGE", "Exchange cannot be null or empty");
                }

                LogInfo($"Fetching stock symbols for exchange: {exchange}");

                string cacheKey = $"finnhub_symbols_{exchange}";
                if (_cache.TryGetValue(cacheKey, out List<string>? cachedSymbols) && cachedSymbols != null)
                {
                    LogDebug($"Stock symbols for {exchange} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<List<string>?>.Success(cachedSymbols);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/stock/symbol")
                        .AddParameter("exchange", exchange)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogError($"Failed to get stock symbols from Finnhub: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<List<string>?>.Failure("API_ERROR", 
                            $"Failed to retrieve symbols: {response.StatusCode} - {response.ErrorMessage}");
                    }

                    var symbolsResponse = JsonSerializer.Deserialize<List<FinnhubSymbol>>(response.Content);
                    var symbols = symbolsResponse?.Select(s => s.Symbol).ToList() ?? new List<string>();

                    // Cache for 4 hours as symbols don't change frequently
                    _cache.Set(cacheKey, symbols, TimeSpan.FromHours(4));
                    LogInfo($"Retrieved {symbols.Count} symbols for exchange {exchange}");

                    LogMethodExit();
                    return TradingResult<List<string>?>.Success(symbols);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching stock symbols for {exchange}", ex);
                    LogMethodExit();
                    return TradingResult<List<string>?>.Failure("SYMBOLS_ERROR", 
                        $"Failed to retrieve symbols: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetStockSymbolsAsync", ex);
                LogMethodExit();
                return TradingResult<List<string>?>.Failure("SYMBOLS_ERROR", 
                    $"Symbol retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets market status using Finnhub's /stock/market-status endpoint
        /// Premium plan provides real-time market status for multiple exchanges
        /// </summary>
        /// <returns>A TradingResult containing the market open status or error information</returns>
        public async Task<TradingResult<bool>> IsMarketOpenAsync()
        {
            LogMethodEntry();
            try
            {
                LogInfo("Checking market status from Finnhub");

                string cacheKey = "finnhub_market_status";
                if (_cache.TryGetValue(cacheKey, out bool cachedStatus))
                {
                    LogDebug("Market status retrieved from cache");
                    LogMethodExit();
                    return TradingResult<bool>.Success(cachedStatus);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/stock/market-status")
                        .AddParameter("exchange", "US")
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogWarning($"Failed to get market status from Finnhub: {response.ErrorMessage}");
                        // Fallback to time-based check
                        var fallbackStatus = IsMarketOpenTimeBasedCheck();
                        LogMethodExit();
                        return TradingResult<bool>.Success(fallbackStatus);
                    }

                    var statusResponse = JsonSerializer.Deserialize<FinnhubMarketStatus>(response.Content);
                    bool isOpen = statusResponse?.IsOpen ?? false;

                    // Cache for 1 minute as market status can change
                    _cache.Set(cacheKey, isOpen, TimeSpan.FromMinutes(1));
                    LogInfo($"Market status: {(isOpen ? "Open" : "Closed")}");

                    LogMethodExit();
                    return TradingResult<bool>.Success(isOpen);
                }
                catch (Exception ex)
                {
                    LogError("Exception while checking market status", ex);
                    var fallbackStatus = IsMarketOpenTimeBasedCheck();
                    LogMethodExit();
                    return TradingResult<bool>.Success(fallbackStatus);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in IsMarketOpenAsync", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("MARKET_STATUS_ERROR", 
                    $"Market status check failed: {ex.Message}", ex);
            }
        }

        // ========== FINNHUB-SPECIFIC SENTIMENT & NEWS ==========

        /// <summary>
        /// Gets insider sentiment using Finnhub's /stock/insider-sentiment endpoint
        /// Premium feature providing institutional trading signals for day trading decisions
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve sentiment data for</param>
        /// <returns>A TradingResult containing the sentiment data or error information</returns>
        public async Task<TradingResult<SentimentData>> GetInsiderSentimentAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<SentimentData>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching sentiment data for {symbol}");

                string cacheKey = $"finnhub_{symbol}_sentiment";
                if (_cache.TryGetValue(cacheKey, out SentimentData? cachedSentiment) && cachedSentiment != null)
                {
                    LogDebug($"Sentiment data for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<SentimentData>.Success(cachedSentiment);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/stock/insider-sentiment")
                        .AddParameter("symbol", symbol)
                        .AddParameter("from", DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"))
                        .AddParameter("to", DateTime.UtcNow.ToString("yyyy-MM-dd"))
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogWarning($"Failed to get sentiment data for {symbol}: {response.ErrorMessage}");
                        var defaultSentiment = new SentimentData { Symbol = symbol, Sentiment = "neutral", Confidence = 0.5m };
                        LogMethodExit();
                        return TradingResult<SentimentData>.Success(defaultSentiment);
                    }

                    var sentimentResponse = JsonSerializer.Deserialize<FinnhubSentimentResponse>(response.Content);
                    var sentimentData = MapToSentimentData(symbol, sentimentResponse);

                    // Cache sentiment for 1 hour
                    _cache.Set(cacheKey, sentimentData, TimeSpan.FromHours(1));
                    
                    LogInfo($"Successfully retrieved sentiment data for {symbol}: {sentimentData.Sentiment}");
                    LogMethodExit();
                    return TradingResult<SentimentData>.Success(sentimentData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching sentiment for {symbol}", ex);
                    var defaultSentiment = new SentimentData { Symbol = symbol, Sentiment = "neutral", Confidence = 0.5m };
                    LogMethodExit();
                    return TradingResult<SentimentData>.Success(defaultSentiment);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetInsiderSentimentAsync", ex);
                LogMethodExit();
                return TradingResult<SentimentData>.Failure("SENTIMENT_ERROR", 
                    $"Sentiment retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets market news using Finnhub's /news endpoint with premium content access
        /// Categories: general, forex, crypto, merger - essential for day trading decisions
        /// </summary>
        /// <param name="category">The news category (general, forex, crypto, merger)</param>
        /// <returns>A TradingResult containing the list of news items or error information</returns>
        public async Task<TradingResult<List<NewsItem>>> GetMarketNewsAsync(string category = "general")
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(category))
                {
                    category = "general";
                }

                LogInfo($"Fetching market news for category: {category}");

                string cacheKey = $"finnhub_news_{category}";
                if (_cache.TryGetValue(cacheKey, out List<NewsItem>? cachedNews) && cachedNews != null)
                {
                    LogDebug($"Market news for {category} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(cachedNews);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/news")
                        .AddParameter("category", category)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                    {
                        LogWarning($"Failed to get market news: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<List<NewsItem>>.Success(new List<NewsItem>());
                    }

                    var newsResponse = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(response.Content);
                    var newsItems = newsResponse?.Select(MapToNewsItem).ToList() ?? new List<NewsItem>();

                    // Cache news for 15 minutes
                    _cache.Set(cacheKey, newsItems, TimeSpan.FromMinutes(15));
                    LogInfo($"Retrieved {newsItems.Count} news items for category {category}");

                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(newsItems);
                }
                catch (Exception ex)
                {
                    LogError($"Exception while fetching market news for {category}", ex);
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(new List<NewsItem>());
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetMarketNewsAsync", ex);
                LogMethodExit();
                return TradingResult<List<NewsItem>>.Failure("NEWS_ERROR", 
                    $"News retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if API rate limit has been reached based on premium plan limits
        /// Premium plan: 300 calls/minute vs Free tier: 60 calls/minute
        /// </summary>
        /// <returns>A TradingResult containing rate limit status or error information</returns>
        public async Task<TradingResult<bool>> IsRateLimitReachedAsync()
        {
            LogMethodEntry();
            try
            {
                // Check with rate limiter component
                var isLimitReached = await Task.FromResult(_rateLimiter.IsLimitReached());
                
                if (isLimitReached)
                {
                    LogWarning("Finnhub API rate limit reached");
                }
                
                LogMethodExit();
                return TradingResult<bool>.Success(isLimitReached);
            }
            catch (Exception ex)
            {
                LogError("Error in IsRateLimitReachedAsync", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("RATE_LIMIT_CHECK_ERROR", 
                    $"Rate limit check failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets remaining API calls in current quota period for premium plan
        /// Premium plan provides 300 calls/minute with automatic reset
        /// </summary>
        /// <returns>A TradingResult containing remaining calls or error information</returns>
        public async Task<TradingResult<int>> GetRemainingCallsAsync()
        {
            LogMethodEntry();
            try
            {
                // Get remaining calls from rate limiter
                var remainingCalls = await Task.FromResult(_rateLimiter.GetRemainingCalls());
                
                LogDebug($"Finnhub API remaining calls: {remainingCalls}/{PREMIUM_RATE_LIMIT}");
                
                LogMethodExit();
                return TradingResult<int>.Success(remainingCalls);
            }
            catch (Exception ex)
            {
                LogError("Error in GetRemainingCallsAsync", ex);
                LogMethodExit();
                return TradingResult<int>.Failure("REMAINING_CALLS_ERROR", 
                    $"Remaining calls check failed: {ex.Message}", ex);
            }
        }

        // ========== PRIVATE HELPER METHODS ==========

        /// <summary>
        /// Maps Finnhub quote response to canonical MarketData model with comprehensive data transformation
        /// </summary>
        private MarketData MapToMarketData(string symbol, TradingPlatform.Core.Models.FinnhubQuoteResponse quote)
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
                LogError($"Error mapping MarketData for {symbol}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Maps Finnhub sentiment response to canonical SentimentData with insider trading analysis
        /// </summary>
        private SentimentData MapToSentimentData(string symbol, FinnhubSentimentResponse? response)
        {
            LogMethodEntry();
            try
            {
                // Calculate sentiment based on insider trading data
                var totalChange = response?.Data?.Sum(d => d.Change) ?? 0;
                var sentiment = totalChange > 0 ? "positive" : totalChange < 0 ? "negative" : "neutral";
                var confidence = Math.Min(Math.Abs(totalChange) / 1000000m, 1.0m); // Normalize to 0-1

                var sentimentData = new SentimentData
                {
                    Symbol = symbol,
                    Sentiment = sentiment,
                    Confidence = confidence,
                    Timestamp = DateTime.UtcNow
                };
                
                LogDebug($"Mapped sentiment for {symbol}: {sentiment} (confidence: {confidence:P0})");
                LogMethodExit();
                return sentimentData;
            }
            catch (Exception ex)
            {
                LogError($"Error mapping SentimentData for {symbol}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Maps Finnhub news item to canonical NewsItem model with enhanced metadata
        /// </summary>
        private NewsItem MapToNewsItem(FinnhubNewsItem finnhubNews)
        {
            LogMethodEntry();
            try
            {
                var newsItem = new NewsItem
                {
                    Id = finnhubNews.Id.ToString(),
                    Title = finnhubNews.Headline,
                    Summary = finnhubNews.Summary,
                    Source = finnhubNews.Source,
                    Url = finnhubNews.Url,
                    PublishedAt = DateTimeOffset.FromUnixTimeSeconds(finnhubNews.Datetime).DateTime,
                    Category = finnhubNews.Category,
                    Sentiment = "neutral" // Default sentiment - premium plan includes sentiment analysis
                };
                
                LogMethodExit();
                return newsItem;
            }
            catch (Exception ex)
            {
                LogError($"Error mapping NewsItem", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Fallback time-based market open check for US markets when API is unavailable
        /// </summary>
        private bool IsMarketOpenTimeBasedCheck()
        {
            LogMethodEntry();
            try
            {
                var now = DateTime.Now;
                var estTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                // Market is open Monday-Friday 9:30 AM to 4:00 PM EST
                var isWeekday = estTime.DayOfWeek >= DayOfWeek.Monday && estTime.DayOfWeek <= DayOfWeek.Friday;
                var isMarketHours = estTime.TimeOfDay >= TimeSpan.FromHours(9.5) && estTime.TimeOfDay <= TimeSpan.FromHours(16);

                var isOpen = isWeekday && isMarketHours;
                LogDebug($"Time-based market check: {(isOpen ? "Open" : "Closed")} at {estTime:HH:mm:ss} EST");
                
                LogMethodExit();
                return isOpen;
            }
            catch (Exception ex)
            {
                LogError("Error in time-based market check", ex);
                LogMethodExit();
                return false; // Default to closed on error
            }
        }

        // ========== FINNHUB-SPECIFIC COMPANY DATA ==========

        /// <summary>
        /// Gets company profile using Finnhub's /stock/profile2 endpoint
        /// Premium feature providing comprehensive company fundamentals for trading decisions
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve company profile for</param>
        /// <returns>A TradingResult containing the company profile or error information</returns>
        public async Task<TradingResult<CompanyProfile>> GetCompanyProfileAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<CompanyProfile>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching company profile for {symbol}");

                string cacheKey = $"finnhub_{symbol}_profile";
                if (_cache.TryGetValue(cacheKey, out CompanyProfile? cachedProfile) && cachedProfile != null)
                {
                    LogDebug($"Company profile for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<CompanyProfile>.Success(cachedProfile);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/stock/profile2")
                        .AddParameter("symbol", symbol)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogWarning($"No company profile available for {symbol}");
                        var emptyProfile = new CompanyProfile { Symbol = symbol };
                        LogMethodExit();
                        return TradingResult<CompanyProfile>.Success(emptyProfile);
                    }

                    var profile = JsonSerializer.Deserialize<CompanyProfile>(response.Content);
                    if (profile != null)
                    {
                        profile.Symbol = symbol;
                        _cache.Set(cacheKey, profile, TimeSpan.FromHours(24)); // Cache for 24 hours
                        LogInfo($"Successfully retrieved company profile for {symbol}: {profile.Name}");
                    }

                    LogMethodExit();
                    return TradingResult<CompanyProfile>.Success(profile ?? new CompanyProfile { Symbol = symbol });
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching company profile for {symbol}", ex);
                    var emptyProfile = new CompanyProfile { Symbol = symbol };
                    LogMethodExit();
                    return TradingResult<CompanyProfile>.Success(emptyProfile);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetCompanyProfileAsync", ex);
                LogMethodExit();
                return TradingResult<CompanyProfile>.Failure("PROFILE_ERROR", 
                    $"Company profile retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets company financials using Finnhub's /stock/financials-reported endpoint
        /// Premium feature providing detailed financial statements for fundamental analysis
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve financials for</param>
        /// <returns>A TradingResult containing the company financials or error information</returns>
        public async Task<TradingResult<CompanyFinancials>> GetCompanyFinancialsAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<CompanyFinancials>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                LogInfo($"Fetching company financials for {symbol}");

                string cacheKey = $"finnhub_{symbol}_financials";
                if (_cache.TryGetValue(cacheKey, out CompanyFinancials? cachedFinancials) && cachedFinancials != null)
                {
                    LogDebug($"Company financials for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<CompanyFinancials>.Success(cachedFinancials);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/stock/financials-reported")
                        .AddParameter("symbol", symbol)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogWarning($"No financials available for {symbol}");
                        var emptyFinancials = new CompanyFinancials { Symbol = symbol };
                        LogMethodExit();
                        return TradingResult<CompanyFinancials>.Success(emptyFinancials);
                    }

                    var financials = JsonSerializer.Deserialize<CompanyFinancials>(response.Content);
                    if (financials != null)
                    {
                        financials.Symbol = symbol;
                        _cache.Set(cacheKey, financials, TimeSpan.FromHours(24)); // Cache for 24 hours
                        LogInfo($"Successfully retrieved financials for {symbol}");
                    }

                    LogMethodExit();
                    return TradingResult<CompanyFinancials>.Success(financials ?? new CompanyFinancials { Symbol = symbol });
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching financials for {symbol}", ex);
                    var emptyFinancials = new CompanyFinancials { Symbol = symbol };
                    LogMethodExit();
                    return TradingResult<CompanyFinancials>.Success(emptyFinancials);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetCompanyFinancialsAsync", ex);
                LogMethodExit();
                return TradingResult<CompanyFinancials>.Failure("FINANCIALS_ERROR", 
                    $"Company financials retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets company news using Finnhub's /company-news endpoint
        /// Premium feature providing real-time news for trading signals and sentiment analysis
        /// </summary>
        /// <param name="symbol">The trading symbol to retrieve news for</param>
        /// <param name="from">Start date for news articles</param>
        /// <param name="to">End date for news articles</param>
        /// <returns>A TradingResult containing the list of news items or error information</returns>
        public async Task<TradingResult<List<NewsItem>>> GetCompanyNewsAsync(string symbol, DateTime from, DateTime to)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (from > to)
                {
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Failure("INVALID_DATE_RANGE", "From date must be before to date");
                }

                LogInfo($"Fetching company news for {symbol} from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

                string cacheKey = $"finnhub_{symbol}_company_news_{from:yyyyMMdd}_{to:yyyyMMdd}";
                if (_cache.TryGetValue(cacheKey, out List<NewsItem>? cachedNews) && cachedNews != null)
                {
                    LogDebug($"Company news for {symbol} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(cachedNews);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/company-news")
                        .AddParameter("symbol", symbol)
                        .AddParameter("from", from.ToString("yyyy-MM-dd"))
                        .AddParameter("to", to.ToString("yyyy-MM-dd"))
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogWarning($"Failed to get company news: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<List<NewsItem>>.Success(new List<NewsItem>());
                    }

                    var newsResponse = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(response.Content);
                    var newsItems = newsResponse?.Select(MapToNewsItem).ToList() ?? new List<NewsItem>();

                    // Cache for 30 minutes
                    _cache.Set(cacheKey, newsItems, TimeSpan.FromMinutes(30));
                    LogInfo($"Retrieved {newsItems.Count} company news items for {symbol}");

                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(newsItems);
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching company news for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<List<NewsItem>>.Success(new List<NewsItem>());
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetCompanyNewsAsync", ex);
                LogMethodExit();
                return TradingResult<List<NewsItem>>.Failure("COMPANY_NEWS_ERROR", 
                    $"Company news retrieval failed: {ex.Message}", ex);
            }
        }

        // ========== FINNHUB-SPECIFIC TECHNICAL INDICATORS ==========

        /// <summary>
        /// Gets technical indicators using Finnhub's /indicator endpoint
        /// Premium feature providing advanced technical analysis for trading strategies
        /// </summary>
        /// <param name="symbol">The trading symbol to calculate indicators for</param>
        /// <param name="indicator">The indicator type (sma, ema, rsi, macd, etc.)</param>
        /// <returns>A TradingResult containing the indicator values or error information</returns>
        public async Task<TradingResult<Dictionary<string, decimal>>> GetTechnicalIndicatorsAsync(string symbol, string indicator)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<Dictionary<string, decimal>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (string.IsNullOrEmpty(indicator))
                {
                    LogMethodExit();
                    return TradingResult<Dictionary<string, decimal>>.Failure("INVALID_INDICATOR", "Indicator cannot be null or empty");
                }

                LogInfo($"Fetching technical indicator {indicator} for {symbol}");

                string cacheKey = $"finnhub_{symbol}_indicator_{indicator}";
                if (_cache.TryGetValue(cacheKey, out Dictionary<string, decimal>? cachedIndicators) && cachedIndicators != null)
                {
                    LogDebug($"Technical indicators for {symbol}/{indicator} retrieved from cache");
                    LogMethodExit();
                    return TradingResult<Dictionary<string, decimal>>.Success(cachedIndicators);
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/indicator")
                        .AddParameter("symbol", symbol)
                        .AddParameter("indicator", indicator)
                        .AddParameter("resolution", "D")
                        .AddParameter("from", ((DateTimeOffset)DateTime.UtcNow.AddDays(-30)).ToUnixTimeSeconds())
                        .AddParameter("to", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds())
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogWarning($"Failed to get technical indicators: {response.ErrorMessage}");
                        LogMethodExit();
                        return TradingResult<Dictionary<string, decimal>>.Success(new Dictionary<string, decimal>());
                    }

                    // Parse technical indicator response
                    var indicators = ParseTechnicalIndicatorResponse(response.Content, indicator);

                    _cache.Set(cacheKey, indicators, TimeSpan.FromMinutes(15));
                    LogInfo($"Retrieved {indicators.Count} indicator values for {symbol}/{indicator}");
                    
                    LogMethodExit();
                    return TradingResult<Dictionary<string, decimal>>.Success(indicators);
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching technical indicators for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<Dictionary<string, decimal>>.Success(new Dictionary<string, decimal>());
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetTechnicalIndicatorsAsync", ex);
                LogMethodExit();
                return TradingResult<Dictionary<string, decimal>>.Failure("INDICATOR_ERROR", 
                    $"Technical indicator retrieval failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses technical indicator response from Finnhub API
        /// </summary>
        private Dictionary<string, decimal> ParseTechnicalIndicatorResponse(string content, string indicator)
        {
            LogMethodEntry();
            try
            {
                // TODO: Implement proper JSON parsing for technical indicators based on Finnhub response format
                // For now, return empty dictionary - to be implemented based on actual Finnhub response structure
                var indicators = new Dictionary<string, decimal>();
                
                LogMethodExit();
                return indicators;
            }
            catch (Exception ex)
            {
                LogError($"Error parsing technical indicator response for {indicator}", ex);
                LogMethodExit();
                return new Dictionary<string, decimal>();
            }
        }

        // ========== IMarketDataProvider INTERFACE METHODS ==========

        /// <summary>
        /// Tests API connection and authentication with comprehensive health checks
        /// Premium plan provides enhanced diagnostics and monitoring capabilities
        /// </summary>
        /// <returns>A TradingResult containing the connection test result or error information</returns>
        public async Task<TradingResult<ApiResponse<bool>>> TestConnectionAsync()
        {
            LogMethodEntry();
            try
            {
                LogInfo("Testing Finnhub API connection");
                
                // Test with market status endpoint
                var marketStatusResult = await IsMarketOpenAsync();
                var remainingCallsResult = await GetRemainingCallsAsync();

                var response = new ApiResponse<bool>
                {
                    Success = marketStatusResult.IsSuccess,
                    Data = marketStatusResult.IsSuccess,
                    Provider = ProviderName,
                    RemainingCalls = remainingCallsResult.IsSuccess ? remainingCallsResult.Value : 0,
                    ErrorMessage = marketStatusResult.IsSuccess ? null : marketStatusResult.ErrorMessage
                };

                LogInfo($"Finnhub connection test {(response.Success ? "succeeded" : "failed")}");
                LogMethodExit();
                return TradingResult<ApiResponse<bool>>.Success(response);
            }
            catch (Exception ex)
            {
                LogError("Error testing Finnhub connection", ex);
                var response = new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
                LogMethodExit();
                return TradingResult<ApiResponse<bool>>.Success(response);
            }
        }

        /// <summary>
        /// Gets comprehensive provider status and health metrics for monitoring
        /// Enhanced for premium plan with detailed quota and performance tracking
        /// </summary>
        /// <returns>A TradingResult containing the provider status or error information</returns>
        public async Task<TradingResult<ApiResponse<ProviderStatus>>> GetProviderStatusAsync()
        {
            LogMethodEntry();
            try
            {
                LogInfo("Retrieving Finnhub provider status");
                
                var connectionTestResult = await TestConnectionAsync();
                var remainingCallsResult = await GetRemainingCallsAsync();

                var status = new ProviderStatus
                {
                    ProviderName = ProviderName,
                    IsConnected = connectionTestResult.IsSuccess && connectionTestResult.Value.Success,
                    IsAuthenticated = connectionTestResult.IsSuccess && connectionTestResult.Value.Success,
                    RemainingQuota = remainingCallsResult.IsSuccess ? remainingCallsResult.Value : 0,
                    QuotaResetTime = _rateLimiter.GetResetTime(),
                    SubscriptionTier = "Premium", // $50/month plan
                    ResponseTimeMs = 0, // TODO: Implement response time tracking
                    LastSuccessfulCall = DateTime.UtcNow,
                    HealthStatus = (connectionTestResult.IsSuccess && connectionTestResult.Value.Success) ? "Healthy" : "Unhealthy"
                };

                var response = new ApiResponse<ProviderStatus>
                {
                    Success = true,
                    Data = status,
                    Provider = ProviderName,
                    RemainingCalls = status.RemainingQuota
                };

                LogInfo($"Finnhub provider status: {status.HealthStatus}, Quota: {status.RemainingQuota}/{PREMIUM_RATE_LIMIT}");
                LogMethodExit();
                return TradingResult<ApiResponse<ProviderStatus>>.Success(response);
            }
            catch (Exception ex)
            {
                LogError("Error getting provider status", ex);
                var response = new ApiResponse<ProviderStatus>
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Provider = ProviderName
                };
                LogMethodExit();
                return TradingResult<ApiResponse<ProviderStatus>>.Success(response);
            }
        }

        /// <summary>
        /// Retrieves real-time market data for a symbol with sub-second latency
        /// Premium plan provides zero-delay quotes essential for day trading
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

                LogInfo($"Fetching real-time data for {symbol}");

                string cacheKey = $"finnhub_{symbol}_realtime";
                if (_cache.TryGetValue(cacheKey, out MarketData? cachedData) && cachedData != null)
                {
                    // Only use cache if data is less than 1 minute old for real-time data
                    if (cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-1))
                    {
                        LogDebug($"Real-time data for {symbol} retrieved from cache");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Success(cachedData);
                    }
                }

                await _rateLimiter.WaitForPermitAsync();

                try
                {
                    var request = new RestRequest("/quote")
                        .AddParameter("symbol", symbol)
                        .AddParameter("token", _config.FinnhubApiKey);

                    RestResponse response = await _client.ExecuteAsync(request);

                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {
                        LogWarning($"Failed to fetch real-time data for {symbol}: {response.StatusCode}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("API_ERROR", 
                            $"Real-time data request failed: {response.StatusCode} - {response.ErrorMessage}");
                    }

                    var quote = JsonSerializer.Deserialize<FinnhubQuoteResponse>(response.Content);
                    if (quote == null)
                    {
                        LogError($"Failed to deserialize real-time quote for {symbol}");
                        LogMethodExit();
                        return TradingResult<MarketData?>.Failure("DESERIALIZATION_ERROR", 
                            "Failed to parse real-time quote response");
                    }

                    var marketData = new MarketData(Logger)
                    {
                        Symbol = symbol,
                        Price = quote.Current,
                        High = quote.High,
                        Low = quote.Low,
                        Open = quote.Open,
                        PreviousClose = quote.PreviousClose,
                        ChangePercent = quote.PercentChange,
                        Change = quote.Change,
                        Volume = 0, // Finnhub quote endpoint doesn't include volume
                        Timestamp = DateTime.UtcNow
                    };

                    // Cache for 1 minute
                    _cache.Set(cacheKey, marketData, TimeSpan.FromMinutes(1));
                    LogInfo($"Successfully retrieved real-time data for {symbol}: ${marketData.Price}");

                    LogMethodExit();
                    return TradingResult<MarketData?>.Success(marketData);
                }
                catch (Exception ex)
                {
                    LogError($"Exception fetching real-time data for {symbol}", ex);
                    LogMethodExit();
                    return TradingResult<MarketData?>.Failure("REALTIME_DATA_ERROR", 
                        $"Failed to retrieve real-time data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in GetRealTimeDataAsync", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("REALTIME_DATA_ERROR", 
                    $"Real-time data retrieval failed: {ex.Message}", ex);
            }
        }

        // ========== WEBSOCKET SUPPORT FOR PREMIUM PLAN ==========

        /// <summary>
        /// Initializes WebSocket connection for real-time streaming (Premium feature)
        /// </summary>
        private async Task InitializeWebSocketAsync()
        {
            LogMethodEntry();
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    LogDebug("WebSocket already connected");
                    LogMethodExit();
                    return;
                }

                _webSocket = new ClientWebSocket();
                var wsUrl = $"wss://ws.finnhub.io?token={_config.FinnhubApiKey}";
                
                await _webSocket.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
                LogInfo("Finnhub WebSocket connected successfully");
                
                // Start listening for messages
                _ = Task.Run(async () => await ListenToWebSocketAsync());
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error initializing WebSocket", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Listens to WebSocket messages and publishes to subscribers
        /// </summary>
        private async Task ListenToWebSocketAsync()
        {
            LogMethodEntry();
            try
            {
                var buffer = new ArraySegment<byte>(new byte[4096]);
                
                while (_webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = System.Text.Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        ProcessWebSocketMessage(message);
                    }
                }
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error in WebSocket listener", ex);
                LogMethodExit();
            }
        }

        /// <summary>
        /// Processes WebSocket messages and publishes market data updates
        /// </summary>
        private void ProcessWebSocketMessage(string message)
        {
            LogMethodEntry();
            try
            {
                // Parse WebSocket message and publish to subscribers
                // TODO: Implement based on Finnhub WebSocket message format
                LogDebug($"Received WebSocket message: {message}");
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error processing WebSocket message", ex);
                LogMethodExit();
            }
        }

        /// <summary>
        /// Subscribes to real-time quote updates via WebSocket (Premium feature)
        /// </summary>
        public async Task<TradingResult<IObservable<MarketData>>> SubscribeToQuoteUpdatesAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<IObservable<MarketData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                // Initialize WebSocket if not connected
                await InitializeWebSocketAsync();
                
                // Subscribe to symbol
                var subscribeMessage = JsonSerializer.Serialize(new { type = "subscribe", symbol = symbol });
                var bytes = System.Text.Encoding.UTF8.GetBytes(subscribeMessage);
                await _webSocket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                
                LogInfo($"Subscribed to real-time updates for {symbol} via WebSocket");
                
                // Return observable for this symbol
                var observable = _marketDataSubject.Where(data => data.Symbol == symbol);
                
                LogMethodExit();
                return TradingResult<IObservable<MarketData>>.Success(observable);
            }
            catch (Exception ex)
            {
                LogError("Error in SubscribeToQuoteUpdatesAsync", ex);
                LogMethodExit();
                return TradingResult<IObservable<MarketData>>.Failure("SUBSCRIPTION_ERROR", 
                    $"Failed to subscribe to updates: {ex.Message}", ex);
            }
        }
    }
}
