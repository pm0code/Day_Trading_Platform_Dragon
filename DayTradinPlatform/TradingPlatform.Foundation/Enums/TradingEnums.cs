namespace TradingPlatform.Foundation.Enums;

/// <summary>
/// Overall system health status for trading platform components.
/// Used for health checks, monitoring, and automated decision making.
/// </summary>
public enum TradingStatus
{
    /// <summary>
    /// System is fully operational and ready for trading.
    /// </summary>
    Operational = 0,

    /// <summary>
    /// System is starting up and not yet ready for trading.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// System is operational but with reduced functionality.
    /// Some features may be disabled or operating with degraded performance.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// System is experiencing critical issues affecting trading operations.
    /// Trading activities should be halted until issues are resolved.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// System is shutting down gracefully.
    /// New operations should be rejected, existing operations completed.
    /// </summary>
    ShuttingDown = 4,

    /// <summary>
    /// System is offline and not available for trading.
    /// </summary>
    Offline = 5,

    /// <summary>
    /// System is in maintenance mode.
    /// Trading is temporarily suspended for updates or repairs.
    /// </summary>
    Maintenance = 6
}

/// <summary>
/// Health status for individual service components.
/// Provides granular health information for monitoring and diagnostics.
/// </summary>
public enum ServiceHealth
{
    /// <summary>
    /// Service is healthy and operating within normal parameters.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Service is operational but showing warning signs.
    /// Performance may be impacted but functionality is preserved.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Service is experiencing issues but still partially functional.
    /// Some features may be unavailable or operating slowly.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Service is unhealthy and may not be functioning properly.
    /// Critical operations may fail or produce unreliable results.
    /// </summary>
    Unhealthy = 3,

    /// <summary>
    /// Service is completely unavailable or non-responsive.
    /// All operations will fail until service is restored.
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Service health status is unknown or cannot be determined.
    /// May indicate monitoring issues or service startup/shutdown.
    /// </summary>
    Unknown = 5
}

/// <summary>
/// Alert severity levels for trading system notifications.
/// Used for prioritizing alerts and determining response urgency.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert providing status updates or notifications.
    /// No immediate action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning about potential issues that should be monitored.
    /// May require preventive action to avoid problems.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error condition that affects system functionality.
    /// Requires attention but trading can continue with limitations.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical issue that severely impacts trading operations.
    /// Immediate action required to prevent trading disruption.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// Emergency situation requiring immediate intervention.
    /// Trading should be halted until issue is resolved.
    /// </summary>
    Emergency = 4
}

/// <summary>
/// Market session states for trading hours management.
/// Critical for determining when trading operations are allowed.
/// </summary>
public enum MarketSession
{
    /// <summary>
    /// Markets are closed and trading is not allowed.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Pre-market trading session with limited liquidity.
    /// Extended hours trading for qualified participants.
    /// </summary>
    PreMarket = 1,

    /// <summary>
    /// Regular market hours with full trading functionality.
    /// Primary trading session with maximum liquidity.
    /// </summary>
    Open = 2,

    /// <summary>
    /// After-hours trading session with limited liquidity.
    /// Extended hours trading for qualified participants.
    /// </summary>
    AfterHours = 3,

    /// <summary>
    /// Market is temporarily halted due to volatility or news.
    /// Trading is suspended until normal operations resume.
    /// </summary>
    Halted = 4,

    /// <summary>
    /// Market session state is unknown or cannot be determined.
    /// May indicate connectivity issues or system problems.
    /// </summary>
    Unknown = 5
}

/// <summary>
/// Data quality levels for market data validation.
/// Used to indicate reliability and freshness of market data.
/// </summary>
public enum DataQuality
{
    /// <summary>
    /// High-quality, real-time data suitable for all trading decisions.
    /// </summary>
    Excellent = 0,

    /// <summary>
    /// Good quality data with minor delays or occasional gaps.
    /// Suitable for most trading strategies with some limitations.
    /// </summary>
    Good = 1,

    /// <summary>
    /// Fair quality data with noticeable delays or data gaps.
    /// May be suitable for less time-sensitive strategies.
    /// </summary>
    Fair = 2,

    /// <summary>
    /// Poor quality data with significant delays or reliability issues.
    /// Should be used with caution and may require verification.
    /// </summary>
    Poor = 3,

    /// <summary>
    /// Data quality is unacceptable for trading decisions.
    /// Should not be used for any trading operations.
    /// </summary>
    Unacceptable = 4,

    /// <summary>
    /// Data quality cannot be determined or validated.
    /// Use with extreme caution and consider alternative sources.
    /// </summary>
    Unknown = 5
}

/// <summary>
/// Execution environment types for configuration and behavior adaptation.
/// </summary>
public enum ExecutionEnvironment
{
    /// <summary>
    /// Development environment for coding and initial testing.
    /// Debugging features enabled, external connections may be mocked.
    /// </summary>
    Development = 0,

    /// <summary>
    /// Testing environment for automated testing and validation.
    /// May use test data and isolated systems.
    /// </summary>
    Testing = 1,

    /// <summary>
    /// Staging environment that mirrors production configuration.
    /// Used for final validation before production deployment.
    /// </summary>
    Staging = 2,

    /// <summary>
    /// Production environment for live trading operations.
    /// Highest security and reliability requirements.
    /// </summary>
    Production = 3,

    /// <summary>
    /// Disaster recovery environment for business continuity.
    /// Standby systems ready to take over from production.
    /// </summary>
    DisasterRecovery = 4
}

/// <summary>
/// Performance tier classifications for latency-sensitive operations.
/// Used to apply appropriate performance optimizations and monitoring.
/// </summary>
public enum PerformanceTier
{
    /// <summary>
    /// Standard performance requirements for non-critical operations.
    /// Response times measured in seconds.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Enhanced performance for important but not time-critical operations.
    /// Response times measured in hundreds of milliseconds.
    /// </summary>
    Enhanced = 1,

    /// <summary>
    /// High performance for time-sensitive trading operations.
    /// Response times measured in tens of milliseconds.
    /// </summary>
    High = 2,

    /// <summary>
    /// Ultra-high performance for critical trading paths.
    /// Response times measured in milliseconds or sub-milliseconds.
    /// </summary>
    UltraHigh = 3,

    /// <summary>
    /// Maximum performance with all optimizations enabled.
    /// Response times measured in microseconds.
    /// </summary>
    Maximum = 4
}

/// <summary>
/// Security classification levels for data and operations.
/// Determines access controls and protection measures.
/// </summary>
public enum SecurityLevel
{
    /// <summary>
    /// Public information with no security restrictions.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Internal information for company use only.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Confidential information requiring access controls.
    /// </summary>
    Confidential = 2,

    /// <summary>
    /// Restricted information for authorized personnel only.
    /// </summary>
    Restricted = 3,

    /// <summary>
    /// Top secret information with highest security requirements.
    /// </summary>
    TopSecret = 4
}

/// <summary>
/// Compliance requirement levels for regulatory adherence.
/// </summary>
public enum ComplianceLevel
{
    /// <summary>
    /// No specific compliance requirements.
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic compliance with standard business practices.
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Standard financial services compliance requirements.
    /// </summary>
    Standard = 2,

    /// <summary>
    /// Enhanced compliance for regulated trading activities.
    /// </summary>
    Enhanced = 3,

    /// <summary>
    /// Strict compliance for institutional trading and reporting.
    /// </summary>
    Strict = 4,

    /// <summary>
    /// Maximum compliance for market makers and large institutions.
    /// </summary>
    Maximum = 5
}

/// <summary>
/// Cache expiration strategies for different types of trading data.
/// </summary>
public enum CacheStrategy
{
    /// <summary>
    /// No caching - always fetch fresh data.
    /// </summary>
    NoCache = 0,

    /// <summary>
    /// Short-term caching for rapidly changing data.
    /// Suitable for real-time market data.
    /// </summary>
    ShortTerm = 1,

    /// <summary>
    /// Medium-term caching for moderately stable data.
    /// Suitable for company information and fundamentals.
    /// </summary>
    MediumTerm = 2,

    /// <summary>
    /// Long-term caching for stable reference data.
    /// Suitable for static configuration and historical data.
    /// </summary>
    LongTerm = 3,

    /// <summary>
    /// Persistent caching that survives application restarts.
    /// Suitable for large datasets and computed results.
    /// </summary>
    Persistent = 4,

    /// <summary>
    /// Adaptive caching with dynamic expiration based on usage patterns.
    /// </summary>
    Adaptive = 5
}