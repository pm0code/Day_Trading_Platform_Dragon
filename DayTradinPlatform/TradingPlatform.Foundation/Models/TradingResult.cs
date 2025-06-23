using System.Diagnostics.CodeAnalysis;

namespace TradingPlatform.Foundation.Models;

/// <summary>
/// Standardized result wrapper for trading operations.
/// Provides consistent error handling and success/failure patterns across all trading components.
/// Uses financial-precision decimal types for all monetary values.
/// </summary>
/// <typeparam name="T">Type of the result data</typeparam>
public readonly struct TradingResult<T>
{
    private readonly T? _value;
    private readonly TradingError? _error;

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// The successful result value. Only available when IsSuccess is true.
    /// </summary>
    public T? Value => IsSuccess ? _value : default;

    /// <summary>
    /// The error information. Only available when IsSuccess is false.
    /// </summary>
    public TradingError? Error => IsSuccess ? null : _error;

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    private TradingResult(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    /// <summary>
    /// Creates a failed result with an error.
    /// </summary>
    private TradingResult(TradingError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TradingResult<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with an error.
    /// </summary>
    public static TradingResult<T> Failure(TradingError error) => new(error);

    /// <summary>
    /// Creates a failed result with error details.
    /// </summary>
    public static TradingResult<T> Failure(string errorCode, string message, Exception? exception = null, string? correlationId = null)
        => new(new TradingError(errorCode, message, exception, correlationId));

    /// <summary>
    /// Maps the success value to a different type while preserving errors.
    /// </summary>
    public TradingResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        return IsSuccess
            ? TradingResult<TOut>.Success(mapper(Value))
            : TradingResult<TOut>.Failure(Error);
    }

    /// <summary>
    /// Maps the success value to a different result type while preserving errors.
    /// Allows chaining of operations that may fail.
    /// </summary>
    public TradingResult<TOut> Bind<TOut>(Func<T, TradingResult<TOut>> binder)
    {
        return IsSuccess
            ? binder(Value)
            : TradingResult<TOut>.Failure(Error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// Returns the original result for chaining.
    /// </summary>
    public TradingResult<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(Value);
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// Returns the original result for chaining.
    /// </summary>
    public TradingResult<T> OnFailure(Action<TradingError> action)
    {
        if (!IsSuccess)
        {
            action(Error);
        }
        return this;
    }

    /// <summary>
    /// Gets the value or throws an exception if the result is a failure.
    /// </summary>
    public T GetValueOrThrow()
    {
        return IsSuccess ? Value : throw new TradingOperationException(Error);
    }

    /// <summary>
    /// Gets the value or returns the specified default value if the result is a failure.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    /// Implicit conversion from a value to a successful result.
    /// </summary>
    public static implicit operator TradingResult<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from an error to a failed result.
    /// </summary>
    public static implicit operator TradingResult<T>(TradingError error) => Failure(error);

    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {Value}"
            : $"Failure: {Error}";
    }
}

/// <summary>
/// Non-generic trading result for operations that don't return a value.
/// </summary>
public readonly struct TradingResult
{
    private readonly TradingError? _error;

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// The error information. Only available when IsSuccess is false.
    /// </summary>
    public TradingError? Error => IsSuccess ? null : _error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    private TradingResult(bool success)
    {
        IsSuccess = success;
        _error = null;
    }

    /// <summary>
    /// Creates a failed result with an error.
    /// </summary>
    private TradingResult(TradingError error)
    {
        IsSuccess = false;
        _error = error;
    }

    /// <summary>
    /// Represents a successful operation.
    /// </summary>
    public static TradingResult Success() => new(true);

    /// <summary>
    /// Creates a failed result with an error.
    /// </summary>
    public static TradingResult Failure(TradingError error) => new(error);

    /// <summary>
    /// Creates a failed result with error details.
    /// </summary>
    public static TradingResult Failure(string errorCode, string message, Exception? exception = null, string? correlationId = null)
        => new(new TradingError(errorCode, message, exception, correlationId));

    /// <summary>
    /// Maps to a generic result type.
    /// </summary>
    public TradingResult<T> Map<T>(Func<T> valueFactory)
    {
        return IsSuccess
            ? TradingResult<T>.Success(valueFactory())
            : TradingResult<T>.Failure(Error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public TradingResult OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            action();
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public TradingResult OnFailure(Action<TradingError> action)
    {
        if (!IsSuccess)
        {
            action(Error);
        }
        return this;
    }

    /// <summary>
    /// Throws an exception if the result is a failure.
    /// </summary>
    public void ThrowIfFailure()
    {
        if (!IsSuccess)
        {
            throw new TradingOperationException(Error);
        }
    }

    /// <summary>
    /// Implicit conversion from an error to a failed result.
    /// </summary>
    public static implicit operator TradingResult(TradingError error) => Failure(error);

    public override string ToString()
    {
        return IsSuccess ? "Success" : $"Failure: {Error}";
    }
}

/// <summary>
/// Standardized error information for trading operations.
/// Provides detailed context for troubleshooting and monitoring.
/// </summary>
public record TradingError
{
    public string ErrorCode { get; init; }
    public string Message { get; init; }
    public Exception? Exception { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object> Context { get; init; }

    public TradingError(
        string errorCode,
        string message,
        Exception? exception = null,
        string? correlationId = null,
        DateTime? timestamp = null,
        Dictionary<string, object>? context = null)
    {
        ErrorCode = errorCode;
        Message = message;
        Exception = exception;
        CorrelationId = correlationId;
        Timestamp = timestamp ?? DateTime.UtcNow;
        Context = context ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets the full error message including inner exceptions.
    /// </summary>
    public string FullMessage
    {
        get
        {
            if (Exception == null) return Message;

            var messages = new List<string> { Message };
            var ex = Exception;
            while (ex != null)
            {
                messages.Add(ex.Message);
                ex = ex.InnerException;
            }
            return string.Join(" -> ", messages);
        }
    }

    /// <summary>
    /// Common error codes for trading operations.
    /// </summary>
    public static class ErrorCodes
    {
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string MarketDataUnavailable = "MARKET_DATA_UNAVAILABLE";
        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
        public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
        public const string OrderRejected = "ORDER_REJECTED";
        public const string ConnectionFailed = "CONNECTION_FAILED";
        public const string TimeoutExceeded = "TIMEOUT_EXCEEDED";
        public const string ConfigurationError = "CONFIGURATION_ERROR";
        public const string UnauthorizedAccess = "UNAUTHORIZED_ACCESS";
        public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        public const string DataCorrupted = "DATA_CORRUPTED";
        public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
        public const string SystemError = "SYSTEM_ERROR";
        public const string ExternalServiceError = "EXTERNAL_SERVICE_ERROR";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static TradingError Validation(string message, string? correlationId = null, Dictionary<string, object>? context = null)
        => new(ErrorCodes.ValidationFailed, message, null, correlationId, null, context);

    /// <summary>
    /// Creates a rate limit error.
    /// </summary>
    public static TradingError RateLimit(string provider, string? correlationId = null)
        => new(ErrorCodes.RateLimitExceeded, $"Rate limit exceeded for provider: {provider}", null, correlationId);

    /// <summary>
    /// Creates a timeout error.
    /// </summary>
    public static TradingError Timeout(string operation, TimeSpan timeout, string? correlationId = null)
        => new(ErrorCodes.TimeoutExceeded, $"Operation '{operation}' timed out after {timeout}", null, correlationId);

    /// <summary>
    /// Creates a system error from an exception.
    /// </summary>
    public static TradingError System(Exception exception, string? correlationId = null)
        => new(ErrorCodes.SystemError, "An unexpected system error occurred", exception, correlationId);

    public override string ToString()
    {
        var result = $"[{ErrorCode}] {Message}";
        if (!string.IsNullOrEmpty(CorrelationId))
        {
            result += $" (CorrelationId: {CorrelationId})";
        }
        return result;
    }
}

/// <summary>
/// Exception type for trading operation failures.
/// Wraps TradingError information in a throwable exception.
/// </summary>
public class TradingOperationException : Exception
{
    /// <summary>
    /// The trading error that caused this exception.
    /// </summary>
    public TradingError TradingError { get; }

    public TradingOperationException(TradingError tradingError)
        : base(tradingError.FullMessage, tradingError.Exception)
    {
        TradingError = tradingError;
    }

    public TradingOperationException(string errorCode, string message, Exception? innerException = null, string? correlationId = null)
        : this(new TradingError(errorCode, message, innerException, correlationId))
    {
    }
}