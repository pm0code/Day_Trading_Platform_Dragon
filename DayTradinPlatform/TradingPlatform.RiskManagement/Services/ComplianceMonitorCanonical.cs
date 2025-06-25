// File: TradingPlatform.RiskManagement.Services\ComplianceMonitorCanonical.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Foundation.Models;
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
            try
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

                LogInfo($"Compliance status: {(isCompliant ? "Compliant" : "Non-Compliant")} with {allViolations.Count} violations",
                    new { IsCompliant = isCompliant, ViolationCount = allViolations.Count });

                return status;
            }
            catch (Exception ex)
            {
                LogError("Failed to get compliance status", ex);
                return new ComplianceStatus(
                    IsCompliant: false,
                    Violations: new List<ComplianceViolation>(),
                    PDTStatus: new PatternDayTradingStatus(false, 0, 0, 0, DateTime.UtcNow),
                    MarginStatus: new MarginStatus(0, 0, 0, 0, false),
                    LastChecked: DateTime.UtcNow
                );
            }
        }

        public async Task<bool> ValidatePatternDayTradingAsync(string accountId)
        {
            try
            {
                var pdtStatus = await GetPatternDayTradingStatusAsync(accountId);

                if (pdtStatus.IsPatternDayTrader && pdtStatus.DayTradesRemaining <= 0)
                {
                    var violation = new ComplianceViolation(
                        "PDT001",
                        "Day trading limit exceeded for PDT account",
                        "",
                        0m,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
                    );

                    await RecordViolationAsync(accountId, violation);
                    
                    LogWarning($"PDT limit exceeded for account {accountId}",
                        additionalData: new { AccountId = accountId, DayTradesUsed = pdtStatus.DayTradesUsed });

                    return false;
                }

                if (!pdtStatus.IsPatternDayTrader && pdtStatus.DayTradesUsed >= MAX_DAY_TRADES_NON_PDT)
                {
                    var violation = new ComplianceViolation(
                        "PDT002",
                        $"Non-PDT account approaching day trade limit ({pdtStatus.DayTradesUsed}/{MAX_DAY_TRADES_NON_PDT})",
                        "",
                        0m,
                        ViolationSeverity.Warning,
                        DateTime.UtcNow
                    );

                    await RecordViolationAsync(accountId, violation);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to validate PDT for account {accountId}", ex);
                return false;
            }
        }

        public async Task<bool> ValidateMarginRequirementsAsync(string accountId, decimal orderValue)
        {
            try
            {
                var marginStatus = await GetMarginStatusAsync(accountId);
                var requiredMargin = orderValue * DEFAULT_MARGIN_REQUIREMENT;
                var availableMargin = marginStatus.ExcessLiquidity;

                if (availableMargin < requiredMargin)
                {
                    var violation = new ComplianceViolation(
                        "MARGIN001",
                        $"Insufficient margin. Required: ${requiredMargin:N2}, Available: ${availableMargin:N2}",
                        "",
                        orderValue,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
                    );

                    await RecordViolationAsync(accountId, violation);

                    LogWarning($"Margin requirement not met for account {accountId}",
                        additionalData: new 
                        { 
                            AccountId = accountId, 
                            Required = requiredMargin, 
                            Available = availableMargin,
                            OrderValue = orderValue
                        });

                    return false;
                }

                // Check maintenance margin
                var accountBalance = marginStatus.BuyingPower / 4m; // Approximate account balance from buying power
                var maintenanceLevel = accountBalance * MAINTENANCE_MARGIN;

                if (marginStatus.MaintenanceMargin + requiredMargin > maintenanceLevel)
                {
                    var violation = new ComplianceViolation(
                        "MARGIN002",
                        "Order would exceed maintenance margin level",
                        "",
                        orderValue,
                        ViolationSeverity.Warning,
                        DateTime.UtcNow
                    );

                    await RecordViolationAsync(accountId, violation);
                }

                RecordRiskMetric($"Margin.{accountId}.Available", availableMargin);
                RecordRiskMetric($"Margin.{accountId}.Maintenance", marginStatus.MaintenanceMargin);
                RecordRiskMetric($"Margin.{accountId}.Required", requiredMargin);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to validate margin requirements for account {accountId}", ex);
                return false;
            }
        }

        public async Task<bool> ValidateRegulatoryLimitsAsync(OrderRiskRequest request)
        {
            try
            {
                var violations = new List<ComplianceViolation>();

                // Check position limits
                if (request.Quantity > 100000) // Example regulatory limit
                {
                    violations.Add(new ComplianceViolation(
                        "REG001",
                        $"Order quantity {request.Quantity} exceeds regulatory limit",
                        request.Symbol,
                        request.Quantity * request.Price,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
                    ));
                }

                // Check price manipulation rules
                if (request.OrderType == OrderType.Market && request.Quantity > 10000)
                {
                    violations.Add(new ComplianceViolation(
                        "REG002",
                        "Large market order may impact market price",
                        request.Symbol,
                        request.Quantity * request.Price,
                        ViolationSeverity.Warning,
                        DateTime.UtcNow
                    ));
                }

                // Check wash trading
                if (await CheckWashTradingRiskAsync(request))
                {
                    violations.Add(new ComplianceViolation(
                        "REG003",
                        "Potential wash trading detected",
                        request.Symbol,
                        request.Quantity * request.Price,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
                    ));
                }

                foreach (var violation in violations)
                {
                    await RecordViolationAsync(request.AccountId, violation);
                }

                var isValid = !violations.Any(v => v.Severity == ViolationSeverity.Major);

                RecordRiskMetric("RegulatoryChecks.Total", 1);
                RecordRiskMetric("RegulatoryChecks.Violations", violations.Count);

                return isValid;
            }
            catch (Exception ex)
            {
                LogError($"Failed to validate regulatory limits for {request.Symbol}", ex);
                return false;
            }
        }

        public async Task LogComplianceEventAsync(ComplianceEvent complianceEvent)
        {
            try
            {
                _complianceEvents.TryAdd(complianceEvent.EventId, complianceEvent);

                // Publish event as RiskEvent
                await _messageBus.PublishAsync("compliance-events", new RiskEvent
                {
                    EventId = complianceEvent.EventId,
                    Source = "ComplianceMonitor",
                    CorrelationId = CorrelationId,
                    RiskType = "Compliance",
                    Symbol = complianceEvent.Symbol ?? "",
                    CurrentExposure = complianceEvent.Amount ?? 0m,
                    RiskLimit = 0m,
                    UtilizationPercent = 0m,
                    LimitBreached = complianceEvent.Metadata.ContainsKey("Severity") && 
                                   complianceEvent.Metadata["Severity"].ToString() == "Major",
                    Action = complianceEvent.Description
                });

                LogInfo($"Compliance event logged: {complianceEvent.EventType}",
                    new 
                    { 
                        EventId = complianceEvent.EventId,
                        EventType = complianceEvent.EventType,
                        AccountId = complianceEvent.AccountId
                    });

                RecordRiskMetric($"ComplianceEvents.{complianceEvent.EventType}", 1);
            }
            catch (Exception ex)
            {
                LogError($"Failed to log compliance event {complianceEvent.EventId}", ex);
            }
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
                        "PDT003",
                        "Pattern day trading rule violation",
                        context.Symbol,
                        context.OrderValue,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
                    ));
                }

                // Margin validation
                if (context.OrderValue > 0 && !await ValidateMarginRequirementsAsync(context.AccountId, context.OrderValue))
                {
                    violations.Add(new ComplianceViolation(
                        "MARGIN003",
                        "Margin requirement not met",
                        context.Symbol,
                        context.OrderValue,
                        ViolationSeverity.Major,
                        DateTime.UtcNow
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
                    ? $"Compliance violations: {string.Join(", ", violations.Select(v => v.RuleId))}"
                    : "All compliance checks passed";

                assessment.Metrics["ViolationCount"] = violations.Count;
                assessment.Metrics["MajorViolations"] = violations.Count(v => v.Severity == ViolationSeverity.Major);

                return TradingResult<RiskAssessment>.Success(assessment);
            }
            catch (Exception ex)
            {
                LogError("Compliance evaluation failed", ex);
                return TradingResult<RiskAssessment>.Failure(new TradingError("COMPLIANCE_ERROR", $"Compliance evaluation error: {ex.Message}", ex));
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
                    false,
                    0,
                    MAX_DAY_TRADES_NON_PDT,
                    MIN_ACCOUNT_BALANCE_PDT,
                    DateTime.UtcNow.Date
                ));
            });
        }

        private async Task<MarginStatus> GetMarginStatusAsync(string accountId)
        {
            return await Task.Run(() =>
            {
                return _marginStatus.GetOrAdd(accountId, id => new MarginStatus(
                    20000m,  // MaintenanceMargin
                    25000m,  // InitialMargin
                    80000m,  // ExcessLiquidity
                    320000m, // BuyingPower (4x excess liquidity for day trading)
                    false    // HasMarginCall
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
            violations.RemoveAll(v => v.OccurredAt < cutoff);

            await LogComplianceEventAsync(new ComplianceEvent(
                Guid.NewGuid().ToString(),
                violation.RuleId,
                violation.Description,
                accountId,
                violation.Symbol,
                violation.Amount,
                violation.OccurredAt,
                new Dictionary<string, object> { ["Severity"] = violation.Severity }
            ));
        }

        protected override TradingResult ValidateContext(ComplianceContext context)
        {
            var baseValidation = base.ValidateContext(context);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (string.IsNullOrWhiteSpace(context.AccountId))
                return TradingResult.Failure(new TradingError("INVALID_INPUT", "Account ID is required"));

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