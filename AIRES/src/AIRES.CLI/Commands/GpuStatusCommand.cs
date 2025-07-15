using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AIRES.Core.Interfaces;
using AIRES.Foundation.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AIRES.CLI.Commands;

/// <summary>
/// Command to display GPU and Ollama instance status.
/// </summary>
[Description("Display GPU and Ollama instance status")]
public class GpuStatusCommand : AsyncCommand<GpuStatusCommand.Settings>
{
    private readonly IGpuDetectionService _gpuDetection;
    private readonly IEnhancedLoadBalancerService _loadBalancer;
    private readonly IOllamaClient _ollamaClient;
    private readonly IAIRESLogger _logger;
    
    public class Settings : CommandSettings
    {
        [CommandOption("-d|--detailed")]
        [Description("Show detailed GPU information")]
        public bool Detailed { get; set; }
        
        [CommandOption("-r|--refresh")]
        [Description("Auto-refresh interval in seconds (0 to disable)")]
        [DefaultValue(0)]
        public int RefreshInterval { get; set; }
    }
    
    public GpuStatusCommand(
        IGpuDetectionService gpuDetection,
        IEnhancedLoadBalancerService loadBalancer,
        IOllamaClient ollamaClient,
        IAIRESLogger logger)
    {
        _gpuDetection = gpuDetection ?? throw new ArgumentNullException(nameof(gpuDetection));
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _logger.LogDebug("GpuStatusCommand.ExecuteAsync - Entry");
        
        try
        {
            if (settings.RefreshInterval > 0)
            {
                await AnsiConsole.Live(GenerateStatusPanel(settings.Detailed))
                    .AutoClear(false)
                    .Overflow(VerticalOverflow.Ellipsis)
                    .Cropping(VerticalOverflowCropping.Top)
                    .StartAsync(async ctx =>
                    {
                        while (!Console.KeyAvailable)
                        {
                            ctx.UpdateTarget(await GenerateStatusPanel(settings.Detailed));
                            await Task.Delay(settings.RefreshInterval * 1000);
                        }
                    });
            }
            else
            {
                var panel = await GenerateStatusPanel(settings.Detailed);
                AnsiConsole.Write(panel);
            }
            
            _logger.LogDebug("GpuStatusCommand.ExecuteAsync - Exit");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            _logger.LogError("Failed to display GPU status", ex);
            _logger.LogDebug("GpuStatusCommand.ExecuteAsync - Exit");
            return 1;
        }
    }
    
    private async Task<Panel> GenerateStatusPanel(bool detailed)
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header"),
                new Layout("GPUs"),
                new Layout("Instances"),
                new Layout("Models"));
        
        // Header
        layout["Header"].Update(
            new Panel(
                new FigletText("GPU Status")
                    .Centered()
                    .Color(Color.Green))
                .Expand());
        
        // GPU Information
        var gpuTable = await CreateGpuTable(detailed);
        layout["GPUs"].Update(new Panel(gpuTable).Header("GPU Hardware").Expand());
        
        // Instance Status
        var instanceTable = await CreateInstanceTable();
        layout["Instances"].Update(new Panel(instanceTable).Header("Ollama Instances").Expand());
        
        // Model Distribution
        var modelTable = await CreateModelTable();
        layout["Models"].Update(new Panel(modelTable).Header("Model Distribution").Expand());
        
        return new Panel(layout)
            .Header($"[bold]AIRES Multi-GPU Status[/] - {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .Expand();
    }
    
    private async Task<Table> CreateGpuTable(bool detailed)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("GPU")
            .AddColumn("Name")
            .AddColumn("Memory")
            .AddColumn("Temp")
            .AddColumn("Util");
            
        if (detailed)
        {
            table.AddColumn("Power")
                 .AddColumn("Health");
        }
        
        try
        {
            var gpus = await _gpuDetection.DetectAvailableGpusAsync();
            foreach (var gpu in gpus)
            {
                try
                {
                    var health = await _gpuDetection.ValidateGpuHealthAsync(gpu.Id);
                var tempColor = health.Temperature > 80 ? "red" : health.Temperature > 70 ? "yellow" : "green";
                var utilColor = health.GpuUtilization > 90 ? "red" : health.GpuUtilization > 70 ? "yellow" : "green";
                
                var row = new[]
                {
                    $"GPU {gpu.Id}",
                    gpu.Name,
                    $"{health.MemoryUsedMB}/{health.MemoryTotalMB} MB",
                    $"[{tempColor}]{health.Temperature}°C[/]",
                    $"[{utilColor}]{health.GpuUtilization}%[/]"
                };
                
                if (detailed)
                {
                    row = row.Concat(new[]
                    {
                        $"{health.PowerDraw:F1}W",
                        health.IsHealthy ? "[green]Healthy[/]" : "[red]Unhealthy[/]"
                    }).ToArray();
                }
                
                table.AddRow(row);
                }
                catch
                {
                    table.AddRow($"GPU {gpu.Id}", gpu.Name, "-", "[red]Error[/]", "-");
                }
            }
        }
        catch (Exception ex)
        {
            table.AddRow("[red]Error[/]", ex.Message, "-", "-", "-");
        }
        
        return table;
    }
    
    private async Task<Table> CreateInstanceTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Instance")
            .AddColumn("GPU")
            .AddColumn("Port")
            .AddColumn("Status")
            .AddColumn("Score")
            .AddColumn("Requests")
            .AddColumn("Avg Time");
        
        if (_ollamaClient is EnhancedOllamaClient enhancedClient)
        {
            var healthResult = await enhancedClient.GetHealthStatusAsync();
            if (healthResult.IsSuccess)
            {
                var health = healthResult.Value;
                
                foreach (var instance in health.Instances.OrderBy(i => i.GpuId))
                {
                    var statusIcon = instance.IsHealthy ? "[green]●[/]" : "[red]●[/]";
                    var scoreColor = instance.HealthScore > 0.8 ? "green" : 
                                   instance.HealthScore > 0.5 ? "yellow" : "red";
                    
                    table.AddRow(
                        instance.InstanceId,
                        $"GPU {instance.GpuId}",
                        instance.Port.ToString(),
                        $"{statusIcon} {(instance.IsHealthy ? "Online" : "Offline")}",
                        $"[{scoreColor}]{instance.HealthScore:F2}[/]",
                        $"{instance.ActiveRequests} active",
                        $"{instance.AverageResponseTimeMs:F0}ms"
                    );
                }
                
                // Summary row
                table.AddEmptyRow();
                table.AddRow(
                    "[bold]Total[/]",
                    "-",
                    "-",
                    $"{health.HealthyInstances}/{health.TotalInstances} healthy",
                    "-",
                    "-",
                    "-"
                );
            }
            else
            {
                table.AddRow("[red]Error[/]", healthResult.ErrorMessage, "-", "-", "-", "-", "-");
            }
        }
        else
        {
            table.AddRow("Single GPU", "GPU 0", "11434", "[green]●[/] Online", "1.00", "-", "-");
        }
        
        return table;
    }
    
    private async Task<Table> CreateModelTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Model")
            .AddColumn("Size")
            .AddColumn("Location")
            .AddColumn("Last Used");
        
        var modelsResult = await _ollamaClient.ListModelsAsync();
        if (modelsResult.IsSuccess && modelsResult.Value.Models != null)
        {
            foreach (var model in modelsResult.Value.Models.OrderBy(m => m.Name))
            {
                var sizeGB = model.Size / (1024.0 * 1024.0 * 1024.0);
                var modified = DateTimeOffset.FromUnixTimeSeconds(model.ModifiedAt ?? 0).LocalDateTime;
                
                table.AddRow(
                    model.Name,
                    $"{sizeGB:F1} GB",
                    "GPU 0", // TODO: Track which GPU has which model
                    modified.ToString("yyyy-MM-dd HH:mm")
                );
            }
        }
        else
        {
            table.AddRow("[yellow]No models found[/]", "-", "-", "-");
        }
        
        return table;
    }
}