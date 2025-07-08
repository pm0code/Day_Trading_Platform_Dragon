using System.Net;
using System.Text;
using System.Text.Json;
using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Infrastructure.MarketData.Configuration;
using MarketAnalyzer.Infrastructure.MarketData.Models;
using MarketAnalyzer.Infrastructure.MarketData.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace MarketAnalyzer.Infrastructure.MarketData.Tests.Services;

/// <summary>
/// Comprehensive unit tests for FinnhubMarketDataService.
/// Uses MockHttp for HTTP client testing and in-memory cache for caching tests.
/// </summary>
public class FinnhubMarketDataServiceTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly IMemoryCache _cache;
    private readonly IOptions<FinnhubOptions> _options;
    private readonly ILogger<FinnhubMarketDataService> _logger;
    private readonly FinnhubMarketDataService _service;
    private readonly LoggerFactory _loggerFactory;

    public FinnhubMarketDataServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://finnhub.io/api/v1");

        _cache = new MemoryCache(new MemoryCacheOptions());
        
        _options = Options.Create(new FinnhubOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://finnhub.io/api/v1",
            MaxCallsPerMinute = 60,
            TimeoutSeconds = 30,
            QuoteCacheExpirationSeconds = 10,
            CompanyCacheExpirationSeconds = 3600,
            EnableCircuitBreaker = true,
            EnableRetryPolicy = true,
            MaxRetryAttempts = 3
        });

        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<FinnhubMarketDataService>();
        
        _service = new FinnhubMarketDataService(httpClient, _cache, _options, _logger);
    }

    [Fact]
    public async Task GetQuoteAsync_ValidSymbol_ReturnsSuccessResult()
    {
        // Arrange
        var symbol = "AAPL";
        var mockResponse = new FinnhubQuoteResponse
        {
            C = 150.25,
            O = 149.50,
            H = 151.00,
            L = 149.00,
            Pc = 149.75,
            V = 50000000,
            T = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockHttp.When($"https://finnhub.io/api/v1/quote?symbol={symbol}&token=test-api-key")
            .Respond("application/json", JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _service.GetQuoteAsync(symbol);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(symbol, result.Value.Symbol);
        Assert.Equal(150.25m, result.Value.CurrentPrice);
        Assert.Equal(149.50m, result.Value.DayOpen);
        Assert.Equal(151.00m, result.Value.DayHigh);
        Assert.Equal(149.00m, result.Value.DayLow);
        Assert.Equal(149.75m, result.Value.PreviousClose);
        Assert.Equal(50000000L, result.Value.Volume);
    }

    [Fact]
    public async Task GetQuoteAsync_EmptySymbol_ReturnsFailureResult()
    {
        // Act
        var result = await _service.GetQuoteAsync("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SYMBOL", result.Error?.Code);
        Assert.Contains("Symbol cannot be null or empty", result.Error?.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_CachedData_ReturnsCachedResult()
    {
        // Arrange
        var symbol = "MSFT";
        var cachedQuote = new MarketQuote(
            symbol: symbol,
            currentPrice: 300.50m,
            dayOpen: 299.00m,
            dayHigh: 301.00m,
            dayLow: 298.50m,
            previousClose: 299.25m,
            volume: 25000000L,
            timestamp: DateTime.UtcNow,
            hardwareTimestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            marketStatus: MarketStatus.Open
        );

        _cache.Set($"quote_{symbol}", cachedQuote, TimeSpan.FromSeconds(10));

        // Act
        var result = await _service.GetQuoteAsync(symbol);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(symbol, result.Value.Symbol);
        Assert.Equal(300.50m, result.Value.CurrentPrice);
        
        // Verify no HTTP call was made
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetQuoteAsync_HttpError_ReturnsFailureResult()
    {
        // Arrange
        var symbol = "INVALID";
        _mockHttp.When($"https://finnhub.io/api/v1/quote?symbol={symbol}&token=test-api-key")
            .Respond(HttpStatusCode.BadRequest);

        // Act
        var result = await _service.GetQuoteAsync(symbol);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("HTTP_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task GetQuotesAsync_ValidSymbols_ReturnsSuccessResults()
    {
        // Arrange
        var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
        var mockResponses = symbols.Select((symbol, index) => new
        {
            Symbol = symbol,
            Response = new FinnhubQuoteResponse
            {
                C = 150.0 + index,
                O = 149.0 + index,
                H = 151.0 + index,
                L = 148.0 + index,
                Pc = 149.5 + index,
                V = 10000000 + index * 1000000,
                T = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        }).ToArray();

        foreach (var item in mockResponses)
        {
            _mockHttp.When($"https://finnhub.io/api/v1/quote?symbol={item.Symbol}&token=test-api-key")
                .Respond("application/json", JsonSerializer.Serialize(item.Response));
        }

        // Act
        var result = await _service.GetQuotesAsync(symbols);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count());
        
        var quotes = result.Value.ToArray();
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], quotes[i].Symbol);
            Assert.Equal(150.0m + i, quotes[i].CurrentPrice);
        }
    }

    [Fact]
    public async Task GetQuotesAsync_EmptySymbols_ReturnsFailureResult()
    {
        // Act
        var result = await _service.GetQuotesAsync(Array.Empty<string>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SYMBOLS", result.Error?.Code);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_ValidSymbol_ReturnsSuccessResult()
    {
        // Arrange
        var symbol = "AAPL";
        var mockResponse = new FinnhubProfileResponse
        {
            Name = "Apple Inc.",
            Exchange = "NASDAQ",
            FinnhubIndustry = "Technology",
            MarketCapitalization = 2500000000000, // $2.5T
            Country = "US",
            Currency = "USD",
            Ticker = symbol,
            ShareOutstanding = 16000000000
        };

        _mockHttp.When($"https://finnhub.io/api/v1/stock/profile2?symbol={symbol}&token=test-api-key")
            .Respond("application/json", JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _service.GetCompanyProfileAsync(symbol);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(symbol, result.Value.Symbol);
        Assert.Equal("Apple Inc.", result.Value.Name);
        Assert.Equal("NASDAQ", result.Value.Exchange);
        Assert.Equal(MarketCap.MegaCap, result.Value.MarketCap);
        Assert.Equal(Sector.Technology, result.Value.Sector);
    }

    [Fact]
    public async Task GetCompanyProfileAsync_CachedData_ReturnsCachedResult()
    {
        // Arrange
        var symbol = "TSLA";
        var cachedStock = new Stock(
            symbol: symbol,
            exchange: "NASDAQ",
            name: "Tesla, Inc.",
            marketCap: MarketCap.LargeCap,
            sector: Sector.ConsumerDiscretionary,
            industry: "Automotive",
            country: "USA",
            currency: "USD"
        );

        _cache.Set($"profile_{symbol}", cachedStock, TimeSpan.FromHours(1));

        // Act
        var result = await _service.GetCompanyProfileAsync(symbol);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(symbol, result.Value.Symbol);
        Assert.Equal("Tesla, Inc.", result.Value.Name);
        
        // Verify no HTTP call was made
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetHistoricalDataAsync_ValidSymbolAndDates_ReturnsSuccessResult()
    {
        // Arrange
        var symbol = "AAPL";
        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;
        var fromTimestamp = ((DateTimeOffset)fromDate).ToUnixTimeSeconds();
        var toTimestamp = ((DateTimeOffset)toDate).ToUnixTimeSeconds();

        var mockResponse = new FinnhubCandleResponse
        {
            C = new[] { 150.0, 151.0, 152.0 },
            O = new[] { 149.0, 150.0, 151.0 },
            H = new[] { 151.0, 152.0, 153.0 },
            L = new[] { 148.0, 149.0, 150.0 },
            V = new[] { 10000000.0, 11000000.0, 12000000.0 },
            T = new[] { fromTimestamp, fromTimestamp + 86400, fromTimestamp + 172800 },
            S = "ok"
        };

        _mockHttp.When($"https://finnhub.io/api/v1/stock/candle?symbol={symbol}&resolution=1&from={fromTimestamp}&to={toTimestamp}&token=test-api-key")
            .Respond("application/json", JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _service.GetHistoricalDataAsync(symbol, fromDate, toDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count());
        
        var quotes = result.Value.ToArray();
        Assert.Equal(150.0m, quotes[0].CurrentPrice);
        Assert.Equal(151.0m, quotes[1].CurrentPrice);
        Assert.Equal(152.0m, quotes[2].CurrentPrice);
    }

    [Fact]
    public async Task GetHistoricalDataAsync_InvalidDateRange_ReturnsFailureResult()
    {
        // Arrange
        var symbol = "AAPL";
        var fromDate = DateTime.UtcNow;
        var toDate = DateTime.UtcNow.AddDays(-1); // Invalid: from after to

        // Act
        var result = await _service.GetHistoricalDataAsync(symbol, fromDate, toDate);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_DATE_RANGE", result.Error?.Code);
    }

    [Fact]
    public async Task SearchSymbolsAsync_ValidQuery_ReturnsSuccessResult()
    {
        // Arrange
        var query = "apple";
        var mockResponse = new FinnhubSearchResponse
        {
            Count = 2,
            Result = new[]
            {
                new FinnhubSearchResult
                {
                    Description = "Apple Inc.",
                    DisplaySymbol = "AAPL",
                    Symbol = "AAPL",
                    Type = "Common Stock"
                },
                new FinnhubSearchResult
                {
                    Description = "Apple Hospitality REIT Inc.",
                    DisplaySymbol = "APLE",
                    Symbol = "APLE",
                    Type = "REIT"
                }
            }
        };

        _mockHttp.When($"https://finnhub.io/api/v1/search?q={Uri.EscapeDataString(query)}&token=test-api-key")
            .Respond("application/json", JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _service.SearchSymbolsAsync(query);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count());
        
        var stocks = result.Value.ToArray();
        Assert.Equal("AAPL", stocks[0].Symbol);
        Assert.Equal("Apple Inc.", stocks[0].Name);
        Assert.Equal("APLE", stocks[1].Symbol);
        Assert.Equal("Apple Hospitality REIT Inc.", stocks[1].Name);
    }

    [Fact]
    public async Task SearchSymbolsAsync_EmptyQuery_ReturnsFailureResult()
    {
        // Act
        var result = await _service.SearchSymbolsAsync("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUERY", result.Error?.Code);
    }

    [Fact]
    public async Task GetServiceHealthAsync_ServiceHealthy_ReturnsSuccessResult()
    {
        // Arrange
        var mockResponse = new FinnhubQuoteResponse
        {
            C = 150.25,
            O = 149.50,
            H = 151.00,
            L = 149.00,
            Pc = 149.75,
            V = 50000000,
            T = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockHttp.When("https://finnhub.io/api/v1/quote?symbol=AAPL&token=test-api-key")
            .Respond("application/json", JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _service.GetServiceHealthAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task GetServiceHealthAsync_ServiceUnhealthy_ReturnsFailureResult()
    {
        // Arrange
        _mockHttp.When("https://finnhub.io/api/v1/quote?symbol=AAPL&token=test-api-key")
            .Respond(HttpStatusCode.ServiceUnavailable);

        // Act
        var result = await _service.GetServiceHealthAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("HEALTH_CHECK_FAILED", result.Error?.Code);
    }

    // TODO: Implement MarketCap determination testing when DetermineMarketCap method is made testable
    // Test cases would cover: Mega ($200B+), Large ($10B-$200B), Mid ($2B-$10B), 
    // Small ($300M-$2B), Micro ($50M-$300M), Nano (<$50M), Unknown ($0)

    public void Dispose()
    {
        _mockHttp?.Dispose();
        _cache?.Dispose();
        _service?.Dispose();
        _loggerFactory?.Dispose();
    }
}