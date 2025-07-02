using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.DataIngestion.Interfaces;
using System.Collections.Generic;

namespace TradingPlatform.Tests.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all integration tests in the trading platform
    /// Provides infrastructure for testing with real dependencies (database, external services)
    /// </summary>
    public abstract class CanonicalIntegrationTestBase<TService> : CanonicalTestBase<TService>
        where TService : class
    {
        protected IConfiguration Configuration { get; private set; }
        protected string TestDatabaseName { get; private set; }
        protected Dictionary<string, string> TestApiKeys { get; private set; }
        
        // Service host for integration testing
        protected IServiceProvider IntegrationServiceProvider { get; private set; }
        
        protected CanonicalIntegrationTestBase(ITestOutputHelper output) : base(output)
        {
            TestDatabaseName = $"TradingPlatformTest_{Guid.NewGuid():N}";
            TestApiKeys = new Dictionary<string, string>();
            SetupIntegrationEnvironment();
        }
        
        /// <summary>
        /// Setup integration test environment with real dependencies
        /// </summary>
        protected virtual void SetupIntegrationEnvironment()
        {
            // Build configuration for integration tests
            var configBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:TradingDb"] = GetTestConnectionString(),
                    ["Logging:LogLevel:Default"] = "Debug",
                    ["ApiKeys:AlphaVantage"] = GetTestApiKey("AlphaVantage"),
                    ["ApiKeys:Finnhub"] = GetTestApiKey("Finnhub"),
                    ["RateLimits:AlphaVantage:CallsPerMinute"] = "5",
                    ["RateLimits:Finnhub:CallsPerMinute"] = "60",
                    ["Cache:SlidingExpiration"] = "00:05:00",
                    ["Performance:LatencyTargetMs"] = "50"
                })
                .AddEnvironmentVariables("TRADING_TEST_");
                
            Configuration = configBuilder.Build();
        }
        
        /// <summary>
        /// Configure services for integration testing
        /// </summary>
        protected override void ConfigureCommonServices()
        {
            base.ConfigureCommonServices();
            
            // Add configuration
            Services.AddSingleton(Configuration);
            
            // Add real database context (in-memory for testing)
            Services.AddDbContext<TradingDbContext>(options =>
                options.UseInMemoryDatabase(TestDatabaseName));
                
            // Add real caching
            Services.AddDistributedMemoryCache();
            
            // Add real HTTP client
            Services.AddHttpClient();
            
            // Configure real services with test configuration
            ConfigureIntegrationServices(Services);
        }
        
        /// <summary>
        /// Configure additional services for integration testing
        /// </summary>
        protected virtual void ConfigureIntegrationServices(IServiceCollection services)
        {
            // Override in derived classes to add specific integration services
        }
        
        /// <summary>
        /// Get test connection string
        /// </summary>
        protected virtual string GetTestConnectionString()
        {
            // For integration tests, use in-memory database
            return $"DataSource=:memory:";
        }
        
        /// <summary>
        /// Get test API key (can be mocked or real test keys)
        /// </summary>
        protected virtual string GetTestApiKey(string provider)
        {
            // Return test API keys (never use production keys in tests)
            return TestApiKeys.ContainsKey(provider) 
                ? TestApiKeys[provider] 
                : $"TEST_API_KEY_{provider.ToUpper()}";
        }
        
        #region Integration Test Helpers
        
        /// <summary>
        /// Setup test database with seed data
        /// </summary>
        protected async Task SetupTestDatabaseAsync()
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            
            // Ensure database is created
            await dbContext.Database.EnsureCreatedAsync();
            
            // Seed test data
            await SeedTestDataAsync(dbContext);
        }
        
        /// <summary>
        /// Seed test data into database
        /// </summary>
        protected virtual async Task SeedTestDataAsync(TradingDbContext dbContext)
        {
            // Override in derived classes to add test data
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Test with real external service (with timeout)
        /// </summary>
        protected async Task<T> TestWithExternalServiceAsync<T>(
            Func<Task<T>> testAction, 
            int timeoutSeconds = 30)
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            
            try
            {
                return await testAction();
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"External service test timed out after {timeoutSeconds} seconds");
            }
        }
        
        /// <summary>
        /// Test with database transaction rollback
        /// </summary>
        protected async Task TestWithRollbackAsync(Func<TradingDbContext, Task> testAction)
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                await testAction(dbContext);
                // Always rollback to keep test database clean
                await transaction.RollbackAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        
        /// <summary>
        /// Verify service integration health
        /// </summary>
        protected async Task<bool> VerifyServiceHealthAsync<THealthService>()
            where THealthService : IHealthCheckService
        {
            var healthService = ServiceProvider.GetRequiredService<THealthService>();
            var health = await healthService.CheckHealthAsync();
            
            Output.WriteLine($"Service health: {health.Status}");
            foreach (var check in health.Checks)
            {
                Output.WriteLine($"  {check.Key}: {check.Value.Status} - {check.Value.Description}");
            }
            
            return health.Status == HealthStatus.Healthy;
        }
        
        /// <summary>
        /// Test with retry for flaky external services
        /// </summary>
        protected async Task<T> TestWithRetryAsync<T>(
            Func<Task<T>> testAction, 
            int maxRetries = 3,
            int delayMs = 1000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await testAction();
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    Output.WriteLine($"Retry {i + 1}/{maxRetries} after error: {ex.Message}");
                    await Task.Delay(delayMs * (i + 1)); // Exponential backoff
                }
            }
            
            // Last attempt will throw if it fails
            return await testAction();
        }
        
        #endregion
        
        #region Performance Integration Tests
        
        /// <summary>
        /// Test latency against real services
        /// </summary>
        protected async Task AssertLatencyRequirementAsync(
            Func<Task> operation,
            int maxLatencyMs = 100)
        {
            // Warm up
            await operation();
            
            // Measure
            var latencies = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                var latency = await MeasureExecutionTimeAsync(operation);
                latencies.Add(latency);
            }
            
            var avgLatency = latencies.Average();
            var p99Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.99)).FirstOrDefault();
            
            Output.WriteLine($"Latency - Avg: {avgLatency}ms, P99: {p99Latency}ms");
            
            Assert.True(avgLatency <= maxLatencyMs, 
                $"Average latency {avgLatency}ms exceeds requirement {maxLatencyMs}ms");
        }
        
        #endregion
        
        public override void Dispose()
        {
            // Clean up test database
            using (var scope = ServiceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetService<TradingDbContext>();
                dbContext?.Database.EnsureDeleted();
            }
            
            base.Dispose();
        }
    }
    
    // Mock interfaces for integration testing
    public interface IHealthCheckService
    {
        Task<HealthCheckResult> CheckHealthAsync();
    }
    
    public class HealthCheckResult
    {
        public HealthStatus Status { get; set; }
        public Dictionary<string, HealthCheckEntry> Checks { get; set; } = new();
    }
    
    public class HealthCheckEntry
    {
        public HealthStatus Status { get; set; }
        public string Description { get; set; }
    }
    
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
    
    // Mock DbContext for testing
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options) { }
        
        // Add DbSets as needed
        public DbSet<Order> Orders { get; set; }
        public DbSet<Trade> Trades { get; set; }
    }
    
    public class Order
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
    
    public class Trade
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public decimal ExecutionPrice { get; set; }
        public DateTime ExecutionTime { get; set; }
    }
}