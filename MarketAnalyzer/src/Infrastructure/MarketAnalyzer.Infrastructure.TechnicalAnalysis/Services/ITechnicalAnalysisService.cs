using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;

namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Services;

/// <summary>
/// Interface for technical analysis service providing real-time indicator calculations.
/// </summary>
public interface ITechnicalAnalysisService : IDisposable
{
    /// <summary>
    /// Calculates RSI for a symbol with real-time updates.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="period">RSI period (default: 14)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RSI value (0-100)</returns>
    Task<TradingResult<decimal>> CalculateRSIAsync(string symbol, int period = 14, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Simple Moving Average for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="period">SMA period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SMA value</returns>
    Task<TradingResult<decimal>> CalculateSMAAsync(string symbol, int period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Exponential Moving Average for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="period">EMA period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>EMA value</returns>
    Task<TradingResult<decimal>> CalculateEMAAsync(string symbol, int period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Bollinger Bands for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="period">Period for calculation (default: 20)</param>
    /// <param name="standardDeviations">Standard deviations for bands (default: 2.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bollinger bands (upper, middle, lower)</returns>
    Task<TradingResult<(decimal Upper, decimal Middle, decimal Lower)>> CalculateBollingerBandsAsync(
        string symbol, 
        int period = 20, 
        decimal standardDeviations = 2.0m, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates MACD for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="fastPeriod">Fast EMA period (default: 12)</param>
    /// <param name="slowPeriod">Slow EMA period (default: 26)</param>
    /// <param name="signalPeriod">Signal line EMA period (default: 9)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MACD values (MACD line, Signal line, Histogram)</returns>
    Task<TradingResult<(decimal MACD, decimal Signal, decimal Histogram)>> CalculateMACDAsync(
        string symbol, 
        int fastPeriod = 12, 
        int slowPeriod = 26, 
        int signalPeriod = 9, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Average True Range for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="period">ATR period (default: 14)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ATR value</returns>
    Task<TradingResult<decimal>> CalculateATRAsync(string symbol, int period = 14, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Stochastic Oscillator for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="kPeriod">%K period (default: 14)</param>
    /// <param name="dPeriod">%D period (default: 3)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stochastic values (%K and %D)</returns>
    Task<TradingResult<(decimal K, decimal D)>> CalculateStochasticAsync(
        string symbol, 
        int kPeriod = 14, 
        int dPeriod = 3, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates On-Balance Volume for a symbol.
    /// OBV is a momentum indicator that uses volume flow to predict changes in stock price.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OBV value (cumulative volume indicator)</returns>
    Task<TradingResult<decimal>> CalculateOBVAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Volume Profile for a symbol.
    /// Volume Profile shows the volume traded at each price level over a specified period.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="priceLevels">Number of price levels to divide the range into (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping price levels to volume traded at those levels</returns>
    Task<TradingResult<Dictionary<decimal, long>>> CalculateVolumeProfileAsync(
        string symbol, 
        int priceLevels = 20, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Volume Weighted Average Price (VWAP) for a symbol.
    /// VWAP gives the average price weighted by volume, indicating the true average price.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>VWAP value</returns>
    Task<TradingResult<decimal>> CalculateVWAPAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Ichimoku Cloud for a symbol.
    /// Returns all 5 components: Tenkan-sen (Conversion), Kijun-sen (Base), Senkou Span A (Leading A), Senkou Span B (Leading B), Chikou Span (Lagging).
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="tenkanPeriod">Tenkan-sen period (default: 9)</param>
    /// <param name="kijunPeriod">Kijun-sen period (default: 26)</param>
    /// <param name="spanBPeriod">Senkou Span B period (default: 52)</param>
    /// <param name="displacement">Displacement for Senkou spans and Chikou (default: 26)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ichimoku Cloud components (Tenkan, Kijun, SpanA, SpanB, Chikou)</returns>
    Task<TradingResult<(decimal Tenkan, decimal Kijun, decimal SpanA, decimal SpanB, decimal Chikou)>> CalculateIchimokuAsync(
        string symbol, 
        int tenkanPeriod = 9, 
        int kijunPeriod = 26, 
        int spanBPeriod = 52, 
        int displacement = 26,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates indicators with a new market quote.
    /// </summary>
    /// <param name="quote">The market quote to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<TradingResult<bool>> UpdateIndicatorsAsync(MarketQuote quote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available indicators for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of indicator name and value</returns>
    Task<TradingResult<Dictionary<string, decimal>>> GetAllIndicatorsAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time indicator updates for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="indicators">List of indicators to track</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<TradingResult<bool>> SubscribeToIndicatorsAsync(string symbol, List<string> indicators, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from indicator updates for a symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<TradingResult<bool>> UnsubscribeFromIndicatorsAsync(string symbol, CancellationToken cancellationToken = default);
}