using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
/// Shows AIRES system status with real-time health information.
/// </summary>
[Description("Show AIRES system status")]
public class StatusCommand : AsyncCommand<StatusCommand.Settings>
{
    private readonly IAIRESLogger _logger;
    private readonly IAIRESConfiguration _configuration;
    private readonly IFileWatchdogService _watchdog;
    private readonly HealthCheckExecutor _healthCheckExecutor;

    public StatusCommand(
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
        [CommandOption("-d|--detailed")]
        [Description("Show detailed status information")]
        public bool Detailed { get; set; }

        [CommandOption("-h|--health")]
        [Description("Include health check status")]
        [DefaultValue(true)]
        public bool IncludeHealth { get; set; } = true;
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _logger.LogInfo("Retrieving system status");

            // Display header
            var rule = new Rule("[cyan]AIRES System Status[/]")
                .RuleStyle("cyan dim")
                .LeftJustified();
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Get watchdog status
            var watchdogStatus = _watchdog.GetStatus();
            
            // Run quick health check if requested
            HealthCheckReport? healthReport = null;
            if (settings.IncludeHealth)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("Checking system health...", async ctx =>
                    {
                        healthReport = await _healthCheckExecutor.RunQuickHealthCheckAsync();
                    });
            }

            // Create status table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn("Component", c => c.Width(20))
                .AddColumn("Status", c => c.Width(15).Centered())
                .AddColumn("Details", c => c.Width(45));

            // Watchdog status
            if (watchdogStatus.IsSuccess && watchdogStatus.Value != null)
            {
                var status = watchdogStatus.Value;
                var watchdogStatusMarkup = status.IsRunning ? "[green]● Running[/]" : "[red]● Stopped[/]";
                var watchdogDetails = $"Queue: {status.QueueSize}, Processed: {status.ProcessedCount}, Success: {status.SuccessCount}, Failed: {status.FailureCount}";
                
                table.AddRow("File Watchdog", watchdogStatusMarkup, watchdogDetails);
                
                if (status.MonitoredDirectories.Any())
                {
                    table.AddRow("Monitored Paths", "[blue]Active[/]", string.Join(", ", status.MonitoredDirectories));
                }
            }
            else
            {
                table.AddRow("File Watchdog", "[red]● Error[/]", watchdogStatus.ErrorMessage ?? "Unable to retrieve status");
            }

            // Configuration status
            var configStatus = $"Input: {_configuration.Directories.InputDirectory}, Output: {_configuration.Directories.OutputDirectory}";
            table.AddRow("Configuration", "[green]● Loaded[/]", configStatus);

            // Health check status
            if (healthReport != null)
            {
                var healthStatusMarkup = healthReport.OverallStatus switch
                {
                    HealthStatus.Healthy => "[green]● Healthy[/]",
                    HealthStatus.Degraded => "[yellow]● Degraded[/]",
                    HealthStatus.Unhealthy => "[red]● Unhealthy[/]",
                    _ => "[grey]● Unknown[/]"
                };
                
                table.AddRow("System Health", healthStatusMarkup, healthReport.Summary);
                
                // Add individual component health if detailed
                if (settings.Detailed)
                {
                    foreach (var result in healthReport.Results.OrderBy(r => r.Key))
                    {
                        var componentStatus = result.Value.Status switch
                        {
                            HealthStatus.Healthy => "[green]✓[/]",
                            HealthStatus.Degraded => "[yellow]![/]",
                            HealthStatus.Unhealthy => "[red]✗[/]",
                            _ => "[grey]?[/]"
                        };
                        
                        var details = result.Value.Status == HealthStatus.Healthy 
                            ? "Operational" 
                            : result.Value.ErrorMessage ?? string.Join("; ", result.Value.FailureReasons);
                            
                        table.AddRow($"  {componentStatus} {result.Key}", "", Markup.Escape(details));
                    }
                }
            }

            // System metrics
            var process = Process.GetCurrentProcess();
            var cpuTime = process.TotalProcessorTime.TotalSeconds;
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var threadCount = process.Threads.Count;
            
            table.AddRow("System Resources", "[green]● Normal[/]", $"CPU Time: {cpuTime:F1}s, Memory: {memoryMB}MB, Threads: {threadCount}");

            AnsiConsole.Write(table);

            // Recent activity (if detailed)
            if (settings.Detailed && watchdogStatus.IsSuccess && watchdogStatus.Value != null)
            {
                AnsiConsole.WriteLine();
                var activityPanel = new Panel(
                    new Markup($"""
                    [bold]Queue Activity:[/]
                      Current Queue Size: {watchdogStatus.Value.QueueSize}
                      Total Processed: {watchdogStatus.Value.ProcessedCount}
                      Success Rate: {(watchdogStatus.Value.ProcessedCount > 0 ? (double)watchdogStatus.Value.SuccessCount / watchdogStatus.Value.ProcessedCount : 0):P1}
                      Dropped Files: {watchdogStatus.Value.DroppedCount}
                    
                    [bold]Configuration:[/]
                      Polling Interval: {_configuration.Watchdog.PollingIntervalSeconds}s
                      Max Queue Size: {_configuration.Watchdog.MaxQueueSize}
                      Processing Threads: {_configuration.Watchdog.ProcessingThreads}
                      Allowed Extensions: {_configuration.Processing.AllowedExtensions}
                    """))
                {
                    Header = new PanelHeader("[bold]Detailed Information[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey),
                    Padding = new Padding(2, 1)
                };
                
                AnsiConsole.Write(activityPanel);
            }

            _logger.LogInfo("Status command completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/] Failed to retrieve system status");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            _logger.LogError("Status command failed", ex);
            return 1;
        }
    }
}