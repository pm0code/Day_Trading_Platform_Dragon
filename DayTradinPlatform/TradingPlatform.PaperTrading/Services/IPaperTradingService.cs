using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services;

public interface IPaperTradingService
{
    Task<OrderResult> SubmitOrderAsync(OrderRequest orderRequest);
    Task<Order?> GetOrderAsync(string orderId);
    Task<IEnumerable<Order>> GetOrdersAsync();
    Task<OrderResult> CancelOrderAsync(string orderId);
    Task<OrderResult> ModifyOrderAsync(string orderId, OrderRequest modifiedOrder);
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
    Task<ExecutionAnalytics> GetExecutionAnalyticsAsync();
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