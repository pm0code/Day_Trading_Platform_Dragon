// File: TradingPlatform.RiskManagement.Services\PositionMonitorCanonical.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.RiskManagement.Models;

namespace TradingPlatform.RiskManagement.Services
{
    /// <summary>
    /// Canonical implementation of position monitoring with real-time P&L tracking,
    /// exposure limits, and automated risk alerts with comprehensive monitoring.
    /// </summary>
    public class PositionMonitorCanonical : CanonicalServiceBase, IPositionMonitor
    {
        private readonly IMessageBus _messageBus;
        private readonly ConcurrentDictionary<string, Position> _positions = new();
        private readonly ConcurrentDictionary<string, decimal> _symbolExposures = new();
        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
        
        private decimal _totalExposure;
        private decimal _totalUnrealizedPnL;
        private long _positionUpdates;
        private long _priceUpdates;
        
        private const decimal MAX_SINGLE_POSITION_EXPOSURE = 0.20m; // 20% of portfolio
        private const decimal MAX_SECTOR_EXPOSURE = 0.30m; // 30% of portfolio
        private const decimal POSITION_WARNING_THRESHOLD = 0.15m; // 15% of portfolio

        public PositionMonitorCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider.GetRequiredService<ITradingLogger>(), "PositionMonitor")
        {
            _messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            // Subscribe to market data updates
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageBus.SubscribeAsync<MarketDataEvent>(
                        "marketdata.price.updated",
                        "risk-management",
                        "position-monitor",
                        async (message) => await HandlePriceUpdateAsync(message),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    LogError("Error in price update subscription", ex);
                }
            }, cancellationToken);

            // Subscribe to order execution events
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageBus.SubscribeAsync<OrderEvent>(
                        "orders.executed",
                        "risk-management",
                        "position-monitor",
                        async (message) => await HandleOrderExecutionAsync(message),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    LogError("Error in order execution subscription", ex);
                }
            }, cancellationToken);

            LogInfo("Position monitor initialized and subscribed to market events");
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<Position>> GetAllPositionsAsync()
        {
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var positions = _positions.Values.ToList();
                    
                    LogDebug($"Retrieved {positions.Count} positions",
                        new { PositionCount = positions.Count, TotalExposure = _totalExposure });

                    return await Task.FromResult(TradingResult<IEnumerable<Position>>.Success(positions));
                },
                nameof(GetAllPositionsAsync));
            
            return result.IsSuccess ? result.Value : Enumerable.Empty<Position>();
        }

        public async Task<Position?> GetPositionAsync(string symbol)
        {
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    _positions.TryGetValue(symbol, out var position);
                    
                    if (position != null)
                    {
                        LogDebug($"Retrieved position for {symbol}",
                            new 
                            { 
                                Symbol = symbol, 
                                Quantity = position.Quantity,
                                UnrealizedPnL = position.UnrealizedPnL
                            });
                    }

                    return await Task.FromResult(TradingResult<Position?>.Success(position));
                },
                nameof(GetPositionAsync));
            
            return result.IsSuccess ? result.Value : null;
        }

        public async Task UpdatePositionAsync(Position position)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));

            await ExecuteServiceOperationAsync(
                async () =>
                {
                    await _updateSemaphore.WaitAsync();
                    try
                    {
                        var previousPosition = _positions.GetValueOrDefault(position.Symbol);
                        _positions.AddOrUpdate(position.Symbol, position, (key, old) => position);

                        // Update exposure tracking
                        await UpdateExposureMetricsAsync(position, previousPosition);

                        // Check position limits
                        await CheckPositionLimitsAsync(position);

                        // Publish position update event
                        await _messageBus.PublishAsync("positions.updated", new PositionUpdatedEvent
                        {
                            Source = "PositionMonitor",
                            Symbol = position.Symbol,
                            Quantity = position.Quantity,
                            CurrentPrice = position.CurrentPrice,
                            UnrealizedPnL = position.UnrealizedPnL,
                            Timestamp = DateTimeOffset.UtcNow
                        });

                        Interlocked.Increment(ref _positionUpdates);

                        LogInfo($"Position updated for {position.Symbol}",
                            new
                            {
                                Symbol = position.Symbol,
                                Quantity = position.Quantity,
                                AveragePrice = position.AveragePrice,
                                CurrentPrice = position.CurrentPrice,
                                UnrealizedPnL = position.UnrealizedPnL
                            });

                        return TradingResult<object>.Success(null!);
                    }
                    finally
                    {
                        _updateSemaphore.Release();
                    }
                },
                nameof(UpdatePositionAsync));
        }

        public async Task<decimal> GetTotalExposureAsync()
        {
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    await _updateSemaphore.WaitAsync();
                    try
                    {
                        _totalExposure = _positions.Values.Sum(p => Math.Abs(p.Quantity * p.CurrentPrice));
                        
                        LogDebug($"Total exposure calculated: ${_totalExposure:N2}",
                            new { TotalExposure = _totalExposure });

                        return TradingResult<decimal>.Success(_totalExposure);
                    }
                    finally
                    {
                        _updateSemaphore.Release();
                    }
                },
                nameof(GetTotalExposureAsync));
            
            return result.IsSuccess ? result.Value : 0m;
        }

        public async Task<decimal> GetSymbolExposureAsync(string symbol)
        {
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_positions.TryGetValue(symbol, out var position))
                    {
                        var exposure = Math.Abs(position.Quantity * position.CurrentPrice);
                        
                        LogDebug($"Symbol exposure for {symbol}: ${exposure:N2}",
                            new { Symbol = symbol, Exposure = exposure });

                        return TradingResult<decimal>.Success(exposure);
                    }

                    return await Task.FromResult(TradingResult<decimal>.Success(0m));
                },
                nameof(GetSymbolExposureAsync));
            
            return result.IsSuccess ? result.Value : 0m;
        }

        public async Task<IEnumerable<Position>> GetPositionsExceedingLimitsAsync()
        {
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var totalExposure = await GetTotalExposureAsync();
                    var exceedingPositions = new List<Position>();

                    foreach (var position in _positions.Values)
                    {
                        var positionExposure = Math.Abs(position.Quantity * position.CurrentPrice);
                        var exposureRatio = totalExposure > 0 ? positionExposure / totalExposure : 0;

                        if (exposureRatio > MAX_SINGLE_POSITION_EXPOSURE)
                        {
                            exceedingPositions.Add(position);
                            
                            LogWarning($"Position exceeds exposure limit: {position.Symbol}",
                                additionalData: new
                                {
                                    Symbol = position.Symbol,
                                    Exposure = positionExposure,
                                    ExposureRatio = exposureRatio,
                                    Limit = MAX_SINGLE_POSITION_EXPOSURE
                                });
                        }
                    }

                    return TradingResult<IEnumerable<Position>>.Success(exceedingPositions);
                },
                nameof(GetPositionsExceedingLimitsAsync));
            
            return result.IsSuccess ? result.Value : Enumerable.Empty<Position>();
        }

        private async Task HandlePriceUpdateAsync(object message)
        {
            if (message is MarketDataEvent priceUpdate && priceUpdate.EventType == "MarketData")
            {
                if (_positions.TryGetValue(priceUpdate.Symbol, out var position))
                {
                    var previousPrice = position.CurrentPrice;
                    
                    // Create new position with updated values (immutable record)
                    var updatedPosition = position with 
                    { 
                        CurrentPrice = priceUpdate.Price,
                        UnrealizedPnL = position.Quantity * (priceUpdate.Price - position.AveragePrice),
                        LastUpdated = priceUpdate.Timestamp.DateTime
                    };

                    // Update position
                    await UpdatePositionAsync(updatedPosition);

                    Interlocked.Increment(ref _priceUpdates);

                    // Check for significant price movements
                    if (previousPrice > 0)
                    {
                        var priceChange = Math.Abs((priceUpdate.Price - previousPrice) / previousPrice);
                        if (priceChange > 0.05m) // 5% price movement
                        {
                            LogWarning($"Significant price movement detected for {priceUpdate.Symbol}",
                                additionalData: new
                                {
                                    Symbol = priceUpdate.Symbol,
                                    PreviousPrice = previousPrice,
                                    NewPrice = priceUpdate.Price,
                                    ChangePercent = priceChange * 100
                                });
                        }
                    }
                }
            }
        }

        private async Task HandleOrderExecutionAsync(object message)
        {
            if (message is OrderEvent orderExecuted && orderExecuted.Status == "Filled")
            {
                var existingPosition = await GetPositionAsync(orderExecuted.Symbol);
                Position updatedPosition;
                
                if (existingPosition == null)
                {
                    // Create new position
                    updatedPosition = new Position(
                        Symbol: orderExecuted.Symbol,
                        Quantity: orderExecuted.Quantity,
                        AveragePrice: orderExecuted.Price ?? 0m,
                        CurrentPrice: orderExecuted.Price ?? 0m,
                        UnrealizedPnL: 0m,
                        RealizedPnL: 0m,
                        MarketValue: orderExecuted.Quantity * (orderExecuted.Price ?? 0m),
                        RiskExposure: Math.Abs(orderExecuted.Quantity * (orderExecuted.Price ?? 0m)),
                        OpenTime: orderExecuted.ExecutionTime.DateTime,
                        LastUpdated: orderExecuted.ExecutionTime.DateTime
                    );
                }
                else
                {
                    // Update existing position
                    if (orderExecuted.Side == "Buy")
                    {
                        // Calculate weighted average entry price
                        var totalCost = (existingPosition.Quantity * existingPosition.AveragePrice) + 
                                       (orderExecuted.Quantity * (orderExecuted.Price ?? 0m));
                        var newQuantity = existingPosition.Quantity + orderExecuted.Quantity;
                        
                        updatedPosition = existingPosition with
                        {
                            Quantity = newQuantity,
                            AveragePrice = newQuantity > 0 ? totalCost / newQuantity : (orderExecuted.Price ?? 0m),
                            CurrentPrice = orderExecuted.Price ?? existingPosition.CurrentPrice,
                            MarketValue = newQuantity * (orderExecuted.Price ?? existingPosition.CurrentPrice),
                            RiskExposure = Math.Abs(newQuantity * (orderExecuted.Price ?? existingPosition.CurrentPrice)),
                            LastUpdated = orderExecuted.ExecutionTime.DateTime
                        };
                    }
                    else // Sell
                    {
                        var newQuantity = existingPosition.Quantity - orderExecuted.Quantity;
                        
                        // If position is closed
                        if (Math.Abs(newQuantity) < 0.0001m)
                        {
                            _positions.TryRemove(existingPosition.Symbol, out _);
                            
                            LogInfo($"Position closed for {existingPosition.Symbol}",
                                new { Symbol = existingPosition.Symbol, RealizedPnL = existingPosition.RealizedPnL });
                            
                            return;
                        }
                        
                        updatedPosition = existingPosition with
                        {
                            Quantity = newQuantity,
                            CurrentPrice = orderExecuted.Price ?? existingPosition.CurrentPrice,
                            MarketValue = newQuantity * (orderExecuted.Price ?? existingPosition.CurrentPrice),
                            RiskExposure = Math.Abs(newQuantity * (orderExecuted.Price ?? existingPosition.CurrentPrice)),
                            LastUpdated = orderExecuted.ExecutionTime.DateTime
                        };
                    }
                }

                // Update unrealized P&L
                updatedPosition = updatedPosition with 
                { 
                    UnrealizedPnL = updatedPosition.Quantity * (updatedPosition.CurrentPrice - updatedPosition.AveragePrice)
                };

                await UpdatePositionAsync(updatedPosition);
            }
        }

        private async Task UpdateExposureMetricsAsync(Position newPosition, Position? previousPosition)
        {
            await _updateSemaphore.WaitAsync();
            try
            {
                // Update symbol exposure
                var newExposure = Math.Abs(newPosition.Quantity * newPosition.CurrentPrice);
                var previousExposure = previousPosition != null 
                    ? Math.Abs(previousPosition.Quantity * previousPosition.CurrentPrice) 
                    : 0m;

                _symbolExposures.AddOrUpdate(newPosition.Symbol, newExposure, (key, old) => newExposure);

                // Update totals
                _totalExposure = _symbolExposures.Values.Sum();
                _totalUnrealizedPnL = _positions.Values.Sum(p => p.UnrealizedPnL);

                // Record metrics
                UpdateMetric("TotalExposure", _totalExposure);
                UpdateMetric("TotalUnrealizedPnL", _totalUnrealizedPnL);
                UpdateMetric("PositionCount", _positions.Count);
                UpdateMetric($"Exposure.{newPosition.Symbol}", newExposure);
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        private async Task CheckPositionLimitsAsync(Position position)
        {
            var totalExposure = await GetTotalExposureAsync();
            var positionExposure = Math.Abs(position.Quantity * position.CurrentPrice);
            var exposureRatio = totalExposure > 0 ? positionExposure / totalExposure : 0;

            if (exposureRatio > MAX_SINGLE_POSITION_EXPOSURE)
            {
                await _messageBus.PublishAsync("risk.limit.breached", new RiskEvent
                {
                    Source = "PositionMonitor",
                    RiskType = "Position",
                    Symbol = position.Symbol,
                    CurrentExposure = exposureRatio,
                    RiskLimit = MAX_SINGLE_POSITION_EXPOSURE,
                    UtilizationPercent = (exposureRatio / MAX_SINGLE_POSITION_EXPOSURE) * 100,
                    LimitBreached = true,
                    Action = "Block",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            else if (exposureRatio > POSITION_WARNING_THRESHOLD)
            {
                LogWarning($"Position approaching exposure limit: {position.Symbol}",
                    additionalData: new
                    {
                        Symbol = position.Symbol,
                        ExposureRatio = exposureRatio,
                        WarningThreshold = POSITION_WARNING_THRESHOLD,
                        Limit = MAX_SINGLE_POSITION_EXPOSURE
                    });
            }

            // Check unrealized loss limits
            if (position.UnrealizedPnL < -totalExposure * 0.02m) // 2% loss of total exposure
            {
                await _messageBus.PublishAsync("risk.unrealized.loss", new RiskEvent
                {
                    Source = "PositionMonitor",
                    RiskType = "Position",
                    Symbol = position.Symbol,
                    CurrentExposure = Math.Abs(position.UnrealizedPnL),
                    RiskLimit = totalExposure * 0.02m,
                    UtilizationPercent = Math.Abs(position.UnrealizedPnL / (totalExposure * 0.02m)) * 100,
                    LimitBreached = true,
                    Action = "Warn",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var baseMetrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            baseMetrics["TotalPositions"] = _positions.Count;
            baseMetrics["TotalExposure"] = _totalExposure;
            baseMetrics["TotalUnrealizedPnL"] = _totalUnrealizedPnL;
            baseMetrics["PositionUpdates"] = _positionUpdates;
            baseMetrics["PriceUpdates"] = _priceUpdates;
            baseMetrics["LargestPosition"] = !_positions.IsEmpty 
                ? _positions.Values.Max(p => Math.Abs(p.Quantity * p.CurrentPrice)) 
                : 0m;
            baseMetrics["AverageUnrealizedPnL"] = !_positions.IsEmpty 
                ? _positions.Values.Average(p => p.UnrealizedPnL) 
                : 0m;

            return baseMetrics;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Position monitor started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Position monitor stopped. Total positions tracked: {_positions.Count}, Total updates: {_positionUpdates}");
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }

        // Custom event type for position updates
        private sealed record PositionUpdatedEvent : TradingEvent
        {
            public override string EventType => "PositionUpdated";
            public string Symbol { get; init; } = string.Empty;
            public decimal Quantity { get; init; }
            public decimal CurrentPrice { get; init; }
            public decimal UnrealizedPnL { get; init; }
        }
    }
}