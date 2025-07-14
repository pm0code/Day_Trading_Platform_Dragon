using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Log file channel that writes alerts to a dedicated log file.
/// Implements thread-safe file writing with rotation support.
/// </summary>
public class LogFileChannel : AIRESServiceBase, IAlertChannel
{
    private readonly string _logFilePath;
    private readonly long _maxFileSize;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private long _totalAlertsWritten;
    private long _bytesWritten;
    private DateTime _lastWriteTime = DateTime.UtcNow;
    
    public string ChannelName => "LogFile";
    public AlertChannelType ChannelType => AlertChannelType.LogFile;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public LogFileChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(LogFileChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:LogFile");
        IsEnabled = bool.Parse(channelConfig["Enabled"] ?? "true");
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Information");
        
        // Configure log file path and rotation
        _logFilePath = channelConfig["FilePath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIRES", "Alerts", "aires_alerts.log");
        
        _maxFileSize = long.Parse(channelConfig["MaxFileSizeMB"] ?? "100") * 1024 * 1024;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public async Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (alert.Severity < MinimumSeverity)
        {
            LogMethodExit();
            return;
        }
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Check if rotation is needed
            await RotateIfNeeded();
            
            // Format the alert message
            var logEntry = FormatAlertForLog(alert);
            var bytes = Encoding.UTF8.GetBytes(logEntry);
            
            // Write to file
            await using var fileStream = new FileStream(
                _logFilePath, 
                FileMode.Append, 
                FileAccess.Write, 
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            
            await fileStream.WriteAsync(bytes, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
            
            // Update metrics
            _totalAlertsWritten++;
            _bytesWritten += bytes.Length;
            _lastWriteTime = DateTime.UtcNow;
            
            Logger.LogDebug($"Alert written to log file: {alert.Id}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to write alert to log file", ex);
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
            LogMethodExit();
        }
    }
    
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if we can write to the file
            var testPath = _logFilePath + ".health";
            await File.WriteAllTextAsync(testPath, DateTime.UtcNow.ToString("O"));
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public Task<Dictionary<string, object>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["ChannelName"] = ChannelName,
            ["LogFilePath"] = _logFilePath,
            ["TotalAlertsWritten"] = _totalAlertsWritten,
            ["BytesWritten"] = _bytesWritten,
            ["LastWriteTime"] = _lastWriteTime,
            ["FileExists"] = File.Exists(_logFilePath),
            ["FileSizeBytes"] = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0
        };
        
        return Task.FromResult(metrics);
    }
    
    private string FormatAlertForLog(AlertMessage alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{alert.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{alert.Severity}] [{alert.Component}]");
        sb.AppendLine($"ID: {alert.Id}");
        sb.AppendLine($"Message: {alert.Message}");
        
        if (!string.IsNullOrEmpty(alert.SuggestedAction))
        {
            sb.AppendLine($"Suggested Action: {alert.SuggestedAction}");
        }
        
        if (alert.Details != null && alert.Details.Count > 0)
        {
            sb.AppendLine("Details:");
            foreach (var kvp in alert.Details)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        sb.AppendLine(new string('-', 80));
        return sb.ToString();
    }
    
    private async Task RotateIfNeeded()
    {
        if (!File.Exists(_logFilePath))
            return;
        
        var fileInfo = new FileInfo(_logFilePath);
        if (fileInfo.Length < _maxFileSize)
            return;
        
        // Rotate the file
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var rotatedPath = $"{_logFilePath}.{timestamp}";
        
        File.Move(_logFilePath, rotatedPath);
        Logger.LogInfo($"Rotated log file to: {rotatedPath}");
        
        // Reset metrics
        _bytesWritten = 0;
        
        // Clean up old rotated files (keep last 10)
        await CleanupOldRotatedFiles();
    }
    
    private Task CleanupOldRotatedFiles()
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            var fileName = Path.GetFileName(_logFilePath);
            
            if (string.IsNullOrEmpty(directory))
                return Task.CompletedTask;
            
            var rotatedFiles = Directory.GetFiles(directory, $"{fileName}.*")
                .OrderByDescending(f => new FileInfo(f).CreationTimeUtc)
                .Skip(10)
                .ToList();
            
            foreach (var file in rotatedFiles)
            {
                try
                {
                    File.Delete(file);
                    Logger.LogDebug($"Deleted old rotated file: {file}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to delete old rotated file: {file}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to cleanup old rotated files", ex);
        }
        
        return Task.CompletedTask;
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writeSemaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}