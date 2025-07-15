using System;

namespace AIRES.Infrastructure.AI.Models;

/// <summary>
/// Represents an Ollama instance running on a specific GPU.
/// </summary>
public class OllamaInstance
{
    /// <summary>
    /// Gets the GPU ID (0-based index).
    /// </summary>
    public int GpuId { get; init; }
    
    /// <summary>
    /// Gets the port number for this instance.
    /// </summary>
    public int Port { get; init; }
    
    /// <summary>
    /// Gets the base URL for this instance.
    /// </summary>
    public string BaseUrl => $"http://localhost:{Port}";
    
    /// <summary>
    /// Gets or sets whether this instance is currently healthy.
    /// </summary>
    public bool IsHealthy { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the last health check time.
    /// </summary>
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the number of active requests.
    /// </summary>
    public int ActiveRequests { get; set; }
    
    /// <summary>
    /// Gets or sets the total requests processed.
    /// </summary>
    public long TotalRequests { get; set; }
    
    /// <summary>
    /// Gets or sets the total errors encountered.
    /// </summary>
    public long TotalErrors { get; set; }
    
    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTime { get; set; }
    
    /// <summary>
    /// Gets a descriptive name for this instance.
    /// </summary>
    public string Name => $"Ollama-GPU{GpuId}";
}