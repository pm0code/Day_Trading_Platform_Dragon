using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using TradingPlatform.AlternativeData.Providers.Satellite;
using TradingPlatform.AlternativeData.Models;
using TradingPlatform.AlternativeData.Interfaces;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.CostManagement.Services;

namespace TradingPlatform.AlternativeData.Tests;

public class SatelliteDataProviderTests
{
    private readonly Mock<ITradingLogger> _mockLogger;
    private readonly Mock<IProphetTimeSeriesService> _mockProphetService;
    private readonly Mock<INeuralProphetService> _mockNeuralProphetService;
    private readonly Mock<DataSourceCostTracker> _mockCostTracker;
    private readonly Mock<HttpClient> _mockHttpClient;
    private readonly IOptions<AlternativeDataConfiguration> _config;
    private readonly SatelliteDataProvider _provider;

    public SatelliteDataProviderTests()
    {
        _mockLogger = new Mock<ITradingLogger>();
        _mockProphetService = new Mock<IProphetTimeSeriesService>();
        _mockNeuralProphetService = new Mock<INeuralProphetService>();
        _mockCostTracker = new Mock<DataSourceCostTracker>();
        _mockHttpClient = new Mock<HttpClient>();

        var config = new AlternativeDataConfiguration
        {
            Providers = new Dictionary<string, AlternativeDataProvider>(),
            AIModels = new Dictionary<string, AIModelConfig>
            {
                ["Prophet"] = new AIModelConfig
                {
                    ModelName = "Prophet",
                    ModelType = "forecasting",
                    ModelPath = "prophet_model",
                    Parameters = new Dictionary<string, object>(),
                    RequiresGPU = false,
                    MaxBatchSize = 100,
                    Timeout = TimeSpan.FromMinutes(5)
                }
            },
            Processing = new ProcessingSettings
            {
                MaxConcurrentTasks = 10,
                TaskTimeout = TimeSpan.FromMinutes(5),
                RetryAttempts = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                EnableBatching = true,
                BatchSize = 10,
                BatchTimeout = TimeSpan.FromMinutes(1)
            },
            Cost = new CostSettings
            {
                DailyBudget = 1000m,
                MonthlyBudget = 30000m,
                CostPerGPUHour = 2.50m,
                ProviderCosts = new Dictionary<string, decimal>(),
                EnableCostControls = true,
                CostAlertThreshold = 0.8m
            },
            Quality = new QualitySettings
            {
                MinConfidenceScore = 0.7m,
                MinSignalStrength = 0.5m,
                MinDataPoints = 10,
                MaxDataAge = TimeSpan.FromHours(24),
                EnableQualityFiltering = true,
                QualityThresholds = new Dictionary<string, decimal>()
            }
        };

        _config = Options.Create(config);

        _provider = new SatelliteDataProvider(
            _mockLogger.Object,
            _config,
            _mockProphetService.Object,
            _mockNeuralProphetService.Object,
            _mockCostTracker.Object,
            _mockHttpClient.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act & Assert
        _provider.ProviderId.Should().Be("satellite_provider");
        _provider.DataType.Should().Be(AlternativeDataType.SatelliteImagery);
        _provider.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCallCanonicalLoggingMethods()
    {
        // Arrange
        _mockProphetService.Setup(x => x.InitializeAsync(It.IsAny<AIModelConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Core.Models.TradingResult<bool>.Success(true));

        // Act
        var result = await _provider.InitializeAsync();

        // Assert
        result.Should().BeTrue();
        
        // Verify that canonical logging methods were called
        _mockLogger.Verify(x => x.LogMethodEntry(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
        _mockLogger.Verify(x => x.LogMethodExit(It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
        _mockLogger.Verify(x => x.LogInfo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetDataAsync_WithValidRequest_ShouldReturnSuccessResult()
    {
        // Arrange
        var request = new AlternativeDataRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            DataType = AlternativeDataType.SatelliteImagery,
            Symbols = new List<string> { "AAPL", "MSFT" },
            StartTime = DateTime.UtcNow.AddDays(-7),
            EndTime = DateTime.UtcNow,
            RequestedBy = "test_user"
        };

        // Initialize the provider first
        await _provider.InitializeAsync();
        await _provider.StartAsync();

        // Act
        var result = await _provider.GetDataAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.RequestId.Should().Be(request.RequestId);
        result.Data.Success.Should().BeTrue();

        // Verify canonical logging was used
        _mockLogger.Verify(x => x.LogMethodEntry(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
        _mockLogger.Verify(x => x.LogMethodExit(It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetDataAsync_WithInvalidRequest_ShouldReturnFailureResult()
    {
        // Arrange
        var request = new AlternativeDataRequest
        {
            RequestId = "",
            DataType = AlternativeDataType.SocialMediaSentiment, // Wrong data type
            Symbols = new List<string>(),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddDays(-1), // End before start
            RequestedBy = "test_user"
        };

        // Act
        var result = await _provider.GetDataAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        // Verify error logging was used
        _mockLogger.Verify(x => x.LogError(
            It.IsAny<string>(), 
            It.IsAny<Exception>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<int>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EstimateCostAsync_ShouldReturnValidCostEstimate()
    {
        // Arrange
        var request = new AlternativeDataRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            DataType = AlternativeDataType.SatelliteImagery,
            Symbols = new List<string> { "AAPL", "MSFT" },
            StartTime = DateTime.UtcNow.AddDays(-7),
            EndTime = DateTime.UtcNow,
            RequestedBy = "test_user"
        };

        // Act
        var result = await _provider.EstimateCostAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldReturnTrue()
    {
        // Act
        var result = await _provider.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceLifecycle_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var initResult = await _provider.InitializeAsync();
        var startResult = await _provider.StartAsync();
        var healthCheck = await _provider.CheckHealthAsync();
        var stopResult = await _provider.StopAsync();

        // Assert
        initResult.Should().BeTrue();
        startResult.Should().BeTrue();
        healthCheck.Should().NotBeNull();
        healthCheck.IsHealthy.Should().BeTrue();
        stopResult.Should().BeTrue();

        // Verify lifecycle logging
        _mockLogger.Verify(x => x.LogInfo(
            It.Is<string>(s => s.Contains("initialized") || s.Contains("started") || s.Contains("stopped")), 
            It.IsAny<object>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<int>()), 
            Times.AtLeast(3));
    }
}