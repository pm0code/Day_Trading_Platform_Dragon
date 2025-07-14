using AIRES.CLI.Commands;
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
                
                config.AddCommand<ConfigCommand>("config")
                    .WithDescription("Manage AIRES configuration")
                    .WithExample("config", "show")
                    .WithExample("config", "set", "Watchdog.InputDirectory", "/path/to/errors");
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
                .Color(Color.Cyan1));
                
        AnsiConsole.MarkupLine("[bold cyan]AI Error Resolution System v1.0[/]");
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
                
                // Register commands
                services.AddTransient<StartCommand>();
                services.AddTransient<ProcessCommand>();
                services.AddTransient<StatusCommand>();
                services.AddTransient<ConfigCommand>();
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
        _provider = provider;
    }
    
    public ITypeResolver Build()
    {
        return new TypeResolver(_provider);
    }
    
    public void Register(Type service, Type implementation)
    {
        // Not needed for our use case
    }
    
    public void RegisterInstance(Type service, object implementation)
    {
        // Not needed for our use case
    }
    
    public void RegisterLazy(Type service, Func<object> factory)
    {
        // Not needed for our use case
    }
}

internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    
    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }
    
    public object? Resolve(Type? type)
    {
        return type != null ? _provider.GetService(type) : null;
    }
}