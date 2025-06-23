// File: TradingPlatform.DataIngestion\Services\DataIngestionService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.Services
{
    /// <summary>
    /// Orchestrates data ingestion from multiple providers, applying rate limiting, caching, and normalization.
    /// All calculations use decimal arithmetic and comply with FinancialCalculationStandards.md.
    /// </summary>
    public class DataIngestionService : IDataIngestionService
    {
        private readonly ITradingLogger _logger;
        private readonly IAlphaVantageProvider _alphaVantageProvider;
        private readonly IFinnhubProvider _finnhubProvider;
        private readonly IRateLimiter _rateLimiter;
        private readonly ICacheService _cacheService;
        private readonly IMarketDataAggregator _aggregator;
        private readonly ApiConfiguration _config;

        public DataIngestionService(ITradingLogger logger,
                                      IAlphaVantageProvider alphaVantageProvider,
                                      IFinnhubProvider finnhubProvider,
                                      IRateLimiter rateLimiter,
                                      ICacheService cacheService,
                                      IMarketDataAggregator aggregator,
                                      ApiConfiguration config)
        {
            _logger = logger;
            _alphaVantageProvider = alphaVantageProvider;
            _finnhubProvider = finnhubProvider;
            _rateLimiter = rateLimiter;
            _cacheService = cacheService;
            _aggregator = aggregator;
            _config = config;
        }

        public async Task<MarketData> GetRealTimeDataAsync(string symbol)
        {
            return await GetMarketDataAsync(symbol);
        }

        public async Task<List<DailyData>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            var cacheKey = $"historical_{symbol}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
            var cachedData = await _cacheService.GetAsync<List<DailyData>>(cacheKey);
            
            if (cachedData != null)
            {
                return cachedData;
            }
            
            // Try AlphaVantage first
            try
            {
                var data = await _alphaVantageProvider.GetDailyTimeSeriesAsync(symbol, "full");
                // Filter data by date range
                data = data?.Where(d => d.Date >= startDate && d.Date <= endDate).ToList();
                if (data?.Any() == true)
                {
                    await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromHours(_config.Cache.HistoricalCacheHours));
                    return data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching historical data from AlphaVantage for {symbol}", ex);
            }
            
            // Fallback to Finnhub
            try
            {
                // Finnhub doesn't have a direct historical data method in the interface
                // Use candle data for historical information
                var candleData = await _finnhubProvider.GetCandleDataAsync(symbol, "D", startDate, endDate);
                var data = candleData != null ? new List<DailyData> { new DailyData 
                { 
                    Symbol = symbol,
                    Date = endDate,
                    Open = candleData.Open,
                    High = candleData.High,
                    Low = candleData.Low,
                    Close = candleData.Price,
                    Volume = candleData.Volume,
                    AdjustedClose = candleData.Price
                }} : null;
                if (data?.Any() == true)
                {
                    await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromHours(_config.Cache.HistoricalCacheHours));
                    return data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching historical data from Finnhub for {symbol}", ex);
            }
            
            return new List<DailyData>();
        }

        public async Task<List<MarketData>> GetBatchRealTimeDataAsync(List<string> symbols)
        {
            return await GetBatchMarketDataAsync(symbols);
        }

        public IObservable<MarketData> SubscribeRealTimeData(string symbol)
        {
            // Create a merged observable from both providers
            var alphaVantageStream = _alphaVantageProvider.SubscribeToQuoteUpdatesAsync(symbol, TimeSpan.FromMinutes(1))
                .Catch<MarketData, Exception>(ex =>
                {
                    _logger.LogError($"AlphaVantage stream error for {symbol}", ex);
                    return Observable.Empty<MarketData>();
                });
                
            var finnhubStream = Observable.Create<MarketData>(async observer =>
            {
                while (true)
                {
                    try
                    {
                        var data = await _finnhubProvider.GetQuoteAsync(symbol);
                        if (data != null)
                        {
                            observer.OnNext(data);
                        }
                        await Task.Delay(TimeSpan.FromSeconds(30)); // Poll every 30 seconds
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Finnhub stream error for {symbol}", ex);
                    }
                }
            });
            
            // Merge both streams and return the most recent data
            return alphaVantageStream.Merge(finnhubStream)
                .DistinctUntilChanged(x => x.Price);
        }

        private async Task<MarketData> GetMarketDataAsync(string symbol)
        {
            // Check cache first
            var cacheKey = $"marketdata_{symbol}";
            var cachedData = await _cacheService.GetAsync<MarketData>(cacheKey);
            
            if (cachedData != null && cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-_config.Cache.QuoteCacheMinutes))
            {
                return cachedData;
            }
            
            // Use aggregator for intelligent data retrieval
            if (_aggregator != null)
            {
                return await _aggregator.GetMarketDataAsync(symbol);
            }
            
            // Fallback to direct provider calls if aggregator is not available
            try
            {
                var data = await _finnhubProvider.GetQuoteAsync(symbol);
                if (data != null)
                {
                    await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(_config.Cache.QuoteCacheMinutes));
                    return data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching data from Finnhub for {symbol}", ex);
            }
            
            try
            {
                var data = await _alphaVantageProvider.GetGlobalQuoteAsync(symbol);
                if (data != null)
                {
                    await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(_config.Cache.QuoteCacheMinutes));
                    return data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching data from AlphaVantage for {symbol}", ex);
            }
            
            return null;
        }

        private async Task<List<MarketData>> GetBatchMarketDataAsync(List<string> symbols)
        {
            if (_aggregator != null)
            {
                return await _aggregator.GetBatchMarketDataAsync(symbols);
            }
            
            // Fallback implementation if aggregator is not available
            var results = new List<MarketData>();
            
            foreach (var symbol in symbols)
            {
                try
                {
                    var data = await GetMarketDataAsync(symbol);
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error fetching data for {symbol}", ex);
                }
            }
            
            return results;
        }
    }
}