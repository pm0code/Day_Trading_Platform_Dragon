using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using TradingPlatform.ChaosTests.Framework;
using TradingPlatform.Core.Models;
using TradingPlatform.Messaging.Services;
using TradingPlatform.Messaging.Events;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ChaosTests.Scenarios
{
    [Collection("Chaos Tests")]
    public class MessageQueueChaosTests : ChaosTestBase
    {
        private readonly ICanonicalMessageQueue _messageQueue;

        public MessageQueueChaosTests(ITestOutputHelper output) : base(output)
        {
            _messageQueue = GetRequiredService<ICanonicalMessageQueue>();
        }

        [Fact]
        public async Task MessageQueue_WithNetworkPartition_RecoversAndDeliversMessages()
        {
            // Arrange
            var receivedMessages = new List<string>();
            var messageCount = 100;
            var partitionDuration = TimeSpan.FromSeconds(5);

            // Subscribe to messages
            var subscriptionTask = _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "chaos-test-stream",
                "chaos-consumer",
                async (message) =>
                {
                    if (message.ContainsKey("id"))
                    {
                        receivedMessages.Add(message["id"].ToString()!);
                    }
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "chaos-test" },
                CancellationToken.None);

            await Task.Delay(1000); // Let subscription start

            // Act - Send messages before, during, and after partition
            var sendTasks = new List<Task>();

            // Phase 1: Send messages normally
            for (int i = 0; i < messageCount / 3; i++)
            {
                sendTasks.Add(SendMessageWithRetry(i.ToString()));
            }

            await Task.WhenAll(sendTasks);
            sendTasks.Clear();

            // Phase 2: Simulate network partition
            var partitionTask = SimulateNetworkPartition(partitionDuration);

            // Try to send messages during partition
            for (int i = messageCount / 3; i < (2 * messageCount / 3); i++)
            {
                sendTasks.Add(SendMessageWithRetry(i.ToString()));
            }

            await partitionTask;
            await Task.WhenAll(sendTasks);
            sendTasks.Clear();

            // Phase 3: Send messages after recovery
            for (int i = (2 * messageCount / 3); i < messageCount; i++)
            {
                sendTasks.Add(SendMessageWithRetry(i.ToString()));
            }

            await Task.WhenAll(sendTasks);

            // Wait for all messages to be processed
            await Task.Delay(5000);

            // Assert - All messages should eventually be delivered
            receivedMessages.Should().HaveCountGreaterOrEqualTo(messageCount * 0.9); // Allow 10% loss
            Output.WriteLine($"Received {receivedMessages.Count} out of {messageCount} messages");
        }

        [Fact]
        public async Task MessageQueue_WithHighLatency_MaintainsOrdering()
        {
            // Arrange
            var receivedMessages = new List<int>();
            var messageCount = 50;
            var latencyPolicy = CreateLatencyChaosPolicy<TradingResult<string>>(
                injectionRate: 0.3, // 30% of operations
                latency: TimeSpan.FromMilliseconds(500));

            // Subscribe with ordering check
            var subscriptionTask = _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "order-test-stream",
                "order-consumer",
                async (message) =>
                {
                    if (message.ContainsKey("sequence"))
                    {
                        var sequence = Convert.ToInt32(message["sequence"]);
                        receivedMessages.Add(sequence);
                    }
                    
                    // Simulate processing delay
                    await Task.Delay(10);
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "order-test" },
                CancellationToken.None);

            await Task.Delay(1000);

            // Act - Send messages with chaos-induced latency
            for (int i = 0; i < messageCount; i++)
            {
                var message = new Dictionary<string, object>
                {
                    ["sequence"] = i,
                    ["timestamp"] = DateTime.UtcNow
                };

                // Wrap the publish operation with chaos policy
                await latencyPolicy.ExecuteAsync(async () =>
                {
                    return await _messageQueue.PublishAsync(
                        "order-test-stream",
                        message,
                        MessagePriority.Normal,
                        CancellationToken.None);
                });
            }

            // Wait for processing
            await Task.Delay(3000);

            // Assert - Messages should maintain order despite latency
            receivedMessages.Should().HaveCountGreaterOrEqualTo(messageCount * 0.8);
            
            // Check ordering for consecutive messages
            var orderedPairs = 0;
            for (int i = 1; i < receivedMessages.Count; i++)
            {
                if (receivedMessages[i] > receivedMessages[i - 1])
                    orderedPairs++;
            }

            var orderingRatio = (double)orderedPairs / (receivedMessages.Count - 1);
            orderingRatio.Should().BeGreaterThan(0.9); // 90% ordering maintained
            Output.WriteLine($"Ordering ratio: {orderingRatio:P2}");
        }

        [Fact]
        public async Task MessageQueue_WithRandomFailures_ProcessesWithRetries()
        {
            // Arrange
            var processedMessages = new List<string>();
            var failureCount = 0;
            var messageCount = 100;

            // Create exception chaos policy
            var exceptionPolicy = CreateExceptionChaosPolicy<bool>(
                injectionRate: 0.2, // 20% failure rate
                exceptionFactory: (context, ct) =>
                {
                    Interlocked.Increment(ref failureCount);
                    return new InvalidOperationException("Chaos-induced failure");
                });

            // Subscribe with chaos-wrapped handler
            await _messageQueue.SubscribeAsync<TradingEvent>(
                "chaos-events",
                "chaos-processor",
                async (evt) =>
                {
                    // Wrap processing with chaos policy
                    var result = await exceptionPolicy.ExecuteAsync(async () =>
                    {
                        processedMessages.Add(evt.Symbol);
                        await Task.Delay(5); // Simulate work
                        return true;
                    });

                    return result;
                },
                new SubscriptionOptions 
                { 
                    ConsumerName = "chaos-processor",
                    MaxRetries = 3
                },
                CancellationToken.None);

            await Task.Delay(1000);

            // Act - Send events
            for (int i = 0; i < messageCount; i++)
            {
                var evt = new TradingEvent
                {
                    EventType = TradingEventType.MarketDataUpdate,
                    Symbol = $"SYM{i}",
                    Timestamp = DateTime.UtcNow
                };

                await _messageQueue.PublishAsync("chaos-events", evt, MessagePriority.Normal);
            }

            // Wait for processing with retries
            await Task.Delay(5000);

            // Assert
            processedMessages.Should().HaveCountGreaterOrEqualTo(messageCount * 0.95); // 95% success rate with retries
            failureCount.Should().BeGreaterThan(0);
            Output.WriteLine($"Processed {processedMessages.Count}/{messageCount} messages with {failureCount} failures");
        }

        [Fact]
        public async Task MessageQueue_UnderResourceExhaustion_DegradeGracefully()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var messagesSent = 0;
            var messagesReceived = 0;

            // Subscribe
            await _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "resource-test",
                "resource-consumer",
                async (message) =>
                {
                    Interlocked.Increment(ref messagesReceived);
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "resource-test" },
                CancellationToken.None);

            // Act - Send messages while exhausting resources
            var resourceTask = SimulateResourceExhaustion(
                cpuStressThreads: 8,
                memoryMB: 500,
                duration: TimeSpan.FromSeconds(10));

            var sendTask = Task.Run(async () =>
            {
                while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(10))
                {
                    try
                    {
                        await _messageQueue.PublishAsync(
                            "resource-test",
                            new Dictionary<string, object> { ["id"] = messagesSent },
                            MessagePriority.Normal);
                        
                        Interlocked.Increment(ref messagesSent);
                        await Task.Delay(10); // 100 messages/sec target
                    }
                    catch
                    {
                        // Continue despite errors
                    }
                }
            });

            await Task.WhenAll(resourceTask, sendTask);
            await Task.Delay(2000); // Recovery time

            // Assert - System should process some messages despite resource constraints
            messagesReceived.Should().BeGreaterThan(0);
            var successRate = (double)messagesReceived / messagesSent;
            successRate.Should().BeGreaterThan(0.5); // At least 50% success under stress
            
            Output.WriteLine($"Under resource exhaustion: Sent {messagesSent}, Received {messagesReceived} ({successRate:P2})");
        }

        [Fact]
        public async Task MessageQueue_WithPoisonMessages_IsolatesFailures()
        {
            // Arrange
            var goodMessages = new List<string>();
            var poisonMessages = new List<string>();
            var totalMessages = 100;
            var poisonRate = 0.1; // 10% poison messages

            // Subscribe with poison message handling
            await _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "poison-test",
                "poison-consumer",
                async (message) =>
                {
                    try
                    {
                        if (message.ContainsKey("poison") && (bool)message["poison"])
                        {
                            poisonMessages.Add(message["id"].ToString()!);
                            throw new InvalidOperationException("Poison message detected");
                        }

                        goodMessages.Add(message["id"].ToString()!);
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Poison message - should be isolated
                        return false; // Don't acknowledge
                    }
                },
                new SubscriptionOptions 
                { 
                    ConsumerName = "poison-test",
                    MaxRetries = 1 // Don't retry poison messages
                },
                CancellationToken.None);

            await Task.Delay(1000);

            // Act - Send mix of good and poison messages
            for (int i = 0; i < totalMessages; i++)
            {
                var isPoison = ChaosRandom.NextDouble() < poisonRate;
                var message = new Dictionary<string, object>
                {
                    ["id"] = i.ToString(),
                    ["poison"] = isPoison,
                    ["data"] = isPoison ? "CORRUPT" : "valid"
                };

                await _messageQueue.PublishAsync("poison-test", message, MessagePriority.Normal);
            }

            // Wait for processing
            await Task.Delay(3000);

            // Assert - Good messages processed, poison messages isolated
            goodMessages.Should().HaveCountGreaterThan(totalMessages * (1 - poisonRate) * 0.95);
            poisonMessages.Should().HaveCountGreaterThan(0);
            
            Output.WriteLine($"Processed {goodMessages.Count} good messages, isolated {poisonMessages.Count} poison messages");
        }

        private async Task SendMessageWithRetry(string id)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timespan, retry, context) =>
                    {
                        Output.WriteLine($"Retry {retry} for message {id} after {timespan}");
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                await _messageQueue.PublishAsync(
                    "chaos-test-stream",
                    new Dictionary<string, object> { ["id"] = id },
                    MessagePriority.Normal);
            });
        }
    }
}