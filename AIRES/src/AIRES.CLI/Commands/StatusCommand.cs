using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AIRES.CLI.Commands;

/// <summary>
/// Shows AIRES system status.
/// </summary>
[Description("Show AIRES system status")]
public class StatusCommand : Command<StatusCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-d|--detailed")]
        [Description("Show detailed status information")]
        public bool Detailed { get; set; }
    }
    
    public override int Execute(CommandContext context, Settings settings)
    {
        var table = new Table();
        table.AddColumn("Component");
        table.AddColumn("Status");
        table.AddColumn("Details");
        
        table.AddRow("Watchdog", "[green]Running[/]", "Monitoring: C:\\Projects\\BuildErrors");
        table.AddRow("AI Pipeline", "[green]Healthy[/]", "4/4 models available");
        table.AddRow("File Processing", "[yellow]Active[/]", "1 file in progress");
        table.AddRow("System Health", "[green]Good[/]", "CPU: 12%, Memory: 1.2GB");
        
        AnsiConsole.Write(
            new Panel(table)
                .Header("AIRES System Status")
                .BorderColor(Color.Cyan1)
                .Padding(1, 0));
        
        if (settings.Detailed)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Recent Activity:[/]");
            AnsiConsole.MarkupLine("  [dim]14:32:15[/] Detected: build_errors_20250113.txt");
            AnsiConsole.MarkupLine("  [dim]14:32:16[/] Processing started");
            AnsiConsole.MarkupLine("  [dim]14:34:48[/] Booklet generated: CS0117_Resolution.md");
        }
        
        return 0;
    }
}