// <copyright file="TestCompositionRoot.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;

using AIRES.Application.Interfaces;
using AIRES.Core.Configuration;
using AIRES.Foundation.Alerting;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI;
using AIRES.Infrastructure.Configuration;

using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Test composition root for configuring dependency injection in tests.
/// This provides REAL implementations (not mocks) configured for testing scenarios.
/// </summary>
public static class TestCompositionRoot
{
    /// <summary>
    /// Configure services for testing with real implementations.
    /// </summary>
    /// <param name="testConfig">Optional test configuration settings.</param>
    /// <param name="additionalConfiguration">Optional additional service configuration.</param>
    /// <returns>Configured service provider for testing.</returns>
    public static IServiceProvider ConfigureTestServices(
        TestConfiguration? testConfig = null,
        Action<IServiceCollection>? additionalConfiguration = null)
    {
        var services = new ServiceCollection();
        testConfig ??= new TestConfiguration();

        // Configure test-specific implementations
        ConfigureLogging(services, testConfig);
        ConfigureAlerting(services, testConfig);
        ConfigurePersistence(services, testConfig);
        ConfigureHttpClients(services, testConfig);
        ConfigureMediator(services, testConfig);
        ConfigureConfiguration(services, testConfig);

        // Allow additional test-specific configuration
        additionalConfiguration?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create a pre-configured service provider for common test scenarios.
    /// </summary>
    /// <returns>A service provider configured with default test settings.</returns>
    public static IServiceProvider CreateDefaultTestServiceProvider()
    {
        var config = new TestConfiguration
        {
            UseConsoleAlerting = true,
            OllamaBaseUrl = "http://test.ollama.localhost:11434",
            ConfigurationValues = new Dictionary<string, string?>
            {
                ["Alerting:Console:Enabled"] = "true",
                ["Alerting:Console:MinimumSeverity"] = "Information",
                ["Processing:MaxFileSizeMB"] = "10",
                ["Processing:AllowedExtensions"] = ".txt,.log,.cs",
                ["Development:ProjectStandards"] = "Use AIRES canonical patterns,All methods must have LogMethodEntry/LogMethodExit"
            },
        };

        // Configure default test HTTP responses
        var httpHandler = new TestHttpMessageHandler()
            .When(HttpMethod.Get, "http://test.ollama.localhost:11434/")
            .RespondWith(HttpStatusCode.OK, "Ollama is running")
            .When(HttpMethod.Get, "http://test.ollama.localhost:11434/api/list")
            .RespondWithJson(HttpStatusCode.OK, new { models = new[] { new { name = "mistral:latest" } } })
            .DefaultResponse(HttpStatusCode.NotFound);

        config.HttpMessageHandler = httpHandler;

        return ConfigureTestServices(config);
    }

    private static void ConfigureLogging(IServiceCollection services, TestConfiguration config)
    {
        // Use TestCaptureLogger for capturing and asserting logs
        services.AddSingleton<IAIRESLogger, TestCaptureLogger>();
    }

    private static void ConfigureAlerting(IServiceCollection services, TestConfiguration config)
    {
        if (config.UseConsoleAlerting)
        {
            services.AddSingleton<IAIRESAlertingService, ConsoleAlertingService>();
        }
        else
        {
            // Use real AIRESAlertingService with in-memory persistence
            services.AddSingleton<IAlertPersistence, InMemoryAlertPersistence>();
            services.AddSingleton<IAlertThrottler, SimpleAlertThrottler>();
            services.AddSingleton<IAlertChannelFactory, AlertChannelFactory>();
            services.AddSingleton<IAIRESAlertingService, AIRESAlertingService>();
        }
    }

    private static void ConfigurePersistence(IServiceCollection services, TestConfiguration config)
    {
        // Use in-memory booklet persistence for tests
        services.AddSingleton<IBookletPersistenceService, InMemoryBookletPersistenceService>();
    }

    private static void ConfigureHttpClients(IServiceCollection services, TestConfiguration config)
    {
        // Configure OllamaHealthCheckClient with test HTTP handler
        services.AddSingleton<OllamaHealthCheckClient>(provider =>
        {
            var logger = provider.GetRequiredService<IAIRESLogger>();
            var httpClient = new HttpClient(config.HttpMessageHandler ?? new TestHttpMessageHandler())
            {
                BaseAddress = new Uri(config.OllamaBaseUrl),
            };

            return new OllamaHealthCheckClient(logger, httpClient, config.OllamaBaseUrl);
        });
    }

    private static void ConfigureMediator(IServiceCollection services, TestConfiguration config)
    {
        // Add MediatR with handlers from specified assemblies
        var assemblies = config.MediatorAssemblies ?? new[]
        {
            Assembly.GetAssembly(typeof(AIRES.Application.Commands.ParseCompilerErrorsCommand))!,
        };

        services.AddMediatR(cfg =>
        {
            foreach (var assembly in assemblies)
            {
                cfg.RegisterServicesFromAssembly(assembly);
            }
        });
    }

    private static void ConfigureConfiguration(IServiceCollection services, TestConfiguration config)
    {
        // Build IConfiguration from test settings
        var configBuilder = new ConfigurationBuilder();

        if (config.ConfigurationValues != null && config.ConfigurationValues.Count > 0)
        {
            configBuilder.AddInMemoryCollection(config.ConfigurationValues);
        }

        var configuration = configBuilder.Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add AIRES configuration
        services.AddSingleton<IAIRESConfiguration>(provider =>
        {
            var logger = provider.GetRequiredService<IAIRESLogger>();
            return new AIRESConfigurationService(logger);
        });
    }

}
