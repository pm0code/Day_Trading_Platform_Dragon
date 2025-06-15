// File: TradingPlatform.DataIngestion\Models\FinnhubCandleResponse.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Represents Finnhub candle/OHLCV response structure.
    /// Contains arrays of OHLCV data for technical analysis and historical data processing.
    /// Maps directly to Finnhub's /stock/candle API response format.
    /// 
    /// Finnhub Candle API Response Structure:
    /// {
    ///   "o": [261.07, 262.15, ...],  // Open prices array
    ///   "h": [263.31, 264.22, ...],  // High prices array  
    ///   "l": [260.68, 261.45, ...],  // Low prices array
    ///   "c": [261.74, 263.89, ...],  // Close prices array
    ///   "v": [1234567, 987654, ...], // Volume array
    ///   "t": [1582641000, ...],       // UNIX timestamps array
    ///   "s": "ok"                     // Status indicator
    /// }
    /// </summary>
    public class FinnhubCandleResponse
    {
        /// <summary>
        /// Array of opening prices for each time period.
        /// Maps to "o" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("o")]
        public List<decimal> Open { get; set; } = new List<decimal>();

        /// <summary>
        /// Array of highest prices for each time period.
        /// Maps to "h" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("h")]
        public List<decimal> High { get; set; } = new List<decimal>();

        /// <summary>
        /// Array of lowest prices for each time period.
        /// Maps to "l" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("l")]
        public List<decimal> Low { get; set; } = new List<decimal>();

        /// <summary>
        /// Array of closing prices for each time period.
        /// Maps to "c" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("c")]
        public List<decimal> Close { get; set; } = new List<decimal>();

        /// <summary>
        /// Array of volume data for each time period.
        /// Maps to "v" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("v")]
        public List<long> Volume { get; set; } = new List<long>();

        /// <summary>
        /// Array of UNIX timestamps for each time period.
        /// Maps to "t" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("t")]
        public List<long> Timestamp { get; set; } = new List<long>();

        /// <summary>
        /// Status indicator from Finnhub API.
        /// Maps to "s" field in Finnhub JSON response.
        /// Values: "ok" for success, "no_data" for no data available.
        /// </summary>
        [JsonPropertyName("s")]
        public string Status { get; set; } = string.Empty;

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Number of data points in the response.
        /// All arrays should have the same length for valid data.
        /// </summary>
        [JsonIgnore]
        public int Count => Close?.Count ?? 0;

        /// <summary>
        /// Indicates if the response contains valid data.
        /// Checks for "ok" status and non-empty data arrays.
        /// </summary>
        [JsonIgnore]
        public bool HasData => Status == "ok" && Count > 0;

        /// <summary>
        /// Date range covered by this candle data.
        /// Returns the span from first to last timestamp.
        /// </summary>
        [JsonIgnore]
        public TimeSpan DateRange
        {
            get
            {
                if (Timestamp?.Count < 2) return TimeSpan.Zero;
                var first = DateTimeOffset.FromUnixTimeSeconds(Timestamp.First()).DateTime;
                var last = DateTimeOffset.FromUnixTimeSeconds(Timestamp.Last()).DateTime;
                return last - first;
            }
        }

        /// <summary>
        /// Gets the DateTime objects corresponding to the timestamps.
        /// Converts UNIX timestamps to UTC DateTime objects.
        /// </summary>
        [JsonIgnore]
        public List<DateTime> DateTimes => Timestamp?.Select(t =>
            DateTimeOffset.FromUnixTimeSeconds(t).DateTime).ToList() ?? new List<DateTime>();

        // ========== DATA ACCESS METHODS ==========

        /// <summary>
        /// Gets OHLCV data for a specific index.
        /// Returns null if index is out of range.
        /// </summary>
        /// <param name="index">Index of the data point to retrieve</param>
        /// <returns>OHLCV data at the specified index, or null if invalid</returns>
        public CandleData? GetCandle(int index)
        {
            if (index < 0 || index >= Count) return null;

            return new CandleData
            {
                Timestamp = Timestamp[index],
                Open = Open[index],
                High = High[index],
                Low = Low[index],
                Close = Close[index],
                Volume = Volume[index]
            };
        }

        /// <summary>
        /// Gets the most recent (last) candle data.
        /// Returns null if no data is available.
        /// </summary>
        /// <returns>The most recent candle data, or null if empty</returns>
        public CandleData? GetLatestCandle()
        {
            return Count > 0 ? GetCandle(Count - 1) : null;
        }

        /// <summary>
        /// Gets candle data within a specific date range.
        /// </summary>
        /// <param name="startDate">Start date for filtering</param>
        /// <param name="endDate">End date for filtering</param>
        /// <returns>List of candle data within the specified range</returns>
        public List<CandleData> GetCandlesInRange(DateTime startDate, DateTime endDate)
        {
            var result = new List<CandleData>();

            for (int i = 0; i < Count; i++)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(Timestamp[i]).DateTime;
                if (date >= startDate && date <= endDate)
                {
                    var candle = GetCandle(i);
                    if (candle != null) result.Add(candle);
                }
            }

            return result;
        }

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that all data arrays have consistent lengths and logical values.
        /// Ensures data integrity for further processing.
        /// </summary>
        /// <returns>True if the candle data is valid and consistent</returns>
        public bool IsValid()
        {
            // Check status
            if (Status != "ok") return false;

            // Check if we have data
            if (Count == 0) return false;

            // Check array length consistency
            if (Open?.Count != Count || High?.Count != Count ||
                Low?.Count != Count || Volume?.Count != Count ||
                Timestamp?.Count != Count)
                return false;

            // Check data relationships for each candle
            for (int i = 0; i < Count; i++)
            {
                if (Open[i] <= 0 || High[i] <= 0 || Low[i] <= 0 || Close[i] <= 0)
                    return false;

                if (High[i] < Low[i] || High[i] < Open[i] || High[i] < Close[i] ||
                    Low[i] > Open[i] || Low[i] > Close[i])
                    return false;

                if (Volume[i] < 0)
                    return false;

                // Check timestamp is reasonable (after year 2000)
                if (Timestamp[i] < 946684800) // 2000-01-01 UTC
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Converts the candle response to a list of DailyData objects.
        /// Useful for integration with other parts of the trading platform.
        /// </summary>
        /// <param name="symbol">Symbol to assign to each DailyData object</param>
        /// <returns>List of DailyData objects representing the candle data</returns>
        public List<Core.Models.DailyData> ToDailyData(string symbol)
        {
            var result = new List<Core.Models.DailyData>();

            for (int i = 0; i < Count; i++)
            {
                result.Add(new Core.Models.DailyData
                {
                    Symbol = symbol,
                    Date = DateTimeOffset.FromUnixTimeSeconds(Timestamp[i]).DateTime,
                    Open = Open[i],
                    High = High[i],
                    Low = Low[i],
                    Close = Close[i],
                    AdjustedClose = Close[i], // Finnhub doesn't provide separate adjusted close
                    Volume = Volume[i]
                });
            }

            return result;
        }

        /// <summary>
        /// Calculates simple moving averages for the closing prices.
        /// </summary>
        /// <param name="period">Period for moving average calculation</param>
        /// <returns>List of moving average values (null for insufficient data)</returns>
        public List<decimal?> CalculateMovingAverage(int period)
        {
            var result = new List<decimal?>();

            for (int i = 0; i < Count; i++)
            {
                if (i < period - 1)
                {
                    result.Add(null);
                }
                else
                {
                    var sum = 0m;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += Close[j];
                    }
                    result.Add(sum / period);
                }
            }

            return result;
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        /// <returns>Formatted string representation of the candle response</returns>
        public override string ToString()
        {
            return $"FinnhubCandleResponse: Status={Status}, Count={Count}, " +
                   $"Range={DateRange.TotalDays:F1} days";
        }
    }

    /// <summary>
    /// Represents a single candle (OHLCV) data point.
    /// Used for individual candle analysis and processing.
    /// </summary>
    public class CandleData
    {
        public long Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }

        public DateTime DateTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
        public decimal Change => Close - Open;
        public decimal ChangePercent => Open != 0 ? (Change / Open) * 100 : 0;
        public decimal Range => High - Low;
        public bool IsBullish => Close > Open;
        public bool IsBearish => Close < Open;
    }
}

// Total Lines: 243
