using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AIRES.CLI.Health;
using AIRES.Core.Health;
using AIRES.Foundation.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AIRES.CLI.Commands;

/// <summary>
/// Performs comprehensive health checks on all AIRES components.
/// </summary>
[Description("Perform health checks on all AIRES components")]
public class HealthCheckCommand : AsyncCommand<HealthCheckCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
    
    private readonly IAIRESLogger _logger;
    private readonly HealthCheckExecutor _healthCheckExecutor;

    public HealthCheckCommand(
        IAIRESLogger logger,
        HealthCheckExecutor healthCheckExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckExecutor = healthCheckExecutor ?? throw new ArgumentNullException(nameof(healthCheckExecutor));
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-q|--quick")]
        [Description("Run quick health check on critical components only")]
        public bool Quick { get; set; }

        [CommandOption("-d|--detailed")]
        [Description("Show detailed diagnostics for each component")]
        [DefaultValue(true)]
        public bool Detailed { get; set; } = true;

        [CommandOption("-f|--format")]
        [Description("Output format (table, json, simple)")]
        [DefaultValue("table")]
        public string Format { get; set; } = "table";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _logger.LogInfo($"Starting health checks (quick: {settings.Quick}, detailed: {settings.Detailed})");

            // Display header
            var rule = new Rule("[cyan]AIRES Health Check[/]")
                .RuleStyle("cyan dim")
                .LeftJustified();
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Run health checks with status indicator
            HealthCheckReport report = null!;
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync(settings.Quick ? "Running quick health check..." : "Running comprehensive health check...", async ctx =>
                {
                    report = settings.Quick 
                        ? await _healthCheckExecutor.RunQuickHealthCheckAsync()
                        : await _healthCheckExecutor.RunHealthChecksAsync(settings.Detailed);
                });

            // Display results based on format
            switch (settings.Format.ToLower())
            {
                case "json":
                    DisplayJsonResults(report);
                    break;
                case "simple":
                    DisplaySimpleResults(report);
                    break;
                default:
                    DisplayTableResults(report, settings.Detailed);
                    break;
            }

            // Display summary
            AnsiConsole.WriteLine();
            DisplaySummary(report);

            _logger.LogInfo($"Health check completed. Overall status: {report.OverallStatus}");

            // Return exit code based on health status
            return report.OverallStatus switch
            {
                HealthStatus.Healthy => 0,
                HealthStatus.Degraded => 1,
                HealthStatus.Unhealthy => 2,
                _ => 3
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/] Health check failed");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            _logger.LogError("Health check command failed", ex);
            return -1;
        }
    }

    private void DisplayTableResults(HealthCheckReport report, bool detailed)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Component", c => c.Width(25))
            .AddColumn("Type", c => c.Width(15))
            .AddColumn("Status", c => c.Width(12).Centered())
            .AddColumn("Response Time", c => c.Width(15).RightAligned())
            .AddColumn("Details", c => c.Width(40));

        // Group results by component type
        var groupedResults = report.Results
            .OrderBy(r => r.Value.ComponentType)
            .ThenBy(r => r.Key);

        foreach (var result in groupedResults)
        {
            var statusMarkup = result.Value.Status switch
            {
                HealthStatus.Healthy => "[green]● Healthy[/]",
                HealthStatus.Degraded => "[yellow]● Degraded[/]",
                HealthStatus.Unhealthy => "[red]● Unhealthy[/]",
                _ => "[grey]● Unknown[/]"
            };

            var details = "";
            if (result.Value.Status != HealthStatus.Healthy)
            {
                details = result.Value.ErrorMessage ?? string.Join("; ", result.Value.FailureReasons);
            }
            else if (detailed && result.Value.Diagnostics.Any())
            {
                var keyDiagnostics = result.Value.Diagnostics
                    .Take(3)
                    .Select(kvp => $"{kvp.Key}: {kvp.Value}");
                details = string.Join(", ", keyDiagnostics);
            }

            table.AddRow(
                result.Key,
                result.Value.ComponentType,
                statusMarkup,
                $"{result.Value.ResponseTimeMs}ms",
                Markup.Escape(details)
            );
        }

        AnsiConsole.Write(table);
    }

    private void DisplaySimpleResults(HealthCheckReport report)
    {
        foreach (var result in report.Results.OrderBy(r => r.Key))
        {
            var status = result.Value.Status switch
            {
                HealthStatus.Healthy => "[green]PASS[/]",
                HealthStatus.Degraded => "[yellow]WARN[/]",
                HealthStatus.Unhealthy => "[red]FAIL[/]",
                _ => "[grey]UNKNOWN[/]"
            };

            AnsiConsole.MarkupLine($"{status} {result.Key}");
            
            if (result.Value.Status != HealthStatus.Healthy && !string.IsNullOrEmpty(result.Value.ErrorMessage))
            {
                AnsiConsole.MarkupLine($"  [dim]→ {Markup.Escape(result.Value.ErrorMessage)}[/]");
            }
        }
    }

    private void DisplayJsonResults(HealthCheckReport report)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(report, JsonOptions);
        
        AnsiConsole.WriteLine(json);
    }

    private void DisplaySummary(HealthCheckReport report)
    {
        var panel = new Panel(new Markup($"""
            [bold]Overall Status:[/] {GetOverallStatusMarkup(report.OverallStatus)}
            [bold]Duration:[/] {report.TotalDuration}ms
            [bold]Summary:[/] {report.Summary}
            [bold]Checked At:[/] {report.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC
            """))
        {
            Header = new PanelHeader("[bold]Health Check Summary[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(report.OverallStatus switch
            {
                HealthStatus.Healthy => Color.Green,
                HealthStatus.Degraded => Color.Yellow,
                HealthStatus.Unhealthy => Color.Red,
                _ => Color.Grey
            }),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);

        // Display action items if not healthy
        if (report.HasFailures || report.HasWarnings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Action Required:[/]");
            
            if (report.HasFailures)
            {
                var failedComponents = report.Results
                    .Where(r => r.Value.Status == HealthStatus.Unhealthy)
                    .Select(r => r.Key);
                    
                AnsiConsole.MarkupLine($"  [red]✗[/] Fix unhealthy components: {string.Join(", ", failedComponents)}");
            }
            
            if (report.HasWarnings)
            {
                var degradedComponents = report.Results
                    .Where(r => r.Value.Status == HealthStatus.Degraded)
                    .Select(r => r.Key);
                    
                AnsiConsole.MarkupLine($"  [yellow]![/] Review degraded components: {string.Join(", ", degradedComponents)}");
            }
        }
    }

    private string GetOverallStatusMarkup(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "[green]● All Systems Operational[/]",
            HealthStatus.Degraded => "[yellow]● Degraded Performance[/]",
            HealthStatus.Unhealthy => "[red]● System Issues Detected[/]",
            _ => "[grey]● Unknown[/]"
        };
    }
}