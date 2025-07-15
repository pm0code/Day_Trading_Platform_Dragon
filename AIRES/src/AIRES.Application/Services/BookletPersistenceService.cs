using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using AIRES.Application.Interfaces;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.Application.Services;

/// <summary>
/// Service responsible for persisting research booklets to storage.
/// Separates persistence concerns from content generation as per Gemini's guidance.
/// </summary>
public class BookletPersistenceService : AIRESServiceBase, IBookletPersistenceService
{
    private readonly IAIRESConfigurationProvider _configProvider;

    public BookletPersistenceService(
        IAIRESLogger logger,
        IAIRESConfigurationProvider configProvider) 
        : base(logger, nameof(BookletPersistenceService))
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// Saves a research booklet to the file system.
    /// </summary>
    public async Task<AIRESResult<string>> SaveBookletAsync(
        ResearchBooklet booklet,
        string suggestedPath,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            // Ensure directory exists
            var bookletDirectory = _configProvider.OutputDirectory;
            var fullPath = Path.Combine(bookletDirectory, suggestedPath);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                LogDebug($"Ensured directory exists: {directory}");
            }

            // Convert booklet to markdown
            var markdownContent = ConvertToMarkdown(booklet);

            // Write to file
            await File.WriteAllTextAsync(fullPath, markdownContent, Encoding.UTF8, cancellationToken);
            
            LogInfo($"Booklet saved successfully to: {fullPath}");
            LogMethodExit();
            return AIRESResult<string>.Success(fullPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogError("Unauthorized access when saving booklet", ex);
            LogMethodExit();
            return AIRESResult<string>.Failure(
                "BOOKLET_SAVE_UNAUTHORIZED",
                "Insufficient permissions to save booklet",
                ex
            );
        }
        catch (DirectoryNotFoundException ex)
        {
            LogError("Directory not found when saving booklet", ex);
            LogMethodExit();
            return AIRESResult<string>.Failure(
                "BOOKLET_SAVE_DIR_NOT_FOUND",
                "Target directory does not exist",
                ex
            );
        }
        catch (Exception ex)
        {
            LogError("Unexpected error saving booklet", ex);
            LogMethodExit();
            return AIRESResult<string>.Failure(
                "BOOKLET_SAVE_ERROR",
                $"Failed to save booklet: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Saves booklet content as a string directly.
    /// </summary>
    public async Task<AIRESResult<string>> SaveBookletContentAsync(
        string content,
        string filename,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            var bookletDirectory = _configProvider.OutputDirectory;
            var fullPath = Path.Combine(bookletDirectory, filename);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);
            
            LogInfo($"Booklet content saved to: {fullPath}");
            LogMethodExit();
            return AIRESResult<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            LogError("Error saving booklet content", ex);
            LogMethodExit();
            return AIRESResult<string>.Failure(
                "BOOKLET_CONTENT_SAVE_ERROR",
                $"Failed to save booklet content: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Lists all saved booklets.
    /// </summary>
    public AIRESResult<List<string>> ListBooklets()
    {
        LogMethodEntry();
        
        try
        {
            var bookletDirectory = _configProvider.OutputDirectory;
            if (!Directory.Exists(bookletDirectory))
            {
                LogInfo("Booklet directory does not exist yet");
                LogMethodExit();
                return AIRESResult<List<string>>.Success(new List<string>());
            }

            var booklets = Directory.GetFiles(bookletDirectory, "*.md", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .ToList();

            LogInfo($"Found {booklets.Count} booklets");
            LogMethodExit();
            return AIRESResult<List<string>>.Success(booklets!);
        }
        catch (Exception ex)
        {
            LogError("Error listing booklets", ex);
            LogMethodExit();
            return AIRESResult<List<string>>.Failure(
                "BOOKLET_LIST_ERROR",
                $"Failed to list booklets: {ex.Message}",
                ex
            );
        }
    }

    private string ConvertToMarkdown(ResearchBooklet booklet)
    {
        LogMethodEntry();
        
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {booklet.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {booklet.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Batch ID**: {booklet.ErrorBatchId}");
        sb.AppendLine($"**Total Errors**: {booklet.OriginalErrors.Count}");
        sb.AppendLine();

        // Metadata
        if (booklet.Metadata.Any())
        {
            sb.AppendLine("## Metadata");
            foreach (var kvp in booklet.Metadata)
            {
                sb.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
            }
            sb.AppendLine();
        }

        // Original Errors Summary
        sb.AppendLine("## Original Errors");
        sb.AppendLine();
        foreach (var errorGroup in booklet.OriginalErrors.GroupBy(e => e.Code))
        {
            sb.AppendLine($"### {errorGroup.Key} ({errorGroup.Count()} occurrences)");
            var firstError = errorGroup.First();
            sb.AppendLine($"- **Message**: {firstError.Message}");
            sb.AppendLine($"- **Locations**: {string.Join(", ", errorGroup.Select(e => e.Location.ToString()))}");
            sb.AppendLine();
        }

        // Sections
        foreach (var section in booklet.Sections.OrderBy(s => s.Order))
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine();
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        // AI Research Findings Summary
        sb.AppendLine("## AI Research Summary");
        sb.AppendLine();
        foreach (var finding in booklet.AllFindings)
        {
            sb.AppendLine($"### {finding.AIModelName}: {finding.Title}");
            sb.AppendLine();
            // Truncate long content
            var content = finding.Content.Length > 500 
                ? finding.Content.Substring(0, 500) + "..." 
                : finding.Content;
            sb.AppendLine(content);
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine("*Generated by AIRES (AI Error Resolution System)*");

        LogMethodExit();
        return sb.ToString();
    }
    
    /// <summary>
    /// Performs comprehensive health check of the booklet persistence service.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        const long MinimumFreeSpaceBytes = 100 * 1024 * 1024; // 100MB minimum
        
        try
        {
            var diagnostics = new Dictionary<string, object>();
            var failureReasons = new List<string>();
            
            // 1. Check output directory accessibility
            var outputDirectory = _configProvider.OutputDirectory;
            diagnostics["OutputDirectory"] = outputDirectory;
            diagnostics["OutputDirectoryExists"] = Directory.Exists(outputDirectory);
            
            if (!Directory.Exists(outputDirectory))
            {
                // Try to create the directory
                try
                {
                    Directory.CreateDirectory(outputDirectory);
                    LogInfo($"Created output directory: {outputDirectory}");
                    diagnostics["OutputDirectoryCreated"] = true;
                }
                catch (Exception ex)
                {
                    failureReasons.Add($"Output directory does not exist and cannot be created: {outputDirectory}");
                    LogError($"Cannot create output directory: {outputDirectory}", ex);
                    
                    stopwatch.Stop();
                    LogMethodExit();
                    return HealthCheckResult.Unhealthy(
                        nameof(BookletPersistenceService),
                        "Persistence Service",
                        stopwatch.ElapsedMilliseconds,
                        "Output directory not accessible",
                        ex,
                        failureReasons.ToImmutableList(),
                        diagnostics.ToImmutableDictionary()
                    );
                }
            }
            
            // 2. Test write permissions
            try
            {
                var testFilePath = Path.Combine(outputDirectory, $".health_check_{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(testFilePath, "AIRES Health Check Test");
                File.Delete(testFilePath);
                diagnostics["WritePermission"] = true;
                LogDebug("Write permission test passed");
            }
            catch (Exception ex)
            {
                diagnostics["WritePermission"] = false;
                failureReasons.Add($"Cannot write to output directory: {ex.Message}");
                
                stopwatch.Stop();
                LogMethodExit();
                return HealthCheckResult.Unhealthy(
                    nameof(BookletPersistenceService),
                    "Persistence Service",
                    stopwatch.ElapsedMilliseconds,
                    "Cannot write to output directory",
                    ex,
                    failureReasons.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                );
            }
            
            // 3. Check disk space availability
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(outputDirectory) ?? "C:\\");
                var availableSpace = driveInfo.AvailableFreeSpace;
                var totalSpace = driveInfo.TotalSize;
                var usedSpace = totalSpace - availableSpace;
                var usagePercent = (double)usedSpace / totalSpace * 100;
                
                diagnostics["DriveName"] = driveInfo.Name;
                diagnostics["AvailableSpaceGB"] = Math.Round(availableSpace / (1024.0 * 1024 * 1024), 2);
                diagnostics["TotalSpaceGB"] = Math.Round(totalSpace / (1024.0 * 1024 * 1024), 2);
                diagnostics["SpaceUsagePercent"] = Math.Round(usagePercent, 2);
                
                if (availableSpace < MinimumFreeSpaceBytes)
                {
                    failureReasons.Add($"Insufficient disk space: {availableSpace / (1024 * 1024)}MB available, minimum required: {MinimumFreeSpaceBytes / (1024 * 1024)}MB");
                    
                    stopwatch.Stop();
                    LogMethodExit();
                    return HealthCheckResult.Unhealthy(
                        nameof(BookletPersistenceService),
                        "Persistence Service",
                        stopwatch.ElapsedMilliseconds,
                        "Insufficient disk space",
                        null,
                        failureReasons.ToImmutableList(),
                        diagnostics.ToImmutableDictionary()
                    );
                }
                // Warning threshold at 500MB
                else if (availableSpace < MinimumFreeSpaceBytes * 5)
                {
                    failureReasons.Add($"Low disk space warning: {availableSpace / (1024 * 1024)}MB available");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Could not check disk space: {ex.Message}");
                diagnostics["DiskSpaceCheck"] = "Failed";
                // Don't fail health check for this, just warn
            }
            
            // 4. Check existing booklets
            try
            {
                var bookletFiles = Directory.GetFiles(outputDirectory, "*.md", SearchOption.AllDirectories);
                diagnostics["ExistingBooklets"] = bookletFiles.Length;
                diagnostics["OldestBooklet"] = bookletFiles.Any() 
                    ? new FileInfo(bookletFiles.OrderBy(f => new FileInfo(f).CreationTime).First()).CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    : "None";
                diagnostics["NewestBooklet"] = bookletFiles.Any()
                    ? new FileInfo(bookletFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First()).CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    : "None";
                    
                // Calculate total size of booklets
                var totalSize = bookletFiles.Sum(f => new FileInfo(f).Length);
                diagnostics["TotalBookletSizeMB"] = Math.Round(totalSize / (1024.0 * 1024), 2);
            }
            catch (Exception ex)
            {
                LogWarning($"Could not enumerate existing booklets: {ex.Message}");
                diagnostics["BookletEnumeration"] = "Failed";
            }
            
            // 5. Add service metrics
            diagnostics["BookletsSaved"] = GetMetricValue("BookletPersistence.SavedCount");
            diagnostics["SaveErrors"] = GetMetricValue("BookletPersistence.SaveErrors");
            diagnostics["AverageSaveTime"] = GetAverageMetric("BookletPersistence.SaveDuration", "BookletPersistence.SavedCount");
            diagnostics["LastSaveTime"] = GetLastOperationTime("BookletPersistence.LastSave");
            
            stopwatch.Stop();
            
            // Determine health status
            if (failureReasons.Any(r => r.Contains("Low disk space")))
            {
                LogWarning($"Booklet persistence service is degraded: {string.Join("; ", failureReasons)}");
                LogMethodExit();
                
                return HealthCheckResult.Degraded(
                    nameof(BookletPersistenceService),
                    "Persistence Service",
                    stopwatch.ElapsedMilliseconds,
                    failureReasons.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                );
            }
            
            // All checks passed
            LogInfo($"Booklet persistence health check completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return HealthCheckResult.Healthy(
                nameof(BookletPersistenceService),
                "Persistence Service",
                stopwatch.ElapsedMilliseconds,
                diagnostics.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError("Error during booklet persistence health check", ex);
            LogMethodExit();
            
            return HealthCheckResult.Unhealthy(
                nameof(BookletPersistenceService),
                "Persistence Service",
                stopwatch.ElapsedMilliseconds,
                $"Health check failed: {ex.Message}",
                ex,
                ImmutableList.Create($"Exception during health check: {ex.GetType().Name}")
            );
        }
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
    
    private double GetAverageMetric(string totalMetric, string countMetric)
    {
        var total = GetMetricValue(totalMetric);
        var count = GetMetricValue(countMetric);
        return count > 0 ? total / count : 0;
    }
    
    private string GetLastOperationTime(string metricName)
    {
        var metrics = GetMetrics();
        return metrics.GetValueOrDefault(metricName)?.ToString() ?? "Never";
    }
}