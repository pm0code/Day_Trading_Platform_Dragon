using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.PaperTrading.Services;

/// <summary>
/// Paper trading service interface for risk-free trading simulation
/// All operations use TradingResult pattern for consistent error handling
/// </summary>
public interface IPaperTradingService
{
    /// <summary>
    /// Submits a new paper trading order
    /// </summary>
    Task<TradingResult<OrderResult>> SubmitOrderAsync(OrderRequest orderRequest);
    
    /// <summary>
    /// Retrieves a specific order by ID
    /// </summary>
    Task<TradingResult<Order?>> GetOrderAsync(string orderId);
    
    /// <summary>
    /// Retrieves all orders
    /// </summary>
    Task<TradingResult<IEnumerable<Order>>> GetOrdersAsync();
    
    /// <summary>
    /// Cancels an existing order
    /// </summary>
    Task<TradingResult<OrderResult>> CancelOrderAsync(string orderId);
    
    /// <summary>
    /// Modifies an existing order
    /// </summary>
    Task<TradingResult<OrderResult>> ModifyOrderAsync(string orderId, OrderRequest modifiedOrder);
    
    /// <summary>
    /// Gets paper trading service metrics
    /// </summary>
    Task<TradingResult<PaperTradingMetrics>> GetMetricsAsync();
}

public interface IOrderExecutionEngine
{
    Task<Execution?> ExecuteOrderAsync(Order order, decimal marketPrice);
    Task<decimal> CalculateExecutionPriceAsync(Order order, OrderBook orderBook);
    Task<MarketImpact> CalculateMarketImpactAsync(Order order, OrderBook orderBook);
    Task<bool> ShouldExecuteOrderAsync(Order order, decimal currentPrice);
}

public interface IPortfolioManager
{
    Task<Portfolio> GetPortfolioAsync();
    Task<IEnumerable<Position>> GetPositionsAsync();
    Task<Position?> GetPositionAsync(string symbol);
    Task UpdatePositionAsync(string symbol, Execution execution);
    Task<decimal> GetBuyingPowerAsync();
    Task<bool> HasSufficientBuyingPowerAsync(OrderRequest orderRequest, decimal estimatedPrice);
}

public interface IOrderBookSimulator
{
    Task<OrderBook> GetOrderBookAsync(string symbol);
    Task<decimal> GetCurrentPriceAsync(string symbol);
    Task<decimal> CalculateSlippageAsync(string symbol, OrderSide side, decimal quantity);
    Task UpdateOrderBookAsync(string symbol, Execution execution);
}

public interface IExecutionAnalytics
{
    Task<PerformanceMetrics> GetPerformanceMetricsAsync();
    Task<Models.ExecutionAnalytics> GetExecutionAnalyticsAsync();
    Task<IEnumerable<Execution>> GetExecutionHistoryAsync();
    Task<IEnumerable<Execution>> GetExecutionsBySymbolAsync(string symbol);
    Task RecordExecutionAsync(Execution execution);
}

public interface ISlippageCalculator
{
    decimal CalculateSlippage(decimal requestedPrice, decimal executedPrice, OrderSide side);
    Task<decimal> EstimateSlippageAsync(string symbol, OrderSide side, decimal quantity);
    Task<decimal> CalculateMarketImpactAsync(string symbol, decimal quantity, TimeSpan duration);
}