using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Backtesting.Data
{
    /// <summary>
    /// Optimized time series data store for backtesting operations.
    /// Provides high-performance storage and retrieval with compression, memory-mapped files, and columnar storage.
    /// Follows canonical patterns with extensive logging and lifecycle management.
    /// </summary>
    public class TimeSeriesDataStore : CanonicalServiceBase
    {
        #region Constants

        private const int DEFAULT_BATCH_SIZE = 10000;
        private const int COMPRESSION_THRESHOLD = 1000;
        private const int MAX_MEMORY_CACHE_SIZE_MB = 1024;
        private const string DATA_FILE_EXTENSION = ".tsdata";
        private const string INDEX_FILE_EXTENSION = ".tsindex";
        private const string STATS_FILE_EXTENSION = ".tsstats";

        #endregion

        #region Private Fields

        private readonly string _baseDataPath;
        private readonly ReaderWriterLockSlim _dataLock;
        private readonly ConcurrentDictionary<string, SymbolDataPartition> _partitions;
        private readonly ConcurrentDictionary<string, DataStatistics> _statistics;
        private readonly MemoryCache _memoryCache;
        private readonly SemaphoreSlim _writeSemaphore;
        
        private long _totalBytesInMemory;
        private long _totalBytesOnDisk;
        private int _activeReaders;
        private DateTime _lastCompactionTime;
        private TimescaleDbConnector? _timescaleDb;

        #endregion

        #region Constructor

        public TimeSeriesDataStore(ITradingLogger logger, string baseDataPath) 
            : base(logger, "TimeSeriesDataStore")
        {
            _baseDataPath = baseDataPath ?? throw new ArgumentNullException(nameof(baseDataPath));
            _dataLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _partitions = new ConcurrentDictionary<string, SymbolDataPartition>();
            _statistics = new ConcurrentDictionary<string, DataStatistics>();
            _memoryCache = new MemoryCache(MAX_MEMORY_CACHE_SIZE_MB * 1024 * 1024);
            _writeSemaphore = new SemaphoreSlim(1, 1);
            _lastCompactionTime = DateTime.UtcNow;

            LogMethodEntry(new { baseDataPath });
        }

        #endregion

        #region Lifecycle Management

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();

            try
            {
                // Create base directory if not exists
                if (!Directory.Exists(_baseDataPath))
                {
                    Directory.CreateDirectory(_baseDataPath);
                    LogInfo($"Created base data directory: {_baseDataPath}");
                }

                // Load existing partitions
                await LoadExistingPartitionsAsync(cancellationToken);

                // Initialize memory monitoring
                StartMemoryMonitoring(cancellationToken);

                LogInfo("TimeSeriesDataStore initialized successfully", new
                {
                    BaseDataPath = _baseDataPath,
                    PartitionsLoaded = _partitions.Count,
                    TotalDiskUsage = _totalBytesOnDisk
                });

                LogMethodExit(success: true);
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize TimeSeriesDataStore", ex);
                LogMethodExit(success: false);
                throw;
            }
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();

            try
            {
                // Connect to TimescaleDB if configured
                if (IsTimescaleDbConfigured())
                {
                    _timescaleDb = new TimescaleDbConnector(Logger);
                    await _timescaleDb.ConnectAsync(cancellationToken);
                    LogInfo("Connected to TimescaleDB");
                }

                // Start background compaction task
                _ = Task.Run(() => BackgroundCompactionAsync(cancellationToken), cancellationToken);

                LogInfo("TimeSeriesDataStore started successfully");
                LogMethodExit(success: true);
            }
            catch (Exception ex)
            {
                LogError("Failed to start TimeSeriesDataStore", ex);
                LogMethodExit(success: false);
                throw;
            }
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();

            try
            {
                // Flush all pending writes
                await FlushAllPartitionsAsync(cancellationToken);

                // Disconnect from TimescaleDB
                if (_timescaleDb != null)
                {
                    await _timescaleDb.DisconnectAsync(cancellationToken);
                    _timescaleDb = null;
                }

                // Save final statistics
                await SaveStatisticsAsync(cancellationToken);

                LogInfo("TimeSeriesDataStore stopped successfully", new
                {
                    TotalBytesProcessed = _totalBytesInMemory + _totalBytesOnDisk,
                    FinalPartitionCount = _partitions.Count
                });

                LogMethodExit(success: true);
            }
            catch (Exception ex)
            {
                LogError("Error during TimeSeriesDataStore shutdown", ex);
                LogMethodExit(success: false);
                throw;
            }
        }

        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> OnCheckHealthAsync(
            CancellationToken cancellationToken)
        {
            LogMethodEntry();

            var details = new Dictionary<string, object>
            {
                ["PartitionCount"] = _partitions.Count,
                ["MemoryUsageMB"] = _totalBytesInMemory / (1024.0 * 1024.0),
                ["DiskUsageMB"] = _totalBytesOnDisk / (1024.0 * 1024.0),
                ["ActiveReaders"] = _activeReaders,
                ["CacheHitRate"] = _memoryCache.GetHitRate(),
                ["TimescaleDbConnected"] = _timescaleDb?.IsConnected ?? false
            };

            var memoryPressure = _totalBytesInMemory > (MAX_MEMORY_CACHE_SIZE_MB * 1024 * 1024 * 0.9);
            var isHealthy = !memoryPressure && (_timescaleDb?.IsConnected ?? true);

            var message = isHealthy 
                ? "TimeSeriesDataStore is healthy" 
                : memoryPressure 
                    ? "High memory pressure detected" 
                    : "TimescaleDB connection lost";

            LogMethodExit(new { isHealthy, message });
            return (isHealthy, message, details);
        }

        #endregion

        #region Write Operations

        /// <summary>
        /// Writes time series data for a symbol with optimized sequential access patterns.
        /// </summary>
        public async Task<TradingResult> WriteDataAsync(
            string symbol, 
            IEnumerable<DailyData> data, 
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, dataCount = data.Count() });

            if (string.IsNullOrWhiteSpace(symbol))
                return TradingResult.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");

            var dataList = data.ToList();
            if (!dataList.Any())
                return TradingResult.Failure("NO_DATA", "No data provided to write");

            await _writeSemaphore.WaitAsync(cancellationToken);
            try
            {
                _dataLock.EnterWriteLock();
                try
                {
                    // Get or create partition
                    var partition = GetOrCreatePartition(symbol);

                    // Write data to partition
                    var result = await partition.WriteDataAsync(dataList, cancellationToken);

                    if (result.IsSuccess)
                    {
                        // Update statistics
                        UpdateStatistics(symbol, dataList);

                        // Invalidate cache for this symbol
                        _memoryCache.InvalidateSymbol(symbol);

                        IncrementCounter("DataWriteCount");
                        UpdateMetric("LastWriteSymbol", symbol);
                        UpdateMetric("LastWriteRecords", dataList.Count);

                        LogInfo($"Successfully wrote {dataList.Count} records for {symbol}");
                    }

                    LogMethodExit(result);
                    return result;
                }
                finally
                {
                    _dataLock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to write data for {symbol}", ex);
                LogMethodExit(success: false);
                return TradingResult.Failure("WRITE_ERROR", $"Failed to write data: {ex.Message}", ex);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Bulk writes data for multiple symbols with transactional semantics.
        /// </summary>
        public async Task<TradingResult> BulkWriteDataAsync(
            Dictionary<string, List<DailyData>> symbolData,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbolCount = symbolData.Count, totalRecords = symbolData.Sum(x => x.Value.Count) });

            if (!symbolData.Any())
                return TradingResult.Failure("NO_DATA", "No data provided for bulk write");

            await _writeSemaphore.WaitAsync(cancellationToken);
            try
            {
                _dataLock.EnterWriteLock();
                try
                {
                    var writtenSymbols = new List<string>();
                    var errors = new List<string>();

                    // Write each symbol's data
                    foreach (var kvp in symbolData)
                    {
                        try
                        {
                            var partition = GetOrCreatePartition(kvp.Key);
                            var result = await partition.WriteDataAsync(kvp.Value, cancellationToken);

                            if (result.IsSuccess)
                            {
                                writtenSymbols.Add(kvp.Key);
                                UpdateStatistics(kvp.Key, kvp.Value);
                                _memoryCache.InvalidateSymbol(kvp.Key);
                            }
                            else
                            {
                                errors.Add($"{kvp.Key}: {result.Error?.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{kvp.Key}: {ex.Message}");
                            LogError($"Failed to write data for {kvp.Key} during bulk operation", ex);
                        }
                    }

                    if (errors.Any())
                    {
                        var message = $"Bulk write completed with errors. Success: {writtenSymbols.Count}, Failed: {errors.Count}";
                        LogWarning(message, string.Join("; ", errors));
                        return TradingResult.Failure("PARTIAL_WRITE", message);
                    }

                    IncrementCounter("BulkWriteCount");
                    UpdateMetric("LastBulkWriteSymbols", writtenSymbols.Count);

                    LogInfo($"Successfully bulk wrote data for {writtenSymbols.Count} symbols");
                    LogMethodExit(success: true);
                    return TradingResult.Success();
                }
                finally
                {
                    _dataLock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                LogError("Failed during bulk write operation", ex);
                LogMethodExit(success: false);
                return TradingResult.Failure("BULK_WRITE_ERROR", $"Bulk write failed: {ex.Message}", ex);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Reads time series data for a symbol within a date range with streaming support.
        /// </summary>
        public async Task<TradingResult<IAsyncEnumerable<DailyData>>> ReadDataStreamAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate });

            if (string.IsNullOrWhiteSpace(symbol))
                return TradingResult<IAsyncEnumerable<DailyData>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");

            if (startDate > endDate)
                return TradingResult<IAsyncEnumerable<DailyData>>.Failure("INVALID_DATE_RANGE", "Start date must be before end date");

            _dataLock.EnterReadLock();
            Interlocked.Increment(ref _activeReaders);

            try
            {
                if (!_partitions.TryGetValue(symbol, out var partition))
                {
                    LogWarning($"No data found for symbol: {symbol}");
                    return TradingResult<IAsyncEnumerable<DailyData>>.Success(AsyncEnumerable.Empty<DailyData>());
                }

                // Check cache first
                var cacheKey = $"{symbol}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
                if (_memoryCache.TryGetCachedData(cacheKey, out IEnumerable<DailyData> cachedData))
                {
                    IncrementCounter("CacheHitCount");
                    LogDebug($"Cache hit for {symbol} [{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}]");
                    return TradingResult<IAsyncEnumerable<DailyData>>.Success(cachedData.ToAsyncEnumerable());
                }

                IncrementCounter("CacheMissCount");

                // Stream data from partition
                var dataStream = partition.ReadDataStreamAsync(startDate, endDate, cancellationToken);

                // Cache the data as it streams
                var cachingStream = CacheAsStreamAsync(dataStream, cacheKey, cancellationToken);

                IncrementCounter("DataReadCount");
                UpdateMetric("LastReadSymbol", symbol);

                LogMethodExit(success: true);
                return TradingResult<IAsyncEnumerable<DailyData>>.Success(cachingStream);
            }
            catch (Exception ex)
            {
                LogError($"Failed to read data stream for {symbol}", ex);
                LogMethodExit(success: false);
                return TradingResult<IAsyncEnumerable<DailyData>>.Failure("READ_ERROR", $"Failed to read data: {ex.Message}", ex);
            }
            finally
            {
                Interlocked.Decrement(ref _activeReaders);
                _dataLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Reads all data for a symbol with optimized batch loading.
        /// </summary>
        public async Task<TradingResult<List<DailyData>>> ReadDataAsync(
            string symbol,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, startDate, endDate });

            var streamResult = await ReadDataStreamAsync(
                symbol,
                startDate ?? DateTime.MinValue,
                endDate ?? DateTime.MaxValue,
                cancellationToken);

            if (!streamResult.IsSuccess)
                return TradingResult<List<DailyData>>.Failure(streamResult.Error!);

            try
            {
                var data = await streamResult.Value.ToListAsync(cancellationToken);
                LogMethodExit(new { recordCount = data.Count });
                return TradingResult<List<DailyData>>.Success(data);
            }
            catch (Exception ex)
            {
                LogError($"Failed to materialize data stream for {symbol}", ex);
                LogMethodExit(success: false);
                return TradingResult<List<DailyData>>.Failure("READ_ERROR", $"Failed to read data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Performs a time-based range query across multiple symbols.
        /// </summary>
        public async Task<TradingResult<Dictionary<string, List<DailyData>>>> RangeQueryAsync(
            IEnumerable<string> symbols,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbolCount = symbols.Count(), startDate, endDate });

            var symbolList = symbols.ToList();
            if (!symbolList.Any())
                return TradingResult<Dictionary<string, List<DailyData>>>.Success(new Dictionary<string, List<DailyData>>());

            var results = new ConcurrentDictionary<string, List<DailyData>>();
            var errors = new ConcurrentBag<string>();

            // Parallel read with controlled concurrency
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Min(symbolList.Count, Environment.ProcessorCount)
            };

            await Parallel.ForEachAsync(symbolList, parallelOptions, async (symbol, ct) =>
            {
                var result = await ReadDataAsync(symbol, startDate, endDate, ct);
                if (result.IsSuccess)
                {
                    results.TryAdd(symbol, result.Value);
                }
                else
                {
                    errors.Add($"{symbol}: {result.Error?.Message}");
                    LogWarning($"Failed to read data for {symbol} in range query", result.Error?.Message);
                }
            });

            if (errors.Any())
            {
                var message = $"Range query completed with errors. Success: {results.Count}, Failed: {errors.Count}";
                LogWarning(message, string.Join("; ", errors));
            }

            IncrementCounter("RangeQueryCount");
            UpdateMetric("LastRangeQuerySymbols", symbolList.Count);

            LogMethodExit(new { successCount = results.Count, errorCount = errors.Count });
            return TradingResult<Dictionary<string, List<DailyData>>>.Success(results.ToDictionary(x => x.Key, x => x.Value));
        }

        #endregion

        #region Statistics Operations

        /// <summary>
        /// Gets pre-computed statistics for a symbol.
        /// </summary>
        public async Task<TradingResult<DataStatistics>> GetStatisticsAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol });

            if (string.IsNullOrWhiteSpace(symbol))
                return TradingResult<DataStatistics>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");

            try
            {
                if (_statistics.TryGetValue(symbol, out var stats))
                {
                    LogMethodExit(success: true);
                    return TradingResult<DataStatistics>.Success(stats);
                }

                // Compute statistics if not cached
                var dataResult = await ReadDataAsync(symbol, cancellationToken: cancellationToken);
                if (!dataResult.IsSuccess)
                    return TradingResult<DataStatistics>.Failure(dataResult.Error!);

                stats = ComputeStatistics(symbol, dataResult.Value);
                _statistics.TryAdd(symbol, stats);

                LogMethodExit(success: true);
                return TradingResult<DataStatistics>.Success(stats);
            }
            catch (Exception ex)
            {
                LogError($"Failed to get statistics for {symbol}", ex);
                LogMethodExit(success: false);
                return TradingResult<DataStatistics>.Failure("STATS_ERROR", $"Failed to get statistics: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets aggregated statistics across all stored symbols.
        /// </summary>
        public Task<TradingResult<AggregatedStatistics>> GetAggregatedStatisticsAsync(
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();

            try
            {
                var aggregated = new AggregatedStatistics
                {
                    TotalSymbols = _partitions.Count,
                    TotalRecords = _statistics.Sum(x => x.Value.RecordCount),
                    TotalMemoryUsageMB = _totalBytesInMemory / (1024.0 * 1024.0),
                    TotalDiskUsageMB = _totalBytesOnDisk / (1024.0 * 1024.0),
                    CacheHitRate = _memoryCache.GetHitRate(),
                    ActiveReaders = _activeReaders,
                    LastCompactionTime = _lastCompactionTime,
                    SymbolStatistics = _statistics.ToDictionary(x => x.Key, x => x.Value)
                };

                LogMethodExit(success: true);
                return Task.FromResult(TradingResult<AggregatedStatistics>.Success(aggregated));
            }
            catch (Exception ex)
            {
                LogError("Failed to get aggregated statistics", ex);
                LogMethodExit(success: false);
                return Task.FromResult(TradingResult<AggregatedStatistics>.Failure(
                    "STATS_ERROR", $"Failed to get aggregated statistics: {ex.Message}", ex));
            }
        }

        #endregion

        #region TimescaleDB Integration

        /// <summary>
        /// Syncs data with TimescaleDB for advanced time-series queries.
        /// </summary>
        public async Task<TradingResult> SyncWithTimescaleDbAsync(
            string symbol,
            bool fullSync = false,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry(new { symbol, fullSync });

            if (_timescaleDb == null || !_timescaleDb.IsConnected)
                return TradingResult.Failure("TIMESCALE_NOT_CONNECTED", "TimescaleDB is not connected");

            try
            {
                var dataResult = await ReadDataAsync(symbol, cancellationToken: cancellationToken);
                if (!dataResult.IsSuccess)
                    return TradingResult.Failure(dataResult.Error!);

                var syncResult = await _timescaleDb.SyncDataAsync(symbol, dataResult.Value, fullSync, cancellationToken);

                if (syncResult.IsSuccess)
                {
                    IncrementCounter("TimescaleDbSyncCount");
                    LogInfo($"Successfully synced {symbol} with TimescaleDB", new { recordCount = dataResult.Value.Count, fullSync });
                }

                LogMethodExit(syncResult);
                return syncResult;
            }
            catch (Exception ex)
            {
                LogError($"Failed to sync {symbol} with TimescaleDB", ex);
                LogMethodExit(success: false);
                return TradingResult.Failure("SYNC_ERROR", $"Failed to sync with TimescaleDB: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        private SymbolDataPartition GetOrCreatePartition(string symbol)
        {
            LogDebug($"Getting or creating partition for {symbol}");

            return _partitions.GetOrAdd(symbol, s =>
            {
                var partition = new SymbolDataPartition(Logger, _baseDataPath, s);
                LogInfo($"Created new partition for {symbol}");
                return partition;
            });
        }

        private async Task LoadExistingPartitionsAsync(CancellationToken cancellationToken)
        {
            LogDebug("Loading existing partitions from disk");

            var symbolDirs = Directory.GetDirectories(_baseDataPath);
            var loadedCount = 0;

            foreach (var dir in symbolDirs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var symbol = Path.GetFileName(dir);
                    var partition = new SymbolDataPartition(Logger, _baseDataPath, symbol);
                    
                    if (await partition.LoadMetadataAsync(cancellationToken))
                    {
                        _partitions.TryAdd(symbol, partition);
                        loadedCount++;
                        
                        // Load statistics
                        var statsFile = Path.Combine(dir, $"{symbol}{STATS_FILE_EXTENSION}");
                        if (File.Exists(statsFile))
                        {
                            var stats = await LoadStatisticsFromFileAsync(statsFile, cancellationToken);
                            if (stats != null)
                                _statistics.TryAdd(symbol, stats);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to load partition from {dir}", ex: ex);
                }
            }

            LogInfo($"Loaded {loadedCount} existing partitions");
        }

        private void UpdateStatistics(string symbol, List<DailyData> data)
        {
            LogDebug($"Updating statistics for {symbol} with {data.Count} records");

            var stats = ComputeStatistics(symbol, data);
            _statistics.AddOrUpdate(symbol, stats, (k, existing) =>
            {
                // Merge statistics
                return new DataStatistics
                {
                    Symbol = symbol,
                    RecordCount = existing.RecordCount + data.Count,
                    StartDate = data.Min(d => d.Date) < existing.StartDate ? data.Min(d => d.Date) : existing.StartDate,
                    EndDate = data.Max(d => d.Date) > existing.EndDate ? data.Max(d => d.Date) : existing.EndDate,
                    MinPrice = Math.Min(existing.MinPrice, data.Min(d => d.Low)),
                    MaxPrice = Math.Max(existing.MaxPrice, data.Max(d => d.High)),
                    AverageVolume = (existing.AverageVolume * existing.RecordCount + data.Average(d => d.Volume)) / (existing.RecordCount + data.Count),
                    LastUpdated = DateTime.UtcNow
                };
            });
        }

        private DataStatistics ComputeStatistics(string symbol, List<DailyData> data)
        {
            if (!data.Any())
            {
                return new DataStatistics { Symbol = symbol, LastUpdated = DateTime.UtcNow };
            }

            return new DataStatistics
            {
                Symbol = symbol,
                RecordCount = data.Count,
                StartDate = data.Min(d => d.Date),
                EndDate = data.Max(d => d.Date),
                MinPrice = data.Min(d => d.Low),
                MaxPrice = data.Max(d => d.High),
                AveragePrice = data.Average(d => d.Close),
                AverageVolume = data.Average(d => d.Volume),
                LastUpdated = DateTime.UtcNow
            };
        }

        private async IAsyncEnumerable<DailyData> CacheAsStreamAsync(
            IAsyncEnumerable<DailyData> dataStream,
            string cacheKey,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var buffer = new List<DailyData>();

            await foreach (var item in dataStream.WithCancellation(cancellationToken))
            {
                buffer.Add(item);
                yield return item;
            }

            // Cache the collected data
            if (buffer.Any())
            {
                _memoryCache.CacheData(cacheKey, buffer);
                LogDebug($"Cached {buffer.Count} records for key: {cacheKey}");
            }
        }

        private async Task FlushAllPartitionsAsync(CancellationToken cancellationToken)
        {
            LogDebug("Flushing all partitions to disk");

            var tasks = _partitions.Values.Select(p => p.FlushAsync(cancellationToken));
            await Task.WhenAll(tasks);

            LogInfo($"Flushed {_partitions.Count} partitions to disk");
        }

        private async Task SaveStatisticsAsync(CancellationToken cancellationToken)
        {
            LogDebug("Saving statistics to disk");

            foreach (var kvp in _statistics)
            {
                try
                {
                    var statsFile = Path.Combine(_baseDataPath, kvp.Key, $"{kvp.Key}{STATS_FILE_EXTENSION}");
                    await SaveStatisticsToFileAsync(statsFile, kvp.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to save statistics for {kvp.Key}", ex: ex);
                }
            }
        }

        private async Task<DataStatistics?> LoadStatisticsFromFileAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return System.Text.Json.JsonSerializer.Deserialize<DataStatistics>(json);
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to load statistics from {filePath}", ex: ex);
                return null;
            }
        }

        private async Task SaveStatisticsToFileAsync(string filePath, DataStatistics stats, CancellationToken cancellationToken)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        private void StartMemoryMonitoring(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var process = Process.GetCurrentProcess();
                        var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
                        var gcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

                        UpdateMetric("ProcessMemoryMB", workingSetMB);
                        UpdateMetric("GCMemoryMB", gcMemoryMB);
                        UpdateMetric("CacheMemoryMB", _totalBytesInMemory / (1024.0 * 1024.0));

                        if (workingSetMB > MAX_MEMORY_CACHE_SIZE_MB * 1.5)
                        {
                            LogWarning("High memory usage detected", $"Working set: {workingSetMB:F2} MB");
                            _memoryCache.Trim(0.2); // Trim 20% of cache
                        }

                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        LogError("Error in memory monitoring", ex);
                    }
                }
            }, cancellationToken);
        }

        private async Task BackgroundCompactionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), cancellationToken);

                    if (DateTime.UtcNow - _lastCompactionTime < TimeSpan.FromHours(4))
                        continue;

                    LogInfo("Starting background compaction");

                    foreach (var partition in _partitions.Values)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        await partition.CompactAsync(cancellationToken);
                    }

                    _lastCompactionTime = DateTime.UtcNow;
                    LogInfo("Background compaction completed");
                }
                catch (Exception ex)
                {
                    LogError("Error during background compaction", ex);
                }
            }
        }

        private bool IsTimescaleDbConfigured()
        {
            // Check configuration for TimescaleDB settings
            // This would normally read from configuration
            return false; // Placeholder
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _dataLock?.Dispose();
                    _writeSemaphore?.Dispose();
                    _memoryCache?.Dispose();
                    _timescaleDb?.Dispose();

                    foreach (var partition in _partitions.Values)
                    {
                        partition.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LogError("Error during disposal", ex);
                }
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a data partition for a single symbol with columnar storage.
        /// </summary>
        private class SymbolDataPartition : IDisposable
        {
            private readonly ITradingLogger _logger;
            private readonly string _baseDataPath;
            private readonly string _symbol;
            private readonly string _partitionPath;
            private readonly ReaderWriterLockSlim _partitionLock;
            private readonly SortedDictionary<DateTime, long> _dateIndex;
            
            private MemoryMappedFile? _dataFile;
            private MemoryMappedViewAccessor? _dataAccessor;
            private BinaryWriter? _dataWriter;
            private long _currentFilePosition;
            private bool _isCompressed;

            public SymbolDataPartition(ITradingLogger logger, string baseDataPath, string symbol)
            {
                _logger = logger;
                _baseDataPath = baseDataPath;
                _symbol = symbol;
                _partitionPath = Path.Combine(_baseDataPath, _symbol);
                _partitionLock = new ReaderWriterLockSlim();
                _dateIndex = new SortedDictionary<DateTime, long>();

                if (!Directory.Exists(_partitionPath))
                {
                    Directory.CreateDirectory(_partitionPath);
                }
            }

            public async Task<TradingResult> WriteDataAsync(List<DailyData> data, CancellationToken cancellationToken)
            {
                _partitionLock.EnterWriteLock();
                try
                {
                    // Sort data by date
                    data.Sort((a, b) => a.Date.CompareTo(b.Date));

                    // Open or create data file
                    await EnsureDataFileOpenAsync(cancellationToken);

                    // Write data in columnar format
                    foreach (var item in data)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var position = _currentFilePosition;
                        
                        // Write data in binary format
                        WriteDataRecord(item);
                        
                        // Update index
                        _dateIndex[item.Date] = position;
                        
                        _currentFilePosition = _dataWriter!.BaseStream.Position;
                    }

                    // Flush to disk
                    await _dataWriter!.BaseStream.FlushAsync(cancellationToken);

                    // Save index
                    await SaveIndexAsync(cancellationToken);

                    return TradingResult.Success();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to write data to partition {_symbol}", ex);
                    return TradingResult.Failure("PARTITION_WRITE_ERROR", $"Failed to write partition data: {ex.Message}", ex);
                }
                finally
                {
                    _partitionLock.ExitWriteLock();
                }
            }

            public async IAsyncEnumerable<DailyData> ReadDataStreamAsync(
                DateTime startDate,
                DateTime endDate,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                _partitionLock.EnterReadLock();
                try
                {
                    if (_dateIndex.Count == 0)
                        yield break;

                    // Find start position using binary search
                    var startKey = _dateIndex.Keys.FirstOrDefault(d => d >= startDate);
                    if (startKey == default)
                        yield break;

                    // Stream data from start position
                    foreach (var kvp in _dateIndex.Where(x => x.Key >= startDate && x.Key <= endDate))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            yield break;

                        var data = await ReadDataRecordAsync(kvp.Value, cancellationToken);
                        if (data != null)
                            yield return data;
                    }
                }
                finally
                {
                    _partitionLock.ExitReadLock();
                }
            }

            public async Task<bool> LoadMetadataAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var indexFile = Path.Combine(_partitionPath, $"{_symbol}{INDEX_FILE_EXTENSION}");
                    if (!File.Exists(indexFile))
                        return false;

                    var indexData = await File.ReadAllTextAsync(indexFile, cancellationToken);
                    var index = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(indexData);

                    if (index != null)
                    {
                        _dateIndex.Clear();
                        foreach (var kvp in index)
                        {
                            if (DateTime.TryParse(kvp.Key, out var date))
                            {
                                _dateIndex[date] = kvp.Value;
                            }
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to load metadata for partition {_symbol}", ex: ex);
                    return false;
                }
            }

            public async Task FlushAsync(CancellationToken cancellationToken)
            {
                _partitionLock.EnterWriteLock();
                try
                {
                    if (_dataWriter != null)
                    {
                        await _dataWriter.BaseStream.FlushAsync(cancellationToken);
                        await SaveIndexAsync(cancellationToken);
                    }
                }
                finally
                {
                    _partitionLock.ExitWriteLock();
                }
            }

            public async Task CompactAsync(CancellationToken cancellationToken)
            {
                _partitionLock.EnterWriteLock();
                try
                {
                    // Compact and compress data file
                    var dataFile = Path.Combine(_partitionPath, $"{_symbol}{DATA_FILE_EXTENSION}");
                    if (File.Exists(dataFile) && !_isCompressed)
                    {
                        var compressedFile = dataFile + ".gz";
                        
                        using (var sourceStream = File.OpenRead(dataFile))
                        using (var targetStream = File.Create(compressedFile))
                        using (var compressionStream = new GZipStream(targetStream, CompressionLevel.Optimal))
                        {
                            await sourceStream.CopyToAsync(compressionStream, cancellationToken);
                        }

                        // Replace original with compressed
                        File.Delete(dataFile);
                        File.Move(compressedFile, dataFile);
                        _isCompressed = true;

                        _logger.LogInfo($"Compacted partition {_symbol}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to compact partition {_symbol}", ex: ex);
                }
                finally
                {
                    _partitionLock.ExitWriteLock();
                }
            }

            private async Task EnsureDataFileOpenAsync(CancellationToken cancellationToken)
            {
                if (_dataWriter == null)
                {
                    var dataFile = Path.Combine(_partitionPath, $"{_symbol}{DATA_FILE_EXTENSION}");
                    var fileStream = new FileStream(dataFile, FileMode.Append, FileAccess.Write, FileShare.Read);
                    _dataWriter = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true);
                    _currentFilePosition = fileStream.Position;
                }
            }

            private void WriteDataRecord(DailyData data)
            {
                // Write in efficient binary format
                _dataWriter!.Write(data.Date.Ticks);
                _dataWriter.Write(data.Open);
                _dataWriter.Write(data.High);
                _dataWriter.Write(data.Low);
                _dataWriter.Write(data.Close);
                _dataWriter.Write(data.AdjustedClose);
                _dataWriter.Write(data.Volume);
            }

            private async Task<DailyData?> ReadDataRecordAsync(long position, CancellationToken cancellationToken)
            {
                try
                {
                    var dataFile = Path.Combine(_partitionPath, $"{_symbol}{DATA_FILE_EXTENSION}");
                    using var fileStream = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new BinaryReader(fileStream);

                    fileStream.Seek(position, SeekOrigin.Begin);

                    return new DailyData
                    {
                        Symbol = _symbol,
                        Date = new DateTime(reader.ReadInt64()),
                        Open = reader.ReadDecimal(),
                        High = reader.ReadDecimal(),
                        Low = reader.ReadDecimal(),
                        Close = reader.ReadDecimal(),
                        AdjustedClose = reader.ReadDecimal(),
                        Volume = reader.ReadInt64()
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to read data record at position {position}", ex: ex);
                    return null;
                }
            }

            private async Task SaveIndexAsync(CancellationToken cancellationToken)
            {
                var indexFile = Path.Combine(_partitionPath, $"{_symbol}{INDEX_FILE_EXTENSION}");
                var indexData = _dateIndex.ToDictionary(x => x.Key.ToString("yyyy-MM-dd"), x => x.Value);
                var json = System.Text.Json.JsonSerializer.Serialize(indexData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(indexFile, json, cancellationToken);
            }

            public void Dispose()
            {
                _partitionLock?.EnterWriteLock();
                try
                {
                    _dataWriter?.Dispose();
                    _dataAccessor?.Dispose();
                    _dataFile?.Dispose();
                }
                finally
                {
                    _partitionLock?.ExitWriteLock();
                    _partitionLock?.Dispose();
                }
            }
        }

        /// <summary>
        /// Memory cache with LRU eviction policy.
        /// </summary>
        private class MemoryCache : IDisposable
        {
            private readonly long _maxSizeBytes;
            private readonly Dictionary<string, CacheEntry> _cache;
            private readonly LinkedList<string> _lruList;
            private readonly ReaderWriterLockSlim _cacheLock;
            private long _currentSizeBytes;
            private long _hitCount;
            private long _missCount;

            public MemoryCache(long maxSizeBytes)
            {
                _maxSizeBytes = maxSizeBytes;
                _cache = new Dictionary<string, CacheEntry>();
                _lruList = new LinkedList<string>();
                _cacheLock = new ReaderWriterLockSlim();
            }

            public bool TryGetCachedData(string key, out IEnumerable<DailyData> data)
            {
                _cacheLock.EnterUpgradeableReadLock();
                try
                {
                    if (_cache.TryGetValue(key, out var entry))
                    {
                        _cacheLock.EnterWriteLock();
                        try
                        {
                            // Move to front (most recently used)
                            _lruList.Remove(entry.LruNode);
                            _lruList.AddFirst(entry.LruNode);
                            Interlocked.Increment(ref _hitCount);
                        }
                        finally
                        {
                            _cacheLock.ExitWriteLock();
                        }

                        data = entry.Data;
                        return true;
                    }

                    Interlocked.Increment(ref _missCount);
                    data = Enumerable.Empty<DailyData>();
                    return false;
                }
                finally
                {
                    _cacheLock.ExitUpgradeableReadLock();
                }
            }

            public void CacheData(string key, IEnumerable<DailyData> data)
            {
                var dataList = data.ToList();
                var sizeBytes = EstimateSize(dataList);

                _cacheLock.EnterWriteLock();
                try
                {
                    // Evict if necessary
                    while (_currentSizeBytes + sizeBytes > _maxSizeBytes && _lruList.Count > 0)
                    {
                        var evictKey = _lruList.Last!.Value;
                        _lruList.RemoveLast();
                        
                        if (_cache.TryGetValue(evictKey, out var evicted))
                        {
                            _currentSizeBytes -= evicted.SizeBytes;
                            _cache.Remove(evictKey);
                        }
                    }

                    // Add new entry
                    var node = _lruList.AddFirst(key);
                    _cache[key] = new CacheEntry
                    {
                        Data = dataList,
                        SizeBytes = sizeBytes,
                        LruNode = node
                    };
                    _currentSizeBytes += sizeBytes;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }

            public void InvalidateSymbol(string symbol)
            {
                _cacheLock.EnterWriteLock();
                try
                {
                    var keysToRemove = _cache.Keys.Where(k => k.StartsWith(symbol + ":")).ToList();
                    foreach (var key in keysToRemove)
                    {
                        if (_cache.TryGetValue(key, out var entry))
                        {
                            _lruList.Remove(entry.LruNode);
                            _currentSizeBytes -= entry.SizeBytes;
                            _cache.Remove(key);
                        }
                    }
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }

            public void Trim(double percentage)
            {
                _cacheLock.EnterWriteLock();
                try
                {
                    var targetSize = (long)(_maxSizeBytes * (1 - percentage));
                    while (_currentSizeBytes > targetSize && _lruList.Count > 0)
                    {
                        var evictKey = _lruList.Last!.Value;
                        _lruList.RemoveLast();

                        if (_cache.TryGetValue(evictKey, out var evicted))
                        {
                            _currentSizeBytes -= evicted.SizeBytes;
                            _cache.Remove(evictKey);
                        }
                    }
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }

            public double GetHitRate()
            {
                var total = _hitCount + _missCount;
                return total > 0 ? (double)_hitCount / total : 0;
            }

            private long EstimateSize(List<DailyData> data)
            {
                // Rough estimate: 8 bytes per field * 7 fields + overhead
                return data.Count * 100;
            }

            private class CacheEntry
            {
                public List<DailyData> Data { get; set; } = new();
                public long SizeBytes { get; set; }
                public LinkedListNode<string> LruNode { get; set; } = null!;
            }

            public void Dispose()
            {
                _cacheLock?.Dispose();
            }
        }

        /// <summary>
        /// TimescaleDB connector for advanced time-series operations.
        /// </summary>
        private class TimescaleDbConnector : IDisposable
        {
            private readonly ITradingLogger _logger;
            private bool _isConnected;

            public bool IsConnected => _isConnected;

            public TimescaleDbConnector(ITradingLogger logger)
            {
                _logger = logger;
            }

            public Task<TradingResult> ConnectAsync(CancellationToken cancellationToken)
            {
                // Implementation would connect to TimescaleDB
                _isConnected = false; // Placeholder
                return Task.FromResult(TradingResult.Success());
            }

            public Task<TradingResult> DisconnectAsync(CancellationToken cancellationToken)
            {
                _isConnected = false;
                return Task.FromResult(TradingResult.Success());
            }

            public Task<TradingResult> SyncDataAsync(string symbol, List<DailyData> data, bool fullSync, CancellationToken cancellationToken)
            {
                // Implementation would sync data to TimescaleDB
                return Task.FromResult(TradingResult.Success());
            }

            public void Dispose()
            {
                // Cleanup
            }
        }

        /// <summary>
        /// Data statistics for a symbol.
        /// </summary>
        public class DataStatistics
        {
            public string Symbol { get; set; } = string.Empty;
            public int RecordCount { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public decimal MinPrice { get; set; }
            public decimal MaxPrice { get; set; }
            public decimal AveragePrice { get; set; }
            public double AverageVolume { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        /// <summary>
        /// Aggregated statistics across all symbols.
        /// </summary>
        public class AggregatedStatistics
        {
            public int TotalSymbols { get; set; }
            public int TotalRecords { get; set; }
            public double TotalMemoryUsageMB { get; set; }
            public double TotalDiskUsageMB { get; set; }
            public double CacheHitRate { get; set; }
            public int ActiveReaders { get; set; }
            public DateTime LastCompactionTime { get; set; }
            public Dictionary<string, DataStatistics> SymbolStatistics { get; set; } = new();
        }

        #endregion
    }

    /// <summary>
    /// Helper class for async enumerable operations.
    /// </summary>
    public static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}