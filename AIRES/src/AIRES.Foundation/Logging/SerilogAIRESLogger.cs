using Serilog;
using Serilog.Context;

namespace AIRES.Foundation.Logging;

/// <summary>
/// Serilog implementation of IAIRESLogger.
/// </summary>
public class SerilogAIRESLogger : IAIRESLogger
{
    private readonly ILogger _logger;
    private string _correlationId = Guid.NewGuid().ToString();
    
    public SerilogAIRESLogger(ILogger logger)
    {
        _logger = logger.ForContext<SerilogAIRESLogger>();
    }
    
    public void LogTrace(string message, params object[] args)
    {
        _logger.Verbose(message, args);
    }
    
    public void LogDebug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }
    
    public void LogInfo(string message, params object[] args)
    {
        _logger.Information(message, args);
    }
    
    public void LogWarning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }
    
    public void LogError(string message, Exception? ex = null, params object[] args)
    {
        if (ex != null)
            _logger.Error(ex, message, args);
        else
            _logger.Error(message, args);
    }
    
    public void LogFatal(string message, Exception? ex = null, params object[] args)
    {
        if (ex != null)
            _logger.Fatal(ex, message, args);
        else
            _logger.Fatal(message, args);
    }
    
    public void LogMetric(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        var enrichedLogger = _logger;
        
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                enrichedLogger = enrichedLogger.ForContext(tag.Key, tag.Value);
            }
        }
        
        enrichedLogger.Information("Metric {MetricName} = {MetricValue}", metricName, value);
    }
    
    public void LogEvent(string eventName, Dictionary<string, object>? properties = null)
    {
        var enrichedLogger = _logger;
        
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                enrichedLogger = enrichedLogger.ForContext(prop.Key, prop.Value);
            }
        }
        
        enrichedLogger.Information("Event {EventName} occurred", eventName);
    }
    
    public void LogDuration(string operationName, TimeSpan duration, Dictionary<string, string>? tags = null)
    {
        LogMetric($"{operationName}.Duration", duration.TotalMilliseconds, tags);
    }
    
    public IDisposable BeginScope(string scopeName, Dictionary<string, object>? properties = null)
    {
        var disposables = new List<IDisposable>
        {
            LogContext.PushProperty("Scope", scopeName)
        };
        
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                disposables.Add(LogContext.PushProperty(prop.Key, prop.Value));
            }
        }
        
        return new CompositeDisposable(disposables);
    }
    
    public void SetCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        LogContext.PushProperty("CorrelationId", correlationId);
    }
    
    public string GetCorrelationId()
    {
        return _correlationId;
    }
    
    public void LogHealthCheck(string componentName, bool isHealthy, string? details = null)
    {
        var status = isHealthy ? "Healthy" : "Unhealthy";
        _logger.Information("Health check for {ComponentName}: {Status}. {Details}", 
            componentName, status, details ?? "No additional details");
    }
    
    public void LogStatus(string statusName, string statusValue, Dictionary<string, object>? additionalInfo = null)
    {
        var enrichedLogger = _logger;
        
        if (additionalInfo != null)
        {
            foreach (var info in additionalInfo)
            {
                enrichedLogger = enrichedLogger.ForContext(info.Key, info.Value);
            }
        }
        
        enrichedLogger.Information("Status {StatusName} = {StatusValue}", statusName, statusValue);
    }
    
    private class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;
        
        public CompositeDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables;
        }
        
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}