#!/bin/bash
# Add missing documentation to TestInfrastructure methods

echo "=== Adding missing documentation ==="

# Fix TestHttpMessageHandler.When method
sed -i '/\/\/\/ <summary>/,/\/\/\/ <\/summary>/{
    /Configure a response for requests matching specific criteria/a\
    /// <param name="method">The HTTP method to match.</param>\
    /// <param name="urlPattern">The URL pattern to match (supports wildcards).</param>\
    /// <returns>This instance for fluent configuration.</returns>
}' tests/AIRES.TestInfrastructure/TestHttpMessageHandler.cs

# Fix TestHttpMessageHandler.RespondWith method
sed -i '/Configure the response for the most recently added matcher/{
    a\
    /// </summary>\
    /// <param name="statusCode">The HTTP status code to return.</param>\
    /// <param name="content">Optional response content.</param>\
    /// <param name="contentType">Content type of the response.</param>\
    /// <returns>This instance for fluent configuration.</returns>\
    /// <summary>
}' tests/AIRES.TestInfrastructure/TestHttpMessageHandler.cs

# Fix TestHttpMessageHandler.WithHeader method
sed -i '/Configure the response with custom headers/{
    a\
    /// </summary>\
    /// <param name="name">Header name.</param>\
    /// <param name="value">Header value.</param>\
    /// <returns>This instance for fluent configuration.</returns>\
    /// <summary>
}' tests/AIRES.TestInfrastructure/TestHttpMessageHandler.cs

echo "=== Documentation added ==="
echo "Run 'dotnet build' to verify remaining issues"