namespace TradingPlatform.Common.Mathematics;

/// <summary>
/// Enhanced financial mathematics library specifically designed for day trading operations.
/// All calculations use System.Decimal to ensure financial precision compliance.
/// Extends the basic FinancialMath from Core with trading-specific calculations.
/// </summary>
public static class TradingMath
{
    private const int DEFAULT_PRECISION = 10;
    private const decimal EPSILON = 0.0000000001m;

    #region Core Mathematical Functions

    /// <summary>
    /// Calculates square root using Newton's method with decimal precision.
    /// Replaces System.Math.Sqrt which returns double.
    /// </summary>
    /// <param name="value">Value to calculate square root for</param>
    /// <returns>Square root with decimal precision</returns>
    public static decimal Sqrt(decimal value)
    {
        if (value < 0m) throw new ArgumentException("Cannot calculate square root of negative number");
        if (value == 0m) return 0m;
        if (value == 1m) return 1m;

        decimal guess = value / 2m;
        decimal previousGuess;

        do
        {
            previousGuess = guess;
            guess = (guess + value / guess) / 2m;
        } while (Math.Abs(guess - previousGuess) > EPSILON);

        return Math.Round(guess, DEFAULT_PRECISION, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Calculates natural logarithm using decimal precision.
    /// Uses Taylor series expansion for decimal calculations.
    /// </summary>
    /// <param name="value">Value to calculate natural log for</param>
    /// <returns>Natural logarithm with decimal precision</returns>
    public static decimal Ln(decimal value)
    {
        if (value <= 0m) throw new ArgumentException("Cannot calculate logarithm of non-positive number");
        if (value == 1m) return 0m;

        // For values close to 1, use Taylor series: ln(1+x) = x - x²/2 + x³/3 - x⁴/4 + ...
        if (value > 0.5m && value < 1.5m)
        {
            decimal x = value - 1m;
            decimal result = 0m;
            decimal term = x;

            for (int n = 1; n <= 50 && Math.Abs(term) > EPSILON; n++)
            {
                result += term / n * (n % 2 == 1 ? 1 : -1);
                term *= x;
            }

            return Math.Round(result, DEFAULT_PRECISION, MidpointRounding.ToEven);
        }

        // For other values, use scaling and recursion
        if (value > 1.5m)
        {
            return Ln(value / 2m) + Ln(2m);
        }
        else
        {
            return -Ln(1m / value);
        }
    }

    #endregion

    #region Trading Performance Metrics

    /// <summary>
    /// Calculates Profit and Loss (PnL) for a trading position.
    /// Handles both long and short positions with commission costs.
    /// </summary>
    /// <param name="entryPrice">Price at position entry</param>
    /// <param name="exitPrice">Price at position exit</param>
    /// <param name="quantity">Number of shares/contracts</param>
    /// <param name="isLong">True for long position, false for short</param>
    /// <param name="commissionPerShare">Commission cost per share</param>
    /// <returns>Net PnL after commissions</returns>
    public static decimal CalculatePnL(decimal entryPrice, decimal exitPrice, decimal quantity, bool isLong = true, decimal commissionPerShare = 0m)
    {
        if (entryPrice <= 0m) throw new ArgumentException("Entry price must be positive");
        if (exitPrice <= 0m) throw new ArgumentException("Exit price must be positive");
        if (quantity <= 0m) throw new ArgumentException("Quantity must be positive");

        decimal grossPnL = isLong
            ? (exitPrice - entryPrice) * quantity
            : (entryPrice - exitPrice) * quantity;

        decimal totalCommission = commissionPerShare * quantity * 2m; // Entry + Exit

        return RoundFinancial(grossPnL - totalCommission);
    }

    /// <summary>
    /// Calculates percentage return on a trading position.
    /// Essential for comparing performance across different position sizes.
    /// </summary>
    /// <param name="entryPrice">Price at position entry</param>
    /// <param name="exitPrice">Price at position exit</param>
    /// <param name="isLong">True for long position, false for short</param>
    /// <returns>Percentage return (e.g., 5.25 for 5.25%)</returns>
    public static decimal CalculateReturn(decimal entryPrice, decimal exitPrice, bool isLong = true)
    {
        if (entryPrice <= 0m) throw new ArgumentException("Entry price must be positive");
        if (exitPrice <= 0m) throw new ArgumentException("Exit price must be positive");

        decimal returnPercent = isLong
            ? ((exitPrice - entryPrice) / entryPrice) * 100m
            : ((entryPrice - exitPrice) / entryPrice) * 100m;

        return RoundFinancial(returnPercent, 4);
    }

    /// <summary>
    /// Calculates maximum drawdown from a series of PnL values.
    /// Critical metric for risk assessment and strategy evaluation.
    /// </summary>
    /// <param name="pnlValues">Sequential PnL values (cumulative or individual)</param>
    /// <param name="isCumulative">Whether PnL values are cumulative or individual</param>
    /// <returns>Maximum drawdown as a positive percentage</returns>
    public static decimal CalculateMaxDrawdown(IEnumerable<decimal> pnlValues, bool isCumulative = true)
    {
        var pnlList = pnlValues.ToList();
        if (!pnlList.Any()) return 0m;

        // Convert to cumulative if needed
        var cumulativePnL = isCumulative
            ? pnlList
            : pnlList.Aggregate(new List<decimal>(), (acc, value) =>
            {
                acc.Add((acc.LastOrDefault() + value));
                return acc;
            });

        decimal maxDrawdown = 0m;
        decimal peak = cumulativePnL.First();

        foreach (var value in cumulativePnL)
        {
            if (value > peak)
            {
                peak = value;
            }
            else
            {
                decimal drawdown = peak - value;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return RoundFinancial(maxDrawdown, 4);
    }

    /// <summary>
    /// Calculates Sharpe ratio for a trading strategy.
    /// Measures risk-adjusted return performance.
    /// </summary>
    /// <param name="returns">Series of returns (as percentages)</param>
    /// <param name="riskFreeRate">Annual risk-free rate (as percentage)</param>
    /// <param name="tradingDaysPerYear">Number of trading days per year</param>
    /// <returns>Annualized Sharpe ratio</returns>
    public static decimal CalculateSharpeRatio(IEnumerable<decimal> returns, decimal riskFreeRate = 2m, int tradingDaysPerYear = 252)
    {
        var returnsList = returns.ToList();
        if (!returnsList.Any()) return 0m;

        decimal averageReturn = returnsList.Average();
        decimal standardDeviation = StandardDeviation(returnsList);

        if (standardDeviation == 0m) return 0m;

        decimal dailyRiskFreeRate = riskFreeRate / tradingDaysPerYear / 100m;
        decimal excessReturn = averageReturn - dailyRiskFreeRate;
        decimal annualizedExcessReturn = excessReturn * tradingDaysPerYear;
        decimal annualizedVolatility = standardDeviation * Sqrt(tradingDaysPerYear);

        return RoundFinancial(annualizedExcessReturn / annualizedVolatility, 4);
    }

    /// <summary>
    /// Calculates Value at Risk (VaR) for a given confidence level.
    /// Estimates potential loss over a specific time period.
    /// </summary>
    /// <param name="returns">Historical returns (as percentages)</param>
    /// <param name="confidenceLevel">Confidence level (e.g., 0.95 for 95%)</param>
    /// <param name="portfolioValue">Current portfolio value</param>
    /// <returns>VaR amount (positive value representing potential loss)</returns>
    public static decimal CalculateVaR(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m, decimal portfolioValue = 100000m)
    {
        var sortedReturns = returns.OrderBy(r => r).ToList();
        if (!sortedReturns.Any()) return 0m;

        int index = (int)Math.Floor((double)((1m - confidenceLevel) * sortedReturns.Count));
        index = Math.Max(0, Math.Min(index, sortedReturns.Count - 1));

        decimal varReturn = sortedReturns[index];
        decimal varAmount = Math.Abs(varReturn * portfolioValue / 100m);

        return RoundFinancial(varAmount);
    }

    #endregion

    #region Technical Analysis Calculations

    /// <summary>
    /// Calculates Volume Weighted Average Price (VWAP) for intraday trading.
    /// Essential for execution quality measurement and algorithmic trading.
    /// </summary>
    /// <param name="priceVolumeData">Collection of (price, volume) tuples</param>
    /// <returns>VWAP with financial precision</returns>
    public static decimal CalculateVWAP(IEnumerable<(decimal price, decimal volume)> priceVolumeData)
    {
        var data = priceVolumeData.ToList();
        if (!data.Any()) throw new ArgumentException("Price-volume data cannot be empty");

        decimal totalValue = data.Sum(pv => pv.price * pv.volume);
        decimal totalVolume = data.Sum(pv => pv.volume);

        if (totalVolume == 0m) throw new ArgumentException("Total volume cannot be zero");

        return RoundFinancial(totalValue / totalVolume);
    }

    /// <summary>
    /// Calculates Time Weighted Average Price (TWAP) for execution benchmarking.
    /// Used to measure execution performance against time-based benchmarks.
    /// </summary>
    /// <param name="pricesWithTimestamps">Collection of (price, timestamp) tuples</param>
    /// <returns>TWAP with financial precision</returns>
    public static decimal CalculateTWAP(IEnumerable<(decimal price, DateTime timestamp)> pricesWithTimestamps)
    {
        var data = pricesWithTimestamps.OrderBy(pt => pt.timestamp).ToList();
        if (data.Count < 2) return data.FirstOrDefault().price;

        decimal weightedSum = 0m;
        decimal totalWeight = 0m;

        for (int i = 0; i < data.Count - 1; i++)
        {
            decimal weight = (decimal)(data[i + 1].timestamp - data[i].timestamp).TotalSeconds;
            weightedSum += data[i].price * weight;
            totalWeight += weight;
        }

        return totalWeight > 0m ? RoundFinancial(weightedSum / totalWeight) : data.Last().price;
    }

    /// <summary>
    /// Calculates Relative Strength Index (RSI) for momentum analysis.
    /// Key technical indicator for identifying overbought/oversold conditions.
    /// </summary>
    /// <param name="prices">Price series for RSI calculation</param>
    /// <param name="period">RSI period (typically 14)</param>
    /// <returns>RSI value between 0 and 100</returns>
    public static decimal CalculateRSI(IEnumerable<decimal> prices, int period = 14)
    {
        var priceList = prices.ToList();
        if (priceList.Count < period + 1) throw new ArgumentException($"Need at least {period + 1} prices for RSI calculation");

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < priceList.Count; i++)
        {
            decimal change = priceList[i] - priceList[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        decimal avgGain = gains.Take(period).Average();
        decimal avgLoss = losses.Take(period).Average();

        // Use Wilder's smoothing method for subsequent values
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        if (avgLoss == 0m) return 100m;

        decimal rs = avgGain / avgLoss;
        decimal rsi = 100m - (100m / (1m + rs));

        return RoundFinancial(rsi, 2);
    }

    /// <summary>
    /// Calculates Bollinger Bands for volatility analysis.
    /// Returns upper band, middle band (SMA), and lower band.
    /// </summary>
    /// <param name="prices">Price series</param>
    /// <param name="period">Period for moving average</param>
    /// <param name="standardDeviations">Number of standard deviations for bands</param>
    /// <returns>Tuple of (upperBand, middleBand, lowerBand)</returns>
    public static (decimal upperBand, decimal middleBand, decimal lowerBand) CalculateBollingerBands(
        IEnumerable<decimal> prices, int period = 20, decimal standardDeviations = 2m)
    {
        var priceList = prices.ToList();
        if (priceList.Count < period) throw new ArgumentException($"Need at least {period} prices for Bollinger Bands");

        var recentPrices = priceList.TakeLast(period);
        decimal middleBand = recentPrices.Average();
        decimal stdDev = StandardDeviation(recentPrices);

        decimal upperBand = middleBand + (standardDeviations * stdDev);
        decimal lowerBand = middleBand - (standardDeviations * stdDev);

        return (
            RoundFinancial(upperBand),
            RoundFinancial(middleBand),
            RoundFinancial(lowerBand)
        );
    }

    #endregion

    #region Risk Management Calculations

    /// <summary>
    /// Calculates optimal position size using Kelly Criterion.
    /// Helps determine the optimal fraction of capital to risk per trade.
    /// </summary>
    /// <param name="winRate">Historical win rate (as decimal, e.g., 0.6 for 60%)</param>
    /// <param name="avgWin">Average winning trade amount</param>
    /// <param name="avgLoss">Average losing trade amount (positive value)</param>
    /// <returns>Kelly percentage (as decimal, e.g., 0.25 for 25%)</returns>
    public static decimal CalculateKellyPercent(decimal winRate, decimal avgWin, decimal avgLoss)
    {
        if (winRate < 0m || winRate > 1m) throw new ArgumentException("Win rate must be between 0 and 1");
        if (avgWin <= 0m) throw new ArgumentException("Average win must be positive");
        if (avgLoss <= 0m) throw new ArgumentException("Average loss must be positive");

        decimal lossRate = 1m - winRate;
        decimal kellyPercent = (winRate / avgLoss) - (lossRate / avgWin);

        // Cap at 25% for practical risk management
        return Math.Max(0m, Math.Min(0.25m, RoundFinancial(kellyPercent, 4)));
    }

    /// <summary>
    /// Calculates correlation coefficient between two price series.
    /// Used for portfolio diversification and pair trading strategies.
    /// </summary>
    /// <param name="series1">First price series</param>
    /// <param name="series2">Second price series</param>
    /// <returns>Correlation coefficient between -1 and 1</returns>
    public static decimal CalculateCorrelation(IEnumerable<decimal> series1, IEnumerable<decimal> series2)
    {
        var list1 = series1.ToList();
        var list2 = series2.ToList();

        if (list1.Count != list2.Count) throw new ArgumentException("Series must have equal length");
        if (list1.Count < 2) throw new ArgumentException("Need at least 2 data points");

        decimal mean1 = list1.Average();
        decimal mean2 = list2.Average();

        decimal numerator = 0m;
        decimal sumSquares1 = 0m;
        decimal sumSquares2 = 0m;

        for (int i = 0; i < list1.Count; i++)
        {
            decimal diff1 = list1[i] - mean1;
            decimal diff2 = list2[i] - mean2;

            numerator += diff1 * diff2;
            sumSquares1 += diff1 * diff1;
            sumSquares2 += diff2 * diff2;
        }

        decimal denominator = Sqrt(sumSquares1 * sumSquares2);

        return denominator == 0m ? 0m : RoundFinancial(numerator / denominator, 4);
    }

    #endregion

    #region Statistical Functions

    /// <summary>
    /// Calculates standard deviation with decimal precision.
    /// Essential for volatility calculations and risk metrics.
    /// </summary>
    /// <param name="values">Series of values</param>
    /// <param name="usePopulation">True for population std dev, false for sample</param>
    /// <returns>Standard deviation with decimal precision</returns>
    public static decimal StandardDeviation(IEnumerable<decimal> values, bool usePopulation = false)
    {
        var variance = Variance(values, usePopulation);
        return Sqrt(variance);
    }

    /// <summary>
    /// Calculates variance with decimal precision.
    /// </summary>
    /// <param name="values">Series of values</param>
    /// <param name="usePopulation">True for population variance, false for sample</param>
    /// <returns>Variance with decimal precision</returns>
    public static decimal Variance(IEnumerable<decimal> values, bool usePopulation = false)
    {
        var valueList = values.ToList();
        if (!valueList.Any()) return 0m;
        if (valueList.Count == 1 && !usePopulation) return 0m;

        var mean = valueList.Average();
        var sumOfSquaredDifferences = valueList.Sum(v => (v - mean) * (v - mean));
        var divisor = usePopulation ? valueList.Count : valueList.Count - 1;

        return RoundFinancial(sumOfSquaredDifferences / divisor);
    }

    /// <summary>
    /// Calculates moving average with specified period.
    /// </summary>
    /// <param name="values">Series of values</param>
    /// <param name="period">Moving average period</param>
    /// <returns>Series of moving averages</returns>
    public static IEnumerable<decimal> MovingAverage(IEnumerable<decimal> values, int period)
    {
        var valueList = values.ToList();
        var result = new List<decimal>();

        for (int i = period - 1; i < valueList.Count; i++)
        {
            var periodValues = valueList.Skip(i - period + 1).Take(period);
            result.Add(RoundFinancial(periodValues.Average()));
        }

        return result;
    }

    #endregion

    #region Utility Functions

    /// <summary>
    /// Rounds decimal value to financial precision (default 2 decimal places).
    /// Uses banker's rounding for consistent behavior.
    /// </summary>
    /// <param name="value">Value to round</param>
    /// <param name="decimals">Number of decimal places</param>
    /// <returns>Rounded value with financial precision</returns>
    public static decimal RoundFinancial(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Calculates percentage with financial precision.
    /// </summary>
    /// <param name="value">Numerator value</param>
    /// <param name="total">Denominator value</param>
    /// <param name="decimals">Precision for percentage</param>
    /// <returns>Percentage value (e.g., 5.25 for 5.25%)</returns>
    public static decimal CalculatePercentage(decimal value, decimal total, int decimals = 4)
    {
        if (total == 0m) return 0m;
        return RoundFinancial((value / total) * 100m, decimals);
    }

    /// <summary>
    /// Calculates percentage change between two values.
    /// </summary>
    /// <param name="oldValue">Original value</param>
    /// <param name="newValue">New value</param>
    /// <param name="decimals">Precision for percentage</param>
    /// <returns>Percentage change (e.g., 5.25 for 5.25% increase)</returns>
    public static decimal CalculatePercentageChange(decimal oldValue, decimal newValue, int decimals = 4)
    {
        if (oldValue == 0m) return 0m;
        return RoundFinancial(((newValue - oldValue) / oldValue) * 100m, decimals);
    }

    /// <summary>
    /// Clamps a value between minimum and maximum bounds.
    /// Useful for risk management and position sizing limits.
    /// </summary>
    /// <param name="value">Value to clamp</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <returns>Clamped value within bounds</returns>
    public static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (min > max) throw new ArgumentException("Minimum cannot be greater than maximum");
        return Math.Max(min, Math.Min(max, value));
    }

    #endregion
}