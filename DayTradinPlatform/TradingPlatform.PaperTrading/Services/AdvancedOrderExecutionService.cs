using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.PaperTrading.Interfaces;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Advanced order execution service implementing TWAP, VWAP, and Iceberg orders.
    /// Uses canonical patterns for consistency and monitoring.
    /// </summary>
    public class AdvancedOrderExecutionService : CanonicalServiceBase, IAdvancedOrderExecutionService
    {
        private readonly IOrderExecutionEngine _executionEngine;
        private readonly IMarketDataService _marketDataService;
        private readonly IVolumeAnalysisService _volumeAnalysisService;
        private readonly AdvancedOrderConfiguration _configuration;
        private readonly Dictionary<string, AdvancedOrderContext> _activeOrders;
        private readonly SemaphoreSlim _orderLock;
        private readonly Random _random;

        public AdvancedOrderExecutionService(
            IOrderExecutionEngine executionEngine,
            IMarketDataService marketDataService,
            IVolumeAnalysisService volumeAnalysisService,
            AdvancedOrderConfiguration configuration,
            ITradingLogger logger)
            : base(logger, "AdvancedOrderExecution")
        {
            _executionEngine = executionEngine;
            _marketDataService = marketDataService;
            _volumeAnalysisService = volumeAnalysisService;
            _configuration = configuration;
            _activeOrders = new Dictionary<string, AdvancedOrderContext>();
            _orderLock = new SemaphoreSlim(1, 1);
            _random = new Random();
        }

        #region TWAP Implementation

        public async Task<AdvancedOrderResult> SubmitTwapOrderAsync(TwapOrder order)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // Validate order
                var validation = ValidateTwapOrder(order);
                if (!validation.IsValid)
                {
                    return new AdvancedOrderResult(
                        false, 
                        null, 
                        "TWAP order validation failed", 
                        AdvancedOrderType.TWAP, 
                        DateTime.UtcNow, 
                        validation);
                }

                // Create execution context
                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.TWAP,
                    State = AdvancedOrderState.Created,
                    Slices = GenerateTwapSlices(order),
                    StartTime = order.StartTime,
                    Statistics = new ExecutionStatistics(
                        0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                // Register order
                await _orderLock.WaitAsync();
                try
                {
                    _activeOrders[order.OrderId] = context;
                }
                finally
                {
                    _orderLock.Release();
                }

                // Start execution
                _ = Task.Run(() => ExecuteTwapOrderAsync(context));

                return new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "TWAP order submitted successfully",
                    AdvancedOrderType.TWAP,
                    DateTime.UtcNow);
            });
        }

        private async Task ExecuteTwapOrderAsync(AdvancedOrderContext context)
        {
            var twapOrder = (TwapOrder)context.Order;
            context.State = AdvancedOrderState.Running;

            try
            {
                foreach (var slice in context.Slices)
                {
                    // Wait for scheduled time
                    var delay = slice.ScheduledTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }

                    // Check if order was cancelled
                    if (context.State == AdvancedOrderState.Cancelled)
                    {
                        break;
                    }

                    // Apply randomization if enabled
                    var executeQuantity = slice.Quantity;
                    if (twapOrder.RandomizeSliceSize)
                    {
                        executeQuantity = RandomizeQuantity(
                            slice.Quantity, 
                            twapOrder.MinSliceSize, 
                            twapOrder.MaxSliceSize);
                    }

                    // Execute slice
                    await ExecuteSliceAsync(context, slice, executeQuantity);
                }

                // Update final state
                context.State = context.FilledQuantity >= twapOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TWAP order execution failed for {OrderId}", twapOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
        }

        private List<OrderSlice> GenerateTwapSlices(TwapOrder order)
        {
            var slices = new List<OrderSlice>();
            var sliceQuantity = order.AverageSliceSize;
            var interval = order.AverageInterval;
            var currentTime = order.StartTime;

            for (int i = 0; i < order.NumberOfSlices; i++)
            {
                // Apply timing randomization
                if (order.RandomizeSliceTiming && i > 0)
                {
                    var randomFactor = (_random.NextDouble() - 0.5) * 2 * (double)_configuration.SliceTimingRandomization;
                    var adjustedInterval = TimeSpan.FromTicks((long)(interval.Ticks * (1 + randomFactor)));
                    
                    // Respect min/max intervals
                    if (order.MinInterval.HasValue && adjustedInterval < order.MinInterval.Value)
                        adjustedInterval = order.MinInterval.Value;
                    if (order.MaxInterval.HasValue && adjustedInterval > order.MaxInterval.Value)
                        adjustedInterval = order.MaxInterval.Value;
                        
                    currentTime = currentTime.Add(adjustedInterval);
                }
                else if (i > 0)
                {
                    currentTime = currentTime.Add(interval);
                }

                // Ensure we don't exceed end time
                if (currentTime > order.EndTime)
                {
                    currentTime = order.EndTime;
                }

                // Calculate slice quantity (ensure last slice includes any remainder)
                var quantity = (i == order.NumberOfSlices - 1)
                    ? order.TotalQuantity - (sliceQuantity * i)
                    : sliceQuantity;

                slices.Add(new OrderSlice(
                    $"{order.OrderId}_slice_{i}",
                    order.OrderId,
                    order.Symbol,
                    order.Side,
                    quantity,
                    order.LimitPrice,
                    currentTime,
                    null,
                    null,
                    SliceStatus.Pending,
                    null));
            }

            return slices;
        }

        #endregion

        #region VWAP Implementation

        public async Task<AdvancedOrderResult> SubmitVwapOrderAsync(VwapOrder order)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // Validate order
                var validation = ValidateVwapOrder(order);
                if (!validation.IsValid)
                {
                    return new AdvancedOrderResult(
                        false, 
                        null, 
                        "VWAP order validation failed", 
                        AdvancedOrderType.VWAP, 
                        DateTime.UtcNow, 
                        validation);
                }

                // Get volume profile
                var volumeProfile = await GetVolumeProfileAsync(order);

                // Create execution context
                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.VWAP,
                    State = AdvancedOrderState.Created,
                    Slices = GenerateVwapSlices(order, volumeProfile),
                    StartTime = order.StartTime,
                    VolumeProfile = volumeProfile,
                    Statistics = new ExecutionStatistics(
                        0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                // Register order
                await _orderLock.WaitAsync();
                try
                {
                    _activeOrders[order.OrderId] = context;
                }
                finally
                {
                    _orderLock.Release();
                }

                // Start execution
                _ = Task.Run(() => ExecuteVwapOrderAsync(context));

                return new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "VWAP order submitted successfully",
                    AdvancedOrderType.VWAP,
                    DateTime.UtcNow);
            });
        }

        private async Task ExecuteVwapOrderAsync(AdvancedOrderContext context)
        {
            var vwapOrder = (VwapOrder)context.Order;
            context.State = AdvancedOrderState.Running;

            try
            {
                foreach (var slice in context.Slices)
                {
                    // Wait for scheduled time
                    var delay = slice.ScheduledTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }

                    // Check if order was cancelled
                    if (context.State == AdvancedOrderState.Cancelled)
                    {
                        break;
                    }

                    // Get current market volume
                    var currentVolume = await _marketDataService.GetCurrentVolumeAsync(vwapOrder.Symbol);
                    
                    // Calculate dynamic slice size based on participation rate
                    var targetQuantity = currentVolume * vwapOrder.ParticipationRate;
                    targetQuantity = Math.Max(vwapOrder.MinSliceSize, 
                                            Math.Min(vwapOrder.MaxSliceSize, targetQuantity));

                    // Execute slice
                    await ExecuteSliceAsync(context, slice, targetQuantity);
                }

                // Update final state
                context.State = context.FilledQuantity >= vwapOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VWAP order execution failed for {OrderId}", vwapOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
        }

        private async Task<List<VolumeProfile>> GetVolumeProfileAsync(VwapOrder order)
        {
            if (order.UseHistoricalVolume)
            {
                return await _volumeAnalysisService.GetHistoricalVolumeProfileAsync(
                    order.Symbol, 
                    order.HistoricalDays);
            }
            else
            {
                return await _volumeAnalysisService.GetIntradayVolumeProfileAsync(
                    order.Symbol);
            }
        }

        private List<OrderSlice> GenerateVwapSlices(VwapOrder order, List<VolumeProfile> volumeProfile)
        {
            var slices = new List<OrderSlice>();
            var remainingQuantity = order.TotalQuantity;
            var currentTime = order.StartTime;

            foreach (var profile in volumeProfile)
            {
                // Skip if outside time window
                if (profile.Time < order.StartTime || profile.Time > order.EndTime)
                    continue;

                // Calculate quantity based on volume percentage
                var sliceQuantity = order.TotalQuantity * profile.VolumePercentage;
                sliceQuantity = Math.Min(remainingQuantity, sliceQuantity);
                
                // Apply min/max constraints
                sliceQuantity = Math.Max(order.MinSliceSize, 
                                       Math.Min(order.MaxSliceSize, sliceQuantity));

                if (sliceQuantity > 0 && remainingQuantity > 0)
                {
                    slices.Add(new OrderSlice(
                        $"{order.OrderId}_vwap_{slices.Count}",
                        order.OrderId,
                        order.Symbol,
                        order.Side,
                        sliceQuantity,
                        order.LimitPrice,
                        profile.Time,
                        null,
                        null,
                        SliceStatus.Pending,
                        null));

                    remainingQuantity -= sliceQuantity;
                }
            }

            return slices;
        }

        #endregion

        #region Iceberg Implementation

        public async Task<AdvancedOrderResult> SubmitIcebergOrderAsync(IcebergOrder order)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // Validate order
                var validation = ValidateIcebergOrder(order);
                if (!validation.IsValid)
                {
                    return new AdvancedOrderResult(
                        false, 
                        null, 
                        "Iceberg order validation failed", 
                        AdvancedOrderType.Iceberg, 
                        DateTime.UtcNow, 
                        validation);
                }

                // Create execution context
                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.Iceberg,
                    State = AdvancedOrderState.Created,
                    Slices = new List<OrderSlice>(),
                    StartTime = DateTime.UtcNow,
                    Statistics = new ExecutionStatistics(
                        0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                // Register order
                await _orderLock.WaitAsync();
                try
                {
                    _activeOrders[order.OrderId] = context;
                }
                finally
                {
                    _orderLock.Release();
                }

                // Start execution
                _ = Task.Run(() => ExecuteIcebergOrderAsync(context));

                return new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "Iceberg order submitted successfully",
                    AdvancedOrderType.Iceberg,
                    DateTime.UtcNow);
            });
        }

        private async Task ExecuteIcebergOrderAsync(AdvancedOrderContext context)
        {
            var icebergOrder = (IcebergOrder)context.Order;
            context.State = AdvancedOrderState.Running;
            var remainingQuantity = icebergOrder.TotalQuantity;

            try
            {
                while (remainingQuantity > 0 && context.State == AdvancedOrderState.Running)
                {
                    // Determine visible quantity for this iteration
                    var visibleQuantity = icebergOrder.VisibleQuantity;
                    if (icebergOrder.RandomizeVisibleQuantity)
                    {
                        visibleQuantity = RandomizeQuantity(
                            icebergOrder.VisibleQuantity,
                            icebergOrder.MinVisibleQuantity,
                            icebergOrder.MaxVisibleQuantity);
                    }

                    // Ensure we don't exceed remaining quantity
                    visibleQuantity = Math.Min(visibleQuantity, remainingQuantity);

                    // Create slice for visible portion
                    var slice = new OrderSlice(
                        $"{icebergOrder.OrderId}_iceberg_{context.Slices.Count}",
                        icebergOrder.OrderId,
                        icebergOrder.Symbol,
                        icebergOrder.Side,
                        visibleQuantity,
                        icebergOrder.LimitPrice,
                        DateTime.UtcNow,
                        null,
                        null,
                        SliceStatus.Pending,
                        null);

                    context.Slices.Add(slice);

                    // Execute visible portion
                    var childOrder = new OrderRequest(
                        icebergOrder.Symbol,
                        icebergOrder.UnderlyingType,
                        icebergOrder.Side,
                        visibleQuantity,
                        icebergOrder.LimitPrice,
                        null,
                        icebergOrder.TimeInForce,
                        slice.SliceId);

                    var result = await _executionEngine.SubmitOrderAsync(childOrder);
                    
                    if (result.IsSuccess && result.OrderId != null)
                    {
                        // Monitor child order until filled or cancelled
                        await MonitorIcebergSliceAsync(context, slice, result.OrderId);
                        
                        var filledQuantity = await GetFilledQuantityAsync(result.OrderId);
                        remainingQuantity -= filledQuantity;
                        context.FilledQuantity += filledQuantity;
                    }
                    else
                    {
                        _logger.LogWarning("Iceberg slice submission failed for {OrderId}", icebergOrder.OrderId);
                        break;
                    }

                    // Add small delay to avoid appearing algorithmic
                    if (_configuration.EnableAntiGaming && remainingQuantity > 0)
                    {
                        var delay = TimeSpan.FromMilliseconds(_random.Next(500, 2000));
                        await Task.Delay(delay);
                    }
                }

                // Update final state
                context.State = context.FilledQuantity >= icebergOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Iceberg order execution failed for {OrderId}", icebergOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
        }

        #endregion

        #region Common Execution Methods

        private async Task ExecuteSliceAsync(AdvancedOrderContext context, OrderSlice slice, decimal quantity)
        {
            try
            {
                slice = slice with { Status = SliceStatus.Submitted };

                // Create child order
                var orderRequest = new OrderRequest(
                    slice.Symbol,
                    slice.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market,
                    slice.Side,
                    quantity,
                    slice.LimitPrice,
                    null,
                    TimeInForce.IOC, // Use IOC for slices to avoid hanging orders
                    slice.SliceId);

                var startTime = DateTime.UtcNow;
                var result = await _executionEngine.SubmitOrderAsync(orderRequest);

                if (result.IsSuccess && result.OrderId != null)
                {
                    slice = slice with 
                    { 
                        ChildOrderId = result.OrderId,
                        Status = SliceStatus.Filled,
                        ExecutedTime = DateTime.UtcNow
                    };

                    // Get execution details
                    var execution = await _executionEngine.GetExecutionDetailsAsync(result.OrderId);
                    if (execution != null)
                    {
                        slice = slice with { ExecutedPrice = execution.Price };
                        context.FilledQuantity += execution.Quantity;
                        context.TotalCommission += execution.Commission;
                        context.TotalSlippage += execution.Slippage;
                        context.SuccessfulSlices++;

                        // Update statistics
                        UpdateExecutionStatistics(context, execution, DateTime.UtcNow - startTime);
                    }
                }
                else
                {
                    slice = slice with { Status = SliceStatus.Failed };
                    context.FailedSlices++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute slice {SliceId}", slice.SliceId);
                slice = slice with { Status = SliceStatus.Failed };
                context.FailedSlices++;
            }
        }

        private void UpdateExecutionStatistics(AdvancedOrderContext context, Execution execution, TimeSpan executionTime)
        {
            var stats = context.Statistics;
            
            // Update running averages
            var totalSlices = context.SuccessfulSlices + context.FailedSlices;
            var newAvgSlippage = ((stats.AverageSlippage * (totalSlices - 1)) + execution.Slippage) / totalSlices;
            var newAvgExecTime = TimeSpan.FromTicks(
                ((stats.AverageExecutionTime.Ticks * (totalSlices - 1)) + executionTime.Ticks) / totalSlices);

            context.Statistics = stats with
            {
                AverageSlippage = newAvgSlippage,
                TotalSlippage = context.TotalSlippage,
                AverageExecutionTime = newAvgExecTime,
                TotalCommission = context.TotalCommission,
                SuccessfulSlices = context.SuccessfulSlices,
                FailedSlices = context.FailedSlices
            };
        }

        private decimal RandomizeQuantity(decimal baseQuantity, decimal? min, decimal? max)
        {
            var randomFactor = (_random.NextDouble() - 0.5) * 2 * (double)_configuration.SliceSizeRandomization;
            var adjustedQuantity = baseQuantity * (1 + (decimal)randomFactor);

            if (min.HasValue && adjustedQuantity < min.Value)
                adjustedQuantity = min.Value;
            if (max.HasValue && adjustedQuantity > max.Value)
                adjustedQuantity = max.Value;

            // Round to nearest 100 shares to avoid odd lots
            return Math.Round(adjustedQuantity / 100) * 100;
        }

        private async Task MonitorIcebergSliceAsync(AdvancedOrderContext context, OrderSlice slice, string childOrderId)
        {
            // Simple monitoring - in production would be more sophisticated
            var maxWaitTime = TimeSpan.FromMinutes(5);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                var status = await _executionEngine.GetOrderStatusAsync(childOrderId);
                if (status == OrderStatus.Filled || status == OrderStatus.Cancelled)
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private async Task<decimal> GetFilledQuantityAsync(string orderId)
        {
            var order = await _executionEngine.GetOrderAsync(orderId);
            return order?.FilledQuantity ?? 0m;
        }

        #endregion

        #region Validation Methods

        private ValidationResult ValidateTwapOrder(TwapOrder order)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (order.TotalQuantity <= 0)
                errors.Add("Total quantity must be positive");

            if (order.NumberOfSlices <= 0)
                errors.Add("Number of slices must be positive");

            if (order.StartTime >= order.EndTime)
                errors.Add("Start time must be before end time");

            if (order.StartTime < DateTime.UtcNow)
                warnings.Add("Start time is in the past");

            if (order.Duration < TimeSpan.FromMinutes(1))
                errors.Add("Duration must be at least 1 minute");

            if (order.AverageSliceSize < _configuration.MinOrderSize)
                warnings.Add($"Average slice size ({order.AverageSliceSize}) is below minimum order size ({_configuration.MinOrderSize})");

            return new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);
        }

        private ValidationResult ValidateVwapOrder(VwapOrder order)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (order.TotalQuantity <= 0)
                errors.Add("Total quantity must be positive");

            if (!order.IsValidParticipationRate)
                errors.Add($"Participation rate must be between 0 and {order.MaxParticipationRate:P}");

            if (order.StartTime >= order.EndTime)
                errors.Add("Start time must be before end time");

            if (order.MinSliceSize >= order.MaxSliceSize)
                errors.Add("Min slice size must be less than max slice size");

            if (order.UseHistoricalVolume && order.HistoricalDays <= 0)
                errors.Add("Historical days must be positive when using historical volume");

            return new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);
        }

        private ValidationResult ValidateIcebergOrder(IcebergOrder order)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (order.TotalQuantity <= 0)
                errors.Add("Total quantity must be positive");

            if (!order.IsValidVisibleQuantity)
                errors.Add("Visible quantity must be positive and less than 20% of total quantity");

            if (order.UnderlyingType != OrderType.Market && order.UnderlyingType != OrderType.Limit)
                errors.Add("Iceberg orders only support Market or Limit underlying types");

            if (order.UnderlyingType == OrderType.Limit && !order.LimitPrice.HasValue)
                errors.Add("Limit price required for Limit order type");

            if (order.RandomizeVisibleQuantity)
            {
                if (!order.MinVisibleQuantity.HasValue || !order.MaxVisibleQuantity.HasValue)
                    errors.Add("Min and max visible quantities required when randomization is enabled");
                else if (order.MinVisibleQuantity >= order.MaxVisibleQuantity)
                    errors.Add("Min visible quantity must be less than max visible quantity");
            }

            return new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);
        }

        #endregion

        #region Order Management

        public async Task<AdvancedOrderStatus?> GetOrderStatusAsync(string orderId)
        {
            await _orderLock.WaitAsync();
            try
            {
                if (_activeOrders.TryGetValue(orderId, out var context))
                {
                    return new AdvancedOrderStatus(
                        orderId,
                        context.Type,
                        GetTotalQuantity(context.Order),
                        context.FilledQuantity,
                        GetTotalQuantity(context.Order) - context.FilledQuantity,
                        context.FilledQuantity > 0 ? context.TotalValue / context.FilledQuantity : 0,
                        context.Slices.Count,
                        context.Slices.Count(s => s.Status == SliceStatus.Filled),
                        context.Slices.Count(s => s.Status == SliceStatus.Pending),
                        context.StartTime,
                        context.State == AdvancedOrderState.Completed ? DateTime.UtcNow : null,
                        context.State,
                        context.Slices,
                        context.Statistics);
                }
                return null;
            }
            finally
            {
                _orderLock.Release();
            }
        }

        public async Task<bool> CancelOrderAsync(string orderId)
        {
            await _orderLock.WaitAsync();
            try
            {
                if (_activeOrders.TryGetValue(orderId, out var context))
                {
                    context.State = AdvancedOrderState.Cancelled;
                    
                    // Cancel any pending child orders
                    foreach (var slice in context.Slices.Where(s => s.Status == SliceStatus.Submitted && s.ChildOrderId != null))
                    {
                        await _executionEngine.CancelOrderAsync(slice.ChildOrderId);
                    }
                    
                    return true;
                }
                return false;
            }
            finally
            {
                _orderLock.Release();
            }
        }

        private decimal GetTotalQuantity(IAdvancedOrder order)
        {
            return order switch
            {
                TwapOrder twap => twap.TotalQuantity,
                VwapOrder vwap => vwap.TotalQuantity,
                IcebergOrder iceberg => iceberg.TotalQuantity,
                _ => 0m
            };
        }

        #endregion

        #region Nested Types

        private class AdvancedOrderContext
        {
            public IAdvancedOrder Order { get; set; } = null!;
            public AdvancedOrderType Type { get; set; }
            public AdvancedOrderState State { get; set; }
            public List<OrderSlice> Slices { get; set; } = new();
            public DateTime StartTime { get; set; }
            public decimal FilledQuantity { get; set; }
            public decimal TotalValue { get; set; }
            public decimal TotalCommission { get; set; }
            public decimal TotalSlippage { get; set; }
            public int SuccessfulSlices { get; set; }
            public int FailedSlices { get; set; }
            public ExecutionStatistics Statistics { get; set; } = null!;
            public List<VolumeProfile>? VolumeProfile { get; set; }
        }

        #endregion
    }

    #region Interfaces

    public interface IAdvancedOrderExecutionService
    {
        Task<AdvancedOrderResult> SubmitTwapOrderAsync(TwapOrder order);
        Task<AdvancedOrderResult> SubmitVwapOrderAsync(VwapOrder order);
        Task<AdvancedOrderResult> SubmitIcebergOrderAsync(IcebergOrder order);
        Task<AdvancedOrderStatus?> GetOrderStatusAsync(string orderId);
        Task<bool> CancelOrderAsync(string orderId);
    }

    public interface IVolumeAnalysisService
    {
        Task<List<VolumeProfile>> GetHistoricalVolumeProfileAsync(string symbol, int days);
        Task<List<VolumeProfile>> GetIntradayVolumeProfileAsync(string symbol);
    }

    #endregion
}