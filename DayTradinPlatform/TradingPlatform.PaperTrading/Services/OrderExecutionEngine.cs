using TradingPlatform.PaperTrading.Models;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.PaperTrading.Services;

public class OrderExecutionEngine : IOrderExecutionEngine
{
    private readonly IOrderBookSimulator _orderBookSimulator;
    private readonly ISlippageCalculator _slippageCalculator;
    private readonly ITradingLogger _logger;

    public OrderExecutionEngine(
        IOrderBookSimulator orderBookSimulator,
        ISlippageCalculator slippageCalculator,
        ITradingLogger logger)
    {
        _orderBookSimulator = orderBookSimulator;
        _slippageCalculator = slippageCalculator;
        _logger = logger;
    }

    public async Task<Execution?> ExecuteOrderAsync(Order order, decimal marketPrice)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var shouldExecute = await ShouldExecuteOrderAsync(order, marketPrice);
            if (!shouldExecute)
            {
                TradingLogOrchestrator.Instance.LogInfo($"Order {order.OrderId} not executed - conditions not met");
                return null;
            }

            var orderBook = await _orderBookSimulator.GetOrderBookAsync(order.Symbol);
            var executionPrice = await CalculateExecutionPriceAsync(order, orderBook);

            // Calculate slippage
            var expectedPrice = order.Type == OrderType.Market ? marketPrice : (order.LimitPrice ?? marketPrice);
            var slippage = _slippageCalculator.CalculateSlippage(expectedPrice, executionPrice, order.Side);

            // Simulate execution latency (realistic for paper trading)
            var executionLatency = CalculateExecutionLatency(order.Type);
            await Task.Delay(executionLatency);

            // Create execution record
            var execution = new Execution(
                ExecutionId: Guid.NewGuid().ToString(),
                OrderId: order.OrderId,
                Symbol: order.Symbol,
                Side: order.Side,
                Quantity: order.RemainingQuantity,
                Price: executionPrice,
                Commission: CalculateCommission(order.RemainingQuantity, executionPrice),
                ExecutionTime: DateTime.UtcNow,
                VenueId: SelectExecutionVenue(order.Symbol).ToString(),
                Slippage: slippage
            );

            // Update order book to reflect execution
            await _orderBookSimulator.UpdateOrderBookAsync(order.Symbol, execution);

            var elapsed = DateTime.UtcNow - startTime;
            TradingLogOrchestrator.Instance.LogInfo($"Order {order.OrderId} executed: {execution.Quantity}@{execution.Price} with {slippage} slippage in {elapsed.TotalMilliseconds}ms");

            return execution;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error executing order {order.OrderId}", ex);
            return null;
        }
    }

    public async Task<decimal> CalculateExecutionPriceAsync(Order order, OrderBook orderBook)
    {
        try
        {
            return order.Type switch
            {
                OrderType.Market => CalculateMarketExecutionPrice(order, orderBook),
                OrderType.Limit => order.LimitPrice ?? 0m,
                OrderType.Stop => await CalculateStopExecutionPriceAsync(order, orderBook),
                OrderType.StopLimit => await CalculateStopLimitExecutionPriceAsync(order, orderBook),
                OrderType.TrailingStop => await CalculateTrailingStopPriceAsync(order, orderBook),
                _ => throw new NotSupportedException($"Order type {order.Type} not supported")
            };
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error calculating execution price for order {order.OrderId}", ex);
            throw;
        }
    }

    public async Task<MarketImpact> CalculateMarketImpactAsync(Order order, OrderBook orderBook)
    {
        try
        {
            var orderValue = order.Quantity * (order.LimitPrice ?? GetMidPrice(orderBook));
            // var marketCap = 1000000000m; // Simplified - would get from market data (unused for now)
            var adv = 5000000m; // Average daily volume - would get from market data

            // Market impact model based on order size relative to market
            var participationRate = orderValue / adv;
            var temporaryImpact = 0.01m * TradingPlatform.Common.Mathematics.TradingMath.Sqrt(participationRate);
            var permanentImpact = 0.005m * participationRate;
            var priceImpact = temporaryImpact + permanentImpact;

            // Impact duration based on order size
            var impactDuration = TimeSpan.FromMinutes(Math.Min(60, Math.Max(1, (double)participationRate * 100)));

            return await Task.FromResult(new MarketImpact(
                PriceImpact: priceImpact,
                TemporaryImpact: temporaryImpact,
                PermanentImpact: permanentImpact,
                Duration: impactDuration
            ));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error calculating market impact for order {order.OrderId}", ex);
            throw;
        }
    }

    public async Task<bool> ShouldExecuteOrderAsync(Order order, decimal currentPrice)
    {
        try
        {
            return order.Type switch
            {
                OrderType.Market => true,
                OrderType.Limit => CheckLimitOrderExecution(order, currentPrice),
                OrderType.Stop => CheckStopOrderExecution(order, currentPrice),
                OrderType.StopLimit => CheckStopLimitOrderExecution(order, currentPrice),
                OrderType.TrailingStop => await CheckTrailingStopExecutionAsync(order, currentPrice),
                _ => false
            };
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error checking execution conditions for order {order.OrderId}", ex);
            return false;
        }
    }

    private decimal CalculateMarketExecutionPrice(Order order, OrderBook orderBook)
    {
        var levels = order.Side == OrderSide.Buy ? orderBook.Asks : orderBook.Bids;
        var remainingQuantity = order.RemainingQuantity;
        var weightedPrice = 0m;
        var totalQuantity = 0m;

        foreach (var level in levels.OrderBy(l => order.Side == OrderSide.Buy ? l.Price : -l.Price))
        {
            var quantityAtLevel = Math.Min(remainingQuantity, level.Size);
            weightedPrice += level.Price * quantityAtLevel;
            totalQuantity += quantityAtLevel;
            remainingQuantity -= quantityAtLevel;

            if (remainingQuantity <= 0) break;
        }

        return totalQuantity > 0 ? weightedPrice / totalQuantity : GetMidPrice(orderBook);
    }

    private async Task<decimal> CalculateStopExecutionPriceAsync(Order order, OrderBook orderBook)
    {
        // Simplified stop order execution - would use current market price
        return await Task.FromResult(GetMidPrice(orderBook));
    }

    private async Task<decimal> CalculateStopLimitExecutionPriceAsync(Order order, OrderBook orderBook)
    {
        // For stop-limit orders, use limit price once stop is triggered
        return await Task.FromResult(order.LimitPrice ?? GetMidPrice(orderBook));
    }

    private async Task<decimal> CalculateTrailingStopPriceAsync(Order order, OrderBook orderBook)
    {
        // Simplified trailing stop calculation
        return await Task.FromResult(GetMidPrice(orderBook));
    }

    private bool CheckLimitOrderExecution(Order order, decimal currentPrice)
    {
        return order.Side == OrderSide.Buy
            ? currentPrice <= (order.LimitPrice ?? 0m)
            : currentPrice >= (order.LimitPrice ?? decimal.MaxValue);
    }

    private bool CheckStopOrderExecution(Order order, decimal currentPrice)
    {
        return order.Side == OrderSide.Buy
            ? currentPrice >= (order.StopPrice ?? decimal.MaxValue)
            : currentPrice <= (order.StopPrice ?? 0m);
    }

    private bool CheckStopLimitOrderExecution(Order order, decimal currentPrice)
    {
        // Stop condition must be met first
        return CheckStopOrderExecution(order, currentPrice);
    }

    private async Task<bool> CheckTrailingStopExecutionAsync(Order order, decimal currentPrice)
    {
        // Simplified trailing stop logic
        return await Task.FromResult(CheckStopOrderExecution(order, currentPrice));
    }

    private decimal GetMidPrice(OrderBook orderBook)
    {
        var bestBid = orderBook.Bids.OrderByDescending(b => b.Price).FirstOrDefault()?.Price ?? 0m;
        var bestAsk = orderBook.Asks.OrderBy(a => a.Price).FirstOrDefault()?.Price ?? 0m;

        return bestBid > 0 && bestAsk > 0 ? (bestBid + bestAsk) / 2m : Math.Max(bestBid, bestAsk);
    }

    private decimal CalculateCommission(decimal quantity, decimal price)
    {
        // Simplified commission structure
        var notionalValue = quantity * price;
        return Math.Max(1.00m, notionalValue * 0.0005m); // 5 basis points, min $1
    }

    private TimeSpan CalculateExecutionLatency(OrderType orderType)
    {
        // Realistic execution latencies for different order types
        return orderType switch
        {
            OrderType.Market => TimeSpan.FromMicroseconds(50),
            OrderType.Limit => TimeSpan.FromMicroseconds(75),
            OrderType.Stop => TimeSpan.FromMicroseconds(100),
            OrderType.StopLimit => TimeSpan.FromMicroseconds(125),
            OrderType.TrailingStop => TimeSpan.FromMicroseconds(150),
            _ => TimeSpan.FromMicroseconds(100)
        };
    }

    private ExecutionVenue SelectExecutionVenue(string symbol)
    {
        // Simplified venue selection - would use smart order routing
        var venues = new[] { ExecutionVenue.NASDAQ, ExecutionVenue.NYSE, ExecutionVenue.ARCA, ExecutionVenue.IEX };
        var random = new Random();
        return venues[random.Next(venues.Length)];
    }
}