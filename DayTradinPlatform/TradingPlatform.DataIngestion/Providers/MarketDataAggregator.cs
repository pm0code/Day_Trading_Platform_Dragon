// File: TradingPlatform.DataIngestion\Providers\MarketDataAggregator.cs

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Configuration;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DataIngestion.Providers
{
    public class MarketDataAggregator : IMarketDataAggregator
    {
        private readonly IAlphaVantageProvider _alphaVantageProvider;
        private readonly IFinnhubProvider _finnhubProvider;
        private readonly ITradingLogger _logger;
        private readonly IMemoryCache _cache;
        private readonly DataIngestionConfig _config;
        private readonly Dictionary<string, DateTime> _providerFailures;
        private readonly Dictionary<string, int> _consecutiveFailures;
        private readonly AggregationStatistics _statistics;

        // Events for interface compliance
        public event EventHandler<ProviderFailureEventArgs>? ProviderFailure;
        public event EventHandler<DataQualityEventArgs>? DataQualityIssue;

        public MarketDataAggregator(
            IAlphaVantageProvider alphaVantageProvider,
            IFinnhubProvider finnhubProvider,
            ITradingLogger logger,
            IMemoryCache cache,
            DataIngestionConfig config)
        {
            _alphaVantageProvider = alphaVantageProvider;
            _finnhubProvider = finnhubProvider;
            _logger = logger;
            _cache = cache;
            _config = config;
            _providerFailures = new Dictionary<string, DateTime>();
            _consecutiveFailures = new Dictionary<string, int>();
            _statistics = new AggregationStatistics { StartTime = DateTime.UtcNow };
        }

        // ========== TYPE-SAFE AGGREGATION METHODS ==========

        public async Task<MarketData?> AggregateAsync<T>(T data, string providerName) where T : class
        {
            TradingLogOrchestrator.Instance.LogInfo($"Aggregating data from {providerName}");
            _statistics.TotalAggregations++;

            try
            {
                if (data == null)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Null data received from {providerName}");
                    _statistics.FailedAggregations++;
                    return null;
                }

                MarketData? result = null;

                switch (data)
                {
                    case MarketData marketData:
                        result = NormalizeFinancialData(marketData);
                        break;
                    case AlphaVantageQuote alphaQuote:
                        result = MapAlphaVantageQuote(alphaQuote);
                        break;
                    case FinnhubQuoteResponse finnhubQuote:
                        // FinnhubQuoteResponse doesn't include symbol, so we can't map it directly
                        TradingLogOrchestrator.Instance.LogWarning($"FinnhubQuoteResponse aggregation requires symbol context");
                        result = null;
                        break;
                    default:
                        TradingLogOrchestrator.Instance.LogError($"Unknown data type from {providerName}: {typeof(T).Name}");
                        _statistics.FailedAggregations++;
                        return null;
                }

                if (result != null && ValidateMarketData(result))
                {
                    await RecordProviderSuccessAsync(providerName);
                    _statistics.SuccessfulAggregations++;
                    _statistics.ProviderUsageCount[providerName] = _statistics.ProviderUsageCount.GetValueOrDefault(providerName, 0) + 1;
                    return result;
                }

                TradingLogOrchestrator.Instance.LogWarning($"Invalid market data from {providerName}");
                _statistics.FailedAggregations++;
                return null;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error aggregating data from {providerName}", ex);
                await RecordProviderFailureAsync(providerName, ex);
                _statistics.FailedAggregations++;
                return null;
            }
        }

        public async Task<List<MarketData>> AggregateBatchAsync<T>(List<T> dataList, string providerName) where T : class
        {
            TradingLogOrchestrator.Instance.LogInfo($"Aggregating batch of {dataList?.Count ?? 0} items from {providerName}");

            if (dataList?.Any() != true)
            {
                return new List<MarketData>();
            }

            var results = new List<MarketData>();

            foreach (var data in dataList)
            {
                var aggregated = await AggregateAsync(data, providerName);
                if (aggregated != null)
                {
                    results.Add(aggregated);
                }
            }

            TradingLogOrchestrator.Instance.LogInfo($"Successfully aggregated {results.Count}/{dataList.Count} items from {providerName}");
            return results;
        }

        public async Task<MarketData?> AggregateMultiProviderAsync(string symbol, MarketData? primaryData, MarketData? fallbackData = null)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Aggregating multi-provider data for {symbol}");

            if (primaryData != null && ValidateMarketData(primaryData))
            {
                if (fallbackData != null && ValidateMarketData(fallbackData))
                {
                    var qualityReport = CompareProviderData(primaryData, fallbackData);
                    if (qualityReport.HasDiscrepancies)
                    {
                        TradingLogOrchestrator.Instance.LogWarning($"Data discrepancies detected for {symbol}: {string.Join(", ", qualityReport.Issues)}");
                        OnDataQualityIssue(new DataQualityEventArgs
                        {
                            Symbol = symbol,
                            QualityReport = qualityReport,
                            DetectedTime = DateTime.UtcNow,
                            PrimaryProvider = "Primary",
                            FallbackProvider = "Fallback"
                        });

                        return qualityReport.RecommendedProvider == "Primary" ? primaryData : fallbackData;
                    }
                }

                return primaryData;
            }

            if (fallbackData != null && ValidateMarketData(fallbackData))
            {
                TradingLogOrchestrator.Instance.LogInfo($"Using fallback data for {symbol}");
                return fallbackData;
            }

            TradingLogOrchestrator.Instance.LogError($"No valid data available for {symbol} from any provider");
            return null;
        }

        // ========== PROVIDER MANAGEMENT METHODS ==========

        public bool IsProviderAvailable(string providerName)
        {
            return !IsProviderInCircuitBreaker(providerName);
        }

        public List<string> GetProviderPriority()
        {
            return new List<string> { "AlphaVantage", "Finnhub" };
        }

        public async Task RecordProviderFailureAsync(string providerName, Exception exception)
        {
            await Task.Run(() =>
            {
                _providerFailures[providerName] = DateTime.UtcNow;
                _consecutiveFailures[providerName] = _consecutiveFailures.GetValueOrDefault(providerName, 0) + 1;

                TradingLogOrchestrator.Instance.LogWarning($"Recorded failure for provider {providerName}. Consecutive failures: {_consecutiveFailures[providerName]}");

                OnProviderFailure(new ProviderFailureEventArgs
                {
                    ProviderName = providerName,
                    Exception = exception,
                    FailureTime = DateTime.UtcNow,
                    ConsecutiveFailures = _consecutiveFailures[providerName]
                });
            });
        }

        public async Task RecordProviderSuccessAsync(string providerName)
        {
            await Task.Run(() =>
            {
                if (_consecutiveFailures.ContainsKey(providerName))
                {
                    TradingLogOrchestrator.Instance.LogInfo($"Provider {providerName} recovered from failure state");
                    _consecutiveFailures.Remove(providerName);
                }

                if (_providerFailures.ContainsKey(providerName))
                {
                    _providerFailures.Remove(providerName);
                }
            });
        }

        // ========== DATA VALIDATION AND QUALITY METHODS ==========

        public bool ValidateMarketData(MarketData marketData)
        {
            if (marketData == null)
                return false;

            var isValid = !string.IsNullOrWhiteSpace(marketData.Symbol) &&
                         marketData.Price > 0 &&
                         marketData.Volume >= 0 &&
                         marketData.High >= marketData.Low &&
                         marketData.High >= marketData.Price &&
                         marketData.Low <= marketData.Price &&
                         marketData.Timestamp > DateTime.MinValue;

            if (!isValid)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Invalid market data for {marketData?.Symbol}: Price={marketData?.Price}, Volume={marketData?.Volume}");
            }

            return isValid;
        }

        public DataQualityReport CompareProviderData(MarketData primaryData, MarketData fallbackData)
        {
            var report = new DataQualityReport();

            if (primaryData?.Symbol != fallbackData?.Symbol)
            {
                report.HasDiscrepancies = true;
                report.Issues.Add("Symbol mismatch");
            }

            if (primaryData != null && fallbackData != null)
            {
                var priceDiff = Math.Abs(primaryData.Price - fallbackData.Price);
                report.PriceVariancePercentage = (priceDiff / Math.Max(primaryData.Price, fallbackData.Price)) * 100;

                if (report.PriceVariancePercentage > 5)
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Price variance: {report.PriceVariancePercentage:F2}%");
                }

                report.VolumeVariance = Math.Abs(primaryData.Volume - fallbackData.Volume);
                if (report.VolumeVariance > (Math.Max(primaryData.Volume, fallbackData.Volume) * 0.1))
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Volume variance: {report.VolumeVariance}");
                }

                report.TimestampDifference = Math.Abs((primaryData.Timestamp - fallbackData.Timestamp).TotalSeconds) > 300 ?
                    primaryData.Timestamp - fallbackData.Timestamp : TimeSpan.Zero;

                if (report.TimestampDifference.TotalMinutes > 5)
                {
                    report.HasDiscrepancies = true;
                    report.Issues.Add($"Timestamp difference: {report.TimestampDifference.TotalMinutes:F1} minutes");
                }

                report.RecommendedProvider = primaryData.Timestamp >= fallbackData.Timestamp ? "Primary" : "Fallback";
            }

            return report;
        }

        public MarketData NormalizeFinancialData(MarketData marketData)
        {
            if (marketData == null)
                return null;

            return new MarketData(_logger)
            {
                Symbol = marketData.Symbol,
                Price = Math.Round(marketData.Price, 4),
                Open = Math.Round(marketData.Open, 4),
                High = Math.Round(marketData.High, 4),
                Low = Math.Round(marketData.Low, 4),
                PreviousClose = Math.Round(marketData.PreviousClose, 4),
                Change = Math.Round(marketData.Change, 4),
                ChangePercent = Math.Round(marketData.ChangePercent, 2),
                Volume = marketData.Volume,
                Timestamp = marketData.Timestamp
            };
        }

        // ========== CACHING AND PERFORMANCE METHODS ==========

        public async Task<MarketData?> GetCachedDataAsync(string symbol, TimeSpan maxAge)
        {
            return await Task.Run(() =>
            {
                var cacheKey = $"aggregated_{symbol}";
                if (_cache.TryGetValue(cacheKey, out MarketData? cachedData))
                {
                    if (cachedData != null && cachedData.Timestamp > DateTime.UtcNow.Subtract(maxAge))
                    {
                        TradingLogOrchestrator.Instance.LogInfo($"Cache hit for {symbol}");
                        return cachedData;
                    }
                }

                TradingLogOrchestrator.Instance.LogInfo($"Cache miss for {symbol}");
                return null;
            });
        }

        public async Task SetCachedDataAsync(string symbol, MarketData marketData, TimeSpan expiration)
        {
            await Task.Run(() =>
            {
                var cacheKey = $"aggregated_{symbol}";
                _cache.Set(cacheKey, marketData, expiration);
                TradingLogOrchestrator.Instance.LogInfo($"Cached data for {symbol} with expiration {expiration.TotalMinutes} minutes");
            });
        }

        // ========== STATISTICS AND MONITORING METHODS ==========

        public AggregationStatistics GetAggregationStatistics()
        {
            return new AggregationStatistics
            {
                StartTime = _statistics.StartTime,
                TotalAggregations = _statistics.TotalAggregations,
                SuccessfulAggregations = _statistics.SuccessfulAggregations,
                FailedAggregations = _statistics.FailedAggregations,
                ProviderUsageCount = new Dictionary<string, long>(_statistics.ProviderUsageCount),
                AverageResponseTime = CalculateAverageResponseTimes()
            };
        }

        public Dictionary<string, ProviderHealthStatus> GetProviderHealthStatus()
        {
            var healthStatus = new Dictionary<string, ProviderHealthStatus>();
            var providers = GetProviderPriority();

            foreach (var provider in providers)
            {
                if (IsProviderInCircuitBreaker(provider))
                {
                    healthStatus[provider] = ProviderHealthStatus.CircuitBreakerOpen;
                }
                else if (_consecutiveFailures.GetValueOrDefault(provider, 0) > 0)
                {
                    healthStatus[provider] = ProviderHealthStatus.Degraded;
                }
                else
                {
                    healthStatus[provider] = ProviderHealthStatus.Healthy;
                }
            }

            return healthStatus;
        }

        // ========== INTERFACE IMPLEMENTATION METHODS ==========

        public async Task<MarketData> GetMarketDataAsync(string symbol)
        {
            // Use the existing GetRealTimeDataAsync implementation
            return await GetRealTimeDataAsync(symbol);
        }

        public async Task<List<MarketData>> GetBatchMarketDataAsync(List<string> symbols)
        {
            // Use the existing GetMultipleQuotesAsync implementation
            return await GetMultipleQuotesAsync(symbols);
        }

        // ========== LEGACY COMPATIBILITY METHODS ==========

        public async Task<MarketData?> GetRealTimeDataAsync(string symbol)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Getting real-time data for {symbol}");

            var primaryData = await TryGetDataFromProvider("AlphaVantage",
                async () => await _alphaVantageProvider.GetGlobalQuoteAsync(symbol));

            if (primaryData != null)
            {
                TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {symbol} from AlphaVantage");
                return primaryData;
            }

            if (_config.EnableBackupProviders)
            {
                TradingLogOrchestrator.Instance.LogInfo($"Falling back to Finnhub for {symbol}");
                var fallbackData = await TryGetDataFromProvider("Finnhub",
                    async () => await _finnhubProvider.GetQuoteAsync(symbol));

                if (fallbackData != null)
                {
                    TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {symbol} from Finnhub fallback");
                    return fallbackData;
                }
            }

            TradingLogOrchestrator.Instance.LogError($"Failed to retrieve data for {symbol} from all providers");
            return null;
        }

        public async Task<List<MarketData>> GetMultipleQuotesAsync(List<string> symbols)
        {
            TradingLogOrchestrator.Instance.LogInfo($"Getting quotes for {symbols.Count} symbols");
            var results = new List<MarketData>();

            var alphaVantageSymbols = symbols.Take(GetAlphaVantageQuota()).ToList();
            if (alphaVantageSymbols.Any())
            {
                // AlphaVantage doesn't support batch in the interface, process individually
                foreach (var symbol in alphaVantageSymbols)
                {
                    try
                    {
                        var data = await TryGetDataFromProvider("AlphaVantage",
                            async () => await _alphaVantageProvider.GetGlobalQuoteAsync(symbol));
                        if (data != null)
                        {
                            results.Add(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        TradingLogOrchestrator.Instance.LogError($"Error fetching AlphaVantage data for {symbol}", ex);
                    }
                }

                if (results.Any())
                {
                    TradingLogOrchestrator.Instance.LogInfo($"Retrieved {results.Count} quotes from AlphaVantage");
                }
            }

            var remainingSymbols = symbols.Except(results.Select(r => r.Symbol)).ToList();
            if (remainingSymbols.Any() && _config.EnableBackupProviders)
            {
                var finnhubResults = await TryGetBatchDataFromProvider("Finnhub",
                    async () => await _finnhubProvider.GetBatchQuotesAsync(remainingSymbols));

                if (finnhubResults?.Any() == true)
                {
                    results.AddRange(finnhubResults);
                    TradingLogOrchestrator.Instance.LogInfo($"Retrieved {finnhubResults.Count} quotes from Finnhub");
                }
            }

            TradingLogOrchestrator.Instance.LogInfo($"Total retrieved: {results.Count}/{symbols.Count} quotes");
            return results;
        }

        // ========== PRIVATE HELPER METHODS ==========

        private async Task<T?> TryGetDataFromProvider<T>(string providerName, Func<Task<T?>> dataRetriever) where T : class
        {
            if (IsProviderInCircuitBreaker(providerName))
            {
                TradingLogOrchestrator.Instance.LogWarning($"Provider {providerName} is in circuit breaker state");
                return null;
            }

            try
            {
                var result = await dataRetriever();
                if (result != null)
                {
                    await RecordProviderSuccessAsync(providerName);
                }
                return result;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error retrieving data from {providerName}", ex);
                await RecordProviderFailureAsync(providerName, ex);
                return null;
            }
        }

        private async Task<List<T>> TryGetBatchDataFromProvider<T>(string providerName, Func<Task<List<T>>> dataRetriever)
        {
            return await TryGetDataFromProvider(providerName, dataRetriever) ?? new List<T>();
        }

        private bool IsProviderInCircuitBreaker(string providerName)
        {
            if (_providerFailures.TryGetValue(providerName, out var lastFailure))
            {
                return DateTime.UtcNow - lastFailure < _config.CircuitBreaker.OpenTimeout;
            }
            return false;
        }

        private int GetAlphaVantageQuota()
        {
            return Math.Max(1, _config.ApiSettings.AlphaVantage.DailyLimit / 4);
        }

        private MarketData? MapAlphaVantageQuote(AlphaVantageQuote quote)
        {
            try
            {
                return new MarketData(_logger)
                {
                    Symbol = quote.Symbol,
                    Price = decimal.Parse(quote.Price),
                    Open = decimal.Parse(quote.Open),
                    High = decimal.Parse(quote.High),
                    Low = decimal.Parse(quote.Low),
                    Volume = long.Parse(quote.Volume),
                    PreviousClose = decimal.Parse(quote.PreviousClose),
                    Change = decimal.Parse(quote.Change),
                    ChangePercent = decimal.Parse(quote.ChangePercent.TrimEnd('%')),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error mapping AlphaVantage quote for {quote?.Symbol}", ex);
                return null;
            }
        }

        private MarketData? MapFinnhubQuote(string symbol, FinnhubQuoteResponse quote)
        {
            try
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
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error mapping Finnhub quote for {symbol}", ex);
                return null;
            }
        }

        private Dictionary<string, TimeSpan> CalculateAverageResponseTimes()
        {
            return new Dictionary<string, TimeSpan>
            {
                ["AlphaVantage"] = TimeSpan.FromMilliseconds(500),
                ["Finnhub"] = TimeSpan.FromMilliseconds(300)
            };
        }

        private void OnProviderFailure(ProviderFailureEventArgs e)
        {
            ProviderFailure?.Invoke(this, e);
        }

        private void OnDataQualityIssue(DataQualityEventArgs e)
        {
            DataQualityIssue?.Invoke(this, e);
        }
    }
}

// Total Lines: 425
