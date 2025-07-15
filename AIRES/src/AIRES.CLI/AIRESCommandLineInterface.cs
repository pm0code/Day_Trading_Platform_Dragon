using AIRES.CLI.Commands;
using AIRES.CLI.Health;
using AIRES.Foundation;
using AIRES.Core;
using AIRES.Watchdog;
using AIRES.Infrastructure;
using AIRES.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AIRES.CLI;

/// <summary>
/// AIRES Command Line Interface entry point.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Display AIRES banner
        DisplayBanner();
        
        try
        {
            // Build host
            var host = CreateHostBuilder(args).Build();
            
            // Create command app
            var app = new CommandApp(new TypeRegistrar(host.Services));
            
            app.Configure(config =>
            {
                config.SetApplicationName("aires");
                config.SetApplicationVersion("1.0.0");
                
                config.AddCommand<StartCommand>("start")
                    .WithDescription("Start AIRES in autonomous watchdog mode")
                    .WithExample("start", "--watchdog")
                    .WithExample("start", "--config", "custom.ini");
                
                config.AddCommand<ProcessCommand>("process")
                    .WithDescription("Process a specific error file")
                    .WithExample("process", "build_errors.txt");
                
                config.AddCommand<StatusCommand>("status")
                    .WithDescription("Show AIRES system status")
                    .WithExample("status");
                
                config.AddCommand<HealthCheckCommand>("health")
                    .WithDescription("Perform health checks on all AIRES components")
                    .WithExample("health")
                    .WithExample("health", "--quick")
                    .WithExample("health", "--format", "json");
                
                config.AddCommand<ConfigCommand>("config")
                    .WithDescription("Manage AIRES configuration")
                    .WithExample("config", "show")
                    .WithExample("config", "set", "Watchdog.InputDirectory", "/path/to/errors");
                
                config.AddCommand<GpuStatusCommand>("gpu-status")
                    .WithDescription("Display GPU and Ollama instance status")
                    .WithExample("gpu-status")
                    .WithExample("gpu-status", "--detailed")
                    .WithExample("gpu-status", "--refresh", "5");
            });
            
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return -1;
        }
    }
    
    private static void DisplayBanner()
    {
        AnsiConsole.Write(
            new FigletText("AIRES")
                .Centered()
                .Color(Color.Green));
                
        AnsiConsole.MarkupLine("[bold green]AI Error Resolution System v1.0[/]");
        AnsiConsole.MarkupLine("[dim]Autonomous error analysis through AI pipeline[/]");
        AnsiConsole.WriteLine();
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddIniFile("aires.ini", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "AIRES_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                var logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithProcessId()
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine("logs", "aires-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30)
                    .CreateLogger();
                
                services.AddSingleton<Serilog.ILogger>(logger);
                
                // Register AIRES services
                services.AddAIRESFoundation();
                services.AddAIRESCore();
                services.AddAIRESWatchdog();
                services.AddAIRESInfrastructure();
                services.AddAIRESApplication();
                
                // Register factories for scoped services
                services.AddSingleton<Health.IAIResearchOrchestratorServiceFactory, Health.AIResearchOrchestratorServiceFactory>();
                services.AddSingleton<Health.IBookletPersistenceServiceFactory, Health.BookletPersistenceServiceFactory>();
                
                // Register health check services
                services.AddSingleton<Health.HealthCheckExecutor>();
                
                // Register individual health checks
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.OrchestratorHealthCheck(
                        sp.GetRequiredService<Health.IAIResearchOrchestratorServiceFactory>()));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.WatchdogHealthCheck(
                        sp.GetRequiredService<Watchdog.Services.IFileWatchdogService>()));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.ConfigurationHealthCheck(
                        sp.GetRequiredService<Core.Configuration.IAIRESConfiguration>()));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.FileSystemHealthCheck(
                        sp.GetRequiredService<Core.Configuration.IAIRESConfiguration>()));
                
                // Register AI service health checks
                // Note: AI services are registered by the infrastructure, so we create simple health checks
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.AIServiceHealthCheck(
                        new object(), // Placeholder - actual service checked through IHealthCheckable
                        "Mistral Documentation"));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.AIServiceHealthCheck(
                        new object(), // Placeholder - actual service checked through IHealthCheckable
                        "DeepSeek Context"));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.AIServiceHealthCheck(
                        new object(), // Placeholder - actual service checked through IHealthCheckable
                        "CodeGemma Pattern"));
                
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.AIServiceHealthCheck(
                        new object(), // Placeholder - actual service checked through IHealthCheckable
                        "Gemma2 Booklet"));
                
                // Register Booklet Persistence health check
                services.AddSingleton<Health.IHealthCheck>(sp => 
                    new Health.BookletPersistenceHealthCheck(
                        sp.GetRequiredService<Health.IBookletPersistenceServiceFactory>()));
                
                // Register commands
                services.AddTransient<StartCommand>();
                services.AddTransient<ProcessCommand>();
                services.AddTransient<StatusCommand>();
                services.AddTransient<ConfigCommand>();
                services.AddTransient<HealthCheckCommand>();
                services.AddTransient<GpuStatusCommand>();
            })
            .UseSerilog();
}

/// <summary>
/// Type registrar for Spectre.Console DI integration.
/// </summary>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _provider;
    
    public TypeRegistrar(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }
    
    public ITypeResolver Build()
    {
        return new TypeResolver(_provider);
    }
    
    public void Register(Type service, Type implementation)
    {
        // Do nothing - we use the existing IServiceProvider
    }
    
    public void RegisterInstance(Type service, object implementation)
    {
        // Do nothing - we use the existing IServiceProvider
    }
    
    public void RegisterLazy(Type service, Func<object> factory)
    {
        // Do nothing - we use the existing IServiceProvider
    }
}

internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    
    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }
    
    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }
        
        try
        {
            return _provider.GetService(type);
        }
        catch
        {
            // Silently fail - Spectre.Console will handle null results
            return null;
        }
    }
}