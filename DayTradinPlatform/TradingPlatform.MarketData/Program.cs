using Serilog;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Configuration;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.DataIngestion.Services;
using TradingPlatform.Messaging.Extensions;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.MarketData.Services;

// Configure Serilog for high-performance trading logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] MarketData: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/marketdata-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Configure Kestrel for high-performance market data serving
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5002); // HTTP for MarketData service
    options.Limits.MaxConcurrentConnections = 2000; // Higher for market data
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
});

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Redis Streams messaging
builder.Services.AddRedisMessaging(builder.Configuration);

// Add market data providers and services
builder.Services.Configure<DataIngestionConfig>(builder.Configuration.GetSection("DataIngestion"));
builder.Services.Configure<FinnhubConfiguration>(builder.Configuration.GetSection("Finnhub"));

// Register market data services
builder.Services.AddSingleton<IAlphaVantageProvider, AlphaVantageProvider>();
builder.Services.AddSingleton<IFinnhubProvider, FinnhubProvider>();
builder.Services.AddSingleton<IMarketDataAggregator, MarketDataAggregator>();
builder.Services.AddSingleton<IDataIngestionService, DataIngestionService>();

// Register MarketData-specific services
builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
builder.Services.AddSingleton<IMarketDataCache, MarketDataCache>();
builder.Services.AddSingleton<ISubscriptionManager, SubscriptionManager>();

// Add CORS for cross-service communication
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "MarketData HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };
});

// Market Data API Endpoints

// Real-time market data endpoint
app.MapGet("/api/market-data/{symbol}", async (string symbol, IMarketDataService marketDataService) =>
{
    var data = await marketDataService.GetMarketDataAsync(symbol);
    return data != null ? Results.Ok(data) : Results.NotFound($"Market data not found for {symbol}");
})
.WithName("GetMarketData")
.WithOpenApi();

// Multiple symbols endpoint for batch requests
app.MapPost("/api/market-data/batch", async (string[] symbols, IMarketDataService marketDataService) =>
{
    var results = await marketDataService.GetMarketDataBatchAsync(symbols);
    return Results.Ok(results);
})
.WithName("GetMarketDataBatch")
.WithOpenApi();

// Subscription management endpoints
app.MapPost("/api/subscriptions/{symbol}", async (string symbol, ISubscriptionManager subscriptionManager) =>
{
    await subscriptionManager.SubscribeAsync(symbol);
    return Results.Ok($"Subscribed to {symbol}");
})
.WithName("Subscribe")
.WithOpenApi();

app.MapDelete("/api/subscriptions/{symbol}", async (string symbol, ISubscriptionManager subscriptionManager) =>
{
    await subscriptionManager.UnsubscribeAsync(symbol);
    return Results.Ok($"Unsubscribed from {symbol}");
})
.WithName("Unsubscribe")
.WithOpenApi();

app.MapGet("/api/subscriptions", async (ISubscriptionManager subscriptionManager) =>
{
    var subscriptions = await subscriptionManager.GetActiveSubscriptionsAsync();
    return Results.Ok(subscriptions);
})
.WithName("GetSubscriptions")
.WithOpenApi();

// Historical data endpoint
app.MapGet("/api/historical/{symbol}", async (string symbol, string? interval, IMarketDataService marketDataService) =>
{
    var data = await marketDataService.GetHistoricalDataAsync(symbol, interval ?? "1d");
    return data != null ? Results.Ok(data) : Results.NotFound($"Historical data not found for {symbol}");
})
.WithName("GetHistoricalData")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", async (IMarketDataService marketDataService) =>
{
    var health = await marketDataService.GetHealthStatusAsync();
    return Results.Ok(health);
})
.WithName("HealthCheck")
.WithOpenApi();

// Performance metrics endpoint
app.MapGet("/api/metrics", async (IMarketDataService marketDataService) =>
{
    var metrics = await marketDataService.GetPerformanceMetricsAsync();
    return Results.Ok(metrics);
})
.WithName("GetMetrics")
.WithOpenApi();

// Start background services for Redis Streams processing
var marketDataService = app.Services.GetRequiredService<IMarketDataService>();
_ = Task.Run(async () => await marketDataService.StartBackgroundProcessingAsync());

Log.Information("TradingPlatform.MarketData service starting on port 5002");
Log.Information("Market data providers: AlphaVantage, Finnhub");
Log.Information("Redis Streams integration active for real-time distribution");

app.Run();