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
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("Symbol is required")
                .Length(1, 10)
                .WithMessage("Symbol must be between 1 and 10 characters");

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

            RuleFor(x => x.Timestamp)
                .GreaterThan(946684800) // 2000-01-01
                .WithMessage("Timestamp must be after year 2000")
                .LessThan(x => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400) // Not more than 1 day in future
                .WithMessage("Timestamp cannot be more than 1 day in the future");

            // Day trading specific validations
            RuleFor(x => x)
                .Must(BeRecentData)
                .WithMessage("Quote data is too old for day trading (>1 hour)")
                .WithName("DataFreshness");

            RuleFor(x => x)
                .Must(HaveReasonableVolatility)
                .WithMessage("Price relationships seem inconsistent")
                .WithName("PriceConsistency");
        }

        private bool BeRecentData(FinnhubQuoteResponse quote)
        {
            var quoteTime = DateTimeOffset.FromUnixTimeSeconds(quote.Timestamp);
            return DateTime.UtcNow - quoteTime <= TimeSpan.FromHours(1);
        }

        private bool HaveReasonableVolatility(FinnhubQuoteResponse quote)
        {
            if (quote.High <= 0 || quote.Low <= 0) return false;

            var dayRange = quote.High - quote.Low;
            var avgPrice = (quote.High + quote.Low) / 2;
            var volatilityPercent = (dayRange / avgPrice) * 100;

            // Reasonable volatility: 0.1% to 50% in a day
            return volatilityPercent >= 0.1m && volatilityPercent <= 50m;
        }
    }

    /// <summary>
    /// Validator for company profile data quality.
    /// </summary>
    public class CompanyProfileValidator : AbstractValidator<CompanyProfile>
    {
        public CompanyProfileValidator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("Symbol is required")
                .Length(1, 10)
                .WithMessage("Symbol must be between 1 and 10 characters");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Company name is required")
                .Length(1, 200)
                .WithMessage("Company name must be between 1 and 200 characters");

            RuleFor(x => x.MarketCapitalization)
                .GreaterThan(0)
                .WithMessage("Market capitalization must be positive")
                .LessThan(50_000_000_000_000m) // $50 trillion
                .WithMessage("Market capitalization seems unrealistic");

            RuleFor(x => x.SharesOutstanding)
                .GreaterThan(0)
                .WithMessage("Shares outstanding must be positive")
                .LessThan(1_000_000_000_000L) // 1 trillion shares
                .WithMessage("Shares outstanding seems unrealistic");

            RuleFor(x => x.FreeFloat)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Free float must be non-negative")
                .LessThanOrEqualTo(x => x.SharesOutstanding)
                .WithMessage("Free float cannot exceed shares outstanding");

            // Day trading specific validations
            RuleFor(x => x)
                .Must(HaveAdequateLiquidityForDayTrading)
                .WithMessage("Company may not have adequate liquidity for day trading")
                .WithName("DayTradingLiquidity");
        }

        private bool HaveAdequateLiquidityForDayTrading(CompanyProfile profile)
        {
            // Minimum thresholds for day trading suitability
            var minMarketCap = 50_000_000m; // $50M
            var minFreeFloat = 10_000_000L; // 10M shares

            return profile.MarketCapitalization >= minMarketCap &&
                   profile.FreeFloat >= minFreeFloat;
        }
    }

    /// <summary>
    /// Validator for market data consistency and quality.
    /// </summary>
    public class MarketDataValidator : AbstractValidator<MarketData>
    {
        public MarketDataValidator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("Symbol is required");

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .WithMessage("Price must be positive");

            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Volume must be non-negative");

            RuleFor(x => x.High)
                .GreaterThanOrEqualTo(x => x.Low)
                .WithMessage("High must be greater than or equal to low")
                .GreaterThanOrEqualTo(x => x.Price)
                .WithMessage("High should be greater than or equal to current price");

            RuleFor(x => x.Low)
                .LessThanOrEqualTo(x => x.Price)
                .WithMessage("Low should be less than or equal to current price");

            RuleFor(x => x.Timestamp)
                .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
                .WithMessage("Timestamp cannot be more than 5 minutes in the future");
        }
    }

    /// <summary>
    /// Validator for news items to ensure data quality.
    /// </summary>
    public class NewsItemValidator : AbstractValidator<NewsItem>
    {
        public NewsItemValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("News item ID is required");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("News title is required")
                .Length(5, 500)
                .WithMessage("News title must be between 5 and 500 characters");

            RuleFor(x => x.Source)
                .NotEmpty()
                .WithMessage("News source is required");

            RuleFor(x => x.Url)
                .NotEmpty()
                .WithMessage("News URL is required")
                .Must(BeValidUrl)
                .WithMessage("News URL must be a valid URL");

            RuleFor(x => x.PublishedAt)
                .LessThanOrEqualTo(DateTime.UtcNow.AddHours(1))
                .WithMessage("Published date cannot be more than 1 hour in the future")
                .GreaterThan(DateTime.UtcNow.AddDays(-365))
                .WithMessage("Published date cannot be more than 1 year old");
        }

        private bool BeValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var validUrl) &&
                   (validUrl.Scheme == Uri.UriSchemeHttp || validUrl.Scheme == Uri.UriSchemeHttps);
        }
    }
}

// Total Lines: 198
