namespace AIRES.Foundation.Results;

/// <summary>
/// AIRES-specific result pattern for all operations.
/// Ensures consistent error handling and result propagation throughout the system.
/// </summary>
public class AIRESResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
    public string TraceId { get; }
    public DateTime Timestamp { get; }
    
    private AIRESResult(bool isSuccess, T? value, string? errorCode, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Exception = exception;
        TraceId = Guid.NewGuid().ToString();
        Timestamp = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static AIRESResult<T> Success(T value)
    {
        return new AIRESResult<T>(true, value, null, null, null);
    }
    
    /// <summary>
    /// Creates a failure result with error details.
    /// </summary>
    public static AIRESResult<T> Failure(string errorCode, string errorMessage, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));
            
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));
            
        return new AIRESResult<T>(false, default, errorCode, errorMessage, exception);
    }
    
    /// <summary>
    /// Maps the result value to a different type if successful.
    /// </summary>
    public AIRESResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (mapper == null)
            throw new ArgumentNullException(nameof(mapper));
            
        return IsSuccess 
            ? AIRESResult<TNew>.Success(mapper(Value!))
            : AIRESResult<TNew>.Failure(ErrorCode!, ErrorMessage!, Exception);
    }
    
    /// <summary>
    /// Chains another operation if this result is successful.
    /// </summary>
    public async Task<AIRESResult<TNew>> BindAsync<TNew>(Func<T, Task<AIRESResult<TNew>>> next)
    {
        if (next == null)
            throw new ArgumentNullException(nameof(next));
            
        return IsSuccess 
            ? await next(Value!)
            : AIRESResult<TNew>.Failure(ErrorCode!, ErrorMessage!, Exception);
    }
    
    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public AIRESResult<T> OnSuccess(Action<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
            
        if (IsSuccess)
            action(Value!);
            
        return this;
    }
    
    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public AIRESResult<T> OnFailure(Action<string, string, Exception?> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
            
        if (!IsSuccess)
            action(ErrorCode!, ErrorMessage!, Exception);
            
        return this;
    }
    
    /// <summary>
    /// Gets the value or throws an exception if the result is a failure.
    /// </summary>
    public T GetValueOrThrow()
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException($"Cannot get value from failed result. Error: {ErrorCode} - {ErrorMessage}",
                Exception);
        }
        
        return Value!;
    }
    
    /// <summary>
    /// Gets the value or a default if the result is a failure.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }
    
    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {Value}"
            : $"Failure: [{ErrorCode}] {ErrorMessage}";
    }
}

/// <summary>
/// Non-generic AIRESResult for operations that don't return a value.
/// </summary>
public class AIRESResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
    public string TraceId { get; }
    public DateTime Timestamp { get; }
    
    private AIRESResult(bool isSuccess, string? errorCode, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Exception = exception;
        TraceId = Guid.NewGuid().ToString();
        Timestamp = DateTime.UtcNow;
    }
    
    public static AIRESResult Success()
    {
        return new AIRESResult(true, null, null, null);
    }
    
    public static AIRESResult Failure(string errorCode, string errorMessage, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));
            
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));
            
        return new AIRESResult(false, errorCode, errorMessage, exception);
    }
}