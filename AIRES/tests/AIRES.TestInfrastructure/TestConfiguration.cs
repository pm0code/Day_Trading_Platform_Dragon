// <copyright file="TestConfiguration.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace AIRES.TestInfrastructure;
/// <summary>
/// Configuration options for test services.
/// </summary>
public class TestConfiguration
{
    /// <summary>
    /// Gets or sets the base URL for Ollama service.
    /// </summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets a value indicating whether to use console alerting instead of full alerting service.
    /// </summary>
    public bool UseConsoleAlerting { get; set; }

    /// <summary>
    /// Gets or sets the custom HTTP message handler for testing HTTP clients.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>
    /// Gets or sets the assemblies to scan for MediatR handlers.
    /// </summary>
    public Assembly[]? MediatorAssemblies { get; set; }

    /// <summary>
    /// Gets or sets the configuration values to use for IConfiguration.
    /// </summary>
    public Dictionary<string, string?>? ConfigurationValues { get; set; }

    /// <summary>
    /// Gets or sets the additional services to register.
    /// </summary>
    public Action<IServiceCollection>? AdditionalServices { get; set; }
}
