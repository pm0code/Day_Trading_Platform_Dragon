using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Models;

public class LogAnalysisResult
{
    public required LogEntry Entry { get; init; }
    public DateTime Timestamp { get; init; }
    public LogSeverity Severity { get; init; }
    public List<string> Categories { get; init; } = new();
    public double AnomalyScore { get; init; }
    public PerformanceImpact PerformanceImpact { get; init; } = new();
    public TradingRelevance TradingRelevance { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
}

public class LogPattern
{
    public required string Type { get; init; }
    public double Confidence { get; init; }
    public required string Description { get; init; }
    public List<string> AffectedOperations { get; init; } = new();
    public LogSeverity Severity { get; init; }
    public DateTime DetectedAt { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public class PerformanceInsights
{
    public TimeSpan TimeWindow { get; init; }
    public DateTime AnalysisTimestamp { get; init; }
    public int TotalOperations { get; init; }
    public double AverageExecutionTime { get; init; }
    public double P95ExecutionTime { get; init; }
    public double P99ExecutionTime { get; init; }
    public double ErrorRate { get; init; }
    public double ThroughputOpsPerSecond { get; init; }
    public List<BottleneckOperation> BottleneckOperations { get; init; } = new();
    public ResourceUtilization ResourceUtilization { get; init; } = new();
    public TradingMetrics TradingSpecificMetrics { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
}

public class BottleneckOperation
{
    public required string OperationName { get; init; }
    public double AverageExecutionTime { get; init; }
    public int CallCount { get; init; }
    public double TotalTimePercent { get; init; }
    public List<string> OptimizationSuggestions { get; init; } = new();
}

public class ResourceUtilization
{
    public double AverageCpuPercent { get; init; }
    public double PeakCpuPercent { get; init; }
    public double AverageMemoryMB { get; init; }
    public double PeakMemoryMB { get; init; }
    public double DiskIOOperationsPerSecond { get; init; }
    public double NetworkThroughputMBps { get; init; }
}

public class TradingMetrics
{
    public int TotalOrders { get; init; }
    public int SuccessfulOrders { get; init; }
    public int FailedOrders { get; init; }
    public double AverageOrderExecutionTime { get; init; }
    public double AverageRiskCheckTime { get; init; }
    public int RiskViolations { get; init; }
    public double MarketDataLatency { get; init; }
    public int PositionUpdates { get; init; }
}

public class IntelligentAlert
{
    public required string Id { get; init; }
    public LogSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Source { get; init; }
    public List<string> Categories { get; init; } = new();
    public double AnomalyScore { get; init; }
    public List<string> Recommendations { get; init; } = new();
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public class LogSearchQuery
{
    public string? SearchText { get; init; }
    public LogLevel? MinLevel { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public List<string> Sources { get; init; } = new();
    public List<string> Categories { get; init; } = new();
    public int MaxResults { get; init; } = 1000;
    public int Skip { get; init; } = 0;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public Dictionary<string, object> Filters { get; init; } = new();
}

public class SearchResult
{
    public required LogSearchQuery Query { get; init; }
    public List<LogEntry> Results { get; init; } = new();
    public int TotalMatches { get; init; }
    public double SearchDurationMs { get; init; }
    public Dictionary<string, object> Aggregations { get; init; } = new();
}

public class PerformanceImpact
{
    public bool IsPerformanceCritical { get; init; }
    public double ExecutionTimeImpact { get; init; }
    public double MemoryImpact { get; init; }
    public double CpuImpact { get; init; }
    public List<string> AffectedComponents { get; init; } = new();
}

public class TradingRelevance
{
    public bool IsTradingCritical { get; init; }
    public string? TradingContext { get; init; }
    public double BusinessImpact { get; init; }
    public List<string> AffectedTradingOperations { get; init; } = new();
}

public class AnalyticsConfiguration
{
    public AnomalyDetectionConfig AnomalyDetection { get; init; } = new();
    public PatternDetectionConfig PatternDetection { get; init; } = new();
    public AlertConfig Alerts { get; init; } = new();
    public PerformanceConfig Performance { get; init; } = new();
}

public class AnomalyDetectionConfig
{
    public double Threshold { get; init; } = 0.8;
    public int MinSamples { get; init; } = 100;
    public TimeSpan LearningWindow { get; init; } = TimeSpan.FromHours(24);
    public bool EnableGpuAcceleration { get; init; } = true;
}

public class PatternDetectionConfig
{
    public TimeSpan DefaultWindow { get; init; } = TimeSpan.FromMinutes(15);
    public int MinOccurrences { get; init; } = 5;
    public double ConfidenceThreshold { get; init; } = 0.7;
}

public class AlertConfig
{
    public bool EnableIntelligentGrouping { get; init; } = true;
    public TimeSpan SuppressionWindow { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxAlertsPerMinute { get; init; } = 10;
}

public class PerformanceConfig
{
    public long CriticalLatencyMicroseconds { get; init; } = 100;
    public double CriticalCpuPercent { get; init; } = 80;
    public double CriticalMemoryPercent { get; init; } = 90;
    public double CriticalErrorRate { get; init; } = 0.01;
}

public enum LogSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class AlertTriggeredEventArgs : EventArgs
{
    public required IntelligentAlert Alert { get; init; }
}

public class PatternDetectedEventArgs : EventArgs
{
    public required LogPattern Pattern { get; init; }
}

public class PerformanceAnalysisEventArgs : EventArgs
{
    public required PerformanceInsights Insights { get; init; }
}