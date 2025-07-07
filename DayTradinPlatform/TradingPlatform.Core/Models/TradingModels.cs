using System;
using System.Runtime.InteropServices;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Order side enumeration for buy/sell orders
    /// </summary>
    public enum OrderSide
    {
        Buy = 0,
        Sell = 1
    }

    /// <summary>
    /// Order type enumeration for different order types
    /// </summary>
    public enum OrderType
    {
        Market = 0,
        Limit = 1,
        StopLoss = 2,
        StopLimit = 3
    }

    /// <summary>
    /// Order status enumeration
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,
        PartiallyFilled = 1,
        Filled = 2,
        Cancelled = 3,
        Rejected = 4,
        Expired = 5
    }

    /// <summary>
    /// Trading order representation optimized for high-performance scenarios
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class Order
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType OrderType { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal RemainingQuantity => Quantity - FilledQuantity;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Strategy { get; set; }
        public string? ClientOrderId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public Order()
        {
            CreatedAt = DateTime.UtcNow;
            Status = OrderStatus.Pending;
        }

        public Order(string symbol, OrderSide side, OrderType orderType, decimal quantity, decimal price)
        {
            Id = Guid.NewGuid().ToString("N");
            Symbol = symbol;
            Side = side;
            OrderType = orderType;
            Quantity = quantity;
            Price = price;
            CreatedAt = DateTime.UtcNow;
            Status = OrderStatus.Pending;
        }

        public bool IsActive => Status == OrderStatus.Pending || Status == OrderStatus.PartiallyFilled;
        public bool IsCompleted => Status == OrderStatus.Filled || Status == OrderStatus.Cancelled || Status == OrderStatus.Rejected || Status == OrderStatus.Expired;
        public decimal GetValue() => Quantity * Price;
    }

    /// <summary>
    /// Order execution result for tracking trade executions
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class OrderExecution
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Commission { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string? CounterpartyId { get; set; }
        public string? ExchangeTradeId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public OrderExecution()
        {
            ExecutionId = Guid.NewGuid().ToString("N");
            ExecutedAt = DateTime.UtcNow;
        }

        public decimal GetNotionalValue() => Quantity * Price;
        public decimal GetNetValue() => GetNotionalValue() - Commission;

        public void Clear()
        {
            ExecutionId = string.Empty;
            OrderId = string.Empty;
            Symbol = string.Empty;
            Side = OrderSide.Buy;
            Quantity = 0;
            Price = 0;
            Commission = 0;
            ExecutedAt = DateTime.MinValue;
            CounterpartyId = null;
            ExchangeTradeId = null;
            Metadata?.Clear();
        }
    }

    /// <summary>
    /// Market data tick for real-time price updates
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MarketTick
    {
        public string Symbol { get; init; }
        public decimal BidPrice { get; init; }
        public decimal AskPrice { get; init; }
        public decimal LastPrice { get; init; }
        public decimal Volume { get; init; }
        public DateTime Timestamp { get; init; }
        public long SequenceNumber { get; init; }

        public decimal Spread => AskPrice - BidPrice;
        public decimal MidPrice => (BidPrice + AskPrice) / 2m;
    }

    /// <summary>
    /// Trading position representation
    /// </summary>
    public class Position
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal MarketPrice { get; set; }
        public decimal UnrealizedPnL => (MarketPrice - AveragePrice) * Quantity;
        public decimal RealizedPnL { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? Strategy { get; set; }

        public bool IsLong => Quantity > 0;
        public bool IsShort => Quantity < 0;
        public bool IsFlat => Quantity == 0;
        public decimal GetNotionalValue() => Math.Abs(Quantity) * MarketPrice;
    }

    /// <summary>
    /// Portfolio summary with risk metrics
    /// </summary>
    public class PortfolioSummary
    {
        public decimal TotalValue { get; set; }
        public decimal CashBalance { get; set; }
        public decimal MarginUsed { get; set; }
        public decimal AvailableMargin { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal DayPnL { get; set; }
        public Dictionary<string, Position> Positions { get; set; } = new();
        public DateTime LastUpdated { get; set; }

        public decimal GetEquity() => TotalValue + UnrealizedPnL;
        public decimal GetMarginUtilization() => MarginUsed / (MarginUsed + AvailableMargin);
        public int GetPositionCount() => Positions.Count(p => !p.Value.IsFlat);
    }
}