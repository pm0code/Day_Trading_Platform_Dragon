using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.Tests.Core.Canonical;
using Microsoft.Extensions.Caching.Memory;
using TradingPlatform.Core.Logging;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.Tests.E2E.DataIngestion
{
    /// <summary>
    /// End-to-end tests for ApiRateLimiter in complete application scenarios
    /// Tests the rate limiter in realistic trading platform workflows
    /// </summary>
    public class ApiRateLimiterE2ETests : CanonicalE2ETestBase<ApiRateLimiterE2ETests.TestStartup>
    {
        public ApiRateLimiterE2ETests(
            WebApplicationFactory<TestStartup> factory,
            ITestOutputHelper output) : base(factory, output)
        {
        }
        
        #region Market Data Fetching Scenarios
        
        [Fact]
        public async Task E2E_MarketDataFetching_RespectsRateLimits()
        {
            // Scenario: Fetch market data for multiple symbols with rate limiting
            var scenario = await ExecuteScenarioAsync("Market Data Fetching", async () =>
            {
                var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
                var responses = new List<HttpResponseMessage>();
                
                // Attempt to fetch data for all symbols rapidly
                foreach (var symbol in symbols)
                {
                    var response = await MakeRequestAsync(
                        HttpMethod.Get,
                        $"/api/marketdata/quote/{symbol}");
                    responses.Add(response);
                }
                
                // First 5 should succeed (AlphaVantage limit)
                var successCount = responses.Count(r => r.IsSuccessStatusCode);
                Assert.Equal(5, successCount);
                
                // 6th request should be rate limited
                var extraResponse = await MakeRequestAsync(
                    HttpMethod.Get,
                    "/api/marketdata/quote/FB");
                    
                Assert.Equal(HttpStatusCode.TooManyRequests, extraResponse.StatusCode);
                
                // Check rate limit headers
                Assert.True(extraResponse.Headers.Contains("X-RateLimit-Limit"));
                Assert.True(extraResponse.Headers.Contains("X-RateLimit-Remaining"));
                Assert.True(extraResponse.Headers.Contains("X-RateLimit-Reset"));
            });
            
            Assert.True(scenario.Success);
        }
        
        [Fact]
        public async Task E2E_MultiProviderDataAggregation()
        {
            // Scenario: Aggregate data from multiple providers with different rate limits
            var scenario = await ExecuteScenarioAsync("Multi-Provider Aggregation", async () =>
            {
                // Fetch from AlphaVantage (5 rpm limit)
                var alphaVantageTask = FetchFromProviderAsync("alphavantage", 7);
                
                // Fetch from Finnhub (60 rpm limit)
                var finnhubTask = FetchFromProviderAsync("finnhub", 10);
                
                var results = await Task.WhenAll(alphaVantageTask, finnhubTask);
                
                // AlphaVantage should have 5 successes, 2 rate limited
                Assert.Equal(5, results[0].Successes);
                Assert.Equal(2, results[0].RateLimited);
                
                // Finnhub should have all 10 successes
                Assert.Equal(10, results[1].Successes);
                Assert.Equal(0, results[1].RateLimited);
            });
            
            Assert.True(scenario.Success);
        }
        
        private async Task<(int Successes, int RateLimited)> FetchFromProviderAsync(
            string provider, int requestCount)
        {
            var successes = 0;
            var rateLimited = 0;
            
            for (int i = 0; i < requestCount; i++)
            {
                var response = await MakeRequestAsync(
                    HttpMethod.Get,
                    $"/api/marketdata/{provider}/quote/AAPL");
                    
                if (response.IsSuccessStatusCode)
                    successes++;
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    rateLimited++;
            }
            
            return (successes, rateLimited);
        }
        
        #endregion
        
        #region Real-Time Trading Scenarios
        
        [Fact]
        public async Task E2E_HighFrequencyTradingBurst()
        {
            // Scenario: HFT system makes burst of requests during market volatility
            var scenario = await ExecuteScenarioAsync("HFT Burst Trading", async () =>
            {
                const int burstSize = 20;
                const int tradersCount = 5;
                
                // Simulate multiple traders making requests simultaneously
                var traderTasks = Enumerable.Range(0, tradersCount)
                    .Select(traderId => SimulateTraderBurstAsync(traderId, burstSize))
                    .ToArray();
                    
                var results = await Task.WhenAll(traderTasks);
                
                // Verify rate limiting worked correctly
                var totalSuccesses = results.Sum(r => r.Successes);
                var totalRateLimited = results.Sum(r => r.RateLimited);
                
                Output.WriteLine($"Total successes: {totalSuccesses}, Rate limited: {totalRateLimited}");
                
                // Should respect global rate limits
                Assert.True(totalSuccesses <= 60); // Finnhub limit for 1 minute
                Assert.True(totalRateLimited > 0); // Some requests should be rate limited
                
                // Verify fair distribution among traders
                var successRates = results.Select(r => (double)r.Successes / burstSize).ToList();
                var avgSuccessRate = successRates.Average();
                
                // No trader should be starved (within 50% of average)
                Assert.All(successRates, rate => 
                    Assert.True(rate >= avgSuccessRate * 0.5, 
                        $"Trader success rate {rate:P} too low compared to average {avgSuccessRate:P}"));
            });
            
            Assert.True(scenario.Success);
        }
        
        private async Task<(int Successes, int RateLimited, List<long> Latencies)> 
            SimulateTraderBurstAsync(int traderId, int requestCount)
        {
            var successes = 0;
            var rateLimited = 0;
            var latencies = new List<long>();
            
            var tasks = Enumerable.Range(0, requestCount)
                .Select(async i =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await MakeRequestAsync(
                        HttpMethod.Post,
                        "/api/trading/order",
                        new
                        {
                            TraderId = traderId,
                            Symbol = "AAPL",
                            Quantity = 100,
                            Price = 150.00m + (i * 0.01m),
                            OrderType = "LIMIT"
                        });
                    sw.Stop();
                    
                    latencies.Add(sw.ElapsedMilliseconds);
                    
                    if (response.IsSuccessStatusCode)
                        Interlocked.Increment(ref successes);
                    else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        Interlocked.Increment(ref rateLimited);
                        
                    return response;
                });
                
            await Task.WhenAll(tasks);
            
            return (successes, rateLimited, latencies);
        }
        
        #endregion
        
        #region Rate Limit Recovery Scenarios
        
        [Fact]
        public async Task E2E_RateLimitRecoveryWithBackoff()
        {
            // Scenario: System recovers from rate limiting with exponential backoff
            var scenario = await ExecuteScenarioAsync("Rate Limit Recovery", async () =>
            {
                // First, exhaust rate limit
                for (int i = 0; i < 6; i++)
                {
                    await MakeRequestAsync(HttpMethod.Get, "/api/marketdata/quote/AAPL");
                }
                
                // Now test recovery with backoff
                var recoveryAttempts = new List<(int Attempt, bool Success, long WaitTime)>();
                var maxAttempts = 5;
                var baseDelay = 100; // ms
                
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var response = await MakeRequestAsync(
                        HttpMethod.Get, 
                        "/api/marketdata/quote/AAPL");
                        
                    if (response.IsSuccessStatusCode)
                    {
                        recoveryAttempts.Add((attempt, true, 0));
                        break;
                    }
                    else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // Get wait time from headers
                        var retryAfter = response.Headers.RetryAfter?.Delta?.TotalMilliseconds ?? 
                                       (baseDelay * Math.Pow(2, attempt));
                                       
                        recoveryAttempts.Add((attempt, false, (long)retryAfter));
                        
                        // Wait with jitter
                        var jitteredWait = retryAfter * (0.8 + new Random().NextDouble() * 0.4);
                        await Task.Delay(TimeSpan.FromMilliseconds(jitteredWait));
                    }
                }
                
                // Should eventually recover
                Assert.True(recoveryAttempts.Any(r => r.Success), 
                    "System should recover from rate limiting");
                    
                // Wait times should increase (exponential backoff)
                var waitTimes = recoveryAttempts.Where(r => !r.Success).Select(r => r.WaitTime).ToList();
                for (int i = 1; i < waitTimes.Count; i++)
                {
                    Assert.True(waitTimes[i] >= waitTimes[i-1], 
                        "Wait times should increase with exponential backoff");
                }
            });
            
            Assert.True(scenario.Success);
        }
        
        #endregion
        
        #region Performance and Load Testing
        
        [Fact]
        public async Task E2E_LoadTest_SustainedTraffic()
        {
            // Load test: Sustained traffic at 80% of rate limit
            var loadResult = await RunLoadTestAsync(
                "/api/marketdata/quote/AAPL",
                concurrentUsers: 10,
                requestsPerUser: 50,
                duration: TimeSpan.FromSeconds(30));
                
            // Assert performance requirements
            Assert.True(loadResult.SuccessRate > 0.95, 
                $"Success rate {loadResult.SuccessRate:P} below 95% threshold");
                
            Assert.True(loadResult.AverageLatency < 100, 
                $"Average latency {loadResult.AverageLatency}ms exceeds 100ms limit");
                
            // Check throughput is close to but not exceeding rate limit
            var expectedThroughput = 60.0 / 60; // 60 requests per minute = 1 req/s
            Assert.True(loadResult.Throughput <= expectedThroughput * 1.1, 
                $"Throughput {loadResult.Throughput:F2} req/s exceeds rate limit");
        }
        
        [Fact]
        public async Task E2E_LatencyRequirements_UnderLoad()
        {
            // Test latency requirements for different endpoints
            var endpoints = new Dictionary<string, int>
            {
                ["/api/marketdata/quote/AAPL"] = 50,      // 50ms for quotes
                ["/api/marketdata/snapshot"] = 100,       // 100ms for snapshots
                ["/api/trading/order"] = 30,              // 30ms for order submission
                ["/health"] = 10                          // 10ms for health checks
            };
            
            // Warm up
            foreach (var endpoint in endpoints.Keys)
            {
                await MakeRequestAsync(HttpMethod.Get, endpoint);
            }
            
            // Test under load
            var tasks = endpoints.Select(async kvp =>
            {
                var latencies = new List<long>();
                
                for (int i = 0; i < 100; i++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await MakeRequestAsync(HttpMethod.Get, kvp.Key);
                    sw.Stop();
                    latencies.Add(sw.ElapsedMilliseconds);
                }
                
                return (Endpoint: kvp.Key, Latencies: latencies, MaxAllowed: kvp.Value);
            });
            
            var results = await Task.WhenAll(tasks);
            
            // Assert latency requirements
            foreach (var result in results)
            {
                var avgLatency = result.Latencies.Average();
                var p99Latency = result.Latencies.OrderBy(l => l).Skip(98).First();
                
                Output.WriteLine($"{result.Endpoint}: Avg={avgLatency:F2}ms, P99={p99Latency}ms, Max={result.MaxAllowed}ms");
                
                Assert.True(avgLatency <= result.MaxAllowed,
                    $"{result.Endpoint} average latency {avgLatency:F2}ms exceeds limit {result.MaxAllowed}ms");
            }
        }
        
        #endregion
        
        #region WebSocket Real-Time Scenarios
        
        [Fact]
        public async Task E2E_WebSocket_RealTimeDataStream()
        {
            // Scenario: WebSocket connection with rate-limited data updates
            var scenario = await ExecuteScenarioAsync("WebSocket Real-Time Stream", async () =>
            {
                using var ws = await ConnectWebSocketAsync("/ws/marketdata");
                
                // Subscribe to multiple symbols
                var symbols = new[] { "AAPL", "GOOGL", "MSFT" };
                foreach (var symbol in symbols)
                {
                    await ws.SendAsync($"{{\"action\":\"subscribe\",\"symbol\":\"{symbol}\"}}");
                }
                
                // Collect updates for 5 seconds
                var updates = new List<string>();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var update = await ws.ReceiveAsync();
                        if (update != null)
                        {
                            updates.Add(update);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                
                // Verify rate limiting on WebSocket updates
                Output.WriteLine($"Received {updates.Count} updates in 5 seconds");
                
                // Should receive updates but respecting rate limits
                Assert.True(updates.Count > 0, "Should receive some updates");
                Assert.True(updates.Count <= 300, "Updates should be rate limited (60/min = 5/sec * 5 sec * 3 symbols)");
            });
            
            Assert.True(scenario.Success);
        }
        
        #endregion
        
        #region Monitoring and Metrics
        
        [Fact]
        public async Task E2E_Monitoring_RateLimitMetrics()
        {
            // Scenario: Verify rate limit metrics are properly exposed
            var scenario = await ExecuteScenarioAsync("Rate Limit Monitoring", async () =>
            {
                // Generate some traffic
                for (int i = 0; i < 10; i++)
                {
                    await MakeRequestAsync(HttpMethod.Get, "/api/marketdata/quote/AAPL");
                }
                
                // Check metrics endpoint
                var metricsResponse = await MakeRequestAsync(HttpMethod.Get, "/metrics");
                await AssertSuccessResponseAsync(metricsResponse);
                
                var metrics = await metricsResponse.Content.ReadAsStringAsync();
                
                // Verify rate limit metrics are present
                Assert.Contains("rate_limit_requests_total", metrics);
                Assert.Contains("rate_limit_requests_limited_total", metrics);
                Assert.Contains("rate_limit_current_usage", metrics);
                Assert.Contains("rate_limit_remaining_requests", metrics);
                
                // Check admin dashboard
                var dashboardResponse = await MakeRequestAsync(
                    HttpMethod.Get, 
                    "/api/admin/ratelimits");
                await AssertSuccessResponseAsync(dashboardResponse);
                
                var dashboard = await GetResponseContentAsync<RateLimitDashboard>(dashboardResponse);
                
                Assert.NotNull(dashboard);
                Assert.True(dashboard.Providers.Count > 0);
                Assert.All(dashboard.Providers, provider =>
                {
                    Assert.NotNull(provider.Name);
                    Assert.True(provider.Limit > 0);
                    Assert.True(provider.Used >= 0);
                    Assert.True(provider.Used <= provider.Limit);
                });
            });
            
            Assert.True(scenario.Success);
        }
        
        #endregion
        
        #region Test Infrastructure
        
        public class TestStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                // Add MVC
                services.AddControllers();
                
                // Add rate limiting
                services.AddMemoryCache();
                services.AddSingleton<ApiConfiguration>();
                services.AddSingleton<IApiRateLimiter, ApiRateLimiter>();
                services.AddSingleton<ITradingLogger, MockTradingLogger>();
                
                // Add test services
                services.AddSingleton<IMarketDataProvider, MockMarketDataProvider>();
            }
            
            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                app.UseRouting();
                
                // Add rate limiting middleware
                app.UseMiddleware<RateLimitingMiddleware>();
                
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    
                    // Test endpoints
                    endpoints.MapGet("/api/marketdata/quote/{symbol}", async context =>
                    {
                        var rateLimiter = context.RequestServices.GetRequiredService<IApiRateLimiter>();
                        
                        if (!await rateLimiter.CanMakeRequestAsync("alphavantage"))
                        {
                            context.Response.StatusCode = 429;
                            context.Response.Headers.Add("X-RateLimit-Limit", "5");
                            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
                            context.Response.Headers.Add("X-RateLimit-Reset", 
                                DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString());
                            context.Response.Headers.Add("Retry-After", "60");
                            return;
                        }
                        
                        await rateLimiter.RecordRequestAsync("alphavantage");
                        
                        var symbol = context.Request.RouteValues["symbol"]?.ToString();
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Symbol = symbol,
                            Price = 150.25m,
                            Timestamp = DateTime.UtcNow
                        });
                    });
                    
                    endpoints.MapGet("/api/marketdata/{provider}/quote/{symbol}", async context =>
                    {
                        var provider = context.Request.RouteValues["provider"]?.ToString();
                        var rateLimiter = context.RequestServices.GetRequiredService<IApiRateLimiter>();
                        
                        if (!await rateLimiter.CanMakeRequestAsync(provider))
                        {
                            context.Response.StatusCode = 429;
                            return;
                        }
                        
                        await rateLimiter.RecordRequestAsync(provider);
                        await context.Response.WriteAsJsonAsync(new { Success = true });
                    });
                    
                    endpoints.MapPost("/api/trading/order", async context =>
                    {
                        var rateLimiter = context.RequestServices.GetRequiredService<IApiRateLimiter>();
                        
                        if (!await rateLimiter.CanMakeRequestAsync("finnhub"))
                        {
                            context.Response.StatusCode = 429;
                            return;
                        }
                        
                        await rateLimiter.RecordRequestAsync("finnhub");
                        await context.Response.WriteAsJsonAsync(new { OrderId = Guid.NewGuid() });
                    });
                    
                    endpoints.MapGet("/metrics", async context =>
                    {
                        await context.Response.WriteAsync("rate_limit_requests_total 100\n");
                        await context.Response.WriteAsync("rate_limit_requests_limited_total 10\n");
                        await context.Response.WriteAsync("rate_limit_current_usage 5\n");
                        await context.Response.WriteAsync("rate_limit_remaining_requests 55\n");
                    });
                    
                    endpoints.MapGet("/api/admin/ratelimits", async context =>
                    {
                        var rateLimiter = context.RequestServices.GetRequiredService<IApiRateLimiter>();
                        var stats = rateLimiter.GetStatistics();
                        
                        await context.Response.WriteAsJsonAsync(new RateLimitDashboard
                        {
                            Providers = new List<ProviderStatus>
                            {
                                new() { Name = "alphavantage", Limit = 5, Used = stats.CurrentRpm },
                                new() { Name = "finnhub", Limit = 60, Used = 0 }
                            }
                        });
                    });
                    
                    endpoints.MapGet("/health", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { Status = "Healthy" });
                    });
                    
                    endpoints.MapGet("/api/marketdata/snapshot", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { Timestamp = DateTime.UtcNow });
                    });
                });
                
                // WebSocket support
                app.UseWebSockets();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/ws/marketdata")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            // Handle WebSocket connection
                            await Task.Delay(100); // Simulate handling
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        await next();
                    }
                });
            }
        }
        
        public class RateLimitingMiddleware
        {
            private readonly RequestDelegate _next;
            
            public RateLimitingMiddleware(RequestDelegate next)
            {
                _next = next;
            }
            
            public async Task InvokeAsync(HttpContext context)
            {
                // Add rate limit headers to all responses
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Add("X-RateLimit-Policy", "sliding-window");
                    return Task.CompletedTask;
                });
                
                await _next(context);
            }
        }
        
        public class RateLimitDashboard
        {
            public List<ProviderStatus> Providers { get; set; }
        }
        
        public class ProviderStatus
        {
            public string Name { get; set; }
            public int Limit { get; set; }
            public int Used { get; set; }
        }
        
        public class MockTradingLogger : ITradingLogger
        {
            public void LogInfo(string message) { }
            public void LogWarning(string message) { }
            public void LogError(string message, Exception ex = null) { }
            public void LogDebug(string message) { }
            public void LogCritical(string message, Exception ex = null) { }
        }
        
        public class MockMarketDataProvider : IMarketDataProvider
        {
            public string Name => "Mock";
            public Task<MarketData> GetQuoteAsync(string symbol) => 
                Task.FromResult(new MarketData { Symbol = symbol, Price = 100m });
        }
        
        public class MarketData
        {
            public string Symbol { get; set; }
            public decimal Price { get; set; }
        }
        
        #endregion
    }
}