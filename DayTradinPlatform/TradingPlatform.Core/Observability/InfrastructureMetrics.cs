// File: TradingPlatform.Core\Observability\InfrastructureMetrics.cs

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Prometheus;

namespace TradingPlatform.Core.Observability;

/// <summary>
/// Comprehensive infrastructure metrics collection for zero-blind-spot monitoring
/// Tracks memory, CPU, network, disk, and system resource utilization with microsecond precision
/// </summary>
public class InfrastructureMetrics : IInfrastructureMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Timer _systemMetricsTimer;
    
    // Performance counters for system monitoring
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryAvailableCounter;
    private readonly PerformanceCounter? _diskReadBytesCounter;
    private readonly PerformanceCounter? _diskWriteBytesCounter;
    private readonly PerformanceCounter? _networkBytesReceivedCounter;
    private readonly PerformanceCounter? _networkBytesSentCounter;
    
    // OpenTelemetry Instruments
    private readonly Counter<long> _memoryAllocationsCounter;
    private readonly Histogram<double> _networkLatencyHistogram;
    private readonly Histogram<double> _diskIoLatencyHistogram;
    private readonly ObservableGauge<double> _cpuUtilizationGauge;
    private readonly ObservableGauge<long> _memoryUsageGauge;
    private readonly Counter<long> _gcCollectionsCounter;
    private readonly Histogram<double> _gcDurationHistogram;
    
    // Prometheus Metrics (for Grafana integration)
    private static readonly Counter PrometheusMemoryAllocations = Metrics
        .CreateCounter("infrastructure_memory_allocations_total", "Total memory allocations", new[] { "context", "generation" });
    
    private static readonly Histogram PrometheusNetworkLatency = Metrics
        .CreateHistogram("infrastructure_network_latency_microseconds", "Network latency in microseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0, 10000.0, 25000.0 }
            });
    
    private static readonly Histogram PrometheusDiskIoLatency = Metrics
        .CreateHistogram("infrastructure_disk_io_latency_microseconds", "Disk I/O latency in microseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0, 10000.0, 25000.0, 50000.0, 100000.0 }
            });
    
    private static readonly Gauge PrometheusCpuUtilization = Metrics
        .CreateGauge("infrastructure_cpu_utilization_percent", "CPU utilization percentage");
    
    private static readonly Gauge PrometheusMemoryUsage = Metrics
        .CreateGauge("infrastructure_memory_usage_bytes", "Memory usage in bytes", new[] { "type" });
    
    private static readonly Counter PrometheusGcCollections = Metrics
        .CreateCounter("infrastructure_gc_collections_total", "Total garbage collections", new[] { "generation" });
    
    private static readonly Histogram PrometheusGcDuration = Metrics
        .CreateHistogram("infrastructure_gc_duration_milliseconds", "Garbage collection duration in milliseconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 0.5, 1, 2.5, 5, 10, 25, 50, 100, 250, 500 }
            });
    
    public InfrastructureMetrics()
    {
        _meter = new Meter("TradingPlatform.Infrastructure", "1.0.0");
        
        // Initialize performance counters (Windows-specific)
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");
                _diskReadBytesCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                _diskWriteBytesCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                _networkBytesReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", GetNetworkInterface());
                _networkBytesSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", GetNetworkInterface());
            }
        }
        catch (Exception ex)
        {
            // Log performance counter initialization failure
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("PerformanceCounterInitialization");
            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to initialize performance counters: {ex.Message}");
        }
        
        // Initialize OpenTelemetry instruments
        _memoryAllocationsCounter = _meter.CreateCounter<long>(
            "infrastructure.memory.allocations",
            "allocations",
            "Total memory allocations");
        
        _networkLatencyHistogram = _meter.CreateHistogram<double>(
            "infrastructure.network.latency",
            "microseconds",
            "Network latency in microseconds");
        
        _diskIoLatencyHistogram = _meter.CreateHistogram<double>(
            "infrastructure.disk.io_latency",
            "microseconds",
            "Disk I/O latency in microseconds");
        
        _cpuUtilizationGauge = _meter.CreateObservableGauge<double>(
            "infrastructure.cpu.utilization",
            unit: "percent",
            description: "CPU utilization percentage",
            observeValues: GetCpuUtilization);
        
        _memoryUsageGauge = _meter.CreateObservableGauge<long>(
            "infrastructure.memory.usage",
            unit: "bytes", 
            description: "Memory usage in bytes",
            observeValues: GetMemoryUsage);
        
        _gcCollectionsCounter = _meter.CreateCounter<long>(
            "infrastructure.gc.collections",
            "collections",
            "Total garbage collections");
        
        _gcDurationHistogram = _meter.CreateHistogram<double>(
            "infrastructure.gc.duration",
            "milliseconds",
            "Garbage collection duration in milliseconds");
        
        // Start periodic system metrics collection
        _systemMetricsTimer = new Timer(CollectSystemMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        
        // Hook GC events for monitoring
        GC.RegisterForFullGCNotification(10, 10);
    }
    
    /// <summary>
    /// Records memory allocation events with context
    /// </summary>
    public void RecordMemoryAllocation(long bytes, string context)
    {
        var generation = GC.GetGeneration(new object());
        
        // OpenTelemetry metrics
        _memoryAllocationsCounter.Add(1,
            new KeyValuePair<string, object?>("context", context),
            new KeyValuePair<string, object?>("generation", generation.ToString()),
            new KeyValuePair<string, object?>("size_category", GetSizeCategory(bytes)));
        
        // Prometheus metrics
        PrometheusMemoryAllocations.WithLabels(context, generation.ToString()).Inc();
        
        // Create detailed activity for large allocations
        if (bytes > 1024 * 1024) // > 1MB
        {
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("LargeMemoryAllocation");
            activity?.SetTag("infrastructure.memory.bytes", bytes.ToString());
            activity?.SetTag("infrastructure.memory.context", context);
            activity?.SetTag("infrastructure.memory.generation", generation.ToString());
            activity?.SetTag("infrastructure.memory.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        }
    }
    
    /// <summary>
    /// Records network latency measurements
    /// </summary>
    public void RecordNetworkLatency(TimeSpan latency, string destination)
    {
        var latencyMicroseconds = latency.TotalMicroseconds;
        
        // OpenTelemetry metrics
        _networkLatencyHistogram.Record(latencyMicroseconds,
            new KeyValuePair<string, object?>("destination", destination),
            new KeyValuePair<string, object?>("latency_category", GetLatencyCategory(latencyMicroseconds)));
        
        // Prometheus metrics
        PrometheusNetworkLatency.Observe(latencyMicroseconds);
        
        // Create activity for high latency events
        if (latencyMicroseconds > 1000) // > 1ms
        {
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("HighNetworkLatency");
            activity?.SetTag("infrastructure.network.latency_microseconds", latencyMicroseconds.ToString("F2"));
            activity?.SetTag("infrastructure.network.destination", destination);
            activity?.SetTag("infrastructure.network.timestamp", DateTimeOffset.UtcNow.ToString("O"));
            
            if (latencyMicroseconds > 10000) // > 10ms - critical for trading
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Critical network latency: {latencyMicroseconds:F2}μs");
            }
        }
    }
    
    /// <summary>
    /// Records disk I/O operation metrics
    /// </summary>
    public void RecordDiskIO(string operation, TimeSpan latency, long bytes)
    {
        var latencyMicroseconds = latency.TotalMicroseconds;
        
        // OpenTelemetry metrics
        _diskIoLatencyHistogram.Record(latencyMicroseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("size_category", GetSizeCategory(bytes)));
        
        // Prometheus metrics
        PrometheusDiskIoLatency.Observe(latencyMicroseconds);
        
        // Create activity for slow disk operations
        if (latencyMicroseconds > 5000) // > 5ms
        {
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("SlowDiskIO");
            activity?.SetTag("infrastructure.disk.operation", operation);
            activity?.SetTag("infrastructure.disk.latency_microseconds", latencyMicroseconds.ToString("F2"));
            activity?.SetTag("infrastructure.disk.bytes", bytes.ToString());
            activity?.SetTag("infrastructure.disk.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        }
    }
    
    /// <summary>
    /// Records CPU utilization metrics
    /// </summary>
    public void RecordCpuUtilization(double percentage)
    {
        PrometheusCpuUtilization.Set(percentage);
        
        // Create activity for high CPU usage
        if (percentage > 80)
        {
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("HighCpuUtilization");
            activity?.SetTag("infrastructure.cpu.utilization_percent", percentage.ToString("F2"));
            activity?.SetTag("infrastructure.cpu.timestamp", DateTimeOffset.UtcNow.ToString("O"));
            
            if (percentage > 95)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Critical CPU utilization: {percentage:F2}%");
            }
        }
    }
    
    /// <summary>
    /// Collects system-wide metrics periodically
    /// </summary>
    private void CollectSystemMetrics(object? state)
    {
        try
        {
            // Collect CPU utilization
            if (_cpuCounter != null)
            {
                var cpuUsage = _cpuCounter.NextValue();
                RecordCpuUtilization(cpuUsage);
            }
            
            // Collect memory metrics
            var totalMemory = GC.GetTotalMemory(false);
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            
            PrometheusMemoryUsage.WithLabels("managed").Set(totalMemory);
            PrometheusGcCollections.WithLabels("0").Inc(gen0Collections);
            PrometheusGcCollections.WithLabels("1").Inc(gen1Collections);
            PrometheusGcCollections.WithLabels("2").Inc(gen2Collections);
            
            // Collect process-specific metrics
            var process = Process.GetCurrentProcess();
            PrometheusMemoryUsage.WithLabels("working_set").Set(process.WorkingSet64);
            PrometheusMemoryUsage.WithLabels("private_memory").Set(process.PrivateMemorySize64);
        }
        catch (Exception ex)
        {
            // Log metrics collection failure
            using var activity = OpenTelemetryInstrumentation.InfrastructureActivitySource.StartActivity("MetricsCollectionFailure");
            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to collect system metrics: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets CPU utilization measurements
    /// </summary>
    private IEnumerable<Measurement<double>> GetCpuUtilization()
    {
        double cpuValue = 0;
        try
        {
            if (_cpuCounter != null)
            {
                cpuValue = _cpuCounter.NextValue();
            }
        }
        catch
        {
            cpuValue = 0;
        }
        yield return new Measurement<double>(cpuValue);
    }
    
    /// <summary>
    /// Gets memory usage measurements
    /// </summary>
    private IEnumerable<Measurement<long>> GetMemoryUsage()
    {
        yield return new Measurement<long>(GC.GetTotalMemory(false), new KeyValuePair<string, object?>("type", "managed"));
        
        var process = Process.GetCurrentProcess();
        yield return new Measurement<long>(process.WorkingSet64, new KeyValuePair<string, object?>("type", "working_set"));
        yield return new Measurement<long>(process.PrivateMemorySize64, new KeyValuePair<string, object?>("type", "private"));
    }
    
    private static string GetSizeCategory(long bytes)
    {
        return bytes switch
        {
            < 1024 => "small",
            < 1024 * 1024 => "medium",
            < 1024 * 1024 * 1024 => "large",
            _ => "huge"
        };
    }
    
    private static string GetLatencyCategory(double microseconds)
    {
        return microseconds switch
        {
            < 100 => "excellent",
            < 500 => "good",
            < 1000 => "acceptable",
            < 5000 => "poor",
            _ => "critical"
        };
    }
    
    private static string GetNetworkInterface()
    {
        // Try to get the first non-loopback network interface
        try
        {
            var interfaceCategory = new PerformanceCounterCategory("Network Interface");
            var instanceNames = interfaceCategory.GetInstanceNames();
            return instanceNames.FirstOrDefault(name => !name.Contains("Loopback") && !name.Contains("isatap")) ?? "_Total";
        }
        catch
        {
            return "_Total";
        }
    }
    
    public void Dispose()
    {
        _systemMetricsTimer?.Dispose();
        _meter?.Dispose();
        _cpuCounter?.Dispose();
        _memoryAvailableCounter?.Dispose();
        _diskReadBytesCounter?.Dispose();
        _diskWriteBytesCounter?.Dispose();
        _networkBytesReceivedCounter?.Dispose();
        _networkBytesSentCounter?.Dispose();
    }
}