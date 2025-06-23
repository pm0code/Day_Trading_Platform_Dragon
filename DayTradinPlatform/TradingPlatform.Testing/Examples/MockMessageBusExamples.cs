using TradingPlatform.Core.Interfaces;
using TradingPlatform.Testing.Mocks;
using TradingPlatform.Testing.Utilities;
using TradingPlatform.Common.Constants;

namespace TradingPlatform.Testing.Examples;

/// <summary>
/// Example usage patterns for the standardized MockMessageBus implementation.
/// Demonstrates comprehensive testing approaches for trading platform components.
/// </summary>
public static class MockMessageBusExamples
{
    /// <summary>
    /// Example: Basic message publication and verification.
    /// </summary>
    public static async Task BasicUsageExample()
    {
        // Create a standard mock for testing
        var mockBus = MessageBusTestHelpers.CreateStandardMock();

        // Publish some test messages
        await mockBus.PublishAsync("orders", new { OrderId = "123", Symbol = "AAPL", Quantity = 100 });
        await mockBus.PublishAsync("market-data", new { Symbol = "AAPL", Price = 150.00m });

        // Verify messages were published
        var orderMessages = mockBus.GetPublishedMessages("orders");
        var marketDataMessages = mockBus.GetPublishedMessages("market-data");

        Console.WriteLine($"Published {orderMessages.Count} order messages");
        Console.WriteLine($"Published {marketDataMessages.Count} market data messages");

        // Verify specific message was published
        bool orderPublished = mockBus.WasMessagePublished("orders", "Object");
        Console.WriteLine($"Order message published: {orderPublished}");
    }

    /// <summary>
    /// Example: Testing with error injection to validate resilience.
    /// </summary>
    public static async Task ErrorInjectionExample()
    {
        // Create mock with 20% error rate
        var mockBus = MessageBusTestHelpers.CreateErrorInjectingMock(0.2);

        int successCount = 0;
        int errorCount = 0;

        // Attempt multiple operations to test error handling
        for (int i = 0; i < 100; i++)
        {
            try
            {
                await mockBus.PublishAsync("test-stream", new { Id = i, Data = $"Message {i}" });
                successCount++;
            }
            catch (Exception)
            {
                errorCount++;
            }
        }

        Console.WriteLine($"Successes: {successCount}, Errors: {errorCount}");
        Console.WriteLine($"Error rate: {(double)errorCount / 100:P1}");
    }

    /// <summary>
    /// Example: Performance testing with latency measurement.
    /// </summary>
    public static async Task PerformanceTestingExample()
    {
        // Create high-performance mock for latency testing
        var mockBus = MessageBusTestHelpers.CreatePerformanceMock();

        // Measure latency
        var startTime = DateTime.UtcNow;

        // Publish multiple messages rapidly
        var tasks = new List<Task>();
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(mockBus.PublishAsync("performance-test", new { Id = i }));
        }

        await Task.WhenAll(tasks);

        var endTime = DateTime.UtcNow;
        var totalTime = endTime - startTime;
        var avgLatency = totalTime.TotalMicroseconds / 1000;

        Console.WriteLine($"Published 1000 messages in {totalTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Average latency: {avgLatency:F1} microseconds");

        // Verify latency meets trading requirements
        var reportedLatency = await mockBus.GetLatencyAsync();
        bool meetsPerfRequirements = reportedLatency.TotalMilliseconds <=
            TradingConstants.PerformanceThresholds.OrderExecutionMaxLatencyMs;

        Console.WriteLine($"Reported latency: {reportedLatency.TotalMicroseconds} microseconds");
        Console.WriteLine($"Meets performance requirements: {meetsPerfRequirements}");
    }

    /// <summary>
    /// Example: Testing trading message patterns and sequences.
    /// </summary>
    public static async Task TradingPatternExample()
    {
        var mockBus = MessageBusTestHelpers.CreateStandardMock();

        // Simulate a typical order flow
        await mockBus.PublishAsync("orders", new { Type = "OrderSubmitted", OrderId = "123" });
        await mockBus.PublishAsync("orders", new { Type = "OrderAccepted", OrderId = "123" });
        await mockBus.PublishAsync("orders", new { Type = "OrderFilled", OrderId = "123" });

        // Verify the trading pattern was followed
        bool validPattern = MessageBusTestHelpers.VerifyTradingPattern(
            mockBus,
            "orders",
            TradingMessagePattern.OrderFillSequence);

        Console.WriteLine($"Valid order fill sequence: {validPattern}");

        // Verify message order
        bool correctOrder = MessageBusTestHelpers.VerifyMessageOrder(
            mockBus,
            "orders",
            "Object", "Object", "Object"); // Type names for anonymous objects

        Console.WriteLine($"Messages in correct order: {correctOrder}");
    }

    /// <summary>
    /// Example: Testing unhealthy system scenarios.
    /// </summary>
    public static async Task UnhealthySystemExample()
    {
        var mockBus = MessageBusTestHelpers.CreateUnhealthyMock();

        // Check health status
        bool isHealthy = await mockBus.IsHealthyAsync();
        Console.WriteLine($"System healthy: {isHealthy}");

        // Test operations under degraded conditions
        try
        {
            await mockBus.PublishAsync("orders", new { OrderId = "456", Symbol = "MSFT" });
            Console.WriteLine("Message published despite unhealthy system");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Operation failed as expected: {ex.Message}");
        }

        // Measure degraded performance
        var latency = await mockBus.GetLatencyAsync();
        Console.WriteLine($"Degraded latency: {latency.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Example: Comprehensive scenario testing.
    /// </summary>
    public static async Task ScenarioTestingExample()
    {
        var scenarios = new[]
        {
            TestScenario.OptimalTrading,
            TestScenario.NetworkLatency,
            TestScenario.SystemDegradation,
            TestScenario.HighFrequencyTrading
        };

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"\nTesting scenario: {scenario}");

            var mockBus = MessageBusTestHelpers.CreateScenarioMock(scenario);

            var startTime = DateTime.UtcNow;

            try
            {
                // Attempt standard operations
                await mockBus.PublishAsync("test", new { Scenario = scenario.ToString() });
                await mockBus.SubscribeAsync<object>("test", "group1", "consumer1", async msg => { });

                var latency = await mockBus.GetLatencyAsync();
                var isHealthy = await mockBus.IsHealthyAsync();

                var endTime = DateTime.UtcNow;
                var operationTime = endTime - startTime;

                Console.WriteLine($"  Latency: {latency.TotalMicroseconds:F1} μs");
                Console.WriteLine($"  Healthy: {isHealthy}");
                Console.WriteLine($"  Operation time: {operationTime.TotalMilliseconds:F2} ms");

                // Validate configuration
                var (isValid, issues) = await MessageBusTestHelpers.ValidateConfigurationAsync(mockBus);
                Console.WriteLine($"  Configuration valid: {isValid}");
                if (issues.Any())
                {
                    foreach (var issue in issues)
                    {
                        Console.WriteLine($"    Issue: {issue}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Scenario failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Example: Asynchronous message waiting with timeouts.
    /// </summary>
    public static async Task AsynchronousWaitingExample()
    {
        var mockBus = MessageBusTestHelpers.CreateStandardMock();

        // Start background task that will publish messages
        var publishingTask = Task.Run(async () =>
        {
            await Task.Delay(100); // Simulate some processing time
            await mockBus.PublishAsync("async-stream", new { Id = 1 });
            await Task.Delay(50);
            await mockBus.PublishAsync("async-stream", new { Id = 2 });
            await Task.Delay(50);
            await mockBus.PublishAsync("async-stream", new { Id = 3 });
        });

        // Wait for messages to be published
        bool receivedAllMessages = await MessageBusTestHelpers.WaitForMessagesAsync(
            mockBus,
            "async-stream",
            3,
            TimeSpan.FromSeconds(1));

        Console.WriteLine($"Received all expected messages: {receivedAllMessages}");

        // Wait for publishing task to complete
        await publishingTask;

        var finalCount = mockBus.GetPublishedMessageCount("async-stream");
        Console.WriteLine($"Final message count: {finalCount}");
    }

    /// <summary>
    /// Runs all examples to demonstrate the MockMessageBus capabilities.
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("=== MockMessageBus Examples ===\n");

        Console.WriteLine("1. Basic Usage:");
        await BasicUsageExample();

        Console.WriteLine("\n2. Error Injection:");
        await ErrorInjectionExample();

        Console.WriteLine("\n3. Performance Testing:");
        await PerformanceTestingExample();

        Console.WriteLine("\n4. Trading Patterns:");
        await TradingPatternExample();

        Console.WriteLine("\n5. Unhealthy System:");
        await UnhealthySystemExample();

        Console.WriteLine("\n6. Scenario Testing:");
        await ScenarioTestingExample();

        Console.WriteLine("\n7. Asynchronous Waiting:");
        await AsynchronousWaitingExample();

        Console.WriteLine("\n=== Examples Complete ===");
    }
}