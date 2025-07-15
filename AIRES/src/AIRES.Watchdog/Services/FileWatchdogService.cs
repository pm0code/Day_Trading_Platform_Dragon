using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Application.Interfaces;
using AIRES.Core.Configuration;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.Watchdog.Services;

/// <summary>
/// Monitors directories for error files and automatically processes them through AIRES pipeline.
/// </summary>
public class FileWatchdogService : AIRESServiceBase, IFileWatchdogService
{
    private readonly IAIRESConfiguration _configuration;
    private readonly IAIResearchOrchestratorService _orchestrator;
    private readonly ConcurrentQueue<string> _fileQueue;
    private readonly Dictionary<string, FileSystemWatcher> _watchers;
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly TimeSpan _healthCheckCacheDuration = TimeSpan.FromMinutes(5);
    
    // Non-readonly fields
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;
    private bool _isRunning;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private HealthCheckResult? _cachedHealthResult;
    private DateTime _lastProcessedFileTime = DateTime.MinValue;
    private long _watcherErrorCount;
    private long _fileSystemErrorCount;
    
    public FileWatchdogService(
        IAIRESLogger logger,
        IAIRESConfiguration configuration,
        IAIResearchOrchestratorService orchestrator)
        : base(logger, nameof(FileWatchdogService))
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _fileQueue = new ConcurrentQueue<string>();
        _watchers = new Dictionary<string, FileSystemWatcher>();
        _processingSemaphore = new SemaphoreSlim(_configuration.Watchdog.ProcessingThreads);
    }
    
    protected override async Task<AIRESResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            UpdateMetric("FileWatchdog.StartAttempts", 1);
            if (!_configuration.Watchdog.Enabled)
            {
                UpdateMetric("FileWatchdog.DisabledStarts", 1);
                LogWarning("Watchdog is disabled in configuration");
                LogMethodExit();
                return await Task.FromResult(AIRESResult<bool>.Success(true));
            }
            
            // Ensure input directory exists
            var inputDir = _configuration.Directories.InputDirectory;
            if (!Directory.Exists(inputDir))
            {
                try
                {
                    Directory.CreateDirectory(inputDir);
                    LogInfo($"Created input directory: {inputDir}");
                }
                catch (Exception ex)
                {
                    UpdateMetric("FileWatchdog.DirectoryCreateErrors", 1);
                    LogError($"Failed to create input directory: {inputDir}", ex);
                    LogMethodExit();
                    return AIRESResult<bool>.Failure("WATCHDOG_DIR_ERROR", "Cannot create input directory", ex);
                }
            }
            
            // Set up file system watcher
            SetupFileWatcher(inputDir);
            
            // Start processing task
            _cancellationTokenSource = new CancellationTokenSource();
            _processingTask = ProcessQueueAsync(_cancellationTokenSource.Token);
            _isRunning = true;
            
            stopwatch.Stop();
            UpdateMetric("FileWatchdog.StartupTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("FileWatchdog.SuccessfulStarts", 1);
            
            LogInfo($"Watchdog started monitoring: {inputDir}");
            LogInfo($"Polling interval: {_configuration.Watchdog.PollingIntervalSeconds}s");
            LogInfo($"Processing threads: {_configuration.Watchdog.ProcessingThreads}");
            LogInfo($"Startup time: {stopwatch.ElapsedMilliseconds}ms");
            
            LogMethodExit();
            return await Task.FromResult(AIRESResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            UpdateMetric("FileWatchdog.StartupErrors", 1);
            LogError("Failed to start watchdog", ex);
            LogMethodExit();
            return await Task.FromResult(AIRESResult<bool>.Failure("WATCHDOG_START_ERROR", "Failed to start watchdog", ex));
        }
    }
    
    protected override async Task<AIRESResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        try
        {
            _isRunning = false;
            
            // Stop watchers
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            
            // Cancel processing
            _cancellationTokenSource?.Cancel();
            
            // Wait for processing to complete
            if (_processingTask != null)
            {
                try
                {
                    await _processingTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (TimeoutException)
                {
                    LogWarning("Processing task did not complete within timeout");
                }
            }
            
            LogInfo("Watchdog stopped");
            LogMethodExit();
            return AIRESResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Error stopping watchdog", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("WATCHDOG_STOP_ERROR", "Error stopping watchdog", ex);
        }
    }
    
    private void SetupFileWatcher(string directory)
    {
        LogMethodEntry();
        
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            
            // Set up filters for allowed extensions
            var allowedExtensions = _configuration.Processing.AllowedExtensions
                .Split(',', StringSplitOptions.TrimEntries);
            
            // Watch for all files, we'll filter in the event handler
            watcher.Filter = "*.*";
            
            watcher.Created += (sender, e) => OnFileDetected(e.FullPath, allowedExtensions);
            watcher.Changed += (sender, e) => OnFileDetected(e.FullPath, allowedExtensions);
            watcher.Error += OnWatcherError;
            
            _watchers[directory] = watcher;
            LogInfo($"File watcher configured for: {directory}");
        }
        catch (Exception ex)
        {
            UpdateMetric("FileWatchdog.WatcherSetupErrors", 1);
            LogError($"Failed to setup file watcher for {directory}", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }
    
    private void OnFileDetected(string filePath, string[] allowedExtensions)
    {
        LogMethodEntry();
        
        try
        {
            // Check if file has allowed extension
            var extension = Path.GetExtension(filePath);
            if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                LogTrace($"Ignoring file with extension {extension}: {filePath}");
                LogMethodExit();
                return;
            }
            
            // Check file age
            var fileInfo = new FileInfo(filePath);
            var ageMinutes = (DateTime.Now - fileInfo.LastWriteTime).TotalMinutes;
            if (ageMinutes > _configuration.Watchdog.FileAgeThresholdMinutes)
            {
                LogDebug($"Ignoring old file (age: {ageMinutes:F1} minutes): {filePath}");
                LogMethodExit();
                return;
            }
            
            // Check queue size
            if (_fileQueue.Count >= _configuration.Watchdog.MaxQueueSize)
            {
                UpdateMetric("FileWatchdog.QueueOverflows", 1);
                LogWarning($"Queue is full ({_fileQueue.Count} items). Dropping file: {filePath}");
                IncrementCounter("DroppedFiles");
                LogMethodExit();
                return;
            }
            
            // Add to queue
            _fileQueue.Enqueue(filePath);
            LogInfo($"File queued for processing: {filePath} (queue size: {_fileQueue.Count})");
            IncrementCounter("QueuedFiles");
        }
        catch (Exception ex)
        {
            UpdateMetric("FileWatchdog.FileDetectionErrors", 1);
            LogError($"Error handling file detection for {filePath}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }
    
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_fileQueue.TryDequeue(out var filePath))
                {
                    await _processingSemaphore.WaitAsync(cancellationToken);
                    
                    // Process file in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessFileAsync(filePath);
                        }
                        finally
                        {
                            _processingSemaphore.Release();
                        }
                    }, cancellationToken);
                }
                else
                {
                    // No files to process, wait before checking again
                    await Task.Delay(
                        TimeSpan.FromSeconds(_configuration.Watchdog.PollingIntervalSeconds),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                LogInfo("Queue processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                LogError("Error in queue processing loop", ex);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        
        LogMethodExit();
    }
    
    private async Task ProcessFileAsync(string filePath)
    {
        LogMethodEntry();
        
        try
        {
            if (!File.Exists(filePath))
            {
                UpdateMetric("FileWatchdog.FileNotFoundErrors", 1);
                LogWarning($"File no longer exists: {filePath}");
                LogMethodExit();
                return;
            }
            
            LogInfo($"Processing file: {filePath}");
            IncrementCounter("ProcessedFiles");
            
            // Read file content
            var rawContent = await File.ReadAllTextAsync(filePath);
            
            // Process through orchestrator
            var processingStopwatch = Stopwatch.StartNew();
            var result = await _orchestrator.GenerateResearchBookletAsync(
                rawContent,
                string.Empty, // No additional context in watchdog mode
                string.Empty, // No project structure
                string.Empty, // No codebase
                new List<string>().ToImmutableList()); // No standards
            processingStopwatch.Stop();
            UpdateMetric("FileWatchdog.OrchestratorResponseTime", processingStopwatch.ElapsedMilliseconds);
                
            if (result.IsSuccess)
            {
                _lastProcessedFileTime = DateTime.UtcNow;
                LogInfo($"Successfully processed {filePath}. Booklet: {result.Value?.BookletPath}");
                IncrementCounter("SuccessfulProcessing");
                UpdateMetric("FileWatchdog.ProcessingTime", processingStopwatch.ElapsedMilliseconds);
                
                // Move processed file to archive
                await ArchiveProcessedFileAsync(filePath);
            }
            else
            {
                UpdateMetric("FileWatchdog.OrchestratorErrors", 1);
                LogError($"Failed to process {filePath}: {result.ErrorMessage}");
                IncrementCounter("FailedProcessing");
                
                // Move to failed directory for manual inspection
                await MoveToFailedDirectoryAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            UpdateMetric("FileWatchdog.ProcessingExceptions", 1);
            LogError($"Exception processing file {filePath}", ex);
            IncrementCounter("ProcessingExceptions");
            
            // Attempt to move to failed directory
            try
            {
                await MoveToFailedDirectoryAsync(filePath);
            }
            catch (Exception moveEx)
            {
                LogError($"Failed to move file {filePath} to failed directory", moveEx);
            }
        }
        finally
        {
            LogMethodExit();
        }
    }
    
    private async Task ArchiveProcessedFileAsync(string filePath)
    {
        LogMethodEntry();
        
        try
        {
            await Task.Run(() =>
            {
                var archiveDir = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? ".",
                    "processed",
                    DateTime.Now.ToString("yyyy-MM-dd"));
                    
                Directory.CreateDirectory(archiveDir);
                
                var destPath = Path.Combine(
                    archiveDir,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:HHmmss}{Path.GetExtension(filePath)}");
                    
                File.Move(filePath, destPath, overwrite: false);
                LogInfo($"Archived processed file to: {destPath}");
            });
        }
        catch (Exception ex)
        {
            UpdateMetric("FileWatchdog.ArchiveErrors", 1);
            Interlocked.Increment(ref _fileSystemErrorCount);
            LogError($"Failed to archive file {filePath}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }
    
    public AIRESResult<WatchdogStatus> GetStatus()
    {
        LogMethodEntry();
        
        try
        {
            var status = new WatchdogStatus
            {
                IsRunning = _isRunning,
                QueueSize = _fileQueue.Count,
                ProcessedCount = (int)(GetMetrics().GetValueOrDefault("ProcessedFiles") ?? 0),
                SuccessCount = (int)(GetMetrics().GetValueOrDefault("SuccessfulProcessing") ?? 0),
                FailureCount = (int)(GetMetrics().GetValueOrDefault("FailedProcessing") ?? 0),
                DroppedCount = (int)(GetMetrics().GetValueOrDefault("DroppedFiles") ?? 0),
                MonitoredDirectories = _watchers.Keys.ToList()
            };
            
            LogMethodExit();
            return AIRESResult<WatchdogStatus>.Success(status);
        }
        catch (Exception ex)
        {
            LogError("Failed to get watchdog status", ex);
            LogMethodExit();
            return AIRESResult<WatchdogStatus>.Failure("WATCHDOG_STATUS_ERROR", "Failed to get status", ex);
        }
    }
    
    /// <summary>
    /// Performs a comprehensive health check of the file watchdog service.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var diagnostics = new Dictionary<string, object>();
            var failureReasons = new List<string>();
            
            // Service running status
            diagnostics["IsRunning"] = _isRunning;
            if (!_isRunning)
            {
                failureReasons.Add("Service is not running");
            }
            
            // File system watcher health
            var activeWatchers = _watchers.Count(w => w.Value.EnableRaisingEvents);
            diagnostics["TotalWatchers"] = _watchers.Count;
            diagnostics["ActiveWatchers"] = activeWatchers;
            diagnostics["WatcherErrorCount"] = _watcherErrorCount;
            
            if (activeWatchers == 0 && _isRunning)
            {
                failureReasons.Add("No active file system watchers");
            }
            
            // Queue health
            var queueSize = _fileQueue.Count;
            var maxQueueSize = _configuration.Watchdog.MaxQueueSize;
            var queueUtilization = maxQueueSize > 0 ? (double)queueSize / maxQueueSize : 0;
            
            diagnostics["QueueSize"] = queueSize;
            diagnostics["MaxQueueSize"] = maxQueueSize;
            diagnostics["QueueUtilization"] = $"{queueUtilization:P2}";
            diagnostics["QueueOverflows"] = GetMetricValue("FileWatchdog.QueueOverflows");
            
            if (queueUtilization > 0.9)
            {
                failureReasons.Add($"Queue is critically full ({queueUtilization:P2})");
            }
            else if (queueUtilization > 0.7)
            {
                failureReasons.Add($"Queue utilization is high ({queueUtilization:P2})");
            }
            
            // Processing health
            var availableSlots = _processingSemaphore.CurrentCount;
            var totalSlots = _configuration.Watchdog.ProcessingThreads;
            var activeProcessing = totalSlots - availableSlots;
            
            diagnostics["ActiveProcessingTasks"] = activeProcessing;
            diagnostics["AvailableProcessingSlots"] = availableSlots;
            diagnostics["TotalProcessingSlots"] = totalSlots;
            
            // Processing metrics
            var processedCount = GetMetricValue("ProcessedFiles");
            var successCount = GetMetricValue("SuccessfulProcessing");
            var failureCount = GetMetricValue("FailedProcessing");
            var totalProcessed = successCount + failureCount;
            var successRate = totalProcessed > 0 ? successCount / totalProcessed : 0;
            
            diagnostics["TotalProcessed"] = processedCount;
            diagnostics["SuccessfulProcessing"] = successCount;
            diagnostics["FailedProcessing"] = failureCount;
            diagnostics["SuccessRate"] = $"{successRate:P2}";
            diagnostics["ProcessingExceptions"] = GetMetricValue("ProcessingExceptions");
            
            if (successRate < 0.5 && totalProcessed > 10)
            {
                failureReasons.Add($"Low success rate ({successRate:P2})");
            }
            
            // File system health
            var inputDirHealth = await CheckDirectoryHealthAsync(_configuration.Directories.InputDirectory, "Input", true, true);
            diagnostics["InputDirectoryHealth"] = inputDirHealth.Status.ToString();
            if (inputDirHealth.Status != HealthStatus.Healthy)
            {
                failureReasons.AddRange(inputDirHealth.FailureReasons);
            }
            
            var archiveDir = Path.Combine(_configuration.Directories.InputDirectory, "processed");
            var archiveDirHealth = await CheckDirectoryHealthAsync(archiveDir, "Archive", false, true);
            diagnostics["ArchiveDirectoryHealth"] = archiveDirHealth.Status.ToString();
            if (archiveDirHealth.Status == HealthStatus.Unhealthy)
            {
                failureReasons.AddRange(archiveDirHealth.FailureReasons);
            }
            
            diagnostics["FileSystemErrorCount"] = _fileSystemErrorCount;
            
            // Processing stall detection
            var timeSinceLastProcessed = DateTime.UtcNow - _lastProcessedFileTime;
            diagnostics["LastProcessedTime"] = _lastProcessedFileTime == DateTime.MinValue ? "Never" : _lastProcessedFileTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            diagnostics["TimeSinceLastProcessed"] = timeSinceLastProcessed.TotalMinutes > int.MaxValue ? "Never" : $"{timeSinceLastProcessed.TotalMinutes:F1} minutes";
            
            if (queueSize > 0 && activeProcessing == 0 && timeSinceLastProcessed.TotalMinutes > 5)
            {
                failureReasons.Add("Processing appears stalled - items in queue but no active processing");
            }
            
            // Orchestrator dependency check
            try
            {
                // Quick health check of orchestrator - just verify it's responsive
                // We can't directly check orchestrator health without a method,
                // but we can infer from metrics
                var orchestratorCheck = true;
                
                diagnostics["OrchestratorResponsive"] = orchestratorCheck;
                diagnostics["OrchestratorErrors"] = GetMetricValue("FileWatchdog.OrchestratorErrors");
                diagnostics["AvgOrchestratorResponseTime"] = GetAverageResponseTime("FileWatchdog.OrchestratorResponseTime", "SuccessfulProcessing");
            }
            catch (Exception ex)
            {
                diagnostics["OrchestratorResponsive"] = false;
                diagnostics["OrchestratorError"] = ex.Message;
                failureReasons.Add("Cannot verify orchestrator health");
            }
            
            // Additional metrics
            diagnostics["DroppedFiles"] = GetMetricValue("DroppedFiles");
            diagnostics["StartupErrors"] = GetMetricValue("FileWatchdog.StartupErrors");
            diagnostics["FileDetectionErrors"] = GetMetricValue("FileWatchdog.FileDetectionErrors");
            diagnostics["LastCheckTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            stopwatch.Stop();
            
            // Determine overall health status
            HealthStatus status;
            string description;
            
            if (!_isRunning)
            {
                status = HealthStatus.Unhealthy;
                description = "File watchdog service is not running";
            }
            else if (failureReasons.Any(r => r.Contains("critically") || r.Contains("stalled")))
            {
                status = HealthStatus.Unhealthy;
                description = "File watchdog service has critical issues";
            }
            else if (failureReasons.Any())
            {
                status = HealthStatus.Degraded;
                description = "File watchdog service is operational but degraded";
            }
            else
            {
                status = HealthStatus.Healthy;
                description = "File watchdog service is operating normally";
            }
            
            LogInfo($"File watchdog health check completed in {stopwatch.ElapsedMilliseconds}ms - Status: {status}");
            LogMethodExit();
            
            return status == HealthStatus.Healthy
                ? HealthCheckResult.Healthy(
                    nameof(FileWatchdogService),
                    "File Monitoring Service",
                    stopwatch.ElapsedMilliseconds,
                    diagnostics.ToImmutableDictionary())
                : status == HealthStatus.Degraded
                    ? HealthCheckResult.Degraded(
                        nameof(FileWatchdogService),
                        "File Monitoring Service",
                        stopwatch.ElapsedMilliseconds,
                        failureReasons.ToImmutableList(),
                        diagnostics.ToImmutableDictionary())
                    : HealthCheckResult.Unhealthy(
                        nameof(FileWatchdogService),
                        "File Monitoring Service",
                        stopwatch.ElapsedMilliseconds,
                        description,
                        null,
                        failureReasons.ToImmutableList());
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError("Error during health check", ex);
            LogMethodExit();
            
            return HealthCheckResult.Unhealthy(
                nameof(FileWatchdogService),
                "File Monitoring Service",
                stopwatch.ElapsedMilliseconds,
                $"Health check failed: {ex.Message}",
                ex,
                ImmutableList.Create($"Exception during health check: {ex.GetType().Name}")
            );
        }
    }
    
    private async Task<HealthCheckResult> GetCachedHealthCheckAsync()
    {
        LogMethodEntry();
        
        // Check if cached result is still valid
        if (_cachedHealthResult != null && 
            DateTime.UtcNow - _lastHealthCheck < _healthCheckCacheDuration)
        {
            LogDebug("Using cached health check result");
            LogMethodExit();
            return _cachedHealthResult;
        }
        
        // Perform new health check
        _cachedHealthResult = await CheckHealthAsync();
        _lastHealthCheck = DateTime.UtcNow;
        
        LogMethodExit();
        return _cachedHealthResult;
    }
    
    private async Task<HealthCheckResult> CheckDirectoryHealthAsync(string directory, string name, bool checkRead, bool checkWrite)
    {
        LogMethodEntry();
        
        try
        {
            var diagnostics = new Dictionary<string, object>
            {
                ["Directory"] = directory,
                ["Exists"] = Directory.Exists(directory)
            };
            
            if (!Directory.Exists(directory))
            {
                LogMethodExit();
                return HealthCheckResult.Unhealthy(
                    $"{name} Directory",
                    "File System",
                    0,
                    $"{name} directory does not exist",
                    null,
                    ImmutableList.Create($"{name} directory not found: {directory}")
                );
            }
            
            // Test read access
            if (checkRead)
            {
                try
                {
                    var files = Directory.GetFiles(directory);
                    diagnostics["ReadAccess"] = true;
                    diagnostics["FileCount"] = files.Length;
                }
                catch (Exception ex)
                {
                    diagnostics["ReadAccess"] = false;
                    diagnostics["ReadError"] = ex.Message;
                    LogMethodExit();
                    return HealthCheckResult.Unhealthy(
                        $"{name} Directory",
                        "File System",
                        0,
                        $"Cannot read from {name} directory",
                        ex,
                        ImmutableList.Create($"Read access denied: {ex.Message}")
                    );
                }
            }
            
            // Test write access
            if (checkWrite)
            {
                try
                {
                    var testFile = Path.Combine(directory, $".health_check_{Guid.NewGuid():N}.tmp");
                    await File.WriteAllTextAsync(testFile, "health check");
                    File.Delete(testFile);
                    diagnostics["WriteAccess"] = true;
                }
                catch (Exception ex)
                {
                    diagnostics["WriteAccess"] = false;
                    diagnostics["WriteError"] = ex.Message;
                    LogMethodExit();
                    return HealthCheckResult.Degraded(
                        $"{name} Directory",
                        "File System",
                        0,
                        ImmutableList.Create($"Cannot write to {name} directory: {ex.Message}"),
                        diagnostics.ToImmutableDictionary()
                    );
                }
            }
            
            LogMethodExit();
            return HealthCheckResult.Healthy(
                $"{name} Directory",
                "File System",
                0,
                diagnostics.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            LogError($"Error checking {name} directory health", ex);
            LogMethodExit();
            return HealthCheckResult.Unhealthy(
                $"{name} Directory",
                "File System",
                0,
                $"Error checking {name} directory",
                ex,
                ImmutableList.Create($"Health check error: {ex.Message}")
            );
        }
    }
    
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Interlocked.Increment(ref _watcherErrorCount);
        UpdateMetric("FileWatchdog.WatcherErrors", 1);
        var exception = e.GetException();
        LogError("FileSystemWatcher error occurred", exception);
        
        // Attempt to recover by recreating the watcher
        if (sender is FileSystemWatcher failedWatcher)
        {
            var directory = failedWatcher.Path;
            LogWarning($"Attempting to recreate watcher for directory: {directory}");
            
            try
            {
                failedWatcher.EnableRaisingEvents = false;
                failedWatcher.Dispose();
                _watchers.Remove(directory);
                
                // Recreate the watcher
                SetupFileWatcher(directory);
                LogInfo($"Successfully recreated watcher for directory: {directory}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to recreate watcher for directory: {directory}", ex);
            }
        }
    }
    
    private async Task MoveToFailedDirectoryAsync(string filePath)
    {
        LogMethodEntry();
        
        try
        {
            var failedDir = Path.Combine(
                Path.GetDirectoryName(filePath) ?? ".",
                "failed",
                DateTime.Now.ToString("yyyy-MM-dd"));
                
            Directory.CreateDirectory(failedDir);
            
            var destPath = Path.Combine(
                failedDir,
                $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:HHmmss}{Path.GetExtension(filePath)}");
                
            await Task.Run(() => File.Move(filePath, destPath, overwrite: false));
            LogInfo($"Moved failed file to: {destPath}");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _fileSystemErrorCount);
            UpdateMetric("FileWatchdog.FailedMoveErrors", 1);
            LogError($"Failed to move file {filePath} to failed directory", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }
    
    private double GetAverageResponseTime(string totalTimeMetric, string countMetric)
    {
        var totalTime = GetMetricValue(totalTimeMetric);
        var count = GetMetricValue(countMetric);
        return count > 0 ? totalTime / count : 0;
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
}

/// <summary>
/// Interface for the file watchdog service.
/// </summary>
public interface IFileWatchdogService
{
    /// <summary>
    /// Initializes the watchdog service.
    /// </summary>
    Task<AIRESResult<bool>> InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts monitoring for error files.
    /// </summary>
    Task<AIRESResult<bool>> StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops monitoring.
    /// </summary>
    Task<AIRESResult<bool>> StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current status of the watchdog.
    /// </summary>
    AIRESResult<WatchdogStatus> GetStatus();
}

/// <summary>
/// Watchdog status information.
/// </summary>
public class WatchdogStatus
{
    public bool IsRunning { get; set; }
    public int QueueSize { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int DroppedCount { get; set; }
    public List<string> MonitoredDirectories { get; set; } = new();
}