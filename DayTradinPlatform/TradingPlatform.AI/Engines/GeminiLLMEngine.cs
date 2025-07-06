using TradingPlatform.AI.Core;
using TradingPlatform.AI.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using System.Text.Json;
using System.Text;

namespace TradingPlatform.AI.Engines;

/// <summary>
/// Canonical Google Gemini LLM engine for advanced reasoning and analysis
/// Integrates Gemini Pro/Flash models with standardized interface and performance monitoring
/// ROI: Advanced reasoning capabilities for market analysis, strategy generation, and decision support
/// </summary>
public class GeminiLLMEngine : CanonicalAIServiceBase<GeminiPrompt, GeminiResponse>
{
    private const string MODEL_TYPE = "Gemini";
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiLLMEngine(
        ITradingLogger logger,
        AIModelConfiguration configuration,
        string apiKey) : base(logger, "GeminiLLMEngine", configuration)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradingPlatform.AI/1.0");
    }

    protected override async Task<TradingResult<bool>> ValidateInputAsync(GeminiPrompt input)
    {
        LogMethodEntry();

        try
        {
            if (input == null)
            {
                return TradingResult<bool>.Failure(
                    "NULL_INPUT",
                    "Input prompt cannot be null",
                    "Gemini requires a valid prompt for processing");
            }

            if (string.IsNullOrWhiteSpace(input.Prompt))
            {
                return TradingResult<bool>.Failure(
                    "EMPTY_PROMPT",
                    "Prompt text cannot be empty",
                    "Gemini requires non-empty prompt text for analysis");
            }

            // Check prompt length (Gemini has token limits)
            if (input.Prompt.Length > 1000000) // ~1M characters (~750K tokens)
            {
                return TradingResult<bool>.Failure(
                    "PROMPT_TOO_LONG",
                    "Prompt exceeds maximum length",
                    "Gemini prompt is too long and may exceed token limits");
            }

            // Validate prompt type if specified
            if (!string.IsNullOrEmpty(input.PromptType) && 
                !IsValidPromptType(input.PromptType))
            {
                return TradingResult<bool>.Failure(
                    "INVALID_PROMPT_TYPE",
                    $"Unsupported prompt type: {input.PromptType}",
                    "The specified prompt type is not supported by this Gemini engine");
            }

            await Task.CompletedTask; // Maintain async signature

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to validate Gemini input", ex);
            return TradingResult<bool>.Failure(
                "INPUT_VALIDATION_EXCEPTION",
                ex.Message,
                "An error occurred while validating the Gemini prompt");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<AIModelMetadata>> SelectOptimalModelAsync(
        GeminiPrompt input, string? modelName)
    {
        LogMethodEntry();

        try
        {
            // Select optimal Gemini model based on prompt complexity and requirements
            var selectedModel = modelName ?? SelectOptimalGeminiModel(input);

            var availableModel = _configuration.AvailableModels
                .FirstOrDefault(m => m.Type == MODEL_TYPE && m.Name == selectedModel);

            if (availableModel == null)
            {
                // Create default Gemini model configuration
                availableModel = CreateDefaultGeminiModel(selectedModel);
            }

            var metadata = new AIModelMetadata
            {
                ModelName = availableModel.Name,
                ModelType = MODEL_TYPE,
                Version = availableModel.Version,
                LoadedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                IsGpuAccelerated = true, // Gemini runs on Google's TPUs
                CanUnload = false, // API-based, no local loading
                Capabilities = availableModel.Capabilities,
                Metadata = availableModel.Parameters
            };

            LogInfo($"Selected Gemini model: {metadata.ModelName} for prompt type: {input.PromptType ?? "general"}");

            return TradingResult<AIModelMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            LogError("Failed to select optimal Gemini model", ex);
            return TradingResult<AIModelMetadata>.Failure(
                "MODEL_SELECTION_FAILED",
                ex.Message,
                "Unable to select appropriate Gemini model configuration");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> EnsureModelLoadedAsync(AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            // For API-based models like Gemini, we just validate API connectivity
            var testResult = await TestGeminiConnectivity(model.ModelName);
            
            if (!testResult.Success)
            {
                return TradingResult<bool>.Failure(
                    "GEMINI_CONNECTIVITY_FAILED",
                    testResult.ErrorMessage ?? "Failed to connect to Gemini API",
                    "Unable to establish connection with Google Gemini API");
            }

            // Mark as "loaded" in our tracking
            lock (_modelLock)
            {
                _loadedModels[model.ModelName] = model;
            }

            LogInfo($"Gemini model {model.ModelName} connectivity verified");

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Failed to ensure Gemini model {model.ModelName} is available", ex);
            return TradingResult<bool>.Failure(
                "MODEL_LOAD_EXCEPTION",
                ex.Message,
                "An error occurred while verifying Gemini model availability");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<GeminiResponse>> PerformInferenceAsync(
        GeminiPrompt input, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            var response = await CallGeminiAPI(input, model);
            
            if (!response.Success || response.Data == null)
            {
                return TradingResult<GeminiResponse>.Failure(
                    "GEMINI_API_CALL_FAILED",
                    response.ErrorMessage ?? "Gemini API call failed",
                    "Failed to get response from Google Gemini API");
            }

            LogInfo($"Gemini inference completed: Model={model.ModelName}, " +
                   $"Input tokens≈{EstimateTokenCount(input.Prompt)}, " +
                   $"Output tokens≈{EstimateTokenCount(response.Data.GeneratedText)}, " +
                   $"Confidence={response.Data.Confidence:P2}");

            return TradingResult<GeminiResponse>.Success(response.Data);
        }
        catch (Exception ex)
        {
            LogError($"Gemini inference failed for model: {model.ModelName}", ex);
            return TradingResult<GeminiResponse>.Failure(
                "GEMINI_INFERENCE_EXCEPTION",
                ex.Message,
                "An error occurred during Gemini API inference");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<GeminiResponse>> PostProcessOutputAsync(
        GeminiResponse rawOutput, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            // Apply post-processing based on prompt type
            var processedResponse = await ApplyPromptTypeSpecificProcessing(rawOutput, model);

            // Validate response quality
            var qualityResult = ValidateResponseQuality(processedResponse);
            if (!qualityResult.Success)
            {
                LogWarning($"Gemini response quality validation failed: {qualityResult.ErrorMessage}");
                processedResponse.Confidence *= 0.8m; // Reduce confidence for quality issues
            }

            // Extract structured data if applicable
            processedResponse = await ExtractStructuredData(processedResponse);

            // Add performance metadata
            processedResponse.Metadata["post_processed_at"] = DateTime.UtcNow;
            processedResponse.Metadata["processing_version"] = "1.0";

            return TradingResult<GeminiResponse>.Success(processedResponse);
        }
        catch (Exception ex)
        {
            LogError("Gemini post-processing failed", ex);
            return TradingResult<GeminiResponse>.Failure(
                "POST_PROCESSING_FAILED",
                ex.Message,
                "Failed to post-process Gemini response");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override decimal GetOutputConfidence(GeminiResponse output)
    {
        return output?.Confidence ?? 0m;
    }

    // Gemini-specific implementation methods
    private string SelectOptimalGeminiModel(GeminiPrompt input)
    {
        return input.PromptType?.ToLower() switch
        {
            "market_analysis" => "gemini-1.5-pro", // Best for complex analysis
            "strategy_generation" => "gemini-1.5-pro", // Best for reasoning
            "risk_assessment" => "gemini-1.5-pro", // Best for detailed analysis
            "quick_summary" => "gemini-1.5-flash", // Fast for simple tasks
            "code_generation" => "gemini-1.5-pro", // Best for code
            "data_extraction" => "gemini-1.5-flash", // Fast for extraction
            _ => input.Prompt.Length > 10000 ? "gemini-1.5-pro" : "gemini-1.5-flash"
        };
    }

    private ModelDefinition CreateDefaultGeminiModel(string modelName)
    {
        var isProModel = modelName.Contains("pro");
        
        return new ModelDefinition
        {
            Name = modelName,
            Type = MODEL_TYPE,
            Version = "1.5",
            IsDefault = modelName == "gemini-1.5-flash",
            Priority = isProModel ? 1 : 2,
            Capabilities = new AIModelCapabilities
            {
                SupportedInputTypes = new() { "GeminiPrompt", "Text" },
                SupportedOutputTypes = new() { "GeminiResponse", "Text" },
                SupportedOperations = new() { 
                    "TextGeneration", "Analysis", "Reasoning", "CodeGeneration", 
                    "DataExtraction", "Summarization", "Translation" 
                },
                MaxBatchSize = 1, // Gemini API typically processes one at a time
                RequiresGpu = false, // API-based
                SupportsStreaming = true,
                MaxInferenceTime = TimeSpan.FromSeconds(isProModel ? 60 : 30),
                MinConfidenceThreshold = 0.7m
            },
            Parameters = new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["top_p"] = 0.9,
                ["top_k"] = 40,
                ["max_output_tokens"] = isProModel ? 8192 : 4096,
                ["safety_settings"] = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }
            }
        };
    }

    private async Task<TradingResult<bool>> TestGeminiConnectivity(string modelName)
    {
        LogMethodEntry();

        try
        {
            var testPrompt = new GeminiPrompt
            {
                Prompt = "Hello, please respond with 'OK' to confirm connectivity.",
                PromptType = "connectivity_test",
                MaxTokens = 10,
                Temperature = 0.1m
            };

            var testModel = new AIModelMetadata
            {
                ModelName = modelName,
                ModelType = MODEL_TYPE
            };

            var response = await CallGeminiAPI(testPrompt, testModel);
            
            if (response.Success && response.Data?.GeneratedText.ToUpper().Contains("OK") == true)
            {
                LogInfo($"Gemini connectivity test successful for model: {modelName}");
                return TradingResult<bool>.Success(true);
            }

            return TradingResult<bool>.Failure(
                "CONNECTIVITY_TEST_FAILED",
                "Gemini connectivity test did not return expected response",
                "Unable to verify Gemini API connectivity");
        }
        catch (Exception ex)
        {
            LogError($"Gemini connectivity test failed for model: {modelName}", ex);
            return TradingResult<bool>.Failure(
                "CONNECTIVITY_TEST_EXCEPTION",
                ex.Message,
                "Error occurred during Gemini connectivity test");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<TradingResult<GeminiResponse>> CallGeminiAPI(GeminiPrompt input, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            var requestUrl = $"{_baseUrl}/{model.ModelName}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = input.Prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = input.Temperature ?? 0.7m,
                    topP = input.TopP ?? 0.9m,
                    topK = input.TopK ?? 40,
                    maxOutputTokens = input.MaxTokens ?? 4096,
                    stopSequences = input.StopSequences ?? Array.Empty<string>()
                },
                safetySettings = model.Metadata.GetValueOrDefault("safety_settings")
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var startTime = DateTime.UtcNow;
            var httpResponse = await _httpClient.PostAsync(requestUrl, httpContent);
            var latency = DateTime.UtcNow - startTime;

            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                LogError($"Gemini API error: {httpResponse.StatusCode} - {responseContent}");
                return TradingResult<GeminiResponse>.Failure(
                    "GEMINI_API_ERROR",
                    $"API returned {httpResponse.StatusCode}: {responseContent}",
                    "Google Gemini API returned an error response");
            }

            var apiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            if (apiResponse?.Candidates?.Any() != true)
            {
                return TradingResult<GeminiResponse>.Failure(
                    "NO_CANDIDATES_RETURNED",
                    "Gemini API returned no candidates",
                    "Gemini API did not generate any response candidates");
            }

            var candidate = apiResponse.Candidates.First();
            var generatedText = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? "";

            var response = new GeminiResponse
            {
                GeneratedText = generatedText,
                ModelName = model.ModelName,
                ModelType = MODEL_TYPE,
                GenerationTime = DateTime.UtcNow,
                Confidence = CalculateConfidence(candidate, input),
                TokensUsed = EstimateTokenCount(input.Prompt) + EstimateTokenCount(generatedText),
                InferenceLatency = latency,
                PromptType = input.PromptType ?? "general",
                FinishReason = candidate.FinishReason ?? "completed",
                SafetyRatings = candidate.SafetyRatings?.ToDictionary(
                    sr => sr.Category ?? "unknown", 
                    sr => sr.Probability ?? "unknown") ?? new Dictionary<string, string>(),
                Metadata = new Dictionary<string, object>
                {
                    ["api_call_time"] = startTime,
                    ["response_time"] = DateTime.UtcNow,
                    ["http_status"] = httpResponse.StatusCode,
                    ["model_version"] = model.Version
                }
            };

            return TradingResult<GeminiResponse>.Success(response);
        }
        catch (Exception ex)
        {
            LogError("Failed to call Gemini API", ex);
            return TradingResult<GeminiResponse>.Failure(
                "GEMINI_API_CALL_EXCEPTION",
                ex.Message,
                "An error occurred while calling the Gemini API");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<GeminiResponse> ApplyPromptTypeSpecificProcessing(
        GeminiResponse response, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            switch (response.PromptType?.ToLower())
            {
                case "market_analysis":
                    response = await ProcessMarketAnalysisResponse(response);
                    break;
                case "strategy_generation":
                    response = await ProcessStrategyGenerationResponse(response);
                    break;
                case "risk_assessment":
                    response = await ProcessRiskAssessmentResponse(response);
                    break;
                case "data_extraction":
                    response = await ProcessDataExtractionResponse(response);
                    break;
                default:
                    // No special processing for general prompts
                    break;
            }

            return response;
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply prompt-specific processing for type: {response.PromptType}", ex);
            return response; // Return original response if processing fails
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<GeminiResponse> ProcessMarketAnalysisResponse(GeminiResponse response)
    {
        // Extract market sentiment, key insights, recommendations
        response.StructuredData["analysis_type"] = "market_analysis";
        response.StructuredData["sentiment"] = ExtractSentiment(response.GeneratedText);
        response.StructuredData["key_insights"] = ExtractKeyInsights(response.GeneratedText);
        response.StructuredData["recommendations"] = ExtractRecommendations(response.GeneratedText);
        
        await Task.CompletedTask;
        return response;
    }

    private async Task<GeminiResponse> ProcessStrategyGenerationResponse(GeminiResponse response)
    {
        // Extract strategy components, entry/exit criteria, risk parameters
        response.StructuredData["response_type"] = "strategy_generation";
        response.StructuredData["strategy_components"] = ExtractStrategyComponents(response.GeneratedText);
        response.StructuredData["entry_criteria"] = ExtractEntryCriteria(response.GeneratedText);
        response.StructuredData["exit_criteria"] = ExtractExitCriteria(response.GeneratedText);
        response.StructuredData["risk_parameters"] = ExtractRiskParameters(response.GeneratedText);
        
        await Task.CompletedTask;
        return response;
    }

    private async Task<GeminiResponse> ProcessRiskAssessmentResponse(GeminiResponse response)
    {
        // Extract risk levels, mitigation strategies, probability assessments
        response.StructuredData["assessment_type"] = "risk_assessment";
        response.StructuredData["risk_level"] = ExtractRiskLevel(response.GeneratedText);
        response.StructuredData["risk_factors"] = ExtractRiskFactors(response.GeneratedText);
        response.StructuredData["mitigation_strategies"] = ExtractMitigationStrategies(response.GeneratedText);
        
        await Task.CompletedTask;
        return response;
    }

    private async Task<GeminiResponse> ProcessDataExtractionResponse(GeminiResponse response)
    {
        // Extract structured data, numbers, dates, entities
        response.StructuredData["extraction_type"] = "data_extraction";
        response.StructuredData["extracted_numbers"] = ExtractNumbers(response.GeneratedText);
        response.StructuredData["extracted_dates"] = ExtractDates(response.GeneratedText);
        response.StructuredData["extracted_entities"] = ExtractEntities(response.GeneratedText);
        
        await Task.CompletedTask;
        return response;
    }

    private async Task<GeminiResponse> ExtractStructuredData(GeminiResponse response)
    {
        // Look for JSON, tables, lists in the response
        if (response.GeneratedText.Contains("{") && response.GeneratedText.Contains("}"))
        {
            try
            {
                var jsonStart = response.GeneratedText.IndexOf('{');
                var jsonEnd = response.GeneratedText.LastIndexOf('}') + 1;
                var jsonText = response.GeneratedText.Substring(jsonStart, jsonEnd - jsonStart);
                
                var parsedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);
                if (parsedJson != null)
                {
                    response.StructuredData["extracted_json"] = parsedJson;
                }
            }
            catch
            {
                // JSON parsing failed, continue without structured data
            }
        }

        await Task.CompletedTask;
        return response;
    }

    private TradingResult<bool> ValidateResponseQuality(GeminiResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.GeneratedText))
        {
            return TradingResult<bool>.Failure(
                "EMPTY_RESPONSE",
                "Generated text is empty",
                "Gemini returned an empty response");
        }

        if (response.GeneratedText.Length < 10)
        {
            return TradingResult<bool>.Failure(
                "RESPONSE_TOO_SHORT",
                "Generated text is too short",
                "Gemini response is unusually short and may be incomplete");
        }

        if (response.FinishReason == "safety")
        {
            return TradingResult<bool>.Failure(
                "SAFETY_FILTERED",
                "Response was filtered for safety",
                "Gemini filtered the response due to safety considerations");
        }

        return TradingResult<bool>.Success(true);
    }

    private decimal CalculateConfidence(GeminiCandidate candidate, GeminiPrompt input)
    {
        decimal confidence = 0.8m; // Base confidence

        // Adjust based on finish reason
        confidence = candidate.FinishReason switch
        {
            "STOP" => confidence,
            "MAX_TOKENS" => confidence * 0.9m,
            "SAFETY" => confidence * 0.3m,
            "RECITATION" => confidence * 0.6m,
            _ => confidence * 0.7m
        };

        // Adjust based on safety ratings
        if (candidate.SafetyRatings?.Any(sr => sr.Probability == "HIGH") == true)
        {
            confidence *= 0.5m;
        }

        // Adjust based on response length vs prompt complexity
        var responseLength = candidate.Content?.Parts?.FirstOrDefault()?.Text?.Length ?? 0;
        var promptLength = input.Prompt.Length;
        
        if (responseLength < promptLength * 0.1) // Very short response for complex prompt
        {
            confidence *= 0.8m;
        }

        return Math.Max(0.1m, Math.Min(1.0m, confidence));
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return text?.Length / 4 ?? 0;
    }

    private bool IsValidPromptType(string promptType)
    {
        var validTypes = new[]
        {
            "market_analysis", "strategy_generation", "risk_assessment", 
            "data_extraction", "quick_summary", "code_generation",
            "translation", "summarization", "general"
        };
        
        return validTypes.Contains(promptType.ToLower());
    }

    // Extraction helper methods (simplified implementations)
    private string ExtractSentiment(string text)
    {
        if (text.ToLower().Contains("bullish") || text.ToLower().Contains("positive"))
            return "bullish";
        if (text.ToLower().Contains("bearish") || text.ToLower().Contains("negative"))
            return "bearish";
        return "neutral";
    }

    private List<string> ExtractKeyInsights(string text)
    {
        // Simple keyword-based extraction (in production, would use more sophisticated NLP)
        var insights = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.ToLower().Contains("insight") || line.ToLower().Contains("key") || 
                line.ToLower().Contains("important") || line.StartsWith("•") || line.StartsWith("-"))
            {
                insights.Add(line.Trim());
            }
        }
        
        return insights;
    }

    private List<string> ExtractRecommendations(string text)
    {
        var recommendations = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.ToLower().Contains("recommend") || line.ToLower().Contains("suggest") || 
                line.ToLower().Contains("should") || line.ToLower().Contains("consider"))
            {
                recommendations.Add(line.Trim());
            }
        }
        
        return recommendations;
    }

    private List<string> ExtractStrategyComponents(string text) => ExtractBulletPoints(text);
    private List<string> ExtractEntryCriteria(string text) => ExtractCriteria(text, "entry");
    private List<string> ExtractExitCriteria(string text) => ExtractCriteria(text, "exit");
    private List<string> ExtractRiskParameters(string text) => ExtractCriteria(text, "risk");
    
    private List<string> ExtractBulletPoints(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*"))
            .Select(line => line.Trim())
            .ToList();
    }

    private List<string> ExtractCriteria(string text, string criteriaType)
    {
        var criteria = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        bool inRelevantSection = false;
        foreach (var line in lines)
        {
            if (line.ToLower().Contains(criteriaType))
            {
                inRelevantSection = true;
                continue;
            }
            
            if (inRelevantSection && (line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*")))
            {
                criteria.Add(line.Trim());
            }
            else if (inRelevantSection && line.Trim() == "")
            {
                break; // End of section
            }
        }
        
        return criteria;
    }

    private string ExtractRiskLevel(string text)
    {
        var lowerText = text.ToLower();
        if (lowerText.Contains("high risk") || lowerText.Contains("very risky"))
            return "HIGH";
        if (lowerText.Contains("medium risk") || lowerText.Contains("moderate"))
            return "MEDIUM";
        if (lowerText.Contains("low risk") || lowerText.Contains("conservative"))
            return "LOW";
        return "UNKNOWN";
    }

    private List<string> ExtractRiskFactors(string text) => ExtractCriteria(text, "risk");
    private List<string> ExtractMitigationStrategies(string text) => ExtractCriteria(text, "mitigation");
    
    private List<decimal> ExtractNumbers(string text)
    {
        var numbers = new List<decimal>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var cleanWord = word.Trim('$', '%', ',', '.', '!', '?', '(', ')');
            if (decimal.TryParse(cleanWord, out var number))
            {
                numbers.Add(number);
            }
        }
        
        return numbers;
    }

    private List<DateTime> ExtractDates(string text)
    {
        var dates = new List<DateTime>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            if (DateTime.TryParse(word, out var date))
            {
                dates.Add(date);
            }
        }
        
        return dates;
    }

    private List<string> ExtractEntities(string text)
    {
        // Simple entity extraction (in production, would use NER models)
        var entities = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            // Look for capitalized words (potential entities)
            if (word.Length > 2 && char.IsUpper(word[0]) && word.All(c => char.IsLetter(c) || c == '.'))
            {
                entities.Add(word);
            }
        }
        
        return entities.Distinct().ToList();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
        
        base.Dispose(disposing);
    }
}

// Gemini-specific model classes
public class GeminiPrompt
{
    public string Prompt { get; set; } = string.Empty;
    public string? PromptType { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? TopP { get; set; }
    public int? TopK { get; set; }
    public int? MaxTokens { get; set; }
    public string[]? StopSequences { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class GeminiResponse
{
    public string GeneratedText { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public DateTime GenerationTime { get; set; }
    public decimal Confidence { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan InferenceLatency { get; set; }
    public string PromptType { get; set; } = string.Empty;
    public string FinishReason { get; set; } = string.Empty;
    public Dictionary<string, string> SafetyRatings { get; set; } = new();
    public Dictionary<string, object> StructuredData { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// Gemini API response models
public class GeminiApiResponse
{
    public GeminiCandidate[]? Candidates { get; set; }
    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

public class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
    public int Index { get; set; }
    public GeminiSafetyRating[]? SafetyRatings { get; set; }
}

public class GeminiContent
{
    public GeminiPart[]? Parts { get; set; }
    public string? Role { get; set; }
}

public class GeminiPart
{
    public string? Text { get; set; }
}

public class GeminiSafetyRating
{
    public string? Category { get; set; }
    public string? Probability { get; set; }
}

public class GeminiPromptFeedback
{
    public GeminiSafetyRating[]? SafetyRatings { get; set; }
}