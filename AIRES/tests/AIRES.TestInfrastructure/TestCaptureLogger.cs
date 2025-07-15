// <copyright file="TestCaptureLogger.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using AIRES.Foundation.Logging;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Test implementation of IAIRESLogger that captures all log entries for verification.
/// This is a REAL implementation (not a mock) designed for testing scenarios.
/// </summary>
public class TestCaptureLogger : IAIRESLogger
{
    private readonly ConcurrentBag<LogEntry> logEntries = new();
    private readonly Stack<IDisposable> scopes = new();
    private string? correlationId;

    /// <summary>
    /// Gets all captured log entries.
    /// </summary>
    public IReadOnlyList<LogEntry> LogEntries => this.logEntries.ToList();

    /// <summary>
    /// Gets all log messages.
    /// </summary>
    public IReadOnlyList<string> LogMessages => this.logEntries.Select(e => e.Message).ToList();

    public void LogMethodEntry(string? memberName = null, string? filePath = null, int lineNumber = 0)
    {
        var entry = new LogEntry
        {
            Level = "Debug",
            Message = $"Entering method: {memberName}",
            MemberName = memberName,
            FilePath = filePath,
            LineNumber = lineNumber,
            Timestamp = DateTime.UtcNow,
        };
        this.logEntries.Add(entry);
    }

    public void LogMethodExit(string? memberName = null, string? filePath = null, int lineNumber = 0)
    {
        var entry = new LogEntry
        {
            Level = "Debug",
            Message = $"Exiting method: {memberName}",
            MemberName = memberName,
            FilePath = filePath,
            LineNumber = lineNumber,
            Timestamp = DateTime.UtcNow,
        };
        this.logEntries.Add(entry);
    }

    public void LogDebug(string message, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Debug",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogInfo(string message, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Information",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogWarning(string message, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Warning",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogError(string message, Exception? ex = null, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Error",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Exception = ex,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogCritical(string message, Exception? exception = null, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Critical",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Exception = exception,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogTrace(string message, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Trace",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogFatal(string message, Exception? ex = null, params object[] args)
    {
        this.logEntries.Add(new LogEntry
        {
            Level = "Critical",
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args),
            Exception = ex,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogMetric(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        var message = $"Metric: {metricName} = {value}";
        if (tags != null && tags.Count > 0)
        {
            message += $" | Tags: {string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = "Information",
            Message = message,
            MetricName = metricName,
            MetricValue = value,
            Tags = tags,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogEvent(string eventName, Dictionary<string, object>? properties = null)
    {
        var message = $"Event: {eventName}";
        if (properties != null && properties.Count > 0)
        {
            message += $" | Properties: {string.Join(", ", properties.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = "Information",
            Message = message,
            EventName = eventName,
            AdditionalData = properties,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogDuration(string operationName, TimeSpan duration, Dictionary<string, string>? tags = null)
    {
        var message = $"Duration: {operationName} took {duration.TotalMilliseconds}ms";
        if (tags != null && tags.Count > 0)
        {
            message += $" | Tags: {string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = "Information",
            Message = message,
            OperationName = operationName,
            ElapsedMilliseconds = (long)duration.TotalMilliseconds,
            Tags = tags,
            Timestamp = DateTime.UtcNow,
        });
    }

    public IDisposable BeginScope(string scopeName, Dictionary<string, object>? properties = null)
    {
        var message = $"Begin Scope: {scopeName}";
        if (properties != null && properties.Count > 0)
        {
            message += $" | Properties: {string.Join(", ", properties.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = "Debug",
            Message = message,
            ScopeName = scopeName,
            AdditionalData = properties,
            Timestamp = DateTime.UtcNow,
        });

        var scope = new TestScope(() =>
        {
            this.logEntries.Add(new LogEntry
            {
                Level = "Debug",
                Message = $"End Scope: {scopeName}",
                ScopeName = scopeName,
                Timestamp = DateTime.UtcNow,
            });
        });

        this.scopes.Push(scope);
        return scope;
    }

    public void SetCorrelationId(string correlationId)
    {
        this.correlationId = correlationId;
        this.logEntries.Add(new LogEntry
        {
            Level = "Debug",
            Message = $"Correlation ID set: {correlationId}",
            CorrelationId = this.correlationId,
            Timestamp = DateTime.UtcNow,
        });
    }

    public string GetCorrelationId()
    {
        return this.correlationId ?? Guid.NewGuid().ToString();
    }

    public void LogHealthCheck(string componentName, bool isHealthy, string? details = null)
    {
        var message = $"Health Check: {componentName} is {(isHealthy ? "HEALTHY" : "UNHEALTHY")}";
        if (!string.IsNullOrEmpty(details))
        {
            message += $" | Details: {details}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = isHealthy ? "Information" : "Warning",
            Message = message,
            ComponentName = componentName,
            IsHealthy = isHealthy,
            Timestamp = DateTime.UtcNow,
        });
    }

    public void LogStatus(string statusName, string statusValue, Dictionary<string, object>? additionalInfo = null)
    {
        var message = $"Status: {statusName} = {statusValue}";
        if (additionalInfo != null && additionalInfo.Count > 0)
        {
            message += $" | Info: {string.Join(", ", additionalInfo.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
        }

        this.logEntries.Add(new LogEntry
        {
            Level = "Information",
            Message = message,
            StatusName = statusName,
            StatusValue = statusValue,
            AdditionalData = additionalInfo,
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Clear all captured log entries.
    /// </summary>
    public void Clear()
    {
        this.logEntries.Clear();
    }

    /// <summary>
    /// Check if any log entry contains the specified text.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool ContainsMessage(string text)
    {
        return this.logEntries.Any(e => e.Message.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all log entries of a specific level.
    /// </summary>
    /// <param name="level">The log level to filter by.</param>
    /// <returns>List of matching log entries.</returns>
    public IReadOnlyList<LogEntry> GetEntriesByLevel(string level)
    {
        return this.logEntries.Where(e => e.Level == level).ToList();
    }

    /// <summary>
    /// Get all error entries.
    /// </summary>
    /// <returns>List of error log entries.</returns>
    public IReadOnlyList<LogEntry> GetErrors()
    {
        return this.logEntries.Where(e => e.Level == "Error" || e.Level == "Critical" || e.Level == "Fatal").ToList();
    }
}
