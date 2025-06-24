// File: TradingPlatform.RiskManagement.Services\PositionMonitorCanonical.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
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
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "PositionMonitor")
        {
            _messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        }

        protected override async Task InitializeServiceAsync()
        {
            await base.InitializeServiceAsync();

            // Subscribe to market data updates
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in _messageBus.SubscribeAsync("marketdata.price.updated"))
                    {
                        await HandlePriceUpdateAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in price update subscription", ex);
                }
            });

            // Subscribe to order execution events
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in _messageBus.SubscribeAsync("orders.executed"))
                    {
                        await HandleOrderExecutionAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in order execution subscription", ex);
                }
            });

            _logger.LogInformation("Position monitor initialized and subscribed to market events");
        }

        public async Task<IEnumerable<Position>> GetAllPositionsAsync()
        {
            return await ExecuteOperationAsync(
                nameof(GetAllPositionsAsync),
                async () =>
                {
                    var positions = _positions.Values.ToList();
                    
                    _logger.LogDebug($"Retrieved {positions.Count} positions",
                        new { PositionCount = positions.Count, TotalExposure = _totalExposure });

                    return await Task.FromResult(TradingResult<IEnumerable<Position>>.Success(positions));
                },
                createDefaultResult: () => Enumerable.Empty<Position>());
        }

        public async Task<Position?> GetPositionAsync(string symbol)
        {
            return await ExecuteOperationAsync(
                nameof(GetPositionAsync),
                async () =>
                {
                    _positions.TryGetValue(symbol, out var position);
                    
                    if (position != null)
                    {
                        _logger.LogDebug($"Retrieved position for {symbol}",
                            new 
                            { 
                                Symbol = symbol, 
                                Quantity = position.Quantity,
                                UnrealizedPnL = position.UnrealizedPnL
                            });
                    }

                    return await Task.FromResult(TradingResult<Position?>.Success(position));
                },
                createDefaultResult: () => (Position?)null);
        }

        public async Task UpdatePositionAsync(Position position)
        {
            await ExecuteOperationAsync(
                nameof(UpdatePositionAsync),
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
                        await _messageBus.PublishAsync(new PositionUpdated
                        {
                            Symbol = position.Symbol,
                            Quantity = position.Quantity,
                            CurrentPrice = position.CurrentPrice,
                            UnrealizedPnL = position.UnrealizedPnL,
                            Timestamp = DateTime.UtcNow
                        });

                        Interlocked.Increment(ref _positionUpdates);

                        _logger.LogInformation($"Position updated for {position.Symbol}",
                            new
                            {
                                Symbol = position.Symbol,
                                Quantity = position.Quantity,
                                EntryPrice = position.EntryPrice,
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
                createDefaultResult: () => new object());
        }

        public async Task<decimal> GetTotalExposureAsync()
        {
            return await ExecuteOperationAsync(
                nameof(GetTotalExposureAsync),
                async () =>
                {
                    await _updateSemaphore.WaitAsync();
                    try
                    {
                        _totalExposure = _positions.Values.Sum(p => Math.Abs(p.Quantity * p.CurrentPrice));
                        
                        _logger.LogDebug($"Total exposure calculated: ${_totalExposure:N2}",
                            new { TotalExposure = _totalExposure });

                        return TradingResult<decimal>.Success(_totalExposure);
                    }
                    finally
                    {
                        _updateSemaphore.Release();
                    }
                },
                createDefaultResult: () => 0m);
        }

        public async Task<decimal> GetSymbolExposureAsync(string symbol)
        {
            return await ExecuteOperationAsync(
                nameof(GetSymbolExposureAsync),
                async () =>
                {
                    if (_positions.TryGetValue(symbol, out var position))
                    {
                        var exposure = Math.Abs(position.Quantity * position.CurrentPrice);
                        
                        _logger.LogDebug($"Symbol exposure for {symbol}: ${exposure:N2}",
                            new { Symbol = symbol, Exposure = exposure });

                        return TradingResult<decimal>.Success(exposure);
                    }

                    return await Task.FromResult(TradingResult<decimal>.Success(0m));
                },
                createDefaultResult: () => 0m);
        }

        public async Task<IEnumerable<Position>> GetPositionsExceedingLimitsAsync()
        {
            return await ExecuteOperationAsync(
                nameof(GetPositionsExceedingLimitsAsync),
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
                            
                            _logger.LogWarning($"Position exceeds exposure limit: {position.Symbol}",
                                new
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
                createDefaultResult: () => Enumerable.Empty<Position>());
        }

        private async Task HandlePriceUpdateAsync(object message)
        {
            if (message is PriceUpdated priceUpdate)
            {
                if (_positions.TryGetValue(priceUpdate.Symbol, out var position))
                {
                    var previousPrice = position.CurrentPrice;
                    position.CurrentPrice = priceUpdate.Price;
                    position.LastUpdated = priceUpdate.Timestamp;

                    // Update unrealized P&L
                    position.UnrealizedPnL = position.Quantity * (position.CurrentPrice - position.EntryPrice);

                    // Update position
                    await UpdatePositionAsync(position);

                    Interlocked.Increment(ref _priceUpdates);

                    // Check for significant price movements
                    if (previousPrice > 0)
                    {
                        var priceChange = Math.Abs((priceUpdate.Price - previousPrice) / previousPrice);
                        if (priceChange > 0.05m) // 5% price movement
                        {
                            _logger.LogWarning($"Significant price movement detected for {priceUpdate.Symbol}",
                                new
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
            if (message is OrderExecuted orderExecuted)
            {
                var position = await GetPositionAsync(orderExecuted.Symbol) ?? new Position
                {
                    Symbol = orderExecuted.Symbol,
                    Quantity = 0,
                    EntryPrice = 0,
                    CurrentPrice = orderExecuted.Price,
                    EntryTime = orderExecuted.ExecutionTime
                };

                // Update position based on order
                if (orderExecuted.Side == OrderSide.Buy)
                {
                    // Calculate weighted average entry price
                    var totalCost = (position.Quantity * position.EntryPrice) + (orderExecuted.Quantity * orderExecuted.Price);
                    var newQuantity = position.Quantity + orderExecuted.Quantity;
                    
                    position.EntryPrice = newQuantity > 0 ? totalCost / newQuantity : orderExecuted.Price;
                    position.Quantity = newQuantity;
                }
                else // Sell
                {
                    position.Quantity -= orderExecuted.Quantity;
                    
                    // If position is closed
                    if (Math.Abs(position.Quantity) < 0.0001m)
                    {
                        _positions.TryRemove(position.Symbol, out _);
                        
                        _logger.LogInformation($"Position closed for {position.Symbol}",
                            new { Symbol = position.Symbol, RealizedPnL = orderExecuted.RealizedPnL });
                        
                        return;
                    }
                }

                position.CurrentPrice = orderExecuted.Price;
                position.LastUpdated = orderExecuted.ExecutionTime;

                await UpdatePositionAsync(position);
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
                RecordMetric("TotalExposure", _totalExposure);
                RecordMetric("TotalUnrealizedPnL", _totalUnrealizedPnL);
                RecordMetric("PositionCount", _positions.Count);
                RecordMetric($"Exposure.{newPosition.Symbol}", newExposure);
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
                await _messageBus.PublishAsync(new RiskLimitBreached
                {
                    LimitType = "PositionExposure",
                    Symbol = position.Symbol,
                    CurrentValue = exposureRatio,
                    LimitValue = MAX_SINGLE_POSITION_EXPOSURE,
                    Message = $"Position {position.Symbol} exceeds maximum exposure limit",
                    Timestamp = DateTime.UtcNow
                });
            }
            else if (exposureRatio > POSITION_WARNING_THRESHOLD)
            {
                _logger.LogWarning($"Position approaching exposure limit: {position.Symbol}",
                    new
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
                await _messageBus.PublishAsync(new RiskLimitBreached
                {
                    LimitType = "UnrealizedLoss",
                    Symbol = position.Symbol,
                    CurrentValue = position.UnrealizedPnL,
                    LimitValue = -totalExposure * 0.02m,
                    Message = $"Position {position.Symbol} has significant unrealized loss",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        protected override Dictionary<string, object> GetServiceMetrics()
        {
            var baseMetrics = base.GetServiceMetrics();
            
            baseMetrics["TotalPositions"] = _positions.Count;
            baseMetrics["TotalExposure"] = _totalExposure;
            baseMetrics["TotalUnrealizedPnL"] = _totalUnrealizedPnL;
            baseMetrics["PositionUpdates"] = _positionUpdates;
            baseMetrics["PriceUpdates"] = _priceUpdates;
            baseMetrics["LargestPosition"] = _positions.Values.Any() 
                ? _positions.Values.Max(p => Math.Abs(p.Quantity * p.CurrentPrice)) 
                : 0m;
            baseMetrics["AverageUnrealizedPnL"] = _positions.Values.Any() 
                ? _positions.Values.Average(p => p.UnrealizedPnL) 
                : 0m;

            return baseMetrics;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}