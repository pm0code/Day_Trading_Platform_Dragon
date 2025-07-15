// <copyright file="RequestMatcher.cs" company="AIRES Team">
// Copyright (c) AIRES Team. All rights reserved.
// </copyright>

using System;
using System.Net.Http;

namespace AIRES.TestInfrastructure;

/// <summary>
/// Represents a request matcher for configuring responses.
/// </summary>
public class RequestMatcher : IEquatable<RequestMatcher>
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public string UrlPattern { get; set; } = string.Empty;

    public bool Equals(RequestMatcher? other)
    {
        if (other == null)
        {
            return false;
        }

        return this.Method == other.Method && this.UrlPattern == other.UrlPattern;
    }

    public override bool Equals(object? obj) => this.Equals(obj as RequestMatcher);

    public override int GetHashCode() => HashCode.Combine(this.Method, this.UrlPattern);
}
