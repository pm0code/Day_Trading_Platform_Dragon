// File: TradingPlatform.Core\Observability\OpenTelemetryInstrumentation.cs

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

namespace TradingPlatform.Core.Observability;

/// <summary>
/// Universal OpenTelemetry instrumentation for zero-blind-spot observability
/// Provides microsecond-precision tracing and metrics for ultra-low latency trading
/// </summary>
public static class OpenTelemetryInstrumentation
{
    public const string ServiceName = "TradingPlatform";
    public const string ServiceVersion = "1.0.0";

    // Activity sources for different platform areas
    public static readonly ActivitySource TradingActivitySource = new("TradingPlatform.Trading");
    public static readonly ActivitySource RiskActivitySource = new("TradingPlatform.Risk");
    public static readonly ActivitySource MarketDataActivitySource = new("TradingPlatform.MarketData");
    public static readonly ActivitySource FixEngineActivitySource = new("TradingPlatform.FixEngine");
    public static readonly ActivitySource InfrastructureActivitySource = new("TradingPlatform.Infrastructure");

    /// <summary>
    /// Configures OpenTelemetry with comprehensive instrumentation for all platform components
    /// </summary>
    public static IServiceCollection AddTradingPlatformObservability(this IServiceCollection services)
    {
        // Configure OpenTelemetry with comprehensive tracing and metrics
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development",
                    ["trading.platform.version"] = ServiceVersion,
                    ["trading.latency.target"] = "100μs",
                    ["trading.compliance.required"] = true
                }))
            .WithTracing(tracing => tracing
                // Add all activity sources for comprehensive coverage
                .AddSource(TradingActivitySource.Name)
                .AddSource(RiskActivitySource.Name)
                .AddSource(MarketDataActivitySource.Name)
                .AddSource(FixEngineActivitySource.Name)
                .AddSource(InfrastructureActivitySource.Name)

                // Add automatic instrumentation for infrastructure components
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = EnrichHttpRequest;
                    options.EnrichWithHttpResponse = EnrichHttpResponse;
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequestMessage = EnrichHttpRequestMessage;
                    options.EnrichWithHttpResponseMessage = EnrichHttpResponseMessage;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.SetDbStatementForStoredProcedure = true;
                    options.EnrichWithIDbCommand = EnrichDatabaseCommand;
                })

                // Export to Jaeger for distributed tracing (free)
                .AddJaegerExporter(options =>
                {
                    options.AgentHost = Environment.GetEnvironmentVariable("JAEGER_AGENT_HOST") ?? "localhost";
                    options.AgentPort = int.Parse(Environment.GetEnvironmentVariable("JAEGER_AGENT_PORT") ?? "6831");
                }))
            .WithMetrics(metrics => metrics
                // Add meters for comprehensive metrics collection
                .AddMeter("TradingPlatform.*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()

                // Export to Prometheus (free)
                .AddPrometheusExporter());

        // Register custom services
        services.AddSingleton<ITradingMetrics, TradingMetrics>();
        services.AddSingleton<IInfrastructureMetrics, InfrastructureMetrics>();
        services.AddSingleton<IObservabilityEnricher, ObservabilityEnricher>();

        return services;
    }

    /// <summary>
    /// Enriches HTTP request traces with trading-specific context
    /// </summary>
    private static void EnrichHttpRequest(Activity activity, HttpRequest request)
    {
        activity.SetTag("http.request.correlation_id", request.Headers["X-Correlation-ID"].FirstOrDefault());
        activity.SetTag("http.request.trading_session", request.Headers["X-Trading-Session"].FirstOrDefault());
        activity.SetTag("http.request.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
        activity.SetTag("trading.request.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Enriches HTTP response traces with performance metrics
    /// </summary>
    private static void EnrichHttpResponse(Activity activity, HttpResponse response)
    {
        activity.SetTag("http.response.content_length", response.ContentLength?.ToString());
        activity.SetTag("trading.response.timestamp", DateTimeOffset.UtcNow.ToString("O"));

        // Calculate and record response time
        if (activity.StartTimeUtc != default)
        {
            var responseTime = DateTimeOffset.UtcNow - activity.StartTimeUtc;
            activity.SetTag("trading.response.time_microseconds", responseTime.TotalMicroseconds.ToString("F2"));

            // Flag latency violations for trading systems
            if (responseTime.TotalMicroseconds > 100)
            {
                activity.SetTag("trading.latency.violation", true);
                activity.SetTag("trading.latency.severity", responseTime.TotalMicroseconds > 1000 ? "critical" : "warning");
            }
        }
    }

    /// <summary>
    /// Enriches HTTP client request traces
    /// </summary>
    private static void EnrichHttpRequestMessage(Activity activity, HttpRequestMessage request)
    {
        activity.SetTag("http.client.correlation_id", request.Headers.GetValues("X-Correlation-ID").FirstOrDefault());
        activity.SetTag("trading.client.request.timestamp", DateTimeOffset.UtcNow.ToString("O"));

        // Identify market data provider requests
        if (request.RequestUri?.Host.Contains("alphavantage") == true)
        {
            activity.SetTag("trading.data_provider", "AlphaVantage");
        }
        else if (request.RequestUri?.Host.Contains("finnhub") == true)
        {
            activity.SetTag("trading.data_provider", "Finnhub");
        }
    }

    /// <summary>
    /// Enriches HTTP client response traces
    /// </summary>
    private static void EnrichHttpResponseMessage(Activity activity, HttpResponseMessage response)
    {
        activity.SetTag("http.client.response.content_length", response.Content.Headers.ContentLength?.ToString());
        activity.SetTag("trading.client.response.timestamp", DateTimeOffset.UtcNow.ToString("O"));

        // Track API rate limiting
        if (response.Headers.Contains("X-RateLimit-Remaining"))
        {
            activity.SetTag("trading.rate_limit.remaining", response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault());
        }
    }

    /// <summary>
    /// Enriches database command traces with trading context
    /// </summary>
    private static void EnrichDatabaseCommand(Activity activity, System.Data.IDbCommand command)
    {
        activity.SetTag("db.operation.timestamp", DateTimeOffset.UtcNow.ToString("O"));

        // Identify trading-specific table operations
        var commandText = command.CommandText?.ToLowerInvariant();
        if (commandText?.Contains("orders") == true)
        {
            activity.SetTag("trading.db.table_type", "orders");
        }
        else if (commandText?.Contains("positions") == true)
        {
            activity.SetTag("trading.db.table_type", "positions");
        }
        else if (commandText?.Contains("market_data") == true)
        {
            activity.SetTag("trading.db.table_type", "market_data");
        }

        // Flag potentially slow queries
        if (command.CommandTimeout > 5)
        {
            activity.SetTag("trading.db.slow_query_risk", true);
        }
    }
}

/// <summary>
/// Interface for trading-specific metrics collection
/// </summary>
public interface ITradingMetrics
{
    void RecordOrderExecution(TimeSpan latency, string symbol, decimal quantity);
    void RecordMarketDataTick(string symbol, TimeSpan processingLatency);
    void RecordRiskViolation(string violationType, string severity);
    void RecordFixMessageProcessing(string messageType, TimeSpan processingTime);
}

/// <summary>
/// Interface for infrastructure metrics collection
/// </summary>
public interface IInfrastructureMetrics
{
    void RecordMemoryAllocation(long bytes, string context);
    void RecordNetworkLatency(TimeSpan latency, string destination);
    void RecordDiskIO(string operation, TimeSpan latency, long bytes);
    void RecordCpuUtilization(double percentage);
}

/// <summary>
/// Interface for observability context enrichment
/// </summary>
public interface IObservabilityEnricher
{
    void EnrichActivity(Activity activity, string context, object? data = null);
    string GenerateCorrelationId();
    void SetTradingContext(string sessionId, string accountId, string userId);
}