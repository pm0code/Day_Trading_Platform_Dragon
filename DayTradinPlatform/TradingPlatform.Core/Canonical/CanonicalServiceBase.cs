using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Base class for all canonical service implementations.
    /// Provides lifecycle management, health checks, and metrics collection.
    /// </summary>
    public abstract class CanonicalServiceBase : CanonicalBase, IDisposable
    {
        private readonly string _serviceName;
        private readonly Stopwatch _uptimeStopwatch;
        private readonly ConcurrentDictionary<string, object> _serviceMetrics;
        private readonly SemaphoreSlim _stateChangeSemaphore;
        private readonly CancellationTokenSource _serviceCancellation;
        
        private ServiceState _currentState;
        private DateTime _lastStateChange;

        /// <summary>
        /// Gets the current service state
        /// </summary>
        protected ServiceState ServiceState => _currentState;

        protected CanonicalServiceBase(ITradingLogger logger, string serviceName) 
            : base(logger, serviceName)
        {
            _serviceName = serviceName;
            _uptimeStopwatch = new Stopwatch();
            _serviceMetrics = new ConcurrentDictionary<string, object>();
            _stateChangeSemaphore = new SemaphoreSlim(1, 1);
            _serviceCancellation = new CancellationTokenSource();
            
            _currentState = ServiceState.Created;
            _lastStateChange = DateTime.UtcNow;
            
            _uptimeStopwatch.Start();
            
            // Initialize default metrics
            UpdateMetric("ServiceName", _serviceName);
            UpdateMetric("CurrentState", _currentState.ToString());
            UpdateMetric("CorrelationId", CorrelationId);
        }

        #region Service Lifecycle

        /// <summary>
        /// Initializes the service
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_currentState != ServiceState.Created)
            {
                LogWarning($"Cannot initialize {_serviceName} from state: {_currentState}");
                return false;
            }

            try
            {
                await ChangeStateAsync(ServiceState.Initializing);
                LogInfo($"Initializing {_serviceName}");
                
                await OnInitializeAsync(cancellationToken);
                await ChangeStateAsync(ServiceState.Initialized);
                
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
                
                await ChangeStateAsync(ServiceState.Failed);
                return false;
            }
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

            try
            {
                await ChangeStateAsync(ServiceState.Starting);
                LogInfo($"Starting {_serviceName}");
                
                await OnStartAsync(cancellationToken);
                await ChangeStateAsync(ServiceState.Running);
                
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
                
                await ChangeStateAsync(ServiceState.Failed);
                return false;
            }
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

            try
            {
                await ChangeStateAsync(ServiceState.Stopping);
                LogInfo($"Stopping {_serviceName}");
                
                await OnStopAsync(cancellationToken);
                await ChangeStateAsync(ServiceState.Stopped);
                
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
                    $"Failed to stop {_serviceName}",
                    ex,
                    "Service shutdown",
                    "Service may not have shut down cleanly",
                    "Check logs for shutdown errors");
                
                await ChangeStateAsync(ServiceState.Failed);
                return false;
            }
        }

        #endregion

        #region Health Checks

        /// <summary>
        /// Performs a health check on the service
        /// </summary>
        public async Task<ServiceHealthCheck> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var healthCheck = new ServiceHealthCheck
            {
                ServiceName = _serviceName,
                CurrentState = _currentState,
                UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds,
                LastStateChange = _lastStateChange,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                if (_currentState != ServiceState.Running)
                {
                    healthCheck.IsHealthy = false;
                    healthCheck.HealthMessage = $"Service is not running (State: {_currentState})";
                }
                else
                {
                    var (isHealthy, message, details) = await OnCheckHealthAsync(cancellationToken);
                    healthCheck.IsHealthy = isHealthy;
                    healthCheck.HealthMessage = message;
                    healthCheck.Details = details;
                }
            }
            catch (Exception ex)
            {
                healthCheck.IsHealthy = false;
                healthCheck.HealthMessage = $"Health check failed: {ex.Message}";
                LogError("Health check failed", ex);
            }

            return healthCheck;
        }

        #endregion

        #region Metrics

        /// <summary>
        /// Updates a service metric
        /// </summary>
        protected void UpdateMetric(string name, object value)
        {
            _serviceMetrics.AddOrUpdate(name, value, (k, v) => value);
        }

        /// <summary>
        /// Increments a counter metric
        /// </summary>
        protected void IncrementCounter(string name, long incrementBy = 1)
        {
            _serviceMetrics.AddOrUpdate(name, 
                incrementBy, 
                (k, v) => v is long current ? current + incrementBy : incrementBy);
        }

        /// <summary>
        /// Gets all service metrics
        /// </summary>
        public virtual IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = new Dictionary<string, object>(_serviceMetrics)
            {
                ["ServiceName"] = _serviceName,
                ["CurrentState"] = _currentState.ToString(),
                ["UptimeSeconds"] = _uptimeStopwatch.Elapsed.TotalSeconds,
                ["LastStateChange"] = _lastStateChange,
                ["CorrelationId"] = CorrelationId
            };

            return metrics;
        }

        #endregion

        #region Abstract Methods

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
                var result = await operation();
                stopwatch.Stop();
                
                UpdateMetric($"{operationName}LastDurationMs", stopwatch.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                IncrementCounter($"{operationName}ErrorCount");
                UpdateMetric($"{operationName}LastErrorTime", DateTime.UtcNow);
                UpdateMetric($"{operationName}LastError", ex.Message);
                
                throw;
            }
        }

        private async Task ChangeStateAsync(ServiceState newState)
        {
            await _stateChangeSemaphore.WaitAsync();
            
            try
            {
                var oldState = _currentState;
                _currentState = newState;
                _lastStateChange = DateTime.UtcNow;
                
                UpdateMetric("CurrentState", newState.ToString());
                
                LogServiceLifecycle($"State change: {oldState} → {newState}", newState);
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
    /// Service health check result
    /// </summary>
    public class ServiceHealthCheck
    {
        public string ServiceName { get; set; } = string.Empty;
        public ServiceState CurrentState { get; set; }
        public bool IsHealthy { get; set; }
        public string HealthMessage { get; set; } = string.Empty;
        public double UptimeSeconds { get; set; }
        public DateTime LastStateChange { get; set; }
        public DateTime CheckedAt { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }
}