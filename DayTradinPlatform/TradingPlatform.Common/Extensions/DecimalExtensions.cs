using TradingPlatform.Common.Mathematics;

namespace TradingPlatform.Common.Extensions;

/// <summary>
/// Extension methods for decimal type to support trading-specific operations.
/// All methods maintain financial precision using System.Decimal arithmetic.
/// </summary>
public static class DecimalExtensions
{
    #region Financial Precision Operations

    /// <summary>
    /// Rounds decimal to financial precision (default 2 decimal places).
    /// Uses banker's rounding for consistent financial calculations.
    /// </summary>
    /// <param name="value">Value to round</param>
    /// <param name="decimals">Number of decimal places (default 2)</param>
    /// <returns>Rounded value with financial precision</returns>
    public static decimal ToFinancialPrecision(this decimal value, int decimals = 2)
    {
        return TradingMath.RoundFinancial(value, decimals);
    }

    /// <summary>
    /// Calculates percentage of total with financial precision.
    /// </summary>
    /// <param name="value">Part value</param>
    /// <param name="total">Total value</param>
    /// <param name="decimals">Precision for percentage (default 4)</param>
    /// <returns>Percentage (e.g., 25.5 for 25.5%)</returns>
    public static decimal PercentageOf(this decimal value, decimal total, int decimals = 4)
    {
        return TradingMath.CalculatePercentage(value, total, decimals);
    }

    /// <summary>
    /// Calculates percentage change from this value to target value.
    /// </summary>
    /// <param name="oldValue">Original value</param>
    /// <param name="newValue">New value</param>
    /// <param name="decimals">Precision for percentage (default 4)</param>
    /// <returns>Percentage change (positive for increase, negative for decrease)</returns>
    public static decimal PercentageChangeTo(this decimal oldValue, decimal newValue, int decimals = 4)
    {
        return TradingMath.CalculatePercentageChange(oldValue, newValue, decimals);
    }

    #endregion

    #region Trading Calculations

    /// <summary>
    /// Calculates PnL for a long position from this entry price.
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="exitPrice">Exit price</param>
    /// <param name="quantity">Position size</param>
    /// <param name="commission">Commission per share (default 0)</param>
    /// <returns>Net PnL after commissions</returns>
    public static decimal LongPnL(this decimal entryPrice, decimal exitPrice, decimal quantity, decimal commission = 0m)
    {
        return TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, isLong: true, commission);
    }

    /// <summary>
    /// Calculates PnL for a short position from this entry price.
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="exitPrice">Exit price</param>
    /// <param name="quantity">Position size</param>
    /// <param name="commission">Commission per share (default 0)</param>
    /// <returns>Net PnL after commissions</returns>
    public static decimal ShortPnL(this decimal entryPrice, decimal exitPrice, decimal quantity, decimal commission = 0m)
    {
        return TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, isLong: false, commission);
    }

    /// <summary>
    /// Calculates return percentage for a long position from this entry price.
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="exitPrice">Exit price</param>
    /// <returns>Return percentage</returns>
    public static decimal LongReturn(this decimal entryPrice, decimal exitPrice)
    {
        return TradingMath.CalculateReturn(entryPrice, exitPrice, isLong: true);
    }

    /// <summary>
    /// Calculates return percentage for a short position from this entry price.
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="exitPrice">Exit price</param>
    /// <returns>Return percentage</returns>
    public static decimal ShortReturn(this decimal entryPrice, decimal exitPrice)
    {
        return TradingMath.CalculateReturn(entryPrice, exitPrice, isLong: false);
    }

    #endregion

    #region Risk Management

    /// <summary>
    /// Clamps value between minimum and maximum bounds.
    /// Useful for position sizing and risk limits.
    /// </summary>
    /// <param name="value">Value to clamp</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <returns>Clamped value within bounds</returns>
    public static decimal Clamp(this decimal value, decimal min, decimal max)
    {
        return TradingMath.Clamp(value, min, max);
    }

    /// <summary>
    /// Calculates position size based on risk percentage of account.
    /// </summary>
    /// <param name="accountValue">Total account value</param>
    /// <param name="riskPercent">Risk percentage as decimal (e.g., 0.02 for 2%)</param>
    /// <param name="entryPrice">Entry price per share</param>
    /// <param name="stopPrice">Stop loss price per share</param>
    /// <returns>Number of shares to buy based on risk management</returns>
    public static decimal CalculatePositionSize(this decimal accountValue, decimal riskPercent, decimal entryPrice, decimal stopPrice)
    {
        if (riskPercent <= 0m || riskPercent > 1m) 
            throw new ArgumentException("Risk percent must be between 0 and 1");
        
        if (entryPrice <= 0m || stopPrice <= 0m) 
            throw new ArgumentException("Prices must be positive");

        decimal riskPerShare = Math.Abs(entryPrice - stopPrice);
        if (riskPerShare == 0m) return 0m;

        decimal maxRisk = accountValue * riskPercent;
        decimal positionSize = maxRisk / riskPerShare;

        return positionSize.ToFinancialPrecision(0); // Round to whole shares
    }

    #endregion

    #region Validation and Comparison

    /// <summary>
    /// Checks if value is within specified tolerance of target value.
    /// Useful for price level comparisons with floating-point precision issues.
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="target">Target value</param>
    /// <param name="tolerance">Tolerance for comparison (default 0.01)</param>
    /// <returns>True if within tolerance</returns>
    public static bool IsNearlyEqual(this decimal value, decimal target, decimal tolerance = 0.01m)
    {
        return Math.Abs(value - target) <= tolerance;
    }

    /// <summary>
    /// Checks if value represents a valid price (positive and reasonable).
    /// </summary>
    /// <param name="value">Price value to validate</param>
    /// <param name="maxPrice">Maximum reasonable price (default 100,000)</param>
    /// <returns>True if valid price</returns>
    public static bool IsValidPrice(this decimal value, decimal maxPrice = 100000m)
    {
        return value > 0m && value <= maxPrice && value == value.ToFinancialPrecision();
    }

    /// <summary>
    /// Checks if value represents a valid quantity (positive integer or fractional).
    /// </summary>
    /// <param name="value">Quantity value to validate</param>
    /// <param name="allowFractional">Whether fractional quantities are allowed</param>
    /// <returns>True if valid quantity</returns>
    public static bool IsValidQuantity(this decimal value, bool allowFractional = false)
    {
        if (value <= 0m) return false;
        
        return allowFractional || value == Math.Floor(value);
    }

    #endregion

    #region Formatting and Display

    /// <summary>
    /// Formats decimal as currency string with appropriate precision.
    /// </summary>
    /// <param name="value">Value to format</param>
    /// <param name="currencySymbol">Currency symbol (default $)</param>
    /// <param name="includeSign">Whether to include + for positive values</param>
    /// <returns>Formatted currency string</returns>
    public static string ToCurrency(this decimal value, string currencySymbol = "$", bool includeSign = false)
    {
        var formatted = value.ToFinancialPrecision().ToString("N2");
        var sign = "";
        
        if (includeSign && value > 0m)
            sign = "+";
        else if (value < 0m)
            sign = "-";

        var absFormatted = Math.Abs(value).ToFinancialPrecision().ToString("N2");
        return $"{sign}{currencySymbol}{absFormatted}";
    }

    /// <summary>
    /// Formats decimal as percentage string.
    /// </summary>
    /// <param name="value">Percentage value (e.g., 5.25 for 5.25%)</param>
    /// <param name="decimals">Number of decimal places (default 2)</param>
    /// <param name="includeSign">Whether to include + for positive values</param>
    /// <returns>Formatted percentage string</returns>
    public static string ToPercentage(this decimal value, int decimals = 2, bool includeSign = false)
    {
        var formatted = value.ToFinancialPrecision(decimals).ToString($"F{decimals}");
        var sign = includeSign && value > 0m ? "+" : "";
        return $"{sign}{formatted}%";
    }

    /// <summary>
    /// Formats decimal with thousands separators for display.
    /// </summary>
    /// <param name="value">Value to format</param>
    /// <param name="decimals">Number of decimal places (default 2)</param>
    /// <returns>Formatted string with thousands separators</returns>
    public static string ToThousands(this decimal value, int decimals = 2)
    {
        return value.ToFinancialPrecision(decimals).ToString($"N{decimals}");
    }

    #endregion

    #region Mathematical Operations

    /// <summary>
    /// Calculates square root of decimal value.
    /// </summary>
    /// <param name="value">Value to calculate square root for</param>
    /// <returns>Square root with decimal precision</returns>
    public static decimal Sqrt(this decimal value)
    {
        return TradingMath.Sqrt(value);
    }

    /// <summary>
    /// Raises decimal to specified power using repeated multiplication.
    /// Limited to reasonable integer powers for performance.
    /// </summary>
    /// <param name="value">Base value</param>
    /// <param name="power">Integer power (limited to -10 to 10)</param>
    /// <returns>Value raised to power</returns>
    public static decimal Power(this decimal value, int power)
    {
        if (power < -10 || power > 10)
            throw new ArgumentException("Power must be between -10 and 10 for decimal calculations");

        if (power == 0) return 1m;
        if (power == 1) return value;

        if (power > 0)
        {
            decimal result = 1m;
            for (int i = 0; i < power; i++)
            {
                result *= value;
            }
            return result;
        }
        else
        {
            return 1m / value.Power(-power);
        }
    }

    /// <summary>
    /// Calculates absolute value.
    /// </summary>
    /// <param name="value">Value to get absolute value for</param>
    /// <returns>Absolute value</returns>
    public static decimal Abs(this decimal value)
    {
        return Math.Abs(value);
    }

    #endregion

    #region Range and Statistics

    /// <summary>
    /// Calculates the range (difference between max and min) of values.
    /// </summary>
    /// <param name="values">Collection of decimal values</param>
    /// <returns>Range (max - min)</returns>
    public static decimal Range(this IEnumerable<decimal> values)
    {
        var valuesList = values.ToList();
        if (!valuesList.Any()) return 0m;
        
        return valuesList.Max() - valuesList.Min();
    }

    /// <summary>
    /// Calculates percentile value from sorted collection.
    /// </summary>
    /// <param name="values">Collection of decimal values</param>
    /// <param name="percentile">Percentile to calculate (0-100)</param>
    /// <returns>Percentile value</returns>
    public static decimal Percentile(this IEnumerable<decimal> values, decimal percentile)
    {
        if (percentile < 0m || percentile > 100m)
            throw new ArgumentException("Percentile must be between 0 and 100");

        var sortedValues = values.OrderBy(v => v).ToList();
        if (!sortedValues.Any()) return 0m;
        if (sortedValues.Count == 1) return sortedValues[0];

        decimal index = (percentile / 100m) * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        decimal weight = index - lowerIndex;
        return sortedValues[lowerIndex] * (1m - weight) + sortedValues[upperIndex] * weight;
    }

    #endregion
}