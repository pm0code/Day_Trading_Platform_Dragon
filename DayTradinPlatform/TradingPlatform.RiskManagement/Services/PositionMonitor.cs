using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
namespace TradingPlatform.RiskManagement.Services;

public class PositionMonitor : IPositionMonitor
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Position> _positions = new();

    public PositionMonitor(IMessageBus messageBus, ILogger logger)
    {
        _messageBus = messageBus;
        _logger = logger;
        
        // TODO: Subscribe to market data updates when Redis messaging is fixed
        // Subscribe to market data updates
        //_ = Task.Run(async () =>
        //{
        //    await foreach (var message in _messageBus.SubscribeAsync("marketdata.price.updated"))
        //    {
        //        await HandlePriceUpdateAsync(message);
        //    }
        //});
        //
        //// Subscribe to order execution events
        //_ = Task.Run(async () =>
        //{
        //    await foreach (var message in _messageBus.SubscribeAsync("orders.executed"))
        //    {
        //        await HandleOrderExecutionAsync(message);
        //    }
        //});
    }

    public async Task<IEnumerable<Position>> GetAllPositionsAsync()
    {
        return await Task.FromResult(_positions.Values.ToList());
    }

    public async Task<Position?> GetPositionAsync(string symbol)
    {
        _positions.TryGetValue(symbol, out var position);
        return await Task.FromResult(position);
    }

    public async Task UpdatePositionAsync(Position position)
    {
        _positions.AddOrUpdate(position.Symbol, position, (key, oldPosition) =>
        {
            // Calculate updated P&L
            var newPosition = position with
            {
                UnrealizedPnL = (position.CurrentPrice - position.AveragePrice) * position.Quantity,
                MarketValue = position.Quantity * position.CurrentPrice,
                RiskExposure = Math.Abs(position.Quantity * position.CurrentPrice),
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogDebug("Position updated for {Symbol}: Qty={Quantity}, Price={Price}, UnrealizedPnL={PnL}", 
                position.Symbol, position.Quantity, position.CurrentPrice, newPosition.UnrealizedPnL);

            return newPosition;
        });

        await _messageBus.PublishAsync("risk.position.updated", new RiskEvent
        {
            RiskType = "Position",
            Symbol = position.Symbol,
            CurrentExposure = position.RiskExposure,
            RiskLimit = 100000m, // TODO: Get from risk limits
            UtilizationPercent = position.RiskExposure / 100000m * 100m,
            LimitBreached = position.RiskExposure > 100000m,
            Action = "Monitor"
        });
    }

    public async Task<decimal> GetTotalExposureAsync()
    {
        var positions = await GetAllPositionsAsync();
        var totalExposure = positions.Sum(p => p.RiskExposure);
        
        _logger.LogDebug("Total portfolio exposure calculated: {TotalExposure:C}", totalExposure);
        return totalExposure;
    }

    public async Task<decimal> GetSymbolExposureAsync(string symbol)
    {
        var position = await GetPositionAsync(symbol);
        var exposure = position?.RiskExposure ?? 0m;
        
        _logger.LogDebug("Symbol exposure for {Symbol}: {Exposure:C}", symbol, exposure);
        return exposure;
    }

    public async Task<IEnumerable<Position>> GetPositionsExceedingLimitsAsync()
    {
        var positions = await GetAllPositionsAsync();
        var maxPositionSize = 100000m; // TODO: Get from risk limits
        
        var exceedingPositions = positions.Where(p => p.RiskExposure > maxPositionSize).ToList();
        
        if (exceedingPositions.Any())
        {
            _logger.LogWarning("Found {Count} positions exceeding limits", exceedingPositions.Count);
        }
        
        return exceedingPositions;
    }

    private async Task HandlePriceUpdateAsync(object message)
    {
        try
        {
            if (message is MarketDataEvent marketDataEvent)
            {
                var position = await GetPositionAsync(marketDataEvent.Symbol);
                if (position != null)
                {
                    var updatedPosition = position with 
                    { 
                        CurrentPrice = marketDataEvent.Price,
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    await UpdatePositionAsync(updatedPosition);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling price update");
        }
    }

    private async Task HandleOrderExecutionAsync(object message)
    {
        try
        {
            if (message is OrderEvent orderEvent)
            {
                // Parse order execution and update positions
                var symbol = orderEvent.Symbol;
                var quantity = orderEvent.Quantity;
                var price = orderEvent.Price ?? 0m;
                
                var existingPosition = await GetPositionAsync(symbol);
                
                if (existingPosition == null)
                {
                    // New position
                    var newPosition = new Position(
                        Symbol: symbol,
                        Quantity: quantity,
                        AveragePrice: price,
                        CurrentPrice: price,
                        UnrealizedPnL: 0m,
                        RealizedPnL: 0m,
                        MarketValue: quantity * price,
                        RiskExposure: Math.Abs(quantity * price),
                        OpenTime: DateTime.UtcNow,
                        LastUpdated: DateTime.UtcNow
                    );
                    
                    await UpdatePositionAsync(newPosition);
                }
                else
                {
                    // Update existing position
                    var newQuantity = existingPosition.Quantity + quantity;
                    var newAveragePrice = newQuantity != 0 
                        ? (existingPosition.AveragePrice * existingPosition.Quantity + price * quantity) / newQuantity
                        : 0m;
                    
                    // Calculate realized P&L if closing position
                    var realizedPnL = existingPosition.RealizedPnL;
                    if ((existingPosition.Quantity > 0 && quantity < 0) || 
                        (existingPosition.Quantity < 0 && quantity > 0))
                    {
                        var closingQuantity = Math.Min(Math.Abs(existingPosition.Quantity), Math.Abs(quantity));
                        realizedPnL += closingQuantity * (price - existingPosition.AveragePrice) * 
                                      Math.Sign(existingPosition.Quantity);
                    }
                    
                    var updatedPosition = existingPosition with
                    {
                        Quantity = newQuantity,
                        AveragePrice = newAveragePrice,
                        CurrentPrice = price,
                        RealizedPnL = realizedPnL,
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    await UpdatePositionAsync(updatedPosition);
                }

                _logger.LogInformation("Position updated from order execution: {Symbol} {Quantity}@{Price}", 
                    symbol, quantity, price);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling order execution");
        }
    }
}