// <copyright file="LogEntry.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Represents a captured log entry.
/// </summary>
public class LogEntry
{
    public string Level { get; set; } = "Information";

    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public Exception? Exception { get; set; }

    public string? MemberName { get; set; }

    public string? FilePath { get; set; }

    public int LineNumber { get; set; }

    // Performance tracking
    public string? OperationName { get; set; }

    public long? ElapsedMilliseconds { get; set; }

    public Dictionary<string, object>? AdditionalData { get; set; }

    // Metrics
    public string? MetricName { get; set; }

    public double? MetricValue { get; set; }

    public string? MetricUnit { get; set; }

    public Dictionary<string, object>? Dimensions { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    // Events
    public string? EventName { get; set; }

    // Scopes
    public string? ScopeName { get; set; }

    // Health and Status
    public string? ComponentName { get; set; }

    public bool? IsHealthy { get; set; }

    public string? StatusName { get; set; }

    public string? StatusValue { get; set; }

    // Correlation
    public string? CorrelationId { get; set; }
}
