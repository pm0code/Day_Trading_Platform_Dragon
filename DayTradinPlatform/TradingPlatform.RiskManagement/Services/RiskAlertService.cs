using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.RiskManagement.Services;

public class RiskAlertService : IRiskAlertService
{
    private readonly IMessageBus _messageBus;
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, RiskAlert> _activeAlerts = new();

    public RiskAlertService(IMessageBus messageBus, ITradingLogger logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<IEnumerable<RiskAlert>> GetActiveAlertsAsync()
    {
        var activeAlerts = _activeAlerts.Values.Where(a => !a.IsResolved).OrderByDescending(a => a.CreatedAt);
        return await Task.FromResult(activeAlerts);
    }

    public async Task CreateAlertAsync(RiskAlert alert)
    {
        _activeAlerts.TryAdd(alert.Id, alert);

        await _messageBus.PublishAsync("risk.alert.created", new AlertEvent
        {
            AlertType = alert.Type.ToString(),
            Symbol = alert.Symbol,
            Message = alert.Message,
            Severity = alert.Severity.ToString(),
            RequiresAcknowledgment = alert.Severity >= RiskLevel.High
        });

        TradingLogOrchestrator.Instance.LogWarning($"Risk alert created: {alert.Type} for {alert.Symbol} - {alert.Message}");

        // Send notification based on severity
        if (alert.Severity >= RiskLevel.High)
        {
            await SendHighPriorityNotificationAsync(alert);
        }
    }

    public async Task ResolveAlertAsync(string alertId)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            var resolvedAlert = alert with
            {
                IsResolved = true,
                ResolvedAt = DateTime.UtcNow
            };

            _activeAlerts.TryUpdate(alertId, resolvedAlert, alert);

            await _messageBus.PublishAsync("risk.alert.resolved", new AlertEvent
            {
                AlertType = alert.Type.ToString(),
                Symbol = alert.Symbol,
                Message = "Alert resolved",
                Severity = alert.Severity.ToString(),
                RequiresAcknowledgment = false
            });

            TradingLogOrchestrator.Instance.LogInfo($"Risk alert resolved: {alertId} for {alert.Symbol}");
        }
    }

    public async Task<bool> CheckDrawdownLimitAsync(decimal currentDrawdown)
    {
        var maxDrawdownLimit = 25000m; // TODO: Get from risk limits

        if (currentDrawdown > maxDrawdownLimit * 0.8m) // 80% threshold
        {
            var alert = new RiskAlert(
                Id: Guid.NewGuid().ToString(),
                Type: RiskAlertType.DrawdownLimit,
                Symbol: "PORTFOLIO",
                Message: $"Portfolio drawdown approaching limit: {currentDrawdown:C} / {maxDrawdownLimit:C}",
                Severity: currentDrawdown > maxDrawdownLimit ? RiskLevel.Critical : RiskLevel.High,
                CreatedAt: DateTime.UtcNow
            );

            await CreateAlertAsync(alert);
            return currentDrawdown <= maxDrawdownLimit;
        }

        return true;
    }

    public async Task<bool> CheckPositionLimitAsync(string symbol, decimal quantity)
    {
        var maxPositionSize = 100000m; // TODO: Get from risk limits
        var estimatedValue = quantity * 100m; // Rough estimate, should use current price

        if (estimatedValue > maxPositionSize * 0.9m) // 90% threshold
        {
            var alert = new RiskAlert(
                Id: Guid.NewGuid().ToString(),
                Type: RiskAlertType.PositionLimit,
                Symbol: symbol,
                Message: $"Position size approaching limit: {estimatedValue:C} / {maxPositionSize:C}",
                Severity: estimatedValue > maxPositionSize ? RiskLevel.Critical : RiskLevel.Medium,
                CreatedAt: DateTime.UtcNow
            );

            await CreateAlertAsync(alert);
            return estimatedValue <= maxPositionSize;
        }

        return true;
    }

    public async Task<bool> CheckDailyLossLimitAsync(decimal currentLoss)
    {
        var maxDailyLoss = 10000m; // TODO: Get from risk limits

        if (currentLoss > maxDailyLoss * 0.8m) // 80% threshold
        {
            var alert = new RiskAlert(
                Id: Guid.NewGuid().ToString(),
                Type: RiskAlertType.DailyLossLimit,
                Symbol: "PORTFOLIO",
                Message: $"Daily loss approaching limit: {currentLoss:C} / {maxDailyLoss:C}",
                Severity: currentLoss > maxDailyLoss ? RiskLevel.Critical : RiskLevel.High,
                CreatedAt: DateTime.UtcNow
            );

            await CreateAlertAsync(alert);
            return currentLoss <= maxDailyLoss;
        }

        return true;
    }

    private async Task SendHighPriorityNotificationAsync(RiskAlert alert)
    {
        try
        {
            // Send immediate notification through message bus
            await _messageBus.PublishAsync("notifications.urgent", new AlertEvent
            {
                AlertType = "UrgentRiskAlert",
                Symbol = alert.Symbol,
                Message = $"URGENT: {alert.Type} Alert - {alert.Message}",
                Severity = alert.Severity.ToString(),
                RequiresAcknowledgment = true
            });

            TradingLogOrchestrator.Instance.LogError($"URGENT RISK ALERT: {alert.Type} for {alert.Symbol} - {alert.Message}",
                userImpact: "Trading restrictions may apply",
                troubleshootingHints: "Review risk alerts and take immediate action");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to send high priority notification for alert {alert.Id}", ex);
        }
    }
}