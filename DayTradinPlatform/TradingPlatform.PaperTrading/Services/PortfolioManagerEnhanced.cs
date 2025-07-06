using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Enhanced portfolio manager with MCP standards compliance.
    /// Provides comprehensive position tracking, P&L calculation, and risk monitoring with event logging.
    /// </summary>
    public class PortfolioManagerEnhanced : CanonicalServiceBaseEnhanced, IPortfolioManager
    {
        #region Configuration

        protected virtual decimal InitialCashBalance => 100_000m;
        protected virtual decimal MarginRequirementLong => 0.5m;  // 50% margin for longs
        protected virtual decimal MarginRequirementShort => 1.5m; // 150% margin for shorts
        protected virtual decimal CommissionBuffer => 0.01m;      // 1% buffer for commissions/slippage
        protected virtual int PositionUpdateIntervalMs => 1000;   // Update positions every second
        protected virtual decimal RiskAlertThreshold => 10_000m;  // Alert on positions > $10k risk
        protected virtual decimal DayPnLAlertThreshold => 1_000m; // Alert on day P&L > $1k

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
        private long _totalPositionsCreated = 0;
        private long _totalPositionsClosed = 0;

        #endregion

        #region Constructor

        public PortfolioManagerEnhanced(
            IServiceProvider serviceProvider,
            IOrderBookSimulator orderBookSimulator)
            : base("PortfolioManager", createChildLogger: true)
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
            
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                "Portfolio manager initialized",
                new 
                { 
                    InitialCash = InitialCashBalance,
                    MarginRequirements = new { Long = MarginRequirementLong, Short = MarginRequirementShort }
                });
        }

        #endregion

        #region IPortfolioManager Implementation

        public async Task<Portfolio> GetPortfolioAsync()
        {
            return await TrackOperationAsync(
                "GetPortfolio",
                async () =>
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
                    
                    // Check for risk alerts
                    if (Math.Abs(dayPnL) > DayPnLAlertThreshold)
                    {
                        Logger.LogRiskEvent(
                            $"Significant day P&L: ${dayPnL:N2}",
                            Math.Abs(dayPnL),
                            DayPnLAlertThreshold,
                            "DayPnL",
                            new { Portfolio = portfolio });
                    }
                    
                    Logger.LogDebug(
                        $"Portfolio snapshot: Equity=${totalEquity:N2}, Positions={positions.Count()}, DayPnL=${dayPnL:N2}",
                        new { PortfolioSummary = portfolio });

                    return portfolio;
                });
        }

        public async Task<IEnumerable<Position>> GetPositionsAsync()
        {
            return await TrackOperationAsync(
                "GetPositions",
                async () =>
                {
                    var positionsList = new List<Position>();
                    var stalePositions = 0;

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
                            stalePositions++;
                            Logger.LogEvent(
                                TradingLogOrchestratorEnhanced.MARKET_DATA_STALE,
                                $"Using stale position data for {kvp.Key}",
                                new { Symbol = kvp.Key, Error = ex.Message },
                                LogLevel.Warning);
                            
                            positionsList.Add(kvp.Value); // Add stale position data
                        }
                    }

                    if (stalePositions > 0)
                    {
                        Logger.LogEvent(
                            TradingLogOrchestratorEnhanced.DATA_VALIDATION_FAILED,
                            $"Failed to update {stalePositions} positions with current market data",
                            new { StaleCount = stalePositions, TotalPositions = positionsList.Count },
                            LogLevel.Warning);
                    }

                    return positionsList.OrderByDescending(p => Math.Abs(p.MarketValue)).AsEnumerable();
                });
        }

        public async Task<Position?> GetPositionAsync(string symbol)
        {
            return await TrackOperationAsync(
                "GetPosition",
                async () =>
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
                },
                new { Symbol = symbol });
        }

        public async Task UpdatePositionAsync(string symbol, Execution execution)
        {
            await TrackOperationAsync(
                "UpdatePosition",
                async () =>
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
                },
                new { Symbol = symbol, ExecutionId = execution.ExecutionId });
        }

        public async Task<decimal> GetBuyingPowerAsync()
        {
            return await TrackOperationAsync(
                "GetBuyingPower",
                async () =>
                {
                    var positions = await GetPositionsAsync();
                    return await CalculateBuyingPowerAsync(positions);
                });
        }

        public async Task<bool> HasSufficientBuyingPowerAsync(OrderRequest orderRequest, decimal estimatedPrice)
        {
            return await TrackOperationAsync(
                "CheckBuyingPower",
                async () =>
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
                        Logger.LogEvent(
                            TradingLogOrchestratorEnhanced.RISK_LIMIT_BREACH,
                            $"Insufficient buying power for {orderRequest.Symbol}",
                            new 
                            { 
                                Symbol = orderRequest.Symbol,
                                Required = requiredCapitalWithBuffer,
                                Available = buyingPower,
                                Shortfall = requiredCapitalWithBuffer - buyingPower
                            },
                            LogLevel.Warning);
                    }

                    UpdateMetric("BuyingPower.Checks", 1);
                    UpdateMetric("BuyingPower.Rejections", hasSufficientPower ? 0 : 1);

                    return hasSufficientPower;
                },
                new { Symbol = orderRequest.Symbol, Quantity = orderRequest.Quantity, EstimatedPrice = estimatedPrice });
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
            Interlocked.Increment(ref _totalPositionsCreated);

            // Update cash balance
            var cashImpact = CalculateCashImpact(execution);
            UpdateCashBalance(cashImpact);

            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.TRADE_EXECUTED,
                $"Created new position for {symbol}",
                new 
                { 
                    Symbol = symbol,
                    Quantity = newPosition.Quantity,
                    Price = execution.Price,
                    MarketValue = newPosition.MarketValue,
                    CashImpact = cashImpact
                });

            UpdateMetric("Positions.Created", _totalPositionsCreated);
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

                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.TRADE_EXECUTED,
                    $"Realized P&L for {symbol}",
                    new 
                    { 
                        Symbol = symbol,
                        ClosingQuantity = closingQuantity,
                        PnLPerShare = pnlPerShare,
                        RealizedPnL = closingPnL,
                        TotalRealizedPnL = _totalRealizedPnL
                    });

                UpdateMetric($"Positions.{symbol}.RealizedPnL", closingPnL);
            }

            if (Math.Abs(newTotalQuantity) < 0.001m) // Position closed (using small epsilon for decimal comparison)
            {
                // Position closed completely
                _positions.TryRemove(symbol, out _);
                newAveragePrice = 0m;
                Interlocked.Increment(ref _totalPositionsClosed);
                
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.TRADE_EXECUTED,
                    $"Closed position for {symbol}",
                    new 
                    { 
                        Symbol = symbol,
                        TotalRealizedPnL = realizedPnL,
                        PositionsClosed = _totalPositionsClosed
                    });

                UpdateMetric("Positions.Closed", _totalPositionsClosed);
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

                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.TRADE_EXECUTED,
                    $"Updated position for {symbol}",
                    new 
                    { 
                        Symbol = symbol,
                        OldQuantity = existingPosition.Quantity,
                        NewQuantity = newTotalQuantity,
                        AveragePrice = newAveragePrice,
                        UnrealizedPnL = updatedPosition.UnrealizedPnL
                    });

                // Check for large position risk
                if (Math.Abs(updatedPosition.MarketValue) > RiskAlertThreshold)
                {
                    Logger.LogRiskEvent(
                        $"Large position exposure for {symbol}",
                        Math.Abs(updatedPosition.MarketValue),
                        RiskAlertThreshold,
                        "PositionSize",
                        new { Position = updatedPosition });
                }
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
            
            Logger.LogDebug(
                $"Cash balance updated: ${oldBalance:N2} -> ${_cashBalance:N2}",
                new { OldBalance = oldBalance, NewBalance = _cashBalance, Change = amount });

            // Check for low cash warning
            if (_cashBalance < 10_000m && oldBalance >= 10_000m)
            {
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.LOSS_LIMIT_APPROACHING,
                    "Cash balance below $10,000",
                    new { CashBalance = _cashBalance },
                    LogLevel.Warning);
            }
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
                
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.SYSTEM_STARTUP,
                    "New trading day started",
                    new { Date = currentDate, StartingEquity = _dayStartEquity });
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

            // Check for position-specific risks
            var positionRisk = Math.Abs(position.UnrealizedPnL);
            if (positionRisk > 1000m) // $1000 unrealized loss
            {
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.RISK_WARNING,
                    $"Significant unrealized P&L for {position.Symbol}",
                    new 
                    { 
                        Symbol = position.Symbol,
                        UnrealizedPnL = position.UnrealizedPnL,
                        PositionSize = position.Quantity
                    },
                    LogLevel.Warning);
            }
        }

        private async void UpdateAllPositions(object? state)
        {
            var operationId = StartOperation("UpdateAllPositions");
            
            try
            {
                var updateTasks = _positions.Keys.Select(async symbol =>
                {
                    await GetPositionAsync(symbol);
                }).ToArray();

                await Task.WhenAll(updateTasks);
                
                CompleteOperation(operationId, new { UpdatedPositions = updateTasks.Length });
                UpdateMetric("PositionUpdates.Completed", 1);
            }
            catch (Exception ex)
            {
                FailOperation(operationId, ex, "Failed to update all positions");
                UpdateMetric("PositionUpdates.Failed", 1);
            }
        }

        #endregion

        #region Lifecycle Management

        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
        {
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                $"Initializing PortfolioManager with ${InitialCashBalance:N2} starting capital",
                new { InitialCashBalance, ServiceName });
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
        {
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.SYSTEM_STARTUP,
                "Starting position update timer",
                new { UpdateIntervalMs = PositionUpdateIntervalMs });
            
            _positionUpdateTimer.Change(
                TimeSpan.FromMilliseconds(PositionUpdateIntervalMs),
                TimeSpan.FromMilliseconds(PositionUpdateIntervalMs));
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
        {
            _positionUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Log final portfolio state
            var portfolio = await GetPortfolioAsync();
            
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.SYSTEM_SHUTDOWN,
                "Portfolio manager stopped",
                new 
                { 
                    FinalValue = portfolio.TotalValue,
                    TotalPnL = portfolio.TotalPnL,
                    PositionsCreated = _totalPositionsCreated,
                    PositionsClosed = _totalPositionsClosed
                });
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<Dictionary<string, HealthCheckEntry>> OnCheckHealthAsync()
        {
            var checks = new Dictionary<string, HealthCheckEntry>();
            
            // Check cash balance health
            checks["cash_balance"] = new HealthCheckEntry
            {
                Status = _cashBalance > 0 ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = $"Cash balance: ${_cashBalance:N2}",
                Data = new Dictionary<string, object> { ["CashBalance"] = _cashBalance }
            };
            
            // Check position count
            var positionCount = _positions.Count;
            checks["position_count"] = new HealthCheckEntry
            {
                Status = positionCount < 100 ? HealthStatus.Healthy : 
                         positionCount < 200 ? HealthStatus.Degraded : HealthStatus.Unhealthy,
                Description = $"Active positions: {positionCount}",
                Data = new Dictionary<string, object> { ["PositionCount"] = positionCount }
            };
            
            // Check market data connectivity
            try
            {
                var testPrice = await _orderBookSimulator.GetCurrentPriceAsync("TEST");
                checks["market_data"] = new HealthCheckEntry
                {
                    Status = HealthStatus.Healthy,
                    Description = "Market data connection healthy"
                };
            }
            catch
            {
                checks["market_data"] = new HealthCheckEntry
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Market data connection failed"
                };
            }
            
            return checks;
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