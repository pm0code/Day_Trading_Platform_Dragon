namespace MarketAnalyzer.Foundation;

/// <summary>
/// Represents an error that occurred during a trading operation.
/// Provides structured error information with error codes, messages, and optional exception details.
/// </summary>
public class TradingError
{
    /// <summary>
    /// Gets the error code. Should be in SCREAMING_SNAKE_CASE format (e.g., "MARKET_DATA_UNAVAILABLE").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional exception that caused this error.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the timestamp when this error occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets additional context data for the error.
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TradingError"/> class.
    /// </summary>
    /// <param name="code">The error code (should be SCREAMING_SNAKE_CASE)</param>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception that caused the error</param>
    public TradingError(string code, string message, Exception? exception = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
        Timestamp = DateTime.UtcNow;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Adds context data to the error.
    /// </summary>
    /// <param name="key">The context key</param>
    /// <param name="value">The context value</param>
    /// <returns>The current TradingError for method chaining</returns>
    public TradingError WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    /// <returns>A string representation</returns>
    public override string ToString()
    {
        var contextStr = Context.Count > 0 
            ? $" Context: {string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"
            : string.Empty;

        return $"[{Code}] {Message}{contextStr}";
    }

    /// <summary>
    /// Returns a detailed string representation including exception details.
    /// </summary>
    /// <returns>A detailed string representation</returns>
    public string ToDetailedString()
    {
        var details = ToString();
        
        if (Exception != null)
        {
            details += $"\nException: {Exception.GetType().Name}: {Exception.Message}";
            if (Exception.StackTrace != null)
                details += $"\nStackTrace: {Exception.StackTrace}";
        }
        
        return details;
    }

    /// <summary>
    /// Common error codes for trading operations.
    /// </summary>
    public static class ErrorCodes
    {
        // Market Data Errors
        public const string MARKET_DATA_UNAVAILABLE = "MARKET_DATA_UNAVAILABLE";
        public const string MARKET_DATA_STALE = "MARKET_DATA_STALE";
        public const string MARKET_DATA_INVALID = "MARKET_DATA_INVALID";
        public const string API_RATE_LIMIT_EXCEEDED = "API_RATE_LIMIT_EXCEEDED";
        public const string API_QUOTA_EXCEEDED = "API_QUOTA_EXCEEDED";
        public const string API_AUTHENTICATION_FAILED = "API_AUTHENTICATION_FAILED";
        
        // Calculation Errors
        public const string CALCULATION_ERROR = "CALCULATION_ERROR";
        public const string INVALID_PARAMETERS = "INVALID_PARAMETERS";
        public const string INSUFFICIENT_DATA = "INSUFFICIENT_DATA";
        public const string DIVISION_BY_ZERO = "DIVISION_BY_ZERO";
        
        // Service Errors
        public const string SERVICE_UNAVAILABLE = "SERVICE_UNAVAILABLE";
        public const string SERVICE_TIMEOUT = "SERVICE_TIMEOUT";
        public const string INITIALIZATION_FAILED = "INITIALIZATION_FAILED";
        public const string CONFIGURATION_ERROR = "CONFIGURATION_ERROR";
        
        // Storage Errors
        public const string STORAGE_ERROR = "STORAGE_ERROR";
        public const string DATA_NOT_FOUND = "DATA_NOT_FOUND";
        public const string CACHE_MISS = "CACHE_MISS";
        public const string SERIALIZATION_ERROR = "SERIALIZATION_ERROR";
        
        // Validation Errors
        public const string VALIDATION_FAILED = "VALIDATION_FAILED";
        public const string INVALID_SYMBOL = "INVALID_SYMBOL";
        public const string INVALID_DATE_RANGE = "INVALID_DATE_RANGE";
        public const string INVALID_AMOUNT = "INVALID_AMOUNT";
        
        // AI/ML Errors
        public const string MODEL_INFERENCE_FAILED = "MODEL_INFERENCE_FAILED";
        public const string MODEL_NOT_LOADED = "MODEL_NOT_LOADED";
        public const string GPU_UNAVAILABLE = "GPU_UNAVAILABLE";
        public const string PREDICTION_CONFIDENCE_LOW = "PREDICTION_CONFIDENCE_LOW";
        
        // Generic Errors
        public const string UNKNOWN_ERROR = "UNKNOWN_ERROR";
        public const string OPERATION_CANCELLED = "OPERATION_CANCELLED";
        public const string TIMEOUT = "TIMEOUT";
        public const string RESOURCE_EXHAUSTED = "RESOURCE_EXHAUSTED";
    }
}