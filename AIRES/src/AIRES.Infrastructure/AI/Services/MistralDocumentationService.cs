using System.Net.Http;
using System.Threading.Tasks;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Clients;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// Mistral AI implementation for fetching and analyzing Microsoft documentation.
/// </summary>
public class MistralDocumentationService : AIRESServiceBase, IErrorDocumentationAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private readonly HttpClient _httpClient;
    private const string MODEL_NAME = "mistral:7b-instruct-q4_K_M";

    public MistralDocumentationService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient,
        HttpClient httpClient)
        : base(logger, nameof(MistralDocumentationService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ErrorDocumentationFinding> AnalyzeAsync(CompilerError error, string relevantCodeSnippet)
    {
        LogMethodEntry();
        
        try
        {
            // First, fetch Microsoft documentation
            var msDocsUrl = $"https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/{error.Code.ToLower()}";
            var msDocsContent = await FetchMicrosoftDocsAsync(msDocsUrl).ConfigureAwait(false);

            // Build prompt for Mistral
            var prompt = BuildPrompt(error, msDocsContent, relevantCodeSnippet);

            // Call Mistral via Ollama
            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are a C# compiler error documentation expert. Analyze Microsoft documentation and provide clear, structured findings.",
                Temperature = 0.1,
                MaxTokens = 2000,
                TimeoutSeconds = 90 // 7B model typically needs 60-90s
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                LogError($"Failed to generate documentation analysis: {result.ErrorMessage}");
                LogMethodExit();
                return new ErrorDocumentationFinding(
                    "Mistral",
                    $"Documentation Analysis for {error.Code}",
                    $"Failed to analyze documentation: {result.ErrorMessage}",
                    msDocsUrl
                );
            }

            var response = result.Value!.Response;
            
            LogInfo($"Successfully generated documentation analysis for {error.Code}");
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                response,
                msDocsUrl
            );
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error during documentation analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                $"Error during analysis: {ex.Message}",
                ""
            );
        }
    }

    private async Task<string> FetchMicrosoftDocsAsync(string url)
    {
        LogMethodEntry();
        
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AIRES/1.0");
            
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                LogWarning($"Failed to fetch Microsoft docs from {url}: {response.StatusCode}");
                LogMethodExit();
                return "Microsoft documentation not available.";
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            // Simple extraction of main content (in real implementation, use HTML parser)
            var startIndex = content.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
            var endIndex = content.IndexOf("</article>", StringComparison.OrdinalIgnoreCase);
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                content = content.Substring(startIndex, endIndex - startIndex + 10);
                // Strip HTML tags (simplified)
                content = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
                content = System.Net.WebUtility.HtmlDecode(content);
            }
            
            LogMethodExit();
            return content.Length > 5000 ? content.Substring(0, 5000) : content;
        }
        catch (Exception ex)
        {
            LogError($"Error fetching Microsoft docs from {url}", ex);
            LogMethodExit();
            return "Error fetching Microsoft documentation.";
        }
    }

    private string BuildPrompt(CompilerError error, string msDocsContent, string codeSnippet)
    {
        return $@"Analyze the following C# compiler error and provide detailed documentation findings:

Error Code: {error.Code}
Error Message: {error.Message}
Error Location: {error.Location}

Relevant Code Snippet:
```csharp
{codeSnippet}
```

Microsoft Documentation Content:
{msDocsContent}

Please provide:
1. Root Cause Explanation: What exactly causes this error?
2. Common Scenarios: When does this error typically occur?
3. Resolution Steps: Clear, actionable steps to fix the error
4. Best Practices: How to avoid this error in the future
5. Related Errors: Other errors that might be connected

Format your response as a structured technical document.";
    }
}