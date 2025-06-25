using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Canonical implementation of the portfolio manager for paper trading.
    /// Provides comprehensive position tracking, P&L calculation, and risk monitoring.
    /// </summary>
    public class PortfolioManagerCanonical : CanonicalServiceBase, IPortfolioManager
    {
        #region Configuration

        protected virtual decimal InitialCashBalance => 100_000m;
        protected virtual decimal MarginRequirementLong => 0.5m;  // 50% margin for longs
        protected virtual decimal MarginRequirementShort => 1.5m; // 150% margin for shorts
        protected virtual decimal CommissionBuffer => 0.01m;      // 1% buffer for commissions/slippage
        protected virtual int PositionUpdateIntervalMs => 1000;    // Update positions every second

        #endregion

        #region Infrastructure

        private readonly IOrderBookSimulator _orderBookSimulator;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, Position> _positions = new();
        private readonly ConcurrentDictionary<string, PositionHistory> _positionHistory = new();
        private readonly Timer _positionUpdateTimer;
        private decimal _cashBalance;
        private decimal _totalRealizedPnL = 0m;
        private decimal _dayStartEquity = 0m;
        private DateTime _dayStartTime;

        #endregion

        #region Constructor

        public PortfolioManagerCanonical(
            IServiceProvider serviceProvider,
            IOrderBookSimulator orderBookSimulator,
            ITradingLogger logger)
            : base(logger, "PortfolioManager")
        {
            _serviceProvider = serviceProvider;
            _orderBookSimulator = orderBookSimulator;
            _cashBalance = InitialCashBalance;
            _dayStartTime = DateTime.UtcNow.Date;
            _dayStartEquity = InitialCashBalance;
            
            _positionUpdateTimer = new Timer(
                UpdateAllPositions,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        #endregion

        #region IPortfolioManager Implementation

        public async Task<Portfolio> GetPortfolioAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var positions = await GetPositionsAsync();
                var totalMarketValue = positions.Sum(p => p.MarketValue);
                var totalUnrealizedPnL = positions.Sum(p => p.UnrealizedPnL);
                var totalEquity = _cashBalance + totalMarketValue;
                var dayPnL = await CalculateDayPnLAsync(positions);
                var buyingPower = await CalculateBuyingPowerAsync(positions);

                var portfolio = new Portfolio(
                    TotalValue: totalEquity,
                    CashBalance: _cashBalance,
                    TotalEquity: totalEquity,
                    DayPnL: dayPnL,
                    TotalPnL: _totalRealizedPnL + totalUnrealizedPnL,
                    BuyingPower: buyingPower,
                    Positions: positions,
                    LastUpdated: DateTime.UtcNow
                );

                // Update metrics
                UpdateMetric("Portfolio.TotalValue", totalEquity);
                UpdateMetric("Portfolio.CashBalance", _cashBalance);
                UpdateMetric("Portfolio.PositionCount", positions.Count());
                UpdateMetric("Portfolio.UnrealizedPnL", totalUnrealizedPnL);
                UpdateMetric("Portfolio.RealizedPnL", _totalRealizedPnL);
                UpdateMetric("Portfolio.DayPnL", dayPnL);
                
                LogDebug($"Portfolio snapshot: Equity=${totalEquity:N2}, " +
                        $"Positions={positions.Count()}, DayPnL=${dayPnL:N2}");

                return await Task.FromResult(portfolio);

            }, "Get portfolio snapshot",
               "Failed to retrieve portfolio",
               "Check position data and market connectivity");
        }

        public async Task<IEnumerable<Position>> GetPositionsAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var positionsList = new List<Position>();

                foreach (var kvp in _positions.ToList())
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
                        
                        // Track position metrics
                        UpdatePositionMetrics(updatedPosition);
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Error updating position for {kvp.Key}",
                                 impact: "Using stale position data",
                                 troubleshooting: "Check market data availability");
                        positionsList.Add(kvp.Value); // Add stale position data
                    }
                }

                return positionsList.OrderByDescending(p => Math.Abs(p.MarketValue)).AsEnumerable();

            }, "Get all positions",
               "Failed to retrieve positions",
               "Check position storage and market data");
        }

        public async Task<Position?> GetPositionAsync(string symbol)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                if (!_positions.TryGetValue(symbol, out var position))
                    return null;

                var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(symbol);

                var updatedPosition = position with
                {
                    CurrentPrice = currentPrice,
                    MarketValue = position.Quantity * currentPrice,
                    UnrealizedPnL = (currentPrice - position.AveragePrice) * position.Quantity,
                    LastUpdated = DateTime.UtcNow
                };

                UpdatePositionMetrics(updatedPosition);
                return updatedPosition;

            }, $"Get position for {symbol}",
               "Failed to retrieve position",
               "Verify symbol and position existence");
        }

        public async Task UpdatePositionAsync(string symbol, Execution execution)
        {
            await ExecuteWithLoggingAsync(async () =>
            {
                ValidateNotNull(execution, nameof(execution));
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                var existingPosition = _positions.GetValueOrDefault(symbol);

                if (existingPosition == null)
                {
                    // Create new position
                    await CreateNewPositionAsync(symbol, execution);
                }
                else
                {
                    // Update existing position
                    await UpdateExistingPositionAsync(symbol, existingPosition, execution);
                }

                // Record position history
                RecordPositionHistory(symbol, execution);

                return TradingResult.Success();

            }, $"Update position for {symbol}",
               "Failed to update position",
               "Check execution data and position state");
        }

        public async Task<decimal> GetBuyingPowerAsync()
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var positions = await GetPositionsAsync();
                return await CalculateBuyingPowerAsync(positions);

            }, "Calculate buying power",
               "Failed to calculate buying power",
               "Check portfolio data and margin requirements");
        }

        public async Task<bool> HasSufficientBuyingPowerAsync(OrderRequest orderRequest, decimal estimatedPrice)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateNotNull(orderRequest, nameof(orderRequest));
                ValidateParameter(estimatedPrice, nameof(estimatedPrice), p => p > 0, "Estimated price must be positive");

                var buyingPower = await GetBuyingPowerAsync();
                var requiredCapital = orderRequest.Quantity * estimatedPrice;

                // Add buffer for commissions and slippage
                var requiredCapitalWithBuffer = requiredCapital * (1 + CommissionBuffer);

                var hasSufficientPower = orderRequest.Side == OrderSide.Buy
                    ? buyingPower >= requiredCapitalWithBuffer
                    : true; // Selling doesn't require additional buying power in paper trading

                if (!hasSufficientPower)
                {
                    LogWarning($"Insufficient buying power for {orderRequest.Symbol}",
                             $"Required: ${requiredCapitalWithBuffer:N2}, Available: ${buyingPower:N2}",
                             impact: "Order will be rejected",
                             troubleshooting: "Reduce order size or close existing positions");
                }

                UpdateMetric("BuyingPower.Checks", 1);
                UpdateMetric("BuyingPower.Rejections", hasSufficientPower ? 0 : 1);

                return hasSufficientPower;

            }, $"Check buying power for {orderRequest.Symbol}",
               "Failed to verify buying power",
               "Verify order parameters and portfolio state");
        }

        #endregion

        #region Position Management

        private async Task CreateNewPositionAsync(string symbol, Execution execution)
        {
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
            var cashImpact = CalculateCashImpact(execution);
            UpdateCashBalance(cashImpact);

            LogInfo($"Created new position for {symbol}: {newPosition.Quantity}@{execution.Price:C}");
            UpdateMetric("Positions.Created", 1);
            UpdateMetric($"Positions.{symbol}.Quantity", newPosition.Quantity);
        }

        private async Task UpdateExistingPositionAsync(string symbol, Position existingPosition, Execution execution)
        {
            var executionQuantity = execution.Side == OrderSide.Buy ? execution.Quantity : -execution.Quantity;
            var newTotalQuantity = existingPosition.Quantity + executionQuantity;

            decimal newAveragePrice;
            decimal realizedPnL = existingPosition.RealizedPnL - execution.Commission;

            // Check if position is being reduced or reversed
            if (Math.Sign(existingPosition.Quantity) != Math.Sign(newTotalQuantity) && existingPosition.Quantity != 0)
            {
                // Calculate realized P&L for closing portion
                var closingQuantity = Math.Min(Math.Abs(existingPosition.Quantity), Math.Abs(executionQuantity));
                var pnlPerShare = execution.Side == OrderSide.Sell
                    ? execution.Price - existingPosition.AveragePrice
                    : existingPosition.AveragePrice - execution.Price;

                var closingPnL = closingQuantity * pnlPerShare;
                realizedPnL += closingPnL;
                _totalRealizedPnL += closingPnL;

                LogInfo($"Realized P&L for {symbol}: ${closingPnL:N2} on {closingQuantity} shares");
                UpdateMetric($"Positions.{symbol}.RealizedPnL", closingPnL);
            }

            if (Math.Abs(newTotalQuantity) < 0.001m) // Position closed (using small epsilon for decimal comparison)
            {
                // Position closed completely
                _positions.TryRemove(symbol, out _);
                newAveragePrice = 0m;
                
                LogInfo($"Closed position for {symbol}, total realized P&L: ${realizedPnL:N2}");
                UpdateMetric("Positions.Closed", 1);
                UpdateMetric($"Positions.{symbol}.Quantity", 0);
            }
            else
            {
                // Calculate new average price
                if (Math.Sign(existingPosition.Quantity) == Math.Sign(executionQuantity))
                {
                    // Adding to position
                    var totalCost = (existingPosition.Quantity * existingPosition.AveragePrice) +
                                   (executionQuantity * execution.Price);
                    newAveragePrice = Math.Abs(totalCost / newTotalQuantity);
                }
                else
                {
                    // Reducing position - keep existing average price
                    newAveragePrice = existingPosition.AveragePrice;
                }

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
                UpdateMetric($"Positions.{symbol}.Quantity", newTotalQuantity);

                LogInfo($"Updated position for {symbol}: {existingPosition.Quantity} -> {newTotalQuantity}@{newAveragePrice:C}");
            }

            // Update cash balance
            var cashImpact = CalculateCashImpact(execution);
            UpdateCashBalance(cashImpact);

            await Task.CompletedTask;
        }

        private decimal CalculateCashImpact(Execution execution)
        {
            return execution.Side == OrderSide.Buy
                ? -(execution.Quantity * execution.Price + execution.Commission)
                : (execution.Quantity * execution.Price - execution.Commission);
        }

        private void UpdateCashBalance(decimal amount)
        {
            var oldBalance = _cashBalance;
            _cashBalance += amount;
            
            UpdateMetric("Portfolio.CashBalance", _cashBalance);
            LogDebug($"Cash balance updated: ${oldBalance:N2} -> ${_cashBalance:N2} (change: ${amount:N2})");
        }

        #endregion

        #region P&L and Risk Calculations

        private async Task<decimal> CalculateDayPnLAsync(IEnumerable<Position> positions)
        {
            // Calculate intraday P&L based on position changes and realized P&L
            var currentEquity = _cashBalance + positions.Sum(p => p.MarketValue);
            var dayPnL = currentEquity - _dayStartEquity;

            // If it's a new trading day, reset day start values
            var currentDate = DateTime.UtcNow.Date;
            if (currentDate > _dayStartTime.Date)
            {
                _dayStartTime = currentDate;
                _dayStartEquity = currentEquity;
                dayPnL = 0; // Reset for new day
                
                LogInfo($"New trading day started. Starting equity: ${_dayStartEquity:N2}");
            }

            return await Task.FromResult(dayPnL);
        }

        private async Task<decimal> CalculateBuyingPowerAsync(IEnumerable<Position> positions)
        {
            var longMarketValue = positions.Where(p => p.Quantity > 0).Sum(p => p.MarketValue);
            var shortMarketValue = Math.Abs(positions.Where(p => p.Quantity < 0).Sum(p => p.MarketValue));

            // Calculate margin requirements
            var longMarginRequired = longMarketValue * MarginRequirementLong;
            var shortMarginRequired = shortMarketValue * MarginRequirementShort;

            // Available buying power = Cash + (Long margin available) - (Short margin required)
            var buyingPower = _cashBalance + longMarginRequired - shortMarginRequired;

            UpdateMetric("BuyingPower.Current", buyingPower);
            UpdateMetric("BuyingPower.LongExposure", longMarketValue);
            UpdateMetric("BuyingPower.ShortExposure", shortMarketValue);

            return await Task.FromResult(Math.Max(0, buyingPower));
        }

        #endregion

        #region Position Tracking and History

        private void RecordPositionHistory(string symbol, Execution execution)
        {
            var history = _positionHistory.GetOrAdd(symbol, _ => new PositionHistory { Symbol = symbol });
            
            history.Executions.Add(execution);
            history.TotalVolume += execution.Quantity;
            history.TotalCommissions += execution.Commission;
            history.LastExecutionTime = execution.ExecutionTime;

            UpdateMetric($"History.{symbol}.TradeCount", history.Executions.Count);
            UpdateMetric($"History.{symbol}.Volume", history.TotalVolume);
        }

        private void UpdatePositionMetrics(Position position)
        {
            var prefix = $"Position.{position.Symbol}";
            
            UpdateMetric($"{prefix}.Quantity", position.Quantity);
            UpdateMetric($"{prefix}.MarketValue", position.MarketValue);
            UpdateMetric($"{prefix}.UnrealizedPnL", position.UnrealizedPnL);
            UpdateMetric($"{prefix}.CurrentPrice", position.CurrentPrice);
            UpdateMetric($"{prefix}.AveragePrice", position.AveragePrice);

            // Track position direction
            if (position.Quantity > 0)
            {
                UpdateMetric("Positions.Long.Count", 1);
                UpdateMetric("Positions.Long.Value", position.MarketValue);
            }
            else if (position.Quantity < 0)
            {
                UpdateMetric("Positions.Short.Count", 1);
                UpdateMetric("Positions.Short.Value", Math.Abs(position.MarketValue));
            }
        }

        private void UpdateAllPositions(object? state)
        {
            try
            {
                var updateTasks = _positions.Keys.Select(async symbol =>
                {
                    await GetPositionAsync(symbol);
                }).ToArray();

                Task.WaitAll(updateTasks, TimeSpan.FromSeconds(5));
                
                UpdateMetric("PositionUpdates.Completed", 1);
            }
            catch (Exception ex)
            {
                LogError("Error updating positions", ex);
                UpdateMetric("PositionUpdates.Failed", 1);
            }
        }

        #endregion

        #region Lifecycle Management

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing PortfolioManager with ${InitialCashBalance:N2} starting capital");
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting position update timer");
            _positionUpdateTimer.Change(
                TimeSpan.FromMilliseconds(PositionUpdateIntervalMs),
                TimeSpan.FromMilliseconds(PositionUpdateIntervalMs));
            await Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping position updates");
            _positionUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Log final portfolio state
            var portfolio = await GetPortfolioAsync();
            LogInfo($"Final portfolio value: ${portfolio.TotalValue:N2}, " +
                   $"Total P&L: ${portfolio.TotalPnL:N2}");
            
            await Task.CompletedTask;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _positionUpdateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Nested Types

        private class PositionHistory
        {
            public string Symbol { get; set; } = string.Empty;
            public List<Execution> Executions { get; set; } = new();
            public decimal TotalVolume { get; set; }
            public decimal TotalCommissions { get; set; }
            public DateTime LastExecutionTime { get; set; }
        }

        #endregion
    }
}