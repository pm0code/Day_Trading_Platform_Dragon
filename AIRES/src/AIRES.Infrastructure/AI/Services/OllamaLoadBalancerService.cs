using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Models;
using AIRES.Infrastructure.AI.Clients;
using AIRES.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// Load balancer service for distributing requests across multiple Ollama instances.
/// Provides fault tolerance and improved performance through GPU utilization.
/// </summary>
public class OllamaLoadBalancerService : AIRESServiceBase
{
    private readonly List<OllamaInstance> _instances;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAIRESConfiguration _configuration;
    private readonly OllamaHealthCheckClient _healthCheckClient;
    private readonly SemaphoreSlim _instanceLock = new(1, 1);
    private int _currentIndex = 0;
    private Timer? _healthCheckTimer;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);

    public OllamaLoadBalancerService(
        IAIRESLogger logger,
        IHttpClientFactory httpClientFactory,
        IAIRESConfiguration configuration,
        OllamaHealthCheckClient healthCheckClient)
        : base(logger, nameof(OllamaLoadBalancerService))
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _healthCheckClient = healthCheckClient ?? throw new ArgumentNullException(nameof(healthCheckClient));
        
        // Initialize instances based on configuration
        _instances = new List<OllamaInstance>
        {
            new() { GpuId = 0, Port = 11434 },
            new() { GpuId = 1, Port = 11435 }
        };
        
        LogInfo($"Initialized with {_instances.Count} Ollama instances");
    }

    /// <summary>
    /// Starts the load balancer service and begins health monitoring.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            await base.StartAsync(cancellationToken);
            
            // Perform initial health check
            await CheckAllInstancesHealthAsync();
            
            // Start periodic health checks
            _healthCheckTimer = new Timer(
                async _ => await CheckAllInstancesHealthAsync(),
                null,
                _healthCheckInterval,
                _healthCheckInterval);
            
            LogInfo("Load balancer started with health monitoring");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to start load balancer", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Stops the load balancer service and health monitoring.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            _healthCheckTimer?.Dispose();
            await base.StopAsync(cancellationToken);
            
            LogInfo("Load balancer stopped");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error stopping load balancer", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Gets the next healthy Ollama instance using round-robin selection.
    /// </summary>
    public async Task<OllamaInstance> GetNextInstanceAsync()
    {
        LogMethodEntry();
        UpdateMetric("LoadBalancer.GetInstanceRequests", 1);
        
        await _instanceLock.WaitAsync();
        try
        {
            var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
            
            if (!healthyInstances.Any())
            {
                UpdateMetric("LoadBalancer.NoHealthyInstances", 1);
                LogError("No healthy Ollama instances available");
                
                // Try to recover by checking health again
                await CheckAllInstancesHealthAsync();
                healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
                
                if (!healthyInstances.Any())
                {
                    throw new InvalidOperationException("No healthy Ollama instances available after health check");
                }
            }
            
            // Round-robin selection among healthy instances
            var instance = healthyInstances[_currentIndex % healthyInstances.Count];
            _currentIndex = (_currentIndex + 1) % healthyInstances.Count;
            
            UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.Selected", 1);
            LogDebug($"Selected {instance.Name} for next request");
            
            LogMethodExit();
            return instance;
        }
        finally
        {
            _instanceLock.Release();
        }
    }

    /// <summary>
    /// Creates an HttpClient configured for the specified Ollama instance.
    /// </summary>
    public HttpClient CreateClientForInstance(OllamaInstance instance)
    {
        LogMethodEntry();
        
        var client = _httpClientFactory.CreateClient($"Ollama-GPU{instance.GpuId}");
        client.BaseAddress = new Uri(instance.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(5); // Ollama can take time for large models
        
        LogDebug($"Created HttpClient for {instance.Name}");
        LogMethodExit();
        return client;
    }

    /// <summary>
    /// Reports successful request completion for metrics tracking.
    /// </summary>
    public async Task ReportSuccessAsync(OllamaInstance instance, double responseTimeMs)
    {
        LogMethodEntry();
        
        await _instanceLock.WaitAsync();
        try
        {
            instance.TotalRequests++;
            instance.ActiveRequests = Math.Max(0, instance.ActiveRequests - 1);
            
            // Update rolling average response time
            instance.AverageResponseTime = 
                (instance.AverageResponseTime * (instance.TotalRequests - 1) + responseTimeMs) 
                / instance.TotalRequests;
            
            UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.SuccessfulRequests", 1);
            UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.ResponseTime", responseTimeMs);
            
            LogDebug($"{instance.Name} request completed in {responseTimeMs:F2}ms");
        }
        finally
        {
            _instanceLock.Release();
            LogMethodExit();
        }
    }

    /// <summary>
    /// Reports request failure for metrics and health tracking.
    /// </summary>
    public async Task ReportFailureAsync(OllamaInstance instance, Exception error)
    {
        LogMethodEntry();
        
        await _instanceLock.WaitAsync();
        try
        {
            instance.TotalErrors++;
            instance.ActiveRequests = Math.Max(0, instance.ActiveRequests - 1);
            
            UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.FailedRequests", 1);
            LogWarning($"{instance.Name} request failed: {error.Message}");
            
            // Check if instance should be marked unhealthy
            var errorRate = (double)instance.TotalErrors / Math.Max(1, instance.TotalRequests);
            if (errorRate > 0.5 && instance.TotalRequests > 10)
            {
                instance.IsHealthy = false;
                LogError($"{instance.Name} marked unhealthy due to high error rate: {errorRate:P}");
                UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.MarkedUnhealthy", 1);
            }
        }
        finally
        {
            _instanceLock.Release();
            LogMethodExit();
        }
    }

    /// <summary>
    /// Checks health of all Ollama instances.
    /// </summary>
    private async Task CheckAllInstancesHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var tasks = _instances.Select(instance => CheckInstanceHealthAsync(instance));
            await Task.WhenAll(tasks);
            
            var healthyCount = _instances.Count(i => i.IsHealthy);
            UpdateMetric("LoadBalancer.HealthyInstances", healthyCount);
            UpdateMetric("LoadBalancer.UnhealthyInstances", _instances.Count - healthyCount);
            
            LogInfo($"Health check complete in {stopwatch.ElapsedMilliseconds}ms. " +
                   $"Healthy: {healthyCount}/{_instances.Count}");
        }
        catch (Exception ex)
        {
            LogError("Error during health check", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Checks health of a single Ollama instance.
    /// </summary>
    private async Task CheckInstanceHealthAsync(OllamaInstance instance)
    {
        LogMethodEntry();
        
        try
        {
            // Create a temporary client for health check
            using var client = CreateClientForInstance(instance);
            var isHealthy = await _healthCheckClient.CheckOllamaHealthAsync(instance.BaseUrl);
            
            await _instanceLock.WaitAsync();
            try
            {
                var wasHealthy = instance.IsHealthy;
                instance.IsHealthy = isHealthy;
                instance.LastHealthCheck = DateTime.UtcNow;
                
                if (wasHealthy != isHealthy)
                {
                    var status = isHealthy ? "healthy" : "unhealthy";
                    LogInfo($"{instance.Name} status changed to {status}");
                    UpdateMetric($"LoadBalancer.GPU{instance.GpuId}.StatusChanged", 1);
                }
            }
            finally
            {
                _instanceLock.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Health check failed for {instance.Name}", ex);
            
            await _instanceLock.WaitAsync();
            try
            {
                instance.IsHealthy = false;
                instance.LastHealthCheck = DateTime.UtcNow;
            }
            finally
            {
                _instanceLock.Release();
            }
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Gets current status of all instances for monitoring.
    /// </summary>
    public async Task<IReadOnlyList<OllamaInstance>> GetInstanceStatusAsync()
    {
        LogMethodEntry();
        
        await _instanceLock.WaitAsync();
        try
        {
            var statusList = _instances.Select(i => new OllamaInstance
            {
                GpuId = i.GpuId,
                Port = i.Port,
                IsHealthy = i.IsHealthy,
                LastHealthCheck = i.LastHealthCheck,
                ActiveRequests = i.ActiveRequests,
                TotalRequests = i.TotalRequests,
                TotalErrors = i.TotalErrors,
                AverageResponseTime = i.AverageResponseTime
            }).ToList();
            
            LogMethodExit();
            return statusList;
        }
        finally
        {
            _instanceLock.Release();
        }
    }
}