using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.RiskManagement.Services;

public class RiskManagementService : IRiskManagementService
{
    private readonly IRiskCalculator _riskCalculator;
    private readonly IPositionMonitor _positionMonitor;
    private readonly IRiskAlertService _alertService;
    private readonly IMessageBus _messageBus;
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, RiskLimits> _riskLimits = new();
    private RiskLimits _defaultLimits = null!;

    public RiskManagementService(
        IRiskCalculator riskCalculator,
        IPositionMonitor positionMonitor,
        IRiskAlertService alertService,
        IMessageBus messageBus,
        ITradingLogger logger)
    {
        _riskCalculator = riskCalculator;
        _positionMonitor = positionMonitor;
        _alertService = alertService;
        _messageBus = messageBus;
        _logger = logger;

        InitializeDefaultLimits();
    }

    public async Task<RiskStatus> GetRiskStatusAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var positions = await _positionMonitor.GetAllPositionsAsync();
            var totalExposure = await _positionMonitor.GetTotalExposureAsync();

            var portfolioValues = positions.Select(p => p.MarketValue);
            var maxDrawdown = _riskCalculator.CalculateMaxDrawdown(portfolioValues);

            var dailyPnL = positions.Sum(p => p.UnrealizedPnL + p.RealizedPnL);
            var currentDrawdown = Math.Abs(Math.Min(0, dailyPnL));

            var riskLevel = DetermineRiskLevel(currentDrawdown, maxDrawdown, totalExposure);

            var status = new RiskStatus(
                CurrentDrawdown: currentDrawdown,
                MaxDrawdown: maxDrawdown,
                DailyPnL: dailyPnL,
                TotalExposure: totalExposure,
                AvailableCapital: 1000000m - totalExposure, // TODO: Get from account service
                RiskLevel: riskLevel,
                LastUpdated: DateTime.UtcNow
            );

            var elapsed = DateTime.UtcNow - startTime;
            TradingLogOrchestrator.Instance.LogInfo($"Risk status calculated in {elapsed.TotalMilliseconds}ms - Risk Level: {riskLevel}");

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

            return status;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating risk status", ex);
            throw;
        }
    }

    public async Task<RiskLimits> GetRiskLimitsAsync()
    {
        return await Task.FromResult(_defaultLimits);
    }

    public async Task UpdateRiskLimitsAsync(RiskLimits limits)
    {
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

        TradingLogOrchestrator.Instance.LogInfo($"Risk limits updated - Max Daily Loss: {limits.MaxDailyLoss}, Max Drawdown: {limits.MaxDrawdown}");
    }

    public async Task<bool> ValidateOrderAsync(OrderRiskRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Check position size limits
            var positionRisk = await CalculatePositionRiskAsync(request.Symbol, request.Quantity, request.Price);
            if (positionRisk > _defaultLimits.MaxPositionSize)
            {
                await _alertService.CreateAlertAsync(new RiskAlert(
                    Id: Guid.NewGuid().ToString(),
                    Type: RiskAlertType.PositionLimit,
                    Symbol: request.Symbol,
                    Message: $"Order exceeds position size limit: {positionRisk:C}",
                    Severity: RiskLevel.High,
                    CreatedAt: DateTime.UtcNow
                ));
                return false;
            }

            // Check symbol concentration
            var symbolExposure = await _positionMonitor.GetSymbolExposureAsync(request.Symbol);
            var orderValue = request.Quantity * request.Price;
            var newSymbolExposure = symbolExposure + orderValue;
            var totalExposure = await _positionMonitor.GetTotalExposureAsync();

            if (newSymbolExposure / (totalExposure + orderValue) > _defaultLimits.MaxSymbolConcentration)
            {
                await _alertService.CreateAlertAsync(new RiskAlert(
                    Id: Guid.NewGuid().ToString(),
                    Type: RiskAlertType.ConcentrationLimit,
                    Symbol: request.Symbol,
                    Message: $"Order would exceed symbol concentration limit",
                    Severity: RiskLevel.Medium,
                    CreatedAt: DateTime.UtcNow
                ));
                return false;
            }

            var elapsed = DateTime.UtcNow - startTime;
            TradingLogOrchestrator.Instance.LogInfo($"Order validation completed in {elapsed.TotalMilliseconds}ms for {request.Symbol}");

            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error validating order for {request.Symbol}", ex);
            return false;
        }
    }

    public async Task<decimal> CalculatePositionRiskAsync(string symbol, decimal quantity, decimal price)
    {
        var position = await _positionMonitor.GetPositionAsync(symbol);
        var orderValue = Math.Abs(quantity * price);

        if (position == null)
        {
            return orderValue;
        }

        var currentExposure = Math.Abs(position.Quantity * position.CurrentPrice);
        var newQuantity = position.Quantity + quantity;
        var newExposure = Math.Abs(newQuantity * price);

        return Math.Max(currentExposure, newExposure);
    }

    public async Task<RiskMetrics> GetRiskMetricsAsync()
    {
        var positions = await _positionMonitor.GetAllPositionsAsync();
        return _riskCalculator.CalculatePortfolioRisk(positions);
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
}