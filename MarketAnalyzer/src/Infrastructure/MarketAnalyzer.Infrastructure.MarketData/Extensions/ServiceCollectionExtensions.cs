using MarketAnalyzer.Infrastructure.MarketData.Configuration;
using MarketAnalyzer.Infrastructure.MarketData.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketAnalyzer.Infrastructure.MarketData.Extensions;

/// <summary>
/// Service collection extensions for registering market data infrastructure services.
/// Follows industry-standard dependency injection patterns.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Finnhub market data services to the service collection.
    /// Configures HttpClient with options pattern and caching.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFinnhubMarketData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure FinnhubOptions using the Options pattern
        services.Configure<FinnhubOptions>(configuration.GetSection(FinnhubOptions.SectionName));

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register HttpClient with basic configuration
        services.AddHttpClient<FinnhubMarketDataService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FinnhubOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            
            // Set default headers
            client.DefaultRequestHeaders.Add("User-Agent", "MarketAnalyzer/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register the market data service
        services.AddScoped<IMarketDataService, FinnhubMarketDataService>();

        return services;
    }

    /// <summary>
    /// Adds Redis caching for market data (optional enhancement).
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">Redis connection string</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisMarketDataCache(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "MarketAnalyzer";
        });

        return services;
    }
}