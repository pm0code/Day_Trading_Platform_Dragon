using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;
using System.Diagnostics;

namespace TradingPlatform.RiskManagement.Services;

/// <summary>
/// High-performance compliance monitoring service for regulatory and risk compliance
/// Implements comprehensive pattern day trading, margin, and regulatory limit validation
/// All operations use TradingResult pattern for consistent error handling and observability
/// Maintains real-time compliance status tracking for trading operations
/// </summary>
public class ComplianceMonitor : CanonicalServiceBase, IComplianceMonitor
{
    private readonly IMessageBus _messageBus;
    private readonly ConcurrentDictionary<string, ComplianceEvent> _complianceEvents = new();
    private readonly ConcurrentDictionary<string, PatternDayTradingStatus> _pdtStatus = new();
    
    // Performance tracking
    private long _totalViolations = 0;
    private long _pdtViolations = 0;
    private long _marginViolations = 0;
    private long _regulatoryViolations = 0;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Initializes a new instance of the ComplianceMonitor with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="messageBus">Message bus for compliance event publishing</param>
    /// <param name="logger">Trading logger for comprehensive compliance tracking</param>
    public ComplianceMonitor(IMessageBus messageBus, ITradingLogger logger) : base(logger, "ComplianceMonitor")
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
    }

    /// <summary>
    /// Retrieves comprehensive compliance status including violations, PDT status, and margin requirements
    /// Aggregates all compliance aspects for real-time monitoring and risk assessment
    /// </summary>
    /// <returns>A TradingResult containing the compliance status or error information</returns>
    public async Task<TradingResult<ComplianceStatus>> GetComplianceStatusAsync()
    {
        LogMethodEntry();
        try
        {
            LogInfo("Retrieving comprehensive compliance status");
            
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

            LogInfo($"Compliance status checked - Compliant: {isCompliant}, Violations: {violations.Count()}");

            LogMethodExit();
            return TradingResult<ComplianceStatus>.Success(status);
        }
        catch (Exception ex)
        {
            LogError("Error retrieving compliance status", ex);
            LogMethodExit();
            return TradingResult<ComplianceStatus>.Failure("COMPLIANCE_STATUS_ERROR", 
                $"Failed to retrieve compliance status: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates pattern day trading rules including trade limits and minimum equity requirements
    /// Enforces FINRA Rule 4210 for day trading accounts to ensure regulatory compliance
    /// </summary>
    /// <param name="accountId">The account ID to validate PDT rules for</param>
    /// <returns>A TradingResult containing validation success status or error information</returns>
    public async Task<TradingResult<bool>> ValidatePatternDayTradingAsync(string accountId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(accountId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_ACCOUNT_ID", "Account ID cannot be null or empty");
            }

            LogInfo($"Validating pattern day trading rules for account {accountId}");
            
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
                
                lock (_metricsLock)
                {
                    _pdtViolations++;
                }
                
                LogWarning($"PDT limit exceeded for account {accountId}");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
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
                
                lock (_metricsLock)
                {
                    _pdtViolations++;
                }
                
                LogWarning($"PDT minimum equity not met for account {accountId}: ${pdtStatus.MinimumEquity:N2}");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }

            LogInfo($"PDT validation passed for account {accountId}");
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Error validating PDT for account {accountId}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("PDT_VALIDATION_ERROR", 
                $"Failed to validate PDT rules: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates margin requirements including buying power and margin call status
    /// Ensures account has sufficient margin for new orders and no active margin calls
    /// </summary>
    /// <param name="accountId">The account ID to validate margin requirements for</param>
    /// <param name="orderValue">The total value of the order being validated</param>
    /// <returns>A TradingResult containing validation success status or error information</returns>
    public async Task<TradingResult<bool>> ValidateMarginRequirementsAsync(string accountId, decimal orderValue)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(accountId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_ACCOUNT_ID", "Account ID cannot be null or empty");
            }

            if (orderValue <= 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_ORDER_VALUE", "Order value must be positive");
            }

            LogInfo($"Validating margin requirements for account {accountId}, order value: ${orderValue:N2}");
            
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
                
                lock (_metricsLock)
                {
                    _marginViolations++;
                }
                
                LogWarning($"Active margin call on account {accountId} prevents new orders");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
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
                
                lock (_metricsLock)
                {
                    _marginViolations++;
                }
                
                LogWarning($"Order value ${orderValue:N2} exceeds buying power ${marginStatus.BuyingPower:N2} for account {accountId}");
                LogMethodExit();
                return TradingResult<bool>.Success(false);
            }

            LogInfo($"Margin validation passed for account {accountId}");
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Error validating margin requirements for account {accountId}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("MARGIN_VALIDATION_ERROR", 
                $"Failed to validate margin requirements: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates regulatory limits including order size restrictions and trading hours
    /// Ensures compliance with exchange and regulatory body rules for order submission
    /// </summary>
    /// <param name="request">The order risk request containing order details to validate</param>
    /// <returns>A TradingResult containing validation success status or error information</returns>
    public async Task<TradingResult<bool>> ValidateRegulatoryLimitsAsync(OrderRiskRequest request)
    {
        LogMethodEntry();
        try
        {
            if (request == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_REQUEST", "Order risk request cannot be null");
            }

            LogInfo($"Validating regulatory limits for {request.Symbol}, quantity: {request.Quantity}, price: ${request.Price:N2}");
            
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
                
                LogWarning($"Order size ${orderValue:N2} exceeds regulatory limit for {request.Symbol}");
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
                
                LogWarning($"Order for {request.Symbol} submitted outside market hours");
            }

            // Log any violations
            foreach (var violation in violations)
            {
                await LogComplianceViolationAsync(violation, request.AccountId);
            }
            
            lock (_metricsLock)
            {
                _regulatoryViolations += violations.Count;
            }

            var hasMajorViolations = violations.Any(v => v.Severity >= ViolationSeverity.Major);

            LogInfo($"Regulatory validation for {request.Symbol}: {(!hasMajorViolations ? "PASSED" : "FAILED")} ({violations.Count} violations)");

            LogMethodExit();
            return TradingResult<bool>.Success(!hasMajorViolations);
        }
        catch (Exception ex)
        {
            LogError($"Error validating regulatory limits for {request?.Symbol}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("REGULATORY_VALIDATION_ERROR", 
                $"Failed to validate regulatory limits: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Logs compliance events for audit trail and real-time monitoring
    /// Publishes events to message bus for downstream processing and alerting
    /// </summary>
    /// <param name="complianceEvent">The compliance event to log</param>
    /// <returns>A TradingResult indicating success or failure of the logging operation</returns>
    public async Task<TradingResult<bool>> LogComplianceEventAsync(ComplianceEvent complianceEvent)
    {
        LogMethodEntry();
        try
        {
            if (complianceEvent == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_EVENT", "Compliance event cannot be null");
            }

            LogInfo($"Logging compliance event: {complianceEvent.EventType} for account {complianceEvent.AccountId}");
            
            _complianceEvents.TryAdd(complianceEvent.EventId, complianceEvent);

            await _messageBus.PublishAsync("compliance.event.logged", new AlertEvent
            {
                AlertType = "ComplianceEvent",
                Symbol = complianceEvent.Symbol ?? "N/A",
                Message = $"{complianceEvent.EventType}: {complianceEvent.Description}",
                Severity = "Info",
                RequiresAcknowledgment = false
            });

            LogInfo($"Compliance event logged: {complianceEvent.EventType} for account {complianceEvent.AccountId}");
            
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Error logging compliance event {complianceEvent?.EventId}", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("EVENT_LOGGING_ERROR", 
                $"Failed to log compliance event: {ex.Message}", ex);
        }
    }

    // ========== PRIVATE HELPER METHODS ==========

    /// <summary>
    /// Retrieves active compliance violations from the last 24 hours for monitoring
    /// </summary>
    private IEnumerable<ComplianceViolation> GetActiveViolations()
    {
        LogMethodEntry();
        try
        {
            var recentCutoff = DateTime.UtcNow.AddHours(-24); // Last 24 hours

            var violations = _complianceEvents.Values
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
                .OrderByDescending(v => v.OccurredAt)
                .ToList();
                
            LogDebug($"Found {violations.Count} active violations in the last 24 hours");
            LogMethodExit();
            return violations;
        }
        catch (Exception ex)
        {
            LogError("Error retrieving active violations", ex);
            LogMethodExit();
            return Enumerable.Empty<ComplianceViolation>();
        }
    }

    /// <summary>
    /// Retrieves pattern day trading status for the specified account
    /// </summary>
    private async Task<PatternDayTradingStatus> GetPatternDayTradingStatusAsync(string accountId)
    {
        LogMethodEntry();
        try
        {
            LogDebug($"Retrieving PDT status for account {accountId}");
            
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
                
                LogDebug($"Created default PDT status for account {accountId}");
            }

            var status = _pdtStatus[accountId];
            LogDebug($"PDT status for {accountId}: IsPatternDayTrader={status.IsPatternDayTrader}, DayTradesRemaining={status.DayTradesRemaining}");
            
            LogMethodExit();
            return await Task.FromResult(status);
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving PDT status for account {accountId}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Retrieves margin status for the specified account
    /// </summary>
    private async Task<MarginStatus> GetMarginStatusAsync(string accountId)
    {
        LogMethodEntry();
        try
        {
            LogDebug($"Retrieving margin status for account {accountId}");
            
            // Simplified margin status - in real implementation, this would query account service
            var marginStatus = new MarginStatus(
                MaintenanceMargin: 25000m,
                InitialMargin: 50000m,
                ExcessLiquidity: 100000m,
                BuyingPower: 200000m,
                HasMarginCall: false
            );
            
            LogDebug($"Margin status for {accountId}: BuyingPower=${marginStatus.BuyingPower:N2}, HasMarginCall={marginStatus.HasMarginCall}");
            
            LogMethodExit();
            return await Task.FromResult(marginStatus);
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving margin status for account {accountId}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Logs compliance violations with comprehensive tracking and alerting
    /// </summary>
    private async Task LogComplianceViolationAsync(ComplianceViolation violation, string accountId)
    {
        LogMethodEntry();
        try
        {
            LogWarning($"Logging compliance violation: {violation.RuleId} - {violation.Description} (Severity: {violation.Severity})");
            
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

            var result = await LogComplianceEventAsync(complianceEvent);
            
            if (result.IsSuccess)
            {
                lock (_metricsLock)
                {
                    _totalViolations++;
                }
                
                LogWarning($"Compliance violation logged: {violation.RuleId} - {violation.Description} (Severity: {violation.Severity})");
            }
            else
            {
                LogError($"Failed to log compliance violation: {result.ErrorMessage}");
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error logging compliance violation {violation?.RuleId}", ex);
            LogMethodExit();
        }
    }
    
    // ========== SERVICE HEALTH & METRICS ==========
    
    /// <summary>
    /// Gets comprehensive metrics about the compliance monitoring service
    /// </summary>
    public async Task<TradingResult<ComplianceMetrics>> GetMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            var metrics = new ComplianceMetrics
            {
                TotalViolations = _totalViolations,
                PDTViolations = _pdtViolations,
                MarginViolations = _marginViolations,
                RegulatoryViolations = _regulatoryViolations,
                ActiveViolations = GetActiveViolations().Count(),
                TotalEvents = _complianceEvents.Count,
                Timestamp = DateTime.UtcNow
            };
            
            LogInfo($"Compliance metrics: {metrics.TotalViolations} total violations, {metrics.ActiveViolations} active");
            LogMethodExit();
            return await Task.FromResult(TradingResult<ComplianceMetrics>.Success(metrics));
        }
        catch (Exception ex)
        {
            LogError("Error retrieving compliance metrics", ex);
            LogMethodExit();
            return TradingResult<ComplianceMetrics>.Failure("METRICS_ERROR", 
                $"Failed to retrieve metrics: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Performs health check on the compliance monitoring service
    /// </summary>
    protected override async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        LogMethodEntry();
        try
        {
            // Check message bus connectivity
            var messageBusHealthy = _messageBus != null;
            
            // Check compliance event processing
            var eventsHealthy = _complianceEvents.Count < 100000; // Prevent memory issues
            
            // Check PDT status tracking
            var pdtHealthy = _pdtStatus.Count < 10000; // Prevent memory issues
            
            var isHealthy = messageBusHealthy && eventsHealthy && pdtHealthy;
            
            var details = new Dictionary<string, object>
            {
                ["MessageBus"] = messageBusHealthy ? "Healthy" : "Unhealthy",
                ["EventProcessing"] = eventsHealthy ? "Healthy" : "Unhealthy",
                ["PDTTracking"] = pdtHealthy ? "Healthy" : "Unhealthy",
                ["TotalEvents"] = _complianceEvents.Count,
                ["TotalViolations"] = _totalViolations,
                ["ActiveViolations"] = GetActiveViolations().Count()
            };
            
            LogMethodExit();
            return new HealthCheckResult(isHealthy, "Compliance Monitor", details);
        }
        catch (Exception ex)
        {
            LogError("Error performing health check", ex);
            LogMethodExit();
            return new HealthCheckResult(false, "Compliance Monitor", 
                new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }
}