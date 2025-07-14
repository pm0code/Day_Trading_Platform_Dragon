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

namespace AIRES.CLI.Commands;

/// <summary>
/// Process a specific error file through AIRES pipeline.
/// </summary>
[Description("Process a specific error file")]
public class ProcessCommand : AsyncCommand<ProcessCommand.Settings>
{
    private static readonly string[] PipelineStages = new[]
    {
        "Initializing Pipeline",
        "Stage 1: Mistral Documentation Research",
        "Stage 2: DeepSeek Context Analysis",
        "Stage 3: CodeGemma Pattern Validation",
        "Stage 4: Gemma2 Booklet Synthesis",
        "Finalizing Results"
    };
    
    private static readonly int[] ProgressSteps = new[] { 5, 25, 45, 65, 85, 100 };
    
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
        try
        {
            // Validate input file
            if (!File.Exists(settings.FilePath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.FilePath}");
                return 3; // File not found exit code
            }
            
            // Check file size
            var fileInfo = new FileInfo(settings.FilePath);
            var maxSizeMB = _configuration.Processing.MaxFileSizeMB;
            if (fileInfo.Length > maxSizeMB * 1024 * 1024)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File exceeds maximum size of {maxSizeMB}MB");
                return 1;
            }
            
            // Check file extension
            var allowedExtensions = _configuration.Processing.AllowedExtensions.Split(',', StringSplitOptions.TrimEntries);
            if (!allowedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File type not allowed. Allowed types: {_configuration.Processing.AllowedExtensions}");
                return 1;
            }
            
            // Read error file content
            _logger.LogInfo($"Reading error file: {settings.FilePath}");
            var rawCompilerOutput = await File.ReadAllTextAsync(settings.FilePath);
            
            if (string.IsNullOrWhiteSpace(rawCompilerOutput))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Error file is empty");
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
            
            // Default values for missing parameters
            var projectCodebase = string.Empty; // TODO: Implement codebase extraction
            var projectStandards = ImmutableList<string>.Empty; // TODO: Load from configuration
            
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
                    
                    // Track progress through stages
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    // Report initialization
                    ((IProgress<(string, double)>)progress).Report(("Initializing Pipeline", 5));
                    
                    // Execute the actual AI pipeline
                    var orchestratorTask = orchestrator.GenerateResearchBookletAsync(
                        rawCompilerOutput,
                        codeContext,
                        projectStructureXml,
                        projectCodebase,
                        projectStandards);
                    
                    // Simulate progress based on expected timings
                    var stageIndex = 0;
                    
                    while (!orchestratorTask.IsCompleted && stageIndex < PipelineStages.Length)
                    {
                        await Task.Delay(500); // Check every 500ms
                        
                        // Update progress based on elapsed time (rough estimation)
                        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        
                        // Assume ~10 seconds per stage
                        if (elapsedSeconds > stageIndex * 10)
                        {
                            stageIndex = Math.Min(stageIndex + 1, PipelineStages.Length - 1);
                            ((IProgress<(string, double)>)progress).Report((PipelineStages[stageIndex], ProgressSteps[stageIndex]));
                        }
                    }
                    
                    // Wait for completion
                    var result = await orchestratorTask;
                    
                    // Report completion
                    ((IProgress<(string, double)>)progress).Report(("Complete!", 100));
                    
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
                
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Processing failed:[/] {result.ErrorMessage}");
                if (!string.IsNullOrEmpty(result.ErrorCode))
                {
                    AnsiConsole.MarkupLine($"[dim]Error code: {result.ErrorCode}[/]");
                }
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error in ProcessCommand", ex);
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return -1;
        }
    }
}