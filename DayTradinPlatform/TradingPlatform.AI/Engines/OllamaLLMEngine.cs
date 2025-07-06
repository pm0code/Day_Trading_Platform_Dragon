using TradingPlatform.AI.Core;
using TradingPlatform.AI.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using System.Text.Json;
using System.Text;

namespace TradingPlatform.AI.Engines;

/// <summary>
/// Canonical Ollama LLM engine for local open-source model inference
/// Supports Llama, Mistral, Mixtral, Phi, Neural-Chat, and other models for financial analysis
/// ROI: Zero API costs, complete data privacy, customizable models, low-latency local inference
/// Features: Streaming responses, model management, quantization support, custom financial fine-tuning
/// </summary>
public class OllamaLLMEngine : CanonicalAIServiceBase<OllamaPrompt, OllamaResponse>
{
    private const string MODEL_TYPE = "Ollama";
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Dictionary<string, OllamaModelInfo> _availableModels = new();

    public OllamaLLMEngine(
        ITradingLogger logger,
        AIModelConfiguration configuration,
        string? baseUrl = null) : base(logger, "OllamaLLMEngine", configuration)
    {
        _baseUrl = baseUrl ?? "http://localhost:11434"; // Default Ollama API endpoint
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradingPlatform.AI/1.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for local model loading
    }

    protected override async Task<TradingResult<bool>> ValidateInputAsync(OllamaPrompt input)
    {
        LogMethodEntry();

        try
        {
            if (input == null)
            {
                return TradingResult<bool>.Failure(
                    "NULL_INPUT",
                    "Input prompt cannot be null",
                    "Ollama requires a valid prompt for local LLM processing");
            }

            if (string.IsNullOrWhiteSpace(input.Prompt))
            {
                return TradingResult<bool>.Failure(
                    "EMPTY_PROMPT",
                    "Prompt text cannot be empty",
                    "Ollama requires non-empty prompt text for analysis");
            }

            // Validate model name
            if (string.IsNullOrWhiteSpace(input.Model))
            {
                LogWarning("No model specified, will use default llama2");
                input.Model = "llama2";
            }

            // Check prompt length (model-specific limits)
            var maxTokens = GetModelMaxTokens(input.Model);
            if (input.Prompt.Length > maxTokens * 4) // Rough estimate: 4 chars per token
            {
                return TradingResult<bool>.Failure(
                    "PROMPT_TOO_LONG",
                    $"Prompt exceeds maximum length for model {input.Model}",
                    $"Ollama prompt is too long for the selected model's context window");
            }

            // Validate prompt type if specified
            if (!string.IsNullOrEmpty(input.PromptType) && 
                !IsValidPromptType(input.PromptType))
            {
                return TradingResult<bool>.Failure(
                    "INVALID_PROMPT_TYPE",
                    $"Unsupported prompt type: {input.PromptType}",
                    "The specified prompt type is not supported by this Ollama engine");
            }

            // Validate streaming configuration
            if (input.Stream && input.StreamCallback == null)
            {
                LogWarning("Streaming enabled but no callback provided, disabling streaming");
                input.Stream = false;
            }

            // Validate quantization if specified
            if (!string.IsNullOrEmpty(input.Quantization))
            {
                var validQuantizations = new[] { "q4_0", "q4_1", "q5_0", "q5_1", "q8_0", "f16", "f32" };
                if (!validQuantizations.Contains(input.Quantization.ToLower()))
                {
                    LogWarning($"Invalid quantization '{input.Quantization}', using model default");
                    input.Quantization = null;
                }
            }

            await Task.CompletedTask; // Maintain async signature

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to validate Ollama input", ex);
            return TradingResult<bool>.Failure(
                "INPUT_VALIDATION_EXCEPTION",
                ex.Message,
                "An error occurred while validating the Ollama prompt");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<AIModelMetadata>> SelectOptimalModelAsync(
        OllamaPrompt input, string? modelName)
    {
        LogMethodEntry();

        try
        {
            // Check available Ollama models
            var availableModels = await GetAvailableOllamaModels();
            if (!availableModels.Success || availableModels.Data == null)
            {
                return TradingResult<AIModelMetadata>.Failure(
                    "OLLAMA_UNAVAILABLE",
                    "Failed to connect to Ollama service",
                    "Ensure Ollama is running locally on the configured endpoint");
            }

            // Select optimal model based on task and available models
            var selectedModel = modelName ?? input.Model ?? SelectOptimalOllamaModel(input, availableModels.Data);

            // Check if model is available locally
            var modelInfo = availableModels.Data.FirstOrDefault(m => 
                m.Name.Equals(selectedModel, StringComparison.OrdinalIgnoreCase));

            if (modelInfo == null)
            {
                // Model not found, attempt to pull it
                LogInfo($"Model {selectedModel} not found locally, attempting to pull...");
                var pullResult = await PullOllamaModel(selectedModel);
                if (!pullResult.Success)
                {
                    return TradingResult<AIModelMetadata>.Failure(
                        "MODEL_PULL_FAILED",
                        $"Failed to pull model {selectedModel}",
                        "Unable to download the requested Ollama model");
                }
                modelInfo = pullResult.Data;
            }

            var metadata = new AIModelMetadata
            {
                ModelName = modelInfo!.Name,
                ModelType = MODEL_TYPE,
                Version = modelInfo.Digest ?? "latest",
                LoadedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                IsGpuAccelerated = await CheckGpuAvailability(),
                CanUnload = true,
                Capabilities = GetModelCapabilities(modelInfo),
                Metadata = new Dictionary<string, object>
                {
                    ["size_bytes"] = modelInfo.Size,
                    ["parameter_size"] = modelInfo.ParameterSize ?? "unknown",
                    ["quantization"] = modelInfo.Quantization ?? "unknown",
                    ["context_length"] = GetModelContextLength(modelInfo.Name),
                    ["supports_streaming"] = true,
                    ["supports_embeddings"] = SupportsEmbeddings(modelInfo.Name)
                }
            };

            LogInfo($"Selected Ollama model: {metadata.ModelName} for prompt type: {input.PromptType ?? "general"}");

            return TradingResult<AIModelMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            LogError("Failed to select optimal Ollama model", ex);
            return TradingResult<AIModelMetadata>.Failure(
                "MODEL_SELECTION_FAILED",
                ex.Message,
                "Unable to select appropriate Ollama model configuration");
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
            // For Ollama, we verify the model is available and optionally warm it up
            var modelCheck = await CheckModelStatus(model.ModelName);
            
            if (!modelCheck.Success)
            {
                return TradingResult<bool>.Failure(
                    "OLLAMA_MODEL_CHECK_FAILED",
                    modelCheck.ErrorMessage ?? "Failed to verify model status",
                    "Unable to confirm Ollama model availability");
            }

            // Warm up the model with a simple prompt (optional but recommended)
            if (_configuration.ModelCacheSettings.PreloadDefaultModels)
            {
                var warmupResult = await WarmupModel(model.ModelName);
                if (!warmupResult.Success)
                {
                    LogWarning($"Model warmup failed for {model.ModelName}, continuing anyway");
                }
            }

            // Mark as "loaded" in our tracking
            lock (_modelLock)
            {
                _loadedModels[model.ModelName] = model;
            }

            LogInfo($"Ollama model {model.ModelName} ready for inference");

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Failed to ensure Ollama model {model.ModelName} is ready", ex);
            return TradingResult<bool>.Failure(
                "MODEL_LOAD_EXCEPTION",
                ex.Message,
                "An error occurred while preparing Ollama model");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<OllamaResponse>> PerformInferenceAsync(
        OllamaPrompt input, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            var response = input.Stream 
                ? await StreamOllamaCompletion(input, model)
                : await GenerateOllamaCompletion(input, model);
            
            if (!response.Success || response.Data == null)
            {
                return TradingResult<OllamaResponse>.Failure(
                    "OLLAMA_INFERENCE_FAILED",
                    response.ErrorMessage ?? "Ollama inference failed",
                    "Failed to get response from local Ollama model");
            }

            LogInfo($"Ollama inference completed: Model={model.ModelName}, " +
                   $"Input tokens≈{EstimateTokenCount(input.Prompt)}, " +
                   $"Output tokens≈{EstimateTokenCount(response.Data.Response)}, " +
                   $"Duration={response.Data.TotalDuration.TotalSeconds:F2}s");

            return TradingResult<OllamaResponse>.Success(response.Data);
        }
        catch (Exception ex)
        {
            LogError($"Ollama inference failed for model: {model.ModelName}", ex);
            return TradingResult<OllamaResponse>.Failure(
                "OLLAMA_INFERENCE_EXCEPTION",
                ex.Message,
                "An error occurred during Ollama model inference");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<OllamaResponse>> PostProcessOutputAsync(
        OllamaResponse rawOutput, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            // Apply post-processing based on prompt type
            var processedResponse = await ApplyPromptTypeSpecificProcessing(rawOutput, model);

            // Extract structured data if applicable
            processedResponse = await ExtractStructuredData(processedResponse);

            // Apply financial domain validation
            processedResponse = ApplyFinancialValidation(processedResponse);

            // Calculate quality metrics
            processedResponse.ResponseQuality = CalculateResponseQuality(processedResponse);

            // Add performance metadata
            processedResponse.Metadata["post_processed_at"] = DateTime.UtcNow;
            processedResponse.Metadata["processing_version"] = "1.0";
            processedResponse.Metadata["model_temperature"] = processedResponse.Temperature;

            return TradingResult<OllamaResponse>.Success(processedResponse);
        }
        catch (Exception ex)
        {
            LogError("Ollama post-processing failed", ex);
            return TradingResult<OllamaResponse>.Failure(
                "POST_PROCESSING_FAILED",
                ex.Message,
                "Failed to post-process Ollama response");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override decimal GetOutputConfidence(OllamaResponse output)
    {
        return output?.Confidence ?? 0m;
    }

    // Ollama-specific implementation methods

    private async Task<TradingResult<List<OllamaModelInfo>>> GetAvailableOllamaModels()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            
            if (!response.IsSuccessStatusCode)
            {
                return TradingResult<List<OllamaModelInfo>>.Failure(
                    "OLLAMA_API_ERROR",
                    $"Ollama API returned {response.StatusCode}",
                    "Failed to retrieve available models from Ollama");
            }

            var content = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<OllamaModelsResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (modelsResponse?.Models == null)
            {
                return TradingResult<List<OllamaModelInfo>>.Success(new List<OllamaModelInfo>());
            }

            var modelInfos = modelsResponse.Models.Select(m => new OllamaModelInfo
            {
                Name = m.Name,
                Digest = m.Digest,
                Size = m.Size,
                ModifiedAt = m.ModifiedAt,
                ParameterSize = ExtractParameterSize(m.Name),
                Quantization = ExtractQuantization(m.Name)
            }).ToList();

            // Cache model info
            foreach (var modelInfo in modelInfos)
            {
                _availableModels[modelInfo.Name] = modelInfo;
            }

            return TradingResult<List<OllamaModelInfo>>.Success(modelInfos);
        }
        catch (Exception ex)
        {
            LogError("Failed to get available Ollama models", ex);
            return TradingResult<List<OllamaModelInfo>>.Failure(
                "MODELS_RETRIEVAL_EXCEPTION",
                ex.Message,
                "Error occurred while retrieving Ollama models");
        }
    }

    private async Task<TradingResult<OllamaModelInfo>> PullOllamaModel(string modelName)
    {
        try
        {
            var requestBody = new { name = modelName, stream = false };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/pull", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return TradingResult<OllamaModelInfo>.Failure(
                    "MODEL_PULL_FAILED",
                    $"Failed to pull model {modelName}: {response.StatusCode}",
                    "Unable to download the requested Ollama model");
            }

            // After successful pull, get model info
            var modelsResult = await GetAvailableOllamaModels();
            if (modelsResult.Success && modelsResult.Data != null)
            {
                var modelInfo = modelsResult.Data.FirstOrDefault(m => 
                    m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
                
                if (modelInfo != null)
                {
                    return TradingResult<OllamaModelInfo>.Success(modelInfo);
                }
            }

            return TradingResult<OllamaModelInfo>.Failure(
                "MODEL_INFO_NOT_FOUND",
                $"Model {modelName} pulled but info not found",
                "Model downloaded but unable to retrieve model information");
        }
        catch (Exception ex)
        {
            LogError($"Failed to pull Ollama model {modelName}", ex);
            return TradingResult<OllamaModelInfo>.Failure(
                "MODEL_PULL_EXCEPTION",
                ex.Message,
                "Error occurred while pulling Ollama model");
        }
    }

    private async Task<bool> CheckGpuAvailability()
    {
        try
        {
            // Check if Ollama is using GPU by querying system info
            // This is a simplified check - in production, would query actual GPU status
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private AIModelCapabilities GetModelCapabilities(OllamaModelInfo modelInfo)
    {
        // Determine capabilities based on model type
        var modelName = modelInfo.Name.ToLower();
        var isLargeModel = modelInfo.ParameterSize?.Contains("70b") == true || 
                          modelInfo.ParameterSize?.Contains("180b") == true;

        return new AIModelCapabilities
        {
            SupportedInputTypes = new() { "OllamaPrompt", "Text" },
            SupportedOutputTypes = new() { "OllamaResponse", "Text" },
            SupportedOperations = new() { 
                "TextGeneration", "Analysis", "Reasoning", "CodeGeneration", 
                "Summarization", "QuestionAnswering", "ChatCompletion" 
            },
            MaxBatchSize = 1, // Ollama processes one prompt at a time
            RequiresGpu = isLargeModel,
            SupportsStreaming = true,
            MaxInferenceTime = TimeSpan.FromMinutes(isLargeModel ? 5 : 2),
            MinConfidenceThreshold = 0.6m
        };
    }

    private string SelectOptimalOllamaModel(OllamaPrompt input, List<OllamaModelInfo> availableModels)
    {
        // Select model based on prompt type and available models
        var preferredModels = input.PromptType?.ToLower() switch
        {
            "code_generation" => new[] { "codellama", "deepseek-coder", "phind-codellama", "wizard-coder" },
            "financial_analysis" => new[] { "llama2:70b", "mixtral", "solar", "yi:34b" },
            "risk_assessment" => new[] { "llama2:13b", "mistral", "neural-chat" },
            "quick_summary" => new[] { "phi", "tinyllama", "orca-mini", "llama2:7b" },
            "reasoning" => new[] { "mixtral:8x7b", "llama2:70b", "wizard-lm" },
            _ => new[] { "llama2", "mistral", "neural-chat", "phi" }
        };

        // Find first available preferred model
        foreach (var preferred in preferredModels)
        {
            if (availableModels.Any(m => m.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase)))
            {
                return availableModels.First(m => 
                    m.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase)).Name;
            }
        }

        // Default to first available model
        return availableModels.FirstOrDefault()?.Name ?? "llama2";
    }

    private int GetModelMaxTokens(string modelName)
    {
        return modelName.ToLower() switch
        {
            var n when n.Contains("llama2") => 4096,
            var n when n.Contains("codellama") => 16384,
            var n when n.Contains("mixtral") => 32768,
            var n when n.Contains("mistral") => 8192,
            var n when n.Contains("phi") => 2048,
            var n when n.Contains("neural-chat") => 4096,
            var n when n.Contains("solar") => 4096,
            var n when n.Contains("yi") && n.Contains("34b") => 200000, // Yi-34B supports 200K context
            _ => 2048 // Conservative default
        };
    }

    private int GetModelContextLength(string modelName)
    {
        return GetModelMaxTokens(modelName);
    }

    private bool SupportsEmbeddings(string modelName)
    {
        // Models that support embeddings generation
        var embeddingModels = new[] { "llama2", "mistral", "nomic-embed-text" };
        return embeddingModels.Any(m => modelName.ToLower().Contains(m));
    }

    private string? ExtractParameterSize(string modelName)
    {
        // Extract parameter size from model name (e.g., "7b", "13b", "70b")
        var match = System.Text.RegularExpressions.Regex.Match(modelName, @"(\d+)b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private string? ExtractQuantization(string modelName)
    {
        // Extract quantization from model name (e.g., "q4_0", "q5_1")
        var match = System.Text.RegularExpressions.Regex.Match(modelName, @"q\d_\d|f16|f32");
        return match.Success ? match.Value : null;
    }

    private async Task<TradingResult<bool>> CheckModelStatus(string modelName)
    {
        try
        {
            var requestBody = new { name = modelName };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/show", content);
            
            return response.IsSuccessStatusCode 
                ? TradingResult<bool>.Success(true)
                : TradingResult<bool>.Failure(
                    "MODEL_NOT_LOADED",
                    $"Model {modelName} is not loaded",
                    "The requested model is not currently loaded in Ollama");
        }
        catch (Exception ex)
        {
            LogError($"Failed to check model status for {modelName}", ex);
            return TradingResult<bool>.Failure(
                "MODEL_CHECK_EXCEPTION",
                ex.Message,
                "Error occurred while checking model status");
        }
    }

    private async Task<TradingResult<bool>> WarmupModel(string modelName)
    {
        try
        {
            var warmupPrompt = new OllamaPrompt
            {
                Model = modelName,
                Prompt = "Hello, please respond with 'OK' to confirm you're ready.",
                Temperature = 0.1m,
                MaxTokens = 10,
                Stream = false
            };

            var metadata = new AIModelMetadata { ModelName = modelName };
            var result = await GenerateOllamaCompletion(warmupPrompt, metadata);
            
            return result.Success 
                ? TradingResult<bool>.Success(true)
                : TradingResult<bool>.Failure(
                    "WARMUP_FAILED",
                    "Model warmup failed",
                    "Unable to warm up the model");
        }
        catch (Exception ex)
        {
            LogError($"Failed to warm up model {modelName}", ex);
            return TradingResult<bool>.Failure(
                "WARMUP_EXCEPTION",
                ex.Message,
                "Error occurred during model warmup");
        }
    }

    private async Task<TradingResult<OllamaResponse>> GenerateOllamaCompletion(
        OllamaPrompt input, AIModelMetadata model)
    {
        try
        {
            var requestBody = new
            {
                model = model.ModelName,
                prompt = input.Prompt,
                system = input.SystemPrompt,
                template = input.Template,
                context = input.Context,
                stream = false,
                options = new
                {
                    num_predict = input.MaxTokens ?? 1024,
                    temperature = input.Temperature ?? 0.7m,
                    top_p = input.TopP ?? 0.9m,
                    top_k = input.TopK ?? 40,
                    repeat_penalty = input.RepeatPenalty ?? 1.1m,
                    stop = input.StopSequences ?? Array.Empty<string>(),
                    num_ctx = GetModelContextLength(model.ModelName)
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var startTime = DateTime.UtcNow;
            var httpResponse = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
            var totalDuration = DateTime.UtcNow - startTime;

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                LogError($"Ollama API error: {httpResponse.StatusCode} - {errorContent}");
                return TradingResult<OllamaResponse>.Failure(
                    "OLLAMA_API_ERROR",
                    $"API returned {httpResponse.StatusCode}: {errorContent}",
                    "Ollama API returned an error response");
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            var ollamaResult = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (ollamaResult == null)
            {
                return TradingResult<OllamaResponse>.Failure(
                    "INVALID_RESPONSE",
                    "Ollama returned invalid response",
                    "Unable to parse Ollama API response");
            }

            var response = new OllamaResponse
            {
                Response = ollamaResult.Response,
                Model = model.ModelName,
                CreatedAt = ollamaResult.CreatedAt ?? DateTime.UtcNow,
                Done = ollamaResult.Done,
                Context = ollamaResult.Context,
                TotalDuration = TimeSpan.FromNanoseconds(ollamaResult.TotalDuration ?? 0),
                LoadDuration = TimeSpan.FromNanoseconds(ollamaResult.LoadDuration ?? 0),
                PromptEvalDuration = TimeSpan.FromNanoseconds(ollamaResult.PromptEvalCount ?? 0),
                EvalDuration = TimeSpan.FromNanoseconds(ollamaResult.EvalDuration ?? 0),
                PromptEvalCount = ollamaResult.PromptEvalCount ?? 0,
                EvalCount = ollamaResult.EvalCount ?? 0,
                Temperature = input.Temperature ?? 0.7m,
                Confidence = CalculateConfidence(ollamaResult, input),
                PromptType = input.PromptType ?? "general"
            };

            return TradingResult<OllamaResponse>.Success(response);
        }
        catch (Exception ex)
        {
            LogError("Failed to generate Ollama completion", ex);
            return TradingResult<OllamaResponse>.Failure(
                "GENERATION_EXCEPTION",
                ex.Message,
                "Error occurred during Ollama completion generation");
        }
    }

    private async Task<TradingResult<OllamaResponse>> StreamOllamaCompletion(
        OllamaPrompt input, AIModelMetadata model)
    {
        try
        {
            var requestBody = new
            {
                model = model.ModelName,
                prompt = input.Prompt,
                system = input.SystemPrompt,
                stream = true,
                options = new
                {
                    num_predict = input.MaxTokens ?? 1024,
                    temperature = input.Temperature ?? 0.7m,
                    top_p = input.TopP ?? 0.9m,
                    top_k = input.TopK ?? 40,
                    repeat_penalty = input.RepeatPenalty ?? 1.1m,
                    stop = input.StopSequences ?? Array.Empty<string>()
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var startTime = DateTime.UtcNow;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
            {
                Content = content
            };

            var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                return TradingResult<OllamaResponse>.Failure(
                    "OLLAMA_STREAM_ERROR",
                    $"Stream API returned {httpResponse.StatusCode}: {errorContent}",
                    "Ollama streaming API returned an error");
            }

            var responseBuilder = new StringBuilder();
            var stream = await httpResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? line;
            OllamaGenerateResponse? lastResponse = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var streamResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(line,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (streamResponse != null)
                    {
                        responseBuilder.Append(streamResponse.Response);
                        
                        // Call stream callback if provided
                        input.StreamCallback?.Invoke(streamResponse.Response);

                        if (streamResponse.Done)
                        {
                            lastResponse = streamResponse;
                            break;
                        }
                    }
                }
                catch (JsonException)
                {
                    LogWarning($"Failed to parse streaming response line: {line}");
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;

            var response = new OllamaResponse
            {
                Response = responseBuilder.ToString(),
                Model = model.ModelName,
                CreatedAt = DateTime.UtcNow,
                Done = true,
                Context = lastResponse?.Context,
                TotalDuration = totalDuration,
                LoadDuration = TimeSpan.FromNanoseconds(lastResponse?.LoadDuration ?? 0),
                PromptEvalDuration = TimeSpan.FromNanoseconds(lastResponse?.PromptEvalCount ?? 0),
                EvalDuration = TimeSpan.FromNanoseconds(lastResponse?.EvalDuration ?? 0),
                PromptEvalCount = lastResponse?.PromptEvalCount ?? 0,
                EvalCount = lastResponse?.EvalCount ?? 0,
                Temperature = input.Temperature ?? 0.7m,
                Confidence = 0.85m, // Default confidence for streaming
                PromptType = input.PromptType ?? "general"
            };

            return TradingResult<OllamaResponse>.Success(response);
        }
        catch (Exception ex)
        {
            LogError("Failed to stream Ollama completion", ex);
            return TradingResult<OllamaResponse>.Failure(
                "STREAMING_EXCEPTION",
                ex.Message,
                "Error occurred during Ollama streaming completion");
        }
    }

    private decimal CalculateConfidence(OllamaGenerateResponse response, OllamaPrompt input)
    {
        decimal confidence = 0.8m; // Base confidence for local models

        // Adjust based on response completeness
        if (response.Done)
        {
            confidence += 0.05m;
        }

        // Adjust based on temperature (lower temperature = higher confidence)
        var temperature = input.Temperature ?? 0.7m;
        confidence += (1m - temperature) * 0.1m;

        // Adjust based on response length vs requested length
        if (input.MaxTokens.HasValue && response.EvalCount.HasValue)
        {
            var completionRatio = (decimal)response.EvalCount.Value / input.MaxTokens.Value;
            if (completionRatio < 0.1m) // Very short response
            {
                confidence *= 0.8m;
            }
        }

        return Math.Max(0.1m, Math.Min(1.0m, confidence));
    }

    private bool IsValidPromptType(string promptType)
    {
        var validTypes = new[]
        {
            "market_analysis", "strategy_generation", "risk_assessment", 
            "code_generation", "data_extraction", "summarization",
            "financial_report", "sentiment_analysis", "general"
        };
        
        return validTypes.Contains(promptType.ToLower());
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return text?.Length / 4 ?? 0;
    }

    private async Task<OllamaResponse> ApplyPromptTypeSpecificProcessing(
        OllamaResponse response, AIModelMetadata model)
    {
        LogMethodEntry();

        try
        {
            switch (response.PromptType?.ToLower())
            {
                case "market_analysis":
                    response.StructuredData["analysis_type"] = "market_analysis";
                    response.StructuredData["key_insights"] = ExtractKeyInsights(response.Response);
                    break;
                    
                case "risk_assessment":
                    response.StructuredData["risk_level"] = ExtractRiskLevel(response.Response);
                    response.StructuredData["risk_factors"] = ExtractRiskFactors(response.Response);
                    break;
                    
                case "code_generation":
                    response.StructuredData["code_language"] = DetectCodeLanguage(response.Response);
                    response.StructuredData["has_code_blocks"] = response.Response.Contains("```");
                    break;
                    
                case "financial_report":
                    response.StructuredData["report_sections"] = ExtractReportSections(response.Response);
                    break;
            }

            await Task.CompletedTask;
            return response;
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply prompt-specific processing for type: {response.PromptType}", ex);
            return response;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<OllamaResponse> ExtractStructuredData(OllamaResponse response)
    {
        try
        {
            // Extract JSON if present
            if (response.Response.Contains("{") && response.Response.Contains("}"))
            {
                var jsonStart = response.Response.IndexOf('{');
                var jsonEnd = response.Response.LastIndexOf('}') + 1;
                if (jsonEnd > jsonStart)
                {
                    var jsonText = response.Response.Substring(jsonStart, jsonEnd - jsonStart);
                    try
                    {
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
            }

            // Extract numbers
            response.StructuredData["extracted_numbers"] = ExtractNumbers(response.Response);
            
            // Extract percentages
            response.StructuredData["extracted_percentages"] = ExtractPercentages(response.Response);

            await Task.CompletedTask;
            return response;
        }
        catch (Exception ex)
        {
            LogError("Failed to extract structured data", ex);
            return response;
        }
    }

    private OllamaResponse ApplyFinancialValidation(OllamaResponse response)
    {
        // Check for unrealistic financial claims
        var numbers = ExtractNumbers(response.Response);
        var unrealisticReturns = numbers.Where(n => n > 1000m).ToList(); // >1000% returns
        
        if (unrealisticReturns.Any())
        {
            response.Metadata["contains_unrealistic_claims"] = true;
            response.Confidence *= 0.7m; // Reduce confidence for unrealistic claims
            LogWarning($"Response contains unrealistic financial claims: {string.Join(", ", unrealisticReturns)}");
        }

        // Check for risk disclaimers
        var hasRiskDisclaimer = response.Response.ToLower().Contains("risk") || 
                               response.Response.ToLower().Contains("disclaimer") ||
                               response.Response.ToLower().Contains("not financial advice");
        
        response.Metadata["has_risk_disclaimer"] = hasRiskDisclaimer;

        return response;
    }

    private decimal CalculateResponseQuality(OllamaResponse response)
    {
        decimal quality = 0.8m; // Base quality for Ollama responses

        // Check response length
        if (response.Response.Length < 50)
        {
            quality -= 0.2m; // Penalize very short responses
        }
        else if (response.Response.Length > 2000)
        {
            quality += 0.1m; // Reward comprehensive responses
        }

        // Check for structure (paragraphs, lists, etc.)
        var hasParagraphs = response.Response.Contains("\n\n");
        var hasLists = response.Response.Contains("•") || response.Response.Contains("-") || 
                      response.Response.Contains("1.");
        
        if (hasParagraphs || hasLists)
        {
            quality += 0.05m;
        }

        // Check for code blocks if code generation
        if (response.PromptType == "code_generation" && response.Response.Contains("```"))
        {
            quality += 0.1m;
        }

        // Ensure quality is within bounds
        return Math.Max(0.1m, Math.Min(1.0m, quality));
    }

    // Helper extraction methods
    private List<string> ExtractKeyInsights(string text)
    {
        var insights = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.ToLower().Contains("insight") || line.ToLower().Contains("key") || 
                line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*"))
            {
                insights.Add(line.Trim());
            }
        }
        
        return insights;
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

    private List<string> ExtractRiskFactors(string text)
    {
        var factors = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        bool inRiskSection = false;
        foreach (var line in lines)
        {
            if (line.ToLower().Contains("risk"))
            {
                inRiskSection = true;
            }
            
            if (inRiskSection && (line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*")))
            {
                factors.Add(line.Trim());
            }
        }
        
        return factors;
    }

    private string DetectCodeLanguage(string text)
    {
        if (text.Contains("```csharp") || text.Contains("```cs"))
            return "csharp";
        if (text.Contains("```python") || text.Contains("```py"))
            return "python";
        if (text.Contains("```javascript") || text.Contains("```js"))
            return "javascript";
        if (text.Contains("```sql"))
            return "sql";
        return "unknown";
    }

    private List<string> ExtractReportSections(string text)
    {
        var sections = new List<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Look for section headers (typically in caps or with special formatting)
            if (line.Length > 3 && line.Length < 100 && 
                (line.ToUpper() == line || line.EndsWith(":") || line.StartsWith("#")))
            {
                sections.Add(line.Trim());
            }
        }
        
        return sections;
    }

    private List<decimal> ExtractNumbers(string text)
    {
        var numbers = new List<decimal>();
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b\d+\.?\d*\b");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (decimal.TryParse(match.Value, out var number))
            {
                numbers.Add(number);
            }
        }
        
        return numbers;
    }

    private List<string> ExtractPercentages(string text)
    {
        var percentages = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b\d+\.?\d*%");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            percentages.Add(match.Value);
        }
        
        return percentages;
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

// Ollama-specific model classes

/// <summary>
/// Input prompt for Ollama LLM
/// </summary>
public class OllamaPrompt
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? Template { get; set; }
    public List<int>? Context { get; set; }
    public string? PromptType { get; set; }
    public bool Stream { get; set; } = false;
    public Action<string>? StreamCallback { get; set; }
    public int? MaxTokens { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? TopP { get; set; }
    public int? TopK { get; set; }
    public decimal? RepeatPenalty { get; set; }
    public string[]? StopSequences { get; set; }
    public string? Quantization { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Response from Ollama LLM
/// </summary>
public class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Done { get; set; }
    public List<int>? Context { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan LoadDuration { get; set; }
    public TimeSpan PromptEvalDuration { get; set; }
    public TimeSpan EvalDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public int EvalCount { get; set; }
    public decimal Temperature { get; set; }
    public decimal Confidence { get; set; }
    public decimal ResponseQuality { get; set; }
    public string PromptType { get; set; } = string.Empty;
    public Dictionary<string, object> StructuredData { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Ollama model information
/// </summary>
public class OllamaModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Digest { get; set; }
    public long Size { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ParameterSize { get; set; }
    public string? Quantization { get; set; }
}

// Ollama API response models
internal class OllamaModelsResponse
{
    public List<OllamaModelDetails>? Models { get; set; }
}

internal class OllamaModelDetails
{
    public string Name { get; set; } = string.Empty;
    public string? Digest { get; set; }
    public long Size { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

internal class OllamaGenerateResponse
{
    public string Response { get; set; } = string.Empty;
    public string? Model { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool Done { get; set; }
    public List<int>? Context { get; set; }
    public long? TotalDuration { get; set; }
    public long? LoadDuration { get; set; }
    public int? PromptEvalCount { get; set; }
    public long? EvalDuration { get; set; }
    public int? EvalCount { get; set; }
}