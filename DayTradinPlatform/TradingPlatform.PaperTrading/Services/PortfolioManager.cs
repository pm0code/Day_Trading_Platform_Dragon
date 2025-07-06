using TradingPlatform.PaperTrading.Models;
using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.PaperTrading.Services;

/// <summary>
/// Comprehensive portfolio management service for paper trading operations
/// Provides real-time position tracking, P&L calculation, and buying power management
/// </summary>
public class PortfolioManager : CanonicalServiceBase, IPortfolioManager
{
    private readonly IOrderBookSimulator _orderBookSimulator;
    private readonly ConcurrentDictionary<string, Position> _positions = new();
    private decimal _cashBalance = 100000m; // Starting with $100k for paper trading
    private decimal _totalRealizedPnL = 0m;

    /// <summary>
    /// Initializes a new instance of the PortfolioManager with required dependencies
    /// </summary>
    /// <param name="orderBookSimulator">Service for obtaining current market prices</param>
    /// <param name="logger">Trading logger for comprehensive portfolio tracking</param>
    public PortfolioManager(
        IOrderBookSimulator orderBookSimulator,
        ITradingLogger logger) : base(logger, "PortfolioManager")
    {
        _orderBookSimulator = orderBookSimulator ?? throw new ArgumentNullException(nameof(orderBookSimulator));
    }

    /// <summary>
    /// Retrieves the complete portfolio with current market valuations and P&L calculations
    /// </summary>
    /// <returns>A TradingResult containing comprehensive portfolio information</returns>
    public async Task<TradingResult<Portfolio>> GetPortfolioAsync()
    {
        LogMethodEntry();
        try
        {
            var positionsResult = await GetPositionsAsync();
            if (!positionsResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<Portfolio>.Failure($"Failed to get positions: {positionsResult.ErrorMessage}", positionsResult.ErrorCode);
            }

            var positions = positionsResult.Value;
            var totalMarketValue = positions.Sum(p => p.MarketValue);
            var totalUnrealizedPnL = positions.Sum(p => p.UnrealizedPnL);
            var totalEquity = _cashBalance + totalMarketValue;
            var dayPnL = CalculateDayPnL(positions);

            var buyingPowerResult = await GetBuyingPowerAsync();
            if (!buyingPowerResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<Portfolio>.Failure($"Failed to calculate buying power: {buyingPowerResult.ErrorMessage}", buyingPowerResult.ErrorCode);
            }

            var portfolio = new Portfolio(
                TotalValue: totalEquity,
                CashBalance: _cashBalance,
                TotalEquity: totalEquity,
                DayPnL: dayPnL,
                TotalPnL: _totalRealizedPnL + totalUnrealizedPnL,
                BuyingPower: buyingPowerResult.Value,
                Positions: positions,
                LastUpdated: DateTime.UtcNow
            );

            LogMethodExit();
            return TradingResult<Portfolio>.Success(portfolio);
        }
        catch (Exception ex)
        {
            LogError("Error getting portfolio", ex);
            LogMethodExit();
            return TradingResult<Portfolio>.Failure($"Portfolio retrieval failed: {ex.Message}", "PORTFOLIO_ERROR");
        }
    }

    /// <summary>
    /// Retrieves all current positions with updated market valuations
    /// </summary>
    /// <returns>A TradingResult containing the collection of current positions</returns>
    public async Task<TradingResult<IEnumerable<Position>>> GetPositionsAsync()
    {
        LogMethodEntry();
        try
        {
            var positionsList = new List<Position>();

            foreach (var kvp in _positions)
            {
                try
                {
                    var position = kvp.Value;
                    var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(position.Symbol);

                    var updatedPosition = position with
                    {
                        CurrentPrice = currentPrice,
                        MarketValue = position.Quantity * currentPrice,
                        UnrealizedPnL = (currentPrice - position.AveragePrice) * position.Quantity,
                        LastUpdated = DateTime.UtcNow
                    };

                    positionsList.Add(updatedPosition);

                    // Update the stored position with current market data
                    _positions.TryUpdate(kvp.Key, updatedPosition, position);
                }
                catch (Exception ex)
                {
                    LogError($"Error updating position for {kvp.Key}", ex);
                    positionsList.Add(kvp.Value); // Add stale position data
                }
            }

            var result = positionsList.OrderByDescending(p => Math.Abs(p.MarketValue));
            LogMethodExit();
            return TradingResult<IEnumerable<Position>>.Success(result);
        }
        catch (Exception ex)
        {
            LogError("Error getting positions", ex);
            LogMethodExit();
            return TradingResult<IEnumerable<Position>>.Failure($"Position retrieval failed: {ex.Message}", "POSITIONS_ERROR");
        }
    }

    /// <summary>
    /// Retrieves a specific position by symbol with current market valuation
    /// </summary>
    /// <param name="symbol">The symbol to retrieve position for</param>
    /// <returns>A TradingResult containing the position or null if not found</returns>
    public async Task<TradingResult<Position?>> GetPositionAsync(string symbol)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<Position?>.Failure("Symbol cannot be null or empty", "INVALID_SYMBOL");
            }

            if (!_positions.TryGetValue(symbol, out var position))
            {
                LogMethodExit();
                return TradingResult<Position?>.Success(null);
            }

            var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(symbol);

            var updatedPosition = position with
            {
                CurrentPrice = currentPrice,
                MarketValue = position.Quantity * currentPrice,
                UnrealizedPnL = (currentPrice - position.AveragePrice) * position.Quantity,
                LastUpdated = DateTime.UtcNow
            };

            LogMethodExit();
            return TradingResult<Position?>.Success(updatedPosition);
        }
        catch (Exception ex)
        {
            LogError($"Error getting current position for {symbol}", ex);
            LogMethodExit();
            return TradingResult<Position?>.Failure($"Position retrieval failed: {ex.Message}", "POSITION_ERROR");
        }
    }

    /// <summary>
    /// Updates a position based on an execution with accurate P&L calculation
    /// </summary>
    /// <param name="symbol">The symbol of the position to update</param>
    /// <param name="execution">The execution details to apply to the position</param>
    /// <returns>A TradingResult indicating success or failure of the update</returns>
    public async Task<TradingResult> UpdatePositionAsync(string symbol, Execution execution)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult.Failure("Symbol cannot be null or empty", "INVALID_SYMBOL");
            }

            if (execution == null)
            {
                LogMethodExit();
                return TradingResult.Failure("Execution cannot be null", "INVALID_EXECUTION");
            }

            var existingPosition = _positions.GetValueOrDefault(symbol);

            if (existingPosition == null)
            {
                // New position
                var newPosition = new Position(
                    Symbol: symbol,
                    Quantity: execution.Side == OrderSide.Buy ? execution.Quantity : -execution.Quantity,
                    AveragePrice: execution.Price,
                    CurrentPrice: execution.Price,
                    MarketValue: execution.Quantity * execution.Price,
                    UnrealizedPnL: 0m,
                    RealizedPnL: -execution.Commission,
                    FirstTradeDate: execution.ExecutionTime,
                    LastUpdated: execution.ExecutionTime
                );

                _positions.TryAdd(symbol, newPosition);

                // Update cash balance
                var cashImpact = execution.Side == OrderSide.Buy
                    ? -(execution.Quantity * execution.Price + execution.Commission)
                    : (execution.Quantity * execution.Price - execution.Commission);

                _cashBalance += cashImpact;

                LogInfo($"Created new position for {symbol}: {newPosition.Quantity}@{execution.Price}");
            }
            else
            {
                // Update existing position
                var executionQuantity = execution.Side == OrderSide.Buy ? execution.Quantity : -execution.Quantity;
                var newTotalQuantity = existingPosition.Quantity + executionQuantity;

                decimal newAveragePrice;
                decimal realizedPnL = existingPosition.RealizedPnL - execution.Commission;

                if (Math.Sign(existingPosition.Quantity) != Math.Sign(newTotalQuantity) && existingPosition.Quantity != 0)
                {
                    // Position is being reduced or reversed - calculate realized P&L
                    var closingQuantity = Math.Min(Math.Abs(existingPosition.Quantity), Math.Abs(executionQuantity));
                    var pnlPerShare = execution.Side == OrderSide.Sell
                        ? execution.Price - existingPosition.AveragePrice
                        : existingPosition.AveragePrice - execution.Price;

                    realizedPnL += closingQuantity * pnlPerShare;
                    _totalRealizedPnL += closingQuantity * pnlPerShare;
                }

                if (newTotalQuantity == 0)
                {
                    // Position closed
                    _positions.TryRemove(symbol, out _);
                    newAveragePrice = 0m;
                }
                else
                {
                    // Calculate new average price
                    if (Math.Sign(existingPosition.Quantity) == Math.Sign(executionQuantity))
                    {
                        // Adding to position
                        var totalCost = (existingPosition.Quantity * existingPosition.AveragePrice) +
                                       (executionQuantity * execution.Price);
                        newAveragePrice = totalCost / newTotalQuantity;
                    }
                    else
                    {
                        // Reducing position - keep existing average price
                        newAveragePrice = existingPosition.AveragePrice;
                    }
                }

                if (newTotalQuantity != 0)
                {
                    var updatedPosition = existingPosition with
                    {
                        Quantity = newTotalQuantity,
                        AveragePrice = newAveragePrice,
                        CurrentPrice = execution.Price,
                        MarketValue = newTotalQuantity * execution.Price,
                        UnrealizedPnL = (execution.Price - newAveragePrice) * newTotalQuantity,
                        RealizedPnL = realizedPnL,
                        LastUpdated = execution.ExecutionTime
                    };

                    _positions.TryUpdate(symbol, updatedPosition, existingPosition);
                }

                // Update cash balance
                var cashImpact = execution.Side == OrderSide.Buy
                    ? -(execution.Quantity * execution.Price + execution.Commission)
                    : (execution.Quantity * execution.Price - execution.Commission);

                _cashBalance += cashImpact;

                LogInfo($"Updated position for {symbol}: {existingPosition.Quantity} -> {newTotalQuantity}@{newAveragePrice}");
            }

            LogMethodExit();
            return TradingResult.Success();
        }
        catch (Exception ex)
        {
            LogError($"Error updating position for {symbol}", ex);
            LogMethodExit();
            return TradingResult.Failure($"Position update failed: {ex.Message}", "UPDATE_ERROR");
        }
    }

    /// <summary>
    /// Calculates current buying power based on cash balance and margin requirements
    /// </summary>
    /// <returns>A TradingResult containing the calculated buying power</returns>
    public async Task<TradingResult<decimal>> GetBuyingPowerAsync()
    {
        LogMethodEntry();
        try
        {
            var positionsResult = await GetPositionsAsync();
            if (!positionsResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure($"Failed to get positions for buying power calculation: {positionsResult.ErrorMessage}", positionsResult.ErrorCode);
            }

            var positions = positionsResult.Value;
            var longMarketValue = positions.Where(p => p.Quantity > 0).Sum(p => p.MarketValue);
            var shortMarketValue = Math.Abs(positions.Where(p => p.Quantity < 0).Sum(p => p.MarketValue));

            // Simplified buying power calculation (for paper trading)
            // Cash + (Long positions * 0.5) - (Short positions * 1.5) for margin
            var buyingPower = _cashBalance + (longMarketValue * 0.5m) - (shortMarketValue * 1.5m);

            LogMethodExit();
            return TradingResult<decimal>.Success(buyingPower);
        }
        catch (Exception ex)
        {
            LogError("Error calculating buying power", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure($"Buying power calculation failed: {ex.Message}", "BUYING_POWER_ERROR");
        }
    }

    /// <summary>
    /// Checks if there is sufficient buying power for a proposed order
    /// </summary>
    /// <param name="orderRequest">The order request to validate</param>
    /// <param name="estimatedPrice">Estimated execution price for the order</param>
    /// <returns>A TradingResult indicating whether sufficient buying power exists</returns>
    public async Task<TradingResult<bool>> HasSufficientBuyingPowerAsync(OrderRequest orderRequest, decimal estimatedPrice)
    {
        LogMethodEntry();
        try
        {
            if (orderRequest == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("Order request cannot be null", "INVALID_ORDER_REQUEST");
            }

            if (estimatedPrice <= 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("Estimated price must be positive", "INVALID_PRICE");
            }

            var buyingPowerResult = await GetBuyingPowerAsync();
            if (!buyingPowerResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure($"Failed to get buying power: {buyingPowerResult.ErrorMessage}", buyingPowerResult.ErrorCode);
            }

            var buyingPower = buyingPowerResult.Value;
            var requiredCapital = orderRequest.Quantity * estimatedPrice;

            // Add buffer for commissions and slippage
            var requiredCapitalWithBuffer = requiredCapital * 1.01m; // 1% buffer

            var hasSufficient = orderRequest.Side == OrderSide.Buy
                ? buyingPower >= requiredCapitalWithBuffer
                : true; // Selling doesn't require additional buying power in paper trading

            LogMethodExit();
            return TradingResult<bool>.Success(hasSufficient);
        }
        catch (Exception ex)
        {
            LogError($"Error checking buying power for {orderRequest?.Symbol}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure($"Buying power check failed: {ex.Message}", "BUYING_POWER_CHECK_ERROR");
        }
    }

    /// <summary>
    /// Calculates the day's profit and loss from current positions
    /// </summary>
    /// <param name="positions">The positions to calculate day P&L for</param>
    /// <returns>Day P&L amount in decimal precision</returns>
    private decimal CalculateDayPnL(IEnumerable<Position> positions)
    {
        LogMethodEntry();
        try
        {
            // Simplified day P&L calculation
            // In a real system, this would track positions from market open
            var dayPnL = positions.Sum(p => p.UnrealizedPnL * 0.1m); // Assume 10% of unrealized P&L is from today
            LogMethodExit();
            return dayPnL;
        }
        catch (Exception ex)
        {
            LogError("Error calculating day P&L", ex);
            LogMethodExit();
            throw;
        }
    }
}