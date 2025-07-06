using System;

namespace TradingPlatform.StrategyEngine.Utilities;

/// <summary>
/// Financial mathematics utility class following industry standards for decimal precision
/// All calculations use System.Decimal to avoid floating-point precision issues
/// Implements banker's rounding and proper financial calculation standards
/// </summary>
public static class FinancialMath
{
    /// <summary>
    /// Standard financial decimal places (4 for high precision)
    /// </summary>
    public const int StandardDecimalPlaces = 4;
    
    /// <summary>
    /// Currency decimal places (2 for most currencies)
    /// </summary>
    public const int CurrencyDecimalPlaces = 2;
    
    /// <summary>
    /// Default rounding strategy (Banker's rounding / Round to Even)
    /// </summary>
    public const MidpointRounding DefaultRounding = MidpointRounding.ToEven;

    #region Basic Financial Operations

    /// <summary>
    /// Calculates percentage change with decimal precision
    /// </summary>
    /// <param name="oldValue">Original value</param>
    /// <param name="newValue">New value</param>
    /// <returns>Percentage change as decimal (e.g., 0.05m for 5%)</returns>
    public static decimal PercentageChange(decimal oldValue, decimal newValue)
    {
        if (oldValue == 0m)
        {
            return newValue == 0m ? 0m : decimal.MaxValue; // Infinite change
        }
        
        return (newValue - oldValue) / decimal.Abs(oldValue);
    }

    /// <summary>
    /// Calculates drawdown from peak value
    /// </summary>
    /// <param name="peakValue">Peak value</param>
    /// <param name="currentValue">Current value</param>
    /// <returns>Drawdown as percentage (0.0 to 1.0)</returns>
    public static decimal CalculateDrawdown(decimal peakValue, decimal currentValue)
    {
        if (peakValue <= 0m) return 0m;
        if (currentValue >= peakValue) return 0m;
        
        return (peakValue - currentValue) / peakValue;
    }

    /// <summary>
    /// Calculates position sizing based on risk percentage
    /// </summary>
    /// <param name="accountBalance">Total account balance</param>
    /// <param name="riskPercentage">Risk percentage (e.g., 0.02m for 2%)</param>
    /// <param name="entryPrice">Entry price per share</param>
    /// <param name="stopLossPrice">Stop loss price per share</param>
    /// <returns>Position size in shares</returns>
    public static decimal CalculatePositionSize(decimal accountBalance, decimal riskPercentage, 
        decimal entryPrice, decimal stopLossPrice)
    {
        if (accountBalance <= 0m || riskPercentage <= 0m || entryPrice <= 0m)
            return 0m;
            
        var riskAmount = accountBalance * riskPercentage;
        var priceRisk = decimal.Abs(entryPrice - stopLossPrice);
        
        if (priceRisk <= 0m) return 0m;
        
        var positionSize = riskAmount / priceRisk;
        return RoundFinancial(positionSize, 0); // Round to whole shares
    }

    /// <summary>
    /// Calculates Sharpe ratio with decimal precision
    /// </summary>
    /// <param name="averageReturn">Average return per period</param>
    /// <param name="standardDeviation">Standard deviation of returns</param>
    /// <param name="riskFreeRate">Risk-free rate per period</param>
    /// <returns>Sharpe ratio</returns>
    public static decimal CalculateSharpeRatio(decimal averageReturn, decimal standardDeviation, decimal riskFreeRate = 0m)
    {
        if (standardDeviation <= 0m) return 0m;
        
        return (averageReturn - riskFreeRate) / standardDeviation;
    }

    /// <summary>
    /// Calculates compound annual growth rate (CAGR)
    /// </summary>
    /// <param name="beginningValue">Starting value</param>
    /// <param name="endingValue">Ending value</param>
    /// <param name="numberOfYears">Number of years</param>
    /// <returns>CAGR as decimal percentage</returns>
    public static decimal CalculateCAGR(decimal beginningValue, decimal endingValue, decimal numberOfYears)
    {
        if (beginningValue <= 0m || numberOfYears <= 0m) return 0m;
        if (endingValue <= 0m) return -1m; // Total loss
        
        // CAGR = (Ending Value / Beginning Value)^(1/n) - 1
        // Using decimal approximation for power function
        var ratio = endingValue / beginningValue;
        var power = 1m / numberOfYears;
        
        // Approximation using Newton's method for nth root
        var result = DecimalPower(ratio, power);
        return result - 1m;
    }

    #endregion

    #region Advanced Financial Calculations

    /// <summary>
    /// Calculates Value at Risk (VaR) using historical method
    /// </summary>
    /// <param name="returns">Historical returns</param>
    /// <param name="confidenceLevel">Confidence level (e.g., 0.95m for 95%)</param>
    /// <returns>VaR as positive value</returns>
    public static decimal CalculateVaR(decimal[] returns, decimal confidenceLevel = 0.95m)
    {
        if (returns == null || returns.Length == 0) return 0m;
        
        var sortedReturns = returns.OrderBy(r => r).ToArray();
        var index = (int)((1m - confidenceLevel) * returns.Length);
        index = Math.Max(0, Math.Min(index, returns.Length - 1));
        
        return decimal.Abs(sortedReturns[index]);
    }

    /// <summary>
    /// Calculates Expected Shortfall (Conditional VaR)
    /// </summary>
    /// <param name="returns">Historical returns</param>
    /// <param name="confidenceLevel">Confidence level (e.g., 0.95m for 95%)</param>
    /// <returns>Expected Shortfall as positive value</returns>
    public static decimal CalculateExpectedShortfall(decimal[] returns, decimal confidenceLevel = 0.95m)
    {
        if (returns == null || returns.Length == 0) return 0m;
        
        var var = CalculateVaR(returns, confidenceLevel);
        var worstReturns = returns.Where(r => r <= -var).ToArray();
        
        if (worstReturns.Length == 0) return var;
        
        return decimal.Abs(worstReturns.Average());
    }

    /// <summary>
    /// Calculates beta coefficient relative to market
    /// </summary>
    /// <param name="assetReturns">Asset returns</param>
    /// <param name="marketReturns">Market returns</param>
    /// <returns>Beta coefficient</returns>
    public static decimal CalculateBeta(decimal[] assetReturns, decimal[] marketReturns)
    {
        if (assetReturns == null || marketReturns == null || 
            assetReturns.Length != marketReturns.Length || assetReturns.Length == 0)
            return 1m; // Default beta
        
        var covariance = CalculateCovariance(assetReturns, marketReturns);
        var marketVariance = CalculateVariance(marketReturns);
        
        if (marketVariance <= 0m) return 1m;
        
        return covariance / marketVariance;
    }

    #endregion

    #region Statistical Functions with Decimal Precision

    /// <summary>
    /// Calculates variance with decimal precision
    /// </summary>
    public static decimal CalculateVariance(decimal[] values)
    {
        if (values == null || values.Length == 0) return 0m;
        
        var mean = values.Average();
        var sumOfSquaredDeviations = values.Sum(v => (v - mean) * (v - mean));
        
        return sumOfSquaredDeviations / values.Length;
    }

    /// <summary>
    /// Calculates standard deviation with decimal precision
    /// </summary>
    public static decimal CalculateStandardDeviation(decimal[] values)
    {
        var variance = CalculateVariance(values);
        return DecimalSqrt(variance);
    }

    /// <summary>
    /// Calculates covariance between two series
    /// </summary>
    public static decimal CalculateCovariance(decimal[] series1, decimal[] series2)
    {
        if (series1 == null || series2 == null || 
            series1.Length != series2.Length || series1.Length == 0)
            return 0m;
        
        var mean1 = series1.Average();
        var mean2 = series2.Average();
        
        var covariance = 0m;
        for (int i = 0; i < series1.Length; i++)
        {
            covariance += (series1[i] - mean1) * (series2[i] - mean2);
        }
        
        return covariance / series1.Length;
    }

    /// <summary>
    /// Calculates correlation coefficient
    /// </summary>
    public static decimal CalculateCorrelation(decimal[] series1, decimal[] series2)
    {
        var covariance = CalculateCovariance(series1, series2);
        var stdDev1 = CalculateStandardDeviation(series1);
        var stdDev2 = CalculateStandardDeviation(series2);
        
        if (stdDev1 <= 0m || stdDev2 <= 0m) return 0m;
        
        return covariance / (stdDev1 * stdDev2);
    }

    #endregion

    #region Decimal Math Utilities

    /// <summary>
    /// Decimal square root approximation using Newton's method
    /// </summary>
    public static decimal DecimalSqrt(decimal value)
    {
        if (value <= 0m) return 0m;
        if (value == 1m) return 1m;
        
        decimal x = value / 2m; // Initial guess
        decimal lastX;
        
        do
        {
            lastX = x;
            x = (x + value / x) / 2m;
        }
        while (decimal.Abs(x - lastX) > 0.0001m);
        
        return RoundFinancial(x, StandardDecimalPlaces);
    }

    /// <summary>
    /// Decimal power approximation for positive exponents
    /// </summary>
    public static decimal DecimalPower(decimal baseValue, decimal exponent)
    {
        if (baseValue <= 0m) return 0m;
        if (exponent == 0m) return 1m;
        if (exponent == 1m) return baseValue;
        
        // For fractional exponents, use approximation
        if (exponent < 1m)
        {
            // nth root approximation
            var iterations = 20;
            var guess = baseValue;
            
            for (int i = 0; i < iterations; i++)
            {
                var power = DecimalPowerInteger(guess, (int)(1m / exponent));
                var derivative = (1m / exponent) * DecimalPowerInteger(guess, (int)(1m / exponent) - 1);
                
                if (derivative == 0m) break;
                
                guess = guess - (power - baseValue) / derivative;
            }
            
            return RoundFinancial(guess, StandardDecimalPlaces);
        }
        
        // For integer exponents
        return DecimalPowerInteger(baseValue, (int)exponent);
    }

    /// <summary>
    /// Decimal power for integer exponents
    /// </summary>
    private static decimal DecimalPowerInteger(decimal baseValue, int exponent)
    {
        if (exponent == 0) return 1m;
        if (exponent == 1) return baseValue;
        if (exponent < 0) return 1m / DecimalPowerInteger(baseValue, -exponent);
        
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
        {
            result *= baseValue;
        }
        
        return result;
    }

    /// <summary>
    /// Rounds decimal to specified places using banker's rounding
    /// </summary>
    public static decimal RoundFinancial(decimal value, int decimalPlaces = StandardDecimalPlaces)
    {
        return decimal.Round(value, decimalPlaces, DefaultRounding);
    }

    /// <summary>
    /// Rounds to currency precision (2 decimal places)
    /// </summary>
    public static decimal RoundCurrency(decimal value)
    {
        return decimal.Round(value, CurrencyDecimalPlaces, DefaultRounding);
    }

    /// <summary>
    /// Checks if a decimal value is approximately equal within tolerance
    /// </summary>
    public static bool IsApproximatelyEqual(decimal value1, decimal value2, decimal tolerance = 0.0001m)
    {
        return decimal.Abs(value1 - value2) <= tolerance;
    }

    #endregion

    #region Validation Utilities

    /// <summary>
    /// Validates that a value is a valid financial amount
    /// </summary>
    public static bool IsValidFinancialAmount(decimal value)
    {
        return !decimal.IsNaN(value) && 
               !decimal.IsInfinity(value) && 
               decimal.Abs(value) < decimal.MaxValue / 2m; // Leave room for calculations
    }

    /// <summary>
    /// Validates that a percentage is in valid range (0-100% as 0.0-1.0)
    /// </summary>
    public static bool IsValidPercentage(decimal percentage)
    {
        return percentage >= 0m && percentage <= 1m;
    }

    /// <summary>
    /// Validates that a price is positive and reasonable
    /// </summary>
    public static bool IsValidPrice(decimal price)
    {
        return price > 0m && price < 1_000_000m; // Reasonable upper bound
    }

    #endregion
}