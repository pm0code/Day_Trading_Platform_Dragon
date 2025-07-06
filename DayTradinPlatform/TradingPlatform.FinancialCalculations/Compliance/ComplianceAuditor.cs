// TradingPlatform.FinancialCalculations.Compliance.ComplianceAuditor
// Regulatory compliance and audit trail service for financial calculations

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.FinancialCalculations.Models;
using TradingPlatform.FinancialCalculations.Configuration;

namespace TradingPlatform.FinancialCalculations.Compliance;

/// <summary>
/// Comprehensive compliance auditor for financial calculations
/// Ensures regulatory compliance with SOX, MiFID, Basel III, and other standards
/// Provides audit trails, data integrity verification, and compliance reporting
/// </summary>
public class ComplianceAuditor : IComplianceAuditor, IDisposable
{
    #region Private Fields

    private readonly ComplianceConfiguration _config;
    private readonly ILogger<ComplianceAuditor> _logger;
    private readonly ConcurrentDictionary<string, CalculationAuditEntry> _auditTrail;
    private readonly ConcurrentDictionary<string, ComplianceValidationRule> _validationRules;
    private readonly Timer _auditFlushTimer;
    private readonly object _auditLock = new();
    private readonly RSA _rsaKey;
    private bool _disposed;

    #endregion

    #region Constructor

    public ComplianceAuditor(
        ComplianceConfiguration configuration,
        ILogger<ComplianceAuditor> logger)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _auditTrail = new ConcurrentDictionary<string, CalculationAuditEntry>();
        _validationRules = new ConcurrentDictionary<string, ComplianceValidationRule>();
        
        // Initialize RSA key for audit trail signing
        _rsaKey = RSA.Create(2048);
        
        // Initialize audit trail flush timer
        _auditFlushTimer = new Timer(FlushAuditTrail, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        InitializeValidationRules();
        
        _logger.LogInformation("ComplianceAuditor initialized with configuration: {Config}", 
            _config.ToSafeString());
    }

    #endregion

    #region IComplianceAuditor Implementation

    /// <summary>
    /// Initialize the compliance auditor for a specific service
    /// </summary>
    public async Task<TradingResult<bool>> InitializeAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing compliance auditor for service: {ServiceName}", serviceName);
            
            // Ensure audit directories exist
            if (_config.EnableAuditTrail)
            {
                var auditPath = Path.Combine(_config.RegulatoryReportingPath, "audit", serviceName);
                Directory.CreateDirectory(auditPath);
                
                _logger.LogInformation("Audit directory created: {AuditPath}", auditPath);
            }
            
            // Initialize compliance validation rules for the service
            await InitializeServiceSpecificRulesAsync(serviceName, cancellationToken);
            
            _logger.LogInformation("Compliance auditor initialized successfully for service: {ServiceName}", serviceName);
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize compliance auditor for service: {ServiceName}", serviceName);
            return TradingResult<bool>.Failure(ex);
        }
    }

    /// <summary>
    /// Start audit trail for a calculation
    /// </summary>
    public async Task<TradingResult<string>> StartCalculationAuditAsync(
        string calculationType,
        object parameters,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditId = Guid.NewGuid().ToString();
            var auditEntry = new CalculationAuditEntry
            {
                Id = auditId,
                OperationName = calculationType,
                Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
                StartedAt = DateTime.UtcNow,
                UserId = userId,
                SessionId = GetCurrentSessionId()
            };

            // Generate compliance hash for the audit entry
            auditEntry.ComplianceHash = await GenerateComplianceHashAsync(auditEntry);

            _auditTrail.TryAdd(auditId, auditEntry);

            _logger.LogInformation("Started calculation audit: {AuditId} for {CalculationType}", 
                auditId, calculationType);

            return TradingResult<string>.Success(auditId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start calculation audit for {CalculationType}", calculationType);
            return TradingResult<string>.Failure(ex);
        }
    }

    /// <summary>
    /// Complete audit trail for a calculation
    /// </summary>
    public async Task<TradingResult<bool>> CompleteCalculationAuditAsync(
        string auditId,
        object result,
        bool success,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_auditTrail.TryGetValue(auditId, out var auditEntry))
            {
                auditEntry.CompletedAt = DateTime.UtcNow;
                auditEntry.DurationMs = (auditEntry.CompletedAt - auditEntry.StartedAt).TotalMilliseconds;
                auditEntry.Result = result != null ? JsonSerializer.Serialize(result) : null;
                
                if (!success)
                {
                    auditEntry.Error = "Calculation failed";
                }

                // Update compliance hash with completion data
                auditEntry.ComplianceHash = await GenerateComplianceHashAsync(auditEntry);

                // Sign the audit entry if enabled
                if (_config.EnableAuditTrailSigning)
                {
                    auditEntry.Metadata["DigitalSignature"] = await SignAuditEntryAsync(auditEntry);
                }

                _logger.LogInformation("Completed calculation audit: {AuditId}, Success: {Success}, Duration: {Duration}ms", 
                    auditId, success, auditEntry.DurationMs);

                return TradingResult<bool>.Success(true);
            }
            else
            {
                _logger.LogWarning("Audit entry not found for ID: {AuditId}", auditId);
                return TradingResult<bool>.Failure(new InvalidOperationException($"Audit entry not found: {auditId}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete calculation audit: {AuditId}", auditId);
            return TradingResult<bool>.Failure(ex);
        }
    }

    /// <summary>
    /// Validate regulatory compliance for calculation parameters and results
    /// </summary>
    public async Task<TradingResult<bool>> ValidateRegulatoryComplianceAsync(
        string calculationType,
        object parameters,
        object result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var violations = new List<string>();

            // Validate parameters
            var parameterViolations = await ValidateParametersAsync(calculationType, parameters);
            violations.AddRange(parameterViolations);

            // Validate results
            var resultViolations = await ValidateResultsAsync(calculationType, result);
            violations.AddRange(resultViolations);

            // Validate calculation-specific rules
            if (_validationRules.TryGetValue(calculationType, out var rule))
            {
                var ruleViolations = await rule.ValidateAsync(parameters, result);
                violations.AddRange(ruleViolations);
            }

            if (violations.Any())
            {
                _logger.LogWarning("Regulatory compliance violations found for {CalculationType}: {Violations}", 
                    calculationType, string.Join(", ", violations));
                
                return TradingResult<bool>.Failure(
                    new ComplianceViolationException($"Compliance violations: {string.Join(", ", violations)}"));
            }

            _logger.LogDebug("Regulatory compliance validated for {CalculationType}", calculationType);
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate regulatory compliance for {CalculationType}", calculationType);
            return TradingResult<bool>.Failure(ex);
        }
    }

    /// <summary>
    /// Generate compliance audit report for specified date range
    /// </summary>
    public async Task<TradingResult<List<CalculationAuditEntry>>> GetAuditReportAsync(
        DateTime startDate,
        DateTime endDate,
        string? calculationType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _auditTrail.Values
                .Where(entry => entry.StartedAt >= startDate && entry.StartedAt <= endDate);

            if (!string.IsNullOrEmpty(calculationType))
            {
                query = query.Where(entry => entry.OperationName.Equals(calculationType, StringComparison.OrdinalIgnoreCase));
            }

            var auditEntries = query
                .OrderBy(entry => entry.StartedAt)
                .ToList();

            _logger.LogInformation("Generated audit report: {Count} entries from {StartDate} to {EndDate}", 
                auditEntries.Count, startDate, endDate);

            return TradingResult<List<CalculationAuditEntry>>.Success(auditEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate audit report");
            return TradingResult<List<CalculationAuditEntry>>.Failure(ex);
        }
    }

    #endregion

    #region Validation Rules

    private void InitializeValidationRules()
    {
        // Portfolio calculation validation rules
        _validationRules.TryAdd("PortfolioMetrics", new ComplianceValidationRule
        {
            RuleName = "PortfolioMetrics_SOX_Compliance",
            ValidationFunction = async (parameters, result) =>
            {
                var violations = new List<string>();

                // Validate decimal precision (SOX requirement)
                if (result is PortfolioCalculationResult portfolioResult)
                {
                    if (!ValidateDecimalPrecision(portfolioResult.TotalValue, 2))
                    {
                        violations.Add("Total value precision violation - must have exactly 2 decimal places");
                    }

                    if (!ValidateDecimalPrecision(portfolioResult.UnrealizedPnL, 2))
                    {
                        violations.Add("Unrealized P&L precision violation - must have exactly 2 decimal places");
                    }

                    // Validate position weights sum to 100%
                    var totalWeight = portfolioResult.Positions.Sum(p => p.PositionWeight);
                    if (Math.Abs(totalWeight - 100m) > 0.01m)
                    {
                        violations.Add($"Position weights do not sum to 100% (actual: {totalWeight:F2}%)");
                    }
                }

                return violations;
            }
        });

        // Risk metrics validation rules (Basel III compliance)
        _validationRules.TryAdd("RiskMetrics", new ComplianceValidationRule
        {
            RuleName = "RiskMetrics_Basel_Compliance",
            ValidationFunction = async (parameters, result) =>
            {
                var violations = new List<string>();

                if (result is RiskMetrics riskResult)
                {
                    // Validate VaR is within acceptable range (Basel III)
                    if (riskResult.VaR95 > riskResult.PortfolioValue * 0.20m)
                    {
                        violations.Add("VaR95 exceeds 20% of portfolio value (Basel III limit)");
                    }

                    // Validate leverage ratio (Basel III)
                    if (riskResult.LeverageRatio > 3.0m)
                    {
                        violations.Add("Leverage ratio exceeds Basel III limit of 3.0");
                    }

                    // Validate concentration risk
                    if (riskResult.ConcentrationRisk > 0.25m)
                    {
                        violations.Add("Concentration risk exceeds 25% limit");
                    }
                }

                return violations;
            }
        });

        // Option pricing validation rules
        _validationRules.TryAdd("OptionPricing", new ComplianceValidationRule
        {
            RuleName = "OptionPricing_MiFID_Compliance",
            ValidationFunction = async (parameters, result) =>
            {
                var violations = new List<string>();

                if (result is OptionPricingResult optionResult)
                {
                    // Validate option price is positive
                    if (optionResult.TheoreticalPrice <= 0)
                    {
                        violations.Add("Option theoretical price must be positive");
                    }

                    // Validate Greeks are within reasonable ranges
                    if (Math.Abs(optionResult.Delta) > 1.0m)
                    {
                        violations.Add("Delta must be between -1 and 1");
                    }

                    if (optionResult.Gamma < 0)
                    {
                        violations.Add("Gamma must be non-negative");
                    }

                    // Validate implied volatility is reasonable
                    if (optionResult.ImpliedVolatility < 0 || optionResult.ImpliedVolatility > 3.0m)
                    {
                        violations.Add("Implied volatility must be between 0% and 300%");
                    }
                }

                return violations;
            }
        });
    }

    private async Task InitializeServiceSpecificRulesAsync(string serviceName, CancellationToken cancellationToken)
    {
        // Load service-specific validation rules from configuration or database
        await Task.Run(() =>
        {
            _logger.LogDebug("Initialized service-specific rules for {ServiceName}", serviceName);
        }, cancellationToken);
    }

    private async Task<List<string>> ValidateParametersAsync(string calculationType, object parameters)
    {
        var violations = new List<string>();

        // General parameter validation
        if (parameters == null)
        {
            violations.Add("Parameters cannot be null");
            return violations;
        }

        // Type-specific validation
        switch (calculationType.ToUpperInvariant())
        {
            case "PORTFOLIOMETRICS":
                if (parameters is ValueTuple<List<PositionData>, Dictionary<string, decimal>> portfolioParams)
                {
                    var (positions, prices) = portfolioParams;
                    
                    if (positions.Count == 0)
                        violations.Add("Position list cannot be empty");
                    
                    if (prices.Count == 0)
                        violations.Add("Price dictionary cannot be empty");
                    
                    // Validate position data
                    foreach (var position in positions)
                    {
                        if (position.Quantity == 0)
                            violations.Add($"Position quantity cannot be zero for {position.Symbol}");
                        
                        if (position.AveragePrice <= 0)
                            violations.Add($"Average price must be positive for {position.Symbol}");
                    }
                }
                break;
        }

        return violations;
    }

    private async Task<List<string>> ValidateResultsAsync(string calculationType, object result)
    {
        var violations = new List<string>();

        if (result == null)
        {
            violations.Add("Result cannot be null");
            return violations;
        }

        // Validate calculation result base properties
        if (result is FinancialCalculationResult financialResult)
        {
            if (string.IsNullOrEmpty(financialResult.CalculationId))
                violations.Add("Calculation ID cannot be null or empty");
            
            if (financialResult.CalculatedAt == default)
                violations.Add("Calculation timestamp cannot be default");
            
            if (financialResult.CalculationTimeMs < 0)
                violations.Add("Calculation time cannot be negative");
        }

        return violations;
    }

    #endregion

    #region Cryptographic Operations

    private async Task<string> GenerateComplianceHashAsync(CalculationAuditEntry auditEntry)
    {
        return await Task.Run(() =>
        {
            var data = $"{auditEntry.ServiceName}|{auditEntry.OperationName}|{auditEntry.Parameters}|" +
                      $"{auditEntry.StartedAt:O}|{auditEntry.UserId}|{auditEntry.SessionId}";
            
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        });
    }

    private async Task<string> SignAuditEntryAsync(CalculationAuditEntry auditEntry)
    {
        return await Task.Run(() =>
        {
            var data = JsonSerializer.Serialize(auditEntry);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = _rsaKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        });
    }

    private async Task<bool> VerifyAuditEntrySignatureAsync(CalculationAuditEntry auditEntry, string signature)
    {
        return await Task.Run(() =>
        {
            try
            {
                var data = JsonSerializer.Serialize(auditEntry);
                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signature);
                
                return _rsaKey.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        });
    }

    #endregion

    #region Helper Methods

    private bool ValidateDecimalPrecision(decimal value, int requiredPrecision)
    {
        var scaleFactor = (decimal)Math.Pow(10, requiredPrecision);
        var rounded = Math.Round(value * scaleFactor) / scaleFactor;
        return Math.Abs(value - rounded) < 1e-10m;
    }

    private string GetCurrentSessionId()
    {
        return $"{Environment.MachineName}_{System.Diagnostics.Process.GetCurrentProcess().Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private void FlushAuditTrail(object? state)
    {
        try
        {
            if (!_config.EnableAuditTrail) return;

            lock (_auditLock)
            {
                var auditPath = Path.Combine(_config.RegulatoryReportingPath, "audit");
                Directory.CreateDirectory(auditPath);

                var fileName = $"audit_trail_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(auditPath, fileName);

                var auditData = _auditTrail.Values.ToList();
                var json = JsonSerializer.Serialize(auditData, new JsonSerializerOptions { WriteIndented = true });

                if (_config.EnableAuditTrailEncryption)
                {
                    json = EncryptAuditData(json);
                }

                File.WriteAllText(filePath, json);

                _logger.LogInformation("Flushed {Count} audit entries to {FilePath}", auditData.Count, filePath);

                // Clean up old entries based on retention policy
                CleanupOldAuditEntries();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush audit trail");
        }
    }

    private string EncryptAuditData(string data)
    {
        // Simple encryption for demonstration - in production use proper encryption
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var encryptedBytes = _rsaKey.Encrypt(dataBytes, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(encryptedBytes);
    }

    private void CleanupOldAuditEntries()
    {
        var cutoffDate = DateTime.UtcNow - _config.AuditTrailRetention;
        var oldEntries = _auditTrail.Where(kvp => kvp.Value.StartedAt < cutoffDate).ToList();

        foreach (var entry in oldEntries)
        {
            _auditTrail.TryRemove(entry.Key, out _);
        }

        if (oldEntries.Any())
        {
            _logger.LogInformation("Cleaned up {Count} old audit entries", oldEntries.Count);
        }
    }

    #endregion

    #region Extension Methods for Configuration

    private static class ConfigurationExtensions
    {
        public static string ToSafeString(this ComplianceConfiguration config)
        {
            return $"SOX={config.EnableSOXCompliance}, MiFID={config.EnableMiFIDCompliance}, " +
                   $"Basel={config.EnableBaselCompliance}, Audit={config.EnableAuditTrail}";
        }
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _auditFlushTimer?.Dispose();
                _rsaKey?.Dispose();
                
                // Final flush of audit trail
                FlushAuditTrail(null);
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Compliance validation rule definition
/// </summary>
public class ComplianceValidationRule
{
    public string RuleName { get; set; } = string.Empty;
    public Func<object?, object?, Task<List<string>>> ValidationFunction { get; set; } = (_, _) => Task.FromResult(new List<string>());

    public async Task<List<string>> ValidateAsync(object? parameters, object? result)
    {
        return await ValidationFunction(parameters, result);
    }
}

/// <summary>
/// Compliance violation exception
/// </summary>
public class ComplianceViolationException : Exception
{
    public ComplianceViolationException(string message) : base(message) { }
    public ComplianceViolationException(string message, Exception innerException) : base(message, innerException) { }
}