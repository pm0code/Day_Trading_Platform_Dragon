using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all services in the trading platform.
    /// Provides standardized service lifecycle, health monitoring, and operational patterns.
    /// </summary>
    public abstract class CanonicalServiceBase : CanonicalBase, IDisposable
    {
        private readonly string _serviceName;
        private readonly Dictionary<string, object> _serviceMetrics;
        private readonly Stopwatch _uptimeStopwatch;
        private ServiceState _currentState;
        private readonly SemaphoreSlim _stateChangeSemaphore;
        private readonly CancellationTokenSource _serviceCancellation;
        private DateTime _lastHealthCheck;

        protected CanonicalServiceBase(
            ITradingLogger logger,
            string? serviceName = null) : base(logger)
        {
            _serviceName = serviceName ?? GetType().Name;
            _serviceMetrics = new Dictionary<string, object>();
            _uptimeStopwatch = Stopwatch.StartNew();
            _currentState = ServiceState.Created;
            _stateChangeSemaphore = new SemaphoreSlim(1, 1);
            _serviceCancellation = new CancellationTokenSource();
            _lastHealthCheck = DateTime.UtcNow;

            LogServiceLifecycle("Service created", ServiceState.Created);
        }

        #region Service Lifecycle

        /// <summary>
        /// Initializes the service
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            return await ChangeStateAsync(
                ServiceState.Initializing,
                async () =>
                {
                    LogInfo($"Initializing {_serviceName}");
                    
                    try
                    {
                        await OnInitializeAsync(cancellationToken);
                        await ChangeStateAsync(ServiceState.Initialized, () => Task.CompletedTask);
                        
                        LogInfo($"{_serviceName} initialized successfully");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogError(
                            $"Failed to initialize {_serviceName}",
                            ex,
                            "Service initialization",
                            "Service will not be available",
                            "Check configuration and dependencies");
                        
                        await ChangeStateAsync(ServiceState.Failed, () => Task.CompletedTask);
                        throw;
                    }
                });
        }

        /// <summary>
        /// Starts the service
        /// </summary>
        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_currentState != ServiceState.Initialized && _currentState != ServiceState.Stopped)
            {
                LogWarning($"Cannot start {_serviceName} from state: {_currentState}");
                return false;
            }

            return await ChangeStateAsync(
                ServiceState.Starting,
                async () =>
                {
                    LogInfo($"Starting {_serviceName}");
                    
                    try
                    {
                        await OnStartAsync(cancellationToken);
                        await ChangeStateAsync(ServiceState.Running, () => Task.CompletedTask);
                        
                        LogInfo($"{_serviceName} started successfully", new
                        {
                            ServiceName = _serviceName,
                            State = ServiceState.Running,
                            UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds
                        });
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogError(
                            $"Failed to start {_serviceName}",
                            ex,
                            "Service startup",
                            "Service failed to start",
                            "Check logs for startup errors");
                        
                        await ChangeStateAsync(ServiceState.Failed, () => Task.CompletedTask);
                        throw;
                    }
                });
        }

        /// <summary>
        /// Stops the service
        /// </summary>
        public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            if (_currentState != ServiceState.Running)
            {
                LogWarning($"Cannot stop {_serviceName} from state: {_currentState}");
                return false;
            }

            return await ChangeStateAsync(
                ServiceState.Stopping,
                async () =>
                {
                    LogInfo($"Stopping {_serviceName}");
                    
                    try
                    {
                        _serviceCancellation.Cancel();
                        await OnStopAsync(cancellationToken);
                        await ChangeStateAsync(ServiceState.Stopped, () => Task.CompletedTask);
                        
                        LogInfo($"{_serviceName} stopped successfully", new
                        {
                            ServiceName = _serviceName,
                            State = ServiceState.Stopped,
                            TotalUptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds
                        });
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogError(
                            $"Error while stopping {_serviceName}",
                            ex,
                            "Service shutdown",
                            "Service may not have stopped cleanly",
                            "Check for resource leaks or hanging operations");
                        
                        await ChangeStateAsync(ServiceState.Failed, () => Task.CompletedTask);
                        throw;
                    }
                });
        }

        #endregion

        #region Health Monitoring

        /// <summary>
        /// Performs a health check on the service
        /// </summary>
        public async Task<ServiceHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var healthCheckStart = Stopwatch.StartNew();
            
            try
            {
                LogDebug($"Performing health check for {_serviceName}");
                
                var healthStatus = new ServiceHealthStatus
                {
                    ServiceName = _serviceName,
                    CurrentState = _currentState,
                    IsHealthy = _currentState == ServiceState.Running,
                    UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds,
                    LastHealthCheck = _lastHealthCheck,
                    CheckDurationMs = 0
                };

                if (_currentState == ServiceState.Running)
                {
                    var customHealth = await OnCheckHealthAsync(cancellationToken);
                    healthStatus.IsHealthy = customHealth.IsHealthy;
                    healthStatus.HealthMessage = customHealth.Message;
                    healthStatus.HealthDetails = customHealth.Details;
                }
                else
                {
                    healthStatus.HealthMessage = $"Service is in {_currentState} state";
                }

                healthCheckStart.Stop();
                healthStatus.CheckDurationMs = healthCheckStart.ElapsedMilliseconds;
                
                _lastHealthCheck = DateTime.UtcNow;
                UpdateMetric("LastHealthCheckDurationMs", healthCheckStart.ElapsedMilliseconds);
                UpdateMetric("HealthStatus", healthStatus.IsHealthy ? "Healthy" : "Unhealthy");
                
                LogDebug($"Health check completed for {_serviceName}", healthStatus);
                
                return healthStatus;
            }
            catch (Exception ex)
            {
                healthCheckStart.Stop();
                
                LogError(
                    $"Health check failed for {_serviceName}",
                    ex,
                    "Health check operation",
                    "Unable to determine service health",
                    "Service may be experiencing issues");
                
                return new ServiceHealthStatus
                {
                    ServiceName = _serviceName,
                    CurrentState = _currentState,
                    IsHealthy = false,
                    HealthMessage = $"Health check failed: {ex.Message}",
                    CheckDurationMs = healthCheckStart.ElapsedMilliseconds,
                    UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds,
                    LastHealthCheck = _lastHealthCheck
                };
            }
        }

        #endregion

        #region Metrics and Monitoring

        /// <summary>
        /// Updates a service metric
        /// </summary>
        protected void UpdateMetric(string metricName, object value)
        {
            lock (_serviceMetrics)
            {
                _serviceMetrics[metricName] = value;
            }
            
            LogDebug($"Metric updated: {metricName} = {value}");
        }

        /// <summary>
        /// Increments a counter metric
        /// </summary>
        protected void IncrementCounter(string counterName, long incrementBy = 1)
        {
            lock (_serviceMetrics)
            {
                if (_serviceMetrics.TryGetValue(counterName, out var currentValue) && currentValue is long current)
                {
                    _serviceMetrics[counterName] = current + incrementBy;
                }
                else
                {
                    _serviceMetrics[counterName] = incrementBy;
                }
            }
        }

        /// <summary>
        /// Gets current service metrics
        /// </summary>
        public Dictionary<string, object> GetMetrics()
        {
            lock (_serviceMetrics)
            {
                var metrics = new Dictionary<string, object>(_serviceMetrics)
                {
                    ["ServiceName"] = _serviceName,
                    ["CurrentState"] = _currentState.ToString(),
                    ["UptimeSeconds"] = _uptimeStopwatch.Elapsed.TotalSeconds,
                    ["CorrelationId"] = _correlationId
                };
                
                return metrics;
            }
        }

        #endregion

        #region Protected Abstract Methods

        /// <summary>
        /// Override to implement service initialization logic
        /// </summary>
        protected abstract Task OnInitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Override to implement service startup logic
        /// </summary>
        protected abstract Task OnStartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Override to implement service shutdown logic
        /// </summary>
        protected abstract Task OnStopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Override to implement custom health check logic
        /// </summary>
        protected virtual Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> OnCheckHealthAsync(
            CancellationToken cancellationToken)
        {
            // Default implementation - override for custom health checks
            return Task.FromResult((true, "Service is running", (Dictionary<string, object>?)null));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the service cancellation token
        /// </summary>
        protected CancellationToken ServiceCancellationToken => _serviceCancellation.Token;

        /// <summary>
        /// Executes a service operation with monitoring
        /// </summary>
        protected async Task<T> ExecuteServiceOperationAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            bool incrementOperationCounter = true,
            [CallerMemberName] string methodName = "")
        {
            if (incrementOperationCounter)
            {
                IncrementCounter($"{operationName}Count");
            }

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = await ExecuteWithLoggingAsync(
                    operation,
                    $"{_serviceName}.{operationName}",
                    "Service operation failed",
                    $"Check {_serviceName} service health and configuration",
                    methodName);
                
                stopwatch.Stop();
                UpdateMetric($"{operationName}LastDurationMs", stopwatch.ElapsedMilliseconds);
                
                return result;
            }
            catch
            {
                if (incrementOperationCounter)
                {
                    IncrementCounter($"{operationName}ErrorCount");
                }
                throw;
            }
        }

        private async Task<bool> ChangeStateAsync(ServiceState newState, Func<Task> stateChangeAction)
        {
            await _stateChangeSemaphore.WaitAsync();
            
            try
            {
                var oldState = _currentState;
                _currentState = newState;
                
                LogServiceLifecycle($"State change: {oldState} → {newState}", newState);
                
                await stateChangeAction();
                
                return true;
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        private void LogServiceLifecycle(string message, ServiceState state)
        {
            LogInfo($"[LIFECYCLE] {message}", new
            {
                ServiceName = _serviceName,
                State = state,
                UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds
            });
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (_currentState == ServiceState.Running)
                        {
                            StopAsync().GetAwaiter().GetResult();
                        }
                        
                        _serviceCancellation?.Cancel();
                        _serviceCancellation?.Dispose();
                        _stateChangeSemaphore?.Dispose();
                        
                        LogInfo($"{_serviceName} disposed", new
                        {
                            ServiceName = _serviceName,
                            TotalUptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds,
                            FinalState = _currentState
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error during {_serviceName} disposal", ex);
                    }
                }
                
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Service state enumeration
    /// </summary>
    public enum ServiceState
    {
        Created,
        Initializing,
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped,
        Failed
    }

    /// <summary>
    /// Service health status structure
    /// </summary>
    public class ServiceHealthStatus
    {
        public string ServiceName { get; set; } = string.Empty;
        public ServiceState CurrentState { get; set; }
        public bool IsHealthy { get; set; }
        public string? HealthMessage { get; set; }
        public Dictionary<string, object>? HealthDetails { get; set; }
        public double UptimeSeconds { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public long CheckDurationMs { get; set; }
    }
}