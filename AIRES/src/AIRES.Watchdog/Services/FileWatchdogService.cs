using System.Collections.Concurrent;
using System.Collections.Immutable;
using AIRES.Application.Interfaces;
using AIRES.Core.Configuration;
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
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;
    private bool _isRunning;
    
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
        
        try
        {
            if (!_configuration.Watchdog.Enabled)
            {
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
            
            LogInfo($"Watchdog started monitoring: {inputDir}");
            LogInfo($"Polling interval: {_configuration.Watchdog.PollingIntervalSeconds}s");
            LogInfo($"Processing threads: {_configuration.Watchdog.ProcessingThreads}");
            
            LogMethodExit();
            return await Task.FromResult(AIRESResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
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
            
            _watchers[directory] = watcher;
            LogInfo($"File watcher configured for: {directory}");
        }
        catch (Exception ex)
        {
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
                LogWarning($"File no longer exists: {filePath}");
                LogMethodExit();
                return;
            }
            
            LogInfo($"Processing file: {filePath}");
            IncrementCounter("ProcessedFiles");
            
            // Read file content
            var rawContent = await File.ReadAllTextAsync(filePath);
            
            // Process through orchestrator
            var result = await _orchestrator.GenerateResearchBookletAsync(
                rawContent,
                string.Empty, // No additional context in watchdog mode
                string.Empty, // No project structure
                string.Empty, // No codebase
                new List<string>().ToImmutableList()); // No standards
                
            if (result.IsSuccess)
            {
                LogInfo($"Successfully processed {filePath}. Booklet: {result.Value?.BookletPath}");
                IncrementCounter("SuccessfulProcessing");
                
                // Move processed file to archive
                await ArchiveProcessedFileAsync(filePath);
            }
            else
            {
                LogError($"Failed to process {filePath}: {result.ErrorMessage}");
                IncrementCounter("FailedProcessing");
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception processing file {filePath}", ex);
            IncrementCounter("ProcessingExceptions");
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