using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.TechnicalAnalysis.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuanTAlib;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Services;

/// <summary>
/// Service for real-time technical analysis using QuanTAlib.
/// Inherits from CanonicalServiceBase for consistency.
/// </summary>
public class TechnicalAnalysisService : CanonicalServiceBase, ITechnicalAnalysisService
{
    private readonly IMemoryCache _cache;
    private readonly Channel<MarketQuote> _quoteChannel;
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _indicators;
    private readonly ConcurrentDictionary<string, List<decimal>> _priceHistory;
    private readonly ConcurrentDictionary<string, List<(decimal High, decimal Low, decimal Close)>> _ohlcHistory;
    private readonly ConcurrentDictionary<string, List<(decimal Price, long Volume)>> _priceVolumeHistory;
    private readonly SemaphoreSlim _calculationSemaphore;
    private readonly CancellationTokenSource _processingTokenSource;
    private Task? _processingTask;

    /// <summary>
    /// Initializes a new instance of the TechnicalAnalysisService class.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="cache">Memory cache for indicator values</param>
    public TechnicalAnalysisService(ILogger<TechnicalAnalysisService> logger, IMemoryCache cache)
        : base(logger, nameof(TechnicalAnalysisService))
    {
        LogMethodEntry();

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _quoteChannel = Channel.CreateUnbounded<MarketQuote>();
        _indicators = new ConcurrentDictionary<string, Dictionary<string, object>>();
        _priceHistory = new ConcurrentDictionary<string, List<decimal>>();
        _ohlcHistory = new ConcurrentDictionary<string, List<(decimal, decimal, decimal)>>();
        _priceVolumeHistory = new ConcurrentDictionary<string, List<(decimal, long)>>();
        _calculationSemaphore = new SemaphoreSlim(1, 1);
        _processingTokenSource = new CancellationTokenSource();

        LogMethodExit();
    }

    #region CanonicalServiceBase Implementation

    /// <inheritdoc />
    protected override Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            LogInfo("Initializing QuanTAlib indicators and processing pipeline");

            // Initialize processing task
            _processingTask = ProcessQuotesAsync(_processingTokenSource.Token);

            LogInfo("TechnicalAnalysisService initialized successfully");
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize TechnicalAnalysisService", ex);
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Failure("INIT_FAILED", "Failed to initialize technical analysis service", ex));
        }
    }

    /// <inheritdoc />
    protected override Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            LogInfo("TechnicalAnalysisService started successfully");
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            LogError("Failed to start TechnicalAnalysisService", ex);
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Failure("START_FAILED", "Failed to start technical analysis service", ex));
        }
    }

    /// <inheritdoc />
    protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            LogInfo("Stopping TechnicalAnalysisService");
            
            _processingTokenSource.Cancel();
            if (_processingTask != null)
            {
                await _processingTask.ConfigureAwait(false);
            }

            LogInfo("TechnicalAnalysisService stopped successfully");
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to stop TechnicalAnalysisService", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("STOP_FAILED", "Failed to stop technical analysis service", ex);
        }
    }

    #endregion

    #region ITechnicalAnalysisService Implementation

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateRSIAsync(string symbol, int period = 14, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"RSI_{symbol}_{period}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < period + 1)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price data for RSI calculation");
                }

                // Use QuanTAlib for RSI calculation
                var rsi = new QuanTAlib.Rsi(period);
                decimal rsiValue = 50m; // Default value

                foreach (var price in prices)
                {
                    var result = rsi.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    rsiValue = (decimal)result.Value;
                }

                // Cache result for 60 seconds
                _cache.Set(cacheKey, rsiValue, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<decimal>.Success(rsiValue);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate RSI for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate RSI", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateSMAAsync(string symbol, int period, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"SMA_{symbol}_{period}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < period)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price data for SMA calculation");
                }

                // Use QuanTAlib for SMA calculation
                var sma = new QuanTAlib.Sma(period);
                decimal smaValue = 0m;

                foreach (var price in prices)
                {
                    var result = sma.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    smaValue = (decimal)result.Value;
                }

                // Cache result for 60 seconds
                _cache.Set(cacheKey, smaValue, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<decimal>.Success(smaValue);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate SMA for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate SMA", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateEMAAsync(string symbol, int period, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"EMA_{symbol}_{period}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < period)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price data for EMA calculation");
                }

                // Use QuanTAlib for EMA calculation
                var ema = new QuanTAlib.Ema(period);
                decimal emaValue = 0m;

                foreach (var price in prices)
                {
                    var result = ema.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    emaValue = (decimal)result.Value;
                }

                // Cache result for 60 seconds
                _cache.Set(cacheKey, emaValue, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<decimal>.Success(emaValue);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate EMA for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate EMA", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<(decimal Upper, decimal Middle, decimal Lower)>> CalculateBollingerBandsAsync(
        string symbol, 
        int period = 20, 
        decimal standardDeviations = 2.0m, 
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"BB_{symbol}_{period}_{standardDeviations}";
            if (_cache.TryGetValue(cacheKey, out (decimal Upper, decimal Middle, decimal Lower) cachedValue))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < period)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal, decimal)>.Failure("INSUFFICIENT_DATA", "Not enough price data for Bollinger Bands calculation");
                }

                // Use QuanTAlib Bband for middle band, calculate upper/lower manually
                var bband = new QuanTAlib.Bband(period, (double)standardDeviations);
                decimal middleBand = 0m;
                decimal standardDev = 0m;

                // Calculate both middle band and standard deviation from price data
                foreach (var price in prices)
                {
                    var bbResult = bband.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    middleBand = (decimal)bbResult.Value;
                }

                // Calculate standard deviation manually for upper/lower bands
                if (prices.Count >= period)
                {
                    var recentPrices = prices.TakeLast(period).ToList();
                    var mean = recentPrices.Average();
                    var variance = recentPrices.Select(p => Math.Pow((double)(p - mean), 2)).Average();
                    standardDev = (decimal)Math.Sqrt(variance);
                }

                var result = (
                    Upper: middleBand + (standardDeviations * standardDev),
                    Middle: middleBand,
                    Lower: middleBand - (standardDeviations * standardDev)
                );

                // Cache result for 60 seconds
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Success(result);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate Bollinger Bands for {symbol}", ex);
            LogMethodExit();
            return TradingResult<(decimal, decimal, decimal)>.Failure("CALCULATION_ERROR", "Failed to calculate Bollinger Bands", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<(decimal MACD, decimal Signal, decimal Histogram)>> CalculateMACDAsync(
        string symbol, 
        int fastPeriod = 12, 
        int slowPeriod = 26, 
        int signalPeriod = 9, 
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"MACD_{symbol}_{fastPeriod}_{slowPeriod}_{signalPeriod}";
            if (_cache.TryGetValue(cacheKey, out (decimal MACD, decimal Signal, decimal Histogram) cachedValue))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < slowPeriod + signalPeriod)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal, decimal)>.Failure("INSUFFICIENT_DATA", "Not enough price data for MACD calculation");
                }

                // Use QuanTAlib for MACD line calculation
                var macd = new QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
                var signalEma = new QuanTAlib.Ema(signalPeriod);
                
                decimal macdLine = 0m;
                decimal signalLine = 0m;
                var macdValues = new List<decimal>();

                // Calculate MACD line for all prices
                foreach (var price in prices)
                {
                    var macdResult = macd.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    macdLine = (decimal)macdResult.Value;
                    macdValues.Add(macdLine);
                }

                // Calculate signal line (EMA of MACD values)
                foreach (var macdValue in macdValues)
                {
                    var signalResult = signalEma.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)macdValue));
                    signalLine = (decimal)signalResult.Value;
                }

                // Calculate histogram (MACD - Signal)
                var histogram = macdLine - signalLine;

                var result = (
                    MACD: macdLine,
                    Signal: signalLine,
                    Histogram: histogram
                );

                // Cache result for 60 seconds
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal)>.Success(result);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate MACD for {symbol}", ex);
            LogMethodExit();
            return TradingResult<(decimal, decimal, decimal)>.Failure("CALCULATION_ERROR", "Failed to calculate MACD", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateATRAsync(string symbol, int period = 14, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"ATR_{symbol}_{period}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceHistory.TryGetValue(symbol, out var prices) || prices.Count < period)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price data for ATR calculation");
                }

                // Use QuanTAlib for ATR calculation
                var atr = new QuanTAlib.Atr(period);
                decimal atrValue = 0m;

                // ATR needs high, low, close data - using close as approximation for now
                foreach (var price in prices)
                {
                    var result = atr.Calc(new QuanTAlib.TValue(DateTime.UtcNow, (double)price));
                    atrValue = (decimal)result.Value;
                }

                // Cache result for 60 seconds
                _cache.Set(cacheKey, atrValue, TimeSpan.FromSeconds(60));

                LogMethodExit();
                return TradingResult<decimal>.Success(atrValue);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate ATR for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate ATR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<(decimal K, decimal D)>> CalculateStochasticAsync(
        string symbol, 
        int kPeriod = 14, 
        int dPeriod = 3, 
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal)>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"STOCH_{symbol}_{kPeriod}_{dPeriod}";
            if (_cache.TryGetValue(cacheKey, out (decimal K, decimal D) cachedValue))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal)>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_ohlcHistory.TryGetValue(symbol, out var ohlcData) || ohlcData.Count < kPeriod + dPeriod)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal)>.Failure("INSUFFICIENT_DATA", "Not enough OHLC data for Stochastic calculation");
                }

                // Industry standard Stochastic calculation using OHLC data
                // %K = [(Current Close - Lowest Low over K periods) / (Highest High - Lowest Low over K periods)] * 100
                
                var recentData = ohlcData.TakeLast(kPeriod + dPeriod).ToList();
                var kValues = new List<decimal>();

                // Calculate %K for each period
                for (int i = kPeriod - 1; i < recentData.Count; i++)
                {
                    var lookbackData = recentData.Skip(i - kPeriod + 1).Take(kPeriod).ToList();
                    var currentClose = lookbackData.Last().Close;
                    var lowestLow = lookbackData.Min(x => x.Low);
                    var highestHigh = lookbackData.Max(x => x.High);

                    if (highestHigh == lowestLow)
                    {
                        // Avoid division by zero - when price is flat, %K should be 50
                        kValues.Add(50m);
                    }
                    else
                    {
                        var kValue = ((currentClose - lowestLow) / (highestHigh - lowestLow)) * 100m;
                        kValues.Add(kValue);
                    }
                }

                if (kValues.Count == 0)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal)>.Failure("CALCULATION_ERROR", "No valid %K values calculated");
                }

                // %D is the SMA of %K values over D period
                var currentK = kValues.Last();
                var dValue = kValues.Count >= dPeriod 
                    ? kValues.TakeLast(dPeriod).Average()
                    : kValues.Average();

                var result = (K: currentK, D: dValue);

                // Cache result for 60 seconds
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));

                LogInfo($"Calculated Stochastic for {symbol}: %K={currentK:F2}, %D={dValue:F2}");
                LogMethodExit();
                return TradingResult<(decimal, decimal)>.Success(result);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate Stochastic for {symbol}", ex);
            LogMethodExit();
            return TradingResult<(decimal, decimal)>.Failure("CALCULATION_ERROR", "Failed to calculate Stochastic", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateOBVAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"OBV_{symbol}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceVolumeHistory.TryGetValue(symbol, out var priceVolumeData) || priceVolumeData.Count < 2)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price/volume data for OBV calculation");
                }

                // On-Balance Volume calculation:
                // If today's closing price > yesterday's closing price: OBV = Previous OBV + Today's Volume
                // If today's closing price < yesterday's closing price: OBV = Previous OBV - Today's Volume  
                // If today's closing price = yesterday's closing price: OBV = Previous OBV
                
                decimal obv = 0m;
                for (int i = 1; i < priceVolumeData.Count; i++)
                {
                    var currentPrice = priceVolumeData[i].Price;
                    var previousPrice = priceVolumeData[i - 1].Price;
                    var currentVolume = priceVolumeData[i].Volume;

                    if (currentPrice > previousPrice)
                    {
                        // Price increased - add volume
                        obv += currentVolume;
                    }
                    else if (currentPrice < previousPrice)
                    {
                        // Price decreased - subtract volume
                        obv -= currentVolume;
                    }
                    // If prices are equal, OBV remains unchanged
                }

                // Cache result for 60 seconds
                _cache.Set(cacheKey, obv, TimeSpan.FromSeconds(60));

                LogInfo($"Calculated OBV for {symbol}: {obv:F0}");
                LogMethodExit();
                return TradingResult<decimal>.Success(obv);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate OBV for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate OBV", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<Dictionary<decimal, long>>> CalculateVolumeProfileAsync(
        string symbol, 
        int priceLevels = 20, 
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<Dictionary<decimal, long>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            if (priceLevels <= 0 || priceLevels > 100)
            {
                LogMethodExit();
                return TradingResult<Dictionary<decimal, long>>.Failure("INVALID_PRICE_LEVELS", "Price levels must be between 1 and 100");
            }

            var cacheKey = $"VP_{symbol}_{priceLevels}";
            if (_cache.TryGetValue(cacheKey, out Dictionary<decimal, long>? cachedValue) && cachedValue != null)
            {
                LogMethodExit();
                return TradingResult<Dictionary<decimal, long>>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceVolumeHistory.TryGetValue(symbol, out var priceVolumeData) || priceVolumeData.Count < 10)
                {
                    LogMethodExit();
                    return TradingResult<Dictionary<decimal, long>>.Failure("INSUFFICIENT_DATA", "Not enough price/volume data for Volume Profile calculation");
                }

                // Volume Profile calculation:
                // 1. Find the price range (min and max prices)
                // 2. Divide the range into specified number of price levels
                // 3. Sum volume for each price level
                
                var minPrice = priceVolumeData.Min(x => x.Price);
                var maxPrice = priceVolumeData.Max(x => x.Price);
                
                if (minPrice == maxPrice)
                {
                    // If all prices are the same, return single level
                    var volumeProfile = new Dictionary<decimal, long> { { minPrice, priceVolumeData.Sum(x => x.Volume) } };
                    _cache.Set(cacheKey, volumeProfile, TimeSpan.FromSeconds(60));
                    LogMethodExit();
                    return TradingResult<Dictionary<decimal, long>>.Success(volumeProfile);
                }

                var priceStep = (maxPrice - minPrice) / priceLevels;
                var profile = new Dictionary<decimal, long>();

                // Initialize all price levels with zero volume
                for (int i = 0; i < priceLevels; i++)
                {
                    var priceLevel = minPrice + (i * priceStep);
                    profile[Math.Round(priceLevel, 2)] = 0;
                }

                // Distribute volume across price levels
                foreach (var (price, volume) in priceVolumeData)
                {
                    // Find which price level this trade belongs to
                    var levelIndex = (int)Math.Floor((price - minPrice) / priceStep);
                    
                    // Handle edge case where price equals maxPrice
                    if (levelIndex >= priceLevels)
                    {
                        levelIndex = priceLevels - 1;
                    }
                    
                    var priceLevel = Math.Round(minPrice + (levelIndex * priceStep), 2);
                    profile[priceLevel] += volume;
                }

                // Remove price levels with zero volume for cleaner output
                var filteredProfile = profile.Where(kvp => kvp.Value > 0)
                                           .OrderBy(kvp => kvp.Key)
                                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Cache result for 60 seconds
                _cache.Set(cacheKey, filteredProfile, TimeSpan.FromSeconds(60));

                LogInfo($"Calculated Volume Profile for {symbol}: {filteredProfile.Count} price levels with volume");
                LogMethodExit();
                return TradingResult<Dictionary<decimal, long>>.Success(filteredProfile);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate Volume Profile for {symbol}", ex);
            LogMethodExit();
            return TradingResult<Dictionary<decimal, long>>.Failure("CALCULATION_ERROR", "Failed to calculate Volume Profile", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<decimal>> CalculateVWAPAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var cacheKey = $"VWAP_{symbol}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                LogMethodExit();
                return TradingResult<decimal>.Success(cachedValue);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_priceVolumeHistory.TryGetValue(symbol, out var priceVolumeData) || priceVolumeData.Count == 0)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("INSUFFICIENT_DATA", "Not enough price/volume data for VWAP calculation");
                }

                // VWAP calculation:
                // VWAP = Sum(Price * Volume) / Sum(Volume)
                // This gives the average price weighted by volume
                
                decimal totalValueTraded = 0m;
                long totalVolume = 0;

                foreach (var (price, volume) in priceVolumeData)
                {
                    totalValueTraded += price * volume;
                    totalVolume += volume;
                }

                if (totalVolume == 0)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure("NO_VOLUME", "No volume data available for VWAP calculation");
                }

                var vwap = totalValueTraded / totalVolume;

                // Cache result for 60 seconds
                _cache.Set(cacheKey, vwap, TimeSpan.FromSeconds(60));

                LogInfo($"Calculated VWAP for {symbol}: {vwap:F2}");
                LogMethodExit();
                return TradingResult<decimal>.Success(vwap);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate VWAP for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", "Failed to calculate VWAP", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<(decimal Tenkan, decimal Kijun, decimal SpanA, decimal SpanB, decimal Chikou)>> CalculateIchimokuAsync(
        string symbol, 
        int tenkanPeriod = 9, 
        int kijunPeriod = 26, 
        int spanBPeriod = 52, 
        int displacement = 26,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            if (tenkanPeriod <= 0 || kijunPeriod <= 0 || spanBPeriod <= 0 || displacement < 0)
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Failure("INVALID_PERIODS", "All periods must be positive and displacement non-negative");
            }

            var cacheKey = $"ICHIMOKU_{symbol}_{tenkanPeriod}_{kijunPeriod}_{spanBPeriod}_{displacement}";
            if (_cache.TryGetValue(cacheKey, out (decimal Tenkan, decimal Kijun, decimal SpanA, decimal SpanB, decimal Chikou)? cachedValue) && cachedValue != null)
            {
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Success(cachedValue.Value);
            }

            await _calculationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Need sufficient OHLC data for the largest period + displacement
                var requiredBars = Math.Max(Math.Max(tenkanPeriod, kijunPeriod), spanBPeriod) + displacement;
                
                if (!_ohlcHistory.TryGetValue(symbol, out var ohlcData) || ohlcData.Count < requiredBars)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Failure("INSUFFICIENT_DATA", $"Not enough OHLC data for Ichimoku calculation. Need {requiredBars} bars, have {ohlcData?.Count ?? 0}");
                }

                if (!_priceHistory.TryGetValue(symbol, out var priceData) || priceData.Count < displacement + 1)
                {
                    LogMethodExit();
                    return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Failure("INSUFFICIENT_PRICE_DATA", "Not enough price data for Chikou Span calculation");
                }

                // Industry-standard Ichimoku Cloud calculations
                
                // 1. Tenkan-sen (Conversion Line): (Highest High + Lowest Low) / 2 over tenkanPeriod
                var tenkanData = ohlcData.TakeLast(tenkanPeriod).ToList();
                var tenkanHigh = tenkanData.Max(x => x.High);
                var tenkanLow = tenkanData.Min(x => x.Low);
                var tenkan = (tenkanHigh + tenkanLow) / 2m;

                // 2. Kijun-sen (Base Line): (Highest High + Lowest Low) / 2 over kijunPeriod
                var kijunData = ohlcData.TakeLast(kijunPeriod).ToList();
                var kijunHigh = kijunData.Max(x => x.High);
                var kijunLow = kijunData.Min(x => x.Low);
                var kijun = (kijunHigh + kijunLow) / 2m;

                // 3. Senkou Span A (Leading Span A): (Tenkan + Kijun) / 2, plotted displacement periods ahead
                var spanA = (tenkan + kijun) / 2m;

                // 4. Senkou Span B (Leading Span B): (Highest High + Lowest Low) / 2 over spanBPeriod, plotted displacement periods ahead
                var spanBData = ohlcData.TakeLast(spanBPeriod).ToList();
                var spanBHigh = spanBData.Max(x => x.High);
                var spanBLow = spanBData.Min(x => x.Low);
                var spanB = (spanBHigh + spanBLow) / 2m;

                // 5. Chikou Span (Lagging Span): Current closing price plotted displacement periods back
                var currentClose = priceData.Last();
                var chikou = currentClose; // Note: In visualization, this would be plotted displacement periods back

                var result = (Tenkan: tenkan, Kijun: kijun, SpanA: spanA, SpanB: spanB, Chikou: chikou);

                // Cache result for 60 seconds
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));

                LogInfo($"Calculated Ichimoku for {symbol}: Tenkan={tenkan:F2}, Kijun={kijun:F2}, SpanA={spanA:F2}, SpanB={spanB:F2}, Chikou={chikou:F2}");
                LogMethodExit();
                return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Success(result);
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate Ichimoku for {symbol}", ex);
            LogMethodExit();
            return TradingResult<(decimal, decimal, decimal, decimal, decimal)>.Failure("CALCULATION_ERROR", "Failed to calculate Ichimoku", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<bool>> UpdateIndicatorsAsync(MarketQuote quote, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (quote == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_QUOTE", "Quote cannot be null");
            }

            // Add quote to processing channel
            await _quoteChannel.Writer.WriteAsync(quote, cancellationToken).ConfigureAwait(false);

            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Failed to update indicators for quote {quote?.Symbol}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("UPDATE_ERROR", "Failed to update indicators", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TradingResult<Dictionary<string, decimal>>> GetAllIndicatorsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return TradingResult<Dictionary<string, decimal>>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var indicators = new Dictionary<string, decimal>();

            // Get common indicators
            var rsiTask = CalculateRSIAsync(symbol, 14, cancellationToken);
            var sma20Task = CalculateSMAAsync(symbol, 20, cancellationToken);
            var ema12Task = CalculateEMAAsync(symbol, 12, cancellationToken);
            var atrTask = CalculateATRAsync(symbol, 14, cancellationToken);
            var stochTask = CalculateStochasticAsync(symbol, 14, 3, cancellationToken);
            var obvTask = CalculateOBVAsync(symbol, cancellationToken);
            var vwapTask = CalculateVWAPAsync(symbol, cancellationToken);
            var ichimokuTask = CalculateIchimokuAsync(symbol, 9, 26, 52, 26, cancellationToken);

            await Task.WhenAll(rsiTask, sma20Task, ema12Task, atrTask, stochTask, obvTask, vwapTask, ichimokuTask).ConfigureAwait(false);

            if (rsiTask.Result.IsSuccess) indicators["RSI_14"] = rsiTask.Result.Value!;
            if (sma20Task.Result.IsSuccess) indicators["SMA_20"] = sma20Task.Result.Value!;
            if (ema12Task.Result.IsSuccess) indicators["EMA_12"] = ema12Task.Result.Value!;
            if (atrTask.Result.IsSuccess) indicators["ATR_14"] = atrTask.Result.Value!;
            if (stochTask.Result.IsSuccess) 
            {
                indicators["STOCH_K_14"] = stochTask.Result.Value!.K;
                indicators["STOCH_D_3"] = stochTask.Result.Value!.D;
            }
            if (obvTask.Result.IsSuccess) indicators["OBV"] = obvTask.Result.Value!;
            if (vwapTask.Result.IsSuccess) indicators["VWAP"] = vwapTask.Result.Value!;
            if (ichimokuTask.Result.IsSuccess) 
            {
                var ichimoku = ichimokuTask.Result.Value!;
                indicators["TENKAN_9"] = ichimoku.Tenkan;
                indicators["KIJUN_26"] = ichimoku.Kijun;
                indicators["SPAN_A"] = ichimoku.SpanA;
                indicators["SPAN_B"] = ichimoku.SpanB;
                indicators["CHIKOU"] = ichimoku.Chikou;
            }

            LogMethodExit();
            return TradingResult<Dictionary<string, decimal>>.Success(indicators);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get all indicators for {symbol}", ex);
            LogMethodExit();
            return TradingResult<Dictionary<string, decimal>>.Failure("GET_INDICATORS_ERROR", "Failed to get indicators", ex);
        }
    }

    /// <inheritdoc />
    public Task<TradingResult<bool>> SubscribeToIndicatorsAsync(string symbol, List<string> indicators, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return Task.FromResult(TradingResult<bool>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty"));
            }

            // Initialize price history for symbol if not exists
            _priceHistory.TryAdd(symbol, new List<decimal>());
            _ohlcHistory.TryAdd(symbol, new List<(decimal, decimal, decimal)>());
            _priceVolumeHistory.TryAdd(symbol, new List<(decimal, long)>());

            LogInfo($"Subscribed to indicators for {symbol}: {string.Join(", ", indicators)}");
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            LogError($"Failed to subscribe to indicators for {symbol}", ex);
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Failure("SUBSCRIBE_ERROR", "Failed to subscribe to indicators", ex));
        }
    }

    /// <inheritdoc />
    public Task<TradingResult<bool>> UnsubscribeFromIndicatorsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();

        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                LogMethodExit();
                return Task.FromResult(TradingResult<bool>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty"));
            }

            // Remove from tracking
            _priceHistory.TryRemove(symbol, out _);
            _ohlcHistory.TryRemove(symbol, out _);
            _indicators.TryRemove(symbol, out _);

            LogInfo($"Unsubscribed from indicators for {symbol}");
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            LogError($"Failed to unsubscribe from indicators for {symbol}", ex);
            LogMethodExit();
            return Task.FromResult(TradingResult<bool>.Failure("UNSUBSCRIBE_ERROR", "Failed to unsubscribe from indicators", ex));
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Processes incoming market quotes asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessQuotesAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            await foreach (var quote in _quoteChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await ProcessSingleQuoteAsync(quote, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            LogInfo("Quote processing was cancelled");
        }
        catch (Exception ex)
        {
            LogError("Error in quote processing loop", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Processes a single market quote.
    /// </summary>
    /// <param name="quote">The market quote to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private Task ProcessSingleQuoteAsync(MarketQuote quote, CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            // Update price history
            if (_priceHistory.TryGetValue(quote.Symbol, out var prices))
            {
                prices.Add(quote.CurrentPrice);
                
                // Keep only last 1000 prices to manage memory
                if (prices.Count > 1000)
                {
                    prices.RemoveAt(0);
                }
            }

            // Update OHLC history for Stochastic and other indicators
            if (_ohlcHistory.TryGetValue(quote.Symbol, out var ohlcData))
            {
                ohlcData.Add((quote.DayHigh, quote.DayLow, quote.CurrentPrice));
                
                // Keep only last 1000 OHLC bars to manage memory
                if (ohlcData.Count > 1000)
                {
                    ohlcData.RemoveAt(0);
                }
            }

            // Update price/volume history for OBV and other volume indicators
            if (_priceVolumeHistory.TryGetValue(quote.Symbol, out var priceVolumeData))
            {
                priceVolumeData.Add((quote.CurrentPrice, quote.Volume));
                
                // Keep only last 1000 price/volume bars to manage memory
                if (priceVolumeData.Count > 1000)
                {
                    priceVolumeData.RemoveAt(0);
                }
            }

            // Invalidate cache for this symbol
            var keysToRemove = new List<string>();
            if (_cache is MemoryCache memoryCache)
            {
                // Note: MemoryCache doesn't expose a way to enumerate keys
                // In production, consider using a different cache implementation
                // or maintaining a separate keys collection
            }

            LogInfo($"Processed quote for {quote.Symbol}: {quote.CurrentPrice:F2}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to process quote for {quote.Symbol}", ex);
        }
        finally
        {
            LogMethodExit();
        }
        
        return Task.CompletedTask;
    }

    #endregion

    #region IDisposable Implementation

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            LogMethodEntry();

            try
            {
                _processingTokenSource?.Cancel();
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
                
                _quoteChannel?.Writer.Complete();
                _calculationSemaphore?.Dispose();
                _processingTokenSource?.Dispose();
                
                LogInfo("TechnicalAnalysisService disposed successfully");
            }
            catch (Exception ex)
            {
                LogError("Error during TechnicalAnalysisService disposal", ex);
            }
            finally
            {
                LogMethodExit();
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}