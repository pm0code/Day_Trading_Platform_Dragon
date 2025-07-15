using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Configuration;
using AIRES.Core.Interfaces;
using AIRES.Core.Models;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Models;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// Enhanced multi-GPU load balancer with weighted routing and health scoring.
/// </summary>
public class EnhancedLoadBalancerService : AIRESServiceBase, IEnhancedLoadBalancerService
{
    private readonly IAIRESConfiguration _configuration;
    private readonly IGpuDetectionService _gpuDetection;
    private readonly IAIRESLogger _logger;
    private readonly ConcurrentDictionary<string, GpuInstance> _instances;
    private readonly ConcurrentDictionary<string, InstanceMetrics> _metrics;
    private readonly SemaphoreSlim _initializationLock;
    private bool _isInitialized;

    public EnhancedLoadBalancerService(
        IAIRESConfiguration configuration,
        IGpuDetectionService gpuDetection,
        IAIRESLogger logger) : base(logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _gpuDetection = gpuDetection ?? throw new ArgumentNullException(nameof(gpuDetection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instances = new ConcurrentDictionary<string, GpuInstance>();
        _metrics = new ConcurrentDictionary<string, InstanceMetrics>();
        _initializationLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Initializes the load balancer and detects available GPUs.
    /// </summary>
    public new async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            await _initializationLock.WaitAsync(cancellationToken);
            if (_isInitialized)
            {
                LogMethodExit();
                return;
            }

            // Detect available GPUs
            var gpus = await _gpuDetection.DetectAvailableGpusAsync(cancellationToken);
            _logger.LogInfo($"Detected {gpus.Count} GPUs");

            // Create instances based on GPU capabilities
            foreach (var gpu in gpus)
            {
                GpuCapabilities capabilities;
                try
                {
                    capabilities = await _gpuDetection.GetGpuCapabilitiesAsync(gpu.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get capabilities for GPU {gpu.Id}: {ex.Message}");
                    continue;
                }
                var instanceCount = capabilities.RecommendedInstanceCount;

                for (int i = 0; i < instanceCount; i++)
                {
                    var port = 11434 + (gpu.Id * 10) + i; // Base port + GPU offset + instance offset
                    var instanceId = $"Ollama-GPU{gpu.Id}-Instance{i}";
                    
                    var instance = new GpuInstance
                    {
                        Id = instanceId,
                        GpuId = gpu.Id,
                        Port = port,
                        BaseUrl = $"http://localhost:{port}",
                        IsHealthy = true,
                        LastHealthCheck = DateTime.UtcNow,
                        MaxMemoryMB = capabilities.TotalMemoryMB / instanceCount,
                        SupportedModels = capabilities.RecommendedModels
                    };

                    _instances[instanceId] = instance;
                    _metrics[instanceId] = new InstanceMetrics { InstanceId = instanceId };
                    
                    _logger.LogInfo($"Created instance {instanceId} on port {port}");
                }
            }

            _isInitialized = true;
            _logger.LogInfo($"Enhanced load balancer initialized with {_instances.Count} instances");
            
            LogMethodExit();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Selects the best instance for a request using weighted scoring.
    /// </summary>
    public async Task<GpuInstance> SelectInstanceAsync(
        ModelRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var healthyInstances = _instances.Values
                .Where(i => i.IsHealthy && i.SupportedModels.Contains(requirements.ModelName))
                .ToList();

            if (!healthyInstances.Any())
            {
                _logger.LogWarning($"No healthy instances available for model {requirements.ModelName}");
                LogMethodExit();
                throw new InvalidOperationException("No healthy instances available");
            }

            // Calculate scores for each instance
            var scoredInstances = new List<(GpuInstance instance, double score)>();
            
            foreach (var instance in healthyInstances)
            {
                var metrics = _metrics[instance.Id];
                var score = CalculateInstanceScore(instance, metrics, requirements);
                scoredInstances.Add((instance, score));
            }

            // Select instance with best score
            var selected = scoredInstances
                .OrderByDescending(x => x.score)
                .First()
                .instance;

            _logger.LogDebug($"Selected instance {selected.Id} with score {scoredInstances.First(x => x.instance.Id == selected.Id).score:F2}");

            // Update request count
            _metrics[selected.Id].ActiveRequests++;

            LogMethodExit();
            return selected;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to select instance", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Reports successful request completion.
    /// </summary>
    public async Task ReportSuccessAsync(
        string instanceId,
        long responseTimeMs,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (_metrics.TryGetValue(instanceId, out var metrics))
            {
                metrics.SuccessCount++;
                metrics.TotalRequests++;
                metrics.ActiveRequests = Math.Max(0, metrics.ActiveRequests - 1);
                metrics.LastResponseTimeMs = responseTimeMs;
                
                // Update moving average
                if (metrics.AverageResponseTimeMs == 0)
                {
                    metrics.AverageResponseTimeMs = responseTimeMs;
                }
                else
                {
                    metrics.AverageResponseTimeMs = 
                        (metrics.AverageResponseTimeMs * 0.8) + (responseTimeMs * 0.2);
                }

                // Update health score
                if (_instances.TryGetValue(instanceId, out var instance))
                {
                    instance.HealthScore = CalculateHealthScore(metrics);
                }

                _logger.LogDebug($"Instance {instanceId} success: {responseTimeMs}ms, Health: {instance?.HealthScore:F2}");
            }

            LogMethodExit();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to report success for {instanceId}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Reports request failure.
    /// </summary>
    public async Task ReportFailureAsync(
        string instanceId,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (_metrics.TryGetValue(instanceId, out var metrics))
            {
                metrics.ErrorCount++;
                metrics.TotalRequests++;
                metrics.ActiveRequests = Math.Max(0, metrics.ActiveRequests - 1);
                metrics.ConsecutiveErrors++;

                // Check circuit breaker threshold
                if (metrics.ConsecutiveErrors >= _configuration.Processing.MaxRetries)
                {
                    if (_instances.TryGetValue(instanceId, out var instance))
                    {
                        instance.IsHealthy = false;
                        instance.LastError = DateTime.UtcNow;
                        _logger.LogWarning($"Instance {instanceId} marked unhealthy after {metrics.ConsecutiveErrors} consecutive errors");
                    }
                }

                _logger.LogDebug($"Instance {instanceId} failure: {errorCode}, Consecutive: {metrics.ConsecutiveErrors}");
            }

            LogMethodExit();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to report failure for {instanceId}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Gets current health status of all instances.
    /// </summary>
    public async Task<LoadBalancerHealth> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var health = new LoadBalancerHealth
            {
                TotalInstances = _instances.Count,
                HealthyInstances = _instances.Values.Count(i => i.IsHealthy),
                Instances = new List<InstanceHealth>()
            };

            foreach (var instance in _instances.Values)
            {
                var metrics = _metrics[instance.Id];
                GpuHealthStatus? gpuHealthStatus = null;
                try
                {
                    gpuHealthStatus = await _gpuDetection.ValidateGpuHealthAsync(instance.GpuId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"GPU health check failed for {instance.GpuId}: {ex.Message}");
                }

                health.Instances.Add(new InstanceHealth
                {
                    InstanceId = instance.Id,
                    GpuId = instance.GpuId,
                    Port = instance.Port,
                    IsHealthy = instance.IsHealthy,
                    HealthScore = instance.HealthScore,
                    ActiveRequests = metrics.ActiveRequests,
                    SuccessRate = metrics.TotalRequests > 0 
                        ? (double)metrics.SuccessCount / metrics.TotalRequests 
                        : 1.0,
                    AverageResponseTimeMs = metrics.AverageResponseTimeMs,
                    GpuTemperature = gpuHealthStatus?.Temperature ?? 0,
                    GpuUtilization = gpuHealthStatus?.GpuUtilization ?? 0,
                    MemoryUtilization = gpuHealthStatus?.MemoryUtilization ?? 0
                });
            }

            LogMethodExit();
            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get health status", ex);
            LogMethodExit();
            throw;
        }
    }

    private double CalculateInstanceScore(
        GpuInstance instance,
        InstanceMetrics metrics,
        ModelRequirements requirements)
    {
        double score = 100.0;

        // Penalize based on active requests (load)
        score -= metrics.ActiveRequests * 10;

        // Penalize based on average response time
        if (metrics.AverageResponseTimeMs > 0)
        {
            score -= Math.Min(50, metrics.AverageResponseTimeMs / 1000);
        }

        // Penalize based on error rate
        if (metrics.TotalRequests > 0)
        {
            var errorRate = (double)metrics.ErrorCount / metrics.TotalRequests;
            score -= errorRate * 50;
        }

        // Boost for matching GPU capabilities
        if (requirements.PreferredGpuId.HasValue && instance.GpuId == requirements.PreferredGpuId.Value)
        {
            score += 20;
        }

        // Apply health score multiplier
        score *= instance.HealthScore;

        return Math.Max(0, score);
    }

    private double CalculateHealthScore(InstanceMetrics metrics)
    {
        double score = 1.0;

        // Success rate component
        if (metrics.TotalRequests > 0)
        {
            var successRate = (double)metrics.SuccessCount / metrics.TotalRequests;
            score *= successRate;
        }

        // Response time component
        if (metrics.AverageResponseTimeMs > 30000) // 30 seconds
        {
            score *= 0.5;
        }
        else if (metrics.AverageResponseTimeMs > 15000) // 15 seconds
        {
            score *= 0.8;
        }

        // Recent errors penalty
        score *= Math.Pow(0.9, metrics.ConsecutiveErrors);

        return Math.Max(0.1, Math.Min(1.0, score));
    }
}

/// <summary>
/// Instance performance metrics.
/// </summary>
public class InstanceMetrics
{
    public string InstanceId { get; set; } = string.Empty;
    public int ActiveRequests { get; set; }
    public long TotalRequests { get; set; }
    public long SuccessCount { get; set; }
    public long ErrorCount { get; set; }
    public int ConsecutiveErrors { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public long LastResponseTimeMs { get; set; }
}