using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Net;

namespace TradingPlatform.Tests.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all end-to-end tests in the trading platform
    /// Provides infrastructure for testing complete user scenarios with full stack
    /// </summary>
    public abstract class CanonicalE2ETestBase<TStartup> : CanonicalIntegrationTestBase<HttpClient>, IClassFixture<WebApplicationFactory<TStartup>>
        where TStartup : class
    {
        protected WebApplicationFactory<TStartup> Factory { get; private set; }
        protected HttpClient ApiClient { get; private set; }
        protected TestServer TestServer { get; private set; }
        
        // Performance tracking
        protected Dictionary<string, List<long>> LatencyMetrics { get; private set; }
        protected Dictionary<string, int> RequestCounts { get; private set; }
        
        protected CanonicalE2ETestBase(
            WebApplicationFactory<TStartup> factory,
            ITestOutputHelper output) : base(output)
        {
            Factory = factory;
            LatencyMetrics = new Dictionary<string, List<long>>();
            RequestCounts = new Dictionary<string, int>();
            
            SetupE2EEnvironment();
        }
        
        /// <summary>
        /// Setup E2E test environment with test server
        /// </summary>
        protected virtual void SetupE2EEnvironment()
        {
            // Configure test server
            Factory = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    ConfigureE2EServices(services);
                });
                
                builder.ConfigureServices(services =>
                {
                    // Add test-specific overrides
                    services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
                });
                
                builder.UseEnvironment("E2ETest");
            });
            
            // Create HTTP client
            ApiClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });
            
            // Get test server for advanced scenarios
            TestServer = Factory.Server;
        }
        
        /// <summary>
        /// Configure services for E2E testing
        /// </summary>
        protected virtual void ConfigureE2EServices(IServiceCollection services)
        {
            // Override external services with test doubles
            services.AddSingleton<IEmailService, TestEmailService>();
            services.AddSingleton<ISmsService, TestSmsService>();
            
            // Add request logging middleware
            services.AddSingleton<PerformanceTrackingMiddleware>();
        }
        
        protected override HttpClient CreateSystemUnderTest()
        {
            return ApiClient;
        }
        
        #region E2E Test Helpers
        
        /// <summary>
        /// Execute a complete user scenario
        /// </summary>
        protected async Task<ScenarioResult> ExecuteScenarioAsync(
            string scenarioName,
            Func<Task> scenario)
        {
            var result = new ScenarioResult { Name = scenarioName };
            var sw = Stopwatch.StartNew();
            
            try
            {
                await scenario();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                Output.WriteLine($"Scenario '{scenarioName}' failed: {ex.Message}");
            }
            finally
            {
                sw.Stop();
                result.Duration = sw.ElapsedMilliseconds;
                result.LatencyMetrics = new Dictionary<string, List<long>>(LatencyMetrics);
                
                Output.WriteLine($"Scenario '{scenarioName}' completed in {sw.ElapsedMilliseconds}ms");
            }
            
            return result;
        }
        
        /// <summary>
        /// Make API request with performance tracking
        /// </summary>
        protected async Task<HttpResponseMessage> MakeRequestAsync(
            HttpMethod method,
            string endpoint,
            object content = null)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var request = new HttpRequestMessage(method, endpoint);
                
                if (content != null)
                {
                    var json = JsonConvert.SerializeObject(content);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                
                var response = await ApiClient.SendAsync(request);
                
                sw.Stop();
                TrackLatency(endpoint, sw.ElapsedMilliseconds);
                
                return response;
            }
            catch
            {
                sw.Stop();
                TrackLatency(endpoint, sw.ElapsedMilliseconds);
                throw;
            }
        }
        
        /// <summary>
        /// Track latency metrics
        /// </summary>
        protected void TrackLatency(string operation, long latencyMs)
        {
            if (!LatencyMetrics.ContainsKey(operation))
            {
                LatencyMetrics[operation] = new List<long>();
            }
            
            LatencyMetrics[operation].Add(latencyMs);
            
            if (!RequestCounts.ContainsKey(operation))
            {
                RequestCounts[operation] = 0;
            }
            
            RequestCounts[operation]++;
        }
        
        /// <summary>
        /// Assert API response is successful
        /// </summary>
        protected async Task AssertSuccessResponseAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatus = HttpStatusCode.OK)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"Response failed: {response.StatusCode} - {content}");
            }
            
            Assert.Equal(expectedStatus, response.StatusCode);
        }
        
        /// <summary>
        /// Get response content as typed object
        /// </summary>
        protected async Task<T> GetResponseContentAsync<T>(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
        
        #endregion
        
        #region Performance E2E Tests
        
        /// <summary>
        /// Assert E2E latency requirements
        /// </summary>
        protected void AssertE2ELatencyRequirements(
            Dictionary<string, int> maxLatencyMs)
        {
            foreach (var requirement in maxLatencyMs)
            {
                if (LatencyMetrics.ContainsKey(requirement.Key))
                {
                    var latencies = LatencyMetrics[requirement.Key];
                    var avgLatency = latencies.Average();
                    var p99Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.99)).FirstOrDefault();
                    
                    Output.WriteLine($"{requirement.Key} - Avg: {avgLatency:F2}ms, P99: {p99Latency}ms, Max allowed: {requirement.Value}ms");
                    
                    Assert.True(avgLatency <= requirement.Value,
                        $"{requirement.Key} average latency {avgLatency:F2}ms exceeds requirement {requirement.Value}ms");
                }
            }
        }
        
        /// <summary>
        /// Run load test scenario
        /// </summary>
        protected async Task<LoadTestResult> RunLoadTestAsync(
            string endpoint,
            int concurrentUsers,
            int requestsPerUser,
            TimeSpan? duration = null)
        {
            var result = new LoadTestResult
            {
                Endpoint = endpoint,
                ConcurrentUsers = concurrentUsers,
                RequestsPerUser = requestsPerUser
            };
            
            var sw = Stopwatch.StartNew();
            var tasks = new List<Task<UserLoadResult>>();
            
            for (int i = 0; i < concurrentUsers; i++)
            {
                var userId = i;
                tasks.Add(Task.Run(async () => await SimulateUserLoadAsync(userId, endpoint, requestsPerUser, duration)));
            }
            
            var userResults = await Task.WhenAll(tasks);
            sw.Stop();
            
            result.TotalDuration = sw.ElapsedMilliseconds;
            result.UserResults = userResults.ToList();
            result.TotalRequests = userResults.Sum(u => u.SuccessfulRequests + u.FailedRequests);
            result.SuccessfulRequests = userResults.Sum(u => u.SuccessfulRequests);
            result.FailedRequests = userResults.Sum(u => u.FailedRequests);
            result.AverageLatency = userResults.SelectMany(u => u.Latencies).DefaultIfEmpty(0).Average();
            result.Throughput = result.TotalRequests * 1000.0 / result.TotalDuration;
            
            Output.WriteLine($"Load test completed: {result.TotalRequests} requests in {result.TotalDuration}ms");
            Output.WriteLine($"Throughput: {result.Throughput:F2} req/s, Success rate: {result.SuccessRate:P}");
            
            return result;
        }
        
        /// <summary>
        /// Simulate single user load
        /// </summary>
        private async Task<UserLoadResult> SimulateUserLoadAsync(
            int userId,
            string endpoint,
            int requestCount,
            TimeSpan? duration)
        {
            var result = new UserLoadResult { UserId = userId };
            var endTime = duration.HasValue ? DateTime.UtcNow + duration.Value : DateTime.MaxValue;
            
            for (int i = 0; i < requestCount && DateTime.UtcNow < endTime; i++)
            {
                try
                {
                    var response = await MakeRequestAsync(HttpMethod.Get, endpoint);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        result.SuccessfulRequests++;
                    }
                    else
                    {
                        result.FailedRequests++;
                    }
                    
                    result.Latencies.Add(GetLastLatency(endpoint));
                }
                catch
                {
                    result.FailedRequests++;
                }
            }
            
            return result;
        }
        
        private long GetLastLatency(string operation)
        {
            return LatencyMetrics.ContainsKey(operation) && LatencyMetrics[operation].Any()
                ? LatencyMetrics[operation].Last()
                : 0;
        }
        
        #endregion
        
        #region WebSocket E2E Tests
        
        /// <summary>
        /// Connect to WebSocket endpoint
        /// </summary>
        protected async Task<TestWebSocketClient> ConnectWebSocketAsync(string path)
        {
            var wsClient = TestServer.CreateWebSocketClient();
            var uri = new Uri(TestServer.BaseAddress, path);
            var ws = await wsClient.ConnectAsync(uri, System.Threading.CancellationToken.None);
            
            return new TestWebSocketClient(ws);
        }
        
        #endregion
        
        public override void Dispose()
        {
            // Output performance summary
            Output.WriteLine("\n=== E2E Test Performance Summary ===");
            foreach (var metric in LatencyMetrics)
            {
                var avg = metric.Value.Average();
                var p99 = metric.Value.OrderBy(l => l).Skip((int)(metric.Value.Count * 0.99)).FirstOrDefault();
                Output.WriteLine($"{metric.Key}: Avg={avg:F2}ms, P99={p99}ms, Count={metric.Value.Count}");
            }
            
            ApiClient?.Dispose();
            base.Dispose();
        }
    }
    
    #region E2E Test Models
    
    public class ScenarioResult
    {
        public string Name { get; set; }
        public bool Success { get; set; }
        public Exception Error { get; set; }
        public long Duration { get; set; }
        public Dictionary<string, List<long>> LatencyMetrics { get; set; }
    }
    
    public class LoadTestResult
    {
        public string Endpoint { get; set; }
        public int ConcurrentUsers { get; set; }
        public int RequestsPerUser { get; set; }
        public long TotalDuration { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double AverageLatency { get; set; }
        public double Throughput { get; set; }
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
        public List<UserLoadResult> UserResults { get; set; } = new();
    }
    
    public class UserLoadResult
    {
        public int UserId { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public List<long> Latencies { get; set; } = new();
    }
    
    public class TestWebSocketClient : IDisposable
    {
        private readonly System.Net.WebSockets.WebSocket _webSocket;
        
        public TestWebSocketClient(System.Net.WebSockets.WebSocket webSocket)
        {
            _webSocket = webSocket;
        }
        
        public async Task SendAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                System.Net.WebSockets.WebSocketMessageType.Text,
                true,
                System.Threading.CancellationToken.None);
        }
        
        public async Task<string> ReceiveAsync()
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var result = await _webSocket.ReceiveAsync(buffer, System.Threading.CancellationToken.None);
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                return Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
            }
            
            return null;
        }
        
        public void Dispose()
        {
            _webSocket?.Dispose();
        }
    }
    
    // Test implementations
    public class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "E2ETest";
        public string ApplicationName { get; set; } = "TradingPlatform.E2ETests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; }
    }
    
    public interface IFileProvider { }
    
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
    
    public class TestEmailService : IEmailService
    {
        public List<(string to, string subject, string body)> SentEmails { get; } = new();
        
        public Task SendEmailAsync(string to, string subject, string body)
        {
            SentEmails.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
    
    public interface ISmsService
    {
        Task SendSmsAsync(string phoneNumber, string message);
    }
    
    public class TestSmsService : ISmsService
    {
        public List<(string phoneNumber, string message)> SentMessages { get; } = new();
        
        public Task SendSmsAsync(string phoneNumber, string message)
        {
            SentMessages.Add((phoneNumber, message));
            return Task.CompletedTask;
        }
    }
    
    public class PerformanceTrackingMiddleware
    {
        // Middleware implementation for tracking performance
    }
    
    #endregion
}