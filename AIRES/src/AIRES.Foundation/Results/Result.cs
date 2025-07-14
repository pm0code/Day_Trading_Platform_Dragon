namespace AIRES.Foundation.Results;

/// <summary>
/// Result pattern for explicit success/failure handling as recommended by Gemini AI.
/// Differentiates between unexpected system errors (exceptions) and expected business failures.
/// </summary>
public class Result<T>
{
    /// <summary>
    /// Gets the value if the result is successful.
    /// </summary>
    public T? Value { get; }
    
    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }
    
    /// <summary>
    /// Gets the error code for specific error identification.
    /// </summary>
    public string? ErrorCode { get; }
    
    /// <summary>
    /// Gets the timestamp when this result was created.
    /// </summary>
    public DateTime Timestamp { get; }

    protected Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Timestamp = DateTime.UtcNow;
    }

    protected Result(string errorMessage, string? errorCode = null)
    {
        IsSuccess = false;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new Result<T>(value);
    
    /// <summary>
    /// Creates a failure result with an error message and optional error code.
    /// </summary>
    public static Result<T> Failure(string errorMessage, string? errorCode = null) => new Result<T>(errorMessage, errorCode);

    /// <summary>
    /// Maps the value of a successful result to a new type.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess 
            ? Result<TNew>.Success(mapper(Value!)) 
            : Result<TNew>.Failure(ErrorMessage!, ErrorCode);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(Value!);
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string, string?> action)
    {
        if (!IsSuccess)
        {
            action(ErrorMessage!, ErrorCode);
        }
        return this;
    }
}