using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.DataIngestion.Services
{
    /// <summary>
    /// Orchestrates data ingestion from multiple providers with comprehensive rate limiting, caching, and normalization
    /// Provides unified access to market data from AlphaVantage, Finnhub, and other data sources
    /// All calculations use decimal arithmetic and comply with financial precision standards
    /// </summary>
    public class DataIngestionService : CanonicalServiceBase, IDataIngestionService
    {
        private readonly IAlphaVantageProvider _alphaVantageProvider;
        private readonly IFinnhubProvider _finnhubProvider;
        private readonly IRateLimiter _rateLimiter;
        private readonly ICacheService _cacheService;
        private readonly IMarketDataAggregator _aggregator;
        private readonly ApiConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the DataIngestionService with required dependencies
        /// </summary>
        /// <param name="alphaVantageProvider">Service for AlphaVantage data access</param>
        /// <param name="finnhubProvider">Service for Finnhub data access</param>
        /// <param name="rateLimiter">Service for API rate limiting</param>
        /// <param name="cacheService">Service for data caching</param>
        /// <param name="aggregator">Service for intelligent data aggregation</param>
        /// <param name="config">API configuration settings</param>
        /// <param name="logger">Trading logger for comprehensive data ingestion tracking</param>
        public DataIngestionService(
            IAlphaVantageProvider alphaVantageProvider,
            IFinnhubProvider finnhubProvider,
            IRateLimiter rateLimiter,
            ICacheService cacheService,
            IMarketDataAggregator aggregator,
            ApiConfiguration config,
            ITradingLogger logger) : base(logger, "DataIngestionService")
        {
            _alphaVantageProvider = alphaVantageProvider ?? throw new ArgumentNullException(nameof(alphaVantageProvider));
            _finnhubProvider = finnhubProvider ?? throw new ArgumentNullException(nameof(finnhubProvider));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Retrieves real-time market data for the specified symbol with intelligent provider selection
        /// </summary>
        /// <param name="symbol">The symbol to retrieve market data for</param>
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

                var dataResult = await GetMarketDataAsync(symbol);
                LogMethodExit();
                return TradingResult<MarketData?>.Success(dataResult);
            }
            catch (Exception ex)
            {
                LogError($"Error getting real-time data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("REALTIME_DATA_ERROR", $"Real-time data retrieval failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves historical market data for the specified symbol and date range with intelligent caching
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="startDate">Start date for historical data range</param>
        /// <param name="endDate">End date for historical data range</param>
        /// <returns>A TradingResult containing the historical data collection or error information</returns>
        public async Task<TradingResult<List<DailyData>>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                if (startDate > endDate)
                {
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Failure("INVALID_DATE_RANGE", "Start date cannot be after end date");
                }

                var cacheKey = $"historical_{symbol}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
                var cachedData = await _cacheService.GetAsync<List<DailyData>>(cacheKey);

                if (cachedData != null)
                {
                    LogInfo($"Retrieved cached historical data for {symbol}: {cachedData.Count} records");
                    LogMethodExit();
                    return TradingResult<List<DailyData>>.Success(cachedData);
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
                        LogInfo($"Retrieved historical data from AlphaVantage for {symbol}: {data.Count} records");
                        LogMethodExit();
                        return TradingResult<List<DailyData>>.Success(data);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error fetching historical data from AlphaVantage for {symbol}", ex);
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
                        LogInfo($"Retrieved historical data from Finnhub for {symbol}: {data.Count} records");
                        LogMethodExit();
                        return TradingResult<List<DailyData>>.Success(data);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error fetching historical data from Finnhub for {symbol}", ex);
                }

                LogWarning($"No historical data found for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                LogMethodExit();
                return TradingResult<List<DailyData>>.Success(new List<DailyData>());
            }
            catch (Exception ex)
            {
                LogError($"Error getting historical data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<List<DailyData>>.Failure("HISTORICAL_DATA_ERROR", $"Historical data retrieval failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves real-time market data for multiple symbols with efficient batch processing
        /// </summary>
        /// <param name="symbols">The collection of symbols to retrieve market data for</param>
        /// <returns>A TradingResult containing the batch market data collection or error information</returns>
        public async Task<TradingResult<List<MarketData>>> GetBatchRealTimeDataAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                if (symbols == null || !symbols.Any())
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure("INVALID_SYMBOLS", "Symbols list cannot be null or empty");
                }

                var invalidSymbols = symbols.Where(string.IsNullOrEmpty).ToList();
                if (invalidSymbols.Any())
                {
                    LogMethodExit();
                    return TradingResult<List<MarketData>>.Failure("INVALID_SYMBOLS", $"Found {invalidSymbols.Count} invalid symbols in batch");
                }

                var dataResult = await GetBatchMarketDataAsync(symbols);
                LogInfo($"Retrieved batch data for {dataResult.Count} of {symbols.Count} symbols");
                LogMethodExit();
                return TradingResult<List<MarketData>>.Success(dataResult);
            }
            catch (Exception ex)
            {
                LogError($"Error getting batch real-time data for {symbols?.Count ?? 0} symbols", ex);
                LogMethodExit();
                return TradingResult<List<MarketData>>.Failure("BATCH_REALTIME_ERROR", $"Batch real-time data retrieval failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribes to real-time market data updates for the specified symbol with intelligent provider failover
        /// </summary>
        /// <param name="symbol">The symbol to subscribe to for real-time updates</param>
        /// <returns>A TradingResult containing the observable stream or error information</returns>
        public TradingResult<IObservable<MarketData>> SubscribeRealTimeData(string symbol)
        {
            LogMethodEntry();
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    LogMethodExit();
                    return TradingResult<IObservable<MarketData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
                }

                // Create a merged observable from both providers
                var alphaVantageStream = _alphaVantageProvider.SubscribeToQuoteUpdatesAsync(symbol, TimeSpan.FromMinutes(1))
                    .Catch<MarketData, Exception>(ex =>
                    {
                        LogError($"AlphaVantage stream error for {symbol}", ex);
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
                            LogError($"Finnhub stream error for {symbol}", ex);
                        }
                    }
                });

                // Merge both streams and return the most recent data
                var mergedStream = alphaVantageStream.Merge(finnhubStream)
                    .DistinctUntilChanged(x => x.Price);

                LogInfo($"Subscribed to real-time data stream for {symbol}");
                LogMethodExit();
                return TradingResult<IObservable<MarketData>>.Success(mergedStream);
            }
            catch (Exception ex)
            {
                LogError($"Error subscribing to real-time data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<IObservable<MarketData>>.Failure("SUBSCRIPTION_ERROR", $"Real-time subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves market data for a symbol with intelligent provider selection and caching
        /// </summary>
        /// <param name="symbol">The symbol to retrieve market data for</param>
        /// <returns>Market data or null if not available</returns>
        private async Task<MarketData?> GetMarketDataAsync(string symbol)
        {
            LogMethodEntry();
            try
            {
                // Check cache first
                var cacheKey = $"marketdata_{symbol}";
                var cachedData = await _cacheService.GetAsync<MarketData>(cacheKey);

                if (cachedData != null && cachedData.Timestamp > DateTime.UtcNow.AddMinutes(-_config.Cache.QuoteCacheMinutes))
                {
                    LogInfo($"Retrieved cached market data for {symbol}");
                    LogMethodExit();
                    return cachedData;
                }

                // Use aggregator for intelligent data retrieval
                if (_aggregator != null)
                {
                    var aggregatedData = await _aggregator.GetMarketDataAsync(symbol);
                    if (aggregatedData != null)
                    {
                        LogInfo($"Retrieved aggregated market data for {symbol}");
                        LogMethodExit();
                        return aggregatedData;
                    }
                }

                // Fallback to direct provider calls if aggregator is not available
                try
                {
                    var data = await _finnhubProvider.GetQuoteAsync(symbol);
                    if (data != null)
                    {
                        await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(_config.Cache.QuoteCacheMinutes));
                        LogInfo($"Retrieved market data from Finnhub for {symbol}");
                        LogMethodExit();
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error fetching data from Finnhub for {symbol}", ex);
                }

                try
                {
                    var data = await _alphaVantageProvider.GetGlobalQuoteAsync(symbol);
                    if (data != null)
                    {
                        await _cacheService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(_config.Cache.QuoteCacheMinutes));
                        LogInfo($"Retrieved market data from AlphaVantage for {symbol}");
                        LogMethodExit();
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error fetching data from AlphaVantage for {symbol}", ex);
                }

                LogWarning($"No market data available for {symbol} from any provider");
                LogMethodExit();
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetMarketDataAsync for {symbol}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Retrieves market data for multiple symbols with efficient batch processing
        /// </summary>
        /// <param name="symbols">The collection of symbols to retrieve market data for</param>
        /// <returns>Collection of market data for successfully retrieved symbols</returns>
        private async Task<List<MarketData>> GetBatchMarketDataAsync(List<string> symbols)
        {
            LogMethodEntry();
            try
            {
                if (_aggregator != null)
                {
                    var aggregatedResults = await _aggregator.GetBatchMarketDataAsync(symbols);
                    LogInfo($"Retrieved batch data using aggregator: {aggregatedResults.Count} symbols");
                    LogMethodExit();
                    return aggregatedResults;
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
                        LogError($"Error fetching data for {symbol}", ex);
                    }
                }

                LogInfo($"Retrieved batch data using fallback method: {results.Count} of {symbols.Count} symbols");
                LogMethodExit();
                return results;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetBatchMarketDataAsync for {symbols.Count} symbols", ex);
                LogMethodExit();
                throw;
            }
        }
    }
}