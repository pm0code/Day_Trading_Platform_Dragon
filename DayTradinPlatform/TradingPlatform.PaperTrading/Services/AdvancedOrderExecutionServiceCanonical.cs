using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.PaperTrading.Interfaces;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Advanced order execution service implementing TWAP, VWAP, and Iceberg orders.
    /// FULLY COMPLIANT with mandatory standards including method logging and TradingResult pattern.
    /// </summary>
    public class AdvancedOrderExecutionServiceCanonical : CanonicalServiceBase, IAdvancedOrderExecutionService
    {
        private readonly IOrderExecutionEngine _executionEngine;
        private readonly IMarketDataService _marketDataService;
        private readonly IVolumeAnalysisService _volumeAnalysisService;
        private readonly AdvancedOrderConfiguration _configuration;
        private readonly Dictionary<string, AdvancedOrderContext> _activeOrders;
        private readonly SemaphoreSlim _orderLock;
        private readonly Random _random;
        
        // Progress reporting
        private readonly Dictionary<string, IProgress<OrderExecutionProgress>> _progressReporters;

        public AdvancedOrderExecutionServiceCanonical(
            IOrderExecutionEngine executionEngine,
            IMarketDataService marketDataService,
            IVolumeAnalysisService volumeAnalysisService,
            AdvancedOrderConfiguration configuration,
            ITradingLogger logger)
            : base(logger, "AdvancedOrderExecution")
        {
            // Constructor logging is handled by base class
            _executionEngine = executionEngine;
            _marketDataService = marketDataService;
            _volumeAnalysisService = volumeAnalysisService;
            _configuration = configuration;
            _activeOrders = new Dictionary<string, AdvancedOrderContext>();
            _orderLock = new SemaphoreSlim(1, 1);
            _random = new Random();
            _progressReporters = new Dictionary<string, IProgress<OrderExecutionProgress>>();
        }

        #region TWAP Implementation

        public async Task<TradingResult<AdvancedOrderResult>> SubmitTwapOrderAsync(
            TwapOrder order, 
            IProgress<OrderExecutionProgress>? progress = null)
        {
            LogMethodEntry();
            try
            {
                // Validate order
                var validationResult = await ValidateTwapOrderAsync(order);
                if (!validationResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        "TWAP order validation failed",
                        "VALIDATION_ERROR",
                        validationResult.Errors);
                }

                // Create execution context
                var contextResult = await CreateTwapExecutionContextAsync(order);
                if (!contextResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        contextResult.ErrorMessage,
                        contextResult.ErrorCode);
                }

                var context = contextResult.Value!;

                // Register order with progress reporter
                await RegisterOrderAsync(order.OrderId, context, progress);

                // Start execution in background
                _ = Task.Run(() => ExecuteTwapOrderWithLoggingAsync(context));

                var result = new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "TWAP order submitted successfully",
                    AdvancedOrderType.TWAP,
                    DateTime.UtcNow,
                    validationResult.Value);

                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit TWAP order {OrderId}", order.OrderId);
                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Failure(
                    $"TWAP submission failed: {ex.Message}",
                    "SUBMISSION_ERROR");
            }
        }

        private async Task<TradingResult<AdvancedOrderContext>> CreateTwapExecutionContextAsync(TwapOrder order)
        {
            LogMethodEntry();
            try
            {
                var slicesResult = await GenerateTwapSlicesAsync(order);
                if (!slicesResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderContext>.Failure(
                        slicesResult.ErrorMessage,
                        slicesResult.ErrorCode);
                }

                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.TWAP,
                    State = AdvancedOrderState.Created,
                    Slices = slicesResult.Value!,
                    StartTime = order.StartTime,
                    Statistics = new ExecutionStatistics(0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                LogMethodExit();
                return TradingResult<AdvancedOrderContext>.Success(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create TWAP execution context");
                LogMethodExit();
                return TradingResult<AdvancedOrderContext>.Failure(
                    $"Context creation failed: {ex.Message}",
                    "CONTEXT_ERROR");
            }
        }

        private async Task ExecuteTwapOrderWithLoggingAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            try
            {
                await ExecuteTwapOrderAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TWAP order execution failed for {OrderId}", 
                    context.Order.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private async Task ExecuteTwapOrderAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            
            var twapOrder = (TwapOrder)context.Order;
            context.State = AdvancedOrderState.Running;
            
            var totalSlices = context.Slices.Count;
            var completedSlices = 0;

            try
            {
                foreach (var slice in context.Slices)
                {
                    // Report progress
                    await ReportProgressAsync(twapOrder.OrderId, new OrderExecutionProgress
                    {
                        OrderId = twapOrder.OrderId,
                        TotalQuantity = twapOrder.TotalQuantity,
                        FilledQuantity = context.FilledQuantity,
                        CompletedSlices = completedSlices,
                        TotalSlices = totalSlices,
                        PercentComplete = (decimal)completedSlices / totalSlices * 100,
                        CurrentState = context.State,
                        EstimatedTimeRemaining = EstimateTimeRemaining(context, completedSlices, totalSlices),
                        Message = $"Executing slice {completedSlices + 1} of {totalSlices}"
                    });

                    // Wait for scheduled time
                    var delay = slice.ScheduledTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        _logger.LogDebug("Waiting {Delay} for next TWAP slice", delay);
                        await Task.Delay(delay);
                    }

                    // Check if order was cancelled
                    if (context.State == AdvancedOrderState.Cancelled)
                    {
                        _logger.LogInformation("TWAP order {OrderId} cancelled", twapOrder.OrderId);
                        break;
                    }

                    // Apply randomization if enabled
                    var executeQuantity = slice.Quantity;
                    if (twapOrder.RandomizeSliceSize)
                    {
                        var randomResult = await RandomizeQuantityAsync(
                            slice.Quantity, 
                            twapOrder.MinSliceSize, 
                            twapOrder.MaxSliceSize);
                        
                        if (randomResult.IsSuccess)
                        {
                            executeQuantity = randomResult.Value;
                        }
                    }

                    // Execute slice
                    var executeResult = await ExecuteSliceAsync(context, slice, executeQuantity);
                    if (executeResult.IsSuccess)
                    {
                        completedSlices++;
                    }
                    else
                    {
                        _logger.LogWarning("Slice execution failed: {Error}", executeResult.ErrorMessage);
                    }
                }

                // Update final state
                context.State = context.FilledQuantity >= twapOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;

                // Final progress report
                await ReportProgressAsync(twapOrder.OrderId, new OrderExecutionProgress
                {
                    OrderId = twapOrder.OrderId,
                    TotalQuantity = twapOrder.TotalQuantity,
                    FilledQuantity = context.FilledQuantity,
                    CompletedSlices = completedSlices,
                    TotalSlices = totalSlices,
                    PercentComplete = 100m,
                    CurrentState = context.State,
                    Message = $"TWAP order {context.State}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TWAP order execution failed for {OrderId}", twapOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
                throw;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private async Task<TradingResult<List<OrderSlice>>> GenerateTwapSlicesAsync(TwapOrder order)
        {
            LogMethodEntry();
            try
            {
                var slices = new List<OrderSlice>();
                var sliceQuantity = order.AverageSliceSize;
                var interval = order.AverageInterval;
                var currentTime = order.StartTime;

                _logger.LogInformation(
                    "Generating {SliceCount} TWAP slices for {OrderId}, " +
                    "avg size: {AvgSize}, avg interval: {AvgInterval}",
                    order.NumberOfSlices, order.OrderId, sliceQuantity, interval);

                for (int i = 0; i < order.NumberOfSlices; i++)
                {
                    // Apply timing randomization
                    if (order.RandomizeSliceTiming && i > 0)
                    {
                        var randomResult = await ApplyTimingRandomizationAsync(
                            interval, 
                            order.MinInterval, 
                            order.MaxInterval);
                        
                        if (randomResult.IsSuccess)
                        {
                            currentTime = currentTime.Add(randomResult.Value);
                        }
                        else
                        {
                            currentTime = currentTime.Add(interval);
                        }
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

                _logger.LogInformation("Generated {Count} TWAP slices for {OrderId}", 
                    slices.Count, order.OrderId);

                LogMethodExit();
                return TradingResult<List<OrderSlice>>.Success(slices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate TWAP slices");
                LogMethodExit();
                return TradingResult<List<OrderSlice>>.Failure(
                    $"Slice generation failed: {ex.Message}",
                    "SLICE_GEN_ERROR");
            }
        }

        #endregion

        #region VWAP Implementation

        public async Task<TradingResult<AdvancedOrderResult>> SubmitVwapOrderAsync(
            VwapOrder order,
            IProgress<OrderExecutionProgress>? progress = null)
        {
            LogMethodEntry();
            try
            {
                // Validate order
                var validationResult = await ValidateVwapOrderAsync(order);
                if (!validationResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        "VWAP order validation failed",
                        "VALIDATION_ERROR",
                        validationResult.Errors);
                }

                // Get volume profile
                var volumeProfileResult = await GetVolumeProfileAsync(order);
                if (!volumeProfileResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        volumeProfileResult.ErrorMessage,
                        volumeProfileResult.ErrorCode);
                }

                // Create execution context
                var contextResult = await CreateVwapExecutionContextAsync(order, volumeProfileResult.Value!);
                if (!contextResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        contextResult.ErrorMessage,
                        contextResult.ErrorCode);
                }

                var context = contextResult.Value!;

                // Register order with progress reporter
                await RegisterOrderAsync(order.OrderId, context, progress);

                // Start execution in background
                _ = Task.Run(() => ExecuteVwapOrderWithLoggingAsync(context));

                var result = new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "VWAP order submitted successfully",
                    AdvancedOrderType.VWAP,
                    DateTime.UtcNow,
                    validationResult.Value);

                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit VWAP order {OrderId}", order.OrderId);
                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Failure(
                    $"VWAP submission failed: {ex.Message}",
                    "SUBMISSION_ERROR");
            }
        }

        private async Task<TradingResult<AdvancedOrderContext>> CreateVwapExecutionContextAsync(
            VwapOrder order, 
            List<VolumeProfile> volumeProfile)
        {
            LogMethodEntry();
            try
            {
                var slicesResult = await GenerateVwapSlicesAsync(order, volumeProfile);
                if (!slicesResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderContext>.Failure(
                        slicesResult.ErrorMessage,
                        slicesResult.ErrorCode);
                }

                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.VWAP,
                    State = AdvancedOrderState.Created,
                    Slices = slicesResult.Value!,
                    StartTime = order.StartTime,
                    VolumeProfile = volumeProfile,
                    Statistics = new ExecutionStatistics(0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                LogMethodExit();
                return TradingResult<AdvancedOrderContext>.Success(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create VWAP execution context");
                LogMethodExit();
                return TradingResult<AdvancedOrderContext>.Failure(
                    $"Context creation failed: {ex.Message}",
                    "CONTEXT_ERROR");
            }
        }

        private async Task ExecuteVwapOrderWithLoggingAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            try
            {
                await ExecuteVwapOrderAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VWAP order execution failed for {OrderId}", 
                    context.Order.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private async Task ExecuteVwapOrderAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            
            var vwapOrder = (VwapOrder)context.Order;
            context.State = AdvancedOrderState.Running;
            
            var totalSlices = context.Slices.Count;
            var completedSlices = 0;

            try
            {
                foreach (var slice in context.Slices)
                {
                    // Report progress
                    await ReportProgressAsync(vwapOrder.OrderId, new OrderExecutionProgress
                    {
                        OrderId = vwapOrder.OrderId,
                        TotalQuantity = vwapOrder.TotalQuantity,
                        FilledQuantity = context.FilledQuantity,
                        CompletedSlices = completedSlices,
                        TotalSlices = totalSlices,
                        PercentComplete = context.FilledQuantity / vwapOrder.TotalQuantity * 100,
                        CurrentState = context.State,
                        EstimatedTimeRemaining = EstimateTimeRemaining(context, completedSlices, totalSlices),
                        Message = $"Following volume profile - {completedSlices + 1}/{totalSlices} slices"
                    });

                    // Wait for scheduled time
                    var delay = slice.ScheduledTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        _logger.LogDebug("Waiting {Delay} for next VWAP slice", delay);
                        await Task.Delay(delay);
                    }

                    // Check if order was cancelled
                    if (context.State == AdvancedOrderState.Cancelled)
                    {
                        _logger.LogInformation("VWAP order {OrderId} cancelled", vwapOrder.OrderId);
                        break;
                    }

                    // Get current market volume and adjust slice size
                    var volumeResult = await _marketDataService.GetCurrentVolumeAsync(vwapOrder.Symbol);
                    if (!volumeResult.IsSuccess)
                    {
                        _logger.LogWarning("Failed to get current volume: {Error}", 
                            volumeResult.ErrorMessage);
                        continue;
                    }
                    
                    // Calculate dynamic slice size based on participation rate
                    var targetQuantity = volumeResult.Value * vwapOrder.ParticipationRate;
                    targetQuantity = Math.Max(vwapOrder.MinSliceSize, 
                                            Math.Min(vwapOrder.MaxSliceSize, targetQuantity));

                    // Execute slice
                    var executeResult = await ExecuteSliceAsync(context, slice, targetQuantity);
                    if (executeResult.IsSuccess)
                    {
                        completedSlices++;
                    }
                    else
                    {
                        _logger.LogWarning("VWAP slice execution failed: {Error}", 
                            executeResult.ErrorMessage);
                    }
                }

                // Update final state
                context.State = context.FilledQuantity >= vwapOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;

                // Final progress report
                await ReportProgressAsync(vwapOrder.OrderId, new OrderExecutionProgress
                {
                    OrderId = vwapOrder.OrderId,
                    TotalQuantity = vwapOrder.TotalQuantity,
                    FilledQuantity = context.FilledQuantity,
                    CompletedSlices = completedSlices,
                    TotalSlices = totalSlices,
                    PercentComplete = 100m,
                    CurrentState = context.State,
                    Message = $"VWAP order {context.State}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VWAP order execution failed for {OrderId}", vwapOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
                throw;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private async Task<TradingResult<List<VolumeProfile>>> GetVolumeProfileAsync(VwapOrder order)
        {
            LogMethodEntry();
            try
            {
                TradingResult<List<VolumeProfile>> result;
                
                if (order.UseHistoricalVolume)
                {
                    _logger.LogInformation("Getting historical volume profile for {Symbol} over {Days} days",
                        order.Symbol, order.HistoricalDays);
                    
                    result = await _volumeAnalysisService.GetHistoricalVolumeProfileAsync(
                        order.Symbol, 
                        order.HistoricalDays);
                }
                else
                {
                    _logger.LogInformation("Getting intraday volume profile for {Symbol}", order.Symbol);
                    
                    result = await _volumeAnalysisService.GetIntradayVolumeProfileAsync(
                        order.Symbol);
                }

                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get volume profile");
                LogMethodExit();
                return TradingResult<List<VolumeProfile>>.Failure(
                    $"Volume profile retrieval failed: {ex.Message}",
                    "VOLUME_PROFILE_ERROR");
            }
        }

        private async Task<TradingResult<List<OrderSlice>>> GenerateVwapSlicesAsync(
            VwapOrder order, 
            List<VolumeProfile> volumeProfile)
        {
            LogMethodEntry();
            try
            {
                var slices = new List<OrderSlice>();
                var remainingQuantity = order.TotalQuantity;
                var currentTime = order.StartTime;

                _logger.LogInformation(
                    "Generating VWAP slices for {OrderId} based on {ProfileCount} volume points",
                    order.OrderId, volumeProfile.Count);

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

                _logger.LogInformation("Generated {Count} VWAP slices for {OrderId}", 
                    slices.Count, order.OrderId);

                LogMethodExit();
                return TradingResult<List<OrderSlice>>.Success(slices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate VWAP slices");
                LogMethodExit();
                return TradingResult<List<OrderSlice>>.Failure(
                    $"VWAP slice generation failed: {ex.Message}",
                    "SLICE_GEN_ERROR");
            }
        }

        #endregion

        #region Iceberg Implementation

        public async Task<TradingResult<AdvancedOrderResult>> SubmitIcebergOrderAsync(
            IcebergOrder order,
            IProgress<OrderExecutionProgress>? progress = null)
        {
            LogMethodEntry();
            try
            {
                // Validate order
                var validationResult = await ValidateIcebergOrderAsync(order);
                if (!validationResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<AdvancedOrderResult>.Failure(
                        "Iceberg order validation failed",
                        "VALIDATION_ERROR",
                        validationResult.Errors);
                }

                // Create execution context
                var context = new AdvancedOrderContext
                {
                    Order = order,
                    Type = AdvancedOrderType.Iceberg,
                    State = AdvancedOrderState.Created,
                    Slices = new List<OrderSlice>(),
                    StartTime = DateTime.UtcNow,
                    Statistics = new ExecutionStatistics(0, 0, 0, 0, TimeSpan.Zero, 0, 0, 0, 0)
                };

                // Register order with progress reporter
                await RegisterOrderAsync(order.OrderId, context, progress);

                // Start execution in background
                _ = Task.Run(() => ExecuteIcebergOrderWithLoggingAsync(context));

                var result = new AdvancedOrderResult(
                    true,
                    order.OrderId,
                    "Iceberg order submitted successfully",
                    AdvancedOrderType.Iceberg,
                    DateTime.UtcNow,
                    validationResult.Value);

                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit Iceberg order {OrderId}", order.OrderId);
                LogMethodExit();
                return TradingResult<AdvancedOrderResult>.Failure(
                    $"Iceberg submission failed: {ex.Message}",
                    "SUBMISSION_ERROR");
            }
        }

        private async Task ExecuteIcebergOrderWithLoggingAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            try
            {
                await ExecuteIcebergOrderAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Iceberg order execution failed for {OrderId}", 
                    context.Order.OrderId);
                context.State = AdvancedOrderState.Failed;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private async Task ExecuteIcebergOrderAsync(AdvancedOrderContext context)
        {
            LogMethodEntry();
            
            var icebergOrder = (IcebergOrder)context.Order;
            context.State = AdvancedOrderState.Running;
            var remainingQuantity = icebergOrder.TotalQuantity;
            var refillCount = 0;

            try
            {
                while (remainingQuantity > 0 && context.State == AdvancedOrderState.Running)
                {
                    // Report progress
                    await ReportProgressAsync(icebergOrder.OrderId, new OrderExecutionProgress
                    {
                        OrderId = icebergOrder.OrderId,
                        TotalQuantity = icebergOrder.TotalQuantity,
                        FilledQuantity = context.FilledQuantity,
                        CompletedSlices = refillCount,
                        TotalSlices = icebergOrder.EstimatedRefills,
                        PercentComplete = context.FilledQuantity / icebergOrder.TotalQuantity * 100,
                        CurrentState = context.State,
                        Message = $"Iceberg refill #{refillCount + 1}, remaining: {remainingQuantity:N0}"
                    });

                    // Determine visible quantity for this iteration
                    var visibleQuantity = icebergOrder.VisibleQuantity;
                    if (icebergOrder.RandomizeVisibleQuantity)
                    {
                        var randomResult = await RandomizeQuantityAsync(
                            icebergOrder.VisibleQuantity,
                            icebergOrder.MinVisibleQuantity,
                            icebergOrder.MaxVisibleQuantity);
                        
                        if (randomResult.IsSuccess)
                        {
                            visibleQuantity = randomResult.Value;
                        }
                    }

                    // Ensure we don't exceed remaining quantity
                    visibleQuantity = Math.Min(visibleQuantity, remainingQuantity);

                    _logger.LogInformation(
                        "Creating iceberg slice #{RefillCount} for {OrderId}, " +
                        "visible: {VisibleQty}, remaining: {RemainingQty}",
                        refillCount + 1, icebergOrder.OrderId, visibleQuantity, remainingQuantity);

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

                    var submitResult = await _executionEngine.SubmitOrderAsync(childOrder);
                    
                    if (submitResult.IsSuccess && submitResult.OrderId != null)
                    {
                        // Monitor child order until filled or cancelled
                        var monitorResult = await MonitorIcebergSliceAsync(
                            context, slice, submitResult.OrderId);
                        
                        if (monitorResult.IsSuccess)
                        {
                            var filledQuantity = monitorResult.Value;
                            remainingQuantity -= filledQuantity;
                            context.FilledQuantity += filledQuantity;
                            refillCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Iceberg slice monitoring failed: {Error}", 
                                monitorResult.ErrorMessage);
                            break;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Iceberg slice submission failed for {OrderId}: {Error}", 
                            icebergOrder.OrderId, submitResult.ErrorMessage);
                        break;
                    }

                    // Add small delay to avoid appearing algorithmic
                    if (_configuration.EnableAntiGaming && remainingQuantity > 0)
                    {
                        var delay = TimeSpan.FromMilliseconds(_random.Next(500, 2000));
                        _logger.LogDebug("Anti-gaming delay: {Delay}", delay);
                        await Task.Delay(delay);
                    }
                }

                // Update final state
                context.State = context.FilledQuantity >= icebergOrder.TotalQuantity * 0.95m
                    ? AdvancedOrderState.Completed
                    : AdvancedOrderState.Failed;

                // Final progress report
                await ReportProgressAsync(icebergOrder.OrderId, new OrderExecutionProgress
                {
                    OrderId = icebergOrder.OrderId,
                    TotalQuantity = icebergOrder.TotalQuantity,
                    FilledQuantity = context.FilledQuantity,
                    CompletedSlices = refillCount,
                    TotalSlices = icebergOrder.EstimatedRefills,
                    PercentComplete = 100m,
                    CurrentState = context.State,
                    Message = $"Iceberg order {context.State}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Iceberg order execution failed for {OrderId}", 
                    icebergOrder.OrderId);
                context.State = AdvancedOrderState.Failed;
                throw;
            }
            finally
            {
                LogMethodExit();
            }
        }

        #endregion

        #region Common Execution Methods

        private async Task<TradingResult> ExecuteSliceAsync(
            AdvancedOrderContext context, 
            OrderSlice slice, 
            decimal quantity)
        {
            LogMethodEntry();
            try
            {
                slice = slice with { Status = SliceStatus.Submitted };

                _logger.LogInformation(
                    "Executing slice {SliceId} for {ParentOrderId}, quantity: {Quantity}",
                    slice.SliceId, slice.ParentOrderId, quantity);

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
                    var executionResult = await _executionEngine.GetExecutionDetailsAsync(result.OrderId);
                    if (executionResult.IsSuccess && executionResult.Value != null)
                    {
                        var execution = executionResult.Value;
                        slice = slice with { ExecutedPrice = execution.Price };
                        context.FilledQuantity += execution.Quantity;
                        context.TotalCommission += execution.Commission;
                        context.TotalSlippage += execution.Slippage;
                        context.SuccessfulSlices++;

                        // Update statistics
                        UpdateExecutionStatistics(context, execution, DateTime.UtcNow - startTime);
                        
                        _logger.LogInformation(
                            "Slice {SliceId} executed successfully. Price: {Price}, Slippage: {Slippage}",
                            slice.SliceId, execution.Price, execution.Slippage);
                    }
                }
                else
                {
                    slice = slice with { Status = SliceStatus.Failed };
                    context.FailedSlices++;
                    
                    _logger.LogWarning("Slice {SliceId} execution failed: {Error}", 
                        slice.SliceId, result.ErrorMessage);
                }

                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute slice {SliceId}", slice.SliceId);
                slice = slice with { Status = SliceStatus.Failed };
                context.FailedSlices++;
                
                LogMethodExit();
                return TradingResult.Failure($"Slice execution failed: {ex.Message}", "SLICE_EXEC_ERROR");
            }
        }

        private void UpdateExecutionStatistics(
            AdvancedOrderContext context, 
            Execution execution, 
            TimeSpan executionTime)
        {
            LogMethodEntry();
            
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

            _logger.LogDebug(
                "Updated execution statistics - Avg slippage: {AvgSlippage:P4}, " +
                "Avg exec time: {AvgExecTime}, Success rate: {SuccessRate:P2}",
                newAvgSlippage, newAvgExecTime, 
                (decimal)context.SuccessfulSlices / totalSlices);

            LogMethodExit();
        }

        private async Task<TradingResult<decimal>> RandomizeQuantityAsync(
            decimal baseQuantity, 
            decimal? min, 
            decimal? max)
        {
            LogMethodEntry();
            try
            {
                var randomFactor = (_random.NextDouble() - 0.5) * 2 * (double)_configuration.SliceSizeRandomization;
                var adjustedQuantity = baseQuantity * (1 + (decimal)randomFactor);

                if (min.HasValue && adjustedQuantity < min.Value)
                    adjustedQuantity = min.Value;
                if (max.HasValue && adjustedQuantity > max.Value)
                    adjustedQuantity = max.Value;

                // Round to nearest 100 shares to avoid odd lots
                adjustedQuantity = Math.Round(adjustedQuantity / 100) * 100;

                _logger.LogDebug(
                    "Randomized quantity from {Base} to {Adjusted} (factor: {Factor:P2})",
                    baseQuantity, adjustedQuantity, randomFactor);

                LogMethodExit();
                return TradingResult<decimal>.Success(adjustedQuantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to randomize quantity");
                LogMethodExit();
                return TradingResult<decimal>.Failure($"Randomization failed: {ex.Message}", "RANDOM_ERROR");
            }
        }

        private async Task<TradingResult<TimeSpan>> ApplyTimingRandomizationAsync(
            TimeSpan baseInterval,
            TimeSpan? minInterval,
            TimeSpan? maxInterval)
        {
            LogMethodEntry();
            try
            {
                var randomFactor = (_random.NextDouble() - 0.5) * 2 * (double)_configuration.SliceTimingRandomization;
                var adjustedInterval = TimeSpan.FromTicks((long)(baseInterval.Ticks * (1 + randomFactor)));
                
                // Respect min/max intervals
                if (minInterval.HasValue && adjustedInterval < minInterval.Value)
                    adjustedInterval = minInterval.Value;
                if (maxInterval.HasValue && adjustedInterval > maxInterval.Value)
                    adjustedInterval = maxInterval.Value;

                _logger.LogDebug(
                    "Randomized interval from {Base} to {Adjusted} (factor: {Factor:P2})",
                    baseInterval, adjustedInterval, randomFactor);

                LogMethodExit();
                return TradingResult<TimeSpan>.Success(adjustedInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to randomize timing");
                LogMethodExit();
                return TradingResult<TimeSpan>.Failure($"Timing randomization failed: {ex.Message}", "TIMING_ERROR");
            }
        }

        private async Task<TradingResult<decimal>> MonitorIcebergSliceAsync(
            AdvancedOrderContext context, 
            OrderSlice slice, 
            string childOrderId)
        {
            LogMethodEntry();
            try
            {
                var maxWaitTime = TimeSpan.FromMinutes(5);
                var startTime = DateTime.UtcNow;
                var checkInterval = TimeSpan.FromSeconds(1);

                _logger.LogInformation(
                    "Monitoring iceberg slice {SliceId} with child order {OrderId}",
                    slice.SliceId, childOrderId);

                while (DateTime.UtcNow - startTime < maxWaitTime)
                {
                    var statusResult = await _executionEngine.GetOrderStatusAsync(childOrderId);
                    if (!statusResult.IsSuccess)
                    {
                        _logger.LogWarning("Failed to get order status: {Error}", statusResult.ErrorMessage);
                        await Task.Delay(checkInterval);
                        continue;
                    }
                    
                    var status = statusResult.Value;
                    if (status == OrderStatus.Filled || status == OrderStatus.Cancelled)
                    {
                        _logger.LogInformation("Iceberg slice {SliceId} status: {Status}", 
                            slice.SliceId, status);
                        break;
                    }
                    
                    await Task.Delay(checkInterval);
                }

                var filledQtyResult = await GetFilledQuantityAsync(childOrderId);
                
                LogMethodExit();
                return filledQtyResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to monitor iceberg slice");
                LogMethodExit();
                return TradingResult<decimal>.Failure($"Monitoring failed: {ex.Message}", "MONITOR_ERROR");
            }
        }

        private async Task<TradingResult<decimal>> GetFilledQuantityAsync(string orderId)
        {
            LogMethodEntry();
            try
            {
                var orderResult = await _executionEngine.GetOrderAsync(orderId);
                if (!orderResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<decimal>.Failure(orderResult.ErrorMessage, orderResult.ErrorCode);
                }

                var filledQty = orderResult.Value?.FilledQuantity ?? 0m;
                
                _logger.LogDebug("Order {OrderId} filled quantity: {FilledQty}", orderId, filledQty);
                
                LogMethodExit();
                return TradingResult<decimal>.Success(filledQty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get filled quantity for order {OrderId}", orderId);
                LogMethodExit();
                return TradingResult<decimal>.Failure($"Failed to get filled quantity: {ex.Message}", "FILL_QTY_ERROR");
            }
        }

        #endregion

        #region Order Management

        private async Task RegisterOrderAsync(
            string orderId, 
            AdvancedOrderContext context,
            IProgress<OrderExecutionProgress>? progress)
        {
            LogMethodEntry();
            
            await _orderLock.WaitAsync();
            try
            {
                _activeOrders[orderId] = context;
                
                if (progress != null)
                {
                    _progressReporters[orderId] = progress;
                }
                
                _logger.LogInformation("Registered order {OrderId} of type {Type}", 
                    orderId, context.Type);
            }
            finally
            {
                _orderLock.Release();
                LogMethodExit();
            }
        }

        private async Task ReportProgressAsync(string orderId, OrderExecutionProgress progress)
        {
            LogMethodEntry();
            
            await _orderLock.WaitAsync();
            try
            {
                if (_progressReporters.TryGetValue(orderId, out var reporter))
                {
                    reporter.Report(progress);
                    
                    _logger.LogDebug(
                        "Progress reported for {OrderId}: {PercentComplete:F1}% - {Message}",
                        orderId, progress.PercentComplete, progress.Message);
                }
            }
            finally
            {
                _orderLock.Release();
                LogMethodExit();
            }
        }

        public async Task<TradingResult<AdvancedOrderStatus>> GetOrderStatusAsync(string orderId)
        {
            LogMethodEntry();
            
            await _orderLock.WaitAsync();
            try
            {
                if (_activeOrders.TryGetValue(orderId, out var context))
                {
                    var status = new AdvancedOrderStatus(
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

                    LogMethodExit();
                    return TradingResult<AdvancedOrderStatus>.Success(status);
                }
                
                LogMethodExit();
                return TradingResult<AdvancedOrderStatus>.Failure(
                    $"Order {orderId} not found",
                    "ORDER_NOT_FOUND");
            }
            finally
            {
                _orderLock.Release();
            }
        }

        public async Task<TradingResult<bool>> CancelOrderAsync(string orderId)
        {
            LogMethodEntry();
            
            await _orderLock.WaitAsync();
            try
            {
                if (_activeOrders.TryGetValue(orderId, out var context))
                {
                    context.State = AdvancedOrderState.Cancelled;
                    
                    _logger.LogInformation("Cancelling order {OrderId}", orderId);
                    
                    // Cancel any pending child orders
                    foreach (var slice in context.Slices.Where(s => s.Status == SliceStatus.Submitted && s.ChildOrderId != null))
                    {
                        var cancelResult = await _executionEngine.CancelOrderAsync(slice.ChildOrderId);
                        if (!cancelResult.IsSuccess)
                        {
                            _logger.LogWarning("Failed to cancel child order {OrderId}: {Error}", 
                                slice.ChildOrderId, cancelResult.ErrorMessage);
                        }
                    }
                    
                    LogMethodExit();
                    return TradingResult<bool>.Success(true);
                }
                
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }
            finally
            {
                _orderLock.Release();
            }
        }

        private decimal GetTotalQuantity(IAdvancedOrder order)
        {
            LogMethodEntry();
            
            var quantity = order switch
            {
                TwapOrder twap => twap.TotalQuantity,
                VwapOrder vwap => vwap.TotalQuantity,
                IcebergOrder iceberg => iceberg.TotalQuantity,
                _ => 0m
            };
            
            LogMethodExit();
            return quantity;
        }

        private TimeSpan EstimateTimeRemaining(
            AdvancedOrderContext context, 
            int completedSlices, 
            int totalSlices)
        {
            LogMethodEntry();
            
            if (completedSlices == 0 || totalSlices == 0)
            {
                LogMethodExit();
                return TimeSpan.Zero;
            }

            var elapsedTime = DateTime.UtcNow - context.StartTime;
            var avgTimePerSlice = elapsedTime / completedSlices;
            var remainingSlices = totalSlices - completedSlices;
            var estimatedTime = avgTimePerSlice * remainingSlices;
            
            _logger.LogDebug(
                "Estimated time remaining: {Time} ({Completed}/{Total} slices)",
                estimatedTime, completedSlices, totalSlices);
            
            LogMethodExit();
            return estimatedTime;
        }

        #endregion

        #region Validation Methods

        private async Task<TradingResult<ValidationResult>> ValidateTwapOrderAsync(TwapOrder order)
        {
            LogMethodEntry();
            
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

            var validationResult = new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);

            LogMethodExit();
            
            return errors.Count == 0 
                ? TradingResult<ValidationResult>.Success(validationResult)
                : TradingResult<ValidationResult>.Failure("Validation failed", "VALIDATION_ERROR", errors);
        }

        private async Task<TradingResult<ValidationResult>> ValidateVwapOrderAsync(VwapOrder order)
        {
            LogMethodEntry();
            
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

            var validationResult = new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);

            LogMethodExit();
            
            return errors.Count == 0 
                ? TradingResult<ValidationResult>.Success(validationResult)
                : TradingResult<ValidationResult>.Failure("Validation failed", "VALIDATION_ERROR", errors);
        }

        private async Task<TradingResult<ValidationResult>> ValidateIcebergOrderAsync(IcebergOrder order)
        {
            LogMethodEntry();
            
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

            var validationResult = new ValidationResult(
                errors.Count == 0,
                errors,
                warnings);

            LogMethodExit();
            
            return errors.Count == 0 
                ? TradingResult<ValidationResult>.Success(validationResult)
                : TradingResult<ValidationResult>.Failure("Validation failed", "VALIDATION_ERROR", errors);
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

    #region Progress Reporting

    /// <summary>
    /// Progress information for advanced order execution
    /// </summary>
    public class OrderExecutionProgress
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public decimal FilledQuantity { get; set; }
        public int CompletedSlices { get; set; }
        public int TotalSlices { get; set; }
        public decimal PercentComplete { get; set; }
        public AdvancedOrderState CurrentState { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}