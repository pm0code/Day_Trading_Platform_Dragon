// File: TradingPlatform.ML/Monitoring/ModelPerformanceMonitor.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Performance;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Monitoring
{
    /// <summary>
    /// Monitors ML model performance in real-time
    /// </summary>
    public class ModelPerformanceMonitor : CanonicalServiceBase
    {
        private readonly ConcurrentDictionary<string, ModelPerformanceTracker> _trackers;
        private readonly ConcurrentDictionary<string, DriftDetector> _driftDetectors;
        private readonly Timer _monitoringTimer;
        private readonly Timer _alertTimer;
        private readonly LatencyTracker _predictionLatencyTracker;
        
        // Thresholds
        private const double AccuracyAlertThreshold = 0.6;
        private const double LatencyAlertThreshold = 100; // ms
        private const double DriftAlertThreshold = 0.15;
        private const int SlidingWindowSize = 1000;
        
        public ModelPerformanceMonitor(
            IServiceProvider serviceProvider,
            ITradingLogger logger)
            : base(serviceProvider, logger, "ModelPerformanceMonitor")
        {
            _trackers = new ConcurrentDictionary<string, ModelPerformanceTracker>();
            _driftDetectors = new ConcurrentDictionary<string, DriftDetector>();
            _predictionLatencyTracker = new LatencyTracker(bufferSize: 10000);
            
            // Start monitoring timers
            _monitoringTimer = new Timer(
                CollectMetrics,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
            
            _alertTimer = new Timer(
                CheckAlerts,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
            
            LogInfo("Model performance monitor initialized");
        }
        
        /// <summary>
        /// Register a model for monitoring
        /// </summary>
        public async Task<TradingResult<bool>> RegisterModelAsync(
            string modelId,
            ModelType modelType,
            ModelPerformanceBaseline baseline,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_trackers.ContainsKey(modelId))
                    {
                        return TradingResult<bool>.Failure(
                            new Exception($"Model {modelId} is already registered"));
                    }
                    
                    var tracker = new ModelPerformanceTracker
                    {
                        ModelId = modelId,
                        ModelType = modelType,
                        Baseline = baseline,
                        StartTime = DateTime.UtcNow,
                        SlidingWindow = new SlidingWindow<PredictionRecord>(SlidingWindowSize)
                    };
                    
                    _trackers[modelId] = tracker;
                    
                    // Initialize drift detector
                    _driftDetectors[modelId] = new DriftDetector(baseline);
                    
                    LogInfo($"Model {modelId} registered for monitoring",
                        additionalData: new { ModelType = modelType });
                    
                    RecordServiceMetric("ModelRegistered", 1, new { modelId });
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(RegisterModelAsync));
        }
        
        /// <summary>
        /// Record a prediction for monitoring
        /// </summary>
        public async Task<TradingResult<bool>> RecordPredictionAsync(
            string modelId,
            PricePredictionInput input,
            PricePrediction prediction,
            double latencyMs,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_trackers.TryGetValue(modelId, out var tracker))
                    {
                        return TradingResult<bool>.Failure(
                            new Exception($"Model {modelId} not registered"));
                    }
                    
                    var record = new PredictionRecord
                    {
                        Timestamp = DateTime.UtcNow,
                        Input = input,
                        Prediction = prediction,
                        LatencyMs = latencyMs
                    };
                    
                    // Add to sliding window
                    tracker.SlidingWindow.Add(record);
                    
                    // Update metrics
                    tracker.TotalPredictions++;
                    tracker.TotalLatency += latencyMs;
                    
                    // Track confidence distribution
                    var confidenceBucket = GetConfidenceBucket(prediction.Confidence);
                    tracker.ConfidenceDistribution.AddOrUpdate(
                        confidenceBucket,
                        1,
                        (key, count) => count + 1);
                    
                    // Check for drift
                    if (_driftDetectors.TryGetValue(modelId, out var driftDetector))
                    {
                        var driftScore = await driftDetector.CheckDriftAsync(input, prediction);
                        tracker.CurrentDriftScore = driftScore;
                        
                        if (driftScore > DriftAlertThreshold)
                        {
                            await RaiseDriftAlert(modelId, driftScore);
                        }
                    }
                    
                    // Record latency
                    _predictionLatencyTracker.RecordLatency(latencyMs);
                    RecordServiceMetric($"Model.{modelId}.Latency", latencyMs);
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(RecordPredictionAsync));
        }
        
        /// <summary>
        /// Record actual outcome for accuracy tracking
        /// </summary>
        public async Task<TradingResult<bool>> RecordActualOutcomeAsync(
            string modelId,
            DateTime predictionTime,
            float actualPrice,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_trackers.TryGetValue(modelId, out var tracker))
                    {
                        return TradingResult<bool>.Failure(
                            new Exception($"Model {modelId} not registered"));
                    }
                    
                    // Find matching prediction
                    var matchingRecord = tracker.SlidingWindow
                        .GetAll()
                        .FirstOrDefault(r => Math.Abs((r.Timestamp - predictionTime).TotalSeconds) < 60);
                    
                    if (matchingRecord != null)
                    {
                        matchingRecord.ActualPrice = actualPrice;
                        matchingRecord.HasActual = true;
                        
                        // Calculate error
                        var error = Math.Abs(matchingRecord.Prediction.PredictedPrice - actualPrice);
                        var errorPercent = error / actualPrice * 100;
                        
                        // Update accuracy metrics
                        tracker.TotalActuals++;
                        tracker.TotalAbsoluteError += error;
                        tracker.TotalSquaredError += error * error;
                        
                        // Check directional accuracy
                        var predictedDirection = matchingRecord.Prediction.PriceChangePercent > 0;
                        var actualDirection = actualPrice > matchingRecord.Input.Close;
                        
                        if (predictedDirection == actualDirection)
                        {
                            tracker.CorrectDirections++;
                        }
                        
                        RecordServiceMetric($"Model.{modelId}.Error", errorPercent);
                    }
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(RecordActualOutcomeAsync));
        }
        
        /// <summary>
        /// Get current performance metrics for a model
        /// </summary>
        public async Task<TradingResult<ModelPerformanceReport>> GetPerformanceReportAsync(
            string modelId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_trackers.TryGetValue(modelId, out var tracker))
                    {
                        return TradingResult<ModelPerformanceReport>.Failure(
                            new Exception($"Model {modelId} not registered"));
                    }
                    
                    var recentPredictions = tracker.SlidingWindow.GetAll().ToList();
                    var withActuals = recentPredictions.Where(p => p.HasActual).ToList();
                    
                    var report = new ModelPerformanceReport
                    {
                        ModelId = modelId,
                        ModelType = tracker.ModelType,
                        MonitoringStartTime = tracker.StartTime,
                        TotalPredictions = tracker.TotalPredictions,
                        
                        // Latency metrics
                        AverageLatencyMs = tracker.TotalPredictions > 0 
                            ? tracker.TotalLatency / tracker.TotalPredictions : 0,
                        P95LatencyMs = CalculatePercentile(
                            recentPredictions.Select(p => p.LatencyMs).ToList(), 0.95),
                        P99LatencyMs = CalculatePercentile(
                            recentPredictions.Select(p => p.LatencyMs).ToList(), 0.99),
                        
                        // Accuracy metrics
                        DirectionalAccuracy = tracker.TotalActuals > 0
                            ? (double)tracker.CorrectDirections / tracker.TotalActuals : 0,
                        MeanAbsoluteError = tracker.TotalActuals > 0
                            ? tracker.TotalAbsoluteError / tracker.TotalActuals : 0,
                        RootMeanSquaredError = tracker.TotalActuals > 0
                            ? Math.Sqrt(tracker.TotalSquaredError / tracker.TotalActuals) : 0,
                        
                        // Confidence distribution
                        ConfidenceDistribution = tracker.ConfidenceDistribution.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (double)kvp.Value / tracker.TotalPredictions),
                        
                        // Drift metrics
                        CurrentDriftScore = tracker.CurrentDriftScore,
                        DriftTrend = CalculateDriftTrend(tracker),
                        
                        // Performance vs baseline
                        PerformanceVsBaseline = CalculatePerformanceVsBaseline(tracker),
                        
                        // Alerts
                        ActiveAlerts = tracker.ActiveAlerts.ToList(),
                        
                        // Time-based analysis
                        HourlyPerformance = CalculateHourlyPerformance(recentPredictions),
                        DailyPerformance = CalculateDailyPerformance(recentPredictions),
                        
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    return TradingResult<ModelPerformanceReport>.Success(report);
                },
                nameof(GetPerformanceReportAsync));
        }
        
        /// <summary>
        /// Get aggregated performance across all models
        /// </summary>
        public async Task<TradingResult<SystemPerformanceReport>> GetSystemPerformanceAsync(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var modelReports = new List<ModelPerformanceReport>();
                    
                    foreach (var modelId in _trackers.Keys)
                    {
                        var reportResult = await GetPerformanceReportAsync(modelId, cancellationToken);
                        if (reportResult.IsSuccess)
                        {
                            modelReports.Add(reportResult.Value);
                        }
                    }
                    
                    var systemReport = new SystemPerformanceReport
                    {
                        TotalModelsMonitored = _trackers.Count,
                        ModelReports = modelReports,
                        
                        // System-wide metrics
                        SystemAverageLatency = modelReports.Any() 
                            ? modelReports.Average(r => r.AverageLatencyMs) : 0,
                        SystemAverageAccuracy = modelReports.Any()
                            ? modelReports.Average(r => r.DirectionalAccuracy) : 0,
                        SystemDriftScore = modelReports.Any()
                            ? modelReports.Average(r => r.CurrentDriftScore) : 0,
                        
                        // Health indicators
                        ModelsWithAlerts = modelReports.Count(r => r.ActiveAlerts.Any()),
                        ModelsWithHighDrift = modelReports.Count(r => r.CurrentDriftScore > DriftAlertThreshold),
                        ModelsWithPoorAccuracy = modelReports.Count(r => r.DirectionalAccuracy < AccuracyAlertThreshold),
                        
                        SystemHealth = CalculateSystemHealth(modelReports),
                        GeneratedAt = DateTime.UtcNow
                    };
                    
                    return TradingResult<SystemPerformanceReport>.Success(systemReport);
                },
                nameof(GetSystemPerformanceAsync));
        }
        
        // Helper methods
        
        private void CollectMetrics(object? state)
        {
            try
            {
                foreach (var (modelId, tracker) in _trackers)
                {
                    var recentPredictions = tracker.SlidingWindow.GetAll().ToList();
                    if (!recentPredictions.Any()) continue;
                    
                    // Calculate recent metrics
                    var recentLatency = recentPredictions.Average(p => p.LatencyMs);
                    var recentWithActuals = recentPredictions.Where(p => p.HasActual).ToList();
                    
                    if (recentWithActuals.Any())
                    {
                        var recentAccuracy = recentWithActuals.Count(p => 
                        {
                            var predictedDir = p.Prediction.PriceChangePercent > 0;
                            var actualDir = p.ActualPrice > p.Input.Close;
                            return predictedDir == actualDir;
                        }) / (double)recentWithActuals.Count;
                        
                        RecordServiceMetric($"Model.{modelId}.RecentAccuracy", recentAccuracy);
                    }
                    
                    RecordServiceMetric($"Model.{modelId}.RecentLatency", recentLatency);
                    RecordServiceMetric($"Model.{modelId}.DriftScore", tracker.CurrentDriftScore);
                }
            }
            catch (Exception ex)
            {
                LogError("Error collecting performance metrics", ex);
            }
        }
        
        private void CheckAlerts(object? state)
        {
            try
            {
                foreach (var (modelId, tracker) in _trackers)
                {
                    var alerts = new List<PerformanceAlert>();
                    
                    // Check latency
                    if (tracker.TotalPredictions > 0)
                    {
                        var avgLatency = tracker.TotalLatency / tracker.TotalPredictions;
                        if (avgLatency > LatencyAlertThreshold)
                        {
                            alerts.Add(new PerformanceAlert
                            {
                                Type = AlertType.HighLatency,
                                Severity = AlertSeverity.Warning,
                                Message = $"Average latency {avgLatency:F2}ms exceeds threshold",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    
                    // Check accuracy
                    if (tracker.TotalActuals > 100)
                    {
                        var accuracy = (double)tracker.CorrectDirections / tracker.TotalActuals;
                        if (accuracy < AccuracyAlertThreshold)
                        {
                            alerts.Add(new PerformanceAlert
                            {
                                Type = AlertType.LowAccuracy,
                                Severity = AlertSeverity.Critical,
                                Message = $"Directional accuracy {accuracy:P1} below threshold",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    
                    // Check drift
                    if (tracker.CurrentDriftScore > DriftAlertThreshold)
                    {
                        alerts.Add(new PerformanceAlert
                        {
                            Type = AlertType.ModelDrift,
                            Severity = AlertSeverity.Warning,
                            Message = $"Drift score {tracker.CurrentDriftScore:F3} exceeds threshold",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    
                    tracker.ActiveAlerts = alerts;
                    
                    // Log critical alerts
                    foreach (var alert in alerts.Where(a => a.Severity == AlertSeverity.Critical))
                    {
                        LogWarning($"Model {modelId} performance alert: {alert.Message}",
                            additionalData: alert);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error checking performance alerts", ex);
            }
        }
        
        private async Task RaiseDriftAlert(string modelId, double driftScore)
        {
            var alert = new PerformanceAlert
            {
                Type = AlertType.ModelDrift,
                Severity = AlertSeverity.Critical,
                Message = $"Model {modelId} drift detected: {driftScore:F3}",
                Timestamp = DateTime.UtcNow
            };
            
            LogWarning($"DRIFT ALERT: {alert.Message}", additionalData: alert);
            RecordServiceMetric($"Model.{modelId}.DriftAlert", 1);
            
            // In production, this would send notifications
            await Task.CompletedTask;
        }
        
        private string GetConfidenceBucket(float confidence)
        {
            return confidence switch
            {
                < 0.2f => "0-20%",
                < 0.4f => "20-40%",
                < 0.6f => "40-60%",
                < 0.8f => "60-80%",
                _ => "80-100%"
            };
        }
        
        private double CalculatePercentile(List<double> values, double percentile)
        {
            if (!values.Any()) return 0;
            
            values.Sort();
            var index = (int)Math.Ceiling(percentile * values.Count) - 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }
        
        private List<double> CalculateDriftTrend(ModelPerformanceTracker tracker)
        {
            // Simple implementation - in production would track historical drift
            return new List<double> { tracker.CurrentDriftScore };
        }
        
        private PerformanceComparison CalculatePerformanceVsBaseline(ModelPerformanceTracker tracker)
        {
            var comparison = new PerformanceComparison();
            
            if (tracker.TotalActuals > 0)
            {
                var currentAccuracy = (double)tracker.CorrectDirections / tracker.TotalActuals;
                comparison.AccuracyDelta = currentAccuracy - tracker.Baseline.ExpectedAccuracy;
            }
            
            if (tracker.TotalPredictions > 0)
            {
                var currentLatency = tracker.TotalLatency / tracker.TotalPredictions;
                comparison.LatencyDelta = currentLatency - tracker.Baseline.ExpectedLatency;
            }
            
            return comparison;
        }
        
        private Dictionary<int, double> CalculateHourlyPerformance(List<PredictionRecord> predictions)
        {
            var hourlyAccuracy = new Dictionary<int, double>();
            
            var byHour = predictions
                .Where(p => p.HasActual)
                .GroupBy(p => p.Timestamp.Hour);
            
            foreach (var group in byHour)
            {
                var correct = group.Count(p =>
                {
                    var predictedDir = p.Prediction.PriceChangePercent > 0;
                    var actualDir = p.ActualPrice > p.Input.Close;
                    return predictedDir == actualDir;
                });
                
                hourlyAccuracy[group.Key] = (double)correct / group.Count();
            }
            
            return hourlyAccuracy;
        }
        
        private Dictionary<DayOfWeek, double> CalculateDailyPerformance(List<PredictionRecord> predictions)
        {
            var dailyAccuracy = new Dictionary<DayOfWeek, double>();
            
            var byDay = predictions
                .Where(p => p.HasActual)
                .GroupBy(p => p.Timestamp.DayOfWeek);
            
            foreach (var group in byDay)
            {
                var correct = group.Count(p =>
                {
                    var predictedDir = p.Prediction.PriceChangePercent > 0;
                    var actualDir = p.ActualPrice > p.Input.Close;
                    return predictedDir == actualDir;
                });
                
                dailyAccuracy[group.Key] = (double)correct / group.Count();
            }
            
            return dailyAccuracy;
        }
        
        private string CalculateSystemHealth(List<ModelPerformanceReport> reports)
        {
            if (!reports.Any()) return "Unknown";
            
            var avgAccuracy = reports.Average(r => r.DirectionalAccuracy);
            var avgDrift = reports.Average(r => r.CurrentDriftScore);
            var modelsWithAlerts = reports.Count(r => r.ActiveAlerts.Any());
            
            if (avgAccuracy > 0.7 && avgDrift < 0.1 && modelsWithAlerts == 0)
                return "Excellent";
            else if (avgAccuracy > 0.6 && avgDrift < 0.15 && modelsWithAlerts <= 1)
                return "Good";
            else if (avgAccuracy > 0.5 && avgDrift < 0.2)
                return "Fair";
            else
                return "Poor";
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monitoringTimer?.Dispose();
                _alertTimer?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
    
    // Supporting classes
    
    internal class ModelPerformanceTracker
    {
        public string ModelId { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public ModelPerformanceBaseline Baseline { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public SlidingWindow<PredictionRecord> SlidingWindow { get; set; } = null!;
        
        public long TotalPredictions { get; set; }
        public double TotalLatency { get; set; }
        public long TotalActuals { get; set; }
        public long CorrectDirections { get; set; }
        public double TotalAbsoluteError { get; set; }
        public double TotalSquaredError { get; set; }
        
        public ConcurrentDictionary<string, long> ConfidenceDistribution { get; } = new();
        public double CurrentDriftScore { get; set; }
        public List<PerformanceAlert> ActiveAlerts { get; set; } = new();
    }
    
    internal class PredictionRecord
    {
        public DateTime Timestamp { get; set; }
        public PricePredictionInput Input { get; set; } = null!;
        public PricePrediction Prediction { get; set; } = null!;
        public double LatencyMs { get; set; }
        public float ActualPrice { get; set; }
        public bool HasActual { get; set; }
    }
    
    internal class DriftDetector
    {
        private readonly ModelPerformanceBaseline _baseline;
        private readonly Queue<double> _recentScores;
        private const int WindowSize = 100;
        
        public DriftDetector(ModelPerformanceBaseline baseline)
        {
            _baseline = baseline;
            _recentScores = new Queue<double>(WindowSize);
        }
        
        public async Task<double> CheckDriftAsync(PricePredictionInput input, PricePrediction prediction)
        {
            // Simplified drift detection - in production would use more sophisticated methods
            var featureVector = new[]
            {
                input.RSI / 100f,
                input.MACD / 10f,
                input.VolumeRatio,
                prediction.Confidence
            };
            
            var distance = 0.0;
            for (int i = 0; i < featureVector.Length && i < _baseline.BaselineFeatures.Length; i++)
            {
                distance += Math.Pow(featureVector[i] - _baseline.BaselineFeatures[i], 2);
            }
            
            var driftScore = Math.Sqrt(distance);
            
            _recentScores.Enqueue(driftScore);
            if (_recentScores.Count > WindowSize)
            {
                _recentScores.Dequeue();
            }
            
            return _recentScores.Average();
        }
    }
    
    internal class SlidingWindow<T>
    {
        private readonly Queue<T> _window;
        private readonly int _maxSize;
        private readonly object _lock = new object();
        
        public SlidingWindow(int maxSize)
        {
            _maxSize = maxSize;
            _window = new Queue<T>(maxSize);
        }
        
        public void Add(T item)
        {
            lock (_lock)
            {
                _window.Enqueue(item);
                if (_window.Count > _maxSize)
                {
                    _window.Dequeue();
                }
            }
        }
        
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                return _window.ToArray();
            }
        }
    }
    
    public class ModelPerformanceBaseline
    {
        public double ExpectedAccuracy { get; set; } = 0.65;
        public double ExpectedLatency { get; set; } = 50;
        public double[] BaselineFeatures { get; set; } = Array.Empty<double>();
        public DateTime CreatedAt { get; set; }
    }
    
    public class ModelPerformanceReport
    {
        public string ModelId { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public DateTime MonitoringStartTime { get; set; }
        public long TotalPredictions { get; set; }
        
        // Latency metrics
        public double AverageLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        
        // Accuracy metrics
        public double DirectionalAccuracy { get; set; }
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
        
        // Confidence distribution
        public Dictionary<string, double> ConfidenceDistribution { get; set; } = new();
        
        // Drift metrics
        public double CurrentDriftScore { get; set; }
        public List<double> DriftTrend { get; set; } = new();
        
        // Performance vs baseline
        public PerformanceComparison PerformanceVsBaseline { get; set; } = new();
        
        // Alerts
        public List<PerformanceAlert> ActiveAlerts { get; set; } = new();
        
        // Time-based analysis
        public Dictionary<int, double> HourlyPerformance { get; set; } = new();
        public Dictionary<DayOfWeek, double> DailyPerformance { get; set; } = new();
        
        public DateTime LastUpdated { get; set; }
    }
    
    public class SystemPerformanceReport
    {
        public int TotalModelsMonitored { get; set; }
        public List<ModelPerformanceReport> ModelReports { get; set; } = new();
        
        // System-wide metrics
        public double SystemAverageLatency { get; set; }
        public double SystemAverageAccuracy { get; set; }
        public double SystemDriftScore { get; set; }
        
        // Health indicators
        public int ModelsWithAlerts { get; set; }
        public int ModelsWithHighDrift { get; set; }
        public int ModelsWithPoorAccuracy { get; set; }
        
        public string SystemHealth { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
    
    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    public class PerformanceComparison
    {
        public double AccuracyDelta { get; set; }
        public double LatencyDelta { get; set; }
    }
    
    public enum AlertType
    {
        HighLatency,
        LowAccuracy,
        ModelDrift,
        SystemFailure
    }
    
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}