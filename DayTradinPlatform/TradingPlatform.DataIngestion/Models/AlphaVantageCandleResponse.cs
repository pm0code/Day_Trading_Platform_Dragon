// File: TradingPlatform.DataIngestion\Models\AlphaVantageCandleResponse.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TradingPlatform.DataIngestion.Interfaces;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Represents a time series response from the AlphaVantage TIME_SERIES_DAILY API.
    /// Maps directly to the JSON structure returned by AlphaVantage's historical data endpoints.
    /// 
    /// AlphaVantage Time Series Daily API Response Structure:
    /// {
    ///   "Meta Data": {
    ///     "1. Information": "Daily Prices (open, high, low, close) and Volumes",
    ///     "2. Symbol": "AAPL",
    ///     "3. Last Refreshed": "2025-06-04",
    ///     "4. Output Size": "Full size",
    ///     "5. Time Zone": "US/Eastern"
    ///   },
    ///   "Time Series (Daily)": {
    ///     "2025-06-04": {
    ///       "1. open": "150.00",
    ///       "2. high": "152.00",
    ///       "3. low": "149.50",
    ///       "4. close": "151.25",
    ///       "5. volume": "1234567"
    ///     },
    ///     "2025-06-03": { ... }
    ///   }
    /// }
    /// 
    /// This model handles AlphaVantage's date-keyed dictionary structure for historical data,
    /// providing utilities for conversion to standard OHLCV formats used throughout the platform.
    /// </summary>
    public class AlphaVantageTimeSeriesResponse
    {
        /// <summary>
        /// Metadata information about the time series data.
        /// Maps to "Meta Data" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("Meta Data")]
        public AlphaVantageMetaData MetaData { get; set; } = new AlphaVantageMetaData();

        /// <summary>
        /// Dictionary of daily time series data keyed by date string.
        /// Maps to "Time Series (Daily)" field in AlphaVantage JSON response.
        /// Key format: "YYYY-MM-DD", Value: OHLCV data for that date.
        /// </summary>
        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageTimeSeries> TimeSeries { get; set; } = new Dictionary<string, AlphaVantageTimeSeries>();

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Number of data points in the time series.
        /// Indicates the amount of historical data available.
        /// </summary>
        [JsonIgnore]
        public int Count => TimeSeries?.Count ?? 0;

        /// <summary>
        /// Indicates if the response contains valid time series data.
        /// Checks for non-empty TimeSeries and valid MetaData.
        /// </summary>
        [JsonIgnore]
        public bool HasValidData => Count > 0 && MetaData != null && !string.IsNullOrWhiteSpace(MetaData.Symbol);

        /// <summary>
        /// Gets the date range covered by this time series data.
        /// Returns the span from earliest to latest date.
        /// </summary>
        [JsonIgnore]
        public TimeSpan DateRange
        {
            get
            {
                if (Count < 2) return TimeSpan.Zero;

                var dates = TimeSeries.Keys
                    .Where(k => DateTime.TryParse(k, out _))
                    .Select(DateTime.Parse)
                    .OrderBy(d => d)
                    .ToList();

                if (dates.Count < 2) return TimeSpan.Zero;
                return dates.Last() - dates.First();
            }
        }

        /// <summary>
        /// Gets the most recent (latest) date in the time series.
        /// Returns DateTime.MinValue if no valid dates are found.
        /// </summary>
        [JsonIgnore]
        public DateTime LatestDate
        {
            get
            {
                var latestDateString = TimeSeries?.Keys
                    .Where(k => DateTime.TryParse(k, out _))
                    .OrderByDescending(DateTime.Parse)
                    .FirstOrDefault();

                return latestDateString != null && DateTime.TryParse(latestDateString, out var result)
                    ? result : DateTime.MinValue;
            }
        }

        /// <summary>
        /// Gets the earliest date in the time series.
        /// Returns DateTime.MinValue if no valid dates are found.
        /// </summary>
        [JsonIgnore]
        public DateTime EarliestDate
        {
            get
            {
                var earliestDateString = TimeSeries?.Keys
                    .Where(k => DateTime.TryParse(k, out _))
                    .OrderBy(DateTime.Parse)
                    .FirstOrDefault();

                return earliestDateString != null && DateTime.TryParse(earliestDateString, out var result)
                    ? result : DateTime.MinValue;
            }
        }

        // ========== DATA ACCESS METHODS ==========

        /// <summary>
        /// Gets time series data for a specific date.
        /// Returns null if the date is not found in the data.
        /// </summary>
        /// <param name="date">Date to retrieve data for (YYYY-MM-DD format)</param>
        /// <returns>Time series data for the specified date, or null if not found</returns>
        public AlphaVantageTimeSeries? GetDataForDate(string date)
        {
            return TimeSeries?.TryGetValue(date, out var data) == true ? data : null;
        }

        /// <summary>
        /// Gets time series data for a specific DateTime.
        /// Converts DateTime to YYYY-MM-DD format for lookup.
        /// </summary>
        /// <param name="date">DateTime to retrieve data for</param>
        /// <returns>Time series data for the specified date, or null if not found</returns>
        public AlphaVantageTimeSeries? GetDataForDate(DateTime date)
        {
            return GetDataForDate(date.ToString("yyyy-MM-dd"));
        }

        /// <summary>
        /// Gets the most recent (latest) time series data point.
        /// Returns null if no data is available.
        /// </summary>
        /// <returns>The most recent time series data, or null if empty</returns>
        public AlphaVantageTimeSeries? GetLatestData()
        {
            if (LatestDate == DateTime.MinValue) return null;
            return GetDataForDate(LatestDate);
        }

        /// <summary>
        /// Gets time series data within a specific date range.
        /// Returns data sorted by date in ascending order.
        /// </summary>
        /// <param name="startDate">Start date for filtering (inclusive)</param>
        /// <param name="endDate">End date for filtering (inclusive)</param>
        /// <returns>List of time series data within the specified range</returns>
        public List<(DateTime Date, AlphaVantageTimeSeries Data)> GetDataInRange(DateTime startDate, DateTime endDate)
        {
            var result = new List<(DateTime, AlphaVantageTimeSeries)>();

            foreach (var kvp in TimeSeries)
            {
                if (DateTime.TryParse(kvp.Key, out var date))
                {
                    if (date >= startDate.Date && date <= endDate.Date)
                    {
                        result.Add((date, kvp.Value));
                    }
                }
            }

            return result.OrderBy(x => x.Date).ToList();
        }

        /// <summary>
        /// Gets the last N trading days of data.
        /// Returns data sorted by date in descending order (most recent first).
        /// </summary>
        /// <param name="numberOfDays">Number of trading days to retrieve</param>
        /// <returns>List of the most recent trading days data</returns>
        public List<(DateTime Date, AlphaVantageTimeSeries Data)> GetLastNDays(int numberOfDays)
        {
            var sortedData = TimeSeries
                .Where(kvp => DateTime.TryParse(kvp.Key, out _))
                .Select(kvp => (Date: DateTime.Parse(kvp.Key), Data: kvp.Value))
                .OrderByDescending(x => x.Date)
                .Take(numberOfDays)
                .ToList();

            return sortedData;
        }

        // ========== CONVERSION METHODS ==========

        /// <summary>
        /// Converts the AlphaVantage time series to a list of DailyData objects.
        /// Useful for integration with other parts of the trading platform.
        /// </summary>
        /// <returns>List of DailyData objects representing the time series data</returns>
        public List<Core.Models.DailyData> ToDailyData()
        {
            var result = new List<Core.Models.DailyData>();

            foreach (var kvp in TimeSeries)
            {
                if (DateTime.TryParse(kvp.Key, out var date))
                {
                    result.Add(new Core.Models.DailyData
                    {
                        Symbol = MetaData?.Symbol ?? "UNKNOWN",
                        Date = date,
                        Open = kvp.Value.OpenAsDecimal,
                        High = kvp.Value.HighAsDecimal,
                        Low = kvp.Value.LowAsDecimal,
                        Close = kvp.Value.CloseAsDecimal,
                        AdjustedClose = kvp.Value.CloseAsDecimal, // AlphaVantage doesn't provide separate adjusted close in basic API
                        Volume = kvp.Value.VolumeAsLong
                    });
                }
            }

            return result.OrderBy(d => d.Date).ToList();
        }

        /// <summary>
        /// Converts to Finnhub-compatible candle response format.
        /// Enables consistent processing across different data providers.
        /// </summary>
        /// <returns>FinnhubCandleResponse with equivalent data</returns>
        public FinnhubCandleResponse ToFinnhubCandleResponse()
        {
            var response = new FinnhubCandleResponse
            {
                Status = HasValidData ? "ok" : "no_data"
            };

            if (HasValidData)
            {
                var sortedData = TimeSeries
                    .Where(kvp => DateTime.TryParse(kvp.Key, out _))
                    .OrderBy(kvp => DateTime.Parse(kvp.Key))
                    .ToList();

                foreach (var kvp in sortedData)
                {
                    var date = DateTime.Parse(kvp.Key);
                    var data = kvp.Value;

                    response.Open.Add(data.OpenAsDecimal);
                    response.High.Add(data.HighAsDecimal);
                    response.Low.Add(data.LowAsDecimal);
                    response.Close.Add(data.CloseAsDecimal);
                    response.Volume.Add(data.VolumeAsLong);
                    response.Timestamp.Add(((DateTimeOffset)date).ToUnixTimeSeconds());
                }
            }

            return response;
        }

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that the time series data is consistent and complete.
        /// Checks for logical data relationships and required fields.
        /// </summary>
        /// <returns>True if the time series data is valid</returns>
        public bool IsValid()
        {
            // Check basic structure
            if (!HasValidData) return false;

            // Validate each time series entry
            foreach (var kvp in TimeSeries)
            {
                // Check date format
                if (!DateTime.TryParse(kvp.Key, out _)) return false;

                // Check data validity
                if (!kvp.Value.IsValid()) return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates simple moving averages for the closing prices.
        /// </summary>
        /// <param name="period">Period for moving average calculation</param>
        /// <returns>Dictionary of date to moving average value</returns>
        public Dictionary<string, decimal?> CalculateMovingAverage(int period)
        {
            var result = new Dictionary<string, decimal?>();
            var sortedData = TimeSeries
                .Where(kvp => DateTime.TryParse(kvp.Key, out _))
                .OrderBy(kvp => DateTime.Parse(kvp.Key))
                .ToList();

            for (int i = 0; i < sortedData.Count; i++)
            {
                var currentDate = sortedData[i].Key;

                if (i < period - 1)
                {
                    result[currentDate] = null;
                }
                else
                {
                    var sum = 0m;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += sortedData[j].Value.CloseAsDecimal;
                    }
                    result[currentDate] = sum / period;
                }
            }

            return result;
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            return HasValidData
                ? $"AlphaVantageTimeSeries[{MetaData.Symbol}]: {Count} days ({EarliestDate:yyyy-MM-dd} to {LatestDate:yyyy-MM-dd})"
                : "AlphaVantageTimeSeries[Invalid]";
        }
    }

    /// <summary>
    /// Represents individual time series data point from AlphaVantage.
    /// Contains OHLCV data for a specific trading day.
    /// All properties use string format as returned by AlphaVantage API.
    /// </summary>
    public class AlphaVantageTimeSeries
    {
        /// <summary>
        /// Opening price for the trading day.
        /// Maps to "1. open" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("1. open")]
        public string Open { get; set; } = "0";

        /// <summary>
        /// Highest price during the trading day.
        /// Maps to "2. high" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("2. high")]
        public string High { get; set; } = "0";

        /// <summary>
        /// Lowest price during the trading day.
        /// Maps to "3. low" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("3. low")]
        public string Low { get; set; } = "0";

        /// <summary>
        /// Closing price for the trading day.
        /// Maps to "4. close" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("4. close")]
        public string Close { get; set; } = "0";

        /// <summary>
        /// Volume of shares traded during the day.
        /// Maps to "5. volume" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("5. volume")]
        public string Volume { get; set; } = "0";

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Converts the string open price to decimal for financial calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal OpenAsDecimal => decimal.TryParse(Open, out var result) ? result : 0m;

        /// <summary>
        /// Converts the string high price to decimal for financial calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal HighAsDecimal => decimal.TryParse(High, out var result) ? result : 0m;

        /// <summary>
        /// Converts the string low price to decimal for financial calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal LowAsDecimal => decimal.TryParse(Low, out var result) ? result : 0m;

        /// <summary>
        /// Converts the string close price to decimal for financial calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal CloseAsDecimal => decimal.TryParse(Close, out var result) ? result : 0m;

        /// <summary>
        /// Converts the string volume to long integer.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public long VolumeAsLong => long.TryParse(Volume, out var result) ? result : 0L;

        /// <summary>
        /// Daily price change (Close - Open) as decimal.
        /// </summary>
        [JsonIgnore]
        public decimal DayChange => CloseAsDecimal - OpenAsDecimal;

        /// <summary>
        /// Daily percentage change ((Close - Open) / Open * 100) as decimal.
        /// Returns 0 if Open is 0 to avoid division by zero.
        /// </summary>
        [JsonIgnore]
        public decimal DayChangePercent => OpenAsDecimal != 0 ? (DayChange / OpenAsDecimal) * 100 : 0;

        /// <summary>
        /// Trading range for the day (High - Low) as decimal.
        /// </summary>
        [JsonIgnore]
        public decimal Range => HighAsDecimal - LowAsDecimal;

        /// <summary>
        /// Indicates if this is a bullish trading day (Close > Open).
        /// </summary>
        [JsonIgnore]
        public bool IsBullish => CloseAsDecimal > OpenAsDecimal;

        /// <summary>
        /// Indicates if this is a bearish trading day (Close < Open).
        /// </summary>
        [JsonIgnore]
        public bool IsBearish => CloseAsDecimal < OpenAsDecimal;

        /// <summary>
        /// Typical price for the day ((High + Low + Close) / 3).
        /// Used in technical analysis calculations.
        /// </summary>
        [JsonIgnore]
        public decimal TypicalPrice => (HighAsDecimal + LowAsDecimal + CloseAsDecimal) / 3;

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that the time series data is logically consistent.
        /// Checks price relationships and positive values.
        /// </summary>
        /// <returns>True if the time series data passes validation checks</returns>
        public bool IsValid()
        {
            // Basic validation checks
            if (OpenAsDecimal <= 0 || HighAsDecimal <= 0 || LowAsDecimal <= 0 || CloseAsDecimal <= 0)
                return false;

            if (VolumeAsLong < 0)
                return false;

            // Logical relationship checks
            if (HighAsDecimal < LowAsDecimal || HighAsDecimal < OpenAsDecimal || HighAsDecimal < CloseAsDecimal ||
                LowAsDecimal > OpenAsDecimal || LowAsDecimal > CloseAsDecimal)
                return false;

            return true;
        }

        /// <summary>
        /// Creates a normalized copy with values rounded to appropriate decimal places.
        /// Prices rounded to 4 decimal places.
        /// </summary>
        /// <returns>A new AlphaVantageTimeSeries with normalized values</returns>
        public AlphaVantageTimeSeries Normalize()
        {
            return new AlphaVantageTimeSeries
            {
                Open = Math.Round(OpenAsDecimal, 4).ToString(),
                High = Math.Round(HighAsDecimal, 4).ToString(),
                Low = Math.Round(LowAsDecimal, 4).ToString(),
                Close = Math.Round(CloseAsDecimal, 4).ToString(),
                Volume = Volume // Volume doesn't need decimal normalization
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            var direction = IsBullish ? "↑" : IsBearish ? "↓" : "→";
            return $"TimeSeries: O:${Open} H:${High} L:${Low} C:${Close} {direction} " +
                   $"{DayChangePercent:+0.00;-0.00;0.00}% Vol:{Volume}";
        }
    }
}

// Total Lines: 412
