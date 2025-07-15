// <copyright file="TestHttpMessageHandler.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Test implementation of HttpMessageHandler for simulating HTTP responses.
/// This is a REAL implementation (not a mock) designed for testing HTTP clients.
/// </summary>
public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<RequestMatcher, ResponseConfiguration> responses = new();
    private readonly List<RecordedRequest> recordedRequests = new();
    private ResponseConfiguration? defaultResponse;

    /// <summary>
    /// Gets all recorded requests made through this handler.
    /// </summary>
    public IReadOnlyList<RecordedRequest> RecordedRequests => this.recordedRequests.AsReadOnly();

    /// <summary>
    /// Configure a response for requests matching specific criteria.
    /// </summary>
    /// <param name="method">The HTTP method to match.</param>
    /// <param name="urlPattern">The URL pattern to match (supports wildcards).</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler When(HttpMethod method, string urlPattern)
    {
        var matcher = new RequestMatcher { Method = method, UrlPattern = urlPattern };
        this.responses[matcher] = new ResponseConfiguration();
        return this;
    }

    /// <summary>
    /// Configure the response for the most recently added matcher.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="content">Optional response content.</param>
    /// <param name="contentType">Content type of the response (default is application/json).</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler RespondWith(HttpStatusCode statusCode, string? content = null, string contentType = "application/json")
    {
        if (this.responses.Count == 0)
        {
            throw new InvalidOperationException("No request matcher configured. Call When() first.");
        }

        var lastMatcher = this.responses.Keys.Last();
        this.responses[lastMatcher] = new ResponseConfiguration
        {
            StatusCode = statusCode,
            Content = content,
            ContentType = contentType,
            Headers = new Dictionary<string, string>(),
        };

        return this;
    }

    /// <summary>
    /// Configure the response with custom headers.
    /// </summary>
    /// <param name="name">Header name.</param>
    /// <param name="value">Header value.</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler WithHeader(string name, string value)
    {
        if (this.responses.Count == 0)
        {
            throw new InvalidOperationException("No request matcher configured. Call When() first.");
        }

        var lastMatcher = this.responses.Keys.Last();
        this.responses[lastMatcher].Headers[name] = value;
        return this;
    }

    /// <summary>
    /// Configure a response with JSON content.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="jsonObject">The object to serialize as JSON.</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler RespondWithJson(HttpStatusCode statusCode, object jsonObject)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(jsonObject);
        return this.RespondWith(statusCode, json, "application/json");
    }

    /// <summary>
    /// Configure a default response for any unmatched requests.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="content">Optional response content.</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler DefaultResponse(HttpStatusCode statusCode, string? content = null)
    {
        this.defaultResponse = new ResponseConfiguration
        {
            StatusCode = statusCode,
            Content = content,
            ContentType = "text/plain",
            Headers = new Dictionary<string, string>(),
        };
        return this;
    }

    /// <summary>
    /// Configure the handler to simulate a timeout.
    /// </summary>
    /// <param name="delayMilliseconds">The delay in milliseconds before timeout.</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler SimulateTimeout(int delayMilliseconds = 5000)
    {
        if (this.responses.Count == 0)
        {
            throw new InvalidOperationException("No request matcher configured. Call When() first.");
        }

        var lastMatcher = this.responses.Keys.Last();
        this.responses[lastMatcher].SimulateTimeout = true;
        this.responses[lastMatcher].TimeoutDelay = delayMilliseconds;
        return this;
    }

    /// <summary>
    /// Configure the handler to throw an exception.
    /// </summary>
    /// <param name="exception">The exception to throw when request is made.</param>
    /// <returns>This instance for fluent configuration.</returns>
    public TestHttpMessageHandler ThrowException(Exception exception)
    {
        if (this.responses.Count == 0)
        {
            throw new InvalidOperationException("No request matcher configured. Call When() first.");
        }

        var lastMatcher = this.responses.Keys.Last();
        this.responses[lastMatcher].ExceptionToThrow = exception;
        return this;
    }

    /// <summary>
    /// Clear all recorded requests.
    /// </summary>
    public void ClearRecordedRequests()
    {
        this.recordedRequests.Clear();
    }

    /// <summary>
    /// Verify that a request was made matching the specified criteria.
    /// </summary>
    /// <param name="method">The HTTP method to match.</param>
    /// <param name="urlPattern">The URL pattern to match.</param>
    /// <param name="expectedCount">Optional expected count of matching requests.</param>
    /// <returns>True if verification passes, false otherwise.</returns>
    public bool VerifyRequest(HttpMethod method, string urlPattern, int? expectedCount = null)
    {
        var matchingRequests = this.recordedRequests.Where(r =>
            r.Method == method &&
            (r.Uri.ToString().Contains(urlPattern) ||
             System.Text.RegularExpressions.Regex.IsMatch(r.Uri.ToString(), urlPattern.Replace("*", ".*")))).ToList();

        if (expectedCount.HasValue)
        {
            return matchingRequests.Count == expectedCount.Value;
        }

        return matchingRequests.Count > 0;
    }

    /// <summary>
    /// Get the number of requests made.
    /// </summary>
    /// <returns>The total count of recorded requests.</returns>
    public int GetRequestCount() => this.recordedRequests.Count;

    /// <summary>
    /// Get the last request made.
    /// </summary>
    /// <returns>The last recorded request or null if none.</returns>
    public RecordedRequest? GetLastRequest() => this.recordedRequests.LastOrDefault();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Record the request
        var recordedRequest = new RecordedRequest
        {
            Method = request.Method,
            Uri = request.RequestUri!,
            Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            Content = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null,
            Timestamp = DateTime.UtcNow,
        };
        this.recordedRequests.Add(recordedRequest);

        // Find matching response configuration
        var matchingConfig = this.FindMatchingResponse(request);

        if (matchingConfig == null)
        {
            matchingConfig = this.defaultResponse ?? new ResponseConfiguration
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = "No matching response configured",
                ContentType = "text/plain",
                Headers = new Dictionary<string, string>(),
            };
        }

        // Simulate timeout if configured
        if (matchingConfig.SimulateTimeout)
        {
            await Task.Delay(matchingConfig.TimeoutDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Throw exception if configured
        if (matchingConfig.ExceptionToThrow != null)
        {
            throw matchingConfig.ExceptionToThrow;
        }

        // Create response
        var response = new HttpResponseMessage(matchingConfig.StatusCode);

        if (!string.IsNullOrEmpty(matchingConfig.Content))
        {
            response.Content = new StringContent(matchingConfig.Content, Encoding.UTF8, matchingConfig.ContentType);
        }

        foreach (var header in matchingConfig.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }

    private ResponseConfiguration? FindMatchingResponse(HttpRequestMessage request)
    {
        foreach (var kvp in this.responses)
        {
            var matcher = kvp.Key;
            var config = kvp.Value;

            if (matcher.Method != request.Method)
            {
                continue;
            }

            if (request.RequestUri == null)
            {
                continue;
            }

            var uri = request.RequestUri.ToString();
            if (matcher.UrlPattern.Contains('*'))
            {
                var pattern = matcher.UrlPattern.Replace("*", ".*");
                if (System.Text.RegularExpressions.Regex.IsMatch(uri, pattern))
                {
                    return config;
                }
            }
            else if (uri.StartsWith(matcher.UrlPattern, StringComparison.OrdinalIgnoreCase))
            {
                return config;
            }
        }

        return null;
    }

}
