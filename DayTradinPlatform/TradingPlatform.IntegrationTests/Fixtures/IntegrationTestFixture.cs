using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.Redis;
using Testcontainers.PostgreSql;
using Xunit;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Messaging.Services;
using TradingPlatform.Testing.Mocks;

namespace TradingPlatform.IntegrationTests.Fixtures
{
    /// <summary>
    /// Base fixture for integration tests providing containerized dependencies
    /// </summary>
    public class IntegrationTestFixture : IAsyncLifetime, IDisposable
    {
        private readonly RedisContainer _redisContainer;
        private readonly PostgreSqlContainer _postgresContainer;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _scope;

        public IServiceProvider ServiceProvider => _scope?.ServiceProvider ?? throw new InvalidOperationException("Fixture not initialized");
        public string RedisConnectionString { get; private set; } = string.Empty;
        public string PostgresConnectionString { get; private set; } = string.Empty;

        public IntegrationTestFixture()
        {
            _redisContainer = new RedisBuilder()
                .WithName($"redis-test-{Guid.NewGuid():N}")
                .WithImage("redis:7-alpine")
                .Build();

            _postgresContainer = new PostgreSqlBuilder()
                .WithName($"postgres-test-{Guid.NewGuid():N}")
                .WithImage("postgres:15-alpine")
                .WithDatabase("tradingplatform_test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();
        }

        public async Task InitializeAsync()
        {
            // Start containers in parallel
            await Task.WhenAll(
                _redisContainer.StartAsync(),
                _postgresContainer.StartAsync()
            );

            RedisConnectionString = _redisContainer.GetConnectionString();
            PostgresConnectionString = _postgresContainer.GetConnectionString();

            // Build service provider
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            _scope = _serviceProvider.CreateScope();

            // Initialize database schema
            await InitializeDatabaseAsync();
        }

        public async Task DisposeAsync()
        {
            _scope?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();

            // Stop containers
            await Task.WhenAll(
                _redisContainer.DisposeAsync().AsTask(),
                _postgresContainer.DisposeAsync().AsTask()
            );
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add trading logger
            services.AddSingleton<ITradingLogger>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TradingLogger>>();
                return new TradingLogger(logger, "IntegrationTests");
            });

            // Add Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = RedisConnectionString;
            });

            // Add message bus
            services.AddSingleton<IMessageBus>(sp =>
            {
                var logger = sp.GetRequiredService<ITradingLogger>();
                return new RedisMessageBus(RedisConnectionString, logger);
            });

            // Add canonical message queue
            services.AddSingleton<ICanonicalMessageQueue, CanonicalMessageQueue>();

            // Add other services as needed by tests
        }

        private async Task InitializeDatabaseAsync()
        {
            // Initialize database schema if needed
            // This would typically run migrations or create tables
            await Task.CompletedTask;
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        public T? GetService<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }
    }

    /// <summary>
    /// Collection definition for tests that share the integration test fixture
    /// </summary>
    [CollectionDefinition("Integration Tests")]
    public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
    {
    }
}