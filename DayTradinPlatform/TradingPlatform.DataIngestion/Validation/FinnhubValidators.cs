// File: TradingPlatform.DataIngestion\Validation\FinnhubValidators.cs

using FluentValidation;
using TradingPlatform.Core.Models;

namespace TradingPlatform.DataIngestion.Validation
{
    /// <summary>
    /// FluentValidation validators for Finnhub API responses.
    /// Ensures data quality and consistency for day trading operations.
    /// </summary>
    public class FinnhubQuoteResponseValidator : AbstractValidator<FinnhubQuoteResponse>
    {
        public FinnhubQuoteResponseValidator()
        {
            RuleFor(x => x.Current)
                .GreaterThan(0)
                .WithMessage("Current price must be greater than 0")
                .LessThan(10000)
                .WithMessage("Current price seems unrealistic (>$10,000)");

            RuleFor(x => x.High)
                .GreaterThanOrEqualTo(x => x.Low)
                .WithMessage("High price must be greater than or equal to low price")
                .GreaterThanOrEqualTo(x => x.Current)
                .When(x => x.Current > 0)
                .WithMessage("High price should be greater than or equal to current price");

            RuleFor(x => x.Low)
                .GreaterThan(0)
                .WithMessage("Low price must be greater than 0")
                .LessThanOrEqualTo(x => x.Current)
                .When(x => x.Current > 0)
                .WithMessage("Low price should be less than or equal to current price");

            RuleFor(x => x.Open)
                .GreaterThan(0)
                .WithMessage("Open price must be greater than 0");

            RuleFor(x => x.PreviousClose)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Previous close must be non-negative");

            // Day trading specific validations
            RuleFor(x => x)
                .Must(HaveReasonableVolatility)
                .WithMessage("Price relationships seem inconsistent")
                .WithName("PriceConsistency");

            RuleFor(x => x.PercentChange)
                .InclusiveBetween(-50, 50)
                .WithMessage("Extreme price change detected (>50%)")
                .WithSeverity(Severity.Warning);
        }

        private bool HaveReasonableVolatility(FinnhubQuoteResponse quote)
        {
            if (quote.High <= 0 || quote.Low <= 0 || quote.Open <= 0 || quote.Current <= 0)
                return false;

            // Check that high/low spread is reasonable (not more than 50% of price)
            var spread = quote.High - quote.Low;
            var midPrice = (quote.High + quote.Low) / 2;
            var spreadPercent = (spread / midPrice) * 100;

            return spreadPercent <= 50; // Max 50% spread
        }
    }

    /// <summary>
    /// Validator for MarketData objects enriched from Finnhub responses.
    /// This is used after Finnhub data is converted to the internal MarketData format.
    /// </summary>
    public class FinnhubMarketDataValidator : AbstractValidator<MarketData>
    {
        public FinnhubMarketDataValidator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("Symbol is required")
                .Length(1, 10)
                .WithMessage("Symbol must be between 1 and 10 characters");

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .WithMessage("Price must be greater than 0");

            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Volume must be non-negative");

            RuleFor(x => x.Timestamp)
                .NotEqual(default(DateTime))
                .WithMessage("Timestamp is required")
                .Must(x => (DateTime.UtcNow - x).TotalHours < 24)
                .WithMessage("Market data is too old (>24 hours)");

            // Day trading specific validations
            RuleFor(x => x)
                .Must(BeRecentData)
                .WithMessage("Market data is too old for day trading (>1 hour)")
                .WithName("DataFreshness")
                .WithSeverity(Severity.Warning);
        }

        private bool BeRecentData(MarketData data)
        {
            return DateTime.UtcNow - data.Timestamp <= TimeSpan.FromHours(1);
        }
    }
}