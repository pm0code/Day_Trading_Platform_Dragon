namespace AIRES.Watchdog.Configuration;

/// <summary>
/// Configuration for AIRES Watchdog loaded from INI file.
/// </summary>
public class WatchdogConfiguration
{
    /// <summary>
    /// Whether watchdog is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Directory to monitor for input files.
    /// </summary>
    public string InputDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Directory to save generated booklets.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Directory to move successfully processed files.
    /// </summary>
    public string ProcessedDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Directory to move files that failed processing.
    /// </summary>
    public string FailedDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// File pattern to monitor (e.g., "*.txt").
    /// </summary>
    public string FilePattern { get; set; } = "*.txt";
    
    /// <summary>
    /// Polling interval in seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 10;
    
    /// <summary>
    /// Maximum concurrent file processing.
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 3;
    
    /// <summary>
    /// Delete processed files after this many days (0 = never delete).
    /// </summary>
    public int DeleteProcessedAfterDays { get; set; } = 30;
    
    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(InputDirectory))
            yield return "InputDirectory is required";
            
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            yield return "OutputDirectory is required";
            
        if (string.IsNullOrWhiteSpace(ProcessedDirectory))
            yield return "ProcessedDirectory is required";
            
        if (string.IsNullOrWhiteSpace(FailedDirectory))
            yield return "FailedDirectory is required";
            
        if (PollingIntervalSeconds < 1)
            yield return "PollingIntervalSeconds must be at least 1";
            
        if (MaxConcurrentProcessing < 1)
            yield return "MaxConcurrentProcessing must be at least 1";
    }
    
    /// <summary>
    /// Ensures all configured directories exist.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(InputDirectory);
        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(ProcessedDirectory);
        Directory.CreateDirectory(FailedDirectory);
    }
}