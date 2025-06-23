using Serilog;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.RiskManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for high-performance logging
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/papertrading-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
            retainedFileCountLimit: 30));

// Configure Kestrel for high-performance networking
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5005); // HTTP
    options.ListenLocalhost(5015, configure => configure.UseHttps()); // HTTPS
    options.Limits.MaxConcurrentConnections = 3000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TradingPlatform PaperTrading API", Version = "v1" });
});

// Add messaging and risk management
builder.Services.AddSingleton<TradingPlatform.Messaging.Interfaces.IMessageBus, TradingPlatform.PaperTrading.Services.MockMessageBus>();

// Add PaperTrading services
builder.Services.AddScoped<IPaperTradingService, PaperTradingService>();
builder.Services.AddScoped<IOrderExecutionEngine, OrderExecutionEngine>();
builder.Services.AddScoped<IPortfolioManager, PortfolioManager>();
builder.Services.AddScoped<IOrderBookSimulator, OrderBookSimulator>();
builder.Services.AddScoped<IExecutionAnalytics, TradingPlatform.PaperTrading.Services.ExecutionAnalytics>();
builder.Services.AddScoped<ISlippageCalculator, SlippageCalculator>();

// Add background services
builder.Services.AddHostedService<OrderProcessingBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Paper Trading API endpoints
app.MapPost("/api/orders", async (IPaperTradingService tradingService, OrderRequest orderRequest) =>
{
    var result = await tradingService.SubmitOrderAsync(orderRequest);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/orders", async (IPaperTradingService tradingService) =>
{
    var orders = await tradingService.GetOrdersAsync();
    return Results.Ok(orders);
});

app.MapGet("/api/orders/{orderId}", async (IPaperTradingService tradingService, string orderId) =>
{
    var order = await tradingService.GetOrderAsync(orderId);
    return order != null ? Results.Ok(order) : Results.NotFound();
});

app.MapDelete("/api/orders/{orderId}", async (IPaperTradingService tradingService, string orderId) =>
{
    var result = await tradingService.CancelOrderAsync(orderId);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/portfolio", async (IPortfolioManager portfolioManager) =>
{
    var portfolio = await portfolioManager.GetPortfolioAsync();
    return Results.Ok(portfolio);
});

app.MapGet("/api/portfolio/positions", async (IPortfolioManager portfolioManager) =>
{
    var positions = await portfolioManager.GetPositionsAsync();
    return Results.Ok(positions);
});

app.MapGet("/api/portfolio/performance", async (IExecutionAnalytics analytics) =>
{
    var performance = await analytics.GetPerformanceMetricsAsync();
    return Results.Ok(performance);
});

app.MapGet("/api/executions", async (IExecutionAnalytics analytics) =>
{
    var executions = await analytics.GetExecutionHistoryAsync();
    return Results.Ok(executions);
});

app.MapGet("/api/executions/analytics", async (IExecutionAnalytics analytics) =>
{
    var analyticsData = await analytics.GetExecutionAnalyticsAsync();
    return Results.Ok(analyticsData);
});

app.MapControllers();

Log.Information("TradingPlatform.PaperTrading starting on ports 5005 (HTTP) and 5015 (HTTPS)");
app.Run();