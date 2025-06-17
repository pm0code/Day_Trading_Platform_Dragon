// File: TradingPlatform.DataIngestion\Providers\FinnhubProvider.cs

using Microsoft.Extensions.Logging;
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

namespace TradingPlatform.DataIngestion.Providers
{
    public class FinnhubProvider : IFinnhubProvider
    {
        private readonly RestClient _client;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IRateLimiter _rateLimiter;
        private readonly ApiConfiguration _config;

        public FinnhubProvider(ILogger logger, IMemoryCache cache,
            IRateLimiter rateLimiter, ApiConfiguration config)
        {
            _logger = logger;
            _cache = cache;
            _rateLimiter = rateLimiter;
            _config = config;
            _client = new RestClient(_config.Finnhub.BaseUrl);
        }

        public async Task<MarketData> GetQuoteAsync(string symbol)
        {
            _logger.LogInformation($"Fetching quote for {symbol} from Finnhub");

            // 1. Check Cache
            string cacheKey = $"finnhub_{symbol}_quote";
            if (_cache.TryGetValue(cacheKey, out MarketData cachedData) &&
                cachedData.Timestamp > DateTime.UtcNow.AddSeconds(-_config.Cache.QuoteCacheSeconds))
            {
                _logger.LogTrace($"Quote for {symbol} retrieved from cache.");
                return cachedData;
            }

            // 2. Rate Limiting
            await _rateLimiter.WaitForPermitAsync();

            try
            {
                // 3. Construct API Request
                var request = new RestRequest("/quote")
                    .AddParameter("symbol", symbol)
                    .AddParameter("token", _config.Finnhub.ApiKey);

                // 4. Execute API Request
                RestResponse response = await _client.ExecuteAsync(request);

                // 5. Error Handling
                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogError($"Failed to get Finnhub quote for {symbol}: {response.ErrorMessage}");
                    return null;
                }

                // 6. Parse JSON Response
                var jsonResponse = JsonSerializer.Deserialize<FinnhubQuoteResponse>(response.Content);
                if (jsonResponse == null)
                {
                    _logger.LogError($"Failed to deserialize Finnhub quote response for {symbol}");
                    return null;
                }

                // 7. Map to MarketData
                var marketData = MapToMarketData(symbol, jsonResponse);

                // 8. Cache the Result
                _cache.Set(cacheKey, marketData, TimeSpan.FromSeconds(_config.Cache.QuoteCacheSeconds));
                _logger.LogTrace($"Successfully retrieved Finnhub quote for {symbol}");
                return marketData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching quote for {symbol} from Finnhub");
                return null;
            }
        }

        public async Task<List<MarketData>> GetBatchQuotesAsync(List<string> symbols)
        {
            _logger.LogInformation($"Fetching batch quotes for {symbols.Count} symbols from Finnhub");
            var results = new List<MarketData>();

            foreach (var symbol in symbols)
            {
                var quote = await GetQuoteAsync(symbol);
                if (quote != null)
                {
                    results.Add(quote);
                }

                // Small delay between requests to respect rate limits
                await Task.Delay(100);
            }

            _logger.LogInformation($"Successfully retrieved {results.Count}/{symbols.Count} quotes from Finnhub");
            return results;
        }

        public async Task<MarketData> GetCandleDataAsync(string symbol, string resolution, DateTime from, DateTime to)
        {
            _logger.LogInformation($"Fetching candle data for {symbol} from Finnhub");

            // Check cache
            string cacheKey = $"finnhub_{symbol}_candle_{resolution}_{from:yyyyMMdd}_{to:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out MarketData cachedData))
            {
                return cachedData;
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
                    .AddParameter("token", _config.Finnhub.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogError($"Failed to get Finnhub candle data for {symbol}: {response.ErrorMessage}");
                    return null;
                }

                var jsonResponse = JsonSerializer.Deserialize<FinnhubCandleResponse>(response.Content);
                if (jsonResponse?.Close?.Any() != true)
                {
                    _logger.LogWarning($"No candle data available for {symbol}");
                    return null;
                }

                var marketData = new MarketData(_logger)
                {
                    Symbol = symbol,
                    Price = jsonResponse.Close.Last(),
                    Open = jsonResponse.Open.Last(),
                    High = jsonResponse.High.Last(),
                    Low = jsonResponse.Low.Last(),
                    Volume = jsonResponse.Volume?.Last() ?? 0,
                    Timestamp = DateTime.UtcNow
                };

                _cache.Set(cacheKey, marketData, TimeSpan.FromMinutes(15));
                return marketData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching candle data for {symbol} from Finnhub");
                return null;
            }
        }

        public async Task<List<DailyData>> FetchHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            var candleData = await GetCandleDataAsync(symbol, "D", startDate, endDate);
            if (candleData == null)
            {
                return new List<DailyData>();
            }

            // Convert MarketData to DailyData format
            var dailyData = new DailyData
            {
                Symbol = candleData.Symbol,
                Date = endDate,
                Open = candleData.Open,
                High = candleData.High,
                Low = candleData.Low,
                Close = candleData.Price,
                Volume = candleData.Volume,
                AdjustedClose = candleData.Price
            };

            return new List<DailyData> { dailyData };
        }

        // ========== NEWLY IMPLEMENTED MISSING METHODS ==========

        public async Task<List<string>> GetStockSymbolsAsync(string exchange = "US")
        {
            _logger.LogInformation($"Fetching stock symbols for exchange: {exchange}");

            string cacheKey = $"finnhub_symbols_{exchange}";
            if (_cache.TryGetValue(cacheKey, out List<string> cachedSymbols))
            {
                _logger.LogTrace($"Stock symbols for {exchange} retrieved from cache");
                return cachedSymbols;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest("/stock/symbol")
                    .AddParameter("exchange", exchange)
                    .AddParameter("token", _config.Finnhub.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogError($"Failed to get stock symbols from Finnhub: {response.ErrorMessage}");
                    return new List<string>();
                }

                var symbolsResponse = JsonSerializer.Deserialize<List<FinnhubSymbol>>(response.Content);
                var symbols = symbolsResponse?.Select(s => s.Symbol).ToList() ?? new List<string>();

                // Cache for 4 hours as symbols don't change frequently
                _cache.Set(cacheKey, symbols, TimeSpan.FromHours(4));
                _logger.LogInformation($"Retrieved {symbols.Count} symbols for exchange {exchange}");

                return symbols;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching stock symbols for {exchange}");
                return new List<string>();
            }
        }

        public async Task<bool> IsMarketOpenAsync()
        {
            _logger.LogInformation("Checking market status from Finnhub");

            string cacheKey = "finnhub_market_status";
            if (_cache.TryGetValue(cacheKey, out bool cachedStatus))
            {
                return cachedStatus;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest("/stock/market-status")
                    .AddParameter("exchange", "US")
                    .AddParameter("token", _config.Finnhub.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogWarning($"Failed to get market status from Finnhub: {response.ErrorMessage}");
                    // Fallback to time-based check
                    return IsMarketOpenTimeBasedCheck();
                }

                var statusResponse = JsonSerializer.Deserialize<FinnhubMarketStatus>(response.Content);
                bool isOpen = statusResponse?.IsOpen ?? false;

                // Cache for 1 minute as market status can change
                _cache.Set(cacheKey, isOpen, TimeSpan.FromMinutes(1));
                _logger.LogInformation($"Market status: {(isOpen ? "Open" : "Closed")}");

                return isOpen;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while checking market status");
                return IsMarketOpenTimeBasedCheck();
            }
        }

        public async Task<SentimentData> GetSentimentAsync(string symbol)
        {
            _logger.LogInformation($"Fetching sentiment data for {symbol}");

            string cacheKey = $"finnhub_{symbol}_sentiment";
            if (_cache.TryGetValue(cacheKey, out SentimentData cachedSentiment))
            {
                return cachedSentiment;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest("/stock/insider-sentiment")
                    .AddParameter("symbol", symbol)
                    .AddParameter("from", DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"))
                    .AddParameter("to", DateTime.UtcNow.ToString("yyyy-MM-dd"))
                    .AddParameter("token", _config.Finnhub.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogWarning($"Failed to get sentiment data for {symbol}: {response.ErrorMessage}");
                    return new SentimentData { Symbol = symbol, Sentiment = "neutral", Confidence = 0.5m };
                }

                var sentimentResponse = JsonSerializer.Deserialize<FinnhubSentimentResponse>(response.Content);
                var sentimentData = MapToSentimentData(symbol, sentimentResponse);

                // Cache sentiment for 1 hour
                _cache.Set(cacheKey, sentimentData, TimeSpan.FromHours(1));

                return sentimentData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching sentiment for {symbol}");
                return new SentimentData { Symbol = symbol, Sentiment = "neutral", Confidence = 0.5m };
            }
        }

        public async Task<List<NewsItem>> GetMarketNewsAsync(string category = "general")
        {
            _logger.LogInformation($"Fetching market news for category: {category}");

            string cacheKey = $"finnhub_news_{category}";
            if (_cache.TryGetValue(cacheKey, out List<NewsItem> cachedNews))
            {
                return cachedNews;
            }

            await _rateLimiter.WaitForPermitAsync();

            try
            {
                var request = new RestRequest("/news")
                    .AddParameter("category", category)
                    .AddParameter("token", _config.Finnhub.ApiKey);

                RestResponse response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogWarning($"Failed to get market news: {response.ErrorMessage}");
                    return new List<NewsItem>();
                }

                var newsResponse = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(response.Content);
                var newsItems = newsResponse?.Select(MapToNewsItem).ToList() ?? new List<NewsItem>();

                // Cache news for 15 minutes
                _cache.Set(cacheKey, newsItems, TimeSpan.FromMinutes(15));
                _logger.LogInformation($"Retrieved {newsItems.Count} news items");

                return newsItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while fetching market news");
                return new List<NewsItem>();
            }
        }

        public async Task<bool> IsRateLimitReachedAsync()
        {
            // Check with rate limiter component
            return await Task.FromResult(_rateLimiter.IsLimitReached());
        }

        public async Task<int> GetRemainingCallsAsync()
        {
            // Get remaining calls from rate limiter
            return await Task.FromResult(_rateLimiter.GetRemainingCalls());
        }

        // ========== PRIVATE HELPER METHODS ==========

        private MarketData MapToMarketData(string symbol, FinnhubQuoteResponse quote)
        {
            return new MarketData(_logger)
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
        }

        private SentimentData MapToSentimentData(string symbol, FinnhubSentimentResponse response)
        {
            // Calculate sentiment based on insider trading data
            var totalChange = response.Data?.Sum(d => d.Change) ?? 0;
            var sentiment = totalChange > 0 ? "positive" : totalChange < 0 ? "negative" : "neutral";
            var confidence = Math.Min(Math.Abs(totalChange) / 1000000m, 1.0m); // Normalize to 0-1

            return new SentimentData
            {
                Symbol = symbol,
                Sentiment = sentiment,
                Confidence = confidence,
                Timestamp = DateTime.UtcNow
            };
        }

        private NewsItem MapToNewsItem(FinnhubNewsItem finnhubNews)
        {
            return new NewsItem
            {
                Id = finnhubNews.Id.ToString(),
                Title = finnhubNews.Headline,
                Summary = finnhubNews.Summary,
                Source = finnhubNews.Source,
                Url = finnhubNews.Url,
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(finnhubNews.Datetime).DateTime,
                Category = finnhubNews.Category,
                Sentiment = "neutral" // Default sentiment
            };
        }

        private bool IsMarketOpenTimeBasedCheck()
        {
            var now = DateTime.Now;
            var estTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

            // Market is open Monday-Friday 9:30 AM to 4:00 PM EST
            var isWeekday = estTime.DayOfWeek >= DayOfWeek.Monday && estTime.DayOfWeek <= DayOfWeek.Friday;
            var isMarketHours = estTime.TimeOfDay >= TimeSpan.FromHours(9.5) && estTime.TimeOfDay <= TimeSpan.FromHours(16);

            return isWeekday && isMarketHours;
        }
    }
}
