using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Core.Performance
{
    /// <summary>
    /// Ultra-low latency order book implementation using sorted arrays
    /// </summary>
    public sealed class OptimizedOrderBook
    {
        private readonly string _symbol;
        private readonly PriceLevel[] _bidLevels;
        private readonly PriceLevel[] _askLevels;
        private readonly Dictionary<string, OrderLocation> _orderIndex;
        private int _bidCount;
        private int _askCount;

        [StructLayout(LayoutKind.Sequential)]
        private struct PriceLevel
        {
            public decimal Price;
            public decimal Quantity;
            public int OrderCount;
            public long UpdateTimestamp;
        }

        private struct OrderLocation
        {
            public bool IsBid;
            public int LevelIndex;
        }

        public OptimizedOrderBook(string symbol, int maxLevels = 100)
        {
            _symbol = symbol;
            _bidLevels = new PriceLevel[maxLevels];
            _askLevels = new PriceLevel[maxLevels];
            _orderIndex = new Dictionary<string, OrderLocation>(maxLevels * 10);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrder(Order order)
        {
            using var latency = LatencyTracking.GetOrCreate("OrderBook.Add").MeasureScope();

            if (order.Side == OrderSide.Buy)
            {
                AddBidOrder(order);
            }
            else
            {
                AddAskOrder(order);
            }

            _orderIndex[order.Id] = new OrderLocation
            {
                IsBid = order.Side == OrderSide.Buy,
                LevelIndex = FindPriceLevel(order.Price, order.Side == OrderSide.Buy)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void AddBidOrder(Order order)
        {
            var insertIndex = _bidCount;

            // Binary search for insertion point (descending order for bids)
            var left = 0;
            var right = _bidCount - 1;

            while (left <= right)
            {
                var mid = (left + right) >> 1;
                if (_bidLevels[mid].Price > order.Price)
                {
                    left = mid + 1;
                }
                else if (_bidLevels[mid].Price < order.Price)
                {
                    right = mid - 1;
                }
                else
                {
                    // Price level exists, update it
                    _bidLevels[mid].Quantity += order.Quantity;
                    _bidLevels[mid].OrderCount++;
                    _bidLevels[mid].UpdateTimestamp = GetTimestamp();
                    return;
                }
            }

            insertIndex = left;

            // Shift elements if needed
            if (insertIndex < _bidCount)
            {
                Array.Copy(_bidLevels, insertIndex, _bidLevels, insertIndex + 1, _bidCount - insertIndex);
            }

            // Insert new level
            _bidLevels[insertIndex] = new PriceLevel
            {
                Price = order.Price,
                Quantity = order.Quantity,
                OrderCount = 1,
                UpdateTimestamp = GetTimestamp()
            };

            _bidCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void AddAskOrder(Order order)
        {
            var insertIndex = _askCount;

            // Binary search for insertion point (ascending order for asks)
            var left = 0;
            var right = _askCount - 1;

            while (left <= right)
            {
                var mid = (left + right) >> 1;
                if (_askLevels[mid].Price < order.Price)
                {
                    left = mid + 1;
                }
                else if (_askLevels[mid].Price > order.Price)
                {
                    right = mid - 1;
                }
                else
                {
                    // Price level exists, update it
                    _askLevels[mid].Quantity += order.Quantity;
                    _askLevels[mid].OrderCount++;
                    _askLevels[mid].UpdateTimestamp = GetTimestamp();
                    return;
                }
            }

            insertIndex = left;

            // Shift elements if needed
            if (insertIndex < _askCount)
            {
                Array.Copy(_askLevels, insertIndex, _askLevels, insertIndex + 1, _askCount - insertIndex);
            }

            // Insert new level
            _askLevels[insertIndex] = new PriceLevel
            {
                Price = order.Price,
                Quantity = order.Quantity,
                OrderCount = 1,
                UpdateTimestamp = GetTimestamp()
            };

            _askCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal bid, decimal ask) GetBestBidAsk()
        {
            var bid = _bidCount > 0 ? _bidLevels[0].Price : 0m;
            var ask = _askCount > 0 ? _askLevels[0].Price : decimal.MaxValue;
            return (bid, ask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetSpread()
        {
            if (_bidCount == 0 || _askCount == 0) return 0m;
            return _askLevels[0].Price - _bidLevels[0].Price;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetMidPrice()
        {
            if (_bidCount == 0 || _askCount == 0) return 0m;
            return (_bidLevels[0].Price + _askLevels[0].Price) / 2m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindPriceLevel(decimal price, bool isBid)
        {
            if (isBid)
            {
                for (int i = 0; i < _bidCount; i++)
                {
                    if (_bidLevels[i].Price == price) return i;
                }
            }
            else
            {
                for (int i = 0; i < _askCount; i++)
                {
                    if (_askLevels[i].Price == price) return i;
                }
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetTimestamp()
        {
            return DateTime.UtcNow.Ticks;
        }

        public void Clear()
        {
            _bidCount = 0;
            _askCount = 0;
            _orderIndex.Clear();
        }

        public int BidLevels => _bidCount;
        public int AskLevels => _askCount;
        public string Symbol => _symbol;
    }

    /// <summary>
    /// Optimized order matching engine
    /// </summary>
    public sealed class OptimizedMatchingEngine
    {
        private readonly OptimizedOrderBook _orderBook;
        private readonly LockFreeQueue<Order> _orderQueue;
        private readonly HighPerformancePool<OrderExecution> _executionPool;

        public OptimizedMatchingEngine(string symbol)
        {
            _orderBook = new OptimizedOrderBook(symbol);
            _orderQueue = new LockFreeQueue<Order>();
            _executionPool = new HighPerformancePool<OrderExecution>(
                () => new OrderExecution(),
                e => e.Clear(),
                maxSize: 10000);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubmitOrder(Order order)
        {
            _orderQueue.Enqueue(order);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ProcessOrders()
        {
            while (_orderQueue.TryDequeue(out var order) && order != null)
            {
                ProcessSingleOrder(order);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSingleOrder(Order order)
        {
            using var latency = LatencyTracking.GetOrCreate("MatchingEngine.Process").MeasureScope();

            if (order.OrderType == OrderType.Market)
            {
                ExecuteMarketOrder(order);
            }
            else
            {
                _orderBook.AddOrder(order);
                CheckForMatches();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteMarketOrder(Order order)
        {
            var (bid, ask) = _orderBook.GetBestBidAsk();
            var executionPrice = order.Side == OrderSide.Buy ? ask : bid;

            if (executionPrice > 0 && executionPrice < decimal.MaxValue)
            {
                using var execution = _executionPool.RentPooled();
                execution.Value.OrderId = order.Id;
                execution.Value.Symbol = order.Symbol;
                execution.Value.Side = order.Side;
                execution.Value.Quantity = order.Quantity;
                execution.Value.Price = executionPrice;
                execution.Value.Commission = order.Quantity * executionPrice * 0.001m;
                execution.Value.ExecutedAt = DateTime.UtcNow;

                // Process execution...
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckForMatches()
        {
            var (bid, ask) = _orderBook.GetBestBidAsk();
            
            // Simple crossing check
            if (bid >= ask && bid > 0 && ask < decimal.MaxValue)
            {
                // Execute crossing orders
                // Implementation depends on specific matching rules
            }
        }
    }
}