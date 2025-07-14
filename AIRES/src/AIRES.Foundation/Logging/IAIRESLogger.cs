namespace AIRES.Foundation.Logging;

/// <summary>
/// AIRES-specific logger interface providing comprehensive logging capabilities
/// including structured logging, metrics, events, and correlation tracking.
/// 
/// This is AIRES's own logging abstraction - completely independent from any other system.
/// </summary>
public interface IAIRESLogger
{
    #region Core Logging Methods
    
    /// <summary>
    /// Logs a trace-level message (most detailed).
    /// </summary>
    void LogTrace(string message, params object[] args);
    
    /// <summary>
    /// Logs a debug-level message.
    /// </summary>
    void LogDebug(string message, params object[] args);
    
    /// <summary>
    /// Logs an information-level message.
    /// </summary>
    void LogInfo(string message, params object[] args);
    
    /// <summary>
    /// Logs a warning-level message.
    /// </summary>
    void LogWarning(string message, params object[] args);
    
    /// <summary>
    /// Logs an error-level message with optional exception.
    /// </summary>
    void LogError(string message, Exception? ex = null, params object[] args);
    
    /// <summary>
    /// Logs a fatal-level message with optional exception.
    /// </summary>
    void LogFatal(string message, Exception? ex = null, params object[] args);
    
    #endregion
    
    #region Metrics and Instrumentation
    
    /// <summary>
    /// Logs a metric value with optional tags.
    /// </summary>
    void LogMetric(string metricName, double value, Dictionary<string, string>? tags = null);
    
    /// <summary>
    /// Logs an event with optional properties.
    /// </summary>
    void LogEvent(string eventName, Dictionary<string, object>? properties = null);
    
    /// <summary>
    /// Logs operation duration with optional tags.
    /// </summary>
    void LogDuration(string operationName, TimeSpan duration, Dictionary<string, string>? tags = null);
    
    #endregion
    
    #region Correlation and Context
    
    /// <summary>
    /// Begins a new logging scope with optional properties.
    /// </summary>
    IDisposable BeginScope(string scopeName, Dictionary<string, object>? properties = null);
    
    /// <summary>
    /// Sets the correlation ID for tracking related operations.
    /// </summary>
    void SetCorrelationId(string correlationId);
    
    /// <summary>
    /// Gets the current correlation ID.
    /// </summary>
    string GetCorrelationId();
    
    #endregion
    
    #region Status and Health
    
    /// <summary>
    /// Logs a health check result.
    /// </summary>
    void LogHealthCheck(string componentName, bool isHealthy, string? details = null);
    
    /// <summary>
    /// Logs status information.
    /// </summary>
    void LogStatus(string statusName, string statusValue, Dictionary<string, object>? additionalInfo = null);
    
    #endregion
}