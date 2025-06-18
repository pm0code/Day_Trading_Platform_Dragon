using TradingPlatform.PaperTrading.Models;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.PaperTrading.Services;

public class OrderBookSimulator : IOrderBookSimulator
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, OrderBook> _orderBooks = new();
    private readonly ConcurrentDictionary<string, decimal> _currentPrices = new();
    private readonly Random _random = new();

    public OrderBookSimulator(ILogger logger)
    {
        _logger = logger;
        
        // Initialize with some sample data
        InitializeSampleOrderBooks();
    }

    public async Task<OrderBook> GetOrderBookAsync(string symbol)
    {
        try
        {
            if (!_orderBooks.TryGetValue(symbol, out var orderBook))
            {
                // Generate synthetic order book for unknown symbols
                orderBook = GenerateSyntheticOrderBook(symbol);
                _orderBooks.TryAdd(symbol, orderBook);
            }
            
            // Update with market movement simulation
            var updatedOrderBook = SimulateMarketMovement(orderBook);
            _orderBooks.TryUpdate(symbol, updatedOrderBook, orderBook);
            
            return await Task.FromResult(updatedOrderBook);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Error getting order book for {Symbol}", symbol);
            throw;
        }
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol)
    {
        try
        {
            if (_currentPrices.TryGetValue(symbol, out var price))
            {
                // Add some random price movement
                var changePercent = (_random.NextDouble() - 0.5) * 0.02; // +/- 1% movement
                var newPrice = price * (1 + (decimal)changePercent);
                
                _currentPrices.TryUpdate(symbol, newPrice, price);
                return await Task.FromResult(newPrice);
            }
            
            // Generate initial price for new symbols
            var initialPrice = GenerateInitialPrice(symbol);
            _currentPrices.TryAdd(symbol, initialPrice);
            return await Task.FromResult(initialPrice);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Error getting current price for {Symbol}", symbol);
            throw;
        }
    }

    public async Task<decimal> CalculateSlippageAsync(string symbol, OrderSide side, decimal quantity)
    {
        try
        {
            var orderBook = await GetOrderBookAsync(symbol);
            var levels = side == OrderSide.Buy ? orderBook.Asks : orderBook.Bids;
            
            var remainingQuantity = quantity;
            var totalCost = 0m;
            var midPrice = GetMidPrice(orderBook);
            
            foreach (var level in levels.OrderBy(l => side == OrderSide.Buy ? l.Price : -l.Price))
            {
                var quantityAtLevel = Math.Min(remainingQuantity, level.Size);
                totalCost += level.Price * quantityAtLevel;
                remainingQuantity -= quantityAtLevel;
                
                if (remainingQuantity <= 0) break;
            }
            
            if (remainingQuantity > 0)
            {
                // Order would walk through entire book - high slippage
                return quantity > 0 ? 0.05m : 0m; // 5% slippage for large orders
            }
            
            var avgExecutionPrice = totalCost / quantity;
            var slippage = Math.Abs(avgExecutionPrice - midPrice) / midPrice;
            
            return await Task.FromResult(slippage);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Error calculating slippage for {Symbol}", symbol);
            return 0.001m; // Default 10 basis points slippage
        }
    }

    public async Task UpdateOrderBookAsync(string symbol, Execution execution)
    {
        try
        {
            if (!_orderBooks.TryGetValue(symbol, out var orderBook))
                return;
            
            // Simulate order book impact
            var updatedOrderBook = SimulateExecutionImpact(orderBook, execution);
            _orderBooks.TryUpdate(symbol, updatedOrderBook, orderBook);
            
            // Update current price
            _currentPrices.TryUpdate(symbol, execution.Price, _currentPrices.GetValueOrDefault(symbol, execution.Price));
            
            TradingLogOrchestrator.Instance.LogInfo("Updated order book for {Symbol} after execution of {Quantity}@{Price}", 
                symbol, execution.Quantity, execution.Price);
                
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Error updating order book for {Symbol}", symbol);
        }
    }

    private void InitializeSampleOrderBooks()
    {
        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "SPY", "QQQ" };
        var basePrices = new[] { 175.50m, 335.25m, 2750.00m, 145.75m, 245.30m, 425.60m, 445.20m, 385.40m };
        
        for (int i = 0; i < symbols.Length; i++)
        {
            var symbol = symbols[i];
            var basePrice = basePrices[i];
            
            _currentPrices.TryAdd(symbol, basePrice);
            
            var orderBook = new OrderBook(
                Symbol: symbol,
                Bids: GenerateOrderBookSide(basePrice, OrderSide.Buy),
                Asks: GenerateOrderBookSide(basePrice, OrderSide.Sell),
                Timestamp: DateTime.UtcNow
            );
            
            _orderBooks.TryAdd(symbol, orderBook);
        }
    }

    private OrderBook GenerateSyntheticOrderBook(string symbol)
    {
        var basePrice = GenerateInitialPrice(symbol);
        
        return new OrderBook(
            Symbol: symbol,
            Bids: GenerateOrderBookSide(basePrice, OrderSide.Buy),
            Asks: GenerateOrderBookSide(basePrice, OrderSide.Sell),
            Timestamp: DateTime.UtcNow
        );
    }

    private IEnumerable<OrderBookLevel> GenerateOrderBookSide(decimal basePrice, OrderSide side)
    {
        var levels = new List<OrderBookLevel>();
        var spread = basePrice * 0.001m; // 10 basis points spread
        
        for (int i = 0; i < 10; i++)
        {
            var priceOffset = spread * (i + 1) * (side == OrderSide.Buy ? -1 : 1);
            var price = Math.Round(basePrice + priceOffset, 2);
            var size = (decimal)(_random.Next(100, 5000));
            var orderCount = _random.Next(1, 10);
            
            levels.Add(new OrderBookLevel(price, size, orderCount));
        }
        
        return levels;
    }

    private decimal GenerateInitialPrice(string symbol)
    {
        // Generate realistic stock prices based on symbol
        var hash = symbol.GetHashCode();
        var priceRange = Math.Abs(hash % 500) + 50; // Between $50-$550
        return (decimal)priceRange + (_random.Next(0, 100) / 100m);
    }

    private OrderBook SimulateMarketMovement(OrderBook orderBook)
    {
        var movementPercent = (_random.NextDouble() - 0.5) * 0.005; // +/- 0.25% movement
        var movementFactor = 1 + (decimal)movementPercent;
        
        var newBids = orderBook.Bids.Select(b => b with { Price = Math.Round(b.Price * movementFactor, 2) });
        var newAsks = orderBook.Asks.Select(a => a with { Price = Math.Round(a.Price * movementFactor, 2) });
        
        return orderBook with
        {
            Bids = newBids,
            Asks = newAsks,
            Timestamp = DateTime.UtcNow
        };
    }

    private OrderBook SimulateExecutionImpact(OrderBook orderBook, Execution execution)
    {
        // Simplified market impact simulation
        var impactPercent = Math.Min(execution.Quantity / 10000m, 0.01m); // Max 1% impact
        var impactFactor = execution.Side == OrderSide.Buy ? 1 + impactPercent : 1 - impactPercent;
        
        var newBids = orderBook.Bids.Select(b => b with { Price = Math.Round(b.Price * impactFactor, 2) });
        var newAsks = orderBook.Asks.Select(a => a with { Price = Math.Round(a.Price * impactFactor, 2) });
        
        return orderBook with
        {
            Bids = newBids,
            Asks = newAsks,
            Timestamp = DateTime.UtcNow
        };
    }

    private decimal GetMidPrice(OrderBook orderBook)
    {
        var bestBid = orderBook.Bids.OrderByDescending(b => b.Price).FirstOrDefault()?.Price ?? 0m;
        var bestAsk = orderBook.Asks.OrderBy(a => a.Price).FirstOrDefault()?.Price ?? 0m;
        
        return bestBid > 0 && bestAsk > 0 ? (bestBid + bestAsk) / 2m : Math.Max(bestBid, bestAsk);
    }
}