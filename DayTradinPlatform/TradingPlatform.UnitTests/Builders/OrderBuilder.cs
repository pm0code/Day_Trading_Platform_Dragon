using System;
using Bogus;
using TradingPlatform.Core.Models;

namespace TradingPlatform.UnitTests.Builders
{
    /// <summary>
    /// Builder for creating test Order instances
    /// </summary>
    public class OrderBuilder
    {
        private readonly Faker<Order> _faker;
        private string _symbol = "AAPL";
        private OrderType _orderType = OrderType.Market;
        private OrderSide _side = OrderSide.Buy;
        private decimal _quantity = 100;
        private decimal _price = 150m;
        private OrderStatus _status = OrderStatus.Pending;

        public OrderBuilder()
        {
            _faker = new Faker<Order>()
                .RuleFor(o => o.Id, f => Guid.NewGuid().ToString())
                .RuleFor(o => o.Symbol, f => _symbol)
                .RuleFor(o => o.OrderType, f => _orderType)
                .RuleFor(o => o.Side, f => _side)
                .RuleFor(o => o.Quantity, f => _quantity)
                .RuleFor(o => o.Price, f => _price)
                .RuleFor(o => o.Status, f => _status)
                .RuleFor(o => o.CreatedAt, f => DateTime.UtcNow)
                .RuleFor(o => o.UpdatedAt, f => DateTime.UtcNow)
                .RuleFor(o => o.TimeInForce, f => TimeInForce.Day)
                .RuleFor(o => o.StrategyId, f => "test-strategy")
                .RuleFor(o => o.ClientOrderId, f => f.Random.AlphaNumeric(10))
                .RuleFor(o => o.AccountId, f => "test-account");
        }

        public OrderBuilder WithSymbol(string symbol)
        {
            _symbol = symbol;
            return this;
        }

        public OrderBuilder WithOrderType(OrderType orderType)
        {
            _orderType = orderType;
            return this;
        }

        public OrderBuilder WithSide(OrderSide side)
        {
            _side = side;
            return this;
        }

        public OrderBuilder WithQuantity(decimal quantity)
        {
            _quantity = quantity;
            return this;
        }

        public OrderBuilder WithPrice(decimal price)
        {
            _price = price;
            return this;
        }

        public OrderBuilder WithStatus(OrderStatus status)
        {
            _status = status;
            return this;
        }

        public OrderBuilder WithLimitOrder(decimal limitPrice)
        {
            _orderType = OrderType.Limit;
            _price = limitPrice;
            return this;
        }

        public OrderBuilder WithStopOrder(decimal stopPrice)
        {
            _orderType = OrderType.Stop;
            _faker.RuleFor(o => o.StopPrice, f => stopPrice);
            return this;
        }

        public OrderBuilder WithStopLimitOrder(decimal stopPrice, decimal limitPrice)
        {
            _orderType = OrderType.StopLimit;
            _price = limitPrice;
            _faker.RuleFor(o => o.StopPrice, f => stopPrice);
            return this;
        }

        public OrderBuilder WithTimeInForce(TimeInForce tif)
        {
            _faker.RuleFor(o => o.TimeInForce, f => tif);
            return this;
        }

        public OrderBuilder WithStrategy(string strategyId)
        {
            _faker.RuleFor(o => o.StrategyId, f => strategyId);
            return this;
        }

        public OrderBuilder WithAccount(string accountId)
        {
            _faker.RuleFor(o => o.AccountId, f => accountId);
            return this;
        }

        public OrderBuilder AsExecuted(decimal executedPrice, decimal executedQuantity)
        {
            _status = OrderStatus.Filled;
            _faker.RuleFor(o => o.ExecutedPrice, f => executedPrice)
                  .RuleFor(o => o.ExecutedQuantity, f => executedQuantity)
                  .RuleFor(o => o.FilledAt, f => DateTime.UtcNow)
                  .RuleFor(o => o.Commission, f => executedPrice * executedQuantity * 0.001m);
            return this;
        }

        public OrderBuilder AsPartiallyFilled(decimal executedQuantity)
        {
            _status = OrderStatus.PartiallyFilled;
            _faker.RuleFor(o => o.ExecutedQuantity, f => executedQuantity)
                  .RuleFor(o => o.ExecutedPrice, f => _price)
                  .RuleFor(o => o.FilledAt, f => DateTime.UtcNow);
            return this;
        }

        public OrderBuilder AsCancelled()
        {
            _status = OrderStatus.Cancelled;
            _faker.RuleFor(o => o.CancelledAt, f => DateTime.UtcNow)
                  .RuleFor(o => o.CancellationReason, f => "User requested");
            return this;
        }

        public OrderBuilder AsRejected(string reason)
        {
            _status = OrderStatus.Rejected;
            _faker.RuleFor(o => o.RejectionReason, f => reason);
            return this;
        }

        public Order Build()
        {
            return _faker.Generate();
        }

        public static implicit operator Order(OrderBuilder builder)
        {
            return builder.Build();
        }
    }
}