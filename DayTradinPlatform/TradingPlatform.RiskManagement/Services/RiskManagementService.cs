using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.RiskManagement.Services;

/// <summary>
/// High-performance risk management service for real-time trading risk monitoring
/// Implements comprehensive risk assessment, position validation, and compliance checking
/// All operations use TradingResult pattern for consistent error handling and observability
/// Maintains sub-millisecond risk validations for high-frequency trading requirements
/// </summary>
public class RiskManagementService : CanonicalServiceBase, IRiskManagementService
{
    private readonly IRiskCalculator _riskCalculator;
    private readonly IPositionMonitor _positionMonitor;
    private readonly IRiskAlertService _alertService;
    private readonly IMessageBus _messageBus;
    private readonly ConcurrentDictionary<string, RiskLimits> _riskLimits = new();
    private RiskLimits _defaultLimits = null!;
    
    // Performance tracking
    private long _totalRiskChecks = 0;
    private long _totalOrderValidations = 0;
    private long _totalRiskAlertsGenerated = 0;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Initializes a new instance of the RiskManagementService with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="riskCalculator">Risk calculation engine for VaR, ES, and portfolio metrics</param>
    /// <param name="positionMonitor">Position monitoring service for real-time exposure tracking</param>
    /// <param name="alertService">Alert service for risk violation notifications</param>
    /// <param name="messageBus">Message bus for risk event publishing</param>
    /// <param name="logger">Trading logger for comprehensive risk monitoring</param>
    public RiskManagementService(
        IRiskCalculator riskCalculator,
        IPositionMonitor positionMonitor,
        IRiskAlertService alertService,
        IMessageBus messageBus,
        ITradingLogger logger) : base(logger, "RiskManagementService")
    {
        _riskCalculator = riskCalculator;
        _positionMonitor = positionMonitor;
        _alertService = alertService;
        _messageBus = messageBus;

        InitializeDefaultLimits();
    }

    /// <summary>
    /// Retrieves comprehensive real-time portfolio risk status including drawdown, exposure, and risk levels
    /// Calculates current position metrics and compares against configured risk limits
    /// </summary>
    /// <returns>A TradingResult containing the current risk status or error information</returns>
    public async Task<TradingResult<RiskStatus>> GetRiskStatusAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            LogInfo("Calculating comprehensive risk status");
            
            var positions = await _positionMonitor.GetAllPositionsAsync();
            var totalExposure = await _positionMonitor.GetTotalExposureAsync();

            var portfolioValues = positions.Select(p => p.MarketValue);
            var maxDrawdown = _riskCalculator.CalculateMaxDrawdown(portfolioValues);

            var dailyPnL = positions.Sum(p => p.UnrealizedPnL + p.RealizedPnL);
            var currentDrawdown = Math.Abs(Math.Min(0, dailyPnL));

            var riskLevel = await DetermineRiskLevelAsync(currentDrawdown, maxDrawdown, totalExposure);

            var status = new RiskStatus(
                CurrentDrawdown: currentDrawdown,
                MaxDrawdown: maxDrawdown,
                DailyPnL: dailyPnL,
                TotalExposure: totalExposure,
                AvailableCapital: 1000000m - totalExposure, // TODO: Get from account service
                RiskLevel: riskLevel,
                LastUpdated: DateTime.UtcNow
            );
            
            lock (_metricsLock)
            {
                _totalRiskChecks++;
            }

            LogInfo($"Risk status calculated in {stopwatch.ElapsedMilliseconds}ms - Risk Level: {riskLevel}, Total Exposure: {totalExposure:C}");

            await _messageBus.PublishAsync("risk.status.updated", new RiskEvent
            {
                RiskType = "StatusUpdate",
                Symbol = "PORTFOLIO",
                CurrentExposure = status.TotalExposure,
                RiskLimit = _defaultLimits.MaxTotalExposure,
                UtilizationPercent = status.TotalExposure / _defaultLimits.MaxTotalExposure * 100m,
                LimitBreached = status.RiskLevel >= RiskLevel.Critical,
                Action = "Monitor"
            });

            LogMethodExit();
            return TradingResult<RiskStatus>.Success(status);
        }
        catch (Exception ex)
        {
            LogError("Error calculating risk status", ex);
            LogMethodExit();
            return TradingResult<RiskStatus>.Failure("RISK_STATUS_ERROR", 
                $"Failed to calculate risk status: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves the currently configured risk limits for the portfolio
    /// Returns comprehensive risk limits including position sizes, drawdown limits, and concentration limits
    /// </summary>
    /// <returns>A TradingResult containing the current risk limits or error information</returns>
    public async Task<TradingResult<RiskLimits>> GetRiskLimitsAsync()
    {
        LogMethodEntry();
        try
        {
            LogDebug("Retrieving current risk limits configuration");
            
            var limits = await Task.FromResult(_defaultLimits);
            
            LogInfo($"Risk limits retrieved - Max Daily Loss: {limits.MaxDailyLoss:C}, Max Total Exposure: {limits.MaxTotalExposure:C}");
            
            LogMethodExit();
            return TradingResult<RiskLimits>.Success(limits);
        }
        catch (Exception ex)
        {
            LogError("Error retrieving risk limits", ex);
            LogMethodExit();
            return TradingResult<RiskLimits>.Failure("RISK_LIMITS_RETRIEVAL_ERROR", 
                $"Failed to retrieve risk limits: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Updates the portfolio risk limits with new configuration values
    /// Validates new limits and publishes update events to all interested systems
    /// </summary>
    /// <param name="limits">The new risk limits configuration to apply</param>
    /// <returns>A TradingResult indicating success or failure of the update</returns>
    public async Task<TradingResult<bool>> UpdateRiskLimitsAsync(RiskLimits limits)
    {
        LogMethodEntry();
        try
        {
            if (limits == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_LIMITS", "Risk limits cannot be null");
            }
            
            LogInfo($"Updating risk limits - Max Daily Loss: {limits.MaxDailyLoss:C}, Max Total Exposure: {limits.MaxTotalExposure:C}");
            
            // Validate new limits
            if (limits.MaxDailyLoss <= 0 || limits.MaxTotalExposure <= 0)
            {
                LogWarning("Invalid risk limits provided - values must be positive");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }

            _defaultLimits = limits;

            await _messageBus.PublishAsync("risk.limits.updated", new RiskEvent
            {
                RiskType = "LimitsUpdate",
                Symbol = "PORTFOLIO",
                CurrentExposure = 0m,
                RiskLimit = limits.MaxTotalExposure,
                UtilizationPercent = 0m,
                LimitBreached = false,
                Action = "Configure"
            });

            LogInfo($"Risk limits updated successfully - Max Daily Loss: {limits.MaxDailyLoss:C}, Max Drawdown: {limits.MaxDrawdown:C}");
            
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Error updating risk limits", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("RISK_LIMITS_UPDATE_ERROR", 
                $"Failed to update risk limits: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates an order against comprehensive risk limits and concentration rules
    /// Performs real-time risk assessment including position size, symbol concentration, and portfolio limits
    /// </summary>
    /// <param name="request">The order risk request containing symbol, quantity, and price information</param>
    /// <returns>A TradingResult indicating whether the order passes risk validation</returns>
    public async Task<TradingResult<bool>> ValidateOrderAsync(OrderRiskRequest request)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (request == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_REQUEST", "Order risk request cannot be null");
            }
            
            if (string.IsNullOrEmpty(request.Symbol))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOL", "Order symbol cannot be null or empty");
            }
            
            LogInfo($"Validating order risk for {request.Symbol}: Quantity={request.Quantity}, Price={request.Price:C}");
            
            // Check position size limits
            var positionRiskResult = await CalculatePositionRiskAsync(request.Symbol, request.Quantity, request.Price);
            if (!positionRiskResult.IsSuccess)
            {
                LogError($"Failed to calculate position risk for {request.Symbol}");
                LogMethodExit();
                return TradingResult<bool>.Failure("POSITION_RISK_CALCULATION_ERROR", 
                    "Failed to calculate position risk");
            }
            
            var positionRisk = positionRiskResult.Value;
            if (positionRisk > _defaultLimits.MaxPositionSize)
            {
                await CreateRiskAlertAsync(RiskAlertType.PositionLimit, request.Symbol, 
                    $"Order exceeds position size limit: {positionRisk:C}", RiskLevel.High);
                
                LogWarning($"Order rejected - position size limit exceeded: {positionRisk:C} > {_defaultLimits.MaxPositionSize:C}");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }

            // Check symbol concentration
            var symbolExposure = await _positionMonitor.GetSymbolExposureAsync(request.Symbol);
            var orderValue = Math.Abs(request.Quantity * request.Price);
            var newSymbolExposure = symbolExposure + orderValue;
            var totalExposure = await _positionMonitor.GetTotalExposureAsync();

            var concentrationRatio = newSymbolExposure / (totalExposure + orderValue);
            if (concentrationRatio > _defaultLimits.MaxSymbolConcentration)
            {
                await CreateRiskAlertAsync(RiskAlertType.ConcentrationLimit, request.Symbol, 
                    $"Order would exceed symbol concentration limit: {concentrationRatio:P2}", RiskLevel.Medium);
                
                LogWarning($"Order rejected - symbol concentration limit exceeded: {concentrationRatio:P2} > {_defaultLimits.MaxSymbolConcentration:P2}");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }
            
            lock (_metricsLock)
            {
                _totalOrderValidations++;
            }

            LogInfo($"Order validation passed in {stopwatch.ElapsedMilliseconds}ms for {request.Symbol}");
            
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Error validating order for {request?.Symbol}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("ORDER_VALIDATION_ERROR", 
                $"Failed to validate order: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the position risk for a potential order including current exposure
    /// Computes the maximum exposure between current position and new position after order execution
    /// </summary>
    /// <param name="symbol">The trading symbol to calculate risk for</param>
    /// <param name="quantity">The order quantity (positive for buy, negative for sell)</param>
    /// <param name="price">The order price per share</param>
    /// <returns>A TradingResult containing the calculated position risk amount</returns>
    public async Task<TradingResult<decimal>> CalculatePositionRiskAsync(string symbol, decimal quantity, decimal price)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }
            
            if (price <= 0)
            {
                LogMethodExit();
                return TradingResult<decimal>.Failure("INVALID_PRICE", "Price must be positive");
            }
            
            LogDebug($"Calculating position risk for {symbol}: Quantity={quantity}, Price={price:C}");
            
            var position = await _positionMonitor.GetPositionAsync(symbol);
            var orderValue = Math.Abs(quantity * price);

            if (position == null)
            {
                LogDebug($"No existing position for {symbol}, risk = order value: {orderValue:C}");
                LogMethodExit();
                return TradingResult<decimal>.Success(orderValue);
            }

            var currentExposure = Math.Abs(position.Quantity * position.CurrentPrice);
            var newQuantity = position.Quantity + quantity;
            var newExposure = Math.Abs(newQuantity * price);
            var positionRisk = Math.Max(currentExposure, newExposure);

            LogDebug($"Position risk calculated for {symbol}: Current={currentExposure:C}, New={newExposure:C}, Risk={positionRisk:C}");
            
            LogMethodExit();
            return TradingResult<decimal>.Success(positionRisk);
        }
        catch (Exception ex)
        {
            LogError($"Error calculating position risk for {symbol}", ex);
            LogMethodExit();
            return TradingResult<decimal>.Failure("POSITION_RISK_CALCULATION_ERROR", 
                $"Failed to calculate position risk: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive portfolio risk metrics including VaR, ES, and other risk measures
    /// Calculates advanced risk analytics across all portfolio positions
    /// </summary>
    /// <returns>A TradingResult containing the portfolio risk metrics or error information</returns>
    public async Task<TradingResult<RiskMetrics>> GetRiskMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            LogInfo("Calculating comprehensive portfolio risk metrics");
            
            var positions = await _positionMonitor.GetAllPositionsAsync();
            var metrics = _riskCalculator.CalculatePortfolioRisk(positions);
            
            LogInfo($"Risk metrics calculated - VaR: {metrics.ValueAtRisk:C}, Portfolio Count: {positions.Count()}");
            
            LogMethodExit();
            return TradingResult<RiskMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            LogError("Error calculating risk metrics", ex);
            LogMethodExit();
            return TradingResult<RiskMetrics>.Failure("RISK_METRICS_ERROR", 
                $"Failed to calculate risk metrics: {ex.Message}", ex);
        }
    }

    private RiskLevel DetermineRiskLevel(decimal currentDrawdown, decimal maxDrawdown, decimal totalExposure)
    {
        var drawdownRatio = currentDrawdown / _defaultLimits.MaxDailyLoss;
        var exposureRatio = totalExposure / _defaultLimits.MaxTotalExposure;

        var maxRatio = Math.Max(drawdownRatio, exposureRatio);

        return maxRatio switch
        {
            >= 0.9m => RiskLevel.Critical,
            >= 0.7m => RiskLevel.High,
            >= 0.5m => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private void InitializeDefaultLimits()
    {
        _defaultLimits = new RiskLimits(
            MaxDailyLoss: 10000m,
            MaxDrawdown: 25000m,
            MaxPositionSize: 100000m,
            MaxTotalExposure: 500000m,
            MaxSymbolConcentration: 0.20m,
            MaxPositions: 20,
            EnableStopLoss: true
        );
    }
    
    /// <summary>
    /// Creates and publishes a risk alert with the specified parameters
    /// </summary>
    private async Task CreateRiskAlertAsync(RiskAlertType alertType, string symbol, string message, RiskLevel severity)
    {
        LogMethodEntry();
        try
        {
            var alert = new RiskAlert(
                Id: Guid.NewGuid().ToString(),
                Type: alertType,
                Symbol: symbol,
                Message: message,
                Severity: severity,
                CreatedAt: DateTime.UtcNow
            );
            
            await _alertService.CreateAlertAsync(alert);
            
            lock (_metricsLock)
            {
                _totalRiskAlertsGenerated++;
            }
            
            LogInfo($"Risk alert created: {alertType} for {symbol} - {message}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error creating risk alert for {symbol}", ex);
            LogMethodExit();
            throw;
        }
    }
    
    // ========== SERVICE HEALTH & METRICS ==========
    
    /// <summary>
    /// Gets comprehensive metrics about the risk management service
    /// </summary>
    public async Task<TradingResult<RiskManagementMetrics>> GetMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            var metrics = new RiskManagementMetrics
            {
                TotalRiskChecks = _totalRiskChecks,
                TotalOrderValidations = _totalOrderValidations,
                TotalRiskAlertsGenerated = _totalRiskAlertsGenerated,
                ActivePositions = (await _positionMonitor.GetAllPositionsAsync()).Count(),
                CurrentTotalExposure = await _positionMonitor.GetTotalExposureAsync(),
                RiskLimitsConfigured = _defaultLimits != null,
                Timestamp = DateTime.UtcNow
            };
            
            LogInfo($"Risk management metrics: {metrics.TotalRiskChecks} checks, {metrics.TotalOrderValidations} validations, {metrics.ActivePositions} positions");
            LogMethodExit();
            return await Task.FromResult(TradingResult<RiskManagementMetrics>.Success(metrics));
        }
        catch (Exception ex)
        {
            LogError("Error retrieving risk management metrics", ex);
            LogMethodExit();
            return TradingResult<RiskManagementMetrics>.Failure("METRICS_ERROR", 
                $"Failed to retrieve metrics: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Performs health check on the risk management service
    /// </summary>
    protected override async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        LogMethodEntry();
        try
        {
            // Check if risk limits are configured
            var hasLimits = _defaultLimits != null;
            
            // Check if position monitor is accessible
            var canAccessPositions = _positionMonitor != null;
            
            // Check if risk calculator is available
            var hasRiskCalculator = _riskCalculator != null;
            
            // Check for service performance
            var performanceHealthy = _totalRiskChecks >= 0 && _totalOrderValidations >= 0;
            
            var isHealthy = hasLimits && canAccessPositions && hasRiskCalculator && performanceHealthy;
            
            var details = new Dictionary<string, object>
            {
                ["HasRiskLimits"] = hasLimits,
                ["CanAccessPositions"] = canAccessPositions,
                ["HasRiskCalculator"] = hasRiskCalculator,
                ["PerformanceHealthy"] = performanceHealthy,
                ["TotalRiskChecks"] = _totalRiskChecks,
                ["TotalOrderValidations"] = _totalOrderValidations,
                ["TotalAlertsGenerated"] = _totalRiskAlertsGenerated
            };
            
            LogMethodExit();
            return new HealthCheckResult(isHealthy, "Risk Management Service", details);
        }
        catch (Exception ex)
        {
            LogError("Error performing health check", ex);
            LogMethodExit();
            return new HealthCheckResult(false, "Risk Management Service", 
                new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }
}

/// <summary>
/// Risk management service metrics for monitoring and performance tracking
/// </summary>
public record RiskManagementMetrics
{
    public long TotalRiskChecks { get; init; }
    public long TotalOrderValidations { get; init; }
    public long TotalRiskAlertsGenerated { get; init; }
    public int ActivePositions { get; init; }
    public decimal CurrentTotalExposure { get; init; }
    public bool RiskLimitsConfigured { get; init; }
    public DateTime Timestamp { get; init; }
}