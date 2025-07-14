using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using System.Text;

namespace AIRES.Application.Services;

/// <summary>
/// Service responsible for persisting research booklets to storage.
/// Separates persistence concerns from content generation as per Gemini's guidance.
/// </summary>
public class BookletPersistenceService : AIRESServiceBase
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
}