using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Testing.Mocks;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Common.Constants;

namespace TradingPlatform.Testing.Utilities;

/// <summary>
/// Helper utilities for testing message bus interactions in trading platform components.
/// Provides standardized patterns for setting up, configuring, and verifying message bus behavior.
/// </summary>
public static class MessageBusTestHelpers
{
    /// <summary>
    /// Creates a pre-configured MockMessageBus for standard testing scenarios.
    /// </summary>
    /// <param name="logger">Optional logger for debugging test issues</param>
    /// <returns>Configured MockMessageBus instance</returns>
    public static MockMessageBus CreateStandardMock(ITradingLogger? logger = null)
    {
        var mock = new MockMessageBus(logger);
        mock.SetSimulatedLatency(TimeSpan.FromMicroseconds(50)); // Realistic low latency
        mock.SetHealthStatus(true);
        mock.SetErrorRate(0.0);
        mock.SetMessageCapture(true, 1000);
        return mock;
    }

    /// <summary>
    /// Creates a MockMessageBus configured for high-latency testing scenarios.
    /// </summary>
    public static MockMessageBus CreateHighLatencyMock(TimeSpan latency, ITradingLogger? logger = null)
    {
        var mock = new MockMessageBus(logger);
        mock.SetSimulatedLatency(latency);
        mock.SetHealthStatus(true);
        mock.SetErrorRate(0.0);
        mock.SetMessageCapture(true, 1000);
        return mock;
    }

    /// <summary>
    /// Creates a MockMessageBus configured for error injection testing.
    /// </summary>
    /// <param name="errorRate">Probability (0-1) of operations failing</param>
    public static MockMessageBus CreateErrorInjectingMock(double errorRate, ITradingLogger? logger = null)
    {
        var mock = new MockMessageBus(logger);
        mock.SetSimulatedLatency(TimeSpan.FromMicroseconds(50));
        mock.SetHealthStatus(true);
        mock.SetErrorRate(errorRate);
        mock.SetMessageCapture(true, 1000);
        return mock;
    }

    /// <summary>
    /// Creates a MockMessageBus configured for unhealthy system testing.
    /// </summary>
    public static MockMessageBus CreateUnhealthyMock(ITradingLogger? logger = null)
    {
        var mock = new MockMessageBus(logger);
        mock.SetSimulatedLatency(TimeSpan.FromMilliseconds(100)); // High latency when unhealthy
        mock.SetHealthStatus(false);
        mock.SetErrorRate(0.5); // 50% error rate when unhealthy
        mock.SetMessageCapture(true, 1000);
        return mock;
    }

    /// <summary>
    /// Creates a MockMessageBus optimized for performance testing scenarios.
    /// </summary>
    public static MockMessageBus CreatePerformanceMock(ITradingLogger? logger = null)
    {
        var mock = new MockMessageBus(logger);
        mock.SetSimulatedLatency(TimeSpan.FromMicroseconds(10)); // Ultra-low latency
        mock.SetHealthStatus(true);
        mock.SetErrorRate(0.0);
        mock.SetMessageCapture(false); // Disable capture for pure performance testing
        return mock;
    }

    /// <summary>
    /// Registers a MockMessageBus in the service collection for dependency injection testing.
    /// </summary>
    /// <param name="services">Service collection to register with</param>
    /// <param name="configureAction">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMockMessageBus(
        this IServiceCollection services, 
        Action<MockMessageBus>? configureAction = null)
    {
        return services.AddSingleton<IMessageBus>(provider =>
        {
            var logger = provider.GetService<ITradingLogger>();
            var mock = CreateStandardMock(logger);
            configureAction?.Invoke(mock);
            return mock;
        });
    }

    /// <summary>
    /// Verifies that specific trading messages were published in the correct order.
    /// </summary>
    /// <param name="mockBus">MockMessageBus to verify</param>
    /// <param name="stream">Stream to check</param>
    /// <param name="expectedMessageTypes">Expected message types in order</param>
    /// <returns>True if messages were published in the expected order</returns>
    public static bool VerifyMessageOrder(MockMessageBus mockBus, string stream, params string[] expectedMessageTypes)
    {
        var messages = mockBus.GetPublishedMessages(stream);
        if (messages.Count < expectedMessageTypes.Length)
            return false;

        for (int i = 0; i < expectedMessageTypes.Length; i++)
        {
            if (messages[i].MessageType != expectedMessageTypes[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Verifies that trading-specific message patterns were followed.
    /// </summary>
    /// <param name="mockBus">MockMessageBus to verify</param>
    /// <param name="orderStream">Order stream name</param>
    /// <param name="expectedPattern">Expected trading message pattern</param>
    /// <returns>True if the pattern was followed</returns>
    public static bool VerifyTradingPattern(MockMessageBus mockBus, string orderStream, TradingMessagePattern expectedPattern)
    {
        var messages = mockBus.GetPublishedMessages(orderStream);
        
        return expectedPattern switch
        {
            TradingMessagePattern.OrderSubmissionAcceptance => VerifyMessageOrder(mockBus, orderStream, 
                "OrderSubmitted", "OrderAccepted"),
            
            TradingMessagePattern.OrderSubmissionRejection => VerifyMessageOrder(mockBus, orderStream, 
                "OrderSubmitted", "OrderRejected"),
            
            TradingMessagePattern.OrderFillSequence => VerifyMessageOrder(mockBus, orderStream, 
                "OrderSubmitted", "OrderAccepted", "OrderFilled"),
            
            TradingMessagePattern.OrderCancellationSequence => VerifyMessageOrder(mockBus, orderStream, 
                "OrderSubmitted", "OrderAccepted", "OrderCancelled"),
            
            TradingMessagePattern.RiskLimitBreach => messages.Any(m => m.MessageType.Contains("RiskLimit")),
            
            _ => false
        };
    }

    /// <summary>
    /// Waits for a specific number of messages to be published with timeout.
    /// Useful for asynchronous testing scenarios.
    /// </summary>
    /// <param name="mockBus">MockMessageBus to monitor</param>
    /// <param name="stream">Stream to monitor</param>
    /// <param name="expectedCount">Expected number of messages</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <returns>True if the expected count was reached within timeout</returns>
    public static async Task<bool> WaitForMessagesAsync(
        MockMessageBus mockBus, 
        string stream, 
        int expectedCount, 
        TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (mockBus.GetPublishedMessageCount(stream) >= expectedCount)
                return true;
            
            await Task.Delay(TimeSpan.FromMilliseconds(10)); // Small polling interval
        }
        
        return false;
    }

    /// <summary>
    /// Creates a comprehensive test scenario with multiple conditions.
    /// </summary>
    /// <param name="scenario">Predefined test scenario</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Configured MockMessageBus for the scenario</returns>
    public static MockMessageBus CreateScenarioMock(TestScenario scenario, ITradingLogger? logger = null)
    {
        return scenario switch
        {
            TestScenario.OptimalTrading => CreateStandardMock(logger),
            TestScenario.NetworkLatency => CreateHighLatencyMock(TimeSpan.FromMilliseconds(200), logger),
            TestScenario.SystemDegradation => CreateErrorInjectingMock(0.1, logger), // 10% error rate
            TestScenario.ServiceOutage => CreateUnhealthyMock(logger),
            TestScenario.HighFrequencyTrading => CreatePerformanceMock(logger),
            TestScenario.StressTest => CreateErrorInjectingMock(0.05, logger), // 5% error rate
            _ => CreateStandardMock(logger)
        };
    }

    /// <summary>
    /// Validates that the mock configuration meets trading platform requirements.
    /// </summary>
    /// <param name="mockBus">MockMessageBus to validate</param>
    /// <returns>Validation result with any issues found</returns>
    public static async Task<(bool IsValid, List<string> Issues)> ValidateConfigurationAsync(MockMessageBus mockBus)
    {
        var issues = new List<string>();
        
        // Check latency requirements
        var latency = await mockBus.GetLatencyAsync();
        if (latency > TimeSpan.FromMilliseconds(TradingConstants.PerformanceThresholds.OrderExecutionMaxLatencyMs))
        {
            issues.Add($"Latency {latency.TotalMilliseconds}ms exceeds trading requirements");
        }
        
        // Check health status
        var isHealthy = await mockBus.IsHealthyAsync();
        if (!isHealthy)
        {
            issues.Add("Message bus is not healthy");
        }
        
        return (issues.Count == 0, issues);
    }
}

/// <summary>
/// Predefined trading message patterns for verification.
/// </summary>
public enum TradingMessagePattern
{
    OrderSubmissionAcceptance,
    OrderSubmissionRejection,
    OrderFillSequence,
    OrderCancellationSequence,
    RiskLimitBreach
}

/// <summary>
/// Predefined test scenarios for comprehensive testing.
/// </summary>
public enum TestScenario
{
    OptimalTrading,
    NetworkLatency,
    SystemDegradation,
    ServiceOutage,
    HighFrequencyTrading,
    StressTest
}