using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace MarketAnalyzer.Foundation;

/// <summary>
/// Base class for all services in MarketAnalyzer. Provides canonical patterns for logging, 
/// lifecycle management, metrics, and error handling.
/// </summary>
public abstract class CanonicalServiceBase : IDisposable
{
    /// <summary>
    /// Gets the logger instance for this service.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service name for logging and metrics.
    /// </summary>
    protected string ServiceName { get; }

    /// <summary>
    /// Gets the current service health status.
    /// </summary>
    public ServiceHealth Health { get; private set; } = ServiceHealth.Unknown;

    /// <summary>
    /// Gets a value indicating whether the service has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the service start time.
    /// </summary>
    public DateTime StartTime { get; private set; }

    /// <summary>
    /// Gets the service metrics.
    /// </summary>
    protected Dictionary<string, object> Metrics { get; } = new();

    private readonly object _metricsLock = new();
    private readonly CancellationTokenSource _disposalTokenSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CanonicalServiceBase"/> class.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="serviceName">Optional service name (defaults to class name)</param>
    protected CanonicalServiceBase(ILogger logger, string? serviceName = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ServiceName = serviceName ?? GetType().Name;
        
        LogMethodEntry();
        LogInfo($"Initializing {ServiceName}");
        LogMethodExit();
    }

    #region Abstract Methods

    /// <summary>
    /// Initializes the service. Called once during service startup.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    protected abstract Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the service. Called after initialization.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    protected abstract Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the service. Called during shutdown.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    protected abstract Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken);

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    public async Task<TradingResult<bool>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            Health = ServiceHealth.Initializing;
            UpdateMetric("InitializationStarted", DateTime.UtcNow);
            
            var result = await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                Health = ServiceHealth.Initialized;
                LogInfo($"{ServiceName} initialized successfully");
                UpdateMetric("InitializationCompleted", DateTime.UtcNow);
            }
            else
            {
                Health = ServiceHealth.Failed;
                LogError($"{ServiceName} initialization failed", null, result.Error?.Message);
                UpdateMetric("InitializationFailed", DateTime.UtcNow);
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            Health = ServiceHealth.Failed;
            LogError($"{ServiceName} initialization threw exception", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("INITIALIZATION_EXCEPTION", $"Service initialization failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Starts the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    public async Task<TradingResult<bool>> StartAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            if (Health != ServiceHealth.Initialized)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_STATE", "Service must be initialized before starting");
            }
            
            Health = ServiceHealth.Starting;
            StartTime = DateTime.UtcNow;
            UpdateMetric("ServiceStarted", StartTime);
            
            var result = await OnStartAsync(cancellationToken).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                Health = ServiceHealth.Running;
                LogInfo($"{ServiceName} started successfully");
                UpdateMetric("ServiceRunning", DateTime.UtcNow);
            }
            else
            {
                Health = ServiceHealth.Failed;
                LogError($"{ServiceName} start failed", null, result.Error?.Message);
                UpdateMetric("ServiceStartFailed", DateTime.UtcNow);
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            Health = ServiceHealth.Failed;
            LogError($"{ServiceName} start threw exception", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("START_EXCEPTION", $"Service start failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A result indicating success or failure</returns>
    public async Task<TradingResult<bool>> StopAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            if (Health != ServiceHealth.Running)
            {
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            
            Health = ServiceHealth.Stopping;
            UpdateMetric("ServiceStopping", DateTime.UtcNow);
            
            var result = await OnStopAsync(cancellationToken).ConfigureAwait(false);
            
            if (result.IsSuccess)
            {
                Health = ServiceHealth.Stopped;
                LogInfo($"{ServiceName} stopped successfully");
                UpdateMetric("ServiceStopped", DateTime.UtcNow);
            }
            else
            {
                Health = ServiceHealth.Failed;
                LogError($"{ServiceName} stop failed", null, result.Error?.Message);
                UpdateMetric("ServiceStopFailed", DateTime.UtcNow);
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            Health = ServiceHealth.Failed;
            LogError($"{ServiceName} stop threw exception", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("STOP_EXCEPTION", $"Service stop failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current service health status.
    /// </summary>
    /// <returns>The current health status</returns>
    public virtual TradingResult<ServiceHealth> GetHealthAsync()
    {
        LogMethodEntry();
        
        var uptime = Health == ServiceHealth.Running ? DateTime.UtcNow - StartTime : TimeSpan.Zero;
        UpdateMetric("Uptime", uptime);
        
        LogMethodExit();
        return TradingResult<ServiceHealth>.Success(Health);
    }

    #endregion

    #region Logging Methods (MANDATORY)

    /// <summary>
    /// Logs method entry. MANDATORY for ALL methods.
    /// </summary>
    /// <param name="methodName">The method name (automatically captured)</param>
    protected void LogMethodEntry([CallerMemberName] string methodName = "")
    {
        Logger.LogTrace("[{ServiceName}] ENTRY: {MethodName}", ServiceName, methodName);
        UpdateMetric($"Method_{methodName}_Calls", GetMetricValue($"Method_{methodName}_Calls") + 1);
    }

    /// <summary>
    /// Logs method exit. MANDATORY for ALL methods.
    /// </summary>
    /// <param name="methodName">The method name (automatically captured)</param>
    protected void LogMethodExit([CallerMemberName] string methodName = "")
    {
        Logger.LogTrace("[{ServiceName}] EXIT: {MethodName}", ServiceName, methodName);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log</param>
    protected void LogInfo(string message)
    {
        Logger.LogInformation("[{ServiceName}] {Message}", ServiceName, message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message to log</param>
    protected void LogWarning(string message)
    {
        Logger.LogWarning("[{ServiceName}] {Message}", ServiceName, message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception</param>
    /// <param name="additionalInfo">Additional error information</param>
    protected void LogError(string message, Exception? exception = null, string? additionalInfo = null)
    {
        var fullMessage = additionalInfo != null ? $"{message}. {additionalInfo}" : message;
        
        if (exception != null)
        {
            Logger.LogError(exception, "[{ServiceName}] {Message}", ServiceName, fullMessage);
        }
        else
        {
            Logger.LogError("[{ServiceName}] {Message}", ServiceName, fullMessage);
        }
        
        UpdateMetric("ErrorCount", GetMetricValue("ErrorCount") + 1);
    }

    #endregion

    #region Metrics Methods

    /// <summary>
    /// Updates a metric value.
    /// </summary>
    /// <param name="metricName">The metric name</param>
    /// <param name="value">The metric value</param>
    protected void UpdateMetric(string metricName, object value)
    {
        lock (_metricsLock)
        {
            Metrics[metricName] = value;
        }
    }

    /// <summary>
    /// Increments a counter metric.
    /// </summary>
    /// <param name="metricName">The metric name</param>
    /// <param name="increment">The increment value (default: 1)</param>
    protected void IncrementCounter(string metricName, long increment = 1)
    {
        lock (_metricsLock)
        {
            var current = GetMetricValue(metricName);
            Metrics[metricName] = current + increment;
        }
    }

    /// <summary>
    /// Gets a metric value.
    /// </summary>
    /// <param name="metricName">The metric name</param>
    /// <returns>The metric value as long (0 if not found)</returns>
    protected long GetMetricValue(string metricName)
    {
        lock (_metricsLock)
        {
            return Metrics.TryGetValue(metricName, out var value) 
                ? Convert.ToInt64(value) 
                : 0;
        }
    }

    /// <summary>
    /// Gets all metrics as a read-only dictionary.
    /// </summary>
    /// <returns>A read-only dictionary of metrics</returns>
    public IReadOnlyDictionary<string, object> GetMetrics()
    {
        lock (_metricsLock)
        {
            return new Dictionary<string, object>(Metrics);
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the service and its resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the service and its resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        if (disposing)
        {
            LogMethodEntry();
            
            try
            {
                // Stop the service if it's running
                if (Health == ServiceHealth.Running)
                {
                    var stopTask = StopAsync(_disposalTokenSource.Token);
                    stopTask.Wait(TimeSpan.FromSeconds(30)); // Allow 30 seconds for graceful shutdown
                }
                
                _disposalTokenSource.Cancel();
                _disposalTokenSource.Dispose();
                
                LogInfo($"{ServiceName} disposed successfully");
            }
            catch (Exception ex)
            {
                LogError("Error during service disposal", ex);
            }
            finally
            {
                LogMethodExit();
                IsDisposed = true;
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents the health status of a service.
/// </summary>
public enum ServiceHealth
{
    /// <summary>
    /// Service health is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Service is initializing.
    /// </summary>
    Initializing = 1,

    /// <summary>
    /// Service has been initialized but not started.
    /// </summary>
    Initialized = 2,

    /// <summary>
    /// Service is starting up.
    /// </summary>
    Starting = 3,

    /// <summary>
    /// Service is running normally.
    /// </summary>
    Running = 4,

    /// <summary>
    /// Service is stopping.
    /// </summary>
    Stopping = 5,

    /// <summary>
    /// Service has stopped.
    /// </summary>
    Stopped = 6,

    /// <summary>
    /// Service has failed.
    /// </summary>
    Failed = 7
}