using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.RiskManagement.Services;

public class ComplianceMonitor : IComplianceMonitor
{
    private readonly IMessageBus _messageBus;
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, ComplianceEvent> _complianceEvents = new();
    private readonly ConcurrentDictionary<string, PatternDayTradingStatus> _pdtStatus = new();

    public ComplianceMonitor(IMessageBus messageBus, ITradingLogger logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<ComplianceStatus> GetComplianceStatusAsync()
    {
        var violations = GetActiveViolations();
        var pdtStatus = await GetPatternDayTradingStatusAsync("DEFAULT_ACCOUNT");
        var marginStatus = await GetMarginStatusAsync("DEFAULT_ACCOUNT");
        
        var isCompliant = !violations.Any(v => v.Severity >= ViolationSeverity.Major);
        
        var status = new ComplianceStatus(
            IsCompliant: isCompliant,
            Violations: violations,
            PDTStatus: pdtStatus,
            MarginStatus: marginStatus,
            LastChecked: DateTime.UtcNow
        );

        TradingLogOrchestrator.Instance.LogInfo($"Compliance status checked - Compliant: {isCompliant}, Violations: {violations.Count(}"));

        return status;
    }

    public async Task<bool> ValidatePatternDayTradingAsync(string accountId)
    {
        var pdtStatus = await GetPatternDayTradingStatusAsync(accountId);
        
        if (pdtStatus.IsPatternDayTrader && pdtStatus.DayTradesRemaining <= 0)
        {
            var violation = new ComplianceViolation(
                RuleId: "PDT_001",
                Description: "Pattern Day Trading limit exceeded",
                Symbol: "N/A",
                Amount: 0m,
                Severity: ViolationSeverity.Major,
                OccurredAt: DateTime.UtcNow
            );

            await LogComplianceViolationAsync(violation, accountId);
            return false;
        }

        if (pdtStatus.IsPatternDayTrader && pdtStatus.MinimumEquity < 25000m)
        {
            var violation = new ComplianceViolation(
                RuleId: "PDT_002",
                Description: "Pattern Day Trader minimum equity requirement not met",
                Symbol: "N/A",
                Amount: pdtStatus.MinimumEquity,
                Severity: ViolationSeverity.Critical,
                OccurredAt: DateTime.UtcNow
            );

            await LogComplianceViolationAsync(violation, accountId);
            return false;
        }

        return true;
    }

    public async Task<bool> ValidateMarginRequirementsAsync(string accountId, decimal orderValue)
    {
        var marginStatus = await GetMarginStatusAsync(accountId);
        
        if (marginStatus.HasMarginCall)
        {
            var violation = new ComplianceViolation(
                RuleId: "MARGIN_001",
                Description: "Active margin call prevents new orders",
                Symbol: "N/A",
                Amount: orderValue,
                Severity: ViolationSeverity.Critical,
                OccurredAt: DateTime.UtcNow
            );

            await LogComplianceViolationAsync(violation, accountId);
            return false;
        }

        if (orderValue > marginStatus.BuyingPower)
        {
            var violation = new ComplianceViolation(
                RuleId: "MARGIN_002",
                Description: "Order value exceeds available buying power",
                Symbol: "N/A",
                Amount: orderValue,
                Severity: ViolationSeverity.Major,
                OccurredAt: DateTime.UtcNow
            );

            await LogComplianceViolationAsync(violation, accountId);
            return false;
        }

        return true;
    }

    public async Task<bool> ValidateRegulatoryLimitsAsync(OrderRiskRequest request)
    {
        var violations = new List<ComplianceViolation>();

        // Validate order size limits (example: no single order > $1M)
        var orderValue = request.Quantity * request.Price;
        if (orderValue > 1000000m)
        {
            violations.Add(new ComplianceViolation(
                RuleId: "REG_001",
                Description: "Order size exceeds regulatory limit",
                Symbol: request.Symbol,
                Amount: orderValue,
                Severity: ViolationSeverity.Major,
                OccurredAt: DateTime.UtcNow
            ));
        }

        // Validate trading hours (simplified - should check actual market hours)
        var now = DateTime.Now.TimeOfDay;
        var marketOpen = new TimeSpan(9, 30, 0);
        var marketClose = new TimeSpan(16, 0, 0);
        
        if (now < marketOpen || now > marketClose)
        {
            violations.Add(new ComplianceViolation(
                RuleId: "REG_002",
                Description: "Order submitted outside market hours",
                Symbol: request.Symbol,
                Amount: orderValue,
                Severity: ViolationSeverity.Warning,
                OccurredAt: DateTime.UtcNow
            ));
        }

        // Log any violations
        foreach (var violation in violations)
        {
            await LogComplianceViolationAsync(violation, request.AccountId);
        }

        var hasMajorViolations = violations.Any(v => v.Severity >= ViolationSeverity.Major);
        
        TradingLogOrchestrator.Instance.LogInfo($"Regulatory validation for {request.Symbol}: {!hasMajorViolations ? "PASSED" : "FAILED"} ({violations.Count} violations)");

        return !hasMajorViolations;
    }

    public async Task LogComplianceEventAsync(ComplianceEvent complianceEvent)
    {
        _complianceEvents.TryAdd(complianceEvent.EventId, complianceEvent);
        
        await _messageBus.PublishAsync("compliance.event.logged", new AlertEvent
        {
            AlertType = "ComplianceEvent",
            Symbol = complianceEvent.Symbol ?? "N/A",
            Message = $"{complianceEvent.EventType}: {complianceEvent.Description}",
            Severity = "Info",
            RequiresAcknowledgment = false
        });

        TradingLogOrchestrator.Instance.LogInfo($"Compliance event logged: {complianceEvent.EventType} for account {complianceEvent.AccountId}");
    }

    private IEnumerable<ComplianceViolation> GetActiveViolations()
    {
        var recentCutoff = DateTime.UtcNow.AddHours(-24); // Last 24 hours
        
        return _complianceEvents.Values
            .Where(e => e.Timestamp >= recentCutoff)
            .Where(e => e.EventType.Contains("Violation"))
            .Select(e => new ComplianceViolation(
                RuleId: e.Metadata.GetValueOrDefault("RuleId", "UNKNOWN")?.ToString() ?? "UNKNOWN",
                Description: e.Description,
                Symbol: e.Symbol ?? "N/A",
                Amount: e.Amount ?? 0m,
                Severity: Enum.Parse<ViolationSeverity>(e.Metadata.GetValueOrDefault("Severity", "Warning")?.ToString() ?? "Warning"),
                OccurredAt: e.Timestamp
            ))
            .OrderByDescending(v => v.OccurredAt);
    }

    private async Task<PatternDayTradingStatus> GetPatternDayTradingStatusAsync(string accountId)
    {
        // Simplified PDT status - in real implementation, this would query account service
        if (!_pdtStatus.ContainsKey(accountId))
        {
            _pdtStatus.TryAdd(accountId, new PatternDayTradingStatus(
                IsPatternDayTrader: false,
                DayTradesUsed: 0,
                DayTradesRemaining: 3,
                MinimumEquity: 30000m,
                PeriodStart: DateTime.Today
            ));
        }

        return await Task.FromResult(_pdtStatus[accountId]);
    }

    private async Task<MarginStatus> GetMarginStatusAsync(string accountId)
    {
        // Simplified margin status - in real implementation, this would query account service
        return await Task.FromResult(new MarginStatus(
            MaintenanceMargin: 25000m,
            InitialMargin: 50000m,
            ExcessLiquidity: 100000m,
            BuyingPower: 200000m,
            HasMarginCall: false
        ));
    }

    private async Task LogComplianceViolationAsync(ComplianceViolation violation, string accountId)
    {
        var complianceEvent = new ComplianceEvent(
            EventId: Guid.NewGuid().ToString(),
            EventType: "ComplianceViolation",
            Description: violation.Description,
            AccountId: accountId,
            Symbol: violation.Symbol,
            Amount: violation.Amount,
            Timestamp: violation.OccurredAt,
            Metadata: new Dictionary<string, object>
            {
                ["RuleId"] = violation.RuleId,
                ["Severity"] = violation.Severity.ToString()
            }
        );

        await LogComplianceEventAsync(complianceEvent);
        
        TradingLogOrchestrator.Instance.LogWarning($"Compliance violation: {violation.RuleId} - {violation.Description} (Severity: {violation.Severity})");
    }
}