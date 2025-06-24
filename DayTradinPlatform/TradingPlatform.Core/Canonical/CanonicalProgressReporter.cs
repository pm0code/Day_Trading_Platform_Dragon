using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical progress reporting implementation for long-running operations
    /// </summary>
    public class CanonicalProgressReporter : IDisposable, IProgress<ProgressReport>
    {
        private readonly ITradingLogger? _logger;
        private readonly string _operationName;
        private readonly int _totalStages;
        private readonly Action<ProgressReport>? _progressHandler;
        private readonly object _lock = new object();
        
        private ProgressReport _currentReport;
        private DateTime _startTime;
        private DateTime? _completionTime;
        private bool _disposed;

        public CanonicalProgressReporter(
            string operationName,
            Action<ProgressReport>? progressHandler = null,
            int totalStages = 1)
        {
            _operationName = operationName;
            _progressHandler = progressHandler;
            _totalStages = totalStages;
            _startTime = DateTime.UtcNow;
            
            _currentReport = new ProgressReport
            {
                OperationName = operationName,
                ProgressPercentage = 0,
                Message = "Started",
                StartedAt = _startTime,
                IsCompleted = false,
                HasError = false
            };
        }

        public CanonicalProgressReporter(
            ITradingLogger logger,
            string operationName,
            Action<ProgressReport>? progressHandler = null,
            int totalStages = 1)
            : this(operationName, progressHandler, totalStages)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Reports progress with a percentage and message
        /// </summary>
        public void ReportProgress(double percentage, string message)
        {
            lock (_lock)
            {
                if (_currentReport.IsCompleted)
                    return;

                _currentReport.ProgressPercentage = Math.Max(0, Math.Min(100, percentage));
                _currentReport.Message = message;
                _currentReport.UpdatedAt = DateTime.UtcNow;

                // Calculate estimated time remaining
                if (percentage > 0 && percentage < 100)
                {
                    var elapsed = DateTime.UtcNow - _startTime;
                    var estimatedTotal = elapsed.TotalSeconds * (100 / percentage);
                    var remaining = estimatedTotal - elapsed.TotalSeconds;
                    _currentReport.EstimatedTimeRemaining = TimeSpan.FromSeconds(remaining);
                }

                ReportCurrentProgress();
            }
        }

        /// <summary>
        /// Reports progress for a specific stage
        /// </summary>
        public void ReportStageProgress(int stage, double stagePercentage, string message)
        {
            if (stage < 1 || stage > _totalStages)
                return;

            var overallPercentage = ((stage - 1) * 100.0 + stagePercentage) / _totalStages;
            ReportProgress(overallPercentage, $"Stage {stage}/{_totalStages}: {message}");
        }

        /// <summary>
        /// Marks the operation as complete
        /// </summary>
        public void Complete(string message = "Operation completed successfully")
        {
            lock (_lock)
            {
                if (_currentReport.IsCompleted)
                    return;

                _currentReport.ProgressPercentage = 100;
                _currentReport.Message = message;
                _currentReport.IsCompleted = true;
                _currentReport.CompletedAt = DateTime.UtcNow;
                _completionTime = DateTime.UtcNow;

                ReportCurrentProgress();
            }
        }

        /// <summary>
        /// Reports an error during the operation
        /// </summary>
        public void ReportError(Exception exception, string message)
        {
            lock (_lock)
            {
                _currentReport.HasError = true;
                _currentReport.ErrorMessage = message;
                _currentReport.LastException = exception;
                _currentReport.UpdatedAt = DateTime.UtcNow;

                ReportCurrentProgress();
            }
        }

        /// <summary>
        /// Gets the current progress report
        /// </summary>
        public ProgressReport GetCurrentReport()
        {
            lock (_lock)
            {
                return new ProgressReport
                {
                    OperationName = _currentReport.OperationName,
                    ProgressPercentage = _currentReport.ProgressPercentage,
                    Message = _currentReport.Message,
                    StartedAt = _currentReport.StartedAt,
                    UpdatedAt = _currentReport.UpdatedAt,
                    CompletedAt = _currentReport.CompletedAt,
                    EstimatedTimeRemaining = _currentReport.EstimatedTimeRemaining,
                    IsCompleted = _currentReport.IsCompleted,
                    HasError = _currentReport.HasError,
                    ErrorMessage = _currentReport.ErrorMessage,
                    LastException = _currentReport.LastException
                };
            }
        }

        /// <summary>
        /// IProgress<T> implementation
        /// </summary>
        public void Report(ProgressReport value)
        {
            ReportProgress(value.ProgressPercentage, value.Message ?? string.Empty);
        }

        /// <summary>
        /// Returns this reporter as IProgress<ProgressReport>
        /// </summary>
        public IProgress<ProgressReport> AsIProgress() => this;

        private void ReportCurrentProgress()
        {
            // Notify handler if provided
            _progressHandler?.Invoke(GetCurrentReport());

            // Log progress if logger is available
            _logger?.LogInfo($"[Progress] {_currentReport.OperationName}: {_currentReport.ProgressPercentage:F1}% - {_currentReport.Message}",
                new
                {
                    Operation = _currentReport.OperationName,
                    ProgressPercentage = _currentReport.ProgressPercentage,
                    Message = _currentReport.Message,
                    StartedAt = _currentReport.StartedAt,
                    UpdatedAt = _currentReport.UpdatedAt,
                    EstimatedTimeRemaining = _currentReport.EstimatedTimeRemaining,
                    HasError = _currentReport.HasError,
                    IsCompleted = _currentReport.IsCompleted
                });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (!_currentReport.IsCompleted && !_currentReport.HasError)
                {
                    _currentReport.Message = "Operation disposed without completion";
                    _currentReport.UpdatedAt = DateTime.UtcNow;
                    Complete("Operation was disposed");
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Progress report data structure
    /// </summary>
    public class ProgressReport
    {
        public string OperationName { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
        public string? Message { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public bool IsCompleted { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? LastException { get; set; }
    }
}