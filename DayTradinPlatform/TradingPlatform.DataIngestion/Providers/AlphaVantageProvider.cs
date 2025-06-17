// File: TradingPlatform.DataIngestion\Providers\AlphaVantageProvider.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using System.Text.Json;

namespace TradingPlatform.DataIngestion.Providers
{
    public class AlphaVantageProvider : IAlphaVantageProvider
    {
        private readonly RestClient _client;
        private readonly ILogger<AlphaVantageProvider> _logger;
        private readonly IMemoryCache _cache;
        private readonly IRateLimiter _rateLimiter;
        private readonly ApiConfiguration _config;

        public AlphaVantageProvider(ILogger<AlphaVantageProvider> logger,
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
            _logger.LogInformation($"Fetching real-time data for {symbol} from AlphaVantage");

            // 1. Check Cache
            string cacheKey = $"alphavantage_{symbol}_realtime";
            if (_cache.TryGetValue(cacheKey, out MarketData cachedData) &&
                cachedData.Timestamp > DateTime.UtcNow.AddSeconds(-_config.Cache.RealTimeCacheSeconds))
            {
                _logger.LogTrace($"Real-time data for {symbol} retrieved from cache.");
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
                    _logger.LogError($"Error fetching real-time data from AlphaVantage for {symbol}: {response.ErrorMessage}");
                    return null;
                }

                // 6. Parse JSON Response using external model
                var jsonResponse = JsonSerializer.Deserialize<AlphaVantageGlobalQuoteResponse>(response.Content);
                if (jsonResponse?.GlobalQuote == null)
                {
                    _logger.LogError($"Failed to deserialize AlphaVantage response for {symbol}");
                    return null;
                }

                // 7. Map to MarketData (using decimal parsing)
                MarketData marketData = MapToMarketData(jsonResponse.GlobalQuote);

                // 8. Cache the Result
                _cache.Set(cacheKey, marketData, TimeSpan.FromSeconds(_config.Cache.RealTimeCacheSeconds));
                _logger.LogTrace($"Successfully retrieved real-time data for {symbol} from AlphaVantage");
                return marketData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching real-time data for {symbol} from AlphaVantage");
                return null;
            }
        }

        public async Task<List<DailyData>> FetchHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            string cacheKey = $"alphavantage_{symbol}_historical_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out List<DailyData> cachedData))
            {
                _logger.LogTrace($"Historical data for {symbol} retrieved from cache");
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
                    _logger.LogError($"Failed to fetch historical data for {symbol}: {response.ErrorMessage}");
                    return new List<DailyData>();
                }

                // Use external model for deserialization
                var jsonResponse = JsonSerializer.Deserialize<AlphaVantageTimeSeriesResponse>(response.Content);
                if (jsonResponse?.TimeSeries == null)
                {
                    _logger.LogWarning($"No historical data available for {symbol}");
                    return new List<DailyData>();
                }

                // Convert to DailyData using the new model's method
                var historicalData = jsonResponse.ToDailyData()
                    .Where(d => d.Date >= startDate && d.Date <= endDate)
                    .OrderBy(d => d.Date)
                    .ToList();

                // Cache for 1 hour as historical data doesn't change frequently
                _cache.Set(cacheKey, historicalData, TimeSpan.FromHours(1));
                _logger.LogInformation($"Retrieved {historicalData.Count} historical records for {symbol}");

                return historicalData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching historical data for {symbol}");
                return new List<DailyData>();
            }
        }

        public async Task<List<MarketData>> GetBatchRealTimeDataAsync(List<string> symbols)
        {
            _logger.LogInformation($"Fetching batch real-time data for {symbols.Count} symbols from AlphaVantage");
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

            _logger.LogInformation($"Successfully retrieved {results.Count}/{symbols.Count} real-time quotes from AlphaVantage");
            return results;
        }

        public IObservable<MarketData> SubscribeRealTimeData(string symbol)
        {
            _logger.LogInformation($"Setting up real-time subscription for {symbol} using AlphaVantage polling");

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
                            _logger.LogError(ex, $"Error in real-time subscription for {symbol}");
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
            _logger.LogInformation($"Fetching market data for {symbol} from AlphaVantage");

            // Use GetRealTimeDataAsync as the primary implementation
            return await GetRealTimeDataAsync(symbol);
        }

        public async Task<List<MarketData>> GetBatchQuotesAsync(List<string> symbols)
        {
            _logger.LogInformation($"Fetching batch quotes for {symbols.Count} symbols from AlphaVantage");

            // Use GetBatchRealTimeDataAsync as the primary implementation
            return await GetBatchRealTimeDataAsync(symbols);
        }

        public async Task<MarketData> GetDailyDataAsync(string symbol, int days)
        {
            _logger.LogInformation($"Fetching {days} days of data for {symbol} from AlphaVantage");

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
            _logger.LogInformation($"Fetching quote for {symbol} from AlphaVantage");

            // Use GetRealTimeDataAsync as the primary implementation
            return await GetRealTimeDataAsync(symbol);
        }

        // ========== PRIVATE HELPER METHODS ==========

        private MarketData MapToMarketData(AlphaVantageQuote quote)
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
                _logger.LogError(ex, $"Error mapping AlphaVantage quote data for {quote?.Symbol}");
                return null;
            }
        }
    }
}

// Total Lines: 254
