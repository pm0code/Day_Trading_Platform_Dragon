// TradingPlatform.Core.Canonical.CanonicalServiceBaseEnhanced
// Enhanced canonical base class with automatic method logging via TradingLogOrchestratorEnhanced
// Provides operation tracking, child logger support, and MCP-compliant event codes

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Foundation.Enums;

namespace TradingPlatform.Core.Canonical;

/// <summary>
/// Enhanced canonical base class for all services with automatic method logging
/// Integrates with TradingLogOrchestratorEnhanced for comprehensive tracking
/// </summary>
public abstract class CanonicalServiceBaseEnhanced : ICanonicalService, IDisposable
{
    #region Core Infrastructure
    
    protected readonly IChildLogger Logger;
    protected readonly string ServiceName;
    protected readonly Dictionary<string, string> ServiceMetadata;
    private readonly ConcurrentDictionary<string, ServiceHealth> _healthChecks = new();
    private bool _isInitialized;
    private bool _isStarted;
    private bool _disposed;
    
    #endregion
    
    #region Constructor
    
    protected CanonicalServiceBaseEnhanced(string serviceName, Dictionary<string, string>? metadata = null)
    {
        ServiceName = serviceName;
        ServiceMetadata = metadata ?? new Dictionary<string, string>();
        ServiceMetadata["ServiceType"] = GetType().Name;
        ServiceMetadata["CreatedAt"] = DateTime.UtcNow.ToString("O");
        
        // Create child logger for this service
        Logger = TradingLogOrchestratorEnhanced.Instance.CreateChildLogger(ServiceName, ServiceMetadata);
        
        // Log service instantiation
        Logger.LogEvent(TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
            $"Service '{ServiceName}' instantiated",
            new { Metadata = ServiceMetadata });
    }
    
    #endregion
    
    #region Lifecycle Management with Automatic Logging
    
    /// <summary>
    /// Initialize service with automatic operation tracking
    /// </summary>
    public virtual async Task<TradingResult<bool>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("Initialize", async () =>
        {
            if (_isInitialized)
            {
                Logger.LogWarning("Service already initialized");
                return TradingResult<bool>.Success(true);
            }
            
            try
            {
                Logger.LogInfo("Initializing service...");
                
                // Perform service-specific initialization
                var result = await OnInitializeAsync(cancellationToken);
                
                if (result.IsSuccess)
                {
                    _isInitialized = true;
                    Logger.LogEvent(TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                        "Service initialized successfully",
                        new { ServiceName });
                }
                else
                {
                    Logger.LogEvent(TradingLogOrchestratorEnhanced.COMPONENT_FAILED,
                        "Service initialization failed",
                        new { Error = result.Error },
                        LogLevel.Error);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Service initialization failed", ex);
                return TradingResult<bool>.Failure("SERVICE_INIT_FAILED", $"Service initialization failed: {ex.Message}", ex);
            }
        });
    }
    
    /// <summary>
    /// Start service with automatic operation tracking
    /// </summary>
    public virtual async Task<TradingResult<bool>> StartAsync(CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("Start", async () =>
        {
            if (!_isInitialized)
            {
                Logger.LogError("Cannot start service - not initialized");
                return TradingResult<bool>.Failure("SERVICE_NOT_INITIALIZED", "Service not initialized");
            }
            
            if (_isStarted)
            {
                Logger.LogWarning("Service already started");
                return TradingResult<bool>.Success(true);
            }
            
            try
            {
                Logger.LogInfo("Starting service...");
                
                var result = await OnStartAsync(cancellationToken);
                
                if (result.IsSuccess)
                {
                    _isStarted = true;
                    Logger.LogEvent(TradingLogOrchestratorEnhanced.SYSTEM_STARTUP,
                        "Service started successfully",
                        new { ServiceName });
                }
                else
                {
                    Logger.LogEvent(TradingLogOrchestratorEnhanced.COMPONENT_FAILED,
                        "Service start failed",
                        new { Error = result.Error },
                        LogLevel.Error);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Service start failed", ex);
                return TradingResult<bool>.Failure("SERVICE_START_FAILED", $"Service start failed: {ex.Message}", ex);
            }
        });
    }
    
    /// <summary>
    /// Stop service with automatic operation tracking
    /// </summary>
    public virtual async Task<TradingResult<bool>> StopAsync(CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("Stop", async () =>
        {
            if (!_isStarted)
            {
                Logger.LogWarning("Service not started");
                return TradingResult<bool>.Success(true);
            }
            
            try
            {
                Logger.LogInfo("Stopping service...");
                
                var result = await OnStopAsync(cancellationToken);
                
                if (result.IsSuccess)
                {
                    _isStarted = false;
                    Logger.LogEvent(TradingLogOrchestratorEnhanced.SYSTEM_SHUTDOWN,
                        "Service stopped successfully",
                        new { ServiceName });
                }
                else
                {
                    Logger.LogError("Service stop failed", null, result.Error?.Message);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Service stop failed", ex);
                return TradingResult<bool>.Failure("SERVICE_STOP_FAILED", $"Service stop failed: {ex.Message}", ex);
            }
        });
    }
    
    #endregion
    
    #region Health Monitoring with Event Codes
    
    /// <summary>
    /// Check service health with automatic logging
    /// </summary>
    public virtual async Task<HealthCheckResult> CheckHealthAsync()
    {
        return await TrackOperationAsync("HealthCheck", async () =>
        {
            var checks = new Dictionary<string, HealthCheckEntry>();
            
            // Basic health checks
            checks["initialized"] = new HealthCheckEntry
            {
                Status = _isInitialized ? ServiceHealth.Healthy : ServiceHealth.Unhealthy,
                Description = _isInitialized ? "Service initialized" : "Service not initialized"
            };
            
            checks["started"] = new HealthCheckEntry
            {
                Status = _isStarted ? ServiceHealth.Healthy : ServiceHealth.Degraded,
                Description = _isStarted ? "Service running" : "Service not started"
            };
            
            // Perform service-specific health checks
            var serviceChecks = await OnCheckHealthAsync();
            foreach (var check in serviceChecks)
            {
                checks[check.Key] = check.Value;
            }
            
            // Determine overall health
            var overallStatus = ServiceHealth.Healthy;
            if (checks.Any(c => c.Value.Status == ServiceHealth.Unhealthy))
            {
                overallStatus = ServiceHealth.Unhealthy;
            }
            else if (checks.Any(c => c.Value.Status == ServiceHealth.Degraded))
            {
                overallStatus = ServiceHealth.Degraded;
            }
            
            // Log health status with appropriate event code
            if (overallStatus == ServiceHealth.Healthy)
            {
                Logger.LogEvent(TradingLogOrchestratorEnhanced.HEALTH_CHECK_PASSED,
                    "Health check passed",
                    new { Checks = checks });
            }
            else
            {
                Logger.LogEvent(TradingLogOrchestratorEnhanced.HEALTH_CHECK_FAILED,
                    "Health check failed",
                    new { Status = overallStatus, Checks = checks },
                    LogLevel.Warning);
            }
            
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                Status = overallStatus,
                Checks = checks,
                Timestamp = DateTime.UtcNow
            };
        });
    }
    
    #endregion
    
    #region Automatic Method Logging Helpers
    
    /// <summary>
    /// Track async operation with automatic logging
    /// </summary>
    protected async Task<T> TrackOperationAsync<T>(string operationName, Func<Task<T>> operation,
        object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var fullOperationName = $"{ServiceName}.{operationName}";
        var operationId = Logger.StartOperation(fullOperationName, parameters, memberName, sourceFilePath, sourceLineNumber);
        
        try
        {
            var result = await operation();
            Logger.CompleteOperation(operationId, result, memberName);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            Logger.LogEvent(TradingLogOrchestratorEnhanced.OPERATION_CANCELLED,
                $"Operation '{fullOperationName}' cancelled",
                new { Reason = ex.Message },
                LogLevel.Warning);
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailOperation(operationId, ex, memberName: memberName);
            throw;
        }
    }
    
    /// <summary>
    /// Track sync operation with automatic logging
    /// </summary>
    protected T TrackOperation<T>(string operationName, Func<T> operation,
        object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var fullOperationName = $"{ServiceName}.{operationName}";
        var operationId = Logger.StartOperation(fullOperationName, parameters, memberName, sourceFilePath, sourceLineNumber);
        
        try
        {
            var result = operation();
            Logger.CompleteOperation(operationId, result, memberName);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            Logger.LogEvent(TradingLogOrchestratorEnhanced.OPERATION_CANCELLED,
                $"Operation '{fullOperationName}' cancelled",
                new { Reason = ex.Message },
                LogLevel.Warning);
            throw;
        }
        catch (Exception ex)
        {
            Logger.FailOperation(operationId, ex, memberName: memberName);
            throw;
        }
    }
    
    /// <summary>
    /// Log method entry automatically
    /// </summary>
    protected void LogMethodEntry(object? parameters = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Logger.LogDebug($"→ Entering {memberName}", parameters, memberName, sourceFilePath, sourceLineNumber);
    }
    
    /// <summary>
    /// Log method exit automatically
    /// </summary>
    protected void LogMethodExit(object? result = null, TimeSpan? duration = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var data = new
        {
            Result = result,
            DurationMs = duration?.TotalMilliseconds
        };
        
        Logger.LogDebug($"← Exiting {memberName}", data, memberName, sourceFilePath, sourceLineNumber);
    }
    
    #endregion
    
    #region Performance Monitoring
    
    /// <summary>
    /// Monitor method performance automatically
    /// </summary>
    protected async Task<T> MonitorPerformanceAsync<T>(string operationName, Func<Task<T>> operation,
        TimeSpan? targetDuration = null)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            sw.Stop();
            
            if (targetDuration.HasValue && sw.Elapsed > targetDuration.Value)
            {
                Logger.LogEvent(TradingLogOrchestratorEnhanced.PERFORMANCE_DEGRADATION,
                    $"Operation '{operationName}' exceeded target duration",
                    new
                    {
                        Operation = operationName,
                        ActualMs = sw.ElapsedMilliseconds,
                        TargetMs = targetDuration.Value.TotalMilliseconds,
                        ExceededBy = (sw.Elapsed - targetDuration.Value).TotalMilliseconds
                    },
                    LogLevel.Warning);
            }
            
            return result;
        }
        catch
        {
            sw.Stop();
            throw;
        }
    }
    
    #endregion
    
    #region Abstract Methods for Derived Classes
    
    /// <summary>
    /// Service-specific initialization logic
    /// </summary>
    protected abstract Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Service-specific start logic
    /// </summary>
    protected abstract Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Service-specific stop logic
    /// </summary>
    protected abstract Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Service-specific health checks
    /// </summary>
    protected abstract Task<Dictionary<string, HealthCheckEntry>> OnCheckHealthAsync();
    
    #endregion
    
    #region IDisposable
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    if (_isStarted)
                    {
                        StopAsync().GetAwaiter().GetResult();
                    }
                    
                    Logger.LogInfo($"Service '{ServiceName}' disposed");
                    Logger.Dispose();
                }
                catch (Exception ex)
                {
                    // Log to console as logger might be disposed
                    Console.WriteLine($"Error disposing service '{ServiceName}': {ex.Message}");
                }
            }
            
            _disposed = true;
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    #endregion
}

/// <summary>
/// Canonical service interface
/// </summary>
public interface ICanonicalService
{
    Task<TradingResult<bool>> InitializeAsync(CancellationToken cancellationToken = default);
    Task<TradingResult<bool>> StartAsync(CancellationToken cancellationToken = default);
    Task<TradingResult<bool>> StopAsync(CancellationToken cancellationToken = default);
    Task<HealthCheckResult> CheckHealthAsync();
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceHealth Status { get; set; }
    public Dictionary<string, HealthCheckEntry> Checks { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Health check entry
/// </summary>
public class HealthCheckEntry
{
    public ServiceHealth Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
}