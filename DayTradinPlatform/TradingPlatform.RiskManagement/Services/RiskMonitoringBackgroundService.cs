using TradingPlatform.RiskManagement.Services;
using TradingPlatform.Messaging.Interfaces;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.RiskManagement.Services;

public class RiskMonitoringBackgroundService : BackgroundService
{
    private readonly IRiskManagementService _riskService;
    private readonly IRiskAlertService _alertService;
    private readonly IPositionMonitor _positionMonitor;
    private readonly IComplianceMonitor _complianceMonitor;
    private readonly ITradingLogger _logger;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(5); // Real-time monitoring

    public RiskMonitoringBackgroundService(
        IRiskManagementService riskService,
        IRiskAlertService alertService,
        IPositionMonitor positionMonitor,
        IComplianceMonitor complianceMonitor,
        ITradingLogger logger)
    {
        _riskService = riskService;
        _alertService = alertService;
        _positionMonitor = positionMonitor;
        _complianceMonitor = complianceMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TradingLogOrchestrator.Instance.LogInfo("Risk Monitoring Background Service started");

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
                TradingLogOrchestrator.Instance.LogInfo($"Risk monitoring cycle completed in {elapsed.TotalMilliseconds}ms");

                await Task.Delay(_monitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                TradingLogOrchestrator.Instance.LogInfo("Risk monitoring service is stopping");
                break;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError("Error in risk monitoring cycle", ex);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // Brief delay before retry
            }
        }

        TradingLogOrchestrator.Instance.LogInfo("Risk Monitoring Background Service stopped");
    }

    private async Task MonitorRiskStatusAsync()
    {
        try
        {
            var riskStatus = await _riskService.GetRiskStatusAsync();

            // Check if risk level is elevated
            if (riskStatus.RiskLevel >= Models.RiskLevel.High)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Elevated risk level detected: {riskStatus.RiskLevel} - Drawdown: {riskStatus.CurrentDrawdown}, Exposure: {riskStatus.TotalExposure}");
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error monitoring risk status", ex);
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

                TradingLogOrchestrator.Instance.LogWarning($"Position exceeding limits: {position.Symbol} - Exposure: {position.RiskExposure}");
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error monitoring position limits", ex);
        }
    }

    private async Task MonitorComplianceAsync()
    {
        try
        {
            var complianceStatus = await _complianceMonitor.GetComplianceStatusAsync();

            if (!complianceStatus.IsCompliant)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Compliance violations detected: {complianceStatus.Violations.Count()} violations");

                foreach (var violation in complianceStatus.Violations.Where(v => v.Severity >= Models.ViolationSeverity.Major))
                {
                    TradingLogOrchestrator.Instance.LogError($"Major compliance violation: {violation.RuleId} - {violation.Description}");
                }
            }

            // Monitor PDT status
            if (complianceStatus.PDTStatus.IsPatternDayTrader && complianceStatus.PDTStatus.DayTradesRemaining <= 1)
            {
                TradingLogOrchestrator.Instance.LogWarning($"PDT day trades running low: {complianceStatus.PDTStatus.DayTradesRemaining} remaining");
            }

            // Monitor margin status
            if (complianceStatus.MarginStatus.HasMarginCall)
            {
                TradingLogOrchestrator.Instance.LogError("MARGIN CALL ACTIVE - Immediate attention required", userImpact: "Trading restrictions in effect", troubleshootingHints: "Add funds or close positions immediately");
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error monitoring compliance", ex);
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
                TradingLogOrchestrator.Instance.LogError($"DRAWDOWN LIMIT EXCEEDED: {currentDrawdown:C}", userImpact: "Trading may be halted", troubleshootingHints: "Review position sizes and stop losses");
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error checking drawdown limits", ex);
        }
    }
}