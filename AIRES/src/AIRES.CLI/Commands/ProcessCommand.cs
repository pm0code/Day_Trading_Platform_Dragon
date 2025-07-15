using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Collections.Immutable;
using AIRES.Application.Interfaces;
using AIRES.Application.Services;
using AIRES.Core.Configuration;
using AIRES.Foundation.Logging;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace AIRES.CLI.Commands;

/// <summary>
/// Process a specific error file through AIRES pipeline.
/// </summary>
[Description("Process a specific error file")]
public class ProcessCommand : AsyncCommand<ProcessCommand.Settings>
{
    private readonly IOrchestratorFactory _orchestratorFactory;
    private readonly IAIRESConfiguration _configuration;
    private readonly IAIRESLogger _logger;
    
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to the error file to process")]
        public string FilePath { get; set; } = string.Empty;
        
        [CommandOption("-o|--output")]
        [Description("Output directory for booklet")]
        public string? OutputDirectory { get; set; }
        
        [CommandOption("-c|--context")]
        [Description("Additional code context file")]
        public string? ContextFile { get; set; }
        
        [CommandOption("-p|--project")]
        [Description("Project structure XML file")]
        public string? ProjectStructureFile { get; set; }
        
        [CommandOption("-d|--codebase-dir")]
        [Description("Directory containing project codebase for context")]
        public string? ProjectCodebaseDirectory { get; set; }
        
        [CommandOption("--parallel")]
        [Description("Use parallel processing for AI models (experimental)")]
        [DefaultValue(false)]
        public bool UseParallel { get; set; } = false;
    }
    
    public ProcessCommand(
        IOrchestratorFactory orchestratorFactory,
        IAIRESConfiguration configuration,
        IAIRESLogger logger)
    {
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _logger.LogDebug("ProcessCommand.ExecuteAsync - Entry");
        try
        {
            // Validate input file
            if (!File.Exists(settings.FilePath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.FilePath}");
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 3; // File not found exit code
            }
            
            // Check file size
            var fileInfo = new FileInfo(settings.FilePath);
            var maxSizeMB = _configuration.Processing.MaxFileSizeMB;
            if (fileInfo.Length > maxSizeMB * 1024 * 1024)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File exceeds maximum size of {maxSizeMB}MB");
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 1;
            }
            
            // Check file extension
            var allowedExtensions = _configuration.Processing.AllowedExtensions.Split(',', StringSplitOptions.TrimEntries);
            if (!allowedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File type not allowed. Allowed types: {_configuration.Processing.AllowedExtensions}");
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 1;
            }
            
            // Read error file content
            _logger.LogInfo($"Reading error file: {settings.FilePath}");
            var rawCompilerOutput = await File.ReadAllTextAsync(settings.FilePath);
            
            if (string.IsNullOrWhiteSpace(rawCompilerOutput))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Error file is empty");
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 1;
            }
            
            // Read optional context files
            var codeContext = string.Empty;
            if (!string.IsNullOrEmpty(settings.ContextFile) && File.Exists(settings.ContextFile))
            {
                codeContext = await File.ReadAllTextAsync(settings.ContextFile);
                _logger.LogInfo($"Loaded code context from: {settings.ContextFile}");
            }
            
            var projectStructureXml = string.Empty;
            if (!string.IsNullOrEmpty(settings.ProjectStructureFile) && File.Exists(settings.ProjectStructureFile))
            {
                projectStructureXml = await File.ReadAllTextAsync(settings.ProjectStructureFile);
                _logger.LogInfo($"Loaded project structure from: {settings.ProjectStructureFile}");
            }
            
            // Load project standards from configuration
            var projectStandards = LoadProjectStandards();
            
            // Extract project codebase if specified
            var projectCodebase = await ExtractProjectCodebaseAsync(settings.ProjectCodebaseDirectory);
            
            AnsiConsole.MarkupLine($"[cyan]Processing:[/] {Path.GetFileName(settings.FilePath)}");
            if (settings.UseParallel)
            {
                AnsiConsole.MarkupLine("[yellow]Using PARALLEL processing mode (experimental)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Using sequential processing mode[/]");
            }
            AnsiConsole.WriteLine();
            
            // Get the appropriate orchestrator
            var orchestrator = _orchestratorFactory.CreateOrchestrator(settings.UseParallel);
            
            // Process through AI pipeline with real progress tracking
            var result = await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn() { Alignment = Justify.Left },
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[bold]AIRES Pipeline[/]", maxValue: 100);
                    
                    // Create a progress reporter
                    var progress = new Progress<(string stage, double percentage)>(report =>
                    {
                        task.Description = $"[bold]{report.stage}[/]";
                        task.Value = report.percentage;
                    });
                    
                    // Execute the AI pipeline with real progress reporting
                    var result = await orchestrator.GenerateResearchBookletAsync(
                        rawCompilerOutput,
                        codeContext,
                        projectStructureXml,
                        projectCodebase,
                        projectStandards,
                        progress,
                        CancellationToken.None);
                    
                    return result;
                });
            
            AnsiConsole.WriteLine();
            
            if (result.IsSuccess && result.Value != null)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Processing complete!");
                AnsiConsole.MarkupLine($"[dim]Booklet generated: {result.Value.BookletPath}[/]");
                AnsiConsole.WriteLine();
                
                // Display timing information
                if (result.Value.StepTimings.Any())
                {
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("Stage")
                        .AddColumn("Duration (ms)");
                    
                    foreach (var timing in result.Value.StepTimings)
                    {
                        table.AddRow(timing.Key, timing.Value.ToString());
                    }
                    
                    table.AddRow("[bold]Total[/]", $"[bold]{result.Value.ProcessingTimeMs}[/]");
                    
                    AnsiConsole.Write(table);
                }
                
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Processing failed:[/] {result.ErrorMessage}");
                if (!string.IsNullOrEmpty(result.ErrorCode))
                {
                    AnsiConsole.MarkupLine($"[dim]Error code: {result.ErrorCode}[/]");
                }
                _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error in ProcessCommand", ex);
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            _logger.LogDebug("ProcessCommand.ExecuteAsync - Exit");
            return -1;
        }
    }
    
    private ImmutableList<string> LoadProjectStandards()
    {
        _logger.LogDebug("LoadProjectStandards - Entry");
        _logger.LogDebug("Loading project standards from configuration");
        
        try
        {
            var standards = new List<string>();
            
            // Load from configuration - could be a comma-separated list
            var configuredStandards = _configuration.GetValue("Development:ProjectStandards");
            if (!string.IsNullOrWhiteSpace(configuredStandards))
            {
                standards.AddRange(configuredStandards.Split(',', StringSplitOptions.TrimEntries));
                _logger.LogInfo($"Loaded {standards.Count} project standards from configuration");
            }
            
            // Add default AIRES standards if none configured
            if (standards.Count == 0)
            {
                standards.Add("Use AIRES canonical patterns (AIRESServiceBase, AIRESResult<T>)");
                standards.Add("All methods must have LogMethodEntry/LogMethodExit");
                standards.Add("Use IAIRESLogger interface for logging");
                standards.Add("Zero mock implementations allowed");
                standards.Add("80% minimum test coverage required");
                standards.Add("Follow MANDATORY_DEVELOPMENT_STANDARDS-AIRES-V5.md");
                _logger.LogInfo("Using default AIRES project standards");
            }
            
            _logger.LogDebug("LoadProjectStandards - Exit");
            return standards.ToImmutableList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load project standards: {ex.Message}");
            _logger.LogDebug("LoadProjectStandards - Exit");
            return ImmutableList<string>.Empty;
        }
    }
    
    private async Task<string> ExtractProjectCodebaseAsync(string? codebaseDirectory)
    {
        _logger.LogDebug("ExtractProjectCodebaseAsync - Entry");
        _logger.LogDebug($"Extracting project codebase from: {codebaseDirectory ?? "not specified"}");
        
        if (string.IsNullOrWhiteSpace(codebaseDirectory))
        {
            _logger.LogDebug("No codebase directory specified");
            _logger.LogDebug("ExtractProjectCodebaseAsync - Exit");
            return string.Empty;
        }
        
        if (!Directory.Exists(codebaseDirectory))
        {
            _logger.LogWarning($"Codebase directory does not exist: {codebaseDirectory}");
            _logger.LogDebug("ExtractProjectCodebaseAsync - Exit");
            return string.Empty;
        }
        
        try
        {
            var codebaseBuilder = new StringBuilder();
            codebaseBuilder.AppendLine($"Project Codebase from: {codebaseDirectory}");
            codebaseBuilder.AppendLine(new string('=', 80));
            
            // Get relevant source files
            var extensions = new[] { ".cs", ".csproj", ".json", ".xml", ".config" };
            var files = Directory.GetFiles(codebaseDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Take(20); // Limit to prevent huge context
            
            foreach (var file in files)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(codebaseDirectory, file);
                    var content = await File.ReadAllTextAsync(file);
                    
                    codebaseBuilder.AppendLine($"\n--- File: {relativePath} ---");
                    codebaseBuilder.AppendLine(content);
                    
                    // Limit total size
                    if (codebaseBuilder.Length > 50000)
                    {
                        codebaseBuilder.AppendLine("\n... (truncated for size)");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to read file {file}: {ex.Message}");
                }
            }
            
            _logger.LogInfo($"Extracted codebase context from {files.Count()} files");
            _logger.LogDebug("ExtractProjectCodebaseAsync - Exit");
            return codebaseBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to extract project codebase", ex);
            _logger.LogDebug("ExtractProjectCodebaseAsync - Exit");
            return string.Empty;
        }
    }
}