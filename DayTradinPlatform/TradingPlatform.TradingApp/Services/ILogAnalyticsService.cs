// TradingPlatform.TradingApp.Services.ILogAnalyticsService
// Interface for AI-powered log analytics and pattern recognition
// Supports RTX GPU acceleration for ML processing

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Services;

/// <summary>
/// Interface for AI-powered log analytics and pattern recognition
/// Provides intelligent analysis, anomaly detection, and performance insights
/// </summary>
public interface ILogAnalyticsService
{
    #region Pattern Analysis
    
    /// <summary>
    /// Analyze log patterns for anomalies and trends
    /// </summary>
    Task<PatternAnalysisResult> AnalyzePatternAsync(IEnumerable<LogEntry> entries);
    
    /// <summary>
    /// Detect performance degradation patterns
    /// </summary>
    Task<PerformanceTrendResult> AnalyzePerformanceTrendAsync(IEnumerable<LogEntry> entries);
    
    /// <summary>
    /// Identify trading efficiency patterns
    /// </summary>
    Task<TradingEfficiencyResult> AnalyzeTradingEfficiencyAsync(IEnumerable<LogEntry> entries);
    
    #endregion
    
    #region AI/ML Processing
    
    /// <summary>
    /// Process logs with AI/ML models for insights
    /// </summary>
    Task<AiInsightsResult> ProcessWithAiAsync(IEnumerable<LogEntry> entries);
    
    /// <summary>
    /// Calculate anomaly scores using ML models
    /// </summary>
    Task<double> CalculateAnomalyScoreAsync(LogEntry entry);
    
    /// <summary>
    /// Predict potential system issues
    /// </summary>
    Task<PredictionResult> PredictSystemIssuesAsync(IEnumerable<LogEntry> recentEntries);
    
    #endregion
    
    #region Performance Analytics
    
    /// <summary>
    /// Calculate real-time performance metrics
    /// </summary>
    Task<PerformanceMetrics> CalculatePerformanceMetricsAsync(IEnumerable<LogEntry> entries);
    
    /// <summary>
    /// Generate performance recommendations
    /// </summary>
    Task<List<PerformanceRecommendation>> GenerateRecommendationsAsync(PerformanceMetrics metrics);
    
    #endregion
    
    #region Alert Management
    
    /// <summary>
    /// Process and categorize alerts intelligently
    /// </summary>
    Task<List<IntelligentAlert>> ProcessAlertsAsync(IEnumerable<LogEntry> entries);
    
    /// <summary>
    /// Prioritize alerts based on trading impact
    /// </summary>
    Task<List<IntelligentAlert>> PrioritizeAlertsAsync(List<IntelligentAlert> alerts);
    
    #endregion
    
    #region Data Export and Reporting
    
    /// <summary>
    /// Export log data in various formats
    /// </summary>
    Task<ExportResult> ExportLogsAsync(IEnumerable<LogEntry> entries, ExportFormat format);
    
    /// <summary>
    /// Generate comprehensive analytics report
    /// </summary>
    Task<AnalyticsReport> GenerateReportAsync(DateTime startTime, DateTime endTime);
    
    #endregion
    
    #region Configuration and Events
    
    /// <summary>
    /// Event fired when new patterns are detected
    /// </summary>
    event EventHandler<PatternDetectedEventArgs>? PatternDetected;
    
    /// <summary>
    /// Event fired when anomalies are detected
    /// </summary>
    event EventHandler<AnomalyDetectedEventArgs>? AnomalyDetected;
    
    /// <summary>
    /// Update AI/ML model configuration
    /// </summary>
    Task UpdateModelConfigurationAsync(AiConfiguration config);
    
    #endregion
}

#region Result Models

/// <summary>
/// Pattern analysis result
/// </summary>
public class PatternAnalysisResult
{
    public string PatternType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> AffectedMethods { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Performance trend analysis result
/// </summary>
public class PerformanceTrendResult
{
    public string TrendDirection { get; set; } = string.Empty; // Improving, Degrading, Stable
    public double TrendStrength { get; set; }
    public Dictionary<string, double> MetricChanges { get; set; } = new();
    public List<string> PerformanceIssues { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
}

/// <summary>
/// Trading efficiency analysis result
/// </summary>
public class TradingEfficiencyResult
{
    public double OverallEfficiency { get; set; }
    public double AverageLatency { get; set; }
    public double FillRate { get; set; }
    public double AverageSlippage { get; set; }
    public Dictionary<string, double> SymbolPerformance { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
}

/// <summary>
/// AI insights processing result
/// </summary>
public class AiInsightsResult
{
    public double ModelConfidence { get; set; }
    public List<string> KeyInsights { get; set; } = new();
    public List<AnomalyDetection> DetectedAnomalies { get; set; } = new();
    public Dictionary<string, object> ModelMetrics { get; set; } = new();
    public DateTime NextModelUpdate { get; set; }
}

/// <summary>
/// System issue prediction result
/// </summary>
public class PredictionResult
{
    public List<PredictedIssue> PredictedIssues { get; set; } = new();
    public double OverallRiskScore { get; set; }
    public Dictionary<string, double> ComponentRiskScores { get; set; } = new();
    public List<string> PreventiveMeasures { get; set; } = new();
}

/// <summary>
/// Performance metrics calculation result
/// </summary>
public class PerformanceMetrics
{
    public double AverageTradingLatency { get; set; }
    public double AverageOrderExecutionTime { get; set; }
    public double SystemHealthScore { get; set; }
    public double ThroughputRate { get; set; }
    public Dictionary<string, double> ComponentLatencies { get; set; } = new();
    public Dictionary<string, int> ErrorCounts { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Performance recommendation
/// </summary>
public class PerformanceRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // High, Medium, Low
    public double PotentialImprovement { get; set; }
    public List<string> ImplementationSteps { get; set; } = new();
}

/// <summary>
/// Intelligent alert with AI-powered categorization
/// </summary>
public class IntelligentAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
    public string Category { get; set; } = string.Empty;
    public double ImpactScore { get; set; }
    public DateTime Timestamp { get; set; }
    public LogEntry SourceLogEntry { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Anomaly detection result
/// </summary>
public class AnomalyDetection
{
    public string Type { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Description { get; set; } = string.Empty;
    public LogEntry AffectedEntry { get; set; } = new();
    public List<string> PossibleCauses { get; set; } = new();
}

/// <summary>
/// Predicted issue
/// </summary>
public class PredictedIssue
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Probability { get; set; }
    public TimeSpan EstimatedTimeToOccurrence { get; set; }
    public string Severity { get; set; } = string.Empty;
    public List<string> PreventionSteps { get; set; } = new();
}

/// <summary>
/// Export result
/// </summary>
public class ExportResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public ExportFormat Format { get; set; }
    public int RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Comprehensive analytics report
/// </summary>
public class AnalyticsReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
    public List<PatternAnalysisResult> DetectedPatterns { get; set; } = new();
    public List<IntelligentAlert> CriticalAlerts { get; set; } = new();
    public List<PerformanceRecommendation> Recommendations { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
}

/// <summary>
/// Export format enumeration
/// </summary>
public enum ExportFormat
{
    Json,
    Csv,
    Excel,
    Parquet
}

#endregion

#region Event Args

/// <summary>
/// Pattern detected event arguments
/// </summary>
public class PatternDetectedEventArgs : EventArgs
{
    public PatternAnalysisResult Pattern { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Anomaly detected event arguments
/// </summary>
public class AnomalyDetectedEventArgs : EventArgs
{
    public AnomalyDetection Anomaly { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

#endregion