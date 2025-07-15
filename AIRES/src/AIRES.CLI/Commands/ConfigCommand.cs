using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using AIRES.Core.Configuration;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Reflection;

namespace AIRES.CLI.Commands;

/// <summary>
/// Manage AIRES configuration.
/// </summary>
[Description("Manage AIRES configuration")]
public class ConfigCommand : AsyncCommand<ConfigCommand.Settings>
{
    private readonly IAIRESConfiguration _configuration;
    private readonly IAIRESLogger _logger;
    
    public ConfigCommand(IAIRESConfiguration configuration, IAIRESLogger logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action to perform (show, set, get, reload)")]
        public string Action { get; set; } = "show";
        
        [CommandArgument(1, "[key]")]
        [Description("Configuration key (e.g., Directories.InputDirectory)")]
        public string? Key { get; set; }
        
        [CommandArgument(2, "[value]")]
        [Description("Configuration value")]
        public string? Value { get; set; }
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        LogMethodEntry();
        
        try
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
                        LogMethodExit();
                        return 1;
                    }
                    await SetConfigurationAsync(settings.Key, settings.Value);
                    break;
                    
                case "get":
                    if (string.IsNullOrEmpty(settings.Key))
                    {
                        AnsiConsole.MarkupLine("[red]Error:[/] Key is required for 'get' action");
                        LogMethodExit();
                        return 1;
                    }
                    GetConfiguration(settings.Key);
                    break;
                    
                case "reload":
                    await ReloadConfigurationAsync();
                    break;
                    
                default:
                    AnsiConsole.MarkupLine($"[red]Error:[/] Unknown action '{settings.Action}'");
                    LogMethodExit();
                    return 1;
            }
            
            LogMethodExit();
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Configuration command failed", ex);
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            LogMethodExit();
            return -1;
        }
    }
    
    private void ShowConfiguration()
    {
        LogMethodEntry();
        
        var tree = new Tree("[bold]AIRES Configuration[/]");
        
        // Directories
        var directories = tree.AddNode("[yellow]Directories[/]");
        directories.AddNode($"InputDirectory: {_configuration.Directories.InputDirectory}");
        directories.AddNode($"OutputDirectory: {_configuration.Directories.OutputDirectory}");
        directories.AddNode($"TempDirectory: {_configuration.Directories.TempDirectory}");
        directories.AddNode($"AlertDirectory: {_configuration.Directories.AlertDirectory}");
        directories.AddNode($"LogDirectory: {_configuration.Directories.LogDirectory}");
        
        // AI Services
        var aiServices = tree.AddNode("[yellow]AI Services[/]");
        aiServices.AddNode($"OllamaBaseUrl: {_configuration.AIServices.OllamaBaseUrl}");
        aiServices.AddNode($"OllamaTimeout: {_configuration.AIServices.OllamaTimeout}s");
        aiServices.AddNode($"MistralModel: {_configuration.AIServices.MistralModel}");
        aiServices.AddNode($"DeepSeekModel: {_configuration.AIServices.DeepSeekModel}");
        aiServices.AddNode($"CodeGemmaModel: {_configuration.AIServices.CodeGemmaModel}");
        aiServices.AddNode($"Gemma2Model: {_configuration.AIServices.Gemma2Model}");
        
        // Pipeline
        var pipeline = tree.AddNode("[yellow]Pipeline[/]");
        pipeline.AddNode($"MaxRetries: {_configuration.Pipeline.MaxRetries}");
        pipeline.AddNode($"RetryDelay: {_configuration.Pipeline.RetryDelay}s");
        pipeline.AddNode($"EnableParallelProcessing: {_configuration.Pipeline.EnableParallelProcessing}");
        pipeline.AddNode($"BatchSize: {_configuration.Pipeline.BatchSize}");
        
        // Watchdog
        var watchdog = tree.AddNode("[yellow]Watchdog[/]");
        watchdog.AddNode($"Enabled: {_configuration.Watchdog.Enabled}");
        watchdog.AddNode($"PollingInterval: {_configuration.Watchdog.PollingIntervalSeconds}s");
        watchdog.AddNode($"MaxQueueSize: {_configuration.Watchdog.MaxQueueSize}");
        watchdog.AddNode($"ProcessingThreads: {_configuration.Watchdog.ProcessingThreads}");
        
        // Processing
        var processing = tree.AddNode("[yellow]Processing[/]");
        processing.AddNode($"MaxFileSizeMB: {_configuration.Processing.MaxFileSizeMB}");
        processing.AddNode($"AllowedExtensions: {_configuration.Processing.AllowedExtensions}");
        processing.AddNode($"MaxErrorsPerFile: {_configuration.Processing.MaxErrorsPerFile}");
        
        // Alerting
        var alerting = tree.AddNode("[yellow]Alerting[/]");
        alerting.AddNode($"Enabled: {_configuration.Alerting.Enabled}");
        alerting.AddNode($"ConsoleAlerts: {_configuration.Alerting.ConsoleAlerts}");
        alerting.AddNode($"FileAlerts: {_configuration.Alerting.FileAlerts}");
        alerting.AddNode($"WindowsEventLog: {_configuration.Alerting.WindowsEventLog}");
        
        // Logging
        var logging = tree.AddNode("[yellow]Logging[/]");
        logging.AddNode($"LogLevel: {_configuration.Logging.LogLevel}");
        logging.AddNode($"StructuredLogging: {_configuration.Logging.StructuredLogging}");
        logging.AddNode($"LogFilePath: {_configuration.Logging.LogFilePath}");
        logging.AddNode($"ConsoleLogging: {_configuration.Logging.ConsoleLoggingEnabled}");
        
        AnsiConsole.Write(tree);
        
        LogMethodExit();
    }
    
    private async Task SetConfigurationAsync(string key, string value)
    {
        LogMethodEntry();
        
        try
        {
            // Convert nested property format to INI section:property format
            var iniKey = ConvertToIniKey(key);
            
            if (iniKey == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid configuration key: {key}");
                AnsiConsole.MarkupLine("[dim]Use format: Section.Property (e.g., Directories.InputDirectory)[/]");
                LogMethodExit();
                return;
            }
            
            await _configuration.SetValueAsync(iniKey, value);
            AnsiConsole.MarkupLine($"[green]✓[/] Set {key} = {value}");
            AnsiConsole.MarkupLine("[dim]Configuration updated and saved to aires.ini[/]");
            _logger.LogInfo($"Configuration updated: {key} = {value}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to set configuration: {ex.Message}");
            _logger.LogError($"Failed to set configuration key '{key}'", ex);
        }
        
        LogMethodExit();
    }
    
    private void GetConfiguration(string key)
    {
        LogMethodEntry();
        
        try
        {
            // Try to get value using reflection for nested properties
            var value = GetNestedPropertyValue(_configuration, key);
            
            if (value != null)
            {
                AnsiConsole.MarkupLine($"{key} = {value}");
            }
            else
            {
                // Try raw value
                var rawValue = _configuration.GetValue(key);
                if (rawValue != null)
                {
                    AnsiConsole.MarkupLine($"{key} = {rawValue}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Key '{key}' not found");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to get configuration: {ex.Message}");
            _logger.LogError($"Failed to get configuration key '{key}'", ex);
        }
        
        LogMethodExit();
    }
    
    private async Task ReloadConfigurationAsync()
    {
        LogMethodEntry();
        
        try
        {
            AnsiConsole.MarkupLine("[yellow]Reloading configuration...[/]");
            await _configuration.ReloadAsync();
            AnsiConsole.MarkupLine("[green]✓[/] Configuration reloaded successfully");
            _logger.LogInfo("Configuration reloaded");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to reload configuration: {ex.Message}");
            _logger.LogError("Failed to reload configuration", ex);
        }
        
        LogMethodExit();
    }
    
    private object? GetNestedPropertyValue(object obj, string propertyPath)
    {
        LogMethodEntry();
        
        try
        {
            var properties = propertyPath.Split('.');
            object? current = obj;
            
            foreach (var prop in properties)
            {
                if (current == null) break;
                
                var type = current.GetType();
                var property = type.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                
                if (property != null)
                {
                    current = property.GetValue(current);
                }
                else
                {
                    LogMethodExit();
                    return null;
                }
            }
            
            LogMethodExit();
            return current;
        }
        catch
        {
            LogMethodExit();
            return null;
        }
    }
    
    private void LogMethodEntry([System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
    {
        _logger.LogDebug($"[ConfigCommand] Entering {methodName}");
    }
    
    private void LogMethodExit([System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
    {
        _logger.LogDebug($"[ConfigCommand] Exiting {methodName}");
    }
    
    private void LogDebug(string message)
    {
        _logger.LogDebug($"[ConfigCommand] {message}");
    }
    
    private void LogWarning(string message)
    {
        _logger.LogWarning($"[ConfigCommand] {message}");
    }
    
    private void LogError(string message, Exception ex)
    {
        _logger.LogError($"[ConfigCommand] {message}", ex);
    }
    
    private string? ConvertToIniKey(string key)
    {
        LogMethodEntry();
        
        try
        {
            // Map configuration section names to INI section names
            var sectionMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Directories", "Directories" },
                { "AIServices", "AI_Services" },
                { "Pipeline", "Pipeline" },
                { "Watchdog", "Watchdog" },
                { "Processing", "Processing" },
                { "Alerting", "Alerting" },
                { "Logging", "Logging" },
                { "Monitoring", "Monitoring" },
                { "Security", "Security" },
                { "Performance", "Performance" },
                { "Development", "Development" }
            };
            
            // Split the key into section and property
            var parts = key.Split('.');
            if (parts.Length != 2)
            {
                LogWarning($"Invalid key format: {key}. Expected Section.Property");
                LogMethodExit();
                return null;
            }
            
            var section = parts[0];
            var property = parts[1];
            
            // Map the section name
            if (!sectionMappings.TryGetValue(section, out var iniSection))
            {
                LogWarning($"Unknown configuration section: {section}");
                LogMethodExit();
                return null;
            }
            
            var iniKey = $"{iniSection}:{property}";
            LogDebug($"Converted key '{key}' to INI key '{iniKey}'");
            
            LogMethodExit();
            return iniKey;
        }
        catch (Exception ex)
        {
            LogError($"Error converting key '{key}' to INI format", ex);
            LogMethodExit();
            return null;
        }
    }
}