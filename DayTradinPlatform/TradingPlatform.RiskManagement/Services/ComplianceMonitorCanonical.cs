// File: TradingPlatform.RiskManagement.Services\ComplianceMonitorCanonical.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.RiskManagement.Models;

namespace TradingPlatform.RiskManagement.Services
{
    /// <summary>
    /// Canonical implementation of compliance monitoring with PDT rules, margin requirements,
    /// and regulatory limit validation with comprehensive tracking and alerting.
    /// </summary>
    public class ComplianceMonitorCanonical : CanonicalRiskEvaluator<ComplianceContext>, IComplianceMonitor
    {
        private readonly IMessageBus _messageBus;
        private readonly ConcurrentDictionary<string, ComplianceEvent> _complianceEvents = new();
        private readonly ConcurrentDictionary<string, PatternDayTradingStatus> _pdtStatus = new();
        private readonly ConcurrentDictionary<string, MarginStatus> _marginStatus = new();
        private readonly ConcurrentDictionary<string, List<ComplianceViolation>> _activeViolations = new();

        private const decimal MIN_ACCOUNT_BALANCE_PDT = 25000m;
        private const int MAX_DAY_TRADES_NON_PDT = 3;
        private const decimal DEFAULT_MARGIN_REQUIREMENT = 0.25m; // 25%
        private const decimal MAINTENANCE_MARGIN = 0.30m; // 30%

        protected override string RiskType => "Compliance";

        public ComplianceMonitorCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "ComplianceMonitor")
        {
            _messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        }

        public async Task<ComplianceStatus> GetComplianceStatusAsync()
        {
            return await ExecuteOperationAsync(
                nameof(GetComplianceStatusAsync),
                async () =>
                {
                    var allViolations = new List<ComplianceViolation>();
                    
                    // Aggregate violations from all accounts
                    foreach (var kvp in _activeViolations)
                    {
                        allViolations.AddRange(kvp.Value);
                    }

                    // Get default account statuses
                    var pdtStatus = await GetPatternDayTradingStatusAsync("DEFAULT_ACCOUNT");
                    var marginStatus = await GetMarginStatusAsync("DEFAULT_ACCOUNT");

                    var isCompliant = !allViolations.Any(v => v.Severity >= ViolationSeverity.Major);

                    var status = new ComplianceStatus(
                        IsCompliant: isCompliant,
                        Violations: allViolations,
                        PDTStatus: pdtStatus,
                        MarginStatus: marginStatus,
                        LastChecked: DateTime.UtcNow
                    );

                    // Record metrics
                    RecordRiskMetric("Compliance.TotalViolations", allViolations.Count);
                    RecordRiskMetric("Compliance.MajorViolations", allViolations.Count(v => v.Severity == ViolationSeverity.Major));
                    RecordRiskMetric("Compliance.IsCompliant", isCompliant ? 1m : 0m);

                    _logger.LogInformation($"Compliance status: {(isCompliant ? "Compliant" : "Non-Compliant")} with {allViolations.Count} violations",
                        new { IsCompliant = isCompliant, ViolationCount = allViolations.Count });

                    return TradingResult<ComplianceStatus>.Success(status);
                },
                createDefaultResult: () => new ComplianceStatus(
                    IsCompliant: false,
                    Violations: new List<ComplianceViolation>(),
                    PDTStatus: new PatternDayTradingStatus("DEFAULT_ACCOUNT", false, 0, 0),
                    MarginStatus: new MarginStatus("DEFAULT_ACCOUNT", 0, 0, 0, false),
                    LastChecked: DateTime.UtcNow
                ));
        }

        public async Task<bool> ValidatePatternDayTradingAsync(string accountId)
        {
            var result = await ExecuteOperationAsync(
                nameof(ValidatePatternDayTradingAsync),
                async () =>
                {
                    var pdtStatus = await GetPatternDayTradingStatusAsync(accountId);

                    if (pdtStatus.IsPatternDayTrader && pdtStatus.DayTradesRemaining <= 0)
                    {
                        var violation = new ComplianceViolation(
                            Type: "PatternDayTrading",
                            Description: "Day trading limit exceeded for PDT account",
                            Severity: ViolationSeverity.Major,
                            Timestamp: DateTime.UtcNow,
                            AccountId: accountId
                        );

                        await RecordViolationAsync(accountId, violation);
                        
                        _logger.LogWarning($"PDT limit exceeded for account {accountId}",
                            new { AccountId = accountId, DayTradesUsed = pdtStatus.DayTradesUsed });

                        return TradingResult<bool>.Success(false);
                    }

                    if (!pdtStatus.IsPatternDayTrader && pdtStatus.DayTradesUsed >= MAX_DAY_TRADES_NON_PDT)
                    {
                        var violation = new ComplianceViolation(
                            Type: "NonPDTLimit",
                            Description: $"Non-PDT account approaching day trade limit ({pdtStatus.DayTradesUsed}/{MAX_DAY_TRADES_NON_PDT})",
                            Severity: ViolationSeverity.Warning,
                            Timestamp: DateTime.UtcNow,
                            AccountId: accountId
                        );

                        await RecordViolationAsync(accountId, violation);

                        return TradingResult<bool>.Success(false);
                    }

                    return TradingResult<bool>.Success(true);
                },
                createDefaultResult: () => false);

            return result;
        }

        public async Task<bool> ValidateMarginRequirementsAsync(string accountId, decimal orderValue)
        {
            var result = await ExecuteOperationAsync(
                nameof(ValidateMarginRequirementsAsync),
                async () =>
                {
                    var marginStatus = await GetMarginStatusAsync(accountId);
                    var requiredMargin = orderValue * DEFAULT_MARGIN_REQUIREMENT;

                    if (marginStatus.AvailableMargin < requiredMargin)
                    {
                        var violation = new ComplianceViolation(
                            Type: "MarginRequirement",
                            Description: $"Insufficient margin. Required: ${requiredMargin:N2}, Available: ${marginStatus.AvailableMargin:N2}",
                            Severity: ViolationSeverity.Major,
                            Timestamp: DateTime.UtcNow,
                            AccountId: accountId
                        );

                        await RecordViolationAsync(accountId, violation);

                        _logger.LogWarning($"Margin requirement not met for account {accountId}",
                            new 
                            { 
                                AccountId = accountId, 
                                Required = requiredMargin, 
                                Available = marginStatus.AvailableMargin,
                                OrderValue = orderValue
                            });

                        return TradingResult<bool>.Success(false);
                    }

                    // Check maintenance margin
                    var newMarginUsed = marginStatus.MarginUsed + requiredMargin;
                    var maintenanceLevel = marginStatus.AccountBalance * MAINTENANCE_MARGIN;

                    if (newMarginUsed > maintenanceLevel)
                    {
                        var violation = new ComplianceViolation(
                            Type: "MaintenanceMargin",
                            Description: "Order would exceed maintenance margin level",
                            Severity: ViolationSeverity.Warning,
                            Timestamp: DateTime.UtcNow,
                            AccountId: accountId
                        );

                        await RecordViolationAsync(accountId, violation);
                    }

                    RecordRiskMetric($"Margin.{accountId}.Available", marginStatus.AvailableMargin);
                    RecordRiskMetric($"Margin.{accountId}.Used", marginStatus.MarginUsed);
                    RecordRiskMetric($"Margin.{accountId}.Required", requiredMargin);

                    return TradingResult<bool>.Success(true);
                },
                createDefaultResult: () => false);

            return result;
        }

        public async Task<bool> ValidateRegulatoryLimitsAsync(OrderRiskRequest request)
        {
            var result = await ExecuteOperationAsync(
                nameof(ValidateRegulatoryLimitsAsync),
                async () =>
                {
                    var violations = new List<ComplianceViolation>();

                    // Check position limits
                    if (request.Quantity > 100000) // Example regulatory limit
                    {
                        violations.Add(new ComplianceViolation(
                            Type: "PositionLimit",
                            Description: $"Order quantity {request.Quantity} exceeds regulatory limit",
                            Severity: ViolationSeverity.Major,
                            Timestamp: DateTime.UtcNow,
                            AccountId: request.AccountId
                        ));
                    }

                    // Check price manipulation rules
                    if (request.OrderType == OrderType.Market && request.Quantity > 10000)
                    {
                        violations.Add(new ComplianceViolation(
                            Type: "MarketManipulation",
                            Description: "Large market order may impact market price",
                            Severity: ViolationSeverity.Warning,
                            Timestamp: DateTime.UtcNow,
                            AccountId: request.AccountId
                        ));
                    }

                    // Check wash trading
                    if (await CheckWashTradingRiskAsync(request))
                    {
                        violations.Add(new ComplianceViolation(
                            Type: "WashTrading",
                            Description: "Potential wash trading detected",
                            Severity: ViolationSeverity.Major,
                            Timestamp: DateTime.UtcNow,
                            AccountId: request.AccountId
                        ));
                    }

                    foreach (var violation in violations)
                    {
                        await RecordViolationAsync(request.AccountId, violation);
                    }

                    var isValid = !violations.Any(v => v.Severity == ViolationSeverity.Major);

                    RecordRiskMetric("RegulatoryChecks.Total", 1);
                    RecordRiskMetric("RegulatoryChecks.Violations", violations.Count);

                    return TradingResult<bool>.Success(isValid);
                },
                createDefaultResult: () => false);

            return result;
        }

        public async Task LogComplianceEventAsync(ComplianceEvent complianceEvent)
        {
            await ExecuteOperationAsync(
                nameof(LogComplianceEventAsync),
                async () =>
                {
                    _complianceEvents.TryAdd(complianceEvent.EventId, complianceEvent);

                    // Publish event
                    await _messageBus.PublishAsync(new ComplianceEventOccurred
                    {
                        EventId = complianceEvent.EventId,
                        EventType = complianceEvent.EventType,
                        AccountId = complianceEvent.AccountId,
                        Description = complianceEvent.Description,
                        Severity = complianceEvent.Severity.ToString(),
                        Timestamp = complianceEvent.Timestamp
                    });

                    _logger.LogInformation($"Compliance event logged: {complianceEvent.EventType}",
                        new 
                        { 
                            EventId = complianceEvent.EventId,
                            EventType = complianceEvent.EventType,
                            Severity = complianceEvent.Severity
                        });

                    RecordRiskMetric($"ComplianceEvents.{complianceEvent.EventType}", 1);

                    return TradingResult<object>.Success(null!);
                },
                createDefaultResult: () => new object());
        }

        protected override async Task<TradingResult<RiskAssessment>> EvaluateRiskCoreAsync(
            ComplianceContext context,
            RiskAssessment assessment,
            CancellationToken cancellationToken)
        {
            try
            {
                var violations = new List<ComplianceViolation>();

                // PDT validation
                if (!await ValidatePatternDayTradingAsync(context.AccountId))
                {
                    violations.Add(new ComplianceViolation(
                        Type: "PDTViolation",
                        Description: "Pattern day trading rule violation",
                        Severity: ViolationSeverity.Major,
                        Timestamp: DateTime.UtcNow,
                        AccountId: context.AccountId
                    ));
                }

                // Margin validation
                if (context.OrderValue > 0 && !await ValidateMarginRequirementsAsync(context.AccountId, context.OrderValue))
                {
                    violations.Add(new ComplianceViolation(
                        Type: "MarginViolation",
                        Description: "Margin requirement not met",
                        Severity: ViolationSeverity.Major,
                        Timestamp: DateTime.UtcNow,
                        AccountId: context.AccountId
                    ));
                }

                // Calculate compliance risk score (0-1, higher is riskier)
                decimal riskScore = violations.Count > 0 ? 1m : 0m;
                
                if (violations.Any(v => v.Severity == ViolationSeverity.Major))
                {
                    riskScore = 1m;
                }
                else if (violations.Any(v => v.Severity == ViolationSeverity.Warning))
                {
                    riskScore = 0.5m;
                }

                assessment.RiskScore = riskScore;
                assessment.IsAcceptable = riskScore < 0.5m;
                assessment.ComplianceIssues = violations.Select(v => v.Description).ToList();
                assessment.Reason = violations.Any() 
                    ? $"Compliance violations: {string.Join(", ", violations.Select(v => v.Type))}"
                    : "All compliance checks passed";

                assessment.Metrics["ViolationCount"] = violations.Count;
                assessment.Metrics["MajorViolations"] = violations.Count(v => v.Severity == ViolationSeverity.Major);

                return TradingResult<RiskAssessment>.Success(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogError("Compliance evaluation failed", ex);
                return TradingResult<RiskAssessment>.Failure($"Compliance evaluation error: {ex.Message}", ex);
            }
        }

        protected override async Task<ComplianceCheckResult> CheckComplianceAsync(
            ComplianceContext context,
            RiskAssessment assessment)
        {
            var result = new ComplianceCheckResult { IsCompliant = true };

            // Additional compliance checks beyond base implementation
            var status = await GetComplianceStatusAsync();
            
            if (!status.IsCompliant)
            {
                result.IsCompliant = false;
                result.Issues = status.Violations.Select(v => v.Description).ToList();
            }

            return result;
        }

        private async Task<PatternDayTradingStatus> GetPatternDayTradingStatusAsync(string accountId)
        {
            return await Task.Run(() =>
            {
                return _pdtStatus.GetOrAdd(accountId, id => new PatternDayTradingStatus(
                    AccountId: id,
                    IsPatternDayTrader: false,
                    DayTradesUsed: 0,
                    DayTradesRemaining: MAX_DAY_TRADES_NON_PDT
                ));
            });
        }

        private async Task<MarginStatus> GetMarginStatusAsync(string accountId)
        {
            return await Task.Run(() =>
            {
                return _marginStatus.GetOrAdd(accountId, id => new MarginStatus(
                    AccountId: id,
                    AccountBalance: 100000m, // Mock value
                    MarginUsed: 20000m,
                    AvailableMargin: 80000m,
                    IsMarginCall: false
                ));
            });
        }

        private async Task<bool> CheckWashTradingRiskAsync(OrderRiskRequest request)
        {
            // Simplified wash trading check - in production would check recent trades
            return await Task.FromResult(false);
        }

        private async Task RecordViolationAsync(string accountId, ComplianceViolation violation)
        {
            var violations = _activeViolations.GetOrAdd(accountId, _ => new List<ComplianceViolation>());
            violations.Add(violation);

            // Keep only recent violations (last 24 hours)
            var cutoff = DateTime.UtcNow.AddDays(-1);
            violations.RemoveAll(v => v.Timestamp < cutoff);

            await LogComplianceEventAsync(new ComplianceEvent(
                EventId: Guid.NewGuid().ToString(),
                EventType: violation.Type,
                AccountId: accountId,
                Description: violation.Description,
                Severity: violation.Severity,
                Timestamp: violation.Timestamp
            ));
        }

        protected override TradingResult ValidateContext(ComplianceContext context)
        {
            var baseValidation = base.ValidateContext(context);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (string.IsNullOrWhiteSpace(context.AccountId))
                return TradingResult.Failure("Account ID is required");

            return TradingResult.Success();
        }
    }

    /// <summary>
    /// Context for compliance checks
    /// </summary>
    public class ComplianceContext
    {
        public string AccountId { get; set; } = "";
        public decimal OrderValue { get; set; }
        public OrderType OrderType { get; set; }
        public string Symbol { get; set; } = "";
        public decimal Quantity { get; set; }
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    }
}