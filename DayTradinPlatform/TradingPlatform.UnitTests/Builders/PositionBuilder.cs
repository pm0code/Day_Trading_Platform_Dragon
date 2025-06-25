using System;
using Bogus;
using TradingPlatform.Core.Models;

namespace TradingPlatform.UnitTests.Builders
{
    /// <summary>
    /// Builder for creating test Position instances
    /// </summary>
    public class PositionBuilder
    {
        private readonly Faker<Position> _faker;
        private string _symbol = "AAPL";
        private decimal _quantity = 100;
        private decimal _averagePrice = 150m;
        private decimal _currentPrice = 152m;
        private PositionSide _side = PositionSide.Long;

        public PositionBuilder()
        {
            _faker = new Faker<Position>()
                .CustomInstantiator(f => new Position
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = _symbol,
                    Quantity = _quantity,
                    AveragePrice = _averagePrice,
                    CurrentPrice = _currentPrice,
                    Side = _side,
                    OpenedAt = DateTime.UtcNow.AddHours(-f.Random.Int(1, 24)),
                    UpdatedAt = DateTime.UtcNow,
                    AccountId = "test-account",
                    StrategyId = "test-strategy"
                })
                .RuleFor(p => p.MarketValue, (f, p) => p.Quantity * p.CurrentPrice)
                .RuleFor(p => p.CostBasis, (f, p) => p.Quantity * p.AveragePrice)
                .RuleFor(p => p.UnrealizedPnL, (f, p) => p.Side == PositionSide.Long 
                    ? (p.CurrentPrice - p.AveragePrice) * p.Quantity 
                    : (p.AveragePrice - p.CurrentPrice) * p.Quantity)
                .RuleFor(p => p.UnrealizedPnLPercent, (f, p) => p.UnrealizedPnL / p.CostBasis * 100)
                .RuleFor(p => p.RealizedPnL, f => 0m)
                .RuleFor(p => p.Commission, (f, p) => p.CostBasis * 0.001m)
                .RuleFor(p => p.DayPnL, (f, p) => f.Random.Decimal(-1000, 1000))
                .RuleFor(p => p.DayPnLPercent, (f, p) => p.DayPnL / p.CostBasis * 100);
        }

        public PositionBuilder WithSymbol(string symbol)
        {
            _symbol = symbol;
            return this;
        }

        public PositionBuilder WithQuantity(decimal quantity)
        {
            _quantity = quantity;
            return this;
        }

        public PositionBuilder WithAveragePrice(decimal price)
        {
            _averagePrice = price;
            return this;
        }

        public PositionBuilder WithCurrentPrice(decimal price)
        {
            _currentPrice = price;
            return this;
        }

        public PositionBuilder WithSide(PositionSide side)
        {
            _side = side;
            return this;
        }

        public PositionBuilder AsLong()
        {
            _side = PositionSide.Long;
            return this;
        }

        public PositionBuilder AsShort()
        {
            _side = PositionSide.Short;
            return this;
        }

        public PositionBuilder WithProfit(decimal profitAmount)
        {
            if (_side == PositionSide.Long)
            {
                _currentPrice = _averagePrice + (profitAmount / _quantity);
            }
            else
            {
                _currentPrice = _averagePrice - (profitAmount / _quantity);
            }
            return this;
        }

        public PositionBuilder WithLoss(decimal lossAmount)
        {
            if (_side == PositionSide.Long)
            {
                _currentPrice = _averagePrice - (lossAmount / _quantity);
            }
            else
            {
                _currentPrice = _averagePrice + (lossAmount / _quantity);
            }
            return this;
        }

        public PositionBuilder WithProfitPercentage(decimal percentage)
        {
            if (_side == PositionSide.Long)
            {
                _currentPrice = _averagePrice * (1 + percentage / 100);
            }
            else
            {
                _currentPrice = _averagePrice * (1 - percentage / 100);
            }
            return this;
        }

        public PositionBuilder WithLossPercentage(decimal percentage)
        {
            if (_side == PositionSide.Long)
            {
                _currentPrice = _averagePrice * (1 - percentage / 100);
            }
            else
            {
                _currentPrice = _averagePrice * (1 + percentage / 100);
            }
            return this;
        }

        public PositionBuilder WithRealizedPnL(decimal realizedPnL)
        {
            _faker.RuleFor(p => p.RealizedPnL, f => realizedPnL);
            return this;
        }

        public PositionBuilder WithStopLoss(decimal stopPrice)
        {
            _faker.RuleFor(p => p.StopLoss, f => stopPrice);
            return this;
        }

        public PositionBuilder WithTakeProfit(decimal takeProfitPrice)
        {
            _faker.RuleFor(p => p.TakeProfit, f => takeProfitPrice);
            return this;
        }

        public PositionBuilder WithStrategy(string strategyId)
        {
            _faker.RuleFor(p => p.StrategyId, f => strategyId);
            return this;
        }

        public PositionBuilder AsIntraday()
        {
            _faker.RuleFor(p => p.OpenedAt, f => DateTime.UtcNow.Date.AddHours(f.Random.Int(9, 15)))
                  .RuleFor(p => p.IsIntraday, f => true);
            return this;
        }

        public Position Build()
        {
            return _faker.Generate();
        }

        public static implicit operator Position(PositionBuilder builder)
        {
            return builder.Build();
        }
    }
}