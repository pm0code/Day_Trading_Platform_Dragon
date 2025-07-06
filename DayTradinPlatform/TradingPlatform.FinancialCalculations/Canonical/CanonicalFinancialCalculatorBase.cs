// TradingPlatform.FinancialCalculations.Canonical.CanonicalFinancialCalculatorBase
// Enhanced canonical base class for all financial calculation services
// Provides GPU acceleration, decimal precision, regulatory compliance, and audit trails

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ILGPU;
using ILGPU.Runtime;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Models;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.GPU.Models;
using TradingPlatform.FinancialCalculations.Models;
using TradingPlatform.FinancialCalculations.Configuration;
using TradingPlatform.FinancialCalculations.Compliance;

namespace TradingPlatform.FinancialCalculations.Canonical;

/// <summary>
/// Enhanced canonical base class for financial calculation services with GPU acceleration
/// Features:
/// - GPU-accelerated calculations with automatic fallback to CPU
/// - Decimal precision maintenance with scaled integer calculations
/// - Regulatory compliance and audit trail generation
/// - Performance monitoring and optimization
/// - Automatic validation and error handling
/// - Multi-currency support with precision controls
/// - Real-time calculation result caching
/// </summary>
public abstract class CanonicalFinancialCalculatorBase : CanonicalServiceBaseEnhanced
{
    #region Core Infrastructure

    protected readonly FinancialCalculationConfiguration Configuration;
    protected readonly IComplianceAuditor ComplianceAuditor;
    protected readonly GpuOrchestrator GpuOrchestrator;
    protected readonly ConcurrentDictionary<string, CalculationResult> ResultCache;
    protected readonly ConcurrentDictionary<string, CalculationAuditEntry> AuditTrail;
    protected readonly ConcurrentDictionary<string, PerformanceMetrics> PerformanceMetrics;
    
    // Decimal precision constants
    protected const int DEFAULT_DECIMAL_PRECISION = 4;
    protected const long DEFAULT_SCALE_FACTOR = 10000L; // 10^4 for 4 decimal places
    protected const int MAX_DECIMAL_PRECISION = 10;
    
    // Performance monitoring
    private readonly ConcurrentDictionary<string, CalculationPerformanceStats> _performanceStats = new();
    private readonly Timer _performanceFlushTimer;
    
    // GPU acceleration
    private readonly object _gpuLock = new();
    private bool _gpuInitialized;
    private Context? _gpuContext;
    private Accelerator? _gpuAccelerator;
    
    #endregion
    
    #region Constructor

    protected CanonicalFinancialCalculatorBase(
        string serviceName,
        FinancialCalculationConfiguration configuration,
        IComplianceAuditor complianceAuditor,
        GpuOrchestrator? gpuOrchestrator = null,
        Dictionary<string, string>? metadata = null)
        : base(serviceName, metadata)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ComplianceAuditor = complianceAuditor ?? throw new ArgumentNullException(nameof(complianceAuditor));
        GpuOrchestrator = gpuOrchestrator ?? new GpuOrchestrator();
        
        ResultCache = new ConcurrentDictionary<string, CalculationResult>();
        AuditTrail = new ConcurrentDictionary<string, CalculationAuditEntry>();
        PerformanceMetrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        
        // Initialize performance monitoring
        _performanceFlushTimer = new Timer(FlushPerformanceMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        Logger.LogInfo($"Financial calculator '{ServiceName}' instantiated with configuration",
            new { Configuration = configuration.ToSafeString() });
    }

    #endregion
    
    #region Lifecycle Management

    protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initialize GPU acceleration
            await InitializeGpuAccelerationAsync(cancellationToken);
            
            // Initialize compliance auditing
            await ComplianceAuditor.InitializeAsync(ServiceName, cancellationToken);
            
            // Initialize calculation-specific components
            await OnInitializeCalculationEngineAsync(cancellationToken);
            
            Logger.LogInfo("Financial calculation engine initialized successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize financial calculation engine", ex);
            return TradingResult<bool>.Failure(ex);
        }
    }

    protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Start GPU orchestrator if available
            if (GpuOrchestrator != null)
            {
                await GpuOrchestrator.StartAsync(cancellationToken);
            }
            
            // Start calculation-specific services
            await OnStartCalculationEngineAsync(cancellationToken);
            
            Logger.LogInfo("Financial calculation engine started successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to start financial calculation engine", ex);
            return TradingResult<bool>.Failure(ex);
        }
    }

    protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Stop calculation-specific services
            await OnStopCalculationEngineAsync(cancellationToken);
            
            // Stop GPU orchestrator
            if (GpuOrchestrator != null)
            {
                await GpuOrchestrator.StopAsync(cancellationToken);
            }
            
            // Flush performance metrics
            FlushPerformanceMetrics(null);
            
            Logger.LogInfo("Financial calculation engine stopped successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to stop financial calculation engine", ex);
            return TradingResult<bool>.Failure(ex);
        }
    }

    protected override async Task<Dictionary<string, HealthCheckEntry>> OnCheckHealthAsync()
    {
        var checks = new Dictionary<string, HealthCheckEntry>();
        
        // GPU health check
        checks["gpu_acceleration"] = new HealthCheckEntry
        {
            Status = _gpuInitialized ? HealthStatus.Healthy : HealthStatus.Degraded,
            Description = _gpuInitialized ? "GPU acceleration available" : "Using CPU fallback",
            Data = new Dictionary<string, object>
            {
                ["gpu_available"] = _gpuInitialized,
                ["gpu_device"] = _gpuAccelerator?.Name ?? "None"
            }
        };
        
        // Cache health check
        checks["result_cache"] = new HealthCheckEntry
        {
            Status = HealthStatus.Healthy,
            Description = "Result cache operational",
            Data = new Dictionary<string, object>
            {
                ["cache_size"] = ResultCache.Count,
                ["cache_limit"] = Configuration.CacheConfiguration.MaxCacheSize
            }
        };
        
        // Performance metrics check
        var avgLatency = _performanceStats.Values.Any() ? _performanceStats.Values.Average(p => p.AverageLatencyMs) : 0;
        checks["performance"] = new HealthCheckEntry
        {
            Status = avgLatency < Configuration.PerformanceThresholds.MaxLatencyMs ? HealthStatus.Healthy : HealthStatus.Degraded,
            Description = $"Average calculation latency: {avgLatency:F2}ms",
            Data = new Dictionary<string, object>
            {
                ["avg_latency_ms"] = avgLatency,
                ["threshold_ms"] = Configuration.PerformanceThresholds.MaxLatencyMs,
                ["total_calculations"] = _performanceStats.Values.Sum(p => p.TotalCalculations)
            }
        };
        
        // Add service-specific health checks
        var serviceChecks = await OnCheckCalculationEngineHealthAsync();
        foreach (var check in serviceChecks)
        {
            checks[check.Key] = check.Value;
        }
        
        return checks;
    }

    #endregion
    
    #region GPU Acceleration Management

    private async Task InitializeGpuAccelerationAsync(CancellationToken cancellationToken)
    {
        if (!Configuration.GpuConfiguration.EnableGpuAcceleration)
        {
            Logger.LogInfo("GPU acceleration disabled by configuration");
            return;
        }
        
        try
        {
            lock (_gpuLock)
            {
                if (_gpuInitialized) return;
                
                Logger.LogInfo("Initializing GPU acceleration...");
                
                // Initialize ILGPU context
                _gpuContext = Context.CreateDefault();
                
                // Try to get the best available GPU
                var gpuDevice = _gpuContext.GetCudaDevice(0);
                if (gpuDevice != null)
                {
                    _gpuAccelerator = gpuDevice.CreateAccelerator(_gpuContext);
                    Logger.LogInfo($"GPU acceleration initialized with device: {_gpuAccelerator.Name}");
                }
                else
                {
                    // Fall back to CPU accelerator
                    var cpuDevice = _gpuContext.GetCPUDevice();
                    _gpuAccelerator = cpuDevice.CreateAccelerator(_gpuContext);
                    Logger.LogWarning("GPU not available, using CPU accelerator");
                }
                
                _gpuInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to initialize GPU acceleration, using CPU fallback", ex);
            _gpuInitialized = false;
        }
    }

    protected async Task<T> ExecuteWithGpuAsync<T>(
        string operationName,
        Func<Accelerator, Task<T>> gpuOperation,
        Func<Task<T>> cpuFallback,
        CancellationToken cancellationToken = default)
    {
        var auditEntry = StartAuditTrail(operationName);
        
        try
        {
            if (_gpuInitialized && _gpuAccelerator != null && Configuration.GpuConfiguration.EnableGpuAcceleration)
            {
                Logger.LogDebug($"Executing {operationName} with GPU acceleration");
                auditEntry.ExecutionMethod = "GPU";
                
                var result = await gpuOperation(_gpuAccelerator);
                CompleteAuditTrail(auditEntry, result, true);
                return result;
            }
            else
            {
                Logger.LogDebug($"Executing {operationName} with CPU fallback");
                auditEntry.ExecutionMethod = "CPU";
                
                var result = await cpuFallback();
                CompleteAuditTrail(auditEntry, result, false);
                return result;
            }
        }
        catch (Exception ex)
        {
            auditEntry.Error = ex.Message;
            auditEntry.CompletedAt = DateTime.UtcNow;
            Logger.LogError($"Error executing {operationName}", ex);
            throw;
        }
    }

    #endregion
    
    #region Decimal Precision Management

    /// <summary>
    /// Convert decimal to scaled integer for GPU calculations
    /// </summary>
    protected static long ToScaledInteger(decimal value, int precision = DEFAULT_DECIMAL_PRECISION)
    {
        var scaleFactor = (long)Math.Pow(10, precision);
        return (long)(value * scaleFactor);
    }

    /// <summary>
    /// Convert scaled integer back to decimal
    /// </summary>
    protected static decimal FromScaledInteger(long scaledValue, int precision = DEFAULT_DECIMAL_PRECISION)
    {
        var scaleFactor = (decimal)Math.Pow(10, precision);
        return scaledValue / scaleFactor;
    }

    /// <summary>
    /// Validate decimal precision meets regulatory requirements
    /// </summary>
    protected bool ValidateDecimalPrecision(decimal value, int requiredPrecision)
    {
        var scaleFactor = (decimal)Math.Pow(10, requiredPrecision);
        var rounded = Math.Round(value * scaleFactor) / scaleFactor;
        return Math.Abs(value - rounded) < 1e-10m;
    }

    /// <summary>
    /// Round decimal according to regulatory standards
    /// </summary>
    protected decimal RoundToRegulatory(decimal value, int precision = DEFAULT_DECIMAL_PRECISION, MidpointRounding roundingMode = MidpointRounding.ToEven)
    {
        return Math.Round(value, precision, roundingMode);
    }

    #endregion
    
    #region Audit Trail Management

    protected CalculationAuditEntry StartAuditTrail(string operationName, object? parameters = null)
    {
        var auditEntry = new CalculationAuditEntry
        {
            Id = Guid.NewGuid().ToString(),
            ServiceName = ServiceName,
            OperationName = operationName,
            Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
            StartedAt = DateTime.UtcNow,
            UserId = GetCurrentUserId(),
            SessionId = GetCurrentSessionId()
        };
        
        AuditTrail.TryAdd(auditEntry.Id, auditEntry);
        return auditEntry;
    }

    protected void CompleteAuditTrail(CalculationAuditEntry auditEntry, object? result, bool usedGpu)
    {
        auditEntry.CompletedAt = DateTime.UtcNow;
        auditEntry.DurationMs = (auditEntry.CompletedAt - auditEntry.StartedAt).TotalMilliseconds;
        auditEntry.Result = result != null ? JsonSerializer.Serialize(result) : null;
        auditEntry.UsedGpuAcceleration = usedGpu;
        
        // Generate compliance hash for regulatory requirements
        auditEntry.ComplianceHash = GenerateComplianceHash(auditEntry);
        
        // Log audit entry
        Logger.LogInfo($"Calculation audit: {auditEntry.OperationName}",
            new
            {
                AuditId = auditEntry.Id,
                DurationMs = auditEntry.DurationMs,
                UsedGpu = usedGpu,
                ComplianceHash = auditEntry.ComplianceHash
            });
    }

    private string GenerateComplianceHash(CalculationAuditEntry entry)
    {
        var data = $"{entry.ServiceName}|{entry.OperationName}|{entry.Parameters}|{entry.StartedAt:O}|{entry.UserId}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    #endregion
    
    #region Performance Monitoring

    protected void RecordPerformanceMetric(string operationName, double latencyMs, bool usedGpu)
    {
        _performanceStats.AddOrUpdate(operationName,
            new CalculationPerformanceStats
            {
                OperationName = operationName,
                TotalCalculations = 1,
                AverageLatencyMs = latencyMs,
                MinLatencyMs = latencyMs,
                MaxLatencyMs = latencyMs,
                GpuUsageCount = usedGpu ? 1 : 0,
                CpuUsageCount = usedGpu ? 0 : 1,
                LastUpdated = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.TotalCalculations++;
                existing.AverageLatencyMs = (existing.AverageLatencyMs * (existing.TotalCalculations - 1) + latencyMs) / existing.TotalCalculations;
                existing.MinLatencyMs = Math.Min(existing.MinLatencyMs, latencyMs);
                existing.MaxLatencyMs = Math.Max(existing.MaxLatencyMs, latencyMs);
                if (usedGpu) existing.GpuUsageCount++;
                else existing.CpuUsageCount++;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });
    }

    private void FlushPerformanceMetrics(object? state)
    {
        try
        {
            var metrics = _performanceStats.Values.ToArray();
            if (metrics.Length > 0)
            {
                Logger.LogInfo("Performance metrics summary", new { Metrics = metrics });
                
                // Reset counters for next period
                _performanceStats.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to flush performance metrics", ex);
        }
    }

    #endregion
    
    #region Result Caching

    protected async Task<T> GetOrCalculateAsync<T>(
        string cacheKey,
        Func<Task<T>> calculator,
        TimeSpan? cacheExpiry = null)
    {
        // Check cache first
        if (ResultCache.TryGetValue(cacheKey, out var cachedResult))
        {
            var expiry = cacheExpiry ?? Configuration.CacheConfiguration.DefaultCacheExpiry;
            if (DateTime.UtcNow - cachedResult.CalculatedAt < expiry)
            {
                Logger.LogDebug($"Cache hit for key: {cacheKey}");
                return (T)cachedResult.Value;
            }
            else
            {
                // Remove expired entry
                ResultCache.TryRemove(cacheKey, out _);
            }
        }
        
        // Calculate and cache result
        var result = await calculator();
        var calculationResult = new CalculationResult
        {
            Key = cacheKey,
            Value = result,
            CalculatedAt = DateTime.UtcNow,
            ServiceName = ServiceName
        };
        
        ResultCache.TryAdd(cacheKey, calculationResult);
        
        // Maintain cache size limit
        if (ResultCache.Count > Configuration.CacheConfiguration.MaxCacheSize)
        {
            var oldestKey = ResultCache.OrderBy(kvp => kvp.Value.CalculatedAt).First().Key;
            ResultCache.TryRemove(oldestKey, out _);
        }
        
        return result;
    }

    #endregion
    
    #region Validation and Error Handling

    protected void ValidateCalculationInput<T>(T input, string parameterName) where T : class
    {
        if (input == null)
            throw new ArgumentNullException(parameterName);
        
        // Perform service-specific validation
        OnValidateCalculationInput(input, parameterName);
    }

    protected void ValidateDecimalRange(decimal value, decimal minValue, decimal maxValue, string parameterName)
    {
        if (value < minValue || value > maxValue)
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minValue} and {maxValue}");
    }

    protected void ValidateFinancialData(IEnumerable<decimal> data, string parameterName)
    {
        if (data == null)
            throw new ArgumentNullException(parameterName);
        
        var dataArray = data.ToArray();
        if (dataArray.Length == 0)
            throw new ArgumentException("Data cannot be empty", parameterName);
        
        if (dataArray.Any(d => decimal.IsNaN(d) || decimal.IsInfinity(d)))
            throw new ArgumentException("Data contains invalid values (NaN or Infinity)", parameterName);
    }

    #endregion
    
    #region Helper Methods

    private string GetCurrentUserId()
    {
        // In a real implementation, this would get the current user from the security context
        return "system";
    }

    private string GetCurrentSessionId()
    {
        // In a real implementation, this would get the current session ID
        return Environment.MachineName + "_" + Process.GetCurrentProcess().Id;
    }

    #endregion
    
    #region Abstract Methods for Derived Classes

    /// <summary>
    /// Initialize calculation engine specific components
    /// </summary>
    protected abstract Task OnInitializeCalculationEngineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Start calculation engine specific services
    /// </summary>
    protected abstract Task OnStartCalculationEngineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stop calculation engine specific services
    /// </summary>
    protected abstract Task OnStopCalculationEngineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Perform calculation engine specific health checks
    /// </summary>
    protected abstract Task<Dictionary<string, HealthCheckEntry>> OnCheckCalculationEngineHealthAsync();

    /// <summary>
    /// Validate calculation input (service-specific)
    /// </summary>
    protected abstract void OnValidateCalculationInput<T>(T input, string parameterName) where T : class;

    #endregion
    
    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _performanceFlushTimer?.Dispose();
            _gpuAccelerator?.Dispose();
            _gpuContext?.Dispose();
        }
        
        base.Dispose(disposing);
    }

    #endregion
}