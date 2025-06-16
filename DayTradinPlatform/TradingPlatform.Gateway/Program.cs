using Serilog;
using TradingPlatform.Gateway.Services;
using TradingPlatform.Messaging.Extensions;
using TradingPlatform.Logging.Configuration;
using TradingPlatform.Logging.Interfaces;

// High-performance trading gateway startup with comprehensive logging infrastructure
var builder = WebApplication.CreateBuilder(args);

// Configure comprehensive trading platform logging
builder.Host.ConfigureTradingLogging("Gateway");

// Add Redis Streams messaging for microservices communication
builder.Services.AddRedisMessageBusForDevelopment();

// Add comprehensive trading logging services
builder.Services.AddTradingLogging("Gateway");

// Add Gateway services
builder.Services.AddSingleton<IGatewayOrchestrator, GatewayOrchestrator>();
builder.Services.AddSingleton<IProcessManager, ProcessManager>();
builder.Services.AddSingleton<IHealthMonitor, HealthMonitor>();

// Configure Kestrel for high-performance networking
builder.WebHost.ConfigureKestrel(options =>
{
    // Optimize for local trading workstation
    options.ListenLocalhost(5000); // HTTP
    options.ListenLocalhost(5001, configure => configure.UseHttps()); // HTTPS
    
    // Performance optimizations for trading applications
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MaxRequestBodySize = 1024 * 1024; // 1MB max request
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Configure request pipeline for minimal latency
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Add Serilog request logging with performance metrics
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.IncludeQueryInRequestPath = true;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
    };
});

// Health check endpoint for monitoring
app.MapGet("/health", async (IHealthMonitor healthMonitor) =>
{
    var health = await healthMonitor.GetHealthStatusAsync();
    return health.IsHealthy ? Results.Ok(health) : Results.StatusCode(503);
});

// Market data endpoints
app.MapGet("/api/market-data/{symbol}", async (string symbol, IGatewayOrchestrator orchestrator) =>
{
    var result = await orchestrator.GetMarketDataAsync(symbol);
    return Results.Ok(result);
});

app.MapPost("/api/market-data/subscribe", async (string[] symbols, IGatewayOrchestrator orchestrator) =>
{
    await orchestrator.SubscribeToMarketDataAsync(symbols);
    return Results.Ok(new { Message = "Subscribed to market data", Symbols = symbols });
});

// Order management endpoints
app.MapPost("/api/orders", async (OrderRequest request, IGatewayOrchestrator orchestrator) =>
{
    var result = await orchestrator.SubmitOrderAsync(request);
    return Results.Ok(result);
});

app.MapGet("/api/orders/{orderId}", async (string orderId, IGatewayOrchestrator orchestrator) =>
{
    var result = await orchestrator.GetOrderStatusAsync(orderId);
    return result != null ? Results.Ok(result) : Results.NotFound();
});

// Strategy management endpoints
app.MapGet("/api/strategies", async (IGatewayOrchestrator orchestrator) =>
{
    var strategies = await orchestrator.GetActiveStrategiesAsync();
    return Results.Ok(strategies);
});

app.MapPost("/api/strategies/{strategyId}/start", async (string strategyId, IGatewayOrchestrator orchestrator) =>
{
    await orchestrator.StartStrategyAsync(strategyId);
    return Results.Ok(new { Message = "Strategy started", StrategyId = strategyId });
});

app.MapPost("/api/strategies/{strategyId}/stop", async (string strategyId, IGatewayOrchestrator orchestrator) =>
{
    await orchestrator.StopStrategyAsync(strategyId);
    return Results.Ok(new { Message = "Strategy stopped", StrategyId = strategyId });
});

// Risk management endpoints
app.MapGet("/api/risk/status", async (IGatewayOrchestrator orchestrator) =>
{
    var status = await orchestrator.GetRiskStatusAsync();
    return Results.Ok(status);
});

app.MapGet("/api/risk/limits", async (IGatewayOrchestrator orchestrator) =>
{
    var limits = await orchestrator.GetRiskLimitsAsync();
    return Results.Ok(limits);
});

// Performance metrics endpoint
app.MapGet("/api/metrics", async (IGatewayOrchestrator orchestrator) =>
{
    var metrics = await orchestrator.GetPerformanceMetricsAsync();
    return Results.Ok(metrics);
});

// Process management endpoints (development/monitoring)
app.MapGet("/api/processes", async (IProcessManager processManager) =>
{
    var processes = await processManager.GetProcessStatusAsync();
    return Results.Ok(processes);
});

app.MapPost("/api/processes/{serviceName}/restart", async (string serviceName, IProcessManager processManager) =>
{
    await processManager.RestartServiceAsync(serviceName);
    return Results.Ok(new { Message = "Service restarted", ServiceName = serviceName });
});

// WebSocket endpoint for real-time data streaming
app.MapGet("/ws", async (HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var streamingService = context.RequestServices.GetRequiredService<IGatewayOrchestrator>();
        await streamingService.HandleWebSocketConnectionAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

Log.Information("Trading Platform Gateway starting on ports 5000 (HTTP) and 5001 (HTTPS)");
Log.Information("Gateway optimized for single-user on-premise trading workstation");

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway failed to start");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// DTOs for API endpoints
public record OrderRequest(string Symbol, string OrderType, string Side, decimal Quantity, decimal? Price);
