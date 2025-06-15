// File: TradingPlatform.Core\Models\DailyData.cs

using System;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Represents daily OHLCV (Open, High, Low, Close, Volume) data for a financial instrument.
    /// Used for historical data analysis, backtesting, and technical indicator calculations.
    /// Follows financial calculation standards with decimal precision for all price values.
    /// </summary>
    public class DailyData
    {
        /// <summary>
        /// The financial instrument symbol this daily data represents.
        /// Must match the symbol format used throughout the trading platform.
        /// </summary>
        public required string Symbol { get; set; }

        /// <summary>
        /// The trading date for this daily data.
        /// Represents the calendar date of the trading session.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Opening price for the trading day.
        /// The first traded price when the market opened.
        /// </summary>
        public decimal Open { get; set; }

        /// <summary>
        /// Highest price during the trading day.
        /// The maximum price reached during the trading session.
        /// </summary>
        public decimal High { get; set; }

        /// <summary>
        /// Lowest price during the trading day.
        /// The minimum price reached during the trading session.
        /// </summary>
        public decimal Low { get; set; }

        /// <summary>
        /// Closing price for the trading day.
        /// The last traded price when the market closed.
        /// </summary>
        public decimal Close { get; set; }

        /// <summary>
        /// Adjusted closing price accounting for stock splits, dividends, and other corporate actions.
        /// Used for accurate historical analysis and backtesting.
        /// </summary>
        public decimal AdjustedClose { get; set; }

        /// <summary>
        /// Total volume of shares traded during the day.
        /// Indicates the liquidity and interest in the security.
        /// </summary>
        public long Volume { get; set; }

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Daily price change (Close - Open).
        /// Positive values indicate price appreciation, negative indicate decline.
        /// </summary>
        public decimal DayChange => Close - Open;

        /// <summary>
        /// Daily percentage change ((Close - Open) / Open * 100).
        /// Returns 0 if Open is 0 to avoid division by zero.
        /// </summary>
        public decimal DayChangePercent => Open != 0 ? (DayChange / Open) * 100 : 0;

        /// <summary>
        /// Daily trading range (High - Low).
        /// Indicates the volatility and price movement during the trading day.
        /// </summary>
        public decimal Range => High - Low;

        /// <summary>
        /// True Range calculation for volatility analysis.
        /// Max of: (High - Low), |High - PreviousClose|, |Low - PreviousClose|
        /// Requires previous day's close for accurate calculation.
        /// </summary>
        /// <param name="previousClose">Previous trading day's closing price</param>
        /// <returns>True Range value for volatility calculations</returns>
        public decimal GetTrueRange(decimal previousClose)
        {
            var highLow = High - Low;
            var highPrevClose = Math.Abs(High - previousClose);
            var lowPrevClose = Math.Abs(Low - previousClose);

            return Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
        }

        /// <summary>
        /// Position of close within the day's range (0-100%).
        /// 0% = close at day's low, 100% = close at day's high
        /// Returns 50% if range is 0 to avoid division by zero.
        /// </summary>
        public decimal ClosePositionInRange => Range != 0 ? ((Close - Low) / Range) * 100 : 50;

        /// <summary>
        /// Average price for the day ((High + Low + Close) / 3).
        /// Commonly used as a reference price for technical analysis.
        /// </summary>
        public decimal TypicalPrice => (High + Low + Close) / 3;

        /// <summary>
        /// Weighted close price ((High + Low + 2*Close) / 4).
        /// Gives more weight to the closing price in the average.
        /// </summary>
        public decimal WeightedClose => (High + Low + (2 * Close)) / 4;

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that the daily data is logically consistent.
        /// Checks price relationships, positive values, and reasonable date.
        /// </summary>
        /// <returns>True if the daily data passes validation checks</returns>
        public bool IsValid()
        {
            // Basic validation checks
            if (Open <= 0 || High <= 0 || Low <= 0 || Close <= 0 || Volume < 0)
                return false;

            // Logical relationship checks
            if (High < Low || High < Open || High < Close || Low > Open || Low > Close)
                return false;

            // Date should be reasonable (after 1990, not in the future)
            if (Date < new DateTime(1990, 1, 1) || Date > DateTime.Today.AddDays(1))
                return false;

            // AdjustedClose should be positive
            if (AdjustedClose <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// Determines if this is a bullish trading day (Close > Open).
        /// </summary>
        /// <returns>True if closing price is higher than opening price</returns>
        public bool IsBullish() => Close > Open;

        /// <summary>
        /// Determines if this is a bearish trading day (Close < Open).
        /// </summary>
        /// <returns>True if closing price is lower than opening price</returns>
        public bool IsBearish() => Close < Open;

        /// <summary>
        /// Determines if this is a doji trading day (Close approximately equals Open).
        /// </summary>
        /// <param name="tolerance">Tolerance percentage for doji detection (default 0.1%)</param>
        /// <returns>True if close and open are within tolerance</returns>
        public bool IsDoji(decimal tolerance = 0.1m)
        {
            if (Open == 0) return false;
            var changePercent = Math.Abs(DayChangePercent);
            return changePercent <= tolerance;
        }

        /// <summary>
        /// Creates a normalized copy with values rounded to appropriate decimal places.
        /// Prices rounded to 4 decimal places, percentages to 2 decimal places.
        /// </summary>
        /// <returns>A new DailyData with normalized values</returns>
        public DailyData Normalize()
        {
            return new DailyData
            {
                Symbol = Symbol,
                Date = Date,
                Open = Math.Round(Open, 4),
                High = Math.Round(High, 4),
                Low = Math.Round(Low, 4),
                Close = Math.Round(Close, 4),
                AdjustedClose = Math.Round(AdjustedClose, 4),
                Volume = Volume
            };
        }

        /// <summary>
        /// Compares this daily data with another for significant differences.
        /// Useful for detecting data inconsistencies between providers.
        /// </summary>
        /// <param name="other">Another DailyData to compare with</param>
        /// <param name="tolerancePercent">Tolerance percentage for price differences</param>
        /// <returns>True if daily data are similar within tolerance</returns>
        public bool IsSimilarTo(DailyData other, decimal tolerancePercent = 1.0m)
        {
            if (other == null || Symbol != other.Symbol || Date.Date != other.Date.Date)
                return false;

            var tolerance = tolerancePercent / 100m;

            return IsWithinTolerance(Open, other.Open, tolerance) &&
                   IsWithinTolerance(High, other.High, tolerance) &&
                   IsWithinTolerance(Low, other.Low, tolerance) &&
                   IsWithinTolerance(Close, other.Close, tolerance) &&
                   Math.Abs(Volume - other.Volume) <= (Math.Max(Volume, other.Volume) * tolerance);
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// Provides essential daily data information in a readable format.
        /// </summary>
        /// <returns>Formatted string representation of the daily data</returns>
        public override string ToString()
        {
            var direction = IsBullish() ? "↑" : IsBearish() ? "↓" : "→";
            return $"DailyData[{Symbol}]: {Date:yyyy-MM-dd} " +
                   $"O:${Open:F2} H:${High:F2} L:${Low:F2} C:${Close:F2} {direction} " +
                   $"{DayChangePercent:+0.00;-0.00;0.00}% Vol:{Volume:N0}";
        }

        /// <summary>
        /// Hash code generation for equality comparisons and collections.
        /// Based on symbol and date for uniqueness.
        /// </summary>
        /// <returns>Hash code for this daily data</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Symbol, Date.Date);
        }

        /// <summary>
        /// Equality comparison based on symbol and date.
        /// Two daily data objects are equal if they represent the same symbol on the same date.
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns>True if objects are equal</returns>
        public override bool Equals(object? obj)
        {
            if (obj is DailyData other)
            {
                return Symbol == other.Symbol && Date.Date == other.Date.Date;
            }
            return false;
        }

        // ========== PRIVATE HELPER METHODS ==========

        private bool IsWithinTolerance(decimal value1, decimal value2, decimal tolerance)
        {
            if (value1 == 0 && value2 == 0) return true;
            if (value1 == 0 || value2 == 0) return false;

            var difference = Math.Abs(value1 - value2);
            var average = (value1 + value2) / 2;
            return difference / average <= tolerance;
        }
    }
}

// Total Lines: 230
