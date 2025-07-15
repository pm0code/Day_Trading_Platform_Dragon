using System;
using System.Collections.Generic;

namespace AIRES.Core.Models;

/// <summary>
/// Information about a detected GPU.
/// </summary>
public class GpuInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MemoryTotalMB { get; set; }
    public int MemoryAvailableMB { get; set; }
    public int ComputeCapability { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public bool SupportsFloat16 { get; set; }
    public bool SupportsBFloat16 { get; set; }
}

/// <summary>
/// Detailed GPU capabilities and recommendations.
/// </summary>
public class GpuCapabilities
{
    public int GpuId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalMemoryMB { get; set; }
    public int AvailableMemoryMB { get; set; }
    public int ComputeCapability { get; set; }
    public bool SupportsFloat16 { get; set; }
    public bool SupportsBFloat16 { get; set; }
    public int RecommendedInstanceCount { get; set; }
    public List<string> RecommendedModels { get; set; } = new();
}

/// <summary>
/// GPU health status information.
/// </summary>
public class GpuHealthStatus
{
    public int GpuId { get; set; }
    public int Temperature { get; set; }
    public int GpuUtilization { get; set; }
    public int MemoryUtilization { get; set; }
    public int MemoryUsedMB { get; set; }
    public int MemoryTotalMB { get; set; }
    public float PowerDraw { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// GPU instance information.
/// </summary>
public class GpuInstance
{
    public string Id { get; set; } = string.Empty;
    public int GpuId { get; set; }
    public int Port { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public DateTime? LastError { get; set; }
    public double HealthScore { get; set; } = 1.0;
    public int MaxMemoryMB { get; set; }
    public List<string> SupportedModels { get; set; } = new();
}

/// <summary>
/// Model requirements for instance selection.
/// </summary>
public class ModelRequirements
{
    public string ModelName { get; set; } = string.Empty;
    public int EstimatedMemoryMB { get; set; }
    public int? PreferredGpuId { get; set; }
    public bool RequiresFloat16 { get; set; }
    public bool RequiresBFloat16 { get; set; }
}

/// <summary>
/// Load balancer health status.
/// </summary>
public class LoadBalancerHealth
{
    public int TotalInstances { get; set; }
    public int HealthyInstances { get; set; }
    public List<InstanceHealth> Instances { get; set; } = new();
}

/// <summary>
/// Individual instance health information.
/// </summary>
public class InstanceHealth
{
    public string InstanceId { get; set; } = string.Empty;
    public int GpuId { get; set; }
    public int Port { get; set; }
    public bool IsHealthy { get; set; }
    public double HealthScore { get; set; }
    public int ActiveRequests { get; set; }
    public double SuccessRate { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public int GpuTemperature { get; set; }
    public int GpuUtilization { get; set; }
    public int MemoryUtilization { get; set; }
}