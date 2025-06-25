using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Canonical implementation of the order book simulator for paper trading.
    /// Provides realistic market microstructure simulation with comprehensive monitoring.
    /// </summary>
    public class OrderBookSimulatorCanonical : CanonicalServiceBase, IOrderBookSimulator
    {
        #region Configuration

        protected virtual int DefaultOrderBookDepth => 10;
        protected virtual decimal DefaultSpreadBasisPoints => 10m;
        protected virtual decimal MaxPriceMovementPercent => 0.005m;
        protected virtual decimal MaxMarketImpactPercent => 0.01m;
        protected virtual int OrderBookUpdateIntervalMs => 100;

        #endregion

        #region Infrastructure

        private readonly ConcurrentDictionary<string, OrderBook> _orderBooks = new();
        private readonly ConcurrentDictionary<string, decimal> _currentPrices = new();
        private readonly ConcurrentDictionary<string, MarketStats> _marketStats = new();
        private readonly Random _random = new();
        private readonly Timer _marketSimulationTimer;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Constructor

        public OrderBookSimulatorCanonical(
            IServiceProvider serviceProvider,
            ITradingLogger logger)
            : base(logger, "OrderBookSimulator")
        {
            _serviceProvider = serviceProvider;
            _marketSimulationTimer = new Timer(
                SimulateMarketActivity,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        #endregion

        #region IOrderBookSimulator Implementation

        public async Task<OrderBook> GetOrderBookAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                if (!_orderBooks.TryGetValue(symbol, out var orderBook))
                {
                    // Generate synthetic order book for unknown symbols
                    orderBook = GenerateSyntheticOrderBook(symbol);
                    _orderBooks.TryAdd(symbol, orderBook);
                    
                    LogInfo($"Generated synthetic order book for new symbol: {symbol}");
                    UpdateMetric("OrderBooks.Generated", 1);
                }

                // Update with market movement simulation
                var updatedOrderBook = SimulateMarketMovement(orderBook);
                _orderBooks.TryUpdate(symbol, updatedOrderBook, orderBook);

                UpdateMetric($"OrderBooks.{symbol}.Accesses", 1);
                UpdateMetric("OrderBooks.TotalAccesses", 1);

                return await Task.FromResult(updatedOrderBook);

            }, $"Get order book for {symbol}",
               "Failed to retrieve order book",
               "Check symbol validity and market data availability");
        }

        public async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                if (_currentPrices.TryGetValue(symbol, out var price))
                {
                    // Add realistic price movement
                    var stats = GetOrCreateMarketStats(symbol);
                    var volatility = stats.Volatility;
                    var trend = stats.Trend;
                    
                    // Price movement based on volatility and trend
                    var randomComponent = (_random.NextDouble() - 0.5) * 2;
                    var movementPercent = (randomComponent * volatility + trend) * MaxPriceMovementPercent;
                    var newPrice = Math.Round(price * (1 + (decimal)movementPercent), 2);

                    // Ensure price stays positive
                    newPrice = Math.Max(0.01m, newPrice);

                    _currentPrices.TryUpdate(symbol, newPrice, price);
                    UpdatePriceMetrics(symbol, newPrice, price);

                    return await Task.FromResult(newPrice);
                }

                // Generate initial price for new symbols
                var initialPrice = GenerateInitialPrice(symbol);
                _currentPrices.TryAdd(symbol, initialPrice);
                
                LogInfo($"Generated initial price for {symbol}: ${initialPrice:F2}");
                UpdateMetric($"Prices.{symbol}.Initial", initialPrice);

                return await Task.FromResult(initialPrice);

            }, $"Get current price for {symbol}",
               "Failed to retrieve current price",
               "Check symbol validity");
        }

        public async Task<decimal> CalculateSlippageAsync(string symbol, OrderSide side, decimal quantity)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");
                ValidateParameter(quantity, nameof(quantity), q => q > 0, "Quantity must be positive");

                var orderBook = await GetOrderBookAsync(symbol);
                var levels = side == OrderSide.Buy ? orderBook.Asks : orderBook.Bids;

                var remainingQuantity = quantity;
                var totalCost = 0m;
                var midPrice = GetMidPrice(orderBook);
                var levelsCrossed = 0;

                foreach (var level in levels.OrderBy(l => side == OrderSide.Buy ? l.Price : -l.Price))
                {
                    var quantityAtLevel = Math.Min(remainingQuantity, level.Size);
                    totalCost += level.Price * quantityAtLevel;
                    remainingQuantity -= quantityAtLevel;
                    levelsCrossed++;

                    if (remainingQuantity <= 0) break;
                }

                decimal slippage;
                if (remainingQuantity > 0)
                {
                    // Order would walk through entire book - high slippage
                    slippage = 0.05m; // 5% slippage for orders exceeding book depth
                    LogWarning($"Order size {quantity} exceeds book depth for {symbol}",
                              impact: "High slippage expected",
                              troubleshooting: "Consider splitting large orders");
                }
                else
                {
                    var avgExecutionPrice = totalCost / quantity;
                    slippage = Math.Abs(avgExecutionPrice - midPrice) / midPrice;
                }

                // Record slippage metrics
                UpdateMetric($"Slippage.{symbol}.Average", slippage);
                UpdateMetric($"Slippage.{symbol}.LevelsCrossed", levelsCrossed);
                UpdateMetric("Slippage.TotalCalculations", 1);

                LogDebug($"Calculated slippage for {symbol}: {slippage:P2} " +
                        $"(quantity: {quantity}, levels crossed: {levelsCrossed})");

                return await Task.FromResult(slippage);

            }, $"Calculate slippage for {symbol}",
               "Failed to calculate slippage",
               "Verify order book data and quantity parameters");
        }

        public async Task UpdateOrderBookAsync(string symbol, Execution execution)
        {
            await ExecuteWithLoggingAsync(async () =>
            {
                ValidateNotNull(execution, nameof(execution));
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                if (!_orderBooks.TryGetValue(symbol, out var orderBook))
                {
                    LogWarning($"Order book not found for {symbol}, creating new one");
                    orderBook = GenerateSyntheticOrderBook(symbol);
                    _orderBooks.TryAdd(symbol, orderBook);
                }

                // Simulate realistic market impact
                var updatedOrderBook = SimulateExecutionImpact(orderBook, execution);
                _orderBooks.TryUpdate(symbol, updatedOrderBook, orderBook);

                // Update current price based on execution
                var oldPrice = _currentPrices.GetValueOrDefault(symbol, execution.Price);
                _currentPrices.TryUpdate(symbol, execution.Price, oldPrice);

                // Update market statistics
                UpdateMarketStats(symbol, execution);

                LogInfo($"Updated order book for {symbol} after execution: " +
                       $"{execution.Quantity}@{execution.Price:C} " +
                       $"(impact: {Math.Abs(execution.Price - oldPrice) / oldPrice:P2})");

                UpdateMetric($"OrderBooks.{symbol}.Updates", 1);
                UpdateMetric($"OrderBooks.{symbol}.ExecutionVolume", execution.Quantity);
                UpdateMetric("OrderBooks.TotalUpdates", 1);

                await Task.CompletedTask;

            }, $"Update order book for {symbol}",
               "Failed to update order book",
               "Check execution data validity");
        }

        #endregion

        #region Market Simulation

        private void InitializeSampleOrderBooks()
        {
            var popularSymbols = new Dictionary<string, decimal>
            {
                ["AAPL"] = 175.50m,
                ["MSFT"] = 335.25m,
                ["GOOGL"] = 2750.00m,
                ["AMZN"] = 145.75m,
                ["TSLA"] = 245.30m,
                ["NVDA"] = 425.60m,
                ["SPY"] = 445.20m,
                ["QQQ"] = 385.40m,
                ["META"] = 310.50m,
                ["NFLX"] = 425.75m
            };

            foreach (var (symbol, basePrice) in popularSymbols)
            {
                _currentPrices.TryAdd(symbol, basePrice);

                var orderBook = new OrderBook(
                    Symbol: symbol,
                    Bids: GenerateOrderBookSide(basePrice, OrderSide.Buy),
                    Asks: GenerateOrderBookSide(basePrice, OrderSide.Sell),
                    Timestamp: DateTime.UtcNow
                );

                _orderBooks.TryAdd(symbol, orderBook);
                
                // Initialize market stats
                _marketStats.TryAdd(symbol, new MarketStats 
                { 
                    Symbol = symbol,
                    Volatility = 0.02m + (decimal)_random.NextDouble() * 0.03m, // 2-5% volatility
                    Trend = ((decimal)_random.NextDouble() - 0.5m) * 0.001m // -0.05% to +0.05% trend
                });
            }

            LogInfo($"Initialized {popularSymbols.Count} sample order books");
        }

        private OrderBook GenerateSyntheticOrderBook(string symbol)
        {
            var basePrice = _currentPrices.GetValueOrDefault(symbol, GenerateInitialPrice(symbol));

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
            var spreadBps = DefaultSpreadBasisPoints / 10000m;
            var spread = basePrice * spreadBps;

            for (int i = 0; i < DefaultOrderBookDepth; i++)
            {
                // Realistic price levels with increasing spreads
                var levelSpread = spread * (1 + i * 0.1m);
                var priceOffset = levelSpread * (i + 1) * (side == OrderSide.Buy ? -1 : 1);
                var price = Math.Round(basePrice + priceOffset, 2);
                
                // Realistic size distribution (more liquidity near top of book)
                var baseSizeFactor = Math.Pow(0.8, i);
                var size = (decimal)(_random.Next(100, 5000) * baseSizeFactor);
                var orderCount = Math.Max(1, (int)(10 * baseSizeFactor));

                levels.Add(new OrderBookLevel(price, size, orderCount));
            }

            return levels;
        }

        private decimal GenerateInitialPrice(string symbol)
        {
            // Generate realistic stock prices based on symbol characteristics
            var hash = Math.Abs(symbol.GetHashCode());
            var sector = hash % 5; // Simulate different sectors
            
            var (minPrice, maxPrice) = sector switch
            {
                0 => (10m, 100m),    // Small cap
                1 => (50m, 500m),    // Mid cap  
                2 => (100m, 1000m),  // Large cap tech
                3 => (20m, 200m),    // Biotech/volatile
                4 => (30m, 300m),    // General market
                _ => (50m, 500m)
            };

            var priceRange = maxPrice - minPrice;
            var price = minPrice + (priceRange * (decimal)_random.NextDouble());
            
            return Math.Round(price, 2);
        }

        private OrderBook SimulateMarketMovement(OrderBook orderBook)
        {
            var stats = GetOrCreateMarketStats(orderBook.Symbol);
            var movementPercent = (_random.NextDouble() - 0.5) * (double)MaxPriceMovementPercent;
            var movementFactor = 1 + (decimal)movementPercent;

            var newBids = orderBook.Bids.Select(b => b with 
            { 
                Price = Math.Round(b.Price * movementFactor, 2),
                Size = Math.Max(1, b.Size + (decimal)(_random.Next(-100, 100)))
            });
            
            var newAsks = orderBook.Asks.Select(a => a with 
            { 
                Price = Math.Round(a.Price * movementFactor, 2),
                Size = Math.Max(1, a.Size + (decimal)(_random.Next(-100, 100)))
            });

            return orderBook with
            {
                Bids = newBids,
                Asks = newAsks,
                Timestamp = DateTime.UtcNow
            };
        }

        private OrderBook SimulateExecutionImpact(OrderBook orderBook, Execution execution)
        {
            // Realistic market impact based on order size relative to liquidity
            var totalLiquidity = orderBook.Bids.Sum(b => b.Size) + orderBook.Asks.Sum(a => a.Size);
            var impactRatio = execution.Quantity / Math.Max(1, totalLiquidity);
            var impactPercent = Math.Min(impactRatio * 0.1m, MaxMarketImpactPercent);
            
            // Directional impact based on execution side
            var impactFactor = execution.Side == OrderSide.Buy 
                ? 1 + impactPercent 
                : 1 - impactPercent;

            // Update price levels and reduce liquidity at executed levels
            var newBids = orderBook.Bids.Select(b => b with 
            { 
                Price = Math.Round(b.Price * impactFactor, 2),
                Size = execution.Side == OrderSide.Sell && Math.Abs(b.Price - execution.Price) < 0.01m
                    ? Math.Max(1, b.Size - execution.Quantity)
                    : b.Size
            });
            
            var newAsks = orderBook.Asks.Select(a => a with 
            { 
                Price = Math.Round(a.Price * impactFactor, 2),
                Size = execution.Side == OrderSide.Buy && Math.Abs(a.Price - execution.Price) < 0.01m
                    ? Math.Max(1, a.Size - execution.Quantity)
                    : a.Size
            });

            return orderBook with
            {
                Bids = newBids,
                Asks = newAsks,
                Timestamp = DateTime.UtcNow
            };
        }

        private void SimulateMarketActivity(object? state)
        {
            try
            {
                // Periodically update all active order books
                foreach (var symbol in _orderBooks.Keys.Take(10)) // Limit to avoid excessive CPU
                {
                    if (_orderBooks.TryGetValue(symbol, out var orderBook))
                    {
                        var updated = SimulateMarketMovement(orderBook);
                        _orderBooks.TryUpdate(symbol, updated, orderBook);
                    }
                }

                UpdateMetric("MarketSimulation.Updates", 1);
            }
            catch (Exception ex)
            {
                LogError("Error in market simulation timer", ex);
            }
        }

        #endregion

        #region Helper Methods

        private decimal GetMidPrice(OrderBook orderBook)
        {
            var bestBid = orderBook.Bids.OrderByDescending(b => b.Price).FirstOrDefault()?.Price ?? 0m;
            var bestAsk = orderBook.Asks.OrderBy(a => a.Price).FirstOrDefault()?.Price ?? 0m;

            return bestBid > 0 && bestAsk > 0 ? (bestBid + bestAsk) / 2m : Math.Max(bestBid, bestAsk);
        }

        private MarketStats GetOrCreateMarketStats(string symbol)
        {
            return _marketStats.GetOrAdd(symbol, _ => new MarketStats
            {
                Symbol = symbol,
                Volatility = 0.02m + (decimal)_random.NextDouble() * 0.03m,
                Trend = ((decimal)_random.NextDouble() - 0.5m) * 0.001m
            });
        }

        private void UpdateMarketStats(string symbol, Execution execution)
        {
            var stats = GetOrCreateMarketStats(symbol);
            stats.LastExecutionTime = execution.ExecutionTime;
            stats.TotalVolume += execution.Quantity;
            stats.VolumeWeightedPrice = 
                (stats.VolumeWeightedPrice * (stats.TotalVolume - execution.Quantity) + 
                 execution.Price * execution.Quantity) / stats.TotalVolume;
        }

        private void UpdatePriceMetrics(string symbol, decimal newPrice, decimal oldPrice)
        {
            var changePercent = oldPrice > 0 ? (newPrice - oldPrice) / oldPrice : 0;
            
            UpdateMetric($"Prices.{symbol}.Current", newPrice);
            UpdateMetric($"Prices.{symbol}.Change", changePercent);
            UpdateMetric($"Prices.{symbol}.Updates", 1);
            
            // Track high/low
            var highKey = $"Prices.{symbol}.High";
            var lowKey = $"Prices.{symbol}.Low";
            var currentHigh = GetMetric(highKey) as decimal? ?? newPrice;
            var currentLow = GetMetric(lowKey) as decimal? ?? newPrice;
            
            UpdateMetric(highKey, Math.Max(currentHigh, newPrice));
            UpdateMetric(lowKey, Math.Min(currentLow, newPrice));
        }

        #endregion

        #region Lifecycle Management

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing OrderBookSimulator with sample market data");
            InitializeSampleOrderBooks();
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting market simulation timer");
            _marketSimulationTimer.Change(
                TimeSpan.FromMilliseconds(OrderBookUpdateIntervalMs),
                TimeSpan.FromMilliseconds(OrderBookUpdateIntervalMs));
            await Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping market simulation");
            _marketSimulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
            await Task.CompletedTask;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _marketSimulationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Nested Types

        private class MarketStats
        {
            public string Symbol { get; set; } = string.Empty;
            public decimal Volatility { get; set; }
            public decimal Trend { get; set; }
            public decimal TotalVolume { get; set; }
            public decimal VolumeWeightedPrice { get; set; }
            public DateTime LastExecutionTime { get; set; }
        }

        #endregion
    }
}