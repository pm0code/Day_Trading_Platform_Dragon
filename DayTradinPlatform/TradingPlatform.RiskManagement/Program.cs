using Serilog;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for high-performance logging
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/riskmanagement-.log", 
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
            retainedFileCountLimit: 30));

// Configure Kestrel for high-performance networking
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5004); // HTTP
    options.ListenLocalhost(5014, configure => configure.UseHttps()); // HTTPS
    options.Limits.MaxConcurrentConnections = 2000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TradingPlatform RiskManagement API", Version = "v1" });
});

// Add Redis Streams messaging (temporarily disabled for build)
// TODO: Fix Redis messaging interface and re-enable
builder.Services.AddSingleton<TradingPlatform.Messaging.Interfaces.IMessageBus, MockMessageBus>();

// Add RiskManagement services
builder.Services.AddScoped<IRiskManagementService, RiskManagementService>();
builder.Services.AddScoped<IRiskCalculator, RiskCalculator>();
builder.Services.AddScoped<IPositionMonitor, PositionMonitor>();
builder.Services.AddScoped<IRiskAlertService, RiskAlertService>();
builder.Services.AddScoped<IComplianceMonitor, ComplianceMonitor>();

// Add background services
builder.Services.AddHostedService<RiskMonitoringBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Risk Management API endpoints
app.MapGet("/api/risk/status", async (IRiskManagementService riskService) =>
{
    var status = await riskService.GetRiskStatusAsync();
    return Results.Ok(status);
});

app.MapGet("/api/risk/limits", async (IRiskManagementService riskService) =>
{
    var limits = await riskService.GetRiskLimitsAsync();
    return Results.Ok(limits);
});

app.MapPost("/api/risk/limits", async (IRiskManagementService riskService, RiskLimits limits) =>
{
    await riskService.UpdateRiskLimitsAsync(limits);
    return Results.Ok();
});

app.MapGet("/api/risk/positions", async (IPositionMonitor positionMonitor) =>
{
    var positions = await positionMonitor.GetAllPositionsAsync();
    return Results.Ok(positions);
});

app.MapGet("/api/risk/alerts", async (IRiskAlertService alertService) =>
{
    var alerts = await alertService.GetActiveAlertsAsync();
    return Results.Ok(alerts);
});

app.MapGet("/api/risk/compliance", async (IComplianceMonitor complianceMonitor) =>
{
    var compliance = await complianceMonitor.GetComplianceStatusAsync();
    return Results.Ok(compliance);
});

app.MapControllers();

Log.Information("TradingPlatform.RiskManagement starting on ports 5004 (HTTP) and 5014 (HTTPS)");
app.Run();