using Serilog;
using TradingPlatform.Messaging.Extensions;
using TradingPlatform.StrategyEngine.Services;
using TradingPlatform.StrategyEngine.Strategies;

// Configure Serilog for high-performance trading strategy logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] StrategyEngine: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/strategyengine-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Configure Kestrel for high-performance strategy execution
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5003); // HTTP for StrategyEngine service
    options.Limits.MaxConcurrentConnections = 1000; // Strategy execution focused
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Redis Streams messaging
builder.Services.AddRedisMessageBusForDevelopment();

// Register StrategyEngine services
builder.Services.AddSingleton<IStrategyExecutionService, StrategyExecutionService>();
builder.Services.AddSingleton<IStrategyManager, StrategyManager>();
builder.Services.AddSingleton<ISignalProcessor, SignalProcessor>();
builder.Services.AddSingleton<IPerformanceTracker, PerformanceTracker>();

// Register trading strategies
builder.Services.AddSingleton<IGoldenRulesStrategy, GoldenRulesStrategy>();
builder.Services.AddSingleton<IMomentumStrategy, MomentumStrategy>();
builder.Services.AddSingleton<IGapStrategy, GapStrategy>();

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
    options.MessageTemplate = "StrategyEngine HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };
});

// Strategy Management API Endpoints

// Get all active strategies
app.MapGet("/api/strategies", async (IStrategyManager strategyManager) =>
{
    var strategies = await strategyManager.GetActiveStrategiesAsync();
    return Results.Ok(strategies);
})
.WithName("GetActiveStrategies")
.WithOpenApi();

// Start a strategy
app.MapPost("/api/strategies/{strategyId}/start", async (string strategyId, IStrategyManager strategyManager) =>
{
    var result = await strategyManager.StartStrategyAsync(strategyId);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("StartStrategy")
.WithOpenApi();

// Stop a strategy
app.MapPost("/api/strategies/{strategyId}/stop", async (string strategyId, IStrategyManager strategyManager) =>
{
    var result = await strategyManager.StopStrategyAsync(strategyId);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("StopStrategy")
.WithOpenApi();

// Get strategy performance
app.MapGet("/api/strategies/{strategyId}/performance", async (string strategyId, IPerformanceTracker performanceTracker) =>
{
    var performance = await performanceTracker.GetStrategyPerformanceAsync(strategyId);
    return performance != null ? Results.Ok(performance) : Results.NotFound($"Performance data not found for strategy {strategyId}");
})
.WithName("GetStrategyPerformance")
.WithOpenApi();

// Get strategy signals
app.MapGet("/api/strategies/{strategyId}/signals", async (string strategyId, ISignalProcessor signalProcessor) =>
{
    var signals = await signalProcessor.GetRecentSignalsAsync(strategyId);
    return Results.Ok(signals);
})
.WithName("GetStrategySignals")
.WithOpenApi();

// Execute manual signal
app.MapPost("/api/signals/execute", async (TradingPlatform.StrategyEngine.Models.SignalRequest request, ISignalProcessor signalProcessor) =>
{
    var result = await signalProcessor.ProcessManualSignalAsync(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("ExecuteManualSignal")
.WithOpenApi();

// Get execution metrics
app.MapGet("/api/metrics", async (IStrategyExecutionService executionService) =>
{
    var metrics = await executionService.GetExecutionMetricsAsync();
    return Results.Ok(metrics);
})
.WithName("GetExecutionMetrics")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", async (IStrategyExecutionService executionService) =>
{
    var health = await executionService.GetHealthStatusAsync();
    return Results.Ok(health);
})
.WithName("HealthCheck")
.WithOpenApi();

// Start background services for Redis Streams processing
var executionService = app.Services.GetRequiredService<IStrategyExecutionService>();
_ = Task.Run(async () => await executionService.StartBackgroundProcessingAsync());

Log.Information("TradingPlatform.StrategyEngine service starting on port 5003");
Log.Information("Strategy engines: Golden Rules, Momentum, Gap Trading");
Log.Information("Redis Streams integration active for real-time strategy execution");

app.Run();