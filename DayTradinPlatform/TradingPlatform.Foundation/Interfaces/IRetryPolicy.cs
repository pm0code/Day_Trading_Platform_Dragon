namespace TradingPlatform.Foundation.Interfaces;

/// <summary>
/// Retry policy interface for handling transient failures in trading operations.
/// Provides configurable retry logic with exponential backoff and circuit breaker patterns.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an operation with retry logic for transient failures.
    /// Returns the result of the operation if successful within retry limits.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to execute with retry logic</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>Result of the successful operation</returns>
    /// <exception cref="RetryLimitExceededException">Thrown when all retry attempts are exhausted</exception>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with retry logic without return value.
    /// </summary>
    /// <param name="operation">Operation to execute with retry logic</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <exception cref="RetryLimitExceededException">Thrown when all retry attempts are exhausted</exception>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with custom retry predicate.
    /// Allows fine-grained control over which exceptions should trigger retries.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to execute with retry logic</param>
    /// <param name="shouldRetry">Predicate to determine if an exception should trigger a retry</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>Result of the successful operation</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Exception, int, bool> shouldRetry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current retry policy configuration.
    /// </summary>
    RetryPolicyConfiguration Configuration { get; }

    /// <summary>
    /// Gets retry statistics for monitoring and optimization.
    /// </summary>
    RetryPolicyStatistics Statistics { get; }

    /// <summary>
    /// Event raised when a retry attempt is made.
    /// Useful for monitoring and alerting on retry patterns.
    /// </summary>
    event EventHandler<RetryAttemptEventArgs> RetryAttempt;

    /// <summary>
    /// Event raised when retry policy gives up after exhausting all attempts.
    /// Critical for monitoring operation reliability.
    /// </summary>
    event EventHandler<RetryExhaustedEventArgs> RetryExhausted;
}

/// <summary>
/// Circuit breaker interface for preventing cascade failures in trading systems.
/// Monitors failure rates and automatically breaks circuits when thresholds are exceeded.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// May throw CircuitBreakerOpenException if circuit is open.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation if circuit allows execution</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open</exception>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation through the circuit breaker without return value.
    /// </summary>
    /// <param name="operation">Operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open</exception>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Current state of the circuit breaker.
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// Gets circuit breaker configuration.
    /// </summary>
    CircuitBreakerConfiguration Configuration { get; }

    /// <summary>
    /// Gets circuit breaker metrics and statistics.
    /// </summary>
    CircuitBreakerMetrics Metrics { get; }

    /// <summary>
    /// Manually opens the circuit breaker.
    /// Useful for maintenance or emergency situations.
    /// </summary>
    void Open();

    /// <summary>
    /// Manually closes the circuit breaker.
    /// Should be used carefully and only when underlying issues are resolved.
    /// </summary>
    void Close();

    /// <summary>
    /// Resets circuit breaker statistics.
    /// Useful for clearing historical data after resolving issues.
    /// </summary>
    void Reset();

    /// <summary>
    /// Event raised when circuit breaker state changes.
    /// Critical for monitoring system health and automated responses.
    /// </summary>
    event EventHandler<CircuitBreakerStateChangedEventArgs> StateChanged;
}

/// <summary>
/// Resilience policy that combines retry and circuit breaker patterns.
/// Provides comprehensive failure handling for critical trading operations.
/// </summary>
public interface IResiliencePolicy
{
    /// <summary>
    /// Executes an operation with combined retry and circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the successful operation</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// The retry policy component.
    /// </summary>
    IRetryPolicy RetryPolicy { get; }

    /// <summary>
    /// The circuit breaker component.
    /// </summary>
    ICircuitBreaker CircuitBreaker { get; }

    /// <summary>
    /// Timeout policy for operation execution.
    /// </summary>
    TimeSpan OperationTimeout { get; }
}

/// <summary>
/// Configuration for retry policy behavior.
/// </summary>
public record RetryPolicyConfiguration(
    int MaxRetryAttempts,
    TimeSpan InitialDelay,
    TimeSpan MaxDelay,
    double BackoffMultiplier,
    bool UseJitter,
    IReadOnlyList<Type> RetriableExceptions)
{
    /// <summary>
    /// Default retry policy for trading operations.
    /// Conservative settings appropriate for financial operations.
    /// </summary>
    public static RetryPolicyConfiguration TradingDefault => new(
        MaxRetryAttempts: 3,
        InitialDelay: TimeSpan.FromMilliseconds(100),
        MaxDelay: TimeSpan.FromSeconds(5),
        BackoffMultiplier: 2.0,
        UseJitter: true,
        RetriableExceptions: new[]
        {
            typeof(TimeoutException),
            typeof(TaskCanceledException),
            typeof(HttpRequestException)
        });

    /// <summary>
    /// Aggressive retry policy for critical market data operations.
    /// Faster retries with more attempts for time-sensitive operations.
    /// </summary>
    public static RetryPolicyConfiguration MarketDataDefault => new(
        MaxRetryAttempts: 5,
        InitialDelay: TimeSpan.FromMilliseconds(50),
        MaxDelay: TimeSpan.FromSeconds(2),
        BackoffMultiplier: 1.5,
        UseJitter: true,
        RetriableExceptions: new[]
        {
            typeof(TimeoutException),
            typeof(TaskCanceledException),
            typeof(HttpRequestException)
        });
}

/// <summary>
/// Configuration for circuit breaker behavior.
/// </summary>
public record CircuitBreakerConfiguration(
    int FailureThreshold,
    int SuccessThreshold,
    TimeSpan SamplingDuration,
    TimeSpan OpenTimeout,
    int MinimumThroughput)
{
    /// <summary>
    /// Default circuit breaker configuration for trading services.
    /// </summary>
    public static CircuitBreakerConfiguration TradingDefault => new(
        FailureThreshold: 5,
        SuccessThreshold: 3,
        SamplingDuration: TimeSpan.FromMinutes(1),
        OpenTimeout: TimeSpan.FromSeconds(30),
        MinimumThroughput: 10);

    /// <summary>
    /// Sensitive circuit breaker configuration for critical operations.
    /// Lower thresholds and faster recovery for mission-critical components.
    /// </summary>
    public static CircuitBreakerConfiguration CriticalDefault => new(
        FailureThreshold: 3,
        SuccessThreshold: 2,
        SamplingDuration: TimeSpan.FromSeconds(30),
        OpenTimeout: TimeSpan.FromSeconds(15),
        MinimumThroughput: 5);
}

/// <summary>
/// Statistics for retry policy monitoring.
/// </summary>
public record RetryPolicyStatistics(
    long TotalExecutions,
    long SuccessfulExecutions,
    long FailedExecutions,
    long TotalRetryAttempts,
    decimal AverageRetriesPerExecution,
    decimal SuccessRatePercent,
    TimeSpan AverageExecutionTime,
    Dictionary<Type, int> ExceptionCounts)
{
    /// <summary>
    /// Failure rate as a percentage.
    /// </summary>
    public decimal FailureRatePercent => 100m - SuccessRatePercent;
}

/// <summary>
/// Current state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed and operations are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open and operations are blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open and testing if operations should resume.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Metrics for circuit breaker monitoring.
/// </summary>
public record CircuitBreakerMetrics(
    CircuitBreakerState CurrentState,
    TimeSpan TimeInCurrentState,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    decimal FailureRatePercent,
    DateTime LastFailureTime,
    DateTime LastSuccessTime,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses)
{
    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public decimal SuccessRatePercent => 100m - FailureRatePercent;
}

/// <summary>
/// Event arguments for retry attempt notifications.
/// </summary>
public class RetryAttemptEventArgs : EventArgs
{
    public int AttemptNumber { get; }
    public Exception Exception { get; }
    public TimeSpan Delay { get; }
    public DateTime AttemptTime { get; }

    public RetryAttemptEventArgs(int attemptNumber, Exception exception, TimeSpan delay)
    {
        AttemptNumber = attemptNumber;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Delay = delay;
        AttemptTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for retry exhausted notifications.
/// </summary>
public class RetryExhaustedEventArgs : EventArgs
{
    public int TotalAttempts { get; }
    public Exception LastException { get; }
    public TimeSpan TotalDuration { get; }
    public DateTime ExhaustedTime { get; }

    public RetryExhaustedEventArgs(int totalAttempts, Exception lastException, TimeSpan totalDuration)
    {
        TotalAttempts = totalAttempts;
        LastException = lastException ?? throw new ArgumentNullException(nameof(lastException));
        TotalDuration = totalDuration;
        ExhaustedTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for circuit breaker state change notifications.
/// </summary>
public class CircuitBreakerStateChangedEventArgs : EventArgs
{
    public CircuitBreakerState PreviousState { get; }
    public CircuitBreakerState NewState { get; }
    public string Reason { get; }
    public DateTime ChangeTime { get; }

    public CircuitBreakerStateChangedEventArgs(CircuitBreakerState previousState, CircuitBreakerState newState, string reason)
    {
        PreviousState = previousState;
        NewState = newState;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        ChangeTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when retry policy exhausts all attempts.
/// </summary>
public class RetryLimitExceededException : Exception
{
    public int AttemptCount { get; }
    public TimeSpan TotalDuration { get; }

    public RetryLimitExceededException(string message, int attemptCount, TimeSpan totalDuration, Exception innerException)
        : base(message, innerException)
    {
        AttemptCount = attemptCount;
        TotalDuration = totalDuration;
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public TimeSpan TimeUntilRetry { get; }

    public CircuitBreakerOpenException(string message, TimeSpan timeUntilRetry)
        : base(message)
    {
        TimeUntilRetry = timeUntilRetry;
    }
}