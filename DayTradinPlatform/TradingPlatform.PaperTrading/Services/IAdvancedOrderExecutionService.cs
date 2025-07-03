using System;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Interface for advanced order execution service.
    /// FULLY COMPLIANT with TradingResult<T> pattern and progress reporting.
    /// </summary>
    public interface IAdvancedOrderExecutionService
    {
        /// <summary>
        /// Submit a TWAP order with optional progress reporting
        /// </summary>
        Task<TradingResult<AdvancedOrderResult>> SubmitTwapOrderAsync(
            TwapOrder order, 
            IProgress<OrderExecutionProgress>? progress = null);
        
        /// <summary>
        /// Submit a VWAP order with optional progress reporting
        /// </summary>
        Task<TradingResult<AdvancedOrderResult>> SubmitVwapOrderAsync(
            VwapOrder order,
            IProgress<OrderExecutionProgress>? progress = null);
        
        /// <summary>
        /// Submit an Iceberg order with optional progress reporting
        /// </summary>
        Task<TradingResult<AdvancedOrderResult>> SubmitIcebergOrderAsync(
            IcebergOrder order,
            IProgress<OrderExecutionProgress>? progress = null);
        
        /// <summary>
        /// Get the status of an advanced order
        /// </summary>
        Task<TradingResult<AdvancedOrderStatus>> GetOrderStatusAsync(string orderId);
        
        /// <summary>
        /// Cancel an advanced order
        /// </summary>
        Task<TradingResult<bool>> CancelOrderAsync(string orderId);
    }

    /// <summary>
    /// Extended IOrderExecutionEngine interface with TradingResult<T> pattern
    /// </summary>
    public interface IOrderExecutionEngine
    {
        // Original methods
        Task<Execution?> ExecuteOrderAsync(Order order, decimal marketPrice);
        Task<decimal> CalculateExecutionPriceAsync(Order order, OrderBook orderBook);
        Task<MarketImpact> CalculateMarketImpactAsync(Order order, OrderBook orderBook);
        Task<bool> ShouldExecuteOrderAsync(Order order, decimal currentPrice);
        
        // Extended methods with TradingResult<T> pattern
        Task<TradingResult<OrderResult>> SubmitOrderAsync(OrderRequest orderRequest);
        Task<TradingResult<OrderStatus>> GetOrderStatusAsync(string orderId);
        Task<TradingResult<Order>> GetOrderAsync(string orderId);
        Task<TradingResult<bool>> CancelOrderAsync(string orderId);
        Task<TradingResult<Execution>> GetExecutionDetailsAsync(string orderId);
    }

    /// <summary>
    /// IMarketDataService with TradingResult<T> pattern
    /// </summary>
    public interface IMarketDataService
    {
        Task<TradingResult<decimal>> GetCurrentPriceAsync(string symbol);
        Task<TradingResult<decimal>> GetCurrentVolumeAsync(string symbol);
        Task<TradingResult<decimal>> GetVwapAsync(string symbol);
        Task<TradingResult<decimal>> GetTwapAsync(string symbol, DateTime startTime, DateTime endTime);
        Task<TradingResult<(decimal bid, decimal ask)>> GetBidAskAsync(string symbol);
        Task<TradingResult<OrderBook>> GetMarketDepthAsync(string symbol, int levels = 5);
        Task<TradingResult> SubscribeToMarketDataAsync(string symbol, Action<MarketDataUpdate> callback);
        Task<TradingResult> UnsubscribeFromMarketDataAsync(string symbol);
    }

    /// <summary>
    /// IVolumeAnalysisService with TradingResult<T> pattern
    /// </summary>
    public interface IVolumeAnalysisService
    {
        Task<TradingResult<List<VolumeProfile>>> GetHistoricalVolumeProfileAsync(string symbol, int days);
        Task<TradingResult<List<VolumeProfile>>> GetIntradayVolumeProfileAsync(string symbol);
    }
}