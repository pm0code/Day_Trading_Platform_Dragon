using System.Diagnostics;
using System.Runtime.CompilerServices;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.Foundation.Canonical;

/// <summary>
/// Base class for all AIRES services providing canonical patterns for logging,
/// metrics, lifecycle management, and error handling.
/// 
/// ARCHITECTURAL PRINCIPLE: This is AIRES's own foundation - completely independent
/// from any other system or project. All AIRES services MUST inherit from this base.
/// </summary>
public abstract class AIRESServiceBase : IDisposable
{
    protected IAIRESLogger Logger { get; }
    protected string ServiceName { get; }
    
    private readonly Dictionary<string, object> _metrics = new();
    private readonly object _metricsLock = new();
    private readonly Stopwatch _methodTimer = new();
    private bool _disposed;
    
    protected AIRESServiceBase(IAIRESLogger logger, string? serviceName = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ServiceName = serviceName ?? GetType().Name;
        
        Logger.LogInfo($"Initializing AIRES service: {ServiceName}");
    }
    
    #region Canonical Logging Methods
    
    /// <summary>
    /// Logs method entry. MANDATORY for ALL methods in AIRES services.
    /// </summary>
    protected void LogMethodEntry([CallerMemberName] string methodName = "")
    {
        _methodTimer.Restart();
        Logger.LogTrace($"[{ServiceName}] ENTRY: {methodName} | Thread: {Thread.CurrentThread.ManagedThreadId} | Correlation: {Logger.GetCorrelationId()}");
        IncrementCounter($"Method_{methodName}_Calls");
    }
    
    /// <summary>
    /// Logs method exit. MANDATORY for ALL methods in AIRES services.
    /// </summary>
    protected void LogMethodExit([CallerMemberName] string methodName = "")
    {
        var duration = _methodTimer.Elapsed;
        Logger.LogTrace($"[{ServiceName}] EXIT: {methodName} | Duration: {duration.TotalMilliseconds:F2}ms | Correlation: {Logger.GetCorrelationId()}");
        Logger.LogMetric($"{ServiceName}.{methodName}.Duration", duration.TotalMilliseconds);
    }
    
    protected void LogInfo(string message)
    {
        Logger.LogInfo($"[{ServiceName}] {message}");
    }
    
    protected void LogWarning(string message)
    {
        Logger.LogWarning($"[{ServiceName}] {message}");
    }
    
    protected void LogError(string message, Exception? exception = null)
    {
        Logger.LogError($"[{ServiceName}] {message}", exception);
        IncrementCounter("ErrorCount");
    }
    
    protected void LogDebug(string message)
    {
        Logger.LogDebug($"[{ServiceName}] {message}");
    }
    
    protected void LogTrace(string message)
    {
        Logger.LogTrace($"[{ServiceName}] {message}");
    }
    
    #endregion
    
    #region Virtual Lifecycle Methods
    
    /// <summary>
    /// Initializes the service. Override to provide initialization logic.
    /// </summary>
    protected virtual Task<AIRESResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        LogInfo($"{ServiceName} initialized successfully");
        LogMethodExit();
        return Task.FromResult(AIRESResult<bool>.Success(true));
    }
    
    /// <summary>
    /// Starts the service. Override to provide startup logic.
    /// </summary>
    protected virtual Task<AIRESResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        LogInfo($"{ServiceName} started successfully");
        LogMethodExit();
        return Task.FromResult(AIRESResult<bool>.Success(true));
    }
    
    /// <summary>
    /// Stops the service. Override to provide shutdown logic.
    /// </summary>
    protected virtual Task<AIRESResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        LogInfo($"{ServiceName} stopped successfully");
        LogMethodExit();
        return Task.FromResult(AIRESResult<bool>.Success(true));
    }
    
    #endregion
    
    #region Public Service Lifecycle Methods
    
    /// <summary>
    /// Initializes the AIRES service.
    /// </summary>
    public async Task<AIRESResult<bool>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            LogInfo("Service initialization starting");
            var result = await OnInitializeAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                LogInfo("Service initialized successfully");
            }
            else
            {
                LogError($"Service initialization failed: {result.ErrorMessage}");
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            LogError("Service initialization threw exception", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("INIT_EXCEPTION", $"Service initialization failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Starts the AIRES service.
    /// </summary>
    public async Task<AIRESResult<bool>> StartAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            LogInfo("Service starting");
            var result = await OnStartAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                LogInfo("Service started successfully");
                Logger.LogEvent("ServiceStarted", new Dictionary<string, object>
                {
                    ["ServiceName"] = ServiceName,
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            else
            {
                LogError($"Service start failed: {result.ErrorMessage}");
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            LogError("Service start threw exception", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("START_EXCEPTION", $"Service start failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Stops the AIRES service.
    /// </summary>
    public async Task<AIRESResult<bool>> StopAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            LogInfo("Service stopping");
            var result = await OnStopAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                LogInfo("Service stopped successfully");
                Logger.LogEvent("ServiceStopped", new Dictionary<string, object>
                {
                    ["ServiceName"] = ServiceName,
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            else
            {
                LogError($"Service stop failed: {result.ErrorMessage}");
            }
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            LogError("Service stop threw exception", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("STOP_EXCEPTION", $"Service stop failed: {ex.Message}", ex);
        }
    }
    
    #endregion
    
    #region Metrics
    
    protected void IncrementCounter(string metricName, long increment = 1)
    {
        lock (_metricsLock)
        {
            if (_metrics.TryGetValue(metricName, out var value) && value is long current)
            {
                _metrics[metricName] = current + increment;
            }
            else
            {
                _metrics[metricName] = increment;
            }
        }
        
        Logger.LogMetric($"{ServiceName}.{metricName}", increment);
    }
    
    protected void UpdateMetric(string metricName, object value)
    {
        lock (_metricsLock)
        {
            _metrics[metricName] = value;
        }
        
        if (value is double doubleValue)
        {
            Logger.LogMetric($"{ServiceName}.{metricName}", doubleValue);
        }
    }
    
    public IReadOnlyDictionary<string, object> GetMetrics()
    {
        lock (_metricsLock)
        {
            return new Dictionary<string, object>(_metrics);
        }
    }
    
    #endregion
    
    #region Disposal
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
        {
            try
            {
                LogInfo($"{ServiceName} disposing");
                Logger.LogEvent("ServiceDisposed", new Dictionary<string, object>
                {
                    ["ServiceName"] = ServiceName,
                    ["Timestamp"] = DateTime.UtcNow,
                    ["Metrics"] = GetMetrics()
                });
            }
            catch
            {
                // Swallow disposal errors
            }
            finally
            {
                _disposed = true;
            }
        }
    }
    
    #endregion
}