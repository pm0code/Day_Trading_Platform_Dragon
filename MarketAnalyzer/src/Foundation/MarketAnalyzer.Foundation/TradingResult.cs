using System.Runtime.CompilerServices;

namespace MarketAnalyzer.Foundation;

/// <summary>
/// Result pattern for all trading operations. Encapsulates success/failure state with optional value and error information.
/// </summary>
/// <typeparam name="T">The type of the successful result value</typeparam>
public class TradingResult<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the successful result value. Only valid when IsSuccess is true.
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Gets the error information. Only valid when IsFailure is true.
    /// </summary>
    public TradingError? Error { get; private set; }

    private TradingResult(bool isSuccess, T? value, TradingError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The successful result value</param>
    /// <returns>A successful TradingResult</returns>
    public static TradingResult<T> Success(T value)
    {
        return new TradingResult<T>(true, value, null);
    }

    /// <summary>
    /// Creates a failed result with the specified error code and message.
    /// </summary>
    /// <param name="errorCode">The error code (should be SCREAMING_SNAKE_CASE)</param>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception that caused the failure</param>
    /// <returns>A failed TradingResult</returns>
    public static TradingResult<T> Failure(string errorCode, string message, Exception? exception = null)
    {
        var error = new TradingError(errorCode, message, exception);
        return new TradingResult<T>(false, default, error);
    }

    /// <summary>
    /// Creates a failed result with the specified TradingError.
    /// </summary>
    /// <param name="error">The trading error</param>
    /// <returns>A failed TradingResult</returns>
    public static TradingResult<T> Failure(TradingError error)
    {
        return new TradingResult<T>(false, default, error);
    }

    /// <summary>
    /// Implicitly converts a value to a successful TradingResult.
    /// </summary>
    /// <param name="value">The value to convert</param>
    public static implicit operator TradingResult<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Explicitly converts a value to a successful TradingResult (alternate for implicit operator).
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>A successful TradingResult</returns>
    public static TradingResult<T> FromT(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Maps the result value to a new type if successful, otherwise returns the failure.
    /// </summary>
    /// <typeparam name="TResult">The target type</typeparam>
    /// <param name="mapper">The mapping function</param>
    /// <returns>A new TradingResult with the mapped value or the original failure</returns>
    public TradingResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        if (IsFailure)
            return TradingResult<TResult>.Failure(Error!);

        try
        {
            var mappedValue = mapper(Value!);
            return TradingResult<TResult>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            return TradingResult<TResult>.Failure("MAPPING_ERROR", $"Failed to map result: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>The current TradingResult</returns>
    public TradingResult<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(Value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>The current TradingResult</returns>
    public TradingResult<T> OnFailure(Action<TradingError> action)
    {
        if (IsFailure)
            action(Error!);
        return this;
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string representation</returns>
    public override string ToString()
    {
        return IsSuccess 
            ? $"Success: {Value}" 
            : $"Failure: {Error}";
    }
}

/// <summary>
/// Non-generic version of TradingResult for operations that don't return a value.
/// </summary>
public class TradingResult
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error information. Only valid when IsFailure is true.
    /// </summary>
    public TradingError? Error { get; private set; }

    private TradingResult(bool isSuccess, TradingError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful TradingResult</returns>
    public static TradingResult Success()
    {
        return new TradingResult(true, null);
    }

    /// <summary>
    /// Creates a failed result with the specified error code and message.
    /// </summary>
    /// <param name="errorCode">The error code (should be SCREAMING_SNAKE_CASE)</param>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception that caused the failure</param>
    /// <returns>A failed TradingResult</returns>
    public static TradingResult Failure(string errorCode, string message, Exception? exception = null)
    {
        var error = new TradingError(errorCode, message, exception);
        return new TradingResult(false, error);
    }

    /// <summary>
    /// Creates a failed result with the specified TradingError.
    /// </summary>
    /// <param name="error">The trading error</param>
    /// <returns>A failed TradingResult</returns>
    public static TradingResult Failure(TradingError error)
    {
        return new TradingResult(false, error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>The current TradingResult</returns>
    public TradingResult OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>The current TradingResult</returns>
    public TradingResult OnFailure(Action<TradingError> action)
    {
        if (IsFailure)
            action(Error!);
        return this;
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string representation</returns>
    public override string ToString()
    {
        return IsSuccess 
            ? "Success" 
            : $"Failure: {Error}";
    }
}