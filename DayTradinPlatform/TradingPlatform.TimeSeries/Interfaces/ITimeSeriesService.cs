using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Foundation.Models;
using TradingPlatform.TimeSeries.Models;

namespace TradingPlatform.TimeSeries.Interfaces
{
    /// <summary>
    /// Interface for time-series data storage and retrieval
    /// </summary>
    public interface ITimeSeriesService
    {
        /// <summary>
        /// Writes a single data point to the time-series database
        /// </summary>
        Task<TradingResult> WritePointAsync<T>(T point, CancellationToken cancellationToken = default) 
            where T : TimeSeriesPoint;

        /// <summary>
        /// Writes multiple data points in a batch for efficiency
        /// </summary>
        Task<TradingResult> WritePointsAsync<T>(IEnumerable<T> points, CancellationToken cancellationToken = default) 
            where T : TimeSeriesPoint;

        /// <summary>
        /// Queries time-series data with Flux query language
        /// </summary>
        Task<TradingResult<List<T>>> QueryAsync<T>(
            string fluxQuery, 
            CancellationToken cancellationToken = default) 
            where T : TimeSeriesPoint, new();

        /// <summary>
        /// Gets the latest data point for a measurement
        /// </summary>
        Task<TradingResult<T>> GetLatestAsync<T>(
            string measurement,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default) 
            where T : TimeSeriesPoint, new();

        /// <summary>
        /// Gets data points within a time range
        /// </summary>
        Task<TradingResult<List<T>>> GetRangeAsync<T>(
            string measurement,
            DateTime start,
            DateTime end,
            Dictionary<string, string>? tags = null,
            int? limit = null,
            CancellationToken cancellationToken = default) 
            where T : TimeSeriesPoint, new();

        /// <summary>
        /// Aggregates data over a time window
        /// </summary>
        Task<TradingResult<List<AggregatedData>>> AggregateAsync(
            string measurement,
            string field,
            AggregationType aggregationType,
            TimeSpan window,
            DateTime start,
            DateTime end,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes data points matching criteria
        /// </summary>
        Task<TradingResult> DeleteAsync(
            string measurement,
            DateTime start,
            DateTime end,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a continuous query for real-time aggregations
        /// </summary>
        Task<TradingResult> CreateContinuousQueryAsync(
            string name,
            string query,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the time-series database is healthy
        /// </summary>
        Task<TradingResult<bool>> IsHealthyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets database statistics
        /// </summary>
        Task<TradingResult<DatabaseStats>> GetStatsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Types of aggregation operations
    /// </summary>
    public enum AggregationType
    {
        Mean,
        Sum,
        Count,
        Min,
        Max,
        StdDev,
        First,
        Last,
        Median,
        Percentile
    }

    /// <summary>
    /// Aggregated data result
    /// </summary>
    public class AggregatedData
    {
        public DateTime Time { get; set; }
        public decimal Value { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Database statistics
    /// </summary>
    public class DatabaseStats
    {
        public long TotalPoints { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public int BucketCount { get; set; }
        public int MeasurementCount { get; set; }
        public DateTime OldestDataPoint { get; set; }
        public DateTime NewestDataPoint { get; set; }
        public double WriteRatePerSecond { get; set; }
        public double QueryRatePerSecond { get; set; }
        public Dictionary<string, long> PointsByMeasurement { get; set; } = new();
    }
}