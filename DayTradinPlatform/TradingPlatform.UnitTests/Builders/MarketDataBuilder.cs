using System;
using Bogus;
using TradingPlatform.Core.Models;

namespace TradingPlatform.UnitTests.Builders
{
    /// <summary>
    /// Builder for creating test MarketData instances
    /// </summary>
    public class MarketDataBuilder
    {
        private readonly Faker<MarketData> _faker;
        private string _symbol = "AAPL";
        private DateTime _timestamp = DateTime.UtcNow;
        private decimal _basePrice = 150m;
        private decimal _volatility = 0.02m;
        private long _volume = 50000000;

        public MarketDataBuilder()
        {
            _faker = new Faker<MarketData>()
                .CustomInstantiator(f => new MarketData())
                .RuleFor(m => m.Symbol, f => _symbol)
                .RuleFor(m => m.Timestamp, f => _timestamp)
                .RuleFor(m => m.Open, f => _basePrice * (1m + f.Random.Decimal(-_volatility, _volatility)))
                .RuleFor(m => m.High, (f, m) => m.Open * (1m + f.Random.Decimal(0, _volatility * 2)))
                .RuleFor(m => m.Low, (f, m) => m.Open * (1m - f.Random.Decimal(0, _volatility * 2)))
                .RuleFor(m => m.Close, (f, m) => f.Random.Decimal(m.Low, m.High))
                .RuleFor(m => m.Volume, f => f.Random.Long(_volume / 2, _volume * 2))
                .RuleFor(m => m.Volatility, f => _volatility)
                .RuleFor(m => m.RSI, f => f.Random.Decimal(30, 70))
                .RuleFor(m => m.MACD, f => f.Random.Decimal(-2, 2))
                .RuleFor(m => m.BollingerUpper, (f, m) => m.Close * (1m + _volatility * 2))
                .RuleFor(m => m.BollingerLower, (f, m) => m.Close * (1m - _volatility * 2))
                .RuleFor(m => m.VWAP, (f, m) => (m.High + m.Low + m.Close) / 3)
                .RuleFor(m => m.AverageTrueRange, f => _basePrice * _volatility)
                .RuleFor(m => m.SMA20, (f, m) => m.Close * f.Random.Decimal(0.95m, 1.05m))
                .RuleFor(m => m.SMA50, (f, m) => m.Close * f.Random.Decimal(0.90m, 1.10m))
                .RuleFor(m => m.EMA12, (f, m) => m.Close * f.Random.Decimal(0.98m, 1.02m))
                .RuleFor(m => m.EMA26, (f, m) => m.Close * f.Random.Decimal(0.95m, 1.05m));
        }

        public MarketDataBuilder WithSymbol(string symbol)
        {
            _symbol = symbol;
            return this;
        }

        public MarketDataBuilder WithTimestamp(DateTime timestamp)
        {
            _timestamp = timestamp;
            return this;
        }

        public MarketDataBuilder WithBasePrice(decimal price)
        {
            _basePrice = price;
            return this;
        }

        public MarketDataBuilder WithVolatility(decimal volatility)
        {
            _volatility = volatility;
            return this;
        }

        public MarketDataBuilder WithVolume(long volume)
        {
            _volume = volume;
            return this;
        }

        public MarketDataBuilder WithUptrend()
        {
            _faker.RuleFor(m => m.Close, (f, m) => m.Open * (1m + f.Random.Decimal(0.01m, _volatility)))
                  .RuleFor(m => m.RSI, f => f.Random.Decimal(55, 70))
                  .RuleFor(m => m.MACD, f => f.Random.Decimal(0.5m, 2m));
            return this;
        }

        public MarketDataBuilder WithDowntrend()
        {
            _faker.RuleFor(m => m.Close, (f, m) => m.Open * (1m - f.Random.Decimal(0.01m, _volatility)))
                  .RuleFor(m => m.RSI, f => f.Random.Decimal(30, 45))
                  .RuleFor(m => m.MACD, f => f.Random.Decimal(-2m, -0.5m));
            return this;
        }

        public MarketDataBuilder WithGap(decimal gapPercentage)
        {
            _faker.RuleFor(m => m.Open, f => _basePrice * (1m + gapPercentage));
            return this;
        }

        public MarketDataBuilder WithHighVolume()
        {
            _faker.RuleFor(m => m.Volume, f => f.Random.Long(_volume * 2, _volume * 4));
            return this;
        }

        public MarketDataBuilder WithLowVolume()
        {
            _faker.RuleFor(m => m.Volume, f => f.Random.Long(_volume / 4, _volume / 2));
            return this;
        }

        public MarketData Build()
        {
            return _faker.Generate();
        }

        public static implicit operator MarketData(MarketDataBuilder builder)
        {
            return builder.Build();
        }
    }
}