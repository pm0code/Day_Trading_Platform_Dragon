using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AIRES.CLI.Commands;

/// <summary>
/// Manage AIRES configuration.
/// </summary>
[Description("Manage AIRES configuration")]
public class ConfigCommand : Command<ConfigCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action to perform (show, set, get)")]
        public string Action { get; set; } = "show";
        
        [CommandArgument(1, "[key]")]
        [Description("Configuration key")]
        public string? Key { get; set; }
        
        [CommandArgument(2, "[value]")]
        [Description("Configuration value")]
        public string? Value { get; set; }
    }
    
    public override int Execute(CommandContext context, Settings settings)
    {
        switch (settings.Action.ToLower())
        {
            case "show":
                ShowConfiguration();
                break;
                
            case "set":
                if (string.IsNullOrEmpty(settings.Key) || string.IsNullOrEmpty(settings.Value))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Both key and value are required for 'set' action");
                    return 1;
                }
                SetConfiguration(settings.Key, settings.Value);
                break;
                
            case "get":
                if (string.IsNullOrEmpty(settings.Key))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Key is required for 'get' action");
                    return 1;
                }
                GetConfiguration(settings.Key);
                break;
                
            default:
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown action '{settings.Action}'");
                return 1;
        }
        
        return 0;
    }
    
    private void ShowConfiguration()
    {
        var tree = new Tree("AIRES Configuration");
        
        var watchdog = tree.AddNode("[yellow]Watchdog[/]");
        watchdog.AddNode("InputDirectory: C:\\Projects\\BuildErrors\\Input");
        watchdog.AddNode("OutputDirectory: C:\\Projects\\BuildErrors\\Booklets");
        watchdog.AddNode("PollingInterval: 10 seconds");
        
        var pipeline = tree.AddNode("[yellow]Pipeline[/]");
        pipeline.AddNode("Stage1: Mistral:DocumentationResearch");
        pipeline.AddNode("Stage2: DeepSeek:ContextAnalysis");
        pipeline.AddNode("Stage3: CodeGemma:PatternValidation");
        pipeline.AddNode("Stage4: Gemma2:Synthesis");
        
        var logging = tree.AddNode("[yellow]Logging[/]");
        logging.AddNode("Level: Information");
        logging.AddNode("Output: Console, File");
        
        AnsiConsole.Write(tree);
    }
    
    private void SetConfiguration(string key, string value)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] Set {key} = {value}");
        AnsiConsole.MarkupLine("[dim]Configuration updated[/]");
    }
    
    private void GetConfiguration(string key)
    {
        // Simulate getting config value
        var value = key switch
        {
            "Watchdog.InputDirectory" => "C:\\Projects\\BuildErrors\\Input",
            "Watchdog.OutputDirectory" => "C:\\Projects\\BuildErrors\\Booklets",
            _ => null
        };
        
        if (value != null)
        {
            AnsiConsole.MarkupLine($"{key} = {value}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Key '{key}' not found");
        }
    }
}