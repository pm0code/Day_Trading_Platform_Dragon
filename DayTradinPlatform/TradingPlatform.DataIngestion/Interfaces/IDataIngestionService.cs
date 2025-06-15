// File: TradingPlatform.DataIngestion.Interfaces\IDataIngestionService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Defines the contract for data ingestion services, providing methods for retrieving
    /// real-time and historical market data.  All implementations must adhere to
    /// FinancialCalculationStandards.md and use decimal arithmetic for financial values.
    /// </summary>
    public interface IDataIngestionService
    {
        /// <summary>
        /// Retrieves real-time market data for the specified symbol.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve data for.</param>
        /// <returns>A Task containing the MarketData object, or null if data is unavailable.</returns>
        Task<MarketData> GetRealTimeDataAsync(string symbol);


        /// <summary>
        /// Retrieves historical market data for the specified symbol and date range.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve data for.</param>
        /// <param name="startDate">The start date of the historical data range.</param>
        /// <param name="endDate">The end date of the historical data range.</param>
        /// <returns>A Task containing a list of DailyData objects, or an empty list if data is unavailable.</returns>
        Task<List<DailyData>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);


        /// <summary>
        /// Retrieves a batch of real-time market data for the specified symbols.
        /// </summary>
        /// <param name="symbols">A list of symbols to retrieve data for.</param>
        /// <returns>A Task containing a list of MarketData objects. Symbols with unavailable data will have null entries.</returns>
        Task<List<MarketData>> GetBatchRealTimeDataAsync(List<string> symbols);

        /// <summary>
        /// Subscribes to real-time market data updates for the given symbol.
        /// </summary>
        /// <param name="symbol">The symbol to subscribe to.</param>
        /// <returns>An IObservable that streams MarketData updates.</returns>
        IObservable<MarketData> SubscribeRealTimeData(string symbol);
    }
}

// Total Lines: 46
