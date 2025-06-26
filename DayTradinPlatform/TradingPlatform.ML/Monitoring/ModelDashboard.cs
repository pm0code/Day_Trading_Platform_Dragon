// File: TradingPlatform.ML/Monitoring/ModelDashboard.cs

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Monitoring
{
    /// <summary>
    /// Dashboard for visualizing model performance metrics
    /// </summary>
    public class ModelDashboard : CanonicalServiceBase
    {
        private readonly ModelPerformanceMonitor _performanceMonitor;
        private readonly ModelServingInfrastructure _servingInfrastructure;
        
        public ModelDashboard(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            ModelPerformanceMonitor performanceMonitor,
            ModelServingInfrastructure servingInfrastructure)
            : base(serviceProvider, logger, "ModelDashboard")
        {
            _performanceMonitor = performanceMonitor;
            _servingInfrastructure = servingInfrastructure;
        }
        
        /// <summary>
        /// Get dashboard data for a specific model
        /// </summary>
        public async Task<TradingResult<ModelDashboardData>> GetModelDashboardAsync(
            string modelId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // Get performance report
                    var performanceResult = await _performanceMonitor.GetPerformanceReportAsync(
                        modelId, cancellationToken);
                    
                    if (!performanceResult.IsSuccess)
                    {
                        return TradingResult<ModelDashboardData>.Failure(performanceResult.Error);
                    }
                    
                    // Get serving status
                    var servingResult = await _servingInfrastructure.GetServingStatusAsync();
                    var modelStatus = servingResult.IsSuccess
                        ? servingResult.Value.Models.FirstOrDefault(m => m.ModelId == modelId)
                        : null;
                    
                    var dashboardData = new ModelDashboardData
                    {
                        ModelId = modelId,
                        Performance = performanceResult.Value,
                        ServingStatus = modelStatus,
                        
                        // Calculate additional metrics
                        HealthScore = CalculateHealthScore(performanceResult.Value),
                        RecommendedActions = GenerateRecommendations(performanceResult.Value),
                        
                        // Generate visualizations data
                        LatencyChart = GenerateLatencyChartData(performanceResult.Value),
                        AccuracyChart = GenerateAccuracyChartData(performanceResult.Value),
                        DriftChart = GenerateDriftChartData(performanceResult.Value),
                        ConfidenceChart = GenerateConfidenceChartData(performanceResult.Value),
                        
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    return TradingResult<ModelDashboardData>.Success(dashboardData);
                },
                nameof(GetModelDashboardAsync));
        }
        
        /// <summary>
        /// Get system-wide dashboard
        /// </summary>
        public async Task<TradingResult<SystemDashboardData>> GetSystemDashboardAsync(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // Get system performance
                    var systemResult = await _performanceMonitor.GetSystemPerformanceAsync(cancellationToken);
                    if (!systemResult.IsSuccess)
                    {
                        return TradingResult<SystemDashboardData>.Failure(systemResult.Error);
                    }
                    
                    // Get serving status
                    var servingResult = await _servingInfrastructure.GetServingStatusAsync();
                    
                    var dashboardData = new SystemDashboardData
                    {
                        SystemPerformance = systemResult.Value,
                        ServingStatus = servingResult.IsSuccess ? servingResult.Value : null,
                        
                        // Model health summary
                        ModelHealthSummary = GenerateModelHealthSummary(systemResult.Value),
                        
                        // System alerts
                        SystemAlerts = GenerateSystemAlerts(systemResult.Value),
                        
                        // Performance trends
                        PerformanceTrends = GeneratePerformanceTrends(systemResult.Value),
                        
                        // Resource utilization
                        ResourceUtilization = await GetResourceUtilizationAsync(),
                        
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    return TradingResult<SystemDashboardData>.Success(dashboardData);
                },
                nameof(GetSystemDashboardAsync));
        }
        
        /// <summary>
        /// Generate performance summary report
        /// </summary>
        public async Task<TradingResult<string>> GeneratePerformanceReportAsync(
            string modelId,
            ReportFormat format = ReportFormat.Markdown,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var dashboardResult = await GetModelDashboardAsync(modelId, cancellationToken);
                    if (!dashboardResult.IsSuccess)
                    {
                        return TradingResult<string>.Failure(dashboardResult.Error);
                    }
                    
                    var dashboard = dashboardResult.Value;
                    var report = format switch
                    {
                        ReportFormat.Markdown => GenerateMarkdownReport(dashboard),
                        ReportFormat.Html => GenerateHtmlReport(dashboard),
                        ReportFormat.Json => GenerateJsonReport(dashboard),
                        _ => throw new NotSupportedException($"Report format {format} not supported")
                    };
                    
                    return TradingResult<string>.Success(report);
                },
                nameof(GeneratePerformanceReportAsync));
        }
        
        // Helper methods
        
        private double CalculateHealthScore(ModelPerformanceReport performance)
        {
            var score = 100.0;
            
            // Deduct for poor accuracy
            if (performance.DirectionalAccuracy < 0.6)
                score -= 20 * (0.6 - performance.DirectionalAccuracy);
            
            // Deduct for high latency
            if (performance.AverageLatencyMs > 100)
                score -= Math.Min(20, (performance.AverageLatencyMs - 100) / 10);
            
            // Deduct for drift
            if (performance.CurrentDriftScore > 0.1)
                score -= Math.Min(20, performance.CurrentDriftScore * 100);
            
            // Deduct for alerts
            score -= performance.ActiveAlerts.Count * 5;
            
            return Math.Max(0, Math.Min(100, score));
        }
        
        private List<string> GenerateRecommendations(ModelPerformanceReport performance)
        {
            var recommendations = new List<string>();
            
            if (performance.DirectionalAccuracy < 0.6)
            {
                recommendations.Add("Consider retraining model with recent data");
                recommendations.Add("Review feature engineering for improvements");
            }
            
            if (performance.AverageLatencyMs > 100)
            {
                recommendations.Add("Optimize feature extraction pipeline");
                recommendations.Add("Consider model quantization or pruning");
            }
            
            if (performance.CurrentDriftScore > 0.15)
            {
                recommendations.Add("Investigate data distribution changes");
                recommendations.Add("Schedule model retraining");
            }
            
            if (performance.ConfidenceDistribution.GetValueOrDefault("80-100%", 0) < 0.3)
            {
                recommendations.Add("Model confidence is low - review training data quality");
            }
            
            return recommendations;
        }
        
        private ChartData GenerateLatencyChartData(ModelPerformanceReport performance)
        {
            return new ChartData
            {
                Title = "Prediction Latency",
                Type = ChartType.Line,
                XAxis = "Time",
                YAxis = "Latency (ms)",
                Series = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name = "Average",
                        Data = new[] { performance.AverageLatencyMs }
                    },
                    new ChartSeries
                    {
                        Name = "P95",
                        Data = new[] { performance.P95LatencyMs }
                    },
                    new ChartSeries
                    {
                        Name = "P99",
                        Data = new[] { performance.P99LatencyMs }
                    }
                }
            };
        }
        
        private ChartData GenerateAccuracyChartData(ModelPerformanceReport performance)
        {
            var hourlyData = performance.HourlyPerformance
                .OrderBy(kvp => kvp.Key)
                .ToList();
            
            return new ChartData
            {
                Title = "Hourly Accuracy",
                Type = ChartType.Bar,
                XAxis = "Hour of Day",
                YAxis = "Directional Accuracy",
                Series = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name = "Accuracy",
                        Data = hourlyData.Select(kvp => kvp.Value).ToArray()
                    }
                }
            };
        }
        
        private ChartData GenerateDriftChartData(ModelPerformanceReport performance)
        {
            return new ChartData
            {
                Title = "Model Drift",
                Type = ChartType.Line,
                XAxis = "Time",
                YAxis = "Drift Score",
                Series = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name = "Drift",
                        Data = performance.DriftTrend.ToArray()
                    }
                }
            };
        }
        
        private ChartData GenerateConfidenceChartData(ModelPerformanceReport performance)
        {
            var buckets = new[] { "0-20%", "20-40%", "40-60%", "60-80%", "80-100%" };
            var values = buckets.Select(b => 
                performance.ConfidenceDistribution.GetValueOrDefault(b, 0)).ToArray();
            
            return new ChartData
            {
                Title = "Confidence Distribution",
                Type = ChartType.Pie,
                Series = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name = "Distribution",
                        Data = values
                    }
                }
            };
        }
        
        private List<ModelHealthStatus> GenerateModelHealthSummary(SystemPerformanceReport system)
        {
            return system.ModelReports.Select(report => new ModelHealthStatus
            {
                ModelId = report.ModelId,
                Health = CalculateHealthScore(report),
                Status = report.ActiveAlerts.Any() ? "Alert" : "Normal",
                LastUpdate = report.LastUpdated
            }).ToList();
        }
        
        private List<SystemAlert> GenerateSystemAlerts(SystemPerformanceReport system)
        {
            var alerts = new List<SystemAlert>();
            
            if (system.SystemAverageAccuracy < 0.6)
            {
                alerts.Add(new SystemAlert
                {
                    Level = "Critical",
                    Message = $"System average accuracy {system.SystemAverageAccuracy:P1} is below threshold",
                    Timestamp = DateTime.UtcNow
                });
            }
            
            if (system.ModelsWithHighDrift > 0)
            {
                alerts.Add(new SystemAlert
                {
                    Level = "Warning",
                    Message = $"{system.ModelsWithHighDrift} models showing high drift",
                    Timestamp = DateTime.UtcNow
                });
            }
            
            return alerts;
        }
        
        private PerformanceTrend GeneratePerformanceTrends(SystemPerformanceReport system)
        {
            // Simplified - in production would track historical data
            return new PerformanceTrend
            {
                AccuracyTrend = "Stable",
                LatencyTrend = "Improving",
                DriftTrend = system.SystemDriftScore > 0.1 ? "Increasing" : "Stable"
            };
        }
        
        private async Task<ResourceUtilization> GetResourceUtilizationAsync()
        {
            // Simplified - in production would get actual metrics
            return new ResourceUtilization
            {
                CpuUsage = 45.2,
                MemoryUsage = 62.8,
                GpuUsage = 0, // Not using GPU yet
                NetworkBandwidth = 12.5
            };
        }
        
        private string GenerateMarkdownReport(ModelDashboardData dashboard)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"# Model Performance Report: {dashboard.ModelId}");
            sb.AppendLine($"*Generated: {dashboard.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC*");
            sb.AppendLine();
            
            sb.AppendLine($"## Health Score: {dashboard.HealthScore:F1}/100");
            sb.AppendLine();
            
            sb.AppendLine("## Performance Metrics");
            sb.AppendLine($"- **Directional Accuracy**: {dashboard.Performance.DirectionalAccuracy:P1}");
            sb.AppendLine($"- **Average Latency**: {dashboard.Performance.AverageLatencyMs:F1}ms");
            sb.AppendLine($"- **Drift Score**: {dashboard.Performance.CurrentDriftScore:F3}");
            sb.AppendLine($"- **Total Predictions**: {dashboard.Performance.TotalPredictions:N0}");
            sb.AppendLine();
            
            if (dashboard.Performance.ActiveAlerts.Any())
            {
                sb.AppendLine("## Active Alerts");
                foreach (var alert in dashboard.Performance.ActiveAlerts)
                {
                    sb.AppendLine($"- **{alert.Severity}**: {alert.Message}");
                }
                sb.AppendLine();
            }
            
            if (dashboard.RecommendedActions.Any())
            {
                sb.AppendLine("## Recommendations");
                foreach (var action in dashboard.RecommendedActions)
                {
                    sb.AppendLine($"- {action}");
                }
            }
            
            return sb.ToString();
        }
        
        private string GenerateHtmlReport(ModelDashboardData dashboard)
        {
            // Simplified HTML generation
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Model Performance Report: {dashboard.ModelId}</title>
</head>
<body>
    <h1>Model Performance Report: {dashboard.ModelId}</h1>
    <p>Health Score: {dashboard.HealthScore:F1}/100</p>
    <p>Accuracy: {dashboard.Performance.DirectionalAccuracy:P1}</p>
    <p>Latency: {dashboard.Performance.AverageLatencyMs:F1}ms</p>
</body>
</html>";
        }
        
        private string GenerateJsonReport(ModelDashboardData dashboard)
        {
            return System.Text.Json.JsonSerializer.Serialize(dashboard, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }
    
    // Supporting classes
    
    public class ModelDashboardData
    {
        public string ModelId { get; set; } = string.Empty;
        public ModelPerformanceReport Performance { get; set; } = null!;
        public ModelStatus? ServingStatus { get; set; }
        public double HealthScore { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
        
        // Chart data
        public ChartData LatencyChart { get; set; } = null!;
        public ChartData AccuracyChart { get; set; } = null!;
        public ChartData DriftChart { get; set; } = null!;
        public ChartData ConfidenceChart { get; set; } = null!;
        
        public DateTime LastUpdated { get; set; }
    }
    
    public class SystemDashboardData
    {
        public SystemPerformanceReport SystemPerformance { get; set; } = null!;
        public ModelServingStatus? ServingStatus { get; set; }
        public List<ModelHealthStatus> ModelHealthSummary { get; set; } = new();
        public List<SystemAlert> SystemAlerts { get; set; } = new();
        public PerformanceTrend PerformanceTrends { get; set; } = null!;
        public ResourceUtilization ResourceUtilization { get; set; } = null!;
        public DateTime LastUpdated { get; set; }
    }
    
    public class ChartData
    {
        public string Title { get; set; } = string.Empty;
        public ChartType Type { get; set; }
        public string XAxis { get; set; } = string.Empty;
        public string YAxis { get; set; } = string.Empty;
        public List<ChartSeries> Series { get; set; } = new();
    }
    
    public class ChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public double[] Data { get; set; } = Array.Empty<double>();
    }
    
    public class ModelHealthStatus
    {
        public string ModelId { get; set; } = string.Empty;
        public double Health { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
    }
    
    public class SystemAlert
    {
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    public class PerformanceTrend
    {
        public string AccuracyTrend { get; set; } = string.Empty;
        public string LatencyTrend { get; set; } = string.Empty;
        public string DriftTrend { get; set; } = string.Empty;
    }
    
    public class ResourceUtilization
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double GpuUsage { get; set; }
        public double NetworkBandwidth { get; set; }
    }
    
    public enum ChartType
    {
        Line,
        Bar,
        Pie,
        Scatter
    }
    
    public enum ReportFormat
    {
        Markdown,
        Html,
        Json
    }
}