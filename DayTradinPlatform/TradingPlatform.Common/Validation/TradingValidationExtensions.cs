using FluentValidation;
using TradingPlatform.Common.Extensions;

namespace TradingPlatform.Common.Validation;

/// <summary>
/// FluentValidation extensions for trading-specific validation rules.
/// Provides reusable validation logic for financial data, orders, and trading operations.
/// </summary>
public static class TradingValidationExtensions
{
    #region Price Validation

    /// <summary>
    /// Validates that a price is positive and within reasonable bounds.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="minPrice">Minimum allowed price (default 0.01)</param>
    /// <param name="maxPrice">Maximum allowed price (default 100,000)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidTradingPrice<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        decimal minPrice = 0.01m,
        decimal maxPrice = 100000m)
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage("Price must be positive")
            .GreaterThanOrEqualTo(minPrice)
            .WithMessage($"Price must be at least {minPrice:C}")
            .LessThanOrEqualTo(maxPrice)
            .WithMessage($"Price cannot exceed {maxPrice:C}")
            .Must(price => price.IsValidPrice(maxPrice))
            .WithMessage("Price must have valid financial precision");
    }

    /// <summary>
    /// Validates that a nullable price is either null or a valid trading price.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="minPrice">Minimum allowed price (default 0.01)</param>
    /// <param name="maxPrice">Maximum allowed price (default 100,000)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal?> ValidTradingPriceOrNull<T>(
        this IRuleBuilder<T, decimal?> ruleBuilder,
        decimal minPrice = 0.01m,
        decimal maxPrice = 100000m)
    {
        return ruleBuilder
            .Must(price => price == null || price.Value.IsValidPrice(maxPrice))
            .WithMessage("Price must be null or a valid trading price")
            .Must(price => price == null || (price.Value >= minPrice && price.Value <= maxPrice))
            .WithMessage($"Price must be null or between {minPrice:C} and {maxPrice:C}");
    }

    /// <summary>
    /// Validates that a stop price is reasonable relative to entry price.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="entryPriceSelector">Function to get entry price</param>
    /// <param name="isLongPosition">Whether this is a long position</param>
    /// <param name="maxStopPercent">Maximum stop distance as percentage (default 10%)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidStopPrice<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        Func<T, decimal> entryPriceSelector,
        bool isLongPosition = true,
        decimal maxStopPercent = 10m)
    {
        return ruleBuilder
            .ValidTradingPrice()
            .Must((instance, stopPrice) =>
            {
                var entryPrice = entryPriceSelector(instance);
                if (entryPrice <= 0) return false;

                var stopDistance = Math.Abs(stopPrice - entryPrice);
                var stopPercent = (stopDistance / entryPrice) * 100m;

                return stopPercent <= maxStopPercent;
            })
            .WithMessage($"Stop price cannot be more than {maxStopPercent}% away from entry price")
            .Must((instance, stopPrice) =>
            {
                var entryPrice = entryPriceSelector(instance);
                if (entryPrice <= 0) return false;

                // For long positions, stop should be below entry
                // For short positions, stop should be above entry
                return isLongPosition ? stopPrice < entryPrice : stopPrice > entryPrice;
            })
            .WithMessage(isLongPosition 
                ? "Stop price must be below entry price for long positions"
                : "Stop price must be above entry price for short positions");
    }

    #endregion

    #region Quantity Validation

    /// <summary>
    /// Validates that a quantity is positive and within reasonable bounds.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="allowFractional">Whether fractional quantities are allowed</param>
    /// <param name="maxQuantity">Maximum allowed quantity (default 1,000,000)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidTradingQuantity<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        bool allowFractional = false,
        decimal maxQuantity = 1000000m)
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage("Quantity must be positive")
            .LessThanOrEqualTo(maxQuantity)
            .WithMessage($"Quantity cannot exceed {maxQuantity:N0}")
            .Must(quantity => quantity.IsValidQuantity(allowFractional))
            .WithMessage(allowFractional 
                ? "Quantity must be a positive number"
                : "Quantity must be a positive whole number");
    }

    /// <summary>
    /// Validates that a position size is appropriate for the account size.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="accountValueSelector">Function to get account value</param>
    /// <param name="priceSelector">Function to get price per share</param>
    /// <param name="maxPositionPercent">Maximum position size as percentage of account (default 25%)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidPositionSize<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        Func<T, decimal> accountValueSelector,
        Func<T, decimal> priceSelector,
        decimal maxPositionPercent = 25m)
    {
        return ruleBuilder
            .ValidTradingQuantity()
            .Must((instance, quantity) =>
            {
                var accountValue = accountValueSelector(instance);
                var price = priceSelector(instance);
                
                if (accountValue <= 0 || price <= 0) return false;

                var positionValue = quantity * price;
                var positionPercent = (positionValue / accountValue) * 100m;

                return positionPercent <= maxPositionPercent;
            })
            .WithMessage($"Position size cannot exceed {maxPositionPercent}% of account value");
    }

    #endregion

    #region Symbol Validation

    /// <summary>
    /// Validates that a trading symbol follows standard conventions.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="maxLength">Maximum symbol length (default 10)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, string> ValidTradingSymbol<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        int maxLength = 10)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Trading symbol is required")
            .Length(1, maxLength)
            .WithMessage($"Trading symbol must be between 1 and {maxLength} characters")
            .Matches("^[A-Z][A-Z0-9]*$")
            .WithMessage("Trading symbol must start with a letter and contain only uppercase letters and numbers")
            .Must(symbol => !IsReservedSymbol(symbol))
            .WithMessage("Symbol is reserved and cannot be used for trading");
    }

    /// <summary>
    /// Validates that a symbol list contains valid trading symbols.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="maxSymbols">Maximum number of symbols allowed (default 100)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, IEnumerable<string>> ValidTradingSymbolList<T>(
        this IRuleBuilder<T, IEnumerable<string>> ruleBuilder,
        int maxSymbols = 100)
    {
        return ruleBuilder
            .NotNull()
            .WithMessage("Symbol list cannot be null")
            .Must(symbols => symbols.Count() <= maxSymbols)
            .WithMessage($"Cannot specify more than {maxSymbols} symbols")
            .Must(symbols => symbols.All(s => !string.IsNullOrWhiteSpace(s)))
            .WithMessage("All symbols must be non-empty")
            .Must(symbols => symbols.All(s => s.Length <= 10))
            .WithMessage("All symbols must be 10 characters or less")
            .Must(symbols => symbols.Distinct(StringComparer.OrdinalIgnoreCase).Count() == symbols.Count())
            .WithMessage("Symbol list cannot contain duplicates");
    }

    #endregion

    #region Order Validation

    /// <summary>
    /// Validates that an order ID follows standard conventions.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, string> ValidOrderId<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Order ID is required")
            .Length(3, 50)
            .WithMessage("Order ID must be between 3 and 50 characters")
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Order ID can only contain letters, numbers, underscores, and hyphens");
    }

    /// <summary>
    /// Validates that an account ID follows standard conventions.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, string> ValidAccountId<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Account ID is required")
            .Length(5, 20)
            .WithMessage("Account ID must be between 5 and 20 characters")
            .Matches("^[A-Za-z0-9]+$")
            .WithMessage("Account ID can only contain letters and numbers");
    }

    #endregion

    #region Date and Time Validation

    /// <summary>
    /// Validates that a timestamp is recent and not in the future.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="maxAge">Maximum allowed age (default 1 hour)</param>
    /// <param name="allowFuture">Whether future timestamps are allowed (default false)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, DateTime> ValidTradingTimestamp<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        TimeSpan? maxAge = null,
        bool allowFuture = false)
    {
        var maxTimestamp = maxAge ?? TimeSpan.FromHours(1);
        
        return ruleBuilder
            .GreaterThan(DateTime.UtcNow.AddYears(-5))
            .WithMessage("Timestamp cannot be more than 5 years in the past")
            .Must(timestamp => allowFuture || timestamp <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future")
            .Must(timestamp => DateTime.UtcNow - timestamp <= maxTimestamp)
            .WithMessage($"Timestamp cannot be older than {maxTimestamp.TotalHours:F1} hours");
    }

    /// <summary>
    /// Validates that a date range is valid for trading operations.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="endDateSelector">Function to get end date</param>
    /// <param name="maxRangeDays">Maximum allowed range in days (default 365)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, DateTime> ValidTradingDateRange<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        Func<T, DateTime> endDateSelector,
        int maxRangeDays = 365)
    {
        return ruleBuilder
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Start date cannot be in the future")
            .Must((instance, startDate) =>
            {
                var endDate = endDateSelector(instance);
                return startDate <= endDate;
            })
            .WithMessage("Start date must be before or equal to end date")
            .Must((instance, startDate) =>
            {
                var endDate = endDateSelector(instance);
                var daysDiff = (endDate - startDate).TotalDays;
                return daysDiff <= maxRangeDays;
            })
            .WithMessage($"Date range cannot exceed {maxRangeDays} days");
    }

    #endregion

    #region Risk Management Validation

    /// <summary>
    /// Validates that a risk percentage is within acceptable bounds.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="maxRiskPercent">Maximum risk percentage (default 5%)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidRiskPercentage<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        decimal maxRiskPercent = 5m)
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage("Risk percentage must be positive")
            .LessThanOrEqualTo(maxRiskPercent)
            .WithMessage($"Risk percentage cannot exceed {maxRiskPercent}%")
            .Must(risk => risk % 0.01m == 0)
            .WithMessage("Risk percentage must be specified to 2 decimal places");
    }

    /// <summary>
    /// Validates that leverage is within regulatory and risk limits.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="maxLeverage">Maximum allowed leverage (default 4:1)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidLeverage<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        decimal maxLeverage = 4m)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithMessage("Leverage must be at least 1:1")
            .LessThanOrEqualTo(maxLeverage)
            .WithMessage($"Leverage cannot exceed {maxLeverage}:1")
            .Must(leverage => leverage.ToFinancialPrecision(2) == leverage)
            .WithMessage("Leverage must be specified to 2 decimal places");
    }

    #endregion

    #region Market Data Validation

    /// <summary>
    /// Validates that market data prices are consistent (high >= low, etc.).
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="highSelector">Function to get high price</param>
    /// <param name="lowSelector">Function to get low price</param>
    /// <param name="openSelector">Function to get open price</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, decimal> ValidMarketDataPrices<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        Func<T, decimal> highSelector,
        Func<T, decimal> lowSelector,
        Func<T, decimal> openSelector)
    {
        return ruleBuilder
            .ValidTradingPrice()
            .Must((instance, closePrice) =>
            {
                var high = highSelector(instance);
                var low = lowSelector(instance);
                var open = openSelector(instance);

                // Validate price relationships
                return high >= low && 
                       closePrice >= low && closePrice <= high &&
                       open >= low && open <= high;
            })
            .WithMessage("Market data prices are inconsistent (high must be >= low, close and open must be between high and low)");
    }

    /// <summary>
    /// Validates that volume is reasonable for trading.
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="ruleBuilder">Rule builder</param>
    /// <param name="minVolume">Minimum required volume (default 1000)</param>
    /// <param name="maxVolume">Maximum reasonable volume (default 1 billion)</param>
    /// <returns>Rule builder for chaining</returns>
    public static IRuleBuilderOptions<T, long> ValidTradingVolume<T>(
        this IRuleBuilder<T, long> ruleBuilder,
        long minVolume = 1000,
        long maxVolume = 1000000000)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("Volume cannot be negative")
            .LessThanOrEqualTo(maxVolume)
            .WithMessage($"Volume cannot exceed {maxVolume:N0}")
            .Must(volume => volume >= minVolume || volume == 0)
            .WithMessage($"Volume must be 0 or at least {minVolume:N0} for liquid trading");
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Checks if a symbol is reserved and cannot be used for trading.
    /// </summary>
    /// <param name="symbol">Symbol to check</param>
    /// <returns>True if symbol is reserved</returns>
    private static bool IsReservedSymbol(string symbol)
    {
        var reservedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NULL", "TEST", "CASH", "MONEY", "INDEX", "FOREX", "CRYPTO", "ERROR", "INVALID"
        };

        return reservedSymbols.Contains(symbol);
    }

    #endregion
}