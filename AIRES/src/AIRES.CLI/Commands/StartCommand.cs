using AIRES.Core.Configuration;
using AIRES.Foundation.Logging;
using AIRES.Watchdog.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AIRES.CLI.Commands;

/// <summary>
/// Starts AIRES in autonomous watchdog mode.
/// </summary>
[Description("Start AIRES in autonomous watchdog mode")]
public class StartCommand : AsyncCommand<StartCommand.Settings>
{
    private readonly IAIRESLogger _logger;
    private readonly IAIRESConfiguration _configuration;
    private readonly IFileWatchdogService _watchdog;
    
    public StartCommand(
        IAIRESLogger logger,
        IAIRESConfiguration configuration,
        IFileWatchdogService watchdog)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
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
            
            // Initialize services
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("Starting AIRES...", async ctx =>
                {
                    ctx.Status("Initializing watchdog service...");
                    var initResult = await _watchdog.InitializeAsync();
                    if (!initResult.IsSuccess)
                    {
                        throw new InvalidOperationException($"Failed to initialize watchdog: {initResult.ErrorMessage}");
                    }
                    
                    ctx.Status("Loading configuration...");
                    await Task.Delay(500); // Allow time for configuration to settle
                    
                    ctx.Status("Starting file monitoring...");
                    var startResult = await _watchdog.StartAsync();
                    if (!startResult.IsSuccess)
                    {
                        throw new InvalidOperationException($"Failed to start watchdog: {startResult.ErrorMessage}");
                    }
                });
            
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
}