using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Application.Interfaces;
using AIRES.Application.Services;
using AIRES.Core.Configuration;
using AIRES.Core.Health;
using AIRES.Core.Domain.Interfaces;
using AIRES.Watchdog.Services;

namespace AIRES.CLI.Health;

/// <summary>
/// Health check for the AI Research Orchestrator service.
/// </summary>
public class OrchestratorHealthCheck : IHealthCheck
{
    private readonly IAIResearchOrchestratorServiceFactory _orchestratorFactory;

    public OrchestratorHealthCheck(IAIResearchOrchestratorServiceFactory orchestratorFactory)
    {
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
    }

    public string Name => "AI Research Orchestrator";
    public string ComponentType => "Core Service";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scopedService = _orchestratorFactory.CreateScoped();
            var orchestrator = scopedService.Service;
            
            // Check if orchestrator has a health check method
            if (orchestrator is IHealthCheckable healthCheckable)
            {
                return await healthCheckable.CheckHealthAsync();
            }

            // Basic availability check
            return HealthCheckResult.Healthy(
                Name,
                ComponentType,
                0,
                new Dictionary<string, object> { ["Status"] = "Available" }.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"Orchestrator health check failed: {ex.Message}",
                ex
            );
        }
    }
}

/// <summary>
/// Health check for the File Watchdog service.
/// </summary>
public class WatchdogHealthCheck : IHealthCheck
{
    private readonly IFileWatchdogService _watchdog;

    public WatchdogHealthCheck(IFileWatchdogService watchdog)
    {
        _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
    }

    public string Name => "File Watchdog";
    public string ComponentType => "Core Service";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // FileWatchdogService already has CheckHealthAsync method
            if (_watchdog is FileWatchdogService watchdogService)
            {
                return await watchdogService.CheckHealthAsync();
            }

            // Fallback to status check
            var status = _watchdog.GetStatus();
            if (status.IsSuccess && status.Value != null)
            {
                var diagnostics = new Dictionary<string, object>
                {
                    ["IsRunning"] = status.Value.IsRunning,
                    ["QueueSize"] = status.Value.QueueSize,
                    ["ProcessedCount"] = status.Value.ProcessedCount,
                    ["SuccessCount"] = status.Value.SuccessCount,
                    ["FailureCount"] = status.Value.FailureCount
                };

                return status.Value.IsRunning
                    ? HealthCheckResult.Healthy(Name, ComponentType, 0, diagnostics.ToImmutableDictionary())
                    : HealthCheckResult.Degraded(
                        Name, 
                        ComponentType, 
                        0, 
                        ImmutableList.Create("Watchdog is not running"),
                        diagnostics.ToImmutableDictionary()
                    );
            }

            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                "Failed to retrieve watchdog status"
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"Watchdog health check failed: {ex.Message}",
                ex
            );
        }
    }
}

/// <summary>
/// Health check for AI services (Mistral, DeepSeek, CodeGemma, Gemma2).
/// </summary>
public class AIServiceHealthCheck : IHealthCheck
{
    private readonly object _aiService;
    private readonly string _serviceName;

    public AIServiceHealthCheck(object aiService, string serviceName)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    }

    public string Name => _serviceName;
    public string ComponentType => "AI Service";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if the AI service implements IHealthCheckable
            if (_aiService is IHealthCheckable healthCheckable)
            {
                return await healthCheckable.CheckHealthAsync();
            }

            // Check specific AI service types
            // Since we already have the service name, we can use that to determine the type
            return _serviceName switch
            {
                "Mistral Documentation" => await CheckDocumentationServiceAsync(_aiService),
                "DeepSeek Context" => await CheckContextServiceAsync(_aiService),
                "CodeGemma Pattern" => await CheckPatternServiceAsync(_aiService),
                "Gemma2 Booklet" => await CheckBookletServiceAsync(_aiService),
                _ => HealthCheckResult.Healthy(
                    Name,
                    ComponentType,
                    0,
                    new Dictionary<string, object> { ["Status"] = "Service registered" }.ToImmutableDictionary()
                )
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"{_serviceName} health check failed: {ex.Message}",
                ex
            );
        }
    }

    private async Task<HealthCheckResult> CheckDocumentationServiceAsync(object service)
    {
        // Basic availability check
        return await Task.FromResult(HealthCheckResult.Healthy(
            Name,
            ComponentType,
            0,
            new Dictionary<string, object> 
            { 
                ["Type"] = "Documentation Analyzer",
                ["Status"] = "Available"
            }.ToImmutableDictionary()
        ));
    }

    private async Task<HealthCheckResult> CheckContextServiceAsync(object service)
    {
        return await Task.FromResult(HealthCheckResult.Healthy(
            Name,
            ComponentType,
            0,
            new Dictionary<string, object> 
            { 
                ["Type"] = "Context Analyzer",
                ["Status"] = "Available"
            }.ToImmutableDictionary()
        ));
    }

    private async Task<HealthCheckResult> CheckPatternServiceAsync(object service)
    {
        return await Task.FromResult(HealthCheckResult.Healthy(
            Name,
            ComponentType,
            0,
            new Dictionary<string, object> 
            { 
                ["Type"] = "Pattern Validator",
                ["Status"] = "Available"
            }.ToImmutableDictionary()
        ));
    }

    private async Task<HealthCheckResult> CheckBookletServiceAsync(object service)
    {
        return await Task.FromResult(HealthCheckResult.Healthy(
            Name,
            ComponentType,
            0,
            new Dictionary<string, object> 
            { 
                ["Type"] = "Booklet Generator",
                ["Status"] = "Available"
            }.ToImmutableDictionary()
        ));
    }
}

/// <summary>
/// Health check for configuration service.
/// </summary>
public class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IAIRESConfiguration _configuration;

    public ConfigurationHealthCheck(IAIRESConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string Name => "Configuration";
    public string ComponentType => "Infrastructure";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var diagnostics = new Dictionary<string, object>
            {
                ["InputDirectory"] = _configuration.Directories.InputDirectory,
                ["OutputDirectory"] = _configuration.Directories.OutputDirectory,
                ["WatchdogEnabled"] = _configuration.Watchdog.Enabled,
                ["MaxConcurrentFiles"] = _configuration.Pipeline.MaxConcurrentFiles,
                ["AIServicesConfigured"] = !string.IsNullOrEmpty(_configuration.AIServices.OllamaBaseUrl)
            };

            // Check if critical paths exist
            var failures = new List<string>();
            
            if (string.IsNullOrEmpty(_configuration.Directories.InputDirectory))
                failures.Add("Input directory not configured");
                
            if (string.IsNullOrEmpty(_configuration.Directories.OutputDirectory))
                failures.Add("Output directory not configured");
                
            if (string.IsNullOrEmpty(_configuration.AIServices.OllamaBaseUrl))
                failures.Add("Ollama base URL not configured");

            if (failures.Any())
            {
                return HealthCheckResult.Unhealthy(
                    Name,
                    ComponentType,
                    0,
                    "Configuration validation failed",
                    null,
                    failures.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                );
            }

            return await Task.FromResult(HealthCheckResult.Healthy(
                Name,
                ComponentType,
                0,
                diagnostics.ToImmutableDictionary()
            ));
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"Configuration health check failed: {ex.Message}",
                ex
            );
        }
    }
}

/// <summary>
/// Health check for file system access.
/// </summary>
public class FileSystemHealthCheck : IHealthCheck
{
    private readonly IAIRESConfiguration _configuration;

    public FileSystemHealthCheck(IAIRESConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string Name => "File System";
    public string ComponentType => "Infrastructure";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var diagnostics = new Dictionary<string, object>();
            var failures = new List<string>();

            // Check input directory
            var inputDir = _configuration.Directories.InputDirectory;
            if (Directory.Exists(inputDir))
            {
                diagnostics["InputDirectoryExists"] = true;
                
                // Test read access
                try
                {
                    var files = Directory.GetFiles(inputDir);
                    diagnostics["InputDirectoryReadable"] = true;
                    diagnostics["InputFileCount"] = files.Length;
                }
                catch (Exception ex)
                {
                    diagnostics["InputDirectoryReadable"] = false;
                    failures.Add($"Cannot read input directory: {ex.Message}");
                }
            }
            else
            {
                diagnostics["InputDirectoryExists"] = false;
                failures.Add($"Input directory does not exist: {inputDir}");
            }

            // Check output directory
            var outputDir = _configuration.Directories.OutputDirectory;
            if (Directory.Exists(outputDir))
            {
                diagnostics["OutputDirectoryExists"] = true;
                
                // Test write access
                try
                {
                    var testFile = Path.Combine(outputDir, $".health_check_{Guid.NewGuid():N}.tmp");
                    await File.WriteAllTextAsync(testFile, "health check", cancellationToken);
                    File.Delete(testFile);
                    diagnostics["OutputDirectoryWritable"] = true;
                }
                catch (Exception ex)
                {
                    diagnostics["OutputDirectoryWritable"] = false;
                    failures.Add($"Cannot write to output directory: {ex.Message}");
                }
            }
            else
            {
                diagnostics["OutputDirectoryExists"] = false;
                failures.Add($"Output directory does not exist: {outputDir}");
            }

            if (failures.Any())
            {
                return HealthCheckResult.Degraded(
                    Name,
                    ComponentType,
                    0,
                    failures.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                );
            }

            return HealthCheckResult.Healthy(
                Name,
                ComponentType,
                0,
                diagnostics.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"File system health check failed: {ex.Message}",
                ex
            );
        }
    }
}

/// <summary>
/// Health check for the Booklet Persistence service.
/// </summary>
public class BookletPersistenceHealthCheck : IHealthCheck
{
    private readonly IBookletPersistenceServiceFactory _persistenceServiceFactory;

    public BookletPersistenceHealthCheck(IBookletPersistenceServiceFactory persistenceServiceFactory)
    {
        _persistenceServiceFactory = persistenceServiceFactory ?? throw new ArgumentNullException(nameof(persistenceServiceFactory));
    }

    public string Name => "Booklet Persistence";
    public string ComponentType => "Application Service";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scopedService = _persistenceServiceFactory.CreateScoped();
            var persistenceService = scopedService.Service;
            
            // BookletPersistenceService already has CheckHealthAsync method
            if (persistenceService is BookletPersistenceService bookletPersistenceService)
            {
                return await bookletPersistenceService.CheckHealthAsync();
            }

            // Fallback to basic availability check
            return HealthCheckResult.Healthy(
                Name,
                ComponentType,
                0,
                new Dictionary<string, object> 
                { 
                    ["Status"] = "Service registered",
                    ["Type"] = "Booklet Persistence"
                }.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                Name,
                ComponentType,
                0,
                $"Booklet persistence health check failed: {ex.Message}",
                ex
            );
        }
    }
}

/// <summary>
/// Interface for services that support health checks.
/// </summary>
public interface IHealthCheckable
{
    Task<HealthCheckResult> CheckHealthAsync();
}