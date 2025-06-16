using TradingPlatform.RiskManagement.Services;
using TradingPlatform.Messaging.Interfaces;

namespace TradingPlatform.RiskManagement.Services;

public class RiskMonitoringBackgroundService : BackgroundService
{
    private readonly IRiskManagementService _riskService;
    private readonly IRiskAlertService _alertService;
    private readonly IPositionMonitor _positionMonitor;
    private readonly IComplianceMonitor _complianceMonitor;
    private readonly ILogger<RiskMonitoringBackgroundService> _logger;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(5); // Real-time monitoring

    public RiskMonitoringBackgroundService(
        IRiskManagementService riskService,
        IRiskAlertService alertService,
        IPositionMonitor positionMonitor,
        IComplianceMonitor complianceMonitor,
        ILogger<RiskMonitoringBackgroundService> logger)
    {
        _riskService = riskService;
        _alertService = alertService;
        _positionMonitor = positionMonitor;
        _complianceMonitor = complianceMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Risk Monitoring Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                // Run all monitoring tasks concurrently for optimal performance
                await Task.WhenAll(
                    MonitorRiskStatusAsync(),
                    MonitorPositionLimitsAsync(),
                    MonitorComplianceAsync(),
                    CheckDrawdownLimitsAsync()
                );

                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogDebug("Risk monitoring cycle completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);

                await Task.Delay(_monitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Risk monitoring service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in risk monitoring cycle");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // Brief delay before retry
            }
        }

        _logger.LogInformation("Risk Monitoring Background Service stopped");
    }

    private async Task MonitorRiskStatusAsync()
    {
        try
        {
            var riskStatus = await _riskService.GetRiskStatusAsync();
            
            // Check if risk level is elevated
            if (riskStatus.RiskLevel >= Models.RiskLevel.High)
            {
                _logger.LogWarning("Elevated risk level detected: {RiskLevel} - Drawdown: {Drawdown:C}, Exposure: {Exposure:C}", 
                    riskStatus.RiskLevel, riskStatus.CurrentDrawdown, riskStatus.TotalExposure);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring risk status");
        }
    }

    private async Task MonitorPositionLimitsAsync()
    {
        try
        {
            var exceedingPositions = await _positionMonitor.GetPositionsExceedingLimitsAsync();
            
            foreach (var position in exceedingPositions)
            {
                await _alertService.CheckPositionLimitAsync(position.Symbol, position.Quantity);
                
                _logger.LogWarning("Position exceeding limits: {Symbol} - Exposure: {Exposure:C}", 
                    position.Symbol, position.RiskExposure);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring position limits");
        }
    }

    private async Task MonitorComplianceAsync()
    {
        try
        {
            var complianceStatus = await _complianceMonitor.GetComplianceStatusAsync();
            
            if (!complianceStatus.IsCompliant)
            {
                _logger.LogWarning("Compliance violations detected: {ViolationCount} violations", 
                    complianceStatus.Violations.Count());
                
                foreach (var violation in complianceStatus.Violations.Where(v => v.Severity >= Models.ViolationSeverity.Major))
                {
                    _logger.LogError("Major compliance violation: {RuleId} - {Description}", 
                        violation.RuleId, violation.Description);
                }
            }

            // Monitor PDT status
            if (complianceStatus.PDTStatus.IsPatternDayTrader && complianceStatus.PDTStatus.DayTradesRemaining <= 1)
            {
                _logger.LogWarning("PDT day trades running low: {Remaining} remaining", 
                    complianceStatus.PDTStatus.DayTradesRemaining);
            }

            // Monitor margin status
            if (complianceStatus.MarginStatus.HasMarginCall)
            {
                _logger.LogCritical("MARGIN CALL ACTIVE - Immediate attention required");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring compliance");
        }
    }

    private async Task CheckDrawdownLimitsAsync()
    {
        try
        {
            var positions = await _positionMonitor.GetAllPositionsAsync();
            var currentDrawdown = positions.Where(p => p.UnrealizedPnL < 0).Sum(p => Math.Abs(p.UnrealizedPnL));
            
            var isWithinLimits = await _alertService.CheckDrawdownLimitAsync(currentDrawdown);
            
            if (!isWithinLimits)
            {
                _logger.LogCritical("DRAWDOWN LIMIT EXCEEDED: {CurrentDrawdown:C}", currentDrawdown);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking drawdown limits");
        }
    }
}