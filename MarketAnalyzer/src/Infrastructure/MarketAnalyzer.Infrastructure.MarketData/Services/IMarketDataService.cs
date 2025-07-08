using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;

namespace MarketAnalyzer.Infrastructure.MarketData.Services;

/// <summary>
/// Defines the contract for market data services.
/// Follows industry-standard service interface patterns.
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Gets real-time quote data for the specified symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol (e.g., "AAPL")</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the market quote</returns>
    Task<TradingResult<MarketQuote>> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets real-time quote data for multiple symbols.
    /// </summary>
    /// <param name="symbols">The stock symbols</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the market quotes</returns>
    Task<TradingResult<IEnumerable<MarketQuote>>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets company profile information for the specified symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the stock information</returns>
    Task<TradingResult<Stock>> GetCompanyProfileAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical price data for the specified symbol.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="fromDate">The start date</param>
    /// <param name="toDate">The end date</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing historical quotes</returns>
    Task<TradingResult<IEnumerable<MarketQuote>>> GetHistoricalDataAsync(string symbol, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for stocks by company name or symbol.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing matching stocks</returns>
    Task<TradingResult<IEnumerable<Stock>>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time price updates for the specified symbols.
    /// Uses WebSocket for efficient real-time streaming.
    /// </summary>
    /// <param name="symbols">The symbols to subscribe to</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An async enumerable of real-time quotes</returns>
    IAsyncEnumerable<MarketQuote> StreamQuotesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current health status of the market data service.
    /// </summary>
    /// <returns>A trading result containing the health status</returns>
    Task<TradingResult<bool>> GetServiceHealthAsync();
}