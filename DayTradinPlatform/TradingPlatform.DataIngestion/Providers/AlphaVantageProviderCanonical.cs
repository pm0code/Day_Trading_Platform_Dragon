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
    /// Canonical implementation of AlphaVantage market data provider.
    /// Leverages CanonicalProvider base class for standardized caching, rate limiting, and error handling.
    /// </summary>
    public class AlphaVantageProviderCanonical : CanonicalProvider<MarketData>, IAlphaVantageProvider
    {
        private readonly RestClient _client;
        private readonly ApiConfiguration _config;
        private readonly IRateLimiter _rateLimiter;

        #region Configuration Overrides

        protected override int DefaultCacheDurationMinutes => 5;
        protected override int MaxRetryAttempts => 3;
        protected override int RetryDelayMilliseconds => 1000;
        protected override int RateLimitRequestsPerMinute => 5; // AlphaVantage free tier limit
        protected override bool EnableCaching => true;
        protected override bool EnableRateLimiting => true;

        #endregion

        #region Constructor

        public AlphaVantageProviderCanonical(
            ITradingLogger logger,
            IMemoryCache cache,
            IRateLimiter rateLimiter,
            ApiConfiguration config)
            : base(logger, "AlphaVantageProvider", cache)
        {
            _rateLimiter = rateLimiter;
            _config = config;
            _client = new RestClient(_config.AlphaVantage.BaseUrl);
            
            LogMethodEntry(new { 
                ApiKey = _config.AlphaVantage.ApiKey?.Substring(0, 4) + "****",
                BaseUrl = _config.AlphaVantage.BaseUrl 
            });
        }

        #endregion

        #region IMarketDataProvider Implementation

        public string ProviderName => ComponentName;

        public async Task<bool> IsRateLimitReachedAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var isReached = await _rateLimiter.IsRateLimitReachedAsync();
                UpdateMetric("RateLimitReached", isReached);
                return isReached;
            }, "Check rate limit status", "Rate limit check failed", "Verify rate limiter configuration");
        }

        public async Task<int> GetRemainingCallsAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var remaining = await _rateLimiter.GetRemainingCallsAsync();
                UpdateMetric("RemainingCalls", remaining);
                return remaining;
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
                    Message = validationResult.IsSuccess 
                        ? "Connection successful" 
                        : validationResult.Error?.Message ?? "Connection failed",
                    Timestamp = DateTime.UtcNow
                };
            }, "Test AlphaVantage connection", "Connection test failed", "Check network and API credentials");
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
                    IsAuthenticated = !string.IsNullOrEmpty(_config.AlphaVantage.ApiKey),
                    RemainingQuota = remainingCalls,
                    QuotaResetTime = DateTime.UtcNow.Date.AddDays(1), // Daily reset
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
                    Message = "Provider status retrieved",
                    Timestamp = DateTime.UtcNow
                };
            }, "Get provider status", "Failed to get provider status", "Check provider health");
        }

        public async Task<MarketData?> GetRealTimeDataAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"quote_{symbol}",
                async () => await FetchGlobalQuoteAsync(symbol),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromMinutes(1) });
            
            return result.IsSuccess ? result.Value : null;
        }

        #endregion

        #region IAlphaVantageProvider Implementation

        public async Task<MarketData> GetGlobalQuoteAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"global_quote_{symbol}",
                async () => await FetchGlobalQuoteAsync(symbol));
            
            return result.IsSuccess 
                ? result.Value! 
                : throw new InvalidOperationException($"Failed to get global quote: {result.Error?.Message}");
        }

        public async Task<List<MarketData>> GetIntradayDataAsync(string symbol, string interval = "5min")
        {
            var result = await FetchDataAsync(
                $"intraday_{symbol}_{interval}",
                async () =>
                {
                    var request = new RestRequest()
                        .AddParameter("function", "TIME_SERIES_INTRADAY")
                        .AddParameter("symbol", symbol)
                        .AddParameter("interval", interval)
                        .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, symbol);

                    // Parse and convert to MarketData list
                    var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content!);
                    return ParseIntradayData(jsonResponse, symbol);
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromMinutes(1) });

            return result.IsSuccess ? result.Value!.ToList() : new List<MarketData>();
        }

        public async Task<List<DailyData>> GetDailyTimeSeriesAsync(string symbol, string outputSize = "compact")
        {
            var result = await FetchDataAsync(
                $"daily_{symbol}_{outputSize}",
                async () => await FetchDailyDataAsync("TIME_SERIES_DAILY", symbol, outputSize),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(1) });

            return result.IsSuccess ? result.Value!.ToList() : new List<DailyData>();
        }

        public async Task<List<DailyData>> GetDailyAdjustedTimeSeriesAsync(string symbol, string outputSize = "compact")
        {
            var result = await FetchDataAsync(
                $"daily_adjusted_{symbol}_{outputSize}",
                async () => await FetchDailyDataAsync("TIME_SERIES_DAILY_ADJUSTED", symbol, outputSize),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(1) });

            return result.IsSuccess ? result.Value!.ToList() : new List<DailyData>();
        }

        public async Task<List<DailyData>> GetWeeklyTimeSeriesAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"weekly_{symbol}",
                async () => await FetchDailyDataAsync("TIME_SERIES_WEEKLY", symbol, "full"),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(6) });

            return result.IsSuccess ? result.Value!.ToList() : new List<DailyData>();
        }

        public async Task<List<DailyData>> GetMonthlyTimeSeriesAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"monthly_{symbol}",
                async () => await FetchDailyDataAsync("TIME_SERIES_MONTHLY", symbol, "full"),
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(12) });

            return result.IsSuccess ? result.Value!.ToList() : new List<DailyData>();
        }

        public async Task<CompanyOverview> GetCompanyOverviewAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"overview_{symbol}",
                async () =>
                {
                    var request = new RestRequest()
                        .AddParameter("function", "OVERVIEW")
                        .AddParameter("symbol", symbol)
                        .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, symbol);

                    return JsonSerializer.Deserialize<CompanyOverview>(response.Content!)
                        ?? throw new InvalidOperationException($"Failed to parse company overview for {symbol}");
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(24) });

            return result.IsSuccess ? result.Value! : new CompanyOverview();
        }

        public async Task<EarningsData> GetEarningsAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"earnings_{symbol}",
                async () =>
                {
                    var request = new RestRequest()
                        .AddParameter("function", "EARNINGS")
                        .AddParameter("symbol", symbol)
                        .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, symbol);

                    return JsonSerializer.Deserialize<EarningsData>(response.Content!)
                        ?? throw new InvalidOperationException($"Failed to parse earnings data for {symbol}");
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(24) });

            return result.IsSuccess ? result.Value! : new EarningsData();
        }

        public async Task<IncomeStatement> GetIncomeStatementAsync(string symbol)
        {
            var result = await FetchDataAsync(
                $"income_{symbol}",
                async () =>
                {
                    var request = new RestRequest()
                        .AddParameter("function", "INCOME_STATEMENT")
                        .AddParameter("symbol", symbol)
                        .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, symbol);

                    return JsonSerializer.Deserialize<IncomeStatement>(response.Content!)
                        ?? throw new InvalidOperationException($"Failed to parse income statement for {symbol}");
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(24) });

            return result.IsSuccess ? result.Value! : new IncomeStatement();
        }

        public async Task<List<SymbolSearchResult>> SearchSymbolsAsync(string keywords)
        {
            var result = await FetchDataAsync(
                $"search_{keywords}",
                async () =>
                {
                    var request = new RestRequest()
                        .AddParameter("function", "SYMBOL_SEARCH")
                        .AddParameter("keywords", keywords)
                        .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                    var response = await _client.ExecuteAsync(request);
                    ValidateResponse(response, keywords);

                    var searchResponse = JsonSerializer.Deserialize<SymbolSearchResponse>(response.Content!);
                    var matches = searchResponse?.Matches ?? new List<AlphaVantageSymbolMatch>();
                    
                    // Map AlphaVantageSymbolMatch to SymbolSearchResult
                    return matches.Select(m => new SymbolSearchResult
                    {
                        Symbol = m.Symbol,
                        Name = m.Name,
                        Type = m.Type,
                        Region = m.Region,
                        Exchange = "", // AlphaVantage doesn't provide exchange in search
                        Currency = "" // AlphaVantage doesn't provide currency in search
                    }).ToList();
                },
                new CachePolicy { AbsoluteExpiration = TimeSpan.FromHours(1) });

            return result.IsSuccess ? result.Value!.ToList() : new List<SymbolSearchResult>();
        }

        public async Task<bool> IsMarketOpenAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // AlphaVantage doesn't have a market status endpoint
                // Use time-based logic for US markets
                var now = DateTime.UtcNow;
                var easternTime = TimeZoneInfo.ConvertTimeFromUtc(now, 
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
                
                // Market hours: 9:30 AM - 4:00 PM ET, Monday-Friday
                bool isWeekday = easternTime.DayOfWeek >= DayOfWeek.Monday && 
                                 easternTime.DayOfWeek <= DayOfWeek.Friday;
                bool isMarketHours = easternTime.TimeOfDay >= new TimeSpan(9, 30, 0) && 
                                    easternTime.TimeOfDay <= new TimeSpan(16, 0, 0);
                
                return await Task.FromResult(isWeekday && isMarketHours);
                
            }, "Check market status", "Failed to determine market status", "Using time-based logic");
        }

        public IObservable<MarketData> SubscribeToQuoteUpdatesAsync(string symbol, TimeSpan interval)
        {
            // AlphaVantage doesn't support WebSocket, implement polling-based updates
            return System.Reactive.Linq.Observable.Create<MarketData>(async (observer, cancellationToken) =>
            {
                LogInfo($"Starting polling subscription for {symbol} with interval {interval}");
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var data = await GetRealTimeDataAsync(symbol);
                        if (data != null)
                        {
                            observer.OnNext(data);
                        }
                        
                        await Task.Delay(interval, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in quote subscription for {symbol}", ex);
                        observer.OnError(ex);
                        break;
                    }
                }
                
                observer.OnCompleted();
            });
        }

        public async Task<List<DailyData>?> FetchHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            var dailyData = await GetDailyTimeSeriesAsync(symbol, "full");
            return dailyData
                .Where(d => d.Date >= startDate && d.Date <= endDate)
                .OrderBy(d => d.Date)
                .ToList();
        }

        #endregion

        #region Abstract Method Implementations

        protected override async Task<TradingResult> ValidateConfigurationAsync()
        {
            if (string.IsNullOrEmpty(_config.AlphaVantage.ApiKey))
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConfigurationError,
                    "AlphaVantage API key is not configured");
            }

            if (string.IsNullOrEmpty(_config.AlphaVantage.BaseUrl))
            {
                return TradingResult.Failure(
                    TradingError.ErrorCodes.ConfigurationError,
                    "AlphaVantage base URL is not configured");
            }

            return await Task.FromResult(TradingResult.Success());
        }

        protected override async Task<TradingResult> TestConnectivityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Test with a simple API call
                var request = new RestRequest()
                    .AddParameter("function", "GLOBAL_QUOTE")
                    .AddParameter("symbol", "MSFT") // Use a common symbol
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);

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
                var response = await TestConnectivityAsync(cancellationToken);
                
                if (response.IsSuccess)
                {
                    return TradingResult.Success();
                }

                // Check if it's specifically an auth error
                if (response.Error?.Message.Contains("Invalid API key") ?? false)
                {
                    return TradingResult.Failure(
                        TradingError.ErrorCodes.UnauthorizedAccess,
                        "Invalid AlphaVantage API key");
                }

                return response;
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

        private async Task<MarketData> FetchGlobalQuoteAsync(string symbol)
        {
            var request = new RestRequest()
                .AddParameter("function", "GLOBAL_QUOTE")
                .AddParameter("symbol", symbol)
                .AddParameter("apikey", _config.AlphaVantage.ApiKey);

            var response = await _client.ExecuteAsync(request);
            ValidateResponse(response, symbol);

            var jsonResponse = JsonSerializer.Deserialize<AlphaVantageGlobalQuoteResponse>(response.Content!);
            if (jsonResponse?.GlobalQuote == null)
            {
                throw new InvalidOperationException($"Failed to parse global quote for {symbol}");
            }

            return MapToMarketData(jsonResponse.GlobalQuote);
        }

        private async Task<List<DailyData>> FetchDailyDataAsync(string function, string symbol, string outputSize)
        {
            var request = new RestRequest()
                .AddParameter("function", function)
                .AddParameter("symbol", symbol)
                .AddParameter("outputsize", outputSize)
                .AddParameter("apikey", _config.AlphaVantage.ApiKey);

            var response = await _client.ExecuteAsync(request);
            ValidateResponse(response, symbol);

            var jsonResponse = JsonSerializer.Deserialize<AlphaVantageTimeSeriesResponse>(response.Content!);
            if (jsonResponse?.TimeSeries == null)
            {
                return new List<DailyData>();
            }

            return jsonResponse.ToDailyData().ToList();
        }

        private void ValidateResponse(RestResponse response, string context)
        {
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException(
                    $"API request failed for {context}: {response.ErrorMessage ?? "No response"}");
            }

            // Check for API errors in response
            if (response.Content.Contains("Error Message") || 
                response.Content.Contains("Invalid API key"))
            {
                throw new InvalidOperationException(
                    $"API error for {context}: {response.Content}");
            }
        }

        private MarketData MapToMarketData(GlobalQuote quote)
        {
            return new MarketData
            {
                Symbol = quote.Symbol,
                Price = decimal.Parse(quote.Price),
                Volume = long.Parse(quote.Volume),
                Open = decimal.Parse(quote.Open),
                High = decimal.Parse(quote.High),
                Low = decimal.Parse(quote.Low),
                PreviousClose = decimal.Parse(quote.PreviousClose),
                Change = decimal.Parse(quote.Change),
                ChangePercent = ParsePercentage(quote.ChangePercent),
                Timestamp = DateTime.Parse(quote.LatestTradingDay),
                Provider = ProviderName
            };
        }

        private decimal ParsePercentage(string percentString)
        {
            // Remove the '%' sign and parse
            var cleaned = percentString.Replace("%", "").Trim();
            return decimal.Parse(cleaned);
        }

        private List<MarketData> ParseIntradayData(Dictionary<string, object> jsonResponse, string symbol)
        {
            var result = new List<MarketData>();
            
            // Find the time series key (it varies by interval)
            var timeSeriesKey = jsonResponse.Keys.FirstOrDefault(k => k.Contains("Time Series"));
            if (timeSeriesKey == null)
            {
                return result;
            }

            var timeSeries = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                jsonResponse[timeSeriesKey].ToString()!);

            if (timeSeries == null) return result;

            foreach (var kvp in timeSeries)
            {
                var timestamp = DateTime.Parse(kvp.Key);
                var data = kvp.Value;
                
                result.Add(new MarketData
                {
                    Symbol = symbol,
                    Timestamp = timestamp,
                    Open = decimal.Parse(data["1. open"]),
                    High = decimal.Parse(data["2. high"]),
                    Low = decimal.Parse(data["3. low"]),
                    Price = decimal.Parse(data["4. close"]),
                    Volume = long.Parse(data["5. volume"]),
                    Provider = ProviderName
                });
            }

            return result.OrderByDescending(d => d.Timestamp).ToList();
        }

        #endregion

        #region Lifecycle

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing AlphaVantage provider", new { BaseUrl = _config.AlphaVantage.BaseUrl });
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting AlphaVantage provider");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping AlphaVantage provider");
            _client?.Dispose();
            return Task.CompletedTask;
        }

        #endregion
    }

    #region Response Models

    public class SymbolSearchResponse
    {
        [JsonPropertyName("bestMatches")]
        public List<AlphaVantageSymbolMatch> Matches { get; set; } = new();
    }

    public class AlphaVantageSymbolMatch
    {
        [JsonPropertyName("1. symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("2. name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("3. type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("4. region")]
        public string Region { get; set; } = string.Empty;
    }

    public class AlphaVantageGlobalQuoteResponse
    {
        [JsonPropertyName("Global Quote")]
        public GlobalQuote GlobalQuote { get; set; } = new();
    }

    public class GlobalQuote
    {
        [JsonPropertyName("01. symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("02. open")]
        public string Open { get; set; } = "0";
        
        [JsonPropertyName("03. high")]
        public string High { get; set; } = "0";
        
        [JsonPropertyName("04. low")]
        public string Low { get; set; } = "0";
        
        [JsonPropertyName("05. price")]
        public string Price { get; set; } = "0";
        
        [JsonPropertyName("06. volume")]
        public string Volume { get; set; } = "0";
        
        [JsonPropertyName("07. latest trading day")]
        public string LatestTradingDay { get; set; } = string.Empty;
        
        [JsonPropertyName("08. previous close")]
        public string PreviousClose { get; set; } = "0";
        
        [JsonPropertyName("09. change")]
        public string Change { get; set; } = "0";
        
        [JsonPropertyName("10. change percent")]
        public string ChangePercent { get; set; } = "0%";
    }

    #endregion
}