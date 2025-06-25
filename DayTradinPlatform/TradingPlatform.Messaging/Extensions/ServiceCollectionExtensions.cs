using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using StackExchange.Redis;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Services;

namespace TradingPlatform.Messaging.Extensions;

/// <summary>
/// Dependency injection extensions for Redis Streams messaging infrastructure
/// Configures high-performance connections optimized for trading applications
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis Streams message bus with optimized configuration for trading
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Redis connection string (e.g., "localhost:6379")</param>
    /// <param name="configureOptions">Optional Redis configuration callback</param>
    public static IServiceCollection AddRedisMessageBus(this IServiceCollection services,
        string connectionString,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Redis connection string cannot be null or empty", nameof(connectionString));
        }

        // Configure Redis with trading-optimized settings
        var configOptions = ConfigurationOptions.Parse(connectionString);

        // Performance optimizations for trading applications
        configOptions.AsyncTimeout = 1000; // 1 second timeout
        configOptions.ConnectTimeout = 5000; // 5 second connect timeout
        configOptions.SyncTimeout = 1000; // 1 second sync timeout
        // ResponseTimeout is obsolete in newer versions of StackExchange.Redis
        configOptions.KeepAlive = 60; // Keep connection alive
        configOptions.ReconnectRetryPolicy = new ExponentialRetry(250); // Fast reconnect
        configOptions.AbortOnConnectFail = false; // Allow retry on initial connect failure

        // Connection pool optimization
        configOptions.ChannelPrefix = RedisChannel.Literal("trading:"); // Namespace for trading channels

        // Apply custom configuration if provided
        configureOptions?.Invoke(configOptions);

        // Register Redis connection as singleton
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var logger = provider.GetRequiredService<ITradingLogger>();

            try
            {
                var connection = ConnectionMultiplexer.Connect(configOptions);

                // Log connection events for monitoring
                connection.ConnectionFailed += (sender, args) =>
                {
                    logger.LogError("Redis connection failed", null, "Redis connection", null, null,
                        new { EndPoint = args.EndPoint?.ToString(), FailureType = args.FailureType.ToString() });
                };

                connection.ConnectionRestored += (sender, args) =>
                {
                    logger.LogInfo("Redis connection restored: {EndPoint}", args.EndPoint);
                };

                connection.ErrorMessage += (sender, args) =>
                {
                    logger.LogError("Redis error occurred", null, "Redis operation", null, null,
                        new { EndPoint = args.EndPoint?.ToString(), Message = args.Message });
                };

                logger.LogInfo("Redis connection established to {EndPoints}",
                    string.Join(", ", connection.GetEndPoints().Select(ep => ep.ToString())));

                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to connect to Redis", ex, "Redis connection", null, null,
                    new { ConnectionString = connectionString });
                throw;
            }
        });

        // Register message bus as singleton for connection reuse
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        
        // Register canonical message queue wrapper
        services.AddSingleton<ICanonicalMessageQueue, CanonicalMessageQueue>();

        return services;
    }

    /// <summary>
    /// Registers Redis Streams message bus for local development (localhost:6379)
    /// </summary>
    public static IServiceCollection AddRedisMessageBusForDevelopment(this IServiceCollection services)
    {
        return services.AddRedisMessageBus("localhost:6379", options =>
        {
            // Development-specific optimizations
            options.ConnectTimeout = 2000; // Shorter timeout for local development
            options.SyncTimeout = 500; // Faster sync for local Redis
            options.AsyncTimeout = 500; // Faster async operations
        });
    }

    /// <summary>
    /// Registers Redis Streams message bus for production with cluster support
    /// </summary>
    public static IServiceCollection AddRedisMessageBusForProduction(this IServiceCollection services,
        string[] clusterEndpoints, string? password = null)
    {
        if (clusterEndpoints == null || clusterEndpoints.Length == 0)
        {
            throw new ArgumentException("Cluster endpoints cannot be null or empty", nameof(clusterEndpoints));
        }

        var connectionString = string.Join(",", clusterEndpoints);

        return services.AddRedisMessageBus(connectionString, options =>
        {
            // Production optimizations
            options.Password = password;
            options.Ssl = true; // Secure connections in production
            options.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            options.ConnectRetry = 3; // Retry connection attempts
            options.CommandMap = CommandMap.Create(new HashSet<string>
            {
                "INFO", "CONFIG", "CLUSTER", "PING", "ECHO", "CLIENT"
            }, available: false); // Disable admin commands for security
        });
    }
}
