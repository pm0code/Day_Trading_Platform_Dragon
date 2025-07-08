using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.MarketData.Configuration;
using MarketAnalyzer.Infrastructure.MarketData.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace MarketAnalyzer.Infrastructure.MarketData.Services;

/// <summary>
/// Finnhub API market data service implementation.
/// Uses industry-standard libraries: Polly (resilience), System.Threading.RateLimiting, Microsoft.Extensions.Caching.
/// </summary>
public class FinnhubMarketDataService : CanonicalServiceBase, IMarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly FinnhubOptions _options;
    private readonly RateLimiter _rateLimiter;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly JsonSerializerOptions _jsonOptions;
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _webSocketCancellation = new();
    private readonly SemaphoreSlim _webSocketSemaphore = new(1, 1);

    public FinnhubMarketDataService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<FinnhubOptions> options,
        ILogger<FinnhubMarketDataService> logger)
        : base(logger)
    {
        LogMethodEntry();
        try
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            
            // Configure HTTP client with timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            
            // Create rate limiter using .NET built-in rate limiting
            _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = _options.MaxCallsPerMinute,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = _options.MaxCallsPerMinute,
                AutoReplenishment = true
            });

            // Create resilience pipeline using Polly
            _resiliencePipeline = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TaskCanceledException>(),
                    MaxRetryAttempts = _options.MaxRetryAttempts,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = _options.CircuitBreakerFailureThreshold,
                    BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerTimeoutSeconds)
                })
                .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
                .Build();

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            LogInfo($"FinnhubMarketDataService initialized successfully with rate limit {_options.MaxCallsPerMinute}/min");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize FinnhubMarketDataService", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Test API connectivity
            var healthResult = await GetServiceHealthAsync().ConfigureAwait(false);
            if (!healthResult.IsSuccess)
            {
                LogError("Finnhub API health check failed during initialization", null, healthResult.Error?.Message);
                return TradingResult<bool>.Failure("INIT_HEALTH_CHECK_FAILED", "API health check failed during initialization");
            }

            UpdateMetric("InitializationSuccessCount", GetMetricValue("InitializationSuccessCount") + 1);
            LogInfo("FinnhubMarketDataService initialized successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during FinnhubMarketDataService initialization", ex);
            return TradingResult<bool>.Failure("INIT_EXCEPTION", $"Initialization failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Start WebSocket connection if configured
            if (_options.IsPremium)
            {
                await InitializeWebSocketAsync(cancellationToken).ConfigureAwait(false);
            }

            UpdateMetric("StartSuccessCount", GetMetricValue("StartSuccessCount") + 1);
            LogInfo("FinnhubMarketDataService started successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during FinnhubMarketDataService start", ex);
            return TradingResult<bool>.Failure("START_EXCEPTION", $"Start failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Stop WebSocket connection
            _webSocketCancellation.Cancel();
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cancellationToken).ConfigureAwait(false);
            }

            UpdateMetric("StopSuccessCount", GetMetricValue("StopSuccessCount") + 1);
            LogInfo("FinnhubMarketDataService stopped successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during FinnhubMarketDataService stop", ex);
            return TradingResult<bool>.Failure("STOP_EXCEPTION", $"Stop failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<MarketQuote>> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogError("Symbol cannot be null or empty");
                return TradingResult<MarketQuote>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            // Check cache first
            var cacheKey = $"quote_{symbol}";
            if (_cache.TryGetValue(cacheKey, out MarketQuote? cachedQuote))
            {
                Logger.LogTrace("[{ServiceName}] Quote retrieved from cache for symbol {Symbol}", ServiceName, symbol);
                UpdateMetric("CacheHits", GetMetricValue("CacheHits") + 1);
                return TradingResult<MarketQuote>.Success(cachedQuote!);
            }

            // Rate limit check
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                LogWarning($"Rate limit exceeded for symbol {symbol}");
                UpdateMetric("RateLimitExceeded", GetMetricValue("RateLimitExceeded") + 1);
                return TradingResult<MarketQuote>.Failure("RATE_LIMIT_EXCEEDED", "API rate limit exceeded");
            }

            // Make API call with resilience
            var result = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                var url = $"{_options.BaseUrl}/quote?symbol={symbol}&token={_options.ApiKey}";
                using var response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var quoteData = JsonSerializer.Deserialize<FinnhubQuoteResponse>(content, _jsonOptions);
                
                return CreateMarketQuote(symbol, quoteData!);
            }, cancellationToken).ConfigureAwait(false);

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.QuoteCacheExpirationSeconds),
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(cacheKey, result, cacheOptions);

            UpdateMetric("QuoteRequests", GetMetricValue("QuoteRequests") + 1);
            Logger.LogTrace("[{ServiceName}] Quote retrieved successfully for symbol {Symbol}", ServiceName, symbol);
            return TradingResult<MarketQuote>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            LogError($"HTTP error retrieving quote for symbol {symbol}", ex);
            UpdateMetric("HttpErrors", GetMetricValue("HttpErrors") + 1);
            return TradingResult<MarketQuote>.Failure("HTTP_ERROR", $"HTTP error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            LogError($"Request timeout retrieving quote for symbol {symbol}", ex);
            UpdateMetric("TimeoutErrors", GetMetricValue("TimeoutErrors") + 1);
            return TradingResult<MarketQuote>.Failure("TIMEOUT", $"Request timeout: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error retrieving quote for symbol {symbol}", ex);
            UpdateMetric("UnexpectedErrors", GetMetricValue("UnexpectedErrors") + 1);
            return TradingResult<MarketQuote>.Failure("UNEXPECTED_ERROR", $"Unexpected error: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<IEnumerable<MarketQuote>>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (symbols == null || !symbols.Any())
            {
                LogError("Symbols collection cannot be null or empty");
                return TradingResult<IEnumerable<MarketQuote>>.Failure("INVALID_SYMBOLS", "Symbols collection cannot be null or empty");
            }

            var tasks = symbols.Select(symbol => GetQuoteAsync(symbol, cancellationToken));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            
            var quotes = new List<MarketQuote>();
            var failures = new List<string>();
            
            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    quotes.Add(result.Value!);
                }
                else
                {
                    failures.Add(result.Error?.Message ?? "Unknown error");
                }
            }

            if (failures.Any())
            {
                LogWarning($"Some quote requests failed: {string.Join(", ", failures)}");
            }

            UpdateMetric("BulkQuoteRequests", GetMetricValue("BulkQuoteRequests") + 1);
            Logger.LogTrace("[{ServiceName}] Bulk quotes retrieved: {SuccessCount} successful, {FailureCount} failed", ServiceName, quotes.Count, failures.Count);
            return TradingResult<IEnumerable<MarketQuote>>.Success(quotes);
        }
        catch (Exception ex)
        {
            LogError("Unexpected error retrieving bulk quotes", ex);
            return TradingResult<IEnumerable<MarketQuote>>.Failure("BULK_QUOTE_ERROR", $"Bulk quote error: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<Stock>> GetCompanyProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogError("Symbol cannot be null or empty");
                return TradingResult<Stock>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            // Check cache first
            var cacheKey = $"profile_{symbol}";
            if (_cache.TryGetValue(cacheKey, out Stock? cachedStock))
            {
                Logger.LogTrace("[{ServiceName}] Company profile retrieved from cache for symbol {Symbol}", ServiceName, symbol);
                UpdateMetric("CacheHits", GetMetricValue("CacheHits") + 1);
                return TradingResult<Stock>.Success(cachedStock!);
            }

            // Rate limit and API call
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return TradingResult<Stock>.Failure("RATE_LIMIT_EXCEEDED", "API rate limit exceeded");
            }

            var result = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                var url = $"{_options.BaseUrl}/stock/profile2?symbol={symbol}&token={_options.ApiKey}";
                using var response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var profileData = JsonSerializer.Deserialize<FinnhubProfileResponse>(content, _jsonOptions);
                
                return CreateStock(symbol, profileData!);
            }, cancellationToken).ConfigureAwait(false);

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CompanyCacheExpirationSeconds),
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(cacheKey, result, cacheOptions);

            UpdateMetric("ProfileRequests", GetMetricValue("ProfileRequests") + 1);
            return TradingResult<Stock>.Success(result);
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving company profile for symbol {symbol}", ex);
            return TradingResult<Stock>.Failure("PROFILE_ERROR", $"Profile error: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<IEnumerable<MarketQuote>>> GetHistoricalDataAsync(string symbol, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return TradingResult<IEnumerable<MarketQuote>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            if (fromDate >= toDate)
            {
                return TradingResult<IEnumerable<MarketQuote>>.Failure("INVALID_DATE_RANGE", "From date must be before to date");
            }

            var fromTimestamp = ((DateTimeOffset)fromDate).ToUnixTimeSeconds();
            var toTimestamp = ((DateTimeOffset)toDate).ToUnixTimeSeconds();

            using var lease = await _rateLimiter.AcquireAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return TradingResult<IEnumerable<MarketQuote>>.Failure("RATE_LIMIT_EXCEEDED", "API rate limit exceeded");
            }

            var result = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                var url = $"{_options.BaseUrl}/stock/candle?symbol={symbol}&resolution=1&from={fromTimestamp}&to={toTimestamp}&token={_options.ApiKey}";
                using var response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var candleData = JsonSerializer.Deserialize<FinnhubCandleResponse>(content, _jsonOptions);
                
                return CreateHistoricalQuotes(symbol, candleData!);
            }, cancellationToken).ConfigureAwait(false);

            UpdateMetric("HistoricalRequests", GetMetricValue("HistoricalRequests") + 1);
            return TradingResult<IEnumerable<MarketQuote>>.Success(result);
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving historical data for symbol {symbol}", ex);
            return TradingResult<IEnumerable<MarketQuote>>.Failure("HISTORICAL_ERROR", $"Historical data error: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<IEnumerable<Stock>>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return TradingResult<IEnumerable<Stock>>.Failure("INVALID_QUERY", "Search query cannot be null or empty");
            }

            using var lease = await _rateLimiter.AcquireAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return TradingResult<IEnumerable<Stock>>.Failure("RATE_LIMIT_EXCEEDED", "API rate limit exceeded");
            }

            var result = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                var url = $"{_options.BaseUrl}/search?q={Uri.EscapeDataString(query)}&token={_options.ApiKey}";
                using var response = await _httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var searchData = JsonSerializer.Deserialize<FinnhubSearchResponse>(content, _jsonOptions);
                
                return CreateStocksFromSearch(searchData!);
            }, cancellationToken).ConfigureAwait(false);

            UpdateMetric("SearchRequests", GetMetricValue("SearchRequests") + 1);
            return TradingResult<IEnumerable<Stock>>.Success(result);
        }
        catch (Exception ex)
        {
            LogError($"Error searching symbols with query {query}", ex);
            return TradingResult<IEnumerable<Stock>>.Failure("SEARCH_ERROR", $"Search error: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async IAsyncEnumerable<MarketQuote> StreamQuotesAsync(IEnumerable<string> symbols, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (!_options.IsPremium)
        {
            LogWarning("WebSocket streaming requires premium subscription");
            yield break;
        }

        await foreach (var quote in StreamWebSocketQuotesAsync(symbols, cancellationToken))
        {
            yield return quote;
        }
        
        LogMethodExit();
    }

    public async Task<TradingResult<bool>> GetServiceHealthAsync()
    {
        LogMethodEntry();
        try
        {
            // Simple health check by getting a quote for a well-known symbol
            var healthResult = await GetQuoteAsync("AAPL", CancellationToken.None).ConfigureAwait(false);
            
            if (healthResult.IsSuccess)
            {
                Logger.LogTrace("[{ServiceName}] Health check passed", ServiceName);
                UpdateMetric("HealthCheckSuccess", GetMetricValue("HealthCheckSuccess") + 1);
                return TradingResult<bool>.Success(true);
            }
            else
            {
                LogWarning($"Health check failed: {healthResult.Error?.Message}");
                UpdateMetric("HealthCheckFailure", GetMetricValue("HealthCheckFailure") + 1);
                return TradingResult<bool>.Failure("HEALTH_CHECK_FAILED", "Health check failed");
            }
        }
        catch (Exception ex)
        {
            LogError("Health check exception", ex);
            return TradingResult<bool>.Failure("HEALTH_CHECK_EXCEPTION", $"Health check exception: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task InitializeWebSocketAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri($"{_options.WebSocketUrl}?token={_options.ApiKey}"), cancellationToken).ConfigureAwait(false);
            LogInfo("WebSocket connection established");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize WebSocket", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async IAsyncEnumerable<MarketQuote> StreamWebSocketQuotesAsync(IEnumerable<string> symbols, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (_webSocket?.State != WebSocketState.Open)
        {
            LogWarning("WebSocket is not open, cannot stream quotes");
            yield break;
        }

        // Subscribe to symbols
        foreach (var symbol in symbols)
        {
            var subscribeMessage = JsonSerializer.Serialize(new { type = "subscribe", symbol = symbol });
            var subscribeBytes = Encoding.UTF8.GetBytes(subscribeMessage);
            await _webSocket.SendAsync(subscribeBytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }

        // Listen for messages
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult? result = null;
            Exception? error = null;
            List<MarketQuote>? quotes = null;
            
            try
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var wsData = JsonSerializer.Deserialize<FinnhubWebSocketResponse>(message, _jsonOptions);
                    
                    if (wsData?.Data != null)
                    {
                        quotes = new List<MarketQuote>();
                        foreach (var trade in wsData.Data)
                        {
                            quotes.Add(CreateMarketQuoteFromWebSocket(trade));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                error = ex;
            }
            
            if (error != null)
            {
                LogError("Error processing WebSocket message", error);
                break;
            }
            
            if (quotes != null)
            {
                foreach (var quote in quotes)
                {
                    yield return quote;
                }
            }
        }
        
        LogMethodExit();
    }

    private MarketQuote CreateMarketQuote(string symbol, FinnhubQuoteResponse quoteData)
    {
        LogMethodEntry();
        try
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: (decimal)quoteData.C,
                dayOpen: (decimal)quoteData.O,
                dayHigh: (decimal)quoteData.H,
                dayLow: (decimal)quoteData.L,
                previousClose: (decimal)quoteData.Pc,
                volume: (long)quoteData.V,
                timestamp: DateTime.UtcNow,
                hardwareTimestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                marketStatus: DetermineMarketStatus(),
                isRealTime: true
            );

            return quote;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private Stock CreateStock(string symbol, FinnhubProfileResponse profileData)
    {
        LogMethodEntry();
        try
        {
            var marketCap = DetermineMarketCap(profileData.MarketCapitalization);
            var sector = DetermineSector(profileData.FinnhubIndustry);

            var stock = new Stock(
                symbol: symbol,
                exchange: profileData.Exchange ?? "UNKNOWN",
                name: profileData.Name ?? symbol,
                marketCap: marketCap,
                sector: sector,
                industry: profileData.FinnhubIndustry ?? "Unknown",
                country: profileData.Country ?? "US",
                currency: profileData.Currency ?? "USD"
            );

            return stock;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private IEnumerable<MarketQuote> CreateHistoricalQuotes(string symbol, FinnhubCandleResponse candleData)
    {
        LogMethodEntry();
        try
        {
            var quotes = new List<MarketQuote>();
            
            if (candleData.T != null && candleData.T.Any())
            {
                for (int i = 0; i < candleData.T.Length; i++)
                {
                    var quote = new MarketQuote(
                        symbol: symbol,
                        currentPrice: (decimal)candleData.C![i],
                        dayOpen: (decimal)candleData.O![i],
                        dayHigh: (decimal)candleData.H![i],
                        dayLow: (decimal)candleData.L![i],
                        previousClose: i > 0 ? (decimal)candleData.C[i-1] : (decimal)candleData.C[i],
                        volume: (long)candleData.V![i],
                        timestamp: DateTimeOffset.FromUnixTimeSeconds(candleData.T[i]).UtcDateTime,
                        hardwareTimestamp: candleData.T[i] * 1000, // Convert to milliseconds
                        marketStatus: MarketStatus.Unknown,
                        isRealTime: false
                    );
                    quotes.Add(quote);
                }
            }

            return quotes;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private IEnumerable<Stock> CreateStocksFromSearch(FinnhubSearchResponse searchData)
    {
        LogMethodEntry();
        try
        {
            var stocks = new List<Stock>();
            
            if (searchData.Result != null)
            {
                foreach (var result in searchData.Result)
                {
                    var stock = new Stock(
                        symbol: result.Symbol,
                        exchange: result.Type ?? "UNKNOWN",
                        name: result.Description ?? result.Symbol,
                        marketCap: MarketCap.Unknown,
                        sector: Sector.Unknown,
                        industry: "Unknown",
                        country: "US",
                        currency: "USD"
                    );
                    stocks.Add(stock);
                }
            }

            return stocks;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private MarketQuote CreateMarketQuoteFromWebSocket(FinnhubWebSocketTrade trade)
    {
        LogMethodEntry();
        try
        {
            return new MarketQuote(
                symbol: trade.S,
                currentPrice: (decimal)trade.P,
                dayOpen: (decimal)trade.P, // WebSocket doesn't provide OHLC, using price
                dayHigh: (decimal)trade.P,
                dayLow: (decimal)trade.P,
                previousClose: (decimal)trade.P,
                volume: (long)trade.V,
                timestamp: DateTimeOffset.FromUnixTimeMilliseconds(trade.T).UtcDateTime,
                hardwareTimestamp: trade.T,
                marketStatus: DetermineMarketStatus(),
                isRealTime: true
            );
        }
        finally
        {
            LogMethodExit();
        }
    }

    private MarketCap DetermineMarketCap(double marketCapValue)
    {
        LogMethodEntry();
        try
        {
            return marketCapValue switch
            {
                >= 200_000_000_000 => MarketCap.MegaCap,
                >= 10_000_000_000 => MarketCap.LargeCap,
                >= 2_000_000_000 => MarketCap.MidCap,
                >= 300_000_000 => MarketCap.SmallCap,
                >= 50_000_000 => MarketCap.MicroCap,
                > 0 => MarketCap.NanoCap,
                _ => MarketCap.Unknown
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private MarketStatus DetermineMarketStatus()
    {
        LogMethodEntry();
        try
        {
            var now = DateTime.UtcNow;
            var easternTime = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            
            // Check if it's a weekend
            if (easternTime.DayOfWeek == DayOfWeek.Saturday || easternTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return MarketStatus.Closed;
            }
            
            var marketTime = easternTime.TimeOfDay;
            
            // Pre-market: 4:00 AM - 9:30 AM ET
            if (marketTime >= TimeSpan.FromHours(4) && marketTime < TimeSpan.FromHours(9.5))
            {
                return MarketStatus.PreMarket;
            }
            
            // Regular hours: 9:30 AM - 4:00 PM ET
            if (marketTime >= TimeSpan.FromHours(9.5) && marketTime < TimeSpan.FromHours(16))
            {
                return MarketStatus.Open;
            }
            
            // After-hours: 4:00 PM - 8:00 PM ET
            if (marketTime >= TimeSpan.FromHours(16) && marketTime < TimeSpan.FromHours(20))
            {
                return MarketStatus.AfterHours;
            }
            
            return MarketStatus.Closed;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private Sector DetermineSector(string? industry)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(industry))
                return Sector.Unknown;

            return industry.ToLowerInvariant() switch
            {
                var s when s.Contains("technology") || s.Contains("software") => Sector.Technology,
                var s when s.Contains("healthcare") || s.Contains("pharmaceutical") => Sector.HealthCare,
                var s when s.Contains("financial") || s.Contains("bank") => Sector.Financials,
                var s when s.Contains("energy") || s.Contains("oil") => Sector.Energy,
                var s when s.Contains("consumer") || s.Contains("retail") => Sector.ConsumerDiscretionary,
                var s when s.Contains("industrial") || s.Contains("manufacturing") => Sector.Industrials,
                var s when s.Contains("communication") || s.Contains("telecom") => Sector.CommunicationServices,
                var s when s.Contains("utility") || s.Contains("utilities") => Sector.Utilities,
                var s when s.Contains("material") || s.Contains("mining") => Sector.Materials,
                var s when s.Contains("real estate") || s.Contains("reit") => Sector.RealEstate,
                _ => Sector.Unknown
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webSocketCancellation?.Cancel();
            _webSocket?.Dispose();
            _webSocketCancellation?.Dispose();
            _webSocketSemaphore?.Dispose();
            _rateLimiter?.Dispose();
        }
        base.Dispose(disposing);
    }
}