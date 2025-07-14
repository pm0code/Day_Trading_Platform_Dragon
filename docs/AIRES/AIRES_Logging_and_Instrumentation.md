# AIRES Comprehensive Logging and Instrumentation Specification

**Version**: 1.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Critical**: MANDATORY for all AIRES components

## 🎯 Executive Summary

AIRES implements **COMPREHENSIVE CANONICAL LOGGING** to ensure complete observability during autonomous operation. Every action, decision, and state change is logged with structured data for:
1. **Real-time status monitoring**
2. **Debugging and error resolution**
3. **Performance analysis**
4. **Audit trail maintenance**

## 📊 Logging Architecture

### Canonical Logger Interface

```csharp
namespace AIRES.Foundation.Logging
{
    public interface IAIRESLogger
    {
        // Structured logging with correlation
        void LogTrace(string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, Exception? ex = null, params object[] args);
        void LogFatal(string message, Exception? ex = null, params object[] args);
        
        // Metrics and instrumentation
        void LogMetric(string metricName, double value, Dictionary<string, string>? tags = null);
        void LogEvent(string eventName, Dictionary<string, object>? properties = null);
        void LogDuration(string operationName, TimeSpan duration, Dictionary<string, string>? tags = null);
        
        // Correlation and context
        IDisposable BeginScope(string scopeName, Dictionary<string, object>? properties = null);
        void SetCorrelationId(string correlationId);
        string GetCorrelationId();
    }
}
```

### AIRESServiceBase with Mandatory Logging

```csharp
public abstract class AIRESServiceBase
{
    protected IAIRESLogger Logger { get; }
    protected string ServiceName { get; }
    private readonly Stopwatch _methodTimer = new();
    
    protected AIRESServiceBase(IAIRESLogger logger, string serviceName)
    {
        Logger = logger;
        ServiceName = serviceName;
        Logger.LogInfo("Service {ServiceName} initializing", ServiceName);
    }
    
    protected void LogMethodEntry([CallerMemberName] string methodName = "")
    {
        _methodTimer.Restart();
        Logger.LogTrace("[{ServiceName}] ENTRY: {MethodName} | Thread: {ThreadId} | Correlation: {CorrelationId}",
            ServiceName, methodName, Thread.CurrentThread.ManagedThreadId, Logger.GetCorrelationId());
    }
    
    protected void LogMethodExit([CallerMemberName] string methodName = "")
    {
        var duration = _methodTimer.Elapsed;
        Logger.LogTrace("[{ServiceName}] EXIT: {MethodName} | Duration: {Duration}ms | Correlation: {CorrelationId}",
            ServiceName, methodName, duration.TotalMilliseconds, Logger.GetCorrelationId());
        Logger.LogMetric($"{ServiceName}.{methodName}.Duration", duration.TotalMilliseconds);
    }
}
```

## 🔍 Comprehensive Logging Points

### 1. Watchdog Layer Logging

```csharp
public class AIRESWatchdogService : AIRESServiceBase
{
    public async Task MonitorDirectoryAsync()
    {
        LogMethodEntry();
        
        Logger.LogInfo("Watchdog started monitoring directory: {Directory}", _config.InputDirectory);
        Logger.LogEvent("WatchdogStarted", new Dictionary<string, object>
        {
            ["Directory"] = _config.InputDirectory,
            ["PollingInterval"] = _config.PollingIntervalSeconds,
            ["FilePattern"] = _config.FilePattern
        });
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                Logger.LogDebug("Scanning directory for new files");
                var files = Directory.GetFiles(_config.InputDirectory, _config.FilePattern);
                
                Logger.LogMetric("Watchdog.FilesDetected", files.Length, new Dictionary<string, string>
                {
                    ["Directory"] = _config.InputDirectory
                });
                
                foreach (var file in files)
                {
                    Logger.LogInfo("New file detected: {FileName} | Size: {FileSize} bytes", 
                        Path.GetFileName(file), new FileInfo(file).Length);
                    
                    await ProcessFileAsync(file);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds));
            }
            catch (Exception ex)
            {
                Logger.LogError("Watchdog error during scan", ex);
                Logger.LogEvent("WatchdogError", new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["Directory"] = _config.InputDirectory
                });
            }
        }
        
        LogMethodExit();
    }
}
```

### 2. File Processing Logging

```csharp
public async Task<AIRESResult<ErrorBatch>> ProcessFileAsync(string filePath)
{
    LogMethodEntry();
    var correlationId = Guid.NewGuid().ToString();
    Logger.SetCorrelationId(correlationId);
    
    using (Logger.BeginScope("FileProcessing", new Dictionary<string, object>
    {
        ["FilePath"] = filePath,
        ["CorrelationId"] = correlationId,
        ["StartTime"] = DateTime.UtcNow
    }))
    {
        Logger.LogInfo("Starting file processing | File: {FilePath} | Correlation: {CorrelationId}", 
            filePath, correlationId);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Parse file
            Logger.LogDebug("Parsing error file");
            var parseResult = await _errorParser.ParseAsync(filePath);
            Logger.LogDuration("ErrorParsing", stopwatch.Elapsed);
            
            if (!parseResult.IsSuccess)
            {
                Logger.LogWarning("Failed to parse file: {Reason}", parseResult.ErrorMessage);
                return parseResult;
            }
            
            Logger.LogInfo("Successfully parsed {ErrorCount} errors from file", 
                parseResult.Value.Errors.Count);
            
            // Process through pipeline
            Logger.LogDebug("Submitting to AI pipeline");
            var pipelineResult = await _aiPipeline.ProcessAsync(parseResult.Value);
            Logger.LogDuration("AIPipeline.Total", stopwatch.Elapsed);
            
            Logger.LogEvent("FileProcessed", new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["ErrorCount"] = parseResult.Value.Errors.Count,
                ["Duration"] = stopwatch.Elapsed.TotalSeconds,
                ["Success"] = pipelineResult.IsSuccess
            });
            
            LogMethodExit();
            return pipelineResult;
        }
        catch (Exception ex)
        {
            Logger.LogError("File processing failed", ex);
            Logger.LogMetric("FileProcessing.Failures", 1);
            LogMethodExit();
            throw;
        }
    }
}
```

### 3. AI Pipeline Stage Logging

```csharp
public class MistralDocumentationService : AIRESServiceBase
{
    public async Task<AIRESResult<ErrorDocumentationFinding>> AnalyzeAsync(CompilerError error)
    {
        LogMethodEntry();
        
        using (Logger.BeginScope("MistralAnalysis", new Dictionary<string, object>
        {
            ["ErrorCode"] = error.Code,
            ["ErrorMessage"] = error.Message,
            ["Stage"] = "Documentation"
        }))
        {
            Logger.LogInfo("Mistral analyzing error {ErrorCode}", error.Code);
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Fetch Microsoft docs
                Logger.LogDebug("Fetching Microsoft documentation for {ErrorCode}", error.Code);
                var docsUrl = $"https://learn.microsoft.com/.../compiler-messages/{error.Code.ToLower()}";
                var docsContent = await FetchDocsAsync(docsUrl);
                Logger.LogMetric("Mistral.DocsFetchTime", stopwatch.ElapsedMilliseconds);
                
                // Call AI model
                Logger.LogDebug("Calling Mistral AI model");
                var aiResponse = await _ollamaClient.GenerateAsync(prompt);
                Logger.LogMetric("Mistral.AIResponseTime", stopwatch.ElapsedMilliseconds);
                
                // Log AI metrics
                Logger.LogEvent("MistralAnalysisComplete", new Dictionary<string, object>
                {
                    ["ErrorCode"] = error.Code,
                    ["ResponseLength"] = aiResponse.Length,
                    ["Duration"] = stopwatch.Elapsed.TotalSeconds,
                    ["TokensUsed"] = aiResponse.TokenCount
                });
                
                LogMethodExit();
                return AIRESResult<ErrorDocumentationFinding>.Success(finding);
            }
            catch (Exception ex)
            {
                Logger.LogError("Mistral analysis failed for error {ErrorCode}", ex, error.Code);
                Logger.LogMetric("Mistral.Failures", 1);
                LogMethodExit();
                throw;
            }
        }
    }
}
```

### 4. Booklet Generation Logging

```csharp
public class BookletGeneratorService : AIRESServiceBase
{
    public async Task<AIRESResult<ResearchBooklet>> GenerateBookletAsync(
        ErrorBatch errors, 
        IEnumerable<AIResearchFinding> findings)
    {
        LogMethodEntry();
        
        Logger.LogInfo("Generating research booklet for {ErrorCount} errors with {FindingCount} findings",
            errors.Errors.Count, findings.Count());
        
        var bookletId = Guid.NewGuid().ToString();
        
        using (Logger.BeginScope("BookletGeneration", new Dictionary<string, object>
        {
            ["BookletId"] = bookletId,
            ["ErrorBatchId"] = errors.Id,
            ["PrimaryError"] = errors.Errors.FirstOrDefault()?.Code
        }))
        {
            try
            {
                // Generate content
                Logger.LogDebug("Synthesizing findings into booklet content");
                var content = await SynthesizeContentAsync(findings);
                Logger.LogMetric("Booklet.ContentLength", content.Length);
                
                // Save booklet
                var fileName = GenerateFileName(errors);
                var outputPath = Path.Combine(_config.OutputDirectory, fileName);
                
                Logger.LogInfo("Saving booklet to {OutputPath}", outputPath);
                await File.WriteAllTextAsync(outputPath, content);
                
                Logger.LogEvent("BookletGenerated", new Dictionary<string, object>
                {
                    ["BookletId"] = bookletId,
                    ["FileName"] = fileName,
                    ["FileSize"] = content.Length,
                    ["ErrorCount"] = errors.Errors.Count,
                    ["OutputPath"] = outputPath
                });
                
                LogMethodExit();
                return AIRESResult<ResearchBooklet>.Success(booklet);
            }
            catch (Exception ex)
            {
                Logger.LogError("Booklet generation failed", ex);
                LogMethodExit();
                throw;
            }
        }
    }
}
```

## 📈 Metrics and Instrumentation

### Key Metrics Tracked

```csharp
public static class AIRESMetrics
{
    // Watchdog Metrics
    public const string WatchdogFilesDetected = "aires.watchdog.files_detected";
    public const string WatchdogScanDuration = "aires.watchdog.scan_duration_ms";
    public const string WatchdogActiveFiles = "aires.watchdog.active_files";
    
    // Pipeline Metrics
    public const string PipelineExecutions = "aires.pipeline.executions_total";
    public const string PipelineSuccesses = "aires.pipeline.successes_total";
    public const string PipelineFailures = "aires.pipeline.failures_total";
    public const string PipelineDuration = "aires.pipeline.duration_ms";
    
    // AI Model Metrics
    public const string AIModelCalls = "aires.ai.{model}.calls_total";
    public const string AIModelLatency = "aires.ai.{model}.latency_ms";
    public const string AIModelTokens = "aires.ai.{model}.tokens_used";
    public const string AIModelErrors = "aires.ai.{model}.errors_total";
    
    // Booklet Metrics
    public const string BookletsGenerated = "aires.booklets.generated_total";
    public const string BookletSize = "aires.booklets.size_bytes";
    public const string BookletGenerationTime = "aires.booklets.generation_time_ms";
    
    // System Metrics
    public const string SystemMemoryUsage = "aires.system.memory_mb";
    public const string SystemCPUUsage = "aires.system.cpu_percent";
    public const string SystemActiveThreads = "aires.system.threads";
}
```

### Health Check Instrumentation

```csharp
public class AIRESHealthCheck : IAIRESHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        var checks = new Dictionary<string, ComponentHealth>();
        
        // Check Watchdog
        checks["Watchdog"] = new ComponentHealth
        {
            Status = _watchdog.IsRunning ? "Healthy" : "Unhealthy",
            LastActivity = _watchdog.LastScanTime,
            Details = new
            {
                FilesProcessing = _watchdog.ActiveFileCount,
                LastError = _watchdog.LastError
            }
        };
        
        // Check AI Models
        foreach (var model in _aiModels)
        {
            checks[$"AI.{model.Name}"] = new ComponentHealth
            {
                Status = await model.IsHealthyAsync() ? "Healthy" : "Unhealthy",
                LastActivity = model.LastCallTime,
                Details = new
                {
                    CallsToday = model.CallsToday,
                    AverageLatency = model.AverageLatencyMs,
                    ErrorRate = model.ErrorRate
                }
            };
        }
        
        // Check Infrastructure
        checks["Database"] = await CheckDatabaseHealth();
        checks["Kafka"] = await CheckKafkaHealth();
        checks["FileSystem"] = CheckFileSystemHealth();
        
        return new HealthCheckResult
        {
            Status = checks.All(c => c.Value.Status == "Healthy") ? "Healthy" : "Degraded",
            Components = checks,
            Timestamp = DateTime.UtcNow
        };
    }
}
```

## 🔍 Status Check Implementation

### CLI Status Command

```csharp
public class StatusCommand : Command
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var status = await _airesService.GetStatusAsync();
        
        Console.WriteLine("AIRES Status Report");
        Console.WriteLine("==================");
        Console.WriteLine($"Status: {status.OverallStatus}");
        Console.WriteLine($"Uptime: {status.Uptime}");
        Console.WriteLine();
        
        Console.WriteLine("Watchdog Status:");
        Console.WriteLine($"  Monitoring: {status.Watchdog.InputDirectory}");
        Console.WriteLine($"  Files Detected Today: {status.Watchdog.FilesDetectedToday}");
        Console.WriteLine($"  Files Processing: {status.Watchdog.FilesProcessing}");
        Console.WriteLine($"  Last Scan: {status.Watchdog.LastScanTime}");
        Console.WriteLine();
        
        Console.WriteLine("Pipeline Status:");
        Console.WriteLine($"  Executions Today: {status.Pipeline.ExecutionsToday}");
        Console.WriteLine($"  Success Rate: {status.Pipeline.SuccessRate:P}");
        Console.WriteLine($"  Average Duration: {status.Pipeline.AverageDuration}");
        Console.WriteLine($"  Current Stage: {status.Pipeline.CurrentStage}");
        Console.WriteLine();
        
        Console.WriteLine("AI Models:");
        foreach (var model in status.AIModels)
        {
            Console.WriteLine($"  {model.Name}:");
            Console.WriteLine($"    Status: {model.Status}");
            Console.WriteLine($"    Calls Today: {model.CallsToday}");
            Console.WriteLine($"    Average Latency: {model.AverageLatency}ms");
            Console.WriteLine($"    Error Rate: {model.ErrorRate:P}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Recent Activity:");
        foreach (var activity in status.RecentActivities.Take(5))
        {
            Console.WriteLine($"  [{activity.Timestamp:HH:mm:ss}] {activity.Description}");
        }
        
        return 0;
    }
}
```

### Real-time Status API

```csharp
public class AIRESStatusService : AIRESServiceBase
{
    public async Task<AIRESStatus> GetStatusAsync()
    {
        LogMethodEntry();
        
        var status = new AIRESStatus
        {
            OverallStatus = DetermineOverallStatus(),
            Uptime = DateTime.UtcNow - _startTime,
            Watchdog = GetWatchdogStatus(),
            Pipeline = GetPipelineStatus(),
            AIModels = GetAIModelStatuses(),
            SystemMetrics = GetSystemMetrics(),
            RecentActivities = GetRecentActivities(10),
            CurrentErrors = GetCurrentErrors()
        };
        
        Logger.LogDebug("Status check completed | Overall: {Status}", status.OverallStatus);
        LogMethodExit();
        
        return status;
    }
    
    private WatchdogStatus GetWatchdogStatus()
    {
        return new WatchdogStatus
        {
            IsRunning = _watchdog.IsRunning,
            InputDirectory = _config.Watchdog.InputDirectory,
            OutputDirectory = _config.Watchdog.OutputDirectory,
            FilesDetectedToday = _metrics.GetCounter("Watchdog.FilesDetected.Today"),
            FilesProcessing = _watchdog.ActiveFileCount,
            LastScanTime = _watchdog.LastScanTime,
            NextScanTime = _watchdog.NextScanTime,
            LastError = _watchdog.LastError
        };
    }
}
```

## 📁 Log Output Configuration

### Structured Log Format

```json
{
  "timestamp": "2025-01-13T14:32:15.123Z",
  "level": "Information",
  "service": "AIRESWatchdogService",
  "method": "ProcessFileAsync",
  "correlationId": "a3f4b6c8-9d2e-4f1a-b5c7-8e9f0a1b2c3d",
  "message": "New file detected",
  "properties": {
    "fileName": "build_errors_20250113_143022.txt",
    "fileSize": 15632,
    "directory": "/project/build-errors/"
  },
  "metrics": {
    "duration": 125.5
  }
}
```

### Log Aggregation

```ini
[Logging]
# Serilog configuration
MinimumLevel=Debug
WriteTo=Console,File,Seq
OutputTemplate={Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Service}] {Message}{NewLine}{Exception}

# File sink
File.Path=/var/log/aires/aires-.log
File.RollingInterval=Day
File.RetainedFileCountLimit=30

# Seq sink for centralized logging
Seq.ServerUrl=http://localhost:5341
Seq.ApiKey=YOUR_API_KEY

# Metrics
Metrics.Enabled=true
Metrics.Endpoint=http://localhost:9090/metrics
Metrics.Interval=10
```

## 🚨 Critical Logging Requirements

1. **EVERY method MUST have LogMethodEntry/Exit**
2. **EVERY error MUST be logged with full context**
3. **EVERY AI call MUST log latency and tokens**
4. **EVERY file operation MUST log path and result**
5. **EVERY state change MUST be logged**

## 📊 Debugging Support

### Debug Mode

```bash
# Enable verbose logging
aires start --log-level=Debug

# Enable performance profiling
aires start --enable-profiling

# Enable detailed AI logging
aires start --ai-debug
```

### Log Analysis Tools

```bash
# Search logs for specific error
aires logs grep "CS0117"

# Show logs for specific correlation ID
aires logs --correlation-id=a3f4b6c8-9d2e-4f1a-b5c7-8e9f0a1b2c3d

# Export metrics for time range
aires metrics export --from="2025-01-13T10:00:00" --to="2025-01-13T15:00:00"

# Generate activity report
aires report --date=today
```

---

**Remember**: Comprehensive logging is MANDATORY for autonomous operation. Without it, AIRES cannot be effectively monitored or debugged!