using TradingPlatform.Messaging.Interfaces;

namespace TradingPlatform.PaperTrading.Services;

public class MockMessageBus : IMessageBus
{
    public Task<string> PublishAsync<T>(string stream, T message, CancellationToken cancellationToken = default) where T : class
    {
        // Mock implementation - just log
        Console.WriteLine($"Mock publish to {stream}: {message}");
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    public Task SubscribeAsync<T>(string stream, string consumerGroup, string consumerName, Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class
    {
        // Mock implementation - no subscription
        return Task.CompletedTask;
    }

    public Task AcknowledgeAsync(string stream, string consumerGroup, string messageId)
    {
        // Mock implementation
        return Task.CompletedTask;
    }

    public Task<TimeSpan> GetLatencyAsync()
    {
        // Mock implementation - return minimal latency
        return Task.FromResult(TimeSpan.FromMicroseconds(50));
    }

    public Task<bool> IsHealthyAsync()
    {
        // Mock implementation - always healthy
        return Task.FromResult(true);
    }
}