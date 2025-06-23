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

namespace TradingPlatform.DataIngestion.Providers
{
    public class AlphaVantageProvider : IAlphaVantageProvider
    {
        private readonly RestClient _client;
        private readonly ITradingLogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IRateLimiter _rateLimiter;
        private readonly ApiConfiguration _config;
        
        public string ProviderName => "AlphaVantage";

        public AlphaVantageProvider(ITradingLogger logger,
            IMemoryCache cache,
            IRateLimiter rateLimiter,
            ApiConfiguration config)
        {
            _logger = logger;
            _cache = cache;
            _rateLimiter = rateLimiter;
            _config = config;
            _client = new RestClient(_config.AlphaVantage.BaseUrl);
        }

        public async Task<MarketData> GetRealTimeDataAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching real-time data for {symbol} from AlphaVantage");

            // 1. Check Cache
            string cacheKey = $"alphavantage_{symbol}_realtime";
            if (_cache.TryGetValue(cacheKey, out MarketData cachedData) &&
                cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-_config.Cache.QuoteCacheMinutes))
            {
                TradingLogOrchestrator.Instance.LogInfo($"Real-time data for {symbol} retrieved from cache.");
                return cachedData;
            }

            // 2. Rate Limiting
            await _rateLimiter.WaitForPermitAsync();

            try
            {
                // 3. Construct API Request (using GLOBAL_QUOTE function)
                var request = new RestRequest()
                    .AddParameter("function", "GLOBAL_QUOTE")
                    .AddParameter("symbol", symbol)
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                // 4. Execute API Request
                RestResponse response = await _client.ExecuteAsync(request);

                // 5. Error Handling
                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    TradingLogOrchestrator.Instance.LogError($"Error fetching real-time data from AlphaVantage for {symbol}: {response.ErrorMessage}");
                    return null;
                }

                // 6. Parse JSON Response using external model
                var jsonResponse = JsonSerializer.Deserialize<AlphaVantageGlobalQuoteResponse>(response.Content);
                if (jsonResponse?.GlobalQuote == null)
                {
                    TradingLogOrchestrator.Instance.LogError($"Failed to deserialize AlphaVantage response for {symbol}");
                    return null;
                }

                // 7. Map to MarketData (using decimal parsing)
                MarketData marketData = MapToMarketData(jsonResponse.GlobalQuote);

                // 8. Cache the Result
                _cache.Set(cacheKey, marketData, TimeSpan.FromMinutes(_config.Cache.QuoteCacheMinutes));
                TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved real-time data for {symbol} from AlphaVantage");
                return marketData;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception while fetching real-time data for {symbol} from AlphaVantage", ex);
                return null;
            }
        }

        public async Task<List<DailyData>> FetchHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            string cacheKey = $"alphavantage_{symbol}_historical_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out List<DailyData> cachedData))
            {
                TradingLogOrchestrator.Instance.LogInfo($"Historical data for {symbol} retrieved from cache");
                return cachedData;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest()
                    .AddParameter("function", "TIME_SERIES_DAILY")
                    .AddParameter("symbol", symbol)
                    .AddParameter("outputsize", "full")
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    TradingLogOrchestrator.Instance.LogError($"Failed to fetch historical data for {symbol}: {response.ErrorMessage}");
                    return new List<DailyData>();
                }

                // Use external model for deserialization
                var jsonResponse = JsonSerializer.Deserialize<TradingPlatform.Core.Models.AlphaVantageTimeSeriesResponse>(response.Content);
                if (jsonResponse?.TimeSeries == null)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"No historical data available for {symbol}");
                    return new List<DailyData>();
                }

                // Convert to DailyData using the new model's method
                var historicalData = jsonResponse.ToDailyData()
                    .Where(d => d.Date >= startDate && d.Date <= endDate)
                    .OrderBy(d => d.Date)
                    .ToList();

                // Cache for 1 hour as historical data doesn't change frequently
                _cache.Set(cacheKey, historicalData, TimeSpan.FromHours(1));
                TradingLogOrchestrator.Instance.LogInfo($"Retrieved {historicalData.Count} historical records for {symbol}");

                return historicalData;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Exception while fetching historical data for {symbol}", ex);
                return new List<DailyData>();
            }
        }

        public async Task<List<MarketData>> GetBatchRealTimeDataAsync(List<string> symbols)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching batch real-time data for {symbols.Count} symbols from AlphaVantage");
            var results = new List<MarketData>();

            // AlphaVantage doesn't support batch requests, so we process individually
            foreach (var symbol in symbols)
            {
                var marketData = await GetRealTimeDataAsync(symbol);
                if (marketData != null)
                {
                    results.Add(marketData);
                }

                // Add delay to respect API rate limits (5 requests per minute for free tier)
                await Task.Delay(TimeSpan.FromSeconds(12));
            }

            TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {results.Count}/{symbols.Count} real-time quotes from AlphaVantage");
            return results;
        }

        public IObservable<MarketData> SubscribeRealTimeData(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Setting up real-time subscription for {symbol} using AlphaVantage polling");

            // AlphaVantage doesn't support WebSocket streaming, so we implement polling-based subscription
            return System.Reactive.Linq.Observable.Create<MarketData>(async observer =>
            {
                var cancellationTokenSource = new System.Threading.CancellationTokenSource();

                _ = Task.Run(async () =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var data = await GetRealTimeDataAsync(symbol);
                            if (data != null)
                            {
                                observer.OnNext(data);
                            }
                        }
                        catch (Exception ex)
                        {
                            TradingLogOrchestrator.Instance.LogError($"Error in real-time subscription for {symbol}", ex);
                            observer.OnError(ex);
                            break;
                        }

                        // Poll every 60 seconds for free tier compliance
                        await Task.Delay(TimeSpan.FromSeconds(60), cancellationTokenSource.Token);
                    }
                });

                return cancellationTokenSource;
            });
        }

        // ========== LEGACY COMPATIBILITY METHODS (Previously NotImplementedException) ==========

        public async Task<MarketData> FetchMarketDataAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching market data for {symbol} from AlphaVantage");

            // Use GetRealTimeDataAsync as the primary implementation
            return await GetRealTimeDataAsync(symbol);
        }

        public async Task<List<MarketData>> GetBatchQuotesAsync(List<string> symbols)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Fetching batch quotes for {symbols.Count} symbols from AlphaVantage");

            // Use GetBatchRealTimeDataAsync as the primary implementation
            return await GetBatchRealTimeDataAsync(symbols);
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

        private MarketData MapToMarketData(TradingPlatform.Core.Models.AlphaVantageQuote quote)
        {
            try
            {
                return new MarketData(_logger)
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
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error mapping AlphaVantage quote data for {quote?.Symbol}", ex);
                return null;
            }
        }
        
        // ========== IAlphaVantageProvider SPECIFIC METHODS ==========
        
        public async Task<MarketData> GetGlobalQuoteAsync(string symbol)
        {
            // This is already implemented as GetRealTimeDataAsync
            return await GetRealTimeDataAsync(symbol);
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
                    .AddParameter("apikey", _config.AlphaVantage.ApiKey);
                    
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
        
        public async Task<bool> IsMarketOpenAsync()
        {
            // AlphaVantage doesn't have a dedicated market status endpoint
            // Use time-based logic
            var now = DateTime.Now;
            var estTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            
            var isWeekday = estTime.DayOfWeek >= DayOfWeek.Monday && estTime.DayOfWeek <= DayOfWeek.Friday;
            var isMarketHours = estTime.TimeOfDay >= TimeSpan.FromHours(9.5) && estTime.TimeOfDay <= TimeSpan.FromHours(16);
            
            return await Task.FromResult(isWeekday && isMarketHours);
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
