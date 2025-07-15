#!/bin/bash
# Fix member ordering in TestHttpMessageHandler.cs

# Create a temp file with the correct structure
cat > /tmp/fix_testhttp.tmp << 'EOF'
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

EOF

# Remove the duplicate public methods from after SendAsync
sed -i '/^    \/\/\/ <summary>$/,/^    public RecordedRequest\? GetLastRequest().*$/d' tests/AIRES.TestInfrastructure/TestHttpMessageHandler.cs

# Insert the public methods before SendAsync
sed -i '/protected override async Task<HttpResponseMessage> SendAsync/i\
    /// <summary>\
    /// Clear all recorded requests.\
    /// </summary>\
    public void ClearRecordedRequests()\
    {\
        this.recordedRequests.Clear();\
    }\
\
    /// <summary>\
    /// Verify that a request was made matching the specified criteria.\
    /// </summary>\
    /// <param name="method">The HTTP method to match.</param>\
    /// <param name="urlPattern">The URL pattern to match.</param>\
    /// <param name="expectedCount">Optional expected count of matching requests.</param>\
    /// <returns>True if verification passes, false otherwise.</returns>\
    public bool VerifyRequest(HttpMethod method, string urlPattern, int? expectedCount = null)\
    {\
        var matchingRequests = this.recordedRequests.Where(r =>\
            r.Method == method &&\
            (r.Uri.ToString().Contains(urlPattern) ||\
             System.Text.RegularExpressions.Regex.IsMatch(r.Uri.ToString(), urlPattern.Replace("*", ".*")))).ToList();\
\
        if (expectedCount.HasValue)\
        {\
            return matchingRequests.Count == expectedCount.Value;\
        }\
\
        return matchingRequests.Count > 0;\
    }\
\
    /// <summary>\
    /// Get the number of requests made.\
    /// </summary>\
    /// <returns>The total count of recorded requests.</returns>\
    public int GetRequestCount() => this.recordedRequests.Count;\
\
    /// <summary>\
    /// Get the last request made.\
    /// </summary>\
    /// <returns>The last recorded request or null if none.</returns>\
    public RecordedRequest? GetLastRequest() => this.recordedRequests.LastOrDefault();\
' tests/AIRES.TestInfrastructure/TestHttpMessageHandler.cs

echo "Member ordering fixed"