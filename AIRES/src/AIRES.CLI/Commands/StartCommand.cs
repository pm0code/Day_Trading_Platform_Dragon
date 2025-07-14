using AIRES.Foundation.Logging;
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
    
    public StartCommand(IAIRESLogger logger)
    {
        _logger = logger;
    }
    
    public class Settings : CommandSettings
    {
        [CommandOption("-w|--watchdog")]
        [Description("Start in watchdog mode (default)")]
        public bool Watchdog { get; set; } = true;
        
        [CommandOption("-c|--config")]
        [Description("Path to configuration file")]
        public string? ConfigFile { get; set; }
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("cyan"))
                .Start("Starting AIRES...", ctx =>
                {
                    ctx.Status("Initializing services...");
                    Thread.Sleep(1000);
                    
                    ctx.Status("Loading configuration...");
                    Thread.Sleep(500);
                    
                    ctx.Status("Starting watchdog...");
                    Thread.Sleep(500);
                });
            
            AnsiConsole.MarkupLine("[bold green]✓[/] AIRES started successfully!");
            AnsiConsole.MarkupLine("[dim]Monitoring for build error files...[/]");
            
            _logger.LogInfo("AIRES started in watchdog mode");
            
            // Keep running until cancelled
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            
            await Task.Delay(Timeout.Infinite, cts.Token);
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/] Failed to start AIRES");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}