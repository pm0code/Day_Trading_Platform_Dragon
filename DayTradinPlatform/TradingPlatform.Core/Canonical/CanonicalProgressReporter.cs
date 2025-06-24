using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical progress reporting implementation for consistent progress tracking across the platform
    /// </summary>
    public class CanonicalProgressReporter : IProgress<ProgressReport>, IDisposable
    {
        private readonly string _operationName;
        private readonly string _correlationId;
        private readonly Stopwatch _stopwatch;
        private readonly ConcurrentDictionary<string, ProgressStage> _stages;
        private readonly Timer? _periodicReporter;
        private ProgressReport _currentProgress;
        private readonly object _lock = new object();

        public CanonicalProgressReporter(string operationName, bool enablePeriodicReporting = false)
        {
            _operationName = operationName;
            _correlationId = Guid.NewGuid().ToString();
            _stopwatch = Stopwatch.StartNew();
            _stages = new ConcurrentDictionary<string, ProgressStage>();
            _currentProgress = new ProgressReport { OperationName = operationName };

            TradingLogOrchestrator.Instance.LogInfo(
                $"Progress tracking started for: {operationName}",
                new { OperationName = operationName, CorrelationId = _correlationId });

            if (enablePeriodicReporting)
            {
                _periodicReporter = new Timer(
                    ReportPeriodicProgress,
                    null,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// Reports progress update
        /// </summary>
        public void Report(ProgressReport value)
        {
            lock (_lock)
            {
                _currentProgress = value;
                _currentProgress.OperationName = _operationName;
                _currentProgress.ElapsedTime = _stopwatch.Elapsed;
                
                LogProgress(_currentProgress);
            }
        }

        /// <summary>
        /// Reports progress with simple percentage
        /// </summary>
        public void ReportProgress(double percentComplete, string? message = null)
        {
            Report(new ProgressReport
            {
                PercentComplete = percentComplete,
                Message = message ?? $"{percentComplete:F1}% complete"
            });
        }

        /// <summary>
        /// Reports progress for a specific stage
        /// </summary>
        public void ReportStageProgress(string stageName, double stagePercentComplete, string? message = null)
        {
            var stage = _stages.GetOrAdd(stageName, new ProgressStage { Name = stageName });
            stage.PercentComplete = stagePercentComplete;
            stage.Message = message;
            stage.LastUpdated = DateTime.UtcNow;

            var overallProgress = CalculateOverallProgress();
            Report(new ProgressReport
            {
                PercentComplete = overallProgress,
                Message = $"{stageName}: {message ?? $"{stagePercentComplete:F1}% complete"}",
                CurrentStage = stageName,
                Stages = _stages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone())
            });
        }

        /// <summary>
        /// Marks a stage as complete
        /// </summary>
        public void CompleteStage(string stageName)
        {
            ReportStageProgress(stageName, 100, "Complete");
        }

        /// <summary>
        /// Reports an error during progress
        /// </summary>
        public void ReportError(string errorMessage, Exception? exception = null)
        {
            var errorReport = new ProgressReport
            {
                OperationName = _operationName,
                Message = $"Error: {errorMessage}",
                HasError = true,
                ErrorMessage = errorMessage,
                ElapsedTime = _stopwatch.Elapsed
            };

            TradingLogOrchestrator.Instance.LogError(
                $"Progress error for {_operationName}: {errorMessage}",
                exception,
                _operationName,
                "Operation interrupted by error",
                "Check error details and retry if appropriate",
                new { Progress = _currentProgress, CorrelationId = _correlationId });

            Report(errorReport);
        }

        /// <summary>
        /// Completes the progress reporting
        /// </summary>
        public void Complete(string? completionMessage = null)
        {
            _stopwatch.Stop();
            
            Report(new ProgressReport
            {
                PercentComplete = 100,
                Message = completionMessage ?? "Operation completed successfully",
                IsComplete = true,
                ElapsedTime = _stopwatch.Elapsed
            });

            TradingLogOrchestrator.Instance.LogInfo(
                $"Progress completed for: {_operationName}",
                new 
                { 
                    OperationName = _operationName,
                    ElapsedTime = _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"),
                    CorrelationId = _correlationId
                });
        }

        /// <summary>
        /// Creates a sub-progress reporter for nested operations
        /// </summary>
        public CanonicalProgressReporter CreateSubProgress(string subOperationName, double weightInParent = 1.0)
        {
            var subReporter = new CanonicalProgressReporter($"{_operationName} > {subOperationName}");
            
            // Link sub-progress to parent
            subReporter.ProgressChanged += (progress) =>
            {
                ReportStageProgress(subOperationName, progress.PercentComplete, progress.Message);
            };

            return subReporter;
        }

        public event Action<ProgressReport>? ProgressChanged;

        private void LogProgress(ProgressReport progress)
        {
            var logData = new
            {
                Operation = progress.OperationName,
                PercentComplete = progress.PercentComplete,
                Message = progress.Message,
                ElapsedTime = progress.ElapsedTime?.ToString(@"mm\:ss\.fff"),
                EstimatedTimeRemaining = progress.EstimatedTimeRemaining?.ToString(@"mm\:ss\.fff"),
                CurrentStage = progress.CurrentStage,
                StageCount = progress.Stages?.Count ?? 0,
                CorrelationId = _correlationId
            };

            TradingLogOrchestrator.Instance.LogInfo(
                $"[Progress] {progress.OperationName}: {progress.PercentComplete:F1}% - {progress.Message}",
                logData,
                correlationId: _correlationId);

            // Notify listeners
            ProgressChanged?.Invoke(progress);
        }

        private double CalculateOverallProgress()
        {
            if (_stages.IsEmpty) return 0;
            
            var totalProgress = _stages.Values.Sum(s => s.PercentComplete);
            return totalProgress / _stages.Count;
        }

        private void ReportPeriodicProgress(object? state)
        {
            lock (_lock)
            {
                if (_currentProgress.IsComplete || _currentProgress.HasError)
                {
                    _periodicReporter?.Dispose();
                    return;
                }

                // Estimate time remaining based on current progress
                if (_currentProgress.PercentComplete > 0 && _currentProgress.PercentComplete < 100)
                {
                    var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                    var estimatedTotalSeconds = elapsedSeconds / (_currentProgress.PercentComplete / 100);
                    var estimatedRemainingSeconds = estimatedTotalSeconds - elapsedSeconds;
                    _currentProgress.EstimatedTimeRemaining = TimeSpan.FromSeconds(estimatedRemainingSeconds);
                }

                LogProgress(_currentProgress);
            }
        }

        public void Dispose()
        {
            _periodicReporter?.Dispose();
            
            if (!_currentProgress.IsComplete && !_currentProgress.HasError)
            {
                TradingLogOrchestrator.Instance.LogWarning(
                    $"Progress reporter disposed without completion: {_operationName}",
                    new { Progress = _currentProgress, CorrelationId = _correlationId });
            }
        }
    }

    /// <summary>
    /// Progress report data structure
    /// </summary>
    public class ProgressReport
    {
        public string OperationName { get; set; } = string.Empty;
        public double PercentComplete { get; set; }
        public string? Message { get; set; }
        public string? CurrentStage { get; set; }
        public TimeSpan? ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public bool IsComplete { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, ProgressStage>? Stages { get; set; }
    }

    /// <summary>
    /// Progress stage information
    /// </summary>
    public class ProgressStage
    {
        public string Name { get; set; } = string.Empty;
        public double PercentComplete { get; set; }
        public string? Message { get; set; }
        public DateTime LastUpdated { get; set; }

        public ProgressStage Clone()
        {
            return new ProgressStage
            {
                Name = Name,
                PercentComplete = PercentComplete,
                Message = Message,
                LastUpdated = LastUpdated
            };
        }
    }

    /// <summary>
    /// Extension methods for easy progress reporting
    /// </summary>
    public static class ProgressReportingExtensions
    {
        /// <summary>
        /// Executes an operation with progress reporting
        /// </summary>
        public static async Task<T> ExecuteWithProgressAsync<T>(
            this Func<IProgress<ProgressReport>, Task<T>> operation,
            string operationName,
            bool enablePeriodicReporting = false)
        {
            using var progressReporter = new CanonicalProgressReporter(operationName, enablePeriodicReporting);
            
            try
            {
                var result = await operation(progressReporter);
                progressReporter.Complete();
                return result;
            }
            catch (Exception ex)
            {
                progressReporter.ReportError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Reports progress for a batch operation
        /// </summary>
        public static void ReportBatchProgress<T>(
            this IProgress<ProgressReport> progress,
            IList<T> items,
            int currentIndex,
            string? currentItemDescription = null)
        {
            var percentComplete = items.Count > 0 ? (currentIndex + 1) * 100.0 / items.Count : 100;
            var message = currentItemDescription ?? $"Processing item {currentIndex + 1} of {items.Count}";
            
            progress.Report(new ProgressReport
            {
                PercentComplete = percentComplete,
                Message = message
            });
        }
    }
}