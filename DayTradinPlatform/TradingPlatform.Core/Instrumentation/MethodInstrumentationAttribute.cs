// TradingPlatform.Core.Instrumentation.MethodInstrumentationAttribute
// Comprehensive method instrumentation for automated logging across entire platform
// Supports configurable logging levels, parameter capture, performance tracking
// Zero-overhead when disabled, comprehensive regulatory compliance when enabled

using System.Runtime.CompilerServices;

namespace TradingPlatform.Core.Instrumentation;

/// <summary>
/// Comprehensive method instrumentation attribute for automated logging
/// Provides configurable entry/exit logging, parameter capture, performance tracking
/// Integrates with Enhanced TradingLogOrchestrator for complete observability
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class MethodInstrumentationAttribute : Attribute
{
    /// <summary>
    /// Instrumentation level determining what gets logged
    /// </summary>
    public InstrumentationLevel Level { get; set; } = InstrumentationLevel.Standard;
    
    /// <summary>
    /// Whether to log method parameters (respects privacy filters)
    /// </summary>
    public bool LogParameters { get; set; } = true;
    
    /// <summary>
    /// Whether to log return values (respects privacy filters)
    /// </summary>
    public bool LogReturnValue { get; set; } = false;
    
    /// <summary>
    /// Whether to track performance metrics (execution time, memory usage)
    /// </summary>
    public bool TrackPerformance { get; set; } = true;
    
    /// <summary>
    /// Whether this method is trading-critical (requires regulatory compliance logging)
    /// </summary>
    public bool IsTradingCritical { get; set; } = false;
    
    /// <summary>
    /// Whether this method handles sensitive data (applies privacy filters)
    /// </summary>
    public bool HasSensitiveData { get; set; } = false;
    
    /// <summary>
    /// Custom operation name for logging (defaults to method name)
    /// </summary>
    public string? OperationName { get; set; }
    
    /// <summary>
    /// Expected maximum execution time in microseconds (for performance alerts)
    /// </summary>
    public long ExpectedMaxExecutionMicroseconds { get; set; } = 1000; // 1ms default
    
    /// <summary>
    /// Whether to suppress logging for this method (emergency override)
    /// </summary>
    public bool SuppressLogging { get; set; } = false;
}

/// <summary>
/// Instrumentation levels for configurable logging granularity
/// </summary>
public enum InstrumentationLevel
{
    /// <summary>
    /// No instrumentation (emergency override)
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Minimal logging - errors and critical events only
    /// </summary>
    Minimal = 1,
    
    /// <summary>
    /// Standard logging - entry/exit with basic context
    /// </summary>
    Standard = 2,
    
    /// <summary>
    /// Detailed logging - includes parameters and performance metrics
    /// </summary>
    Detailed = 3,
    
    /// <summary>
    /// Comprehensive logging - full regulatory compliance with all context
    /// </summary>
    Comprehensive = 4,
    
    /// <summary>
    /// Debug logging - everything including internal state changes
    /// </summary>
    Debug = 5
}

/// <summary>
/// Trading operation category for specialized instrumentation
/// </summary>
public enum TradingOperationCategory
{
    None,
    OrderManagement,
    RiskManagement,
    PositionTracking,
    MarketData,
    Performance,
    Compliance,
    DataPipeline,
    SystemHealth
}

/// <summary>
/// Enhanced instrumentation attribute for trading-specific operations
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TradingOperationAttribute : MethodInstrumentationAttribute
{
    /// <summary>
    /// Trading operation category for specialized logging
    /// </summary>
    public TradingOperationCategory Category { get; set; } = TradingOperationCategory.None;
    
    /// <summary>
    /// Whether this operation affects positions (requires audit trail)
    /// </summary>
    public bool AffectsPositions { get; set; } = false;
    
    /// <summary>
    /// Whether this operation involves risk calculations
    /// </summary>
    public bool InvolvesRisk { get; set; } = false;
    
    /// <summary>
    /// Whether this operation requires immediate compliance reporting
    /// </summary>
    public bool RequiresComplianceReporting { get; set; } = false;
    
    /// <summary>
    /// Business impact level for prioritizing alerts
    /// </summary>
    public BusinessImpactLevel BusinessImpact { get; set; } = BusinessImpactLevel.Medium;
    
    public TradingOperationAttribute()
    {
        // Trading operations default to more comprehensive logging
        Level = InstrumentationLevel.Detailed;
        IsTradingCritical = true;
        TrackPerformance = true;
        ExpectedMaxExecutionMicroseconds = 100; // 100μs for trading ops
    }
}

/// <summary>
/// Business impact levels for alert prioritization
/// </summary>
public enum BusinessImpactLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Performance-critical operation attribute for ultra-low latency requirements
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PerformanceCriticalAttribute : MethodInstrumentationAttribute
{
    /// <summary>
    /// Target execution time in microseconds
    /// </summary>
    public long TargetExecutionMicroseconds { get; set; } = 50; // 50μs default
    
    /// <summary>
    /// Whether to use high-precision timing (nanosecond accuracy)
    /// </summary>
    public bool UseHighPrecisionTiming { get; set; } = true;
    
    /// <summary>
    /// Whether to track CPU and memory metrics
    /// </summary>
    public bool TrackResourceUsage { get; set; } = true;
    
    public PerformanceCriticalAttribute()
    {
        // Performance-critical operations need minimal overhead
        Level = InstrumentationLevel.Standard;
        LogParameters = false; // Minimize overhead
        LogReturnValue = false;
        TrackPerformance = true;
        ExpectedMaxExecutionMicroseconds = 50;
    }
}

/// <summary>
/// Audit trail attribute for regulatory compliance operations
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditTrailAttribute : MethodInstrumentationAttribute
{
    /// <summary>
    /// Regulatory requirement this audit satisfies
    /// </summary>
    public string? RegulatoryRequirement { get; set; }
    
    /// <summary>
    /// Retention period for audit logs in days
    /// </summary>
    public int RetentionDays { get; set; } = 2555; // 7 years default
    
    /// <summary>
    /// Whether this audit trail requires digital signature
    /// </summary>
    public bool RequiresDigitalSignature { get; set; } = false;
    
    /// <summary>
    /// Compliance category for specialized reporting
    /// </summary>
    public ComplianceCategory ComplianceCategory { get; set; } = ComplianceCategory.Trading;
    
    public AuditTrailAttribute()
    {
        // Audit operations require comprehensive logging
        Level = InstrumentationLevel.Comprehensive;
        IsTradingCritical = true;
        LogParameters = true;
        LogReturnValue = true;
        TrackPerformance = true;
        HasSensitiveData = true; // Assume sensitive until proven otherwise
    }
}

/// <summary>
/// Compliance categories for regulatory reporting
/// </summary>
public enum ComplianceCategory
{
    Trading,
    RiskManagement,
    OrderManagement,
    PositionReporting,
    MarketData,
    SystemAccess,
    DataProtection
}