using System;
using System.Collections.Generic;

namespace TradingPlatform.PaperTrading.Models
{
    /// <summary>
    /// Advanced order types for sophisticated trading strategies.
    /// These order types help minimize market impact and achieve better execution prices.
    /// </summary>
    
    /// <summary>
    /// Time-Weighted Average Price (TWAP) order.
    /// Executes a large order by breaking it into smaller pieces over a specified time period.
    /// Aims to achieve an average execution price close to the time-weighted average price.
    /// </summary>
    public record TwapOrder(
        string OrderId,
        string Symbol,
        OrderSide Side,
        decimal TotalQuantity,
        DateTime StartTime,
        DateTime EndTime,
        int NumberOfSlices,
        bool RandomizeSliceSize,
        bool RandomizeSliceTiming,
        decimal? MinSliceSize,
        decimal? MaxSliceSize,
        TimeSpan? MinInterval,
        TimeSpan? MaxInterval,
        decimal? LimitPrice,
        string? ClientOrderId = null
    ) : IAdvancedOrder
    {
        /// <summary>
        /// Calculate the duration for the TWAP execution
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
        
        /// <summary>
        /// Calculate the average slice size
        /// </summary>
        public decimal AverageSliceSize => TotalQuantity / NumberOfSlices;
        
        /// <summary>
        /// Calculate the average interval between slices
        /// </summary>
        public TimeSpan AverageInterval => TimeSpan.FromTicks(Duration.Ticks / (NumberOfSlices - 1));
    }

    /// <summary>
    /// Volume-Weighted Average Price (VWAP) order.
    /// Executes a large order following the market's volume profile.
    /// Aims to achieve an average execution price close to the volume-weighted average price.
    /// </summary>
    public record VwapOrder(
        string OrderId,
        string Symbol,
        OrderSide Side,
        decimal TotalQuantity,
        DateTime StartTime,
        DateTime EndTime,
        decimal ParticipationRate, // Percentage of market volume to participate in (0.01 = 1%)
        bool UseHistoricalVolume,
        int HistoricalDays, // Number of days to use for historical volume profile
        decimal? LimitPrice,
        decimal MaxParticipationRate, // Maximum participation rate to prevent market impact
        decimal MinSliceSize,
        decimal MaxSliceSize,
        string? ClientOrderId = null
    ) : IAdvancedOrder
    {
        /// <summary>
        /// Validate participation rate is within acceptable bounds
        /// </summary>
        public bool IsValidParticipationRate => 
            ParticipationRate > 0 && 
            ParticipationRate <= MaxParticipationRate && 
            ParticipationRate <= 0.3m; // 30% max to avoid market manipulation
    }

    /// <summary>
    /// Iceberg order (also known as Reserve order).
    /// Shows only a small portion of the total order size to the market.
    /// Automatically replenishes the visible quantity as it gets filled.
    /// </summary>
    public record IcebergOrder(
        string OrderId,
        string Symbol,
        OrderSide Side,
        decimal TotalQuantity,
        decimal VisibleQuantity, // The quantity shown to the market
        OrderType UnderlyingType, // Market or Limit
        decimal? LimitPrice,
        bool RandomizeVisibleQuantity,
        decimal? MinVisibleQuantity,
        decimal? MaxVisibleQuantity,
        TimeInForce TimeInForce = TimeInForce.Day,
        string? ClientOrderId = null
    ) : IAdvancedOrder
    {
        /// <summary>
        /// Calculate the number of refills needed
        /// </summary>
        public int EstimatedRefills => (int)Math.Ceiling(TotalQuantity / VisibleQuantity);
        
        /// <summary>
        /// Validate visible quantity is reasonable
        /// </summary>
        public bool IsValidVisibleQuantity => 
            VisibleQuantity > 0 && 
            VisibleQuantity < TotalQuantity &&
            VisibleQuantity <= TotalQuantity * 0.2m; // Visible should be max 20% of total
    }

    /// <summary>
    /// Common interface for all advanced order types
    /// </summary>
    public interface IAdvancedOrder
    {
        string OrderId { get; }
        string Symbol { get; }
        OrderSide Side { get; }
        string? ClientOrderId { get; }
    }

    /// <summary>
    /// Execution slice for TWAP/VWAP orders
    /// </summary>
    public record OrderSlice(
        string SliceId,
        string ParentOrderId,
        string Symbol,
        OrderSide Side,
        decimal Quantity,
        decimal? LimitPrice,
        DateTime ScheduledTime,
        DateTime? ExecutedTime,
        decimal? ExecutedPrice,
        SliceStatus Status,
        string? ChildOrderId
    );

    /// <summary>
    /// Status of an order slice
    /// </summary>
    public enum SliceStatus
    {
        Pending,
        Submitted,
        PartiallyFilled,
        Filled,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Advanced order execution status
    /// </summary>
    public record AdvancedOrderStatus(
        string OrderId,
        AdvancedOrderType Type,
        decimal TotalQuantity,
        decimal FilledQuantity,
        decimal RemainingQuantity,
        decimal AveragePrice,
        int TotalSlices,
        int CompletedSlices,
        int PendingSlices,
        DateTime StartTime,
        DateTime? EndTime,
        AdvancedOrderState State,
        IEnumerable<OrderSlice> Slices,
        ExecutionStatistics Statistics
    );

    /// <summary>
    /// Type of advanced order
    /// </summary>
    public enum AdvancedOrderType
    {
        TWAP,
        VWAP,
        Iceberg
    }

    /// <summary>
    /// State of an advanced order
    /// </summary>
    public enum AdvancedOrderState
    {
        Created,
        Running,
        Paused,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Execution statistics for advanced orders
    /// </summary>
    public record ExecutionStatistics(
        decimal AverageSlippage,
        decimal TotalSlippage,
        decimal BenchmarkPrice, // TWAP or VWAP benchmark
        decimal PerformanceVsBenchmark, // How much better/worse than benchmark
        TimeSpan AverageExecutionTime,
        decimal MarketImpact,
        decimal TotalCommission,
        int SuccessfulSlices,
        int FailedSlices
    );

    /// <summary>
    /// VWAP calculation data
    /// </summary>
    public record VolumeProfile(
        DateTime Time,
        decimal Volume,
        decimal Price,
        decimal VolumePercentage // Percentage of daily volume
    );

    /// <summary>
    /// Configuration for advanced order execution algorithms
    /// </summary>
    public class AdvancedOrderConfiguration
    {
        /// <summary>
        /// Maximum percentage of market volume for any single order
        /// </summary>
        public decimal MaxMarketParticipation { get; set; } = 0.25m; // 25%

        /// <summary>
        /// Minimum time between order slices to avoid detection
        /// </summary>
        public TimeSpan MinSliceInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum time between order slices to ensure completion
        /// </summary>
        public TimeSpan MaxSliceInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Randomization factor for slice sizes (0-1)
        /// </summary>
        public decimal SliceSizeRandomization { get; set; } = 0.2m; // 20% variation

        /// <summary>
        /// Randomization factor for slice timing (0-1)
        /// </summary>
        public decimal SliceTimingRandomization { get; set; } = 0.15m; // 15% variation

        /// <summary>
        /// Enable smart routing to multiple venues
        /// </summary>
        public bool EnableSmartRouting { get; set; } = true;

        /// <summary>
        /// Enable anti-gaming logic to prevent detection
        /// </summary>
        public bool EnableAntiGaming { get; set; } = true;

        /// <summary>
        /// Maximum slippage tolerance (as percentage)
        /// </summary>
        public decimal MaxSlippageTolerance { get; set; } = 0.005m; // 0.5%

        /// <summary>
        /// Minimum order size (to avoid odd lots)
        /// </summary>
        public decimal MinOrderSize { get; set; } = 100m;

        /// <summary>
        /// Use adaptive algorithms that adjust based on market conditions
        /// </summary>
        public bool UseAdaptiveAlgorithms { get; set; } = true;
    }

    /// <summary>
    /// Request to create an advanced order
    /// </summary>
    public record AdvancedOrderRequest(
        AdvancedOrderType Type,
        string Symbol,
        OrderSide Side,
        decimal TotalQuantity,
        Dictionary<string, object> Parameters,
        string? ClientOrderId = null
    );

    /// <summary>
    /// Result of advanced order submission
    /// </summary>
    public record AdvancedOrderResult(
        bool IsSuccess,
        string? OrderId,
        string Message,
        AdvancedOrderType Type,
        DateTime Timestamp,
        ValidationResult? Validation = null
    );

    /// <summary>
    /// Validation result for advanced orders
    /// </summary>
    public record ValidationResult(
        bool IsValid,
        IEnumerable<string> Errors,
        IEnumerable<string> Warnings
    );
}