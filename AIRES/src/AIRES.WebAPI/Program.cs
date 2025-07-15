using AIRES.Foundation;
using AIRES.Core;
using AIRES.Watchdog;
using AIRES.Infrastructure;
using AIRES.Application;
using AIRES.CLI.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("logs", "aires-api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Services.AddSingleton<Serilog.ILogger>(logger);
builder.Host.UseSerilog(logger);

// Configure application
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddIniFile("aires.ini", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "AIRES_")
    .AddCommandLine(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AIRES Health API",
        Version = "v1",
        Description = "AI Error Resolution System - Health Monitoring API",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "AIRES Team"
        }
    });
});

// Register AIRES services
builder.Services.AddAIRESFoundation();
builder.Services.AddAIRESCore();
builder.Services.AddAIRESWatchdog();
builder.Services.AddAIRESInfrastructure();
builder.Services.AddAIRESApplication();

// Register factories for scoped services
builder.Services.AddSingleton<IAIResearchOrchestratorServiceFactory, AIResearchOrchestratorServiceFactory>();
builder.Services.AddSingleton<IBookletPersistenceServiceFactory, BookletPersistenceServiceFactory>();

// Register health check services
builder.Services.AddSingleton<HealthCheckExecutor>();

// Register individual health checks
builder.Services.AddSingleton<IHealthCheck>(sp => 
    new OrchestratorHealthCheck(
        sp.GetRequiredService<IAIResearchOrchestratorServiceFactory>()));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new WatchdogHealthCheck(
        sp.GetRequiredService<AIRES.Watchdog.Services.IFileWatchdogService>()));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new ConfigurationHealthCheck(
        sp.GetRequiredService<AIRES.Core.Configuration.IAIRESConfiguration>()));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new FileSystemHealthCheck(
        sp.GetRequiredService<AIRES.Core.Configuration.IAIRESConfiguration>()));

// Register AI service health checks
builder.Services.AddSingleton<IHealthCheck>(sp => 
    new AIServiceHealthCheck(
        new object(), // Placeholder - actual service checked through IHealthCheckable
        "Mistral Documentation"));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new AIServiceHealthCheck(
        new object(), // Placeholder - actual service checked through IHealthCheckable
        "DeepSeek Context"));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new AIServiceHealthCheck(
        new object(), // Placeholder - actual service checked through IHealthCheckable
        "CodeGemma Pattern"));

builder.Services.AddSingleton<IHealthCheck>(sp => 
    new AIServiceHealthCheck(
        new object(), // Placeholder - actual service checked through IHealthCheckable
        "Gemma2 Booklet"));

// Register Booklet Persistence health check
builder.Services.AddSingleton<IHealthCheck>(sp => 
    new BookletPersistenceHealthCheck(
        sp.GetRequiredService<IBookletPersistenceServiceFactory>()));

// Configure health checks middleware
builder.Services.AddHealthChecks();

// Configure CORS for monitoring tools
builder.Services.AddCors(options =>
{
    options.AddPolicy("HealthMonitoring", policy =>
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

app.UseHttpsRedirection();

app.UseCors("HealthMonitoring");

app.UseAuthorization();

app.MapControllers();

// Add simple root endpoint
app.MapGet("/", () => new
{
    service = "AIRES Health API",
    version = "1.0.0",
    status = "Running",
    endpoints = new[]
    {
        "/api/health - Comprehensive health check",
        "/api/health/live - Liveness probe",
        "/api/health/ready - Readiness probe",
        "/api/health/{componentName} - Component-specific health",
        "/swagger - API documentation"
    }
});

// Log startup
app.Logger.LogInformation("AIRES Health API started on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000");

app.Run();
