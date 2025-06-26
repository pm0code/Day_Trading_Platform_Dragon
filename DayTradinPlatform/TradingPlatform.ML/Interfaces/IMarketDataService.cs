using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;

namespace TradingPlatform.Core.Interfaces
{
    /// <summary>
    /// Interface for market data service
    /// Placeholder interface for SARI components
    /// </summary>
    public interface IMarketDataService
    {
        Task<TradingResult<MarketData>> GetLatestMarketDataAsync(string symbol, CancellationToken cancellationToken = default);
        Task<TradingResult<List<MarketData>>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<TradingResult<Dictionary<string, MarketData>>> GetBulkMarketDataAsync(List<string> symbols, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Basic market data structure
    /// </summary>
    public class MarketData
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal Price { get; set; }
        public long Volume { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
    }
}