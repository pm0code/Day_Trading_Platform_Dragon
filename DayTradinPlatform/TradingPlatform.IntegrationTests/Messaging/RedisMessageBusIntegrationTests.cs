using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using TradingPlatform.IntegrationTests.Fixtures;
using TradingPlatform.Messaging.Services;
using TradingPlatform.Messaging.Events;

namespace TradingPlatform.IntegrationTests.Messaging
{
    [Collection("Integration Tests")]
    public class RedisMessageBusIntegrationTests
    {
        private readonly IntegrationTestFixture _fixture;
        private readonly ICanonicalMessageQueue _messageQueue;

        public RedisMessageBusIntegrationTests(IntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _messageQueue = _fixture.GetRequiredService<ICanonicalMessageQueue>();
        }

        [Fact]
        public async Task PublishAndSubscribe_SimpleMessage_ShouldWork()
        {
            // Arrange
            var streamName = $"test-stream-{Guid.NewGuid():N}";
            var receivedMessages = new List<TestMessage>();
            var cts = new CancellationTokenSource();

            var testMessage = new TestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = "Hello Integration Test",
                Timestamp = DateTime.UtcNow
            };

            // Act - Subscribe
            var subscribeTask = _messageQueue.SubscribeAsync<TestMessage>(
                streamName,
                "test-consumer",
                async (message) =>
                {
                    receivedMessages.Add(message);
                    cts.Cancel(); // Cancel after receiving first message
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "test-consumer-1" },
                cts.Token);

            // Give subscription time to start
            await Task.Delay(100);

            // Act - Publish
            var publishResult = await _messageQueue.PublishAsync(
                streamName,
                testMessage,
                MessagePriority.Normal);

            // Wait for message or timeout
            try
            {
                await subscribeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel after receiving message
            }

            // Assert
            publishResult.IsSuccess.Should().BeTrue();
            receivedMessages.Should().HaveCount(1);
            receivedMessages[0].Id.Should().Be(testMessage.Id);
            receivedMessages[0].Content.Should().Be(testMessage.Content);
        }

        [Fact]
        public async Task PublishAndSubscribe_MultipleConsumers_ShouldDistributeMessages()
        {
            // Arrange
            var streamName = $"test-stream-{Guid.NewGuid():N}";
            var consumerGroup = "test-group";
            var consumer1Messages = new List<TestMessage>();
            var consumer2Messages = new List<TestMessage>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act - Start two consumers
            var consumer1Task = _messageQueue.SubscribeAsync<TestMessage>(
                streamName,
                consumerGroup,
                async (message) =>
                {
                    consumer1Messages.Add(message);
                    await Task.Delay(50); // Simulate processing
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "consumer-1" },
                cts.Token);

            var consumer2Task = _messageQueue.SubscribeAsync<TestMessage>(
                streamName,
                consumerGroup,
                async (message) =>
                {
                    consumer2Messages.Add(message);
                    await Task.Delay(50); // Simulate processing
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "consumer-2" },
                cts.Token);

            // Give subscriptions time to start
            await Task.Delay(200);

            // Publish multiple messages
            var publishTasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var message = new TestMessage
                {
                    Id = i.ToString(),
                    Content = $"Message {i}",
                    Timestamp = DateTime.UtcNow
                };

                publishTasks.Add(_messageQueue.PublishAsync(streamName, message, MessagePriority.Normal));
            }

            await Task.WhenAll(publishTasks);

            // Wait for processing
            await Task.Delay(1000);
            cts.Cancel();

            try
            {
                await Task.WhenAll(consumer1Task, consumer2Task);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert - Messages should be distributed between consumers
            var totalMessages = consumer1Messages.Count + consumer2Messages.Count;
            totalMessages.Should().Be(10);
            consumer1Messages.Should().NotBeEmpty();
            consumer2Messages.Should().NotBeEmpty();

            // Verify no duplicate processing
            var allIds = consumer1Messages.Select(m => m.Id)
                .Concat(consumer2Messages.Select(m => m.Id))
                .ToList();
            allIds.Should().HaveCount(10);
            allIds.Distinct().Should().HaveCount(10);
        }

        [Fact]
        public async Task PublishWithPriority_ShouldRouteToCorrectStream()
        {
            // Arrange
            var baseStreamName = $"test-stream-{Guid.NewGuid():N}";
            var highPriorityMessages = new List<TestMessage>();
            var normalPriorityMessages = new List<TestMessage>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Subscribe to high priority stream
            var highPriorityTask = _messageQueue.SubscribeAsync<TestMessage>(
                $"{baseStreamName}:high",
                "test-consumer",
                async (message) =>
                {
                    highPriorityMessages.Add(message);
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "high-consumer" },
                cts.Token);

            // Subscribe to normal priority stream
            var normalPriorityTask = _messageQueue.SubscribeAsync<TestMessage>(
                baseStreamName,
                "test-consumer",
                async (message) =>
                {
                    normalPriorityMessages.Add(message);
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "normal-consumer" },
                cts.Token);

            await Task.Delay(200); // Let subscriptions start

            // Act - Publish messages with different priorities
            await _messageQueue.PublishAsync(
                baseStreamName,
                new TestMessage { Id = "1", Content = "High Priority" },
                MessagePriority.High);

            await _messageQueue.PublishAsync(
                baseStreamName,
                new TestMessage { Id = "2", Content = "Normal Priority" },
                MessagePriority.Normal);

            await _messageQueue.PublishAsync(
                baseStreamName,
                new TestMessage { Id = "3", Content = "Low Priority" },
                MessagePriority.Low);

            // Wait for processing
            await Task.Delay(500);
            cts.Cancel();

            // Assert
            highPriorityMessages.Should().HaveCount(1);
            highPriorityMessages[0].Content.Should().Be("High Priority");

            normalPriorityMessages.Should().HaveCountGreaterOrEqualTo(2); // Normal and Low priority
        }

        [Fact]
        public async Task SubscribeWithRetry_ShouldRetryFailedMessages()
        {
            // Arrange
            var streamName = $"test-stream-{Guid.NewGuid():N}";
            var processAttempts = new Dictionary<string, int>();
            var successfulMessages = new List<TestMessage>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act - Subscribe with handler that fails first time
            var subscribeTask = _messageQueue.SubscribeAsync<TestMessage>(
                streamName,
                "test-consumer",
                async (message) =>
                {
                    if (!processAttempts.ContainsKey(message.Id))
                        processAttempts[message.Id] = 0;
                    
                    processAttempts[message.Id]++;

                    // Fail on first attempt
                    if (processAttempts[message.Id] == 1)
                    {
                        throw new Exception("Simulated processing error");
                    }

                    successfulMessages.Add(message);
                    return true;
                },
                new SubscriptionOptions 
                { 
                    ConsumerName = "retry-consumer",
                    MaxRetries = 3
                },
                cts.Token);

            await Task.Delay(200); // Let subscription start

            // Publish test message
            var testMessage = new TestMessage
            {
                Id = "retry-test",
                Content = "Should be retried",
                Timestamp = DateTime.UtcNow
            };

            await _messageQueue.PublishAsync(streamName, testMessage, MessagePriority.Normal);

            // Wait for retries
            await Task.Delay(2000);
            cts.Cancel();

            // Assert
            processAttempts.Should().ContainKey("retry-test");
            processAttempts["retry-test"].Should().BeGreaterOrEqualTo(2); // At least one retry
            successfulMessages.Should().HaveCount(1);
            successfulMessages[0].Id.Should().Be("retry-test");
        }

        [Fact]
        public async Task GetStreamInfo_ShouldReturnCorrectMetrics()
        {
            // Arrange
            var streamName = $"test-stream-{Guid.NewGuid():N}";
            
            // Act - Publish some messages
            for (int i = 0; i < 5; i++)
            {
                await _messageQueue.PublishAsync(
                    streamName,
                    new TestMessage { Id = i.ToString(), Content = $"Message {i}" },
                    MessagePriority.Normal);
            }

            // Get stream info
            var infoResult = await _messageQueue.GetStreamInfoAsync(streamName);

            // Assert
            infoResult.IsSuccess.Should().BeTrue();
            var info = infoResult.Value;
            info.Should().NotBeNull();
            info.Length.Should().Be(5);
            info.ConsumerGroups.Should().NotBeNull();
        }

        private class TestMessage
        {
            public string Id { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}