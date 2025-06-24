using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.DataIngestion.Providers
{
    /// <summary>
    /// Canonical implementation of Finnhub market data provider.
    /// Leverages CanonicalProvider base class for standardized caching, rate limiting, and error handling.
    /// </summary>
    public class FinnhubProviderCanonical : CanonicalProvider<MarketData>, IFinnhubProvider
    {
        private readonly RestClient _client;
        private readonly ApiConfiguration _config;
        private readonly IRateLimiter _rateLimiter;

        #region Configuration Overrides

        protected override int DefaultCacheDurationMinutes => 1; // Real-time data
        protected override int MaxRetryAttempts => 3;
        protected override int RetryDelayMilliseconds => 500;
        protected override int RateLimitRequestsPerMinute => 60; // Finnhub free tier
        protected override bool EnableCaching => true;
        protected override bool EnableRateLimiting => true;

        #endregion

        #region Constructor

        public FinnhubProviderCanonical(
            ITradingLogger logger,
            IMemoryCache cache,
            IRateLimiter rateLimiter,
            ApiConfiguration config)
            : base(logger, "FinnhubProvider", cache)
        {
            _rateLimiter = rateLimiter;
            _config = config;
            _client = new RestClient(_config.Finnhub.BaseUrl);
            
            // Add authentication header
            _client.AddDefaultHeader("X-Finnhub-Token", _config.Finnhub.ApiKey);
            
            LogMethodEntry(new { 
                ApiKey = _config.Finnhub.ApiKey?.Substring(0, 4) + "****",
                BaseUrl = _config.Finnhub.BaseUrl 
            });
        }

        #endregion

        #region IMarketDataProvider Implementation

        public string ProviderName => ComponentName;

        public async Task<bool> IsRateLimitReachedAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var isReached = _rateLimiter.IsLimitReached();
                UpdateMetric("RateLimitReached", isReached);
                return await Task.FromResult(isReached);
            }, "Check rate limit status", "Rate limit check failed", "Verify rate limiter configuration");
        }

        public async Task<int> GetRemainingCallsAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var remaining = _rateLimiter.GetRemainingCalls();
                UpdateMetric("RemainingCalls", remaining);
                return await Task.FromResult(remaining);
            }, "Get remaining API calls", "Failed to get remaining calls", "Check rate limiter state");
        }

        public async Task<ApiResponse<bool>> TestConnectionAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var validationResult = await ValidateProviderAsync();
                
                return new ApiResponse<bool>
                {
                    Success = validationResult.IsSuccess,
                    Data = validationResult.IsSuccess,
                    ErrorMessage = validationResult.IsSuccess 
                        ? "" 
                        : validationResult.Error?.Message ?? "Connection failed",
                    Status = validationResult.IsSuccess ? "Connected" : "Failed",
                    Timestamp = DateTime.UtcNow
                };
            }, "Test Finnhub connection", "Connection test failed", "Check network and API credentials");
        }

        public async Task<ApiResponse<ProviderStatus>> GetProviderStatusAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var isConnected = await TestConnectivityAsync(CancellationToken.None);
                var remainingCalls = await GetRemainingCallsAsync();
                var metrics = GetMetrics();
                
                var status = new ProviderStatus
                {
                    ProviderName = ProviderName,
                    IsConnected = isConnected.IsSuccess,
                    IsAuthenticated = !string.IsNullOrEmpty(_config.Finnhub.ApiKey),
                    RemainingQuota = remainingCalls,
                    QuotaResetTime = DateTime.UtcNow.AddMinutes(1), // Per-minute reset
                    SubscriptionTier = "Free",
                    ResponseTimeMs = metrics.ContainsKey("AverageResponseTime") 
                        ? Convert.ToDecimal(metrics["AverageResponseTime"]) 
                        : 0,
                    LastSuccessfulCall = metrics.ContainsKey("LastSuccessfulCall") 
                        ? (DateTime)metrics["LastSuccessfulCall"] 
                        : DateTime.MinValue,
                    HealthStatus = isConnected.IsSuccess ? "Healthy" : "Unhealthy"
                };
                
                return new ApiResponse<ProviderStatus>
                {
                    Success = true,
                    Data = status,
                    ErrorMessage = "",
                    Status = "Retrieved",
                    Timestamp = DateTime.UtcNow
                };
            }, "Get provider status", "Failed to get provider status", "Check provider health");
        }

        public async Task<MarketData?> GetRealTimeDataAsync(string symbol)
        {
            return await GetQuoteAsync(symbol);
        }

        #endregion

        #region IFinnhubProvider Implementation

        public async Task<MarketData?> GetQuoteAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"quote_{symbol}",
                async () => await FetchQuoteInternal(symbol),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromSeconds(30) }); // Very short cache for real-time
            
            return result.IsSuccess ? result.Value : null;
        }

        public async Task<List<MarketData>?> GetBatchQuotesAsync(List<string> symbols)
        {
            var results = await FetchBatchDataAsync(
                symbols,
                async (batch) =>
                {
                    var quotes = new List<MarketData>();
                    foreach (var symbol in batch)
                    {
                        var quote = await GetQuoteAsync(symbol);
                        if (quote != null)
                        {
                            quotes.Add(quote);
                        }
                    }
                    return quotes;
                },
                maxBatchSize: 10); // Process in smaller batches to avoid rate limits

            return results.IsSuccess ? results.Value?.ToList() : null;
        }

        public async Task<MarketData?> GetCandleDataAsync(string symbol, string resolution, DateTime from, DateTime to)
        {
            var result = await FetchDataAsync(
                $"candle_{symbol}_{resolution}_{from:yyyyMMdd}_{to:yyyyMMdd}",
                async () =>
                {
                    var request = new RestRequest("/stock/candle")
                        .AddParameter("symbol", symbol)
                        .AddParameter("resolution", resolution)
                        .AddParameter("from", ((DateTimeOffset)from).ToUnixTimeSeconds())
                        .AddParameter("to", ((DateTimeOffset)to).ToUnixTimeSeconds());

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, symbol);

                    var candleData = JsonSerializer.Deserialize<FinnhubCandleResponse>(response.Content!);
                    if (candleData == null || candleData.Status != "ok")
                    {
                        throw new InvalidOperationException($"Failed to get candle data for {symbol}");
                    }

                    // Return the latest candle as MarketData
                    var lastIndex = candleData.Close.Count - 1;
                    if (lastIndex < 0) return null;

                    return new MarketData(Logger)
                    {
                        Symbol = symbol,
                        Open = candleData.Open[lastIndex],
                        High = candleData.High[lastIndex],
                        Low = candleData.Low[lastIndex],
                        Price = candleData.Close[lastIndex],
                        Volume = candleData.Volume[lastIndex],
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(candleData.Timestamp[lastIndex]).UtcDateTime
                    };
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromMinutes(5) });

            return result.IsSuccess ? result.Value : null;
        }

        public async Task<CompanyProfile> GetCompanyProfileAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/stock/profile2")
                    .AddParameter("symbol", symbol);

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, symbol);

                return JsonSerializer.Deserialize<CompanyProfile>(response.Content!)
                    ?? throw new InvalidOperationException($"Failed to parse company profile for {symbol}");
            }, 
            $"Fetch company profile for {symbol}", 
            $"Failed to fetch company profile for {symbol}", 
            "Check API response format");
        }

        public async Task<CompanyFinancials> GetCompanyFinancialsAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/stock/financials-reported")
                    .AddParameter("symbol", symbol);

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, symbol);

                return JsonSerializer.Deserialize<CompanyFinancials>(response.Content!)
                    ?? throw new InvalidOperationException($"Failed to parse financials for {symbol}");
            }, 
            $"Fetch company financials for {symbol}", 
            $"Failed to fetch financials for {symbol}", 
            "Check API response format");
        }

        public async Task<bool> IsMarketOpenAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/stock/market-status");
                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, "market status");

                var status = JsonSerializer.Deserialize<FinnhubMarketStatus>(response.Content!);
                return status?.IsOpen ?? false;
            }, 
            "Check market status", 
            "Failed to check market status", 
            "Check API response format");
        }

        public async Task<List<string>?> GetStockSymbolsAsync(string exchange = "US")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/stock/symbol")
                    .AddParameter("exchange", exchange);

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, $"symbols for {exchange}");

                var symbols = JsonSerializer.Deserialize<List<FinnhubSymbol>>(response.Content!);
                return symbols?.Select(s => s.Symbol).ToList() ?? new List<string>();
            }, 
            $"Fetch stock symbols for {exchange}", 
            $"Failed to fetch symbols for {exchange}", 
            "Check API response format");
        }

        public async Task<SentimentData> GetInsiderSentimentAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/stock/insider-sentiment")
                    .AddParameter("symbol", symbol);

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, symbol);

                return JsonSerializer.Deserialize<SentimentData>(response.Content!)
                    ?? new SentimentData { Symbol = symbol, Sentiment = "neutral" };
            }, 
            $"Fetch insider sentiment for {symbol}", 
            $"Failed to fetch sentiment for {symbol}", 
            "Check API response format");
        }

        public async Task<List<NewsItem>> GetMarketNewsAsync(string category = "general")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/news")
                    .AddParameter("category", category);

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, $"news category {category}");

                return JsonSerializer.Deserialize<List<NewsItem>>(response.Content!)
                    ?? new List<NewsItem>();
            }, 
            $"Fetch market news for {category}", 
            $"Failed to fetch market news for {category}", 
            "Check API response format");
        }

        public async Task<List<NewsItem>> GetCompanyNewsAsync(string symbol, DateTime from, DateTime to)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/company-news")
                    .AddParameter("symbol", symbol)
                    .AddParameter("from", from.ToString("yyyy-MM-dd"))
                    .AddParameter("to", to.ToString("yyyy-MM-dd"));

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, $"news for {symbol}");

                return JsonSerializer.Deserialize<List<NewsItem>>(response.Content!)
                    ?? new List<NewsItem>();
            }, 
            $"Fetch company news for {symbol}", 
            $"Failed to fetch news for {symbol}", 
            "Check API response format");
        }

        public async Task<Dictionary<string, decimal>> GetTechnicalIndicatorsAsync(string symbol, string indicator)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var request = new RestRequest("/indicator")
                    .AddParameter("symbol", symbol)
                    .AddParameter("indicator", indicator)
                    .AddParameter("resolution", "D") // Daily by default
                    .AddParameter("from", ((DateTimeOffset)DateTime.UtcNow.AddDays(-30)).ToUnixTimeSeconds())
                    .AddParameter("to", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());

                var response = await _client.ExecuteAsync(request);
                ValidateResponse(response, $"{indicator} for {symbol}");

                var indicatorData = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content!);
                var result = new Dictionary<string, decimal>();
                
                // Parse indicator values (structure varies by indicator)
                if (indicatorData != null)
                {
                    foreach (var kvp in indicatorData)
                    {
                        if (decimal.TryParse(kvp.Value?.ToString(), out var value))
                        {
                            result[kvp.Key] = value;
                        }
                    }
                }

                return result;
            }, 
            $"Fetch technical indicators {indicator} for {symbol}", 
            $"Failed to fetch indicators for {symbol}", 
            "Check API response format");
        }

        #endregion

        #region Abstract Method Implementations

        protected override async Task<TradingResult> ValidateConfigurationAsync()
        {
            if (string.IsNullOrEmpty(_config.Finnhub.ApiKey))
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConfigurationError,
                    "Finnhub API key is not configured");
            }

            if (string.IsNullOrEmpty(_config.Finnhub.BaseUrl))
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConfigurationError,
                    "Finnhub base URL is not configured");
            }

            return await Task.FromResult(TradingResult.Success());
        }

        protected override async Task<TradingResult> TestConnectivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Test with a simple market status call
                var request = new RestRequest("/stock/market-status");
                var response = await _client.ExecuteAsync(request, cancellationToken);
                
                if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
                {
                    UpdateMetric("LastSuccessfulCall", DateTime.UtcNow);
                    return TradingResult.Success();
                }

                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConnectionFailed,
                    $"Connection test failed: {response.ErrorMessage ?? "No response"}");
            }
            catch (Exception ex)
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConnectionFailed,
                    "Connection test failed with exception",
                    ex);
            }
        }

        protected override async Task<TradingResult> ValidateAuthenticationAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Test authentication with a quote request
                var request = new RestRequest("/quote")
                    .AddParameter("symbol", "AAPL");
                    
                var response = await _client.ExecuteAsync(request, cancellationToken);
                
                if (response.IsSuccessful)
                {
                    return TradingResult.Success();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.Content?.Contains("Invalid API key") == true)
                {
                    return TradingResult.Failure(
                        TradingError.ErrorCodes.UnauthorizedAccess,
                        "Invalid Finnhub API key");
                }

                return TradingResult.Failure(
                    TradingError.ErrorCodes.UnauthorizedAccess,
                    $"Authentication failed: {response.ErrorMessage}");
            }
            catch (Exception ex)
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.UnauthorizedAccess,
                    "Authentication validation failed",
                    ex);
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<MarketData> FetchQuoteInternal(string symbol)
        {
            var request = new RestRequest("/quote")
                .AddParameter("symbol", symbol);

            var response = await _client.ExecuteAsync(request);
            ValidateResponse(response, symbol);

            var quote = JsonSerializer.Deserialize<FinnhubQuote>(response.Content!);
            if (quote == null)
            {
                throw new InvalidOperationException($"Failed to parse quote for {symbol}");
            }

            return new MarketData(Logger)
            {
                Symbol = symbol,
                Price = quote.CurrentPrice,
                High = quote.HighPrice,
                Low = quote.LowPrice,
                Open = quote.OpenPrice,
                PreviousClose = quote.PreviousClose,
                Change = quote.Change,
                ChangePercent = quote.PercentChange,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(quote.Timestamp).UtcDateTime
            };
        }

        private void ValidateResponse(RestResponse response, string context)
        {
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException(
                    $"API request failed for {context}: {response.ErrorMessage ?? "No response"}");
            }

            // Check for API errors
            if (response.Content.Contains("error") || 
                response.Content.Contains("Invalid API key"))
            {
                throw new InvalidOperationException(
                    $"API error for {context}: {response.Content}");
            }
        }

        #endregion

        #region Lifecycle

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing Finnhub provider", new { BaseUrl = _config.Finnhub.BaseUrl });
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting Finnhub provider");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping Finnhub provider");
            _client?.Dispose();
            return Task.CompletedTask;
        }

        #endregion
    }

    #region Response Models

    public class FinnhubQuote
    {
        [JsonPropertyName("c")]
        public decimal CurrentPrice { get; set; }
        
        [JsonPropertyName("d")]
        public decimal Change { get; set; }
        
        [JsonPropertyName("dp")]
        public decimal PercentChange { get; set; }
        
        [JsonPropertyName("h")]
        public decimal HighPrice { get; set; }
        
        [JsonPropertyName("l")]
        public decimal LowPrice { get; set; }
        
        [JsonPropertyName("o")]
        public decimal OpenPrice { get; set; }
        
        [JsonPropertyName("pc")]
        public decimal PreviousClose { get; set; }
        
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }
    }

    public class FinnhubCandleResponse
    {
        [JsonPropertyName("c")]
        public List<decimal> Close { get; set; } = new();
        
        [JsonPropertyName("h")]
        public List<decimal> High { get; set; } = new();
        
        [JsonPropertyName("l")]
        public List<decimal> Low { get; set; } = new();
        
        [JsonPropertyName("o")]
        public List<decimal> Open { get; set; } = new();
        
        [JsonPropertyName("v")]
        public List<long> Volume { get; set; } = new();
        
        [JsonPropertyName("t")]
        public List<long> Timestamp { get; set; } = new();
        
        [JsonPropertyName("s")]
        public string Status { get; set; } = string.Empty;
    }

    public class FinnhubMarketStatus
    {
        [JsonPropertyName("isOpen")]
        public bool IsOpen { get; set; }
        
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;
        
        [JsonPropertyName("session")]
        public string Session { get; set; } = string.Empty;
    }

    public class FinnhubSymbol
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    #endregion
}