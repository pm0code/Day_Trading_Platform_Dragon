using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RestSharp;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.DataIngestion.Providers
{
    public class AlphaVantageProviderTests : CanonicalTestBase
    {
        private readonly Mock<IRestClient> _mockRestClient;
        private readonly Mock<IRateLimiter> _mockRateLimiter;
        private readonly Mock<IOptions<MarketConfiguration>> _mockConfig;
        private readonly AlphaVantageProvider _provider;

        public AlphaVantageProviderTests(ITestOutputHelper output) : base(output)
        {
            _mockRestClient = new Mock<IRestClient>();
            _mockRateLimiter = new Mock<IRateLimiter>();
            _mockConfig = new Mock<IOptions<MarketConfiguration>>();

            var config = new MarketConfiguration
            {
                AlphaVantageApiKey = "test_api_key",
                AlphaVantageBaseUrl = "https://api.alphavantage.co",
                MaxRetries = 3,
                RetryDelayMs = 100
            };
            _mockConfig.Setup(x => x.Value).Returns(config);

            _mockRateLimiter.Setup(x => x.WaitIfNeededAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _provider = new AlphaVantageProvider(MockLogger.Object, _mockRestClient.Object, 
                _mockRateLimiter.Object, _mockConfig.Object);
        }

        [Fact]
        public async Task GetQuoteAsync_WithValidSymbol_ReturnsMarketData()
        {
            // Arrange
            var symbol = "AAPL";
            var expectedResponse = @"{
                ""Global Quote"": {
                    ""01. symbol"": ""AAPL"",
                    ""02. open"": ""150.00"",
                    ""03. high"": ""155.00"",
                    ""04. low"": ""149.00"",
                    ""05. price"": ""152.50"",
                    ""06. volume"": ""50000000"",
                    ""07. latest trading day"": ""2024-01-25"",
                    ""08. previous close"": ""149.50"",
                    ""09. change"": ""3.00"",
                    ""10. change percent"": ""2.01%""
                }
            }";

            var mockResponse = new Mock<RestResponse>();
            mockResponse.Setup(x => x.IsSuccessful).Returns(true);
            mockResponse.Setup(x => x.Content).Returns(expectedResponse);
            mockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);

            _mockRestClient.Setup(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            var result = await _provider.GetQuoteAsync(symbol, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var marketData = result.Value;
            marketData.Symbol.Should().Be("AAPL");
            marketData.Open.Should().Be(150.00m);
            marketData.High.Should().Be(155.00m);
            marketData.Low.Should().Be(149.00m);
            marketData.Close.Should().Be(152.50m);
            marketData.Volume.Should().Be(50000000);

            // Verify rate limiter was called
            _mockRateLimiter.Verify(x => x.WaitIfNeededAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetQuoteAsync_WithApiError_ReturnsFailure()
        {
            // Arrange
            var symbol = "INVALID";
            var errorResponse = @"{""Error Message"": ""Invalid API call""}";

            var mockResponse = new Mock<RestResponse>();
            mockResponse.Setup(x => x.IsSuccessful).Returns(false);
            mockResponse.Setup(x => x.Content).Returns(errorResponse);
            mockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.BadRequest);
            mockResponse.Setup(x => x.ErrorMessage).Returns("Bad Request");

            _mockRestClient.Setup(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            var result = await _provider.GetQuoteAsync(symbol, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Bad Request");
        }

        [Fact]
        public async Task GetQuoteAsync_WithRateLimitError_RetriesAndSucceeds()
        {
            // Arrange
            var symbol = "TSLA";
            var rateLimitResponse = @"{""Note"": ""Thank you for using Alpha Vantage!""}";
            var successResponse = @"{
                ""Global Quote"": {
                    ""01. symbol"": ""TSLA"",
                    ""05. price"": ""200.00""
                }
            }";

            var rateLimitMockResponse = new Mock<RestResponse>();
            rateLimitMockResponse.Setup(x => x.IsSuccessful).Returns(true);
            rateLimitMockResponse.Setup(x => x.Content).Returns(rateLimitResponse);
            rateLimitMockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);

            var successMockResponse = new Mock<RestResponse>();
            successMockResponse.Setup(x => x.IsSuccessful).Returns(true);
            successMockResponse.Setup(x => x.Content).Returns(successResponse);
            successMockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);

            _mockRestClient.SetupSequence(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(rateLimitMockResponse.Object) // First call - rate limited
                .ReturnsAsync(successMockResponse.Object);  // Second call - success

            // Act
            var result = await _provider.GetQuoteAsync(symbol, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            result.Value.Symbol.Should().Be("TSLA");
            result.Value.Close.Should().Be(200.00m);

            // Verify retry occurred
            _mockRestClient.Verify(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetIntradayDataAsync_WithValidParameters_ReturnsTimeSeries()
        {
            // Arrange
            var symbol = "AAPL";
            var interval = "5min";
            var timeSeriesResponse = @"{
                ""Meta Data"": {
                    ""1. Information"": ""Intraday (5min) open, high, low, close prices and volume"",
                    ""2. Symbol"": ""AAPL""
                },
                ""Time Series (5min)"": {
                    ""2024-01-25 16:00:00"": {
                        ""1. open"": ""152.00"",
                        ""2. high"": ""152.50"",
                        ""3. low"": ""151.80"",
                        ""4. close"": ""152.30"",
                        ""5. volume"": ""1000000""
                    },
                    ""2024-01-25 15:55:00"": {
                        ""1. open"": ""151.50"",
                        ""2. high"": ""152.00"",
                        ""3. low"": ""151.40"",
                        ""4. close"": ""152.00"",
                        ""5. volume"": ""800000""
                    }
                }
            }";

            var mockResponse = new Mock<RestResponse>();
            mockResponse.Setup(x => x.IsSuccessful).Returns(true);
            mockResponse.Setup(x => x.Content).Returns(timeSeriesResponse);
            mockResponse.Setup(x => x.StatusCode).Returns(HttpStatusCode.OK);

            _mockRestClient.Setup(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            var result = await _provider.GetIntradayDataAsync(symbol, interval, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var dataPoints = result.Value;
            dataPoints.Should().HaveCount(2);
            
            var latestPoint = dataPoints[0];
            latestPoint.Symbol.Should().Be("AAPL");
            latestPoint.Open.Should().Be(152.00m);
            latestPoint.High.Should().Be(152.50m);
            latestPoint.Low.Should().Be(151.80m);
            latestPoint.Close.Should().Be(152.30m);
            latestPoint.Volume.Should().Be(1000000);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task GetQuoteAsync_WithInvalidSymbol_ReturnsValidationError(string? invalidSymbol)
        {
            // Act
            var result = await _provider.GetQuoteAsync(invalidSymbol!, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Symbol cannot be empty");
        }

        [Fact]
        public async Task GetQuoteAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            var symbol = "AAPL";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockRestClient.Setup(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _provider.GetQuoteAsync(symbol, cts.Token));
        }

        [Fact]
        public async Task GetQuoteAsync_WithNetworkError_RetriesAndFails()
        {
            // Arrange
            var symbol = "AAPL";
            
            _mockRestClient.Setup(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _provider.GetQuoteAsync(symbol, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Error!.Message.Should().Contain("Network error");

            // Verify retries occurred
            _mockRestClient.Verify(x => x.ExecuteAsync(
                It.IsAny<RestRequest>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3)); // MaxRetries = 3
        }
    }
}