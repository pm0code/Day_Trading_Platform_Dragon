using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;

namespace TradingPlatform.Backtesting.Data
{
    /// <summary>
    /// Comprehensive historical data manager with efficient storage, retrieval, and caching
    /// </summary>
    public class HistoricalDataManager : CanonicalServiceBase, IHistoricalDataProvider
    {
        private readonly IMarketDataProvider _primaryDataProvider;
        private readonly IMarketDataProvider? _secondaryDataProvider;
        private readonly ConcurrentDictionary<string, CachedDataSet> _cache;
        private readonly SemaphoreSlim _cacheLock;
        private readonly HistoricalDataConfiguration _configuration;
        private readonly DataQualityValidator _qualityValidator;
        private readonly CorporateActionProcessor _corporateActionProcessor;
        private readonly TimeSeriesIndex _timeSeriesIndex;
        
        private long _totalCacheSizeBytes;
        private long _cacheHits;
        private long _cacheMisses;
        private readonly Stopwatch _startupTime;

        public HistoricalDataManager(
            ITradingLogger logger,
            IMarketDataProvider primaryDataProvider,
            IMarketDataProvider? secondaryDataProvider = null,
            HistoricalDataConfiguration? configuration = null)
            : base(logger, nameof(HistoricalDataManager))
        {
            _primaryDataProvider = primaryDataProvider ?? throw new ArgumentNullException(nameof(primaryDataProvider));
            _secondaryDataProvider = secondaryDataProvider;
            _configuration = configuration ?? new HistoricalDataConfiguration();
            
            _cache = new ConcurrentDictionary<string, CachedDataSet>();
            _cacheLock = new SemaphoreSlim(1, 1);
            _qualityValidator = new DataQualityValidator(logger);
            _corporateActionProcessor = new CorporateActionProcessor(logger);
            _timeSeriesIndex = new TimeSeriesIndex();
            _startupTime = Stopwatch.StartNew();

            LogDebug("Historical data manager initialized", new
            {
                PrimaryProvider = _primaryDataProvider.GetType().Name,
                SecondaryProvider = _secondaryDataProvider?.GetType().Name,
                MaxCacheSizeMB = _configuration.MaxCacheSizeMB,
                EnableCompression = _configuration.EnableCompression
            });
        }

        #region IHistoricalDataProvider Implementation

        public async Task<TradingResult<IEnumerable<PriceBar>>> GetHistoricalBarsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            bool adjustForCorporateActions = true,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate, timeframe, adjustForCorporateActions });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate inputs
                var validationResult = ValidateHistoricalDataRequest(symbol, startDate, endDate);
                if (!validationResult.IsSuccess)
                {
                    return TradingResult<IEnumerable<PriceBar>>.Failure(validationResult.Error!);
                }

                // Check cache first
                var cacheKey = GenerateCacheKey(symbol, timeframe, adjustForCorporateActions);
                var cachedData = await GetFromCacheAsync(cacheKey, startDate, endDate, cancellationToken);
                
                if (cachedData != null && cachedData.Any())
                {
                    Interlocked.Increment(ref _cacheHits);
                    LogDebug($"Cache hit for {symbol}", new { 
                        RecordCount = cachedData.Count(),
                        CacheHitRate = GetCacheHitRate()
                    });
                    
                    LogMethodExit(new { RecordCount = cachedData.Count() }, stopwatch.Elapsed, true);
                    return TradingResult<IEnumerable<PriceBar>>.Success(cachedData);
                }

                Interlocked.Increment(ref _cacheMisses);

                // Fetch from data provider
                var fetchResult = await FetchHistoricalBarsAsync(
                    symbol, startDate, endDate, timeframe, cancellationToken);
                
                if (!fetchResult.IsSuccess)
                {
                    LogError($"Failed to fetch historical data for {symbol}",
                        operationContext: "Historical data fetch",
                        userImpact: "Backtest cannot proceed without data",
                        troubleshootingHints: "Check data provider connectivity and symbol validity");
                    
                    return fetchResult;
                }

                var bars = fetchResult.Value!.ToList();

                // Validate data quality
                var qualityResult = await _qualityValidator.ValidatePriceBarsAsync(bars, symbol, timeframe);
                if (!qualityResult.IsSuccess)
                {
                    LogWarning($"Data quality issues detected for {symbol}",
                        impact: "Backtest results may be unreliable",
                        recommendedAction: "Review data quality report and consider different date range",
                        additionalData: qualityResult.Value);
                }

                // Apply corporate actions if requested
                if (adjustForCorporateActions)
                {
                    var adjustmentResult = await ApplyCorporateActionsAsync(
                        bars, symbol, startDate, endDate, cancellationToken);
                    
                    if (adjustmentResult.IsSuccess)
                    {
                        bars = adjustmentResult.Value!.ToList();
                    }
                    else
                    {
                        LogWarning($"Failed to apply corporate actions for {symbol}",
                            impact: "Data may not be properly adjusted for splits/dividends",
                            recommendedAction: "Verify corporate action data availability");
                    }
                }

                // Update cache
                await UpdateCacheAsync(cacheKey, bars, cancellationToken);

                // Update metrics
                UpdateMetric("TotalBarsRetrieved", bars.Count);
                IncrementCounter("HistoricalDataRequests");

                LogInfo($"Retrieved {bars.Count} bars for {symbol}", new
                {
                    Symbol = symbol,
                    StartDate = startDate,
                    EndDate = endDate,
                    Timeframe = timeframe,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    CacheHitRate = GetCacheHitRate()
                });

                LogMethodExit(new { RecordCount = bars.Count }, stopwatch.Elapsed, true);
                return TradingResult<IEnumerable<PriceBar>>.Success(bars);
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving historical bars for {symbol}",
                    ex,
                    operationContext: "GetHistoricalBarsAsync",
                    userImpact: "Historical data unavailable",
                    troubleshootingHints: "Check logs for specific error details");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<IEnumerable<PriceBar>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<IAsyncEnumerable<MarketTick>>> GetHistoricalTicksAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            TickType tickType = TickType.Trade,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate, tickType });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate inputs
                var validationResult = ValidateHistoricalDataRequest(symbol, startDate, endDate);
                if (!validationResult.IsSuccess)
                {
                    return TradingResult<IAsyncEnumerable<MarketTick>>.Failure(validationResult.Error!);
                }

                // Create async enumerable for streaming tick data
                var tickStream = StreamHistoricalTicksAsync(
                    symbol, startDate, endDate, tickType, cancellationToken);

                LogInfo($"Started streaming tick data for {symbol}", new
                {
                    Symbol = symbol,
                    StartDate = startDate,
                    EndDate = endDate,
                    TickType = tickType
                });

                LogMethodExit(stopwatch.Elapsed, true);
                return TradingResult<IAsyncEnumerable<MarketTick>>.Success(tickStream);
            }
            catch (Exception ex)
            {
                LogError($"Error starting tick stream for {symbol}",
                    ex,
                    operationContext: "GetHistoricalTicksAsync",
                    userImpact: "Tick data unavailable",
                    troubleshootingHints: "Verify tick data support for symbol");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<IAsyncEnumerable<MarketTick>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<IAsyncEnumerable<AlignedMarketData>>> GetAlignedDataAsync(
            IEnumerable<string> symbols,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            DataAlignmentMethod alignmentMethod = DataAlignmentMethod.Forward,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbols = string.Join(",", symbols), startDate, endDate, timeframe, alignmentMethod });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var symbolList = symbols.ToList();
                
                // Validate inputs
                if (!symbolList.Any())
                {
                    return TradingResult<IAsyncEnumerable<AlignedMarketData>>.Failure(
                        TradingError.Validation("No symbols provided", CorrelationId));
                }

                // Fetch data for all symbols
                var dataFetchTasks = symbolList.Select(symbol =>
                    GetHistoricalBarsAsync(symbol, startDate, endDate, timeframe, true, cancellationToken)
                ).ToList();

                var results = await Task.WhenAll(dataFetchTasks);

                // Check for failures
                var failures = results.Where(r => !r.IsSuccess).ToList();
                if (failures.Any())
                {
                    var failedSymbols = string.Join(", ", 
                        symbolList.Where((s, i) => !results[i].IsSuccess));
                    
                    LogWarning($"Failed to fetch data for symbols: {failedSymbols}",
                        impact: "Incomplete data alignment",
                        recommendedAction: "Check data availability for failed symbols");
                }

                // Build symbol data dictionary
                var symbolData = new Dictionary<string, List<PriceBar>>();
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].IsSuccess)
                    {
                        symbolData[symbolList[i]] = results[i].Value!.ToList();
                    }
                }

                // Create aligned data stream
                var alignedStream = CreateAlignedDataStream(
                    symbolData, alignmentMethod, cancellationToken);

                LogInfo($"Created aligned data stream for {symbolList.Count} symbols", new
                {
                    SymbolCount = symbolList.Count,
                    SuccessfulSymbols = symbolData.Count,
                    AlignmentMethod = alignmentMethod,
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                });

                LogMethodExit(stopwatch.Elapsed, true);
                return TradingResult<IAsyncEnumerable<AlignedMarketData>>.Success(alignedStream);
            }
            catch (Exception ex)
            {
                LogError("Error creating aligned data stream",
                    ex,
                    operationContext: "GetAlignedDataAsync",
                    userImpact: "Cannot align multi-symbol data",
                    troubleshootingHints: "Check individual symbol data availability");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<IAsyncEnumerable<AlignedMarketData>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<IEnumerable<CorporateAction>>> GetCorporateActionsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate inputs
                var validationResult = ValidateHistoricalDataRequest(symbol, startDate, endDate);
                if (!validationResult.IsSuccess)
                {
                    return TradingResult<IEnumerable<CorporateAction>>.Failure(validationResult.Error!);
                }

                // Fetch corporate actions from data provider
                var actions = await _corporateActionProcessor.GetCorporateActionsAsync(
                    symbol, startDate, endDate, _primaryDataProvider, cancellationToken);

                LogInfo($"Retrieved {actions.Count()} corporate actions for {symbol}", new
                {
                    Symbol = symbol,
                    ActionCount = actions.Count(),
                    StartDate = startDate,
                    EndDate = endDate
                });

                LogMethodExit(new { ActionCount = actions.Count() }, stopwatch.Elapsed, true);
                return TradingResult<IEnumerable<CorporateAction>>.Success(actions);
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving corporate actions for {symbol}",
                    ex,
                    operationContext: "GetCorporateActionsAsync",
                    userImpact: "Corporate action data unavailable",
                    troubleshootingHints: "Check data provider support for corporate actions");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<IEnumerable<CorporateAction>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<DataQualityReport>> ValidateDataQualityAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate, timeframe });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Fetch data for validation
                var dataResult = await GetHistoricalBarsAsync(
                    symbol, startDate, endDate, timeframe, false, cancellationToken);

                if (!dataResult.IsSuccess)
                {
                    return TradingResult<DataQualityReport>.Failure(dataResult.Error!);
                }

                var bars = dataResult.Value!.ToList();
                
                // Perform comprehensive quality validation
                var report = await _qualityValidator.GenerateQualityReportAsync(
                    bars, symbol, startDate, endDate, timeframe);

                LogInfo($"Generated data quality report for {symbol}", new
                {
                    Symbol = symbol,
                    TotalRecords = report.TotalRecords,
                    DataCompleteness = report.DataCompleteness,
                    IssueCount = report.Issues.Count
                });

                LogMethodExit(report, stopwatch.Elapsed, true);
                return TradingResult<DataQualityReport>.Success(report);
            }
            catch (Exception ex)
            {
                LogError($"Error validating data quality for {symbol}",
                    ex,
                    operationContext: "ValidateDataQualityAsync",
                    userImpact: "Cannot assess data quality",
                    troubleshootingHints: "Check data availability first");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<DataQualityReport>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult> PreloadDataAsync(
            IEnumerable<string> symbols,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            IProgress<DataLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbols = string.Join(",", symbols), startDate, endDate, timeframe });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var symbolList = symbols.ToList();
                var totalSymbols = symbolList.Count;
                var completedSymbols = 0;
                var startTime = DateTime.UtcNow;

                LogInfo($"Starting data preload for {totalSymbols} symbols", new
                {
                    SymbolCount = totalSymbols,
                    StartDate = startDate,
                    EndDate = endDate,
                    Timeframe = timeframe
                });

                foreach (var symbol in symbolList)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LogWarning("Data preload cancelled by user");
                        break;
                    }

                    // Report progress
                    var progressData = new DataLoadProgress
                    {
                        TotalSymbols = totalSymbols,
                        CompletedSymbols = completedSymbols,
                        CurrentSymbol = symbol,
                        PercentComplete = (decimal)completedSymbols / totalSymbols * 100,
                        ElapsedTime = DateTime.UtcNow - startTime,
                        EstimatedTimeRemaining = EstimateTimeRemaining(
                            completedSymbols, totalSymbols, DateTime.UtcNow - startTime),
                        BytesLoaded = _totalCacheSizeBytes,
                        TotalBytes = EstimateTotalBytes(totalSymbols, timeframe, startDate, endDate)
                    };

                    progress?.Report(progressData);

                    // Load data for symbol
                    var result = await GetHistoricalBarsAsync(
                        symbol, startDate, endDate, timeframe, true, cancellationToken);

                    if (result.IsSuccess)
                    {
                        completedSymbols++;
                        LogDebug($"Preloaded data for {symbol}", new
                        {
                            Symbol = symbol,
                            RecordCount = result.Value!.Count(),
                            Progress = progressData.PercentComplete
                        });
                    }
                    else
                    {
                        LogWarning($"Failed to preload data for {symbol}",
                            impact: "Symbol will not be cached",
                            recommendedAction: "Check symbol validity and data availability");
                    }
                }

                // Final progress report
                var finalProgress = new DataLoadProgress
                {
                    TotalSymbols = totalSymbols,
                    CompletedSymbols = completedSymbols,
                    CurrentSymbol = "Complete",
                    PercentComplete = 100,
                    ElapsedTime = DateTime.UtcNow - startTime,
                    EstimatedTimeRemaining = TimeSpan.Zero,
                    BytesLoaded = _totalCacheSizeBytes,
                    TotalBytes = _totalCacheSizeBytes
                };

                progress?.Report(finalProgress);

                LogInfo($"Data preload completed", new
                {
                    TotalSymbols = totalSymbols,
                    CompletedSymbols = completedSymbols,
                    CacheSizeMB = _totalCacheSizeBytes / (1024 * 1024),
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                });

                LogMethodExit(stopwatch.Elapsed, true);
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error during data preload",
                    ex,
                    operationContext: "PreloadDataAsync",
                    userImpact: "Data preload incomplete",
                    troubleshootingHints: "Check memory availability and data provider limits");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult.Failure(TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<DateRange>> GetAvailableDateRangeAsync(
            string symbol,
            BarTimeframe timeframe,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, timeframe });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Check cache for existing data range
                var cacheKey = GenerateCacheKey(symbol, timeframe, false);
                if (_cache.TryGetValue(cacheKey, out var cachedData))
                {
                    var range = new DateRange
                    {
                        Start = cachedData.StartDate,
                        End = cachedData.EndDate,
                        TradingDays = cachedData.Data.Count
                    };

                    LogMethodExit(range, stopwatch.Elapsed, true);
                    return TradingResult<DateRange>.Success(range);
                }

                // Query data provider for available range
                // This is a simplified implementation - would need provider-specific logic
                var testEndDate = DateTime.UtcNow.Date;
                var testStartDate = testEndDate.AddYears(-20); // Check up to 20 years back

                var result = await GetHistoricalBarsAsync(
                    symbol, testStartDate, testEndDate, timeframe, false, cancellationToken);

                if (!result.IsSuccess || !result.Value!.Any())
                {
                    return TradingResult<DateRange>.Failure(
                        TradingError.Validation($"No data available for {symbol}", CorrelationId));
                }

                var bars = result.Value!.OrderBy(b => b.Timestamp).ToList();
                var dateRange = new DateRange
                {
                    Start = bars.First().Timestamp,
                    End = bars.Last().Timestamp,
                    TradingDays = bars.Count
                };

                LogInfo($"Available date range for {symbol}", new
                {
                    Symbol = symbol,
                    StartDate = dateRange.Start,
                    EndDate = dateRange.End,
                    TradingDays = dateRange.TradingDays
                });

                LogMethodExit(dateRange, stopwatch.Elapsed, true);
                return TradingResult<DateRange>.Success(dateRange);
            }
            catch (Exception ex)
            {
                LogError($"Error getting date range for {symbol}",
                    ex,
                    operationContext: "GetAvailableDateRangeAsync",
                    userImpact: "Cannot determine data availability",
                    troubleshootingHints: "Check symbol validity");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<DateRange>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult<IEnumerable<string>>> GetAvailableSymbolsAsync(
            DateTime date,
            string? exchange = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { date, exchange });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // This would typically query a symbol master database
                // For now, return symbols from cache that have data for the given date
                var availableSymbols = new List<string>();

                foreach (var kvp in _cache)
                {
                    if (kvp.Value.StartDate <= date && kvp.Value.EndDate >= date)
                    {
                        var symbol = ExtractSymbolFromCacheKey(kvp.Key);
                        if (!string.IsNullOrEmpty(symbol) && !availableSymbols.Contains(symbol))
                        {
                            availableSymbols.Add(symbol);
                        }
                    }
                }

                LogInfo($"Found {availableSymbols.Count} available symbols for {date:yyyy-MM-dd}", new
                {
                    Date = date,
                    Exchange = exchange,
                    SymbolCount = availableSymbols.Count
                });

                LogMethodExit(new { SymbolCount = availableSymbols.Count }, stopwatch.Elapsed, true);
                return TradingResult<IEnumerable<string>>.Success(availableSymbols);
            }
            catch (Exception ex)
            {
                LogError($"Error getting available symbols for {date:yyyy-MM-dd}",
                    ex,
                    operationContext: "GetAvailableSymbolsAsync",
                    userImpact: "Cannot list available symbols",
                    troubleshootingHints: "Check data provider connectivity");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<IEnumerable<string>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        public async Task<TradingResult> ClearCacheAsync(
            IEnumerable<string>? symbols = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            LogMethodEntry(new { symbols = symbols?.Count(), startDate, endDate });
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _cacheLock.WaitAsync();

                var keysToRemove = new List<string>();
                var bytesFreed = 0L;

                if (symbols == null)
                {
                    // Clear all cache
                    keysToRemove.AddRange(_cache.Keys);
                }
                else
                {
                    // Clear specific symbols
                    foreach (var symbol in symbols)
                    {
                        keysToRemove.AddRange(_cache.Keys.Where(k => k.Contains(symbol)));
                    }
                }

                // Apply date filters if specified
                if (startDate.HasValue || endDate.HasValue)
                {
                    keysToRemove = keysToRemove.Where(key =>
                    {
                        if (_cache.TryGetValue(key, out var data))
                        {
                            if (startDate.HasValue && data.EndDate < startDate.Value)
                                return false;
                            if (endDate.HasValue && data.StartDate > endDate.Value)
                                return false;
                            return true;
                        }
                        return false;
                    }).ToList();
                }

                // Remove items from cache
                foreach (var key in keysToRemove)
                {
                    if (_cache.TryRemove(key, out var removed))
                    {
                        bytesFreed += removed.SizeInBytes;
                    }
                }

                Interlocked.Add(ref _totalCacheSizeBytes, -bytesFreed);

                LogInfo("Cache cleared", new
                {
                    ItemsRemoved = keysToRemove.Count,
                    BytesFreedMB = bytesFreed / (1024.0 * 1024.0),
                    RemainingItems = _cache.Count,
                    RemainingCacheSizeMB = _totalCacheSizeBytes / (1024.0 * 1024.0)
                });

                LogMethodExit(stopwatch.Elapsed, true);
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error clearing cache",
                    ex,
                    operationContext: "ClearCacheAsync",
                    userImpact: "Cache may not be fully cleared",
                    troubleshootingHints: "Restart service if cache issues persist");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult.Failure(TradingError.System(ex, CorrelationId));
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<TradingResult<DataProviderStatistics>> GetStatisticsAsync()
        {
            LogMethodEntry();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Task.CompletedTask; // Async for future enhancements

                var stats = new DataProviderStatistics
                {
                    TotalRecords = _cache.Sum(kvp => kvp.Value.Data.Count),
                    CachedRecords = _cache.Sum(kvp => kvp.Value.Data.Count),
                    CacheSizeBytes = _totalCacheSizeBytes,
                    AvailableSymbols = _cache.Select(kvp => ExtractSymbolFromCacheKey(kvp.Key))
                        .Distinct()
                        .Count(s => !string.IsNullOrEmpty(s)),
                    CacheHitRate = GetCacheHitRate(),
                    LastUpdate = DateTime.UtcNow,
                    AverageQueryTime = GetAverageQueryTime()
                };

                // Calculate records by timeframe
                stats.RecordsByTimeframe = _cache
                    .GroupBy(kvp => ExtractTimeframeFromCacheKey(kvp.Key))
                    .Where(g => g.Key != null)
                    .ToDictionary(
                        g => g.Key!.ToString(),
                        g => (long)g.Sum(kvp => kvp.Value.Data.Count)
                    );

                // Calculate records by symbol
                stats.RecordsBySymbol = _cache
                    .GroupBy(kvp => ExtractSymbolFromCacheKey(kvp.Key))
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(
                        g => g.Key!,
                        g => (long)g.Sum(kvp => kvp.Value.Data.Count)
                    );

                // Calculate available date range
                if (_cache.Any())
                {
                    var minDate = _cache.Min(kvp => kvp.Value.StartDate);
                    var maxDate = _cache.Max(kvp => kvp.Value.EndDate);
                    stats.AvailableDateRange = new DateRange
                    {
                        Start = minDate,
                        End = maxDate,
                        TradingDays = _cache.Sum(kvp => kvp.Value.Data.Count)
                    };
                }

                LogInfo("Generated data provider statistics", new
                {
                    TotalRecords = stats.TotalRecords,
                    CacheSizeMB = stats.CacheSizeBytes / (1024.0 * 1024.0),
                    AvailableSymbols = stats.AvailableSymbols,
                    CacheHitRate = stats.CacheHitRate
                });

                LogMethodExit(stats, stopwatch.Elapsed, true);
                return TradingResult<DataProviderStatistics>.Success(stats);
            }
            catch (Exception ex)
            {
                LogError("Error generating statistics",
                    ex,
                    operationContext: "GetStatisticsAsync",
                    userImpact: "Statistics unavailable",
                    troubleshootingHints: "Check cache integrity");

                LogMethodExit(ex, stopwatch.Elapsed, false);
                return TradingResult<DataProviderStatistics>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<IEnumerable<PriceBar>?> GetFromCacheAsync(
            string cacheKey,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            if (!_cache.TryGetValue(cacheKey, out var cachedData))
                return null;

            // Check if cached data covers the requested range
            if (cachedData.StartDate > startDate || cachedData.EndDate < endDate)
                return null;

            // Filter to requested date range
            var filteredData = cachedData.Data
                .Where(bar => bar.Timestamp >= startDate && bar.Timestamp <= endDate)
                .ToList();

            // Update last access time
            cachedData.LastAccessTime = DateTime.UtcNow;
            cachedData.AccessCount++;

            return filteredData;
        }

        private async Task UpdateCacheAsync(
            string cacheKey,
            List<PriceBar> bars,
            CancellationToken cancellationToken)
        {
            if (!bars.Any())
                return;

            try
            {
                await _cacheLock.WaitAsync(cancellationToken);

                // Check cache size limit
                if (_totalCacheSizeBytes > _configuration.MaxCacheSizeMB * 1024 * 1024)
                {
                    await EvictLeastRecentlyUsedAsync();
                }

                // Estimate size of data
                var estimatedSize = EstimateDataSize(bars);

                var cachedData = new CachedDataSet
                {
                    Data = bars,
                    StartDate = bars.Min(b => b.Timestamp),
                    EndDate = bars.Max(b => b.Timestamp),
                    CreatedTime = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 1,
                    SizeInBytes = estimatedSize,
                    IsCompressed = false // Would implement compression if enabled
                };

                _cache.AddOrUpdate(cacheKey, cachedData, (k, v) => cachedData);
                Interlocked.Add(ref _totalCacheSizeBytes, estimatedSize);

                LogDebug($"Updated cache for key: {cacheKey}", new
                {
                    CacheKey = cacheKey,
                    RecordCount = bars.Count,
                    SizeKB = estimatedSize / 1024.0,
                    TotalCacheSizeMB = _totalCacheSizeBytes / (1024.0 * 1024.0)
                });
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task EvictLeastRecentlyUsedAsync()
        {
            var targetSize = (long)(_configuration.MaxCacheSizeMB * 1024 * 1024 * 0.8); // Free 20%
            var itemsToRemove = new List<string>();

            // Sort by last access time
            var sortedItems = _cache
                .OrderBy(kvp => kvp.Value.LastAccessTime)
                .ThenBy(kvp => kvp.Value.AccessCount)
                .ToList();

            var currentSize = _totalCacheSizeBytes;
            
            foreach (var item in sortedItems)
            {
                if (currentSize <= targetSize)
                    break;

                itemsToRemove.Add(item.Key);
                currentSize -= item.Value.SizeInBytes;
            }

            // Remove items
            foreach (var key in itemsToRemove)
            {
                if (_cache.TryRemove(key, out var removed))
                {
                    Interlocked.Add(ref _totalCacheSizeBytes, -removed.SizeInBytes);
                }
            }

            LogDebug($"Evicted {itemsToRemove.Count} items from cache", new
            {
                ItemsEvicted = itemsToRemove.Count,
                NewCacheSizeMB = _totalCacheSizeBytes / (1024.0 * 1024.0)
            });
        }

        private async Task<TradingResult<IEnumerable<PriceBar>>> FetchHistoricalBarsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            CancellationToken cancellationToken)
        {
            try
            {
                // Convert our timeframe to provider-specific format
                var providerTimeframe = ConvertTimeframe(timeframe);
                
                // Try primary provider first
                var primaryResult = await FetchFromProviderAsync(
                    _primaryDataProvider, symbol, startDate, endDate, providerTimeframe, cancellationToken);

                if (primaryResult.IsSuccess && primaryResult.Value!.Any())
                {
                    return TradingResult<IEnumerable<PriceBar>>.Success(
                        ConvertMarketDataToPriceBars(primaryResult.Value, symbol, timeframe));
                }

                // Try secondary provider if available
                if (_secondaryDataProvider != null)
                {
                    LogDebug($"Primary provider failed for {symbol}, trying secondary provider");
                    
                    var secondaryResult = await FetchFromProviderAsync(
                        _secondaryDataProvider, symbol, startDate, endDate, providerTimeframe, cancellationToken);

                    if (secondaryResult.IsSuccess && secondaryResult.Value!.Any())
                    {
                        return TradingResult<IEnumerable<PriceBar>>.Success(
                            ConvertMarketDataToPriceBars(secondaryResult.Value, symbol, timeframe));
                    }
                }

                return TradingResult<IEnumerable<PriceBar>>.Failure(
                    TradingError.Validation($"No data available for {symbol} in specified date range", CorrelationId));
            }
            catch (Exception ex)
            {
                LogError($"Error fetching historical bars for {symbol}", ex);
                return TradingResult<IEnumerable<PriceBar>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        private async Task<TradingResult<IEnumerable<MarketData>>> FetchFromProviderAsync(
            IMarketDataProvider provider,
            string symbol,
            DateTime startDate,
            DateTime endDate,
            string timeframe,
            CancellationToken cancellationToken)
        {
            try
            {
                // This is a simplified implementation
                // Real implementation would use provider-specific methods
                var marketData = new List<MarketData>();
                
                // Most providers have daily limits, so we might need to chunk requests
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    var chunkEnd = currentDate.AddDays(30).Date < endDate ? currentDate.AddDays(30).Date : endDate;
                    
                    // Simulated provider call - replace with actual provider implementation
                    await Task.Delay(100, cancellationToken); // Rate limiting simulation
                    
                    currentDate = chunkEnd.AddDays(1);
                }

                return TradingResult<IEnumerable<MarketData>>.Success(marketData);
            }
            catch (Exception ex)
            {
                LogError($"Provider {provider.GetType().Name} failed for {symbol}", ex);
                return TradingResult<IEnumerable<MarketData>>.Failure(
                    TradingError.ExternalServiceError($"Provider error: {ex.Message}", CorrelationId));
            }
        }

        private IEnumerable<PriceBar> ConvertMarketDataToPriceBars(
            IEnumerable<MarketData> marketData,
            string symbol,
            BarTimeframe timeframe)
        {
            return marketData.Select(md => new PriceBar
            {
                Symbol = symbol,
                Timestamp = md.Timestamp,
                Open = md.Open,
                High = md.High,
                Low = md.Low,
                Close = md.Close,
                Volume = md.Volume,
                Timeframe = timeframe,
                IsAdjusted = false
            });
        }

        private async IAsyncEnumerable<MarketTick> StreamHistoricalTicksAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            TickType tickType,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var currentDate = startDate.Date;
            
            while (currentDate <= endDate.Date && !cancellationToken.IsCancellationRequested)
            {
                // Fetch tick data for current day
                // This is a simplified implementation
                var ticks = new List<MarketTick>();
                
                // Simulate fetching tick data
                for (int i = 0; i < 1000; i++) // Sample ticks
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;

                    yield return new MarketTick
                    {
                        Symbol = symbol,
                        Timestamp = currentDate.AddHours(9.5).AddSeconds(i * 23.4), // Market hours simulation
                        Price = 100m + (decimal)(i % 10) * 0.01m,
                        Size = 100 * (i % 10 + 1),
                        Type = tickType
                    };
                }

                currentDate = currentDate.AddDays(1);
                
                // Skip weekends
                if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                    currentDate = currentDate.AddDays(2);
                else if (currentDate.DayOfWeek == DayOfWeek.Sunday)
                    currentDate = currentDate.AddDays(1);
            }
        }

        private async IAsyncEnumerable<AlignedMarketData> CreateAlignedDataStream(
            Dictionary<string, List<PriceBar>> symbolData,
            DataAlignmentMethod alignmentMethod,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Get all unique timestamps
            var allTimestamps = symbolData
                .SelectMany(kvp => kvp.Value.Select(bar => bar.Timestamp))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Create index for each symbol for efficient lookup
            var symbolIndices = symbolData.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDictionary(bar => bar.Timestamp)
            );

            foreach (var timestamp in allTimestamps)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                var alignedData = new AlignedMarketData
                {
                    Timestamp = timestamp,
                    SymbolData = new Dictionary<string, PriceBar>(),
                    AlignmentInfo = new DataAlignmentInfo()
                };

                foreach (var symbol in symbolData.Keys)
                {
                    if (symbolIndices[symbol].TryGetValue(timestamp, out var bar))
                    {
                        alignedData.SymbolData[symbol] = bar;
                    }
                    else
                    {
                        // Apply alignment method
                        var alignedBar = ApplyAlignment(
                            symbolIndices[symbol], timestamp, alignmentMethod);
                        
                        if (alignedBar != null)
                        {
                            alignedData.SymbolData[symbol] = alignedBar;
                            
                            if (alignmentMethod == DataAlignmentMethod.Forward)
                                alignedData.AlignmentInfo.ForwardFilledSymbols.Add(symbol);
                            else if (alignmentMethod == DataAlignmentMethod.Backward)
                                alignedData.AlignmentInfo.BackFilledSymbols.Add(symbol);
                        }
                        else
                        {
                            alignedData.AlignmentInfo.MissingSymbols++;
                        }
                    }
                }

                yield return alignedData;
            }
        }

        private PriceBar? ApplyAlignment(
            Dictionary<DateTime, PriceBar> symbolIndex,
            DateTime targetTimestamp,
            DataAlignmentMethod method)
        {
            switch (method)
            {
                case DataAlignmentMethod.None:
                    return null;

                case DataAlignmentMethod.Forward:
                    var previousBar = symbolIndex
                        .Where(kvp => kvp.Key < targetTimestamp)
                        .OrderByDescending(kvp => kvp.Key)
                        .FirstOrDefault();
                    return previousBar.Value;

                case DataAlignmentMethod.Backward:
                    var nextBar = symbolIndex
                        .Where(kvp => kvp.Key > targetTimestamp)
                        .OrderBy(kvp => kvp.Key)
                        .FirstOrDefault();
                    return nextBar.Value;

                case DataAlignmentMethod.Previous:
                    var prevBar = symbolIndex
                        .Where(kvp => kvp.Key <= targetTimestamp)
                        .OrderByDescending(kvp => kvp.Key)
                        .FirstOrDefault();
                    return prevBar.Value;

                case DataAlignmentMethod.Linear:
                    // Implement linear interpolation
                    var before = symbolIndex
                        .Where(kvp => kvp.Key < targetTimestamp)
                        .OrderByDescending(kvp => kvp.Key)
                        .FirstOrDefault();
                    var after = symbolIndex
                        .Where(kvp => kvp.Key > targetTimestamp)
                        .OrderBy(kvp => kvp.Key)
                        .FirstOrDefault();

                    if (before.Value != null && after.Value != null)
                    {
                        // Simple linear interpolation
                        var ratio = (targetTimestamp - before.Key).TotalSeconds /
                                   (after.Key - before.Key).TotalSeconds;
                        
                        return new PriceBar
                        {
                            Symbol = before.Value.Symbol,
                            Timestamp = targetTimestamp,
                            Open = InterpolatePrice(before.Value.Open, after.Value.Open, ratio),
                            High = InterpolatePrice(before.Value.High, after.Value.High, ratio),
                            Low = InterpolatePrice(before.Value.Low, after.Value.Low, ratio),
                            Close = InterpolatePrice(before.Value.Close, after.Value.Close, ratio),
                            Volume = (long)(before.Value.Volume + (after.Value.Volume - before.Value.Volume) * ratio),
                            Timeframe = before.Value.Timeframe
                        };
                    }
                    return null;

                default:
                    return null;
            }
        }

        private decimal InterpolatePrice(decimal start, decimal end, double ratio)
        {
            return start + (decimal)((double)(end - start) * ratio);
        }

        private async Task<TradingResult<IEnumerable<PriceBar>>> ApplyCorporateActionsAsync(
            List<PriceBar> bars,
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var actions = await _corporateActionProcessor.GetCorporateActionsAsync(
                    symbol, startDate, endDate, _primaryDataProvider, cancellationToken);

                if (!actions.Any())
                    return TradingResult<IEnumerable<PriceBar>>.Success(bars);

                // Apply actions in reverse chronological order
                foreach (var action in actions.OrderByDescending(a => a.ExDate))
                {
                    bars = _corporateActionProcessor.ApplyAction(bars, action);
                }

                // Mark bars as adjusted
                foreach (var bar in bars)
                {
                    bar.IsAdjusted = true;
                    bar.AdjustedClose = bar.Close;
                }

                return TradingResult<IEnumerable<PriceBar>>.Success(bars);
            }
            catch (Exception ex)
            {
                LogError($"Error applying corporate actions for {symbol}", ex);
                return TradingResult<IEnumerable<PriceBar>>.Failure(
                    TradingError.System(ex, CorrelationId));
            }
        }

        private TradingResult ValidateHistoricalDataRequest(
            string symbol,
            DateTime startDate,
            DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return TradingResult.Failure(
                    TradingError.Validation("Symbol cannot be empty", CorrelationId));
            }

            if (startDate >= endDate)
            {
                return TradingResult.Failure(
                    TradingError.Validation("Start date must be before end date", CorrelationId));
            }

            if (endDate > DateTime.UtcNow)
            {
                return TradingResult.Failure(
                    TradingError.Validation("End date cannot be in the future", CorrelationId));
            }

            return TradingResult.Success();
        }

        private string GenerateCacheKey(string symbol, BarTimeframe timeframe, bool adjusted)
        {
            return $"{symbol}_{timeframe}_{(adjusted ? "ADJ" : "RAW")}";
        }

        private string ExtractSymbolFromCacheKey(string cacheKey)
        {
            var parts = cacheKey.Split('_');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        private BarTimeframe? ExtractTimeframeFromCacheKey(string cacheKey)
        {
            var parts = cacheKey.Split('_');
            if (parts.Length > 1 && Enum.TryParse<BarTimeframe>(parts[1], out var timeframe))
            {
                return timeframe;
            }
            return null;
        }

        private string ConvertTimeframe(BarTimeframe timeframe)
        {
            // Convert to provider-specific format
            return timeframe switch
            {
                BarTimeframe.OneMinute => "1min",
                BarTimeframe.FiveMinutes => "5min",
                BarTimeframe.FifteenMinutes => "15min",
                BarTimeframe.ThirtyMinutes => "30min",
                BarTimeframe.OneHour => "60min",
                BarTimeframe.Daily => "daily",
                BarTimeframe.Weekly => "weekly",
                BarTimeframe.Monthly => "monthly",
                _ => "1min"
            };
        }

        private long EstimateDataSize(List<PriceBar> bars)
        {
            // Rough estimate: 100 bytes per bar
            return bars.Count * 100;
        }

        private TimeSpan EstimateTimeRemaining(int completed, int total, TimeSpan elapsed)
        {
            if (completed == 0) return TimeSpan.Zero;
            
            var averageTimePerItem = elapsed.TotalSeconds / completed;
            var remainingItems = total - completed;
            return TimeSpan.FromSeconds(averageTimePerItem * remainingItems);
        }

        private long EstimateTotalBytes(int symbolCount, BarTimeframe timeframe, DateTime start, DateTime end)
        {
            var days = (end - start).Days;
            var barsPerDay = timeframe switch
            {
                BarTimeframe.OneMinute => 390,
                BarTimeframe.FiveMinutes => 78,
                BarTimeframe.FifteenMinutes => 26,
                BarTimeframe.ThirtyMinutes => 13,
                BarTimeframe.OneHour => 7,
                BarTimeframe.Daily => 1,
                _ => 390
            };
            
            return symbolCount * days * barsPerDay * 100; // 100 bytes per bar estimate
        }

        private decimal GetCacheHitRate()
        {
            var total = _cacheHits + _cacheMisses;
            return total > 0 ? (decimal)_cacheHits / total : 0;
        }

        private TimeSpan GetAverageQueryTime()
        {
            // This would track actual query times in production
            return TimeSpan.FromMilliseconds(50);
        }

        #endregion

        #region Lifecycle Management

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing Historical Data Manager", new
            {
                MaxCacheSizeMB = _configuration.MaxCacheSizeMB,
                EnableCompression = _configuration.EnableCompression,
                EnableTimescaleDB = _configuration.EnableTimescaleDB
            });

            // Initialize time series index
            await _timeSeriesIndex.InitializeAsync(cancellationToken);

            // Warm up cache if configured
            if (_configuration.WarmupSymbols?.Any() == true)
            {
                LogInfo($"Warming up cache for {_configuration.WarmupSymbols.Count} symbols");
                
                var warmupEndDate = DateTime.UtcNow.Date;
                var warmupStartDate = warmupEndDate.AddDays(-_configuration.WarmupDays);
                
                await PreloadDataAsync(
                    _configuration.WarmupSymbols,
                    warmupStartDate,
                    warmupEndDate,
                    BarTimeframe.Daily,
                    null,
                    cancellationToken);
            }

            UpdateMetric("InitializationTimeMs", _startupTime.ElapsedMilliseconds);
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting Historical Data Manager");
            
            // Start background cache maintenance
            _ = Task.Run(async () => await CacheMaintenanceLoopAsync(cancellationToken), cancellationToken);
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping Historical Data Manager", new
            {
                TotalRequests = _cacheHits + _cacheMisses,
                CacheHitRate = GetCacheHitRate(),
                CacheSizeMB = _totalCacheSizeBytes / (1024.0 * 1024.0)
            });

            // Persist cache if configured
            if (_configuration.PersistCacheOnShutdown)
            {
                await PersistCacheAsync(cancellationToken);
            }
        }

        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
            OnCheckHealthAsync(CancellationToken cancellationToken)
        {
            var details = new Dictionary<string, object>
            {
                ["CacheItemCount"] = _cache.Count,
                ["CacheSizeMB"] = _totalCacheSizeBytes / (1024.0 * 1024.0),
                ["CacheHitRate"] = GetCacheHitRate(),
                ["UptimeHours"] = _startupTime.Elapsed.TotalHours
            };

            var isHealthy = _totalCacheSizeBytes < _configuration.MaxCacheSizeMB * 1024 * 1024 * 1.2;
            var message = isHealthy ? "Historical data manager is healthy" : "Cache size exceeds limits";

            return (isHealthy, message, details);
        }

        private async Task CacheMaintenanceLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    
                    // Clean up expired cache entries
                    var expiredKeys = _cache
                        .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessTime > _configuration.CacheExpiration)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredKeys)
                    {
                        if (_cache.TryRemove(key, out var removed))
                        {
                            Interlocked.Add(ref _totalCacheSizeBytes, -removed.SizeInBytes);
                        }
                    }

                    if (expiredKeys.Any())
                    {
                        LogDebug($"Removed {expiredKeys.Count} expired cache entries");
                    }
                }
                catch (Exception ex)
                {
                    LogError("Error in cache maintenance loop", ex);
                }
            }
        }

        private async Task PersistCacheAsync(CancellationToken cancellationToken)
        {
            // Implementation would serialize cache to disk for faster startup
            LogInfo("Persisting cache to disk", new { ItemCount = _cache.Count });
        }

        #endregion

        #region Supporting Classes

        private class CachedDataSet
        {
            public List<PriceBar> Data { get; set; } = new();
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
            public long SizeInBytes { get; set; }
            public bool IsCompressed { get; set; }
        }

        private class DataQualityValidator
        {
            private readonly ITradingLogger _logger;

            public DataQualityValidator(ITradingLogger logger)
            {
                _logger = logger;
            }

            public async Task<TradingResult<DataQualityReport>> ValidatePriceBarsAsync(
                List<PriceBar> bars,
                string symbol,
                BarTimeframe timeframe)
            {
                var report = new DataQualityReport
                {
                    Symbol = symbol,
                    TotalRecords = bars.Count,
                    StartDate = bars.FirstOrDefault()?.Timestamp ?? DateTime.MinValue,
                    EndDate = bars.LastOrDefault()?.Timestamp ?? DateTime.MaxValue
                };

                // Check for missing data
                var expectedBars = CalculateExpectedBars(report.StartDate, report.EndDate, timeframe);
                report.MissingRecords = Math.Max(0, expectedBars - bars.Count);

                // Check for duplicates
                var duplicates = bars
                    .GroupBy(b => b.Timestamp)
                    .Where(g => g.Count() > 1)
                    .ToList();
                report.DuplicateRecords = duplicates.Sum(g => g.Count() - 1);

                // Check for outliers
                foreach (var bar in bars)
                {
                    if (bar.High < bar.Low)
                    {
                        report.Issues.Add(new DataQualityIssue
                        {
                            Timestamp = bar.Timestamp,
                            Type = DataQualityIssueType.InconsistentData,
                            Description = "High price is less than low price",
                            AffectedData = bar
                        });
                        report.OutlierRecords++;
                    }

                    if (bar.Open > bar.High || bar.Open < bar.Low ||
                        bar.Close > bar.High || bar.Close < bar.Low)
                    {
                        report.Issues.Add(new DataQualityIssue
                        {
                            Timestamp = bar.Timestamp,
                            Type = DataQualityIssueType.InconsistentData,
                            Description = "OHLC values are inconsistent",
                            AffectedData = bar
                        });
                        report.OutlierRecords++;
                    }

                    if (bar.Close <= 0 || bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0)
                    {
                        report.Issues.Add(new DataQualityIssue
                        {
                            Timestamp = bar.Timestamp,
                            Type = DataQualityIssueType.NegativePrice,
                            Description = "Invalid price value detected",
                            AffectedData = bar
                        });
                        report.OutlierRecords++;
                    }
                }

                report.DataCompleteness = bars.Count > 0 
                    ? (decimal)(bars.Count - report.MissingRecords - report.DuplicateRecords) / expectedBars 
                    : 0;

                return TradingResult<DataQualityReport>.Success(report);
            }

            public async Task<DataQualityReport> GenerateQualityReportAsync(
                List<PriceBar> bars,
                string symbol,
                DateTime startDate,
                DateTime endDate,
                BarTimeframe timeframe)
            {
                var validationResult = await ValidatePriceBarsAsync(bars, symbol, timeframe);
                var report = validationResult.Value!;

                // Add statistics
                if (bars.Any())
                {
                    var prices = bars.Select(b => b.Close).ToList();
                    report.Statistics["AvgPrice"] = prices.Average();
                    report.Statistics["MinPrice"] = prices.Min();
                    report.Statistics["MaxPrice"] = prices.Max();
                    report.Statistics["StdDev"] = CalculateStandardDeviation(prices);
                    report.Statistics["AvgVolume"] = bars.Average(b => b.Volume);
                }

                return report;
            }

            private int CalculateExpectedBars(DateTime start, DateTime end, BarTimeframe timeframe)
            {
                var tradingDays = GetTradingDays(start, end);
                
                return timeframe switch
                {
                    BarTimeframe.Daily => tradingDays,
                    BarTimeframe.OneHour => tradingDays * 7,
                    BarTimeframe.ThirtyMinutes => tradingDays * 13,
                    BarTimeframe.FifteenMinutes => tradingDays * 26,
                    BarTimeframe.FiveMinutes => tradingDays * 78,
                    BarTimeframe.OneMinute => tradingDays * 390,
                    _ => tradingDays * 390
                };
            }

            private int GetTradingDays(DateTime start, DateTime end)
            {
                var days = 0;
                var current = start.Date;
                
                while (current <= end.Date)
                {
                    if (current.DayOfWeek != DayOfWeek.Saturday && 
                        current.DayOfWeek != DayOfWeek.Sunday)
                    {
                        days++;
                    }
                    current = current.AddDays(1);
                }
                
                return days;
            }

            private double CalculateStandardDeviation(List<decimal> values)
            {
                if (values.Count < 2) return 0;
                
                var avg = values.Average();
                var sum = values.Sum(d => Math.Pow((double)(d - avg), 2));
                return Math.Sqrt(sum / (values.Count - 1));
            }
        }

        private class CorporateActionProcessor
        {
            private readonly ITradingLogger _logger;

            public CorporateActionProcessor(ITradingLogger logger)
            {
                _logger = logger;
            }

            public async Task<IEnumerable<CorporateAction>> GetCorporateActionsAsync(
                string symbol,
                DateTime startDate,
                DateTime endDate,
                IMarketDataProvider provider,
                CancellationToken cancellationToken)
            {
                // This would fetch from provider or database
                // Simplified implementation
                return new List<CorporateAction>();
            }

            public List<PriceBar> ApplyAction(List<PriceBar> bars, CorporateAction action)
            {
                switch (action.Type)
                {
                    case CorporateActionType.StockSplit:
                        return ApplySplit(bars, action);
                    
                    case CorporateActionType.Dividend:
                    case CorporateActionType.SpecialDividend:
                        return ApplyDividend(bars, action);
                    
                    default:
                        return bars;
                }
            }

            private List<PriceBar> ApplySplit(List<PriceBar> bars, CorporateAction split)
            {
                var adjustedBars = new List<PriceBar>();
                
                foreach (var bar in bars)
                {
                    if (bar.Timestamp < split.ExDate)
                    {
                        // Adjust historical prices
                        var adjusted = new PriceBar
                        {
                            Symbol = bar.Symbol,
                            Timestamp = bar.Timestamp,
                            Open = bar.Open / split.Factor,
                            High = bar.High / split.Factor,
                            Low = bar.Low / split.Factor,
                            Close = bar.Close / split.Factor,
                            Volume = (long)(bar.Volume * (double)split.Factor),
                            Timeframe = bar.Timeframe,
                            IsAdjusted = true,
                            AdjustedClose = bar.Close / split.Factor
                        };
                        adjustedBars.Add(adjusted);
                    }
                    else
                    {
                        adjustedBars.Add(bar);
                    }
                }
                
                return adjustedBars;
            }

            private List<PriceBar> ApplyDividend(List<PriceBar> bars, CorporateAction dividend)
            {
                if (!dividend.Amount.HasValue) return bars;
                
                var adjustedBars = new List<PriceBar>();
                
                foreach (var bar in bars)
                {
                    if (bar.Timestamp < dividend.ExDate)
                    {
                        // Adjust for dividend
                        var factor = 1 - (dividend.Amount.Value / bar.Close);
                        var adjusted = new PriceBar
                        {
                            Symbol = bar.Symbol,
                            Timestamp = bar.Timestamp,
                            Open = bar.Open * factor,
                            High = bar.High * factor,
                            Low = bar.Low * factor,
                            Close = bar.Close * factor,
                            Volume = bar.Volume,
                            Timeframe = bar.Timeframe,
                            IsAdjusted = true,
                            AdjustedClose = bar.Close * factor
                        };
                        adjustedBars.Add(adjusted);
                    }
                    else
                    {
                        adjustedBars.Add(bar);
                    }
                }
                
                return adjustedBars;
            }
        }

        private class TimeSeriesIndex
        {
            private readonly ConcurrentDictionary<string, SortedDictionary<DateTime, long>> _index;

            public TimeSeriesIndex()
            {
                _index = new ConcurrentDictionary<string, SortedDictionary<DateTime, long>>();
            }

            public async Task InitializeAsync(CancellationToken cancellationToken)
            {
                // Initialize any persistent storage connections
                await Task.CompletedTask;
            }

            public void AddEntry(string symbol, DateTime timestamp, long offset)
            {
                var symbolIndex = _index.GetOrAdd(symbol, _ => new SortedDictionary<DateTime, long>());
                symbolIndex[timestamp] = offset;
            }

            public long? GetOffset(string symbol, DateTime timestamp)
            {
                if (_index.TryGetValue(symbol, out var symbolIndex) &&
                    symbolIndex.TryGetValue(timestamp, out var offset))
                {
                    return offset;
                }
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration for historical data manager
    /// </summary>
    public class HistoricalDataConfiguration
    {
        public int MaxCacheSizeMB { get; set; } = 4096; // 4GB default
        public bool EnableCompression { get; set; } = true;
        public bool EnableTimescaleDB { get; set; } = false;
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(24);
        public bool PersistCacheOnShutdown { get; set; } = true;
        public List<string>? WarmupSymbols { get; set; }
        public int WarmupDays { get; set; } = 30;
        public string? TimescaleDBConnectionString { get; set; }
        public string? CachePersistencePath { get; set; }
    }
}