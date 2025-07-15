// <copyright file="RecordedRequest.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Represents a recorded HTTP request.
/// </summary>
public class RecordedRequest
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public Uri Uri { get; set; } = new Uri("http://localhost");

    public Dictionary<string, string> Headers { get; set; } = new();

    public string? Content { get; set; }

    public DateTime Timestamp { get; set; }
}
