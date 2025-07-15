using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRES.CLI.Health;
using AIRES.Core.Configuration;
using AIRES.Core.Health;
using AIRES.Foundation.Logging;
using AIRES.Watchdog.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AIRES.CLI.Commands;

/// <summary>
/// Starts AIRES in autonomous watchdog mode with comprehensive health validation.
/// </summary>
[Description("Start AIRES in autonomous watchdog mode")]
public class StartCommand : AsyncCommand<StartCommand.Settings>
{
    private readonly IAIRESLogger _logger;
    private readonly IAIRESConfiguration _configuration;
    private readonly IFileWatchdogService _watchdog;
    private readonly HealthCheckExecutor _healthCheckExecutor;
    
    public StartCommand(
        IAIRESLogger logger,
        IAIRESConfiguration configuration,
        IFileWatchdogService watchdog,
        HealthCheckExecutor healthCheckExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
        _healthCheckExecutor = healthCheckExecutor ?? throw new ArgumentNullException(nameof(healthCheckExecutor));
    }
    
    public class Settings : CommandSettings
    {
        [CommandOption("-w|--watchdog")]
        [Description("Start in watchdog mode (default)")]
        [DefaultValue(true)]
        public bool Watchdog { get; set; } = true;
        
        [CommandOption("-c|--config")]
        [Description("Path to configuration file")]
        public string? ConfigFile { get; set; }
        
        [CommandOption("-d|--directory")]
        [Description("Override input directory to monitor")]
        public string? InputDirectory { get; set; }
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            if (!settings.Watchdog)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Start command requires watchdog mode");
                return 1;
            }
            
            // Display startup information
            var rule = new Rule("[cyan]AIRES Watchdog Mode[/]")
                .RuleStyle("cyan dim")
                .LeftJustified();
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();
            
            // Perform comprehensive health checks before starting
            HealthCheckReport? healthReport = null;
            var healthCheckPassed = false;
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("Starting AIRES...", async ctx =>
                {
                    // Step 1: Validate configuration
                    ctx.Status("Validating configuration...");
                    if (string.IsNullOrEmpty(_configuration.Directories.InputDirectory))
                    {
                        throw new InvalidOperationException("Input directory is not configured");
                    }
                    if (string.IsNullOrEmpty(_configuration.Directories.OutputDirectory))
                    {
                        throw new InvalidOperationException("Output directory is not configured");
                    }
                    _logger.LogInfo("Configuration validated successfully");
                    
                    // Step 2: Run comprehensive health checks
                    ctx.Status("Running system health checks...");
                    healthReport = await _healthCheckExecutor.RunHealthChecksAsync(detailed: true);
                    
                    // Step 3: Evaluate health check results
                    if (healthReport.OverallStatus == HealthStatus.Unhealthy)
                    {
                        // Display health check failures
                        ctx.Status("Health check failures detected");
                        AnsiConsole.WriteLine();
                        
                        var failureTable = new Table()
                            .Border(TableBorder.Rounded)
                            .BorderColor(Color.Red)
                            .AddColumn("Component", c => c.Width(25))
                            .AddColumn("Status", c => c.Width(12))
                            .AddColumn("Error", c => c.Width(50));
                        
                        foreach (var result in healthReport.Results.Where(r => r.Value.Status == HealthStatus.Unhealthy))
                        {
                            failureTable.AddRow(
                                result.Key,
                                "[red]● Unhealthy[/]",
                                Markup.Escape(result.Value.ErrorMessage ?? "Unknown error")
                            );
                        }
                        
                        AnsiConsole.Write(failureTable);
                        throw new InvalidOperationException($"System health check failed: {healthReport.Summary}");
                    }
                    
                    if (healthReport.OverallStatus == HealthStatus.Degraded)
                    {
                        // Show warnings but allow startup
                        ctx.Status("Some components are degraded");
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[yellow]Warning:[/] Some components are degraded but functional");
                        
                        var warningTable = new Table()
                            .Border(TableBorder.Rounded)
                            .BorderColor(Color.Yellow)
                            .AddColumn("Component", c => c.Width(25))
                            .AddColumn("Issue", c => c.Width(60));
                        
                        foreach (var result in healthReport.Results.Where(r => r.Value.Status == HealthStatus.Degraded))
                        {
                            var issues = string.Join("; ", result.Value.FailureReasons);
                            warningTable.AddRow(result.Key, Markup.Escape(issues));
                        }
                        
                        AnsiConsole.Write(warningTable);
                        AnsiConsole.WriteLine();
                    }
                    
                    healthCheckPassed = true;
                    _logger.LogInfo($"Health check completed: {healthReport.Summary}");
                    
                    // Step 4: Initialize watchdog service
                    ctx.Status("Initializing watchdog service...");
                    var initResult = await _watchdog.InitializeAsync();
                    if (!initResult.IsSuccess)
                    {
                        throw new InvalidOperationException($"Failed to initialize watchdog: {initResult.ErrorMessage}");
                    }
                    _logger.LogInfo("Watchdog initialized successfully");
                    
                    ctx.Status("Loading configuration...");
                    await Task.Delay(500); // Allow time for configuration to settle
                    
                    // Step 5: Start file monitoring
                    ctx.Status("Starting file monitoring...");
                    var startResult = await _watchdog.StartAsync();
                    if (!startResult.IsSuccess)
                    {
                        throw new InvalidOperationException($"Failed to start watchdog: {startResult.ErrorMessage}");
                    }
                    _logger.LogInfo("Watchdog started successfully");
                });
            
            // Display health check summary if successful
            if (healthCheckPassed && healthReport != null)
            {
                var healthSummaryPanel = new Panel(
                    new Markup($"""
                    [bold]Health Check Results:[/]
                    Status: {GetHealthStatusMarkup(healthReport.OverallStatus)}
                    Summary: {healthReport.Summary}
                    Duration: {healthReport.TotalDuration}ms
                    """))
                {
                    Header = new PanelHeader("[bold]System Health[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(healthReport.OverallStatus == HealthStatus.Healthy ? Color.Green : Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                
                AnsiConsole.Write(healthSummaryPanel);
                AnsiConsole.WriteLine();
            }
            
            // Display configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Setting", c => c.Width(30))
                .AddColumn("Value");
                
            configTable.AddRow("Input Directory", _configuration.Directories.InputDirectory);
            configTable.AddRow("Output Directory", _configuration.Directories.OutputDirectory);
            configTable.AddRow("Allowed Extensions", _configuration.Processing.AllowedExtensions);
            configTable.AddRow("Polling Interval", $"{_configuration.Watchdog.PollingIntervalSeconds} seconds");
            configTable.AddRow("Processing Threads", _configuration.Watchdog.ProcessingThreads.ToString());
            configTable.AddRow("Max Queue Size", _configuration.Watchdog.MaxQueueSize.ToString());
            
            AnsiConsole.Write(configTable);
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine("[bold green]✓[/] AIRES started successfully!");
            AnsiConsole.MarkupLine("[dim]Monitoring for error files... Press Ctrl+C to stop.[/]");
            AnsiConsole.WriteLine();
            
            _logger.LogInfo("AIRES started in watchdog mode");
            
            // Set up cancellation
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                AnsiConsole.MarkupLine("\n[yellow]Shutting down...[/]");
                cts.Cancel();
            };
            
            // Display live status
            await AnsiConsole.Live(GenerateStatusPanel())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Bottom)
                .StartAsync(async ctx =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        ctx.UpdateTarget(GenerateStatusPanel());
                        
                        try
                        {
                            await Task.Delay(1000, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });
            
            // Stop watchdog
            await _watchdog.StopAsync();
            
            AnsiConsole.MarkupLine("[green]✓[/] AIRES stopped gracefully");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/] Failed to start AIRES");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            _logger.LogError("Failed to start AIRES", ex);
            return 1;
        }
    }
    
    private Panel GenerateStatusPanel()
    {
        var status = _watchdog.GetStatus();
        
        var grid = new Grid()
            .AddColumn()
            .AddColumn()
            .AddRow("[bold]Status:[/]", status.IsSuccess && status.Value?.IsRunning == true ? "[green]Running[/]" : "[red]Stopped[/]")
            .AddRow("[bold]Queue Size:[/]", status.Value?.QueueSize.ToString() ?? "0")
            .AddRow("[bold]Processed:[/]", status.Value?.ProcessedCount.ToString() ?? "0")
            .AddRow("[bold]Successful:[/]", $"[green]{status.Value?.SuccessCount ?? 0}[/]")
            .AddRow("[bold]Failed:[/]", $"[red]{status.Value?.FailureCount ?? 0}[/]")
            .AddRow("[bold]Dropped:[/]", $"[yellow]{status.Value?.DroppedCount ?? 0}[/]");
            
        return new Panel(grid)
            .Header("[bold cyan]Watchdog Status[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("cyan"));
    }
    
    private string GetHealthStatusMarkup(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "[green]● Healthy[/]",
            HealthStatus.Degraded => "[yellow]● Degraded[/]",
            HealthStatus.Unhealthy => "[red]● Unhealthy[/]",
            _ => "[grey]● Unknown[/]"
        };
    }
}