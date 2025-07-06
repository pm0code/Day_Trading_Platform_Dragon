using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Defines the contract for data ingestion services with comprehensive error handling and consistent result patterns
    /// Provides methods for retrieving real-time and historical market data with TradingResult wrapper
    /// All implementations must adhere to financial calculation standards and use decimal arithmetic for precision
    /// </summary>
    public interface IDataIngestionService
    {
        /// <summary>
        /// Retrieves real-time market data for the specified symbol with comprehensive error handling
        /// </summary>
        /// <param name="symbol">The symbol to retrieve data for</param>
        /// <returns>A TradingResult containing the MarketData object or error information</returns>
        Task<TradingResult<MarketData?>> GetRealTimeDataAsync(string symbol);
        /// <summary>
        /// Retrieves historical market data for the specified symbol and date range with comprehensive validation
        /// </summary>
        /// <param name="symbol">The symbol to retrieve data for</param>
        /// <param name="startDate">The start date of the historical data range</param>
        /// <param name="endDate">The end date of the historical data range</param>
        /// <returns>A TradingResult containing a list of DailyData objects or error information</returns>
        Task<TradingResult<List<DailyData>>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);
        /// <summary>
        /// Retrieves a batch of real-time market data for the specified symbols with efficient processing
        /// </summary>
        /// <param name="symbols">A list of symbols to retrieve data for</param>
        /// <returns>A TradingResult containing a list of MarketData objects or error information</returns>
        Task<TradingResult<List<MarketData>>> GetBatchRealTimeDataAsync(List<string> symbols);

        /// <summary>
        /// Subscribes to real-time market data updates for the given symbol with intelligent provider failover
        /// </summary>
        /// <param name="symbol">The symbol to subscribe to</param>
        /// <returns>A TradingResult containing an observable stream or error information</returns>
        TradingResult<IObservable<MarketData>> SubscribeRealTimeData(string symbol);
    }
}

// Total Lines: 46
