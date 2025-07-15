// <copyright file="ResponseConfiguration.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Configuration for an HTTP response.
/// </summary>
public class ResponseConfiguration
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? Content { get; set; }

    public string ContentType { get; set; } = "text/plain";

    public Dictionary<string, string> Headers { get; set; } = new();

    public bool SimulateTimeout { get; set; }

    public int TimeoutDelay { get; set; } = 5000;

    public Exception? ExceptionToThrow { get; set; }
}
