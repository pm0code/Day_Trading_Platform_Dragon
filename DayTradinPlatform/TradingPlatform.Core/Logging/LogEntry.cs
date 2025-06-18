// TradingPlatform.Core.Logging.LogEntry - STRUCTURED JSON LOG ENTRY
// Nanosecond timestamps, rich context, trading-specific data
// Designed for high-performance analytics and ML processing

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Structured log entry with nanosecond precision timestamps and rich context
/// Optimized for high-frequency trading analytics and ML processing
/// </summary>
public sealed class LogEntry
{
    #region Core Properties
    
    /// <summary>
    /// Unique log entry identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    
    /// <summary>
    /// Nanosecond precision timestamp (UTC)
    /// </summary>
    [JsonPropertyName("timestamp_ns")]
    public long TimestampNanoseconds { get; init; } = GetNanosecondTimestamp();
    
    /// <summary>
    /// Human-readable timestamp (ISO 8601)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Log level
    /// </summary>
    [JsonPropertyName("level")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel Level { get; init; }
    
    /// <summary>
    /// Primary log message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    #endregion
    
    #region Context Information
    
    /// <summary>
    /// Source context information
    /// </summary>
    [JsonPropertyName("source")]
    public SourceContext Source { get; init; } = new();
    
    /// <summary>
    /// Thread context information
    /// </summary>
    [JsonPropertyName("thread")]
    public ThreadContext Thread { get; init; } = new();
    
    /// <summary>
    /// Performance context information
    /// </summary>
    [JsonPropertyName("performance")]
    public PerformanceContext? Performance { get; init; }
    
    /// <summary>
    /// Trading context information (if applicable)
    /// </summary>
    [JsonPropertyName("trading")]
    public TradingContext? Trading { get; init; }
    
    /// <summary>
    /// System context information
    /// </summary>
    [JsonPropertyName("system")]
    public SystemContext System { get; init; } = new();
    
    #endregion
    
    #region Exception and Error Information
    
    /// <summary>
    /// Exception information (if applicable)
    /// </summary>
    [JsonPropertyName("exception")]
    public ExceptionContext? Exception { get; init; }
    
    /// <summary>
    /// Operation context
    /// </summary>
    [JsonPropertyName("operation_context")]
    public string? OperationContext { get; init; }
    
    /// <summary>
    /// User impact description
    /// </summary>
    [JsonPropertyName("user_impact")]
    public string? UserImpact { get; init; }
    
    /// <summary>
    /// Troubleshooting hints
    /// </summary>
    [JsonPropertyName("troubleshooting_hints")]
    public string? TroubleshootingHints { get; init; }
    
    #endregion
    
    #region Additional Data
    
    /// <summary>
    /// Additional structured data
    /// </summary>
    [JsonPropertyName("additional_data")]
    public Dictionary<string, object>? AdditionalData { get; init; }
    
    /// <summary>
    /// Correlation ID for request tracking
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    [JsonPropertyName("tags")]
    public HashSet<string> Tags { get; init; } = new();
    
    #endregion
    
    #region ML/Analytics Properties
    
    /// <summary>
    /// Anomaly score (if computed by ML)
    /// </summary>
    [JsonPropertyName("anomaly_score")]
    public double? AnomalyScore { get; set; }
    
    /// <summary>
    /// Risk score (if applicable)
    /// </summary>
    [JsonPropertyName("risk_score")]
    public double? RiskScore { get; init; }
    
    /// <summary>
    /// Alert priority (computed)
    /// </summary>
    [JsonPropertyName("alert_priority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AlertPriority? AlertPriority { get; set; }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get nanosecond precision timestamp
    /// </summary>
    private static long GetNanosecondTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L + 
               (Stopwatch.GetTimestamp() % 1_000_000L);
    }
    
    /// <summary>
    /// Convert to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
    
    /// <summary>
    /// Create from JSON string
    /// </summary>
    public static LogEntry? FromJson(string json)
    {
        return JsonSerializer.Deserialize<LogEntry>(json, JsonSerializerOptions);
    }
    
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    #endregion
}

/// <summary>
/// Source context information
/// </summary>
public sealed class SourceContext
{
    [JsonPropertyName("service")]
    public string Service { get; init; } = "TradingPlatform";
    
    [JsonPropertyName("project")]
    public string? Project { get; init; }
    
    [JsonPropertyName("class_name")]
    public string? ClassName { get; init; }
    
    [JsonPropertyName("method_name")]
    public string? MethodName { get; init; }
    
    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }
    
    [JsonPropertyName("line_number")]
    public int? LineNumber { get; init; }
    
    [JsonPropertyName("assembly")]
    public string? Assembly { get; init; }
}

/// <summary>
/// Thread context information
/// </summary>
public sealed class ThreadContext
{
    [JsonPropertyName("thread_id")]
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
    
    [JsonPropertyName("thread_name")]
    public string? ThreadName { get; init; } = Thread.CurrentThread.Name;
    
    [JsonPropertyName("is_background")]
    public bool IsBackground { get; init; } = Thread.CurrentThread.IsBackground;
    
    [JsonPropertyName("thread_state")]
    public string ThreadState { get; init; } = Thread.CurrentThread.ThreadState.ToString();
}

/// <summary>
/// Performance context information
/// </summary>
public sealed class PerformanceContext
{
    [JsonPropertyName("duration_ns")]
    public long? DurationNanoseconds { get; init; }
    
    [JsonPropertyName("duration_ms")]
    public double? DurationMilliseconds { get; init; }
    
    [JsonPropertyName("operation")]
    public string? Operation { get; init; }
    
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;
    
    [JsonPropertyName("throughput")]
    public double? Throughput { get; init; }
    
    [JsonPropertyName("resource_usage")]
    public Dictionary<string, object>? ResourceUsage { get; init; }
    
    [JsonPropertyName("business_metrics")]
    public Dictionary<string, object>? BusinessMetrics { get; init; }
    
    [JsonPropertyName("comparison_target_ns")]
    public long? ComparisonTargetNanoseconds { get; init; }
    
    [JsonPropertyName("performance_deviation")]
    public double? PerformanceDeviation { get; init; }
}

/// <summary>
/// Trading context information
/// </summary>
public sealed class TradingContext
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }
    
    [JsonPropertyName("action")]
    public string? Action { get; init; }
    
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; init; }
    
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }
    
    [JsonPropertyName("order_id")]
    public string? OrderId { get; init; }
    
    [JsonPropertyName("strategy")]
    public string? Strategy { get; init; }
    
    [JsonPropertyName("venue")]
    public string? Venue { get; init; }
    
    [JsonPropertyName("account")]
    public string? Account { get; init; }
    
    [JsonPropertyName("market_conditions")]
    public Dictionary<string, object>? MarketConditions { get; init; }
    
    [JsonPropertyName("risk_metrics")]
    public Dictionary<string, object>? RiskMetrics { get; init; }
    
    [JsonPropertyName("execution_time_ns")]
    public long? ExecutionTimeNanoseconds { get; init; }
    
    [JsonPropertyName("slippage")]
    public decimal? Slippage { get; init; }
    
    [JsonPropertyName("fill_rate")]
    public decimal? FillRate { get; init; }
}

/// <summary>
/// System context information
/// </summary>
public sealed class SystemContext
{
    [JsonPropertyName("machine_name")]
    public string MachineName { get; init; } = Environment.MachineName;
    
    [JsonPropertyName("process_id")]
    public int ProcessId { get; init; } = Environment.ProcessId;
    
    [JsonPropertyName("environment")]
    public string? Environment { get; init; } = System.Environment.GetEnvironmentVariable("ENVIRONMENT");
    
    [JsonPropertyName("memory_usage_mb")]
    public long? MemoryUsageMB { get; init; }
    
    [JsonPropertyName("cpu_usage_percent")]
    public double? CpuUsagePercent { get; init; }
    
    [JsonPropertyName("disk_usage_percent")]
    public double? DiskUsagePercent { get; init; }
    
    [JsonPropertyName("network_latency_ms")]
    public double? NetworkLatencyMs { get; init; }
}

/// <summary>
/// Exception context information
/// </summary>
public sealed class ExceptionContext
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonPropertyName("stack_trace")]
    public string? StackTrace { get; init; }
    
    [JsonPropertyName("inner_exception")]
    public ExceptionContext? InnerException { get; init; }
    
    [JsonPropertyName("source")]
    public string? Source { get; init; }
    
    [JsonPropertyName("target_site")]
    public string? TargetSite { get; init; }
    
    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; init; }
    
    public static ExceptionContext FromException(Exception exception)
    {
        return new ExceptionContext
        {
            Type = exception.GetType().FullName ?? "Unknown",
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Source = exception.Source,
            TargetSite = exception.TargetMethod?.Name,
            InnerException = exception.InnerException != null 
                ? FromException(exception.InnerException) 
                : null,
            Data = exception.Data.Count > 0 
                ? exception.Data.Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(de => de.Key.ToString() ?? "Unknown", de => de.Value ?? "null")
                : null
        };
    }
}

/// <summary>
/// Alert priority enumeration
/// </summary>
public enum AlertPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
    Emergency = 5
}