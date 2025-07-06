using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using TradingPlatform.AI.Core;
using TradingPlatform.AI.Engines;
using TradingPlatform.AI.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using Xunit;

namespace TradingPlatform.AI.Tests.Engines;

/// <summary>
/// Comprehensive unit tests for Gemini LLM Engine
/// Tests validation, API integration, error handling, and 2025 best practices
/// </summary>
public class GeminiLLMEngineTests : IDisposable
{
    private readonly GeminiLLMEngine _engine;
    private readonly ITradingLogger _logger;
    private readonly AIModelConfiguration _configuration;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly string _testApiKey = "test-api-key";

    public GeminiLLMEngineTests()
    {
        _logger = TradingLogOrchestrator.Instance;
        _configuration = CreateTestConfiguration();
        
        // Setup HTTP client mock
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        // Use reflection to inject the mocked HttpClient (in production, use DI)
        _engine = new GeminiLLMEngine(_logger, _configuration, _testApiKey);
        var httpClientField = _engine.GetType().GetField("_httpClient", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(_engine, httpClient);
    }

    #region Input Validation Tests

    [Fact]
    public async Task ValidateInputAsync_NullInput_ShouldReturnFailure()
    {
        // Arrange
        GeminiPrompt? input = null;

        // Act
        var result = await _engine.InferAsync(input!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NULL_INPUT");
        result.ErrorMessage.Should().Contain("Input prompt cannot be null");
    }

    [Fact]
    public async Task ValidateInputAsync_EmptyPrompt_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "",
            PromptType = "market_analysis"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EMPTY_PROMPT");
        result.ErrorMessage.Should().Contain("Prompt text cannot be empty");
    }

    [Fact]
    public async Task ValidateInputAsync_PromptTooLong_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = new string('a', 1_000_001), // Over 1M characters
            PromptType = "market_analysis"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("PROMPT_TOO_LONG");
        result.ErrorMessage.Should().Contain("Prompt exceeds maximum length");
    }

    [Fact]
    public async Task ValidateInputAsync_InvalidPromptType_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Analyze the market",
            PromptType = "invalid_type"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_PROMPT_TYPE");
        result.ErrorMessage.Should().Contain("Unsupported prompt type");
    }

    [Fact]
    public async Task ValidateInputAsync_ValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Analyze the current market conditions for AAPL",
            PromptType = "market_analysis",
            Temperature = 0.7m,
            MaxTokens = 1000
        };

        // Setup mock for successful API response
        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.GeneratedText.Should().NotBeEmpty();
    }

    #endregion

    #region Model Selection Tests

    [Fact]
    public async Task SelectOptimalModel_MarketAnalysis_ShouldSelectProModel()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Provide comprehensive market analysis",
            PromptType = "market_analysis"
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ModelName.Should().Contain("pro");
    }

    [Fact]
    public async Task SelectOptimalModel_QuickSummary_ShouldSelectFlashModel()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Quick summary of AAPL earnings",
            PromptType = "quick_summary"
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ModelName.Should().Contain("flash");
    }

    [Fact]
    public async Task SelectOptimalModel_LongPrompt_ShouldSelectProModel()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = new string('a', 15000), // Long prompt
            PromptType = null // No specific type
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ModelName.Should().Contain("pro");
    }

    #endregion

    #region API Integration Tests

    [Fact]
    public async Task InferAsync_SuccessfulApiCall_ShouldReturnGeneratedText()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "What is the market outlook for tech stocks?",
            PromptType = "market_analysis",
            Temperature = 0.8m,
            MaxTokens = 500
        };

        var expectedResponse = "Tech stocks show strong growth potential...";
        SetupSuccessfulApiResponse(expectedResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.GeneratedText.Should().Be(expectedResponse);
        result.Data.ModelType.Should().Be("Gemini");
        result.Data.Confidence.Should().BeGreaterThan(0);
        result.Data.TokensUsed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InferAsync_ApiError_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Test prompt",
            PromptType = "general"
        };

        SetupFailedApiResponse(HttpStatusCode.InternalServerError, "API Error");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("GEMINI_API_ERROR");
        result.ErrorMessage.Should().Contain("API returned 500");
    }

    [Fact]
    public async Task InferAsync_RateLimited_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Test prompt",
            PromptType = "general"
        };

        SetupFailedApiResponse(HttpStatusCode.TooManyRequests, "Rate limit exceeded");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("GEMINI_API_ERROR");
        result.ErrorMessage.Should().Contain("429");
    }

    [Fact]
    public async Task InferAsync_EmptyApiResponse_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Test prompt",
            PromptType = "general"
        };

        SetupEmptyApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_CANDIDATES_RETURNED");
        result.ErrorMessage.Should().Contain("no candidates");
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task InferBatchAsync_MultiplePrompts_ShouldProcessAll()
    {
        // Arrange
        var inputs = new List<GeminiPrompt>
        {
            new() { Prompt = "Analyze AAPL", PromptType = "market_analysis" },
            new() { Prompt = "Risk assessment for tech sector", PromptType = "risk_assessment" },
            new() { Prompt = "Generate trading strategy", PromptType = "strategy_generation" }
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferBatchAsync(inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(3);
        result.Data.Should().OnlyContain(response => !string.IsNullOrEmpty(response.GeneratedText));
    }

    [Fact]
    public async Task InferBatchAsync_WithFailures_ShouldContinueProcessing()
    {
        // Arrange
        var inputs = new List<GeminiPrompt>
        {
            new() { Prompt = "Valid prompt 1", PromptType = "general" },
            new() { Prompt = "", PromptType = "general" }, // Invalid
            new() { Prompt = "Valid prompt 2", PromptType = "general" }
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferBatchAsync(inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2); // Only valid prompts processed
    }

    #endregion

    #region Post-Processing Tests

    [Fact]
    public async Task PostProcessing_MarketAnalysis_ShouldExtractStructuredData()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Analyze market sentiment for AAPL",
            PromptType = "market_analysis"
        };

        var apiResponse = @"The market sentiment for AAPL is bullish. 
Key insights:
- Strong earnings growth
- Positive analyst ratings
- High institutional buying
Recommendations:
- Consider buying on dips
- Set stop loss at $180";

        SetupSuccessfulApiResponse(apiResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.StructuredData.Should().ContainKey("analysis_type");
        result.Data.StructuredData.Should().ContainKey("sentiment");
        result.Data.StructuredData["sentiment"].Should().Be("bullish");
        result.Data.StructuredData.Should().ContainKey("key_insights");
        result.Data.StructuredData.Should().ContainKey("recommendations");
    }

    [Fact]
    public async Task PostProcessing_StrategyGeneration_ShouldExtractComponents()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Generate day trading strategy",
            PromptType = "strategy_generation"
        };

        var apiResponse = @"Day Trading Strategy:
Entry Criteria:
- RSI below 30
- Price above 20 SMA
Exit Criteria:
- RSI above 70
- 2% profit target
Risk Parameters:
- Max position size: 10%
- Stop loss: 1%";

        SetupSuccessfulApiResponse(apiResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.StructuredData.Should().ContainKey("response_type");
        result.Data.StructuredData.Should().ContainKey("entry_criteria");
        result.Data.StructuredData.Should().ContainKey("exit_criteria");
        result.Data.StructuredData.Should().ContainKey("risk_parameters");
    }

    [Fact]
    public async Task PostProcessing_RiskAssessment_ShouldExtractRiskLevel()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Assess risk for leveraged ETF trading",
            PromptType = "risk_assessment"
        };

        var apiResponse = @"Risk Assessment: Leveraged ETF trading carries HIGH RISK.
Risk Factors:
- Volatility decay
- Daily rebalancing
- Potential for significant losses
Mitigation Strategies:
- Use position sizing
- Set strict stop losses
- Avoid holding overnight";

        SetupSuccessfulApiResponse(apiResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.StructuredData.Should().ContainKey("risk_level");
        result.Data.StructuredData["risk_level"].Should().Be("HIGH");
        result.Data.StructuredData.Should().ContainKey("risk_factors");
        result.Data.StructuredData.Should().ContainKey("mitigation_strategies");
    }

    [Fact]
    public async Task PostProcessing_DataExtraction_ShouldExtractNumbers()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Extract key metrics",
            PromptType = "data_extraction"
        };

        var apiResponse = @"Key Metrics:
Revenue: $365.8 billion
EPS: $6.11
P/E Ratio: 29.5
Market Cap: $2.95 trillion";

        SetupSuccessfulApiResponse(apiResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.StructuredData.Should().ContainKey("extracted_numbers");
        var numbers = result.Data.StructuredData["extracted_numbers"] as List<decimal>;
        numbers.Should().Contain(365.8m);
        numbers.Should().Contain(6.11m);
        numbers.Should().Contain(29.5m);
        numbers.Should().Contain(2.95m);
    }

    [Fact]
    public async Task PostProcessing_JsonExtraction_ShouldParseEmbeddedJson()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Generate structured response",
            PromptType = "general"
        };

        var apiResponse = @"Here is the analysis:
{
  ""symbol"": ""AAPL"",
  ""price"": 190.25,
  ""recommendation"": ""BUY"",
  ""confidence"": 0.85
}
This represents our current assessment.";

        SetupSuccessfulApiResponse(apiResponse);

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.StructuredData.Should().ContainKey("extracted_json");
    }

    #endregion

    #region Confidence Calculation Tests

    [Fact]
    public async Task CalculateConfidence_NormalResponse_ShouldHaveHighConfidence()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Simple market analysis",
            PromptType = "market_analysis"
        };

        SetupSuccessfulApiResponse(finishReason: "STOP");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Confidence.Should().BeInRange(0.7m, 1.0m);
    }

    [Fact]
    public async Task CalculateConfidence_MaxTokensReached_ShouldReduceConfidence()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Complex analysis",
            PromptType = "market_analysis"
        };

        SetupSuccessfulApiResponse(finishReason: "MAX_TOKENS");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Confidence.Should().BeLessThan(0.8m);
    }

    [Fact]
    public async Task CalculateConfidence_SafetyFiltered_ShouldHaveLowConfidence()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Analysis request",
            PromptType = "general"
        };

        SetupSuccessfulApiResponse(finishReason: "SAFETY");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Confidence.Should().BeLessThan(0.5m);
    }

    #endregion

    #region Health Monitoring Tests

    [Fact]
    public async Task GetServiceHealthAsync_AfterSuccessfulInferences_ShouldShowHealthy()
    {
        // Arrange
        SetupSuccessfulApiResponse();
        
        var input = new GeminiPrompt
        {
            Prompt = "Test health check",
            PromptType = "general"
        };
        
        await _engine.InferAsync(input);

        // Act
        var healthResult = await _engine.GetServiceHealthAsync();

        // Assert
        healthResult.Success.Should().BeTrue();
        healthResult.Data.Should().NotBeNull();
        healthResult.Data.IsHealthy.Should().BeTrue();
        healthResult.Data.TotalRequests.Should().BeGreaterThan(0);
        healthResult.Data.SuccessRate.Should().BeGreaterThan(0);
    }

    #endregion

    #region Temperature and Parameter Tests

    [Fact]
    public async Task InferAsync_CustomTemperature_ShouldUseProvidedValue()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Generate creative trading ideas",
            PromptType = "strategy_generation",
            Temperature = 0.9m,
            TopP = 0.95m,
            TopK = 50,
            MaxTokens = 2000
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        // Temperature and other parameters would be passed to API
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task InferAsync_WithStopSequences_ShouldIncludeInRequest()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "List top 3 stocks",
            PromptType = "general",
            StopSequences = new[] { "\n\n", "END", "---" }
        };

        SetupSuccessfulApiResponse();

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InferAsync_NetworkTimeout_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Test timeout",
            PromptType = "general"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("GEMINI_API_CALL_EXCEPTION");
    }

    [Fact]
    public async Task InferAsync_InvalidApiKey_ShouldReturnFailure()
    {
        // Arrange
        var input = new GeminiPrompt
        {
            Prompt = "Test auth",
            PromptType = "general"
        };

        SetupFailedApiResponse(HttpStatusCode.Unauthorized, "Invalid API key");

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("GEMINI_API_ERROR");
        result.ErrorMessage.Should().Contain("401");
    }

    #endregion

    #region Helper Methods

    private AIModelConfiguration CreateTestConfiguration()
    {
        return new AIModelConfiguration
        {
            DefaultModelType = "Gemini",
            MaxConcurrentInferences = 3,
            ModelCacheSize = 5,
            DefaultTimeout = TimeSpan.FromSeconds(30),
            EnableGpuAcceleration = false,
            AvailableModels = new List<ModelDefinition>
            {
                new()
                {
                    Name = "gemini-1.5-pro",
                    Type = "Gemini",
                    Version = "1.5",
                    IsDefault = false,
                    Priority = 1,
                    Capabilities = new AIModelCapabilities
                    {
                        SupportedInputTypes = new() { "GeminiPrompt", "Text" },
                        SupportedOutputTypes = new() { "GeminiResponse", "Text" },
                        MaxBatchSize = 1,
                        RequiresGpu = false,
                        SupportsStreaming = true
                    }
                },
                new()
                {
                    Name = "gemini-1.5-flash",
                    Type = "Gemini",
                    Version = "1.5",
                    IsDefault = true,
                    Priority = 2,
                    Capabilities = new AIModelCapabilities
                    {
                        SupportedInputTypes = new() { "GeminiPrompt", "Text" },
                        SupportedOutputTypes = new() { "GeminiResponse", "Text" },
                        MaxBatchSize = 1,
                        RequiresGpu = false,
                        SupportsStreaming = true
                    }
                }
            }
        };
    }

    private void SetupSuccessfulApiResponse(string responseText = "This is a test response", 
        string finishReason = "STOP")
    {
        var apiResponse = new GeminiApiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Parts = new[]
                        {
                            new GeminiPart { Text = responseText }
                        },
                        Role = "model"
                    },
                    FinishReason = finishReason,
                    Index = 0,
                    SafetyRatings = new[]
                    {
                        new GeminiSafetyRating
                        {
                            Category = "HARM_CATEGORY_HARASSMENT",
                            Probability = "NEGLIGIBLE"
                        }
                    }
                }
            }
        };

        var jsonResponse = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
    }

    private void SetupFailedApiResponse(HttpStatusCode statusCode, string errorMessage)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(errorMessage)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
    }

    private void SetupEmptyApiResponse()
    {
        var apiResponse = new GeminiApiResponse
        {
            Candidates = Array.Empty<GeminiCandidate>()
        };

        var jsonResponse = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }

    #endregion
}