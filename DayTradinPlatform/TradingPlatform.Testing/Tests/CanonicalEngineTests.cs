using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Testing;

namespace TradingPlatform.Tests.Canonical
{
    /// <summary>
    /// Comprehensive unit tests for CanonicalEngine following canonical test patterns
    /// </summary>
    public class CanonicalEngineTests : CanonicalTestBase
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly TestEngine _engine;

        public CanonicalEngineTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ITradingLogger>();
            _engine = new TestEngine(_mockLogger.Object);
        }

        #region Engine Lifecycle Tests

        [Fact]
        public async Task Engine_Should_StartAndStopCorrectly()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing engine lifecycle");

                // Start engine
                await _engine.StartAsync();
                AssertWithLogging(_engine.ServiceState, ServiceState.Running, "Engine should be running");

                // Submit some work
                var submitResult = await _engine.SubmitAsync(new TestInput { Id = 1, Data = "Test" });
                AssertWithLogging(submitResult.IsSuccess, true, "Submit should succeed when running");

                // Stop engine
                await _engine.StopAsync();
                AssertWithLogging(_engine.ServiceState, ServiceState.Stopped, "Engine should be stopped");

                // Verify can't submit when stopped
                var submitAfterStop = await _engine.SubmitAsync(new TestInput { Id = 2, Data = "Test2" });
                AssertWithLogging(submitAfterStop.IsSuccess, false, "Submit should fail when stopped");
            });
        }

        #endregion

        #region SubmitAsync Tests

        [Fact]
        public async Task SubmitAsync_Should_ProcessSingleItem()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing single item submission");

                // Arrange
                await _engine.StartAsync();
                var input = new TestInput { Id = 1, Data = "Process me" };
                _engine.SetupProcessor(i => new TestOutput { Id = i.Id, Result = i.Data.ToUpper() });

                // Act
                var submitResult = await _engine.SubmitAsync(input);
                await Task.Delay(100); // Allow processing
                var output = await _engine.ReadOutputAsync();

                // Assert
                AssertWithLogging(submitResult.IsSuccess, true, "Submit should succeed");
                AssertWithLogging(output.IsSuccess, true, "Should have output");
                AssertNotNull(output.Value, "Output should not be null");
                AssertWithLogging(output.Value.Id, 1, "Output ID should match");
                AssertWithLogging(output.Value.Result, "PROCESS ME", "Data should be processed");
            });
        }

        [Fact]
        public async Task SubmitAsync_Should_RejectWhenNotRunning()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing submission when not running");

                // Act - Try to submit without starting
                var result = await _engine.SubmitAsync(new TestInput { Id = 1 });

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Submit should fail");
                AssertConditionWithLogging(
                    result.Error?.ErrorCode == TradingError.ErrorCodes.ServiceUnavailable,
                    "Should have service unavailable error");
            });
        }

        #endregion

        #region SubmitBatchAsync Tests

        [Fact]
        public async Task SubmitBatchAsync_Should_ProcessMultipleItems()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing batch submission");

                // Arrange
                await _engine.StartAsync();
                var inputs = Enumerable.Range(1, 10)
                    .Select(i => new TestInput { Id = i, Data = $"Item {i}" })
                    .ToList();
                _engine.SetupProcessor(i => new TestOutput { Id = i.Id, Result = i.Data.ToUpper() });

                // Act
                var submitResult = await _engine.SubmitBatchAsync(inputs);
                await Task.Delay(200); // Allow processing

                // Collect outputs
                var outputs = new List<TestOutput>();
                for (int i = 0; i < 10; i++)
                {
                    var output = await _engine.ReadOutputAsync();
                    if (output.IsSuccess && output.Value != null)
                    {
                        outputs.Add(output.Value);
                    }
                }

                // Assert
                AssertWithLogging(submitResult.IsSuccess, true, "Batch submit should succeed");
                AssertWithLogging(submitResult.Value, 10, "Should submit all items");
                AssertWithLogging(outputs.Count, 10, "Should process all items");
                AssertConditionWithLogging(
                    outputs.All(o => o.Result.StartsWith("ITEM")),
                    "All items should be processed correctly");
            });
        }

        [Fact]
        public async Task SubmitBatchAsync_Should_HandleEmptyBatch()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing empty batch submission");

                // Arrange
                await _engine.StartAsync();
                var emptyBatch = new List<TestInput>();

                // Act
                var result = await _engine.SubmitBatchAsync(emptyBatch);

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Empty batch should succeed");
                AssertWithLogging(result.Value, 0, "Should report 0 items submitted");
            });
        }

        #endregion

        #region Processing Tests

        [Fact]
        public async Task Engine_Should_HandleProcessingErrors()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing error handling during processing");

                // Arrange
                await _engine.StartAsync();
                var errorId = 5;
                _engine.SetupProcessor(i =>
                {
                    if (i.Id == errorId)
                    {
                        throw new InvalidOperationException("Processing error");
                    }
                    return new TestOutput { Id = i.Id, Result = i.Data };
                });

                // Act - Submit items including one that will error
                var inputs = new[]
                {
                    new TestInput { Id = 1, Data = "OK" },
                    new TestInput { Id = errorId, Data = "Error" },
                    new TestInput { Id = 3, Data = "OK" }
                };

                foreach (var input in inputs)
                {
                    await _engine.SubmitAsync(input);
                }

                await Task.Delay(200); // Allow processing

                // Collect successful outputs
                var outputs = new List<TestOutput>();
                while (true)
                {
                    var output = await _engine.ReadOutputAsync();
                    if (!output.IsSuccess) break;
                    if (output.Value != null)
                    {
                        outputs.Add(output.Value);
                    }
                }

                // Assert
                AssertWithLogging(outputs.Count, 2, "Should process 2 successful items");
                AssertConditionWithLogging(
                    !outputs.Any(o => o.Id == errorId),
                    "Error item should not produce output");
            });
        }

        [Fact]
        public async Task Engine_Should_RespectProcessingTimeout()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing processing timeout");

                // Arrange
                await _engine.StartAsync();
                _engine.SetProcessingTimeout(1); // 1 second timeout
                _engine.SetupProcessor(async i =>
                {
                    await Task.Delay(2000); // Longer than timeout
                    return new TestOutput { Id = i.Id };
                });

                // Act
                await _engine.SubmitAsync(new TestInput { Id = 1, Data = "Timeout test" });
                await Task.Delay(1500); // Wait for timeout

                var output = await _engine.ReadOutputAsync();

                // Assert
                AssertWithLogging(output.IsSuccess, false, "Should not have output due to timeout");
            });
        }

        #endregion

        #region Batching Tests

        [Fact]
        public async Task Engine_Should_ProcessInBatches()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing batch processing mode");

                // Arrange
                _engine.EnableBatchProcessing(true, batchSize: 5);
                await _engine.StartAsync();
                
                var batchesProcessed = new List<int>();
                _engine.SetupBatchProcessor((inputs) =>
                {
                    var inputList = inputs.ToList();
                    batchesProcessed.Add(inputList.Count);
                    
                    return inputList.Select(i => TradingResult<TestOutput>.Success(
                        new TestOutput { Id = i.Id, Result = $"Batch of {inputList.Count}" }
                    ));
                });

                // Act - Submit 12 items
                var items = Enumerable.Range(1, 12)
                    .Select(i => new TestInput { Id = i, Data = $"Item {i}" })
                    .ToList();
                    
                await _engine.SubmitBatchAsync(items);
                await Task.Delay(500); // Allow batch processing

                // Assert
                AssertConditionWithLogging(
                    batchesProcessed.Count >= 2,
                    $"Should process at least 2 batches, processed {batchesProcessed.Count}");
                AssertConditionWithLogging(
                    batchesProcessed.Any(b => b == 5),
                    "Should have at least one full batch of 5");
            });
        }

        #endregion

        #region Metrics Tests

        [Fact]
        public async Task Engine_Should_TrackMetrics()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing metrics tracking");

                // Arrange
                await _engine.StartAsync();
                _engine.SetupProcessor(i => new TestOutput { Id = i.Id, Result = i.Data });

                // Act - Process some items
                await _engine.SubmitBatchAsync(Enumerable.Range(1, 5)
                    .Select(i => new TestInput { Id = i, Data = $"Item {i}" })
                    .ToList());
                    
                await Task.Delay(200); // Allow processing

                // Get metrics
                var metrics = _engine.GetMetrics();

                // Assert
                AssertConditionWithLogging(
                    metrics.ContainsKey("TotalProcessed"),
                    "Should track total processed");
                AssertConditionWithLogging(
                    metrics.ContainsKey("Throughput"),
                    "Should track throughput");
                AssertConditionWithLogging(
                    metrics.ContainsKey("Engine.MaxConcurrency"),
                    "Should include engine configuration");
            });
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task Engine_Should_ProcessConcurrently()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing concurrent processing");

                // Arrange
                _engine.SetMaxConcurrency(4);
                await _engine.StartAsync();
                
                var processingTimes = new System.Collections.Concurrent.ConcurrentBag<DateTime>();
                _engine.SetupProcessor(async i =>
                {
                    processingTimes.Add(DateTime.UtcNow);
                    await Task.Delay(100); // Simulate work
                    return new TestOutput { Id = i.Id, Result = "Done" };
                });

                // Act - Submit multiple items
                var items = Enumerable.Range(1, 8)
                    .Select(i => new TestInput { Id = i })
                    .ToList();
                    
                await _engine.SubmitBatchAsync(items);
                await Task.Delay(500); // Allow processing

                // Assert - Check that items were processed in parallel
                var times = processingTimes.OrderBy(t => t).ToList();
                if (times.Count >= 4)
                {
                    var firstBatchEnd = times[3];
                    var secondBatchStart = times[4];
                    
                    AssertConditionWithLogging(
                        (secondBatchStart - firstBatchEnd).TotalMilliseconds < 50,
                        "Second batch should start immediately after first batch slot opens");
                }
            });
        }

        #endregion

        #region Test Helper Classes

        private class TestInput
        {
            public int Id { get; set; }
            public string Data { get; set; } = string.Empty;
        }

        private class TestOutput
        {
            public int Id { get; set; }
            public string Result { get; set; } = string.Empty;
        }

        private class TestEngine : CanonicalEngine<TestInput, TestOutput>
        {
            private Func<TestInput, TestOutput>? _processor;
            private Func<TestInput, Task<TestOutput>>? _asyncProcessor;
            private Func<IEnumerable<TestInput>, IEnumerable<TradingResult<TestOutput>>>? _batchProcessor;
            private bool _useBatchProcessing;
            private int _batchSize = 100;

            protected override int MaxConcurrency { get; set; } = 2;
            protected override int ProcessingTimeoutSeconds { get; set; } = 30;
            protected override bool EnableBatching => _useBatchProcessing;
            protected override int BatchSize => _batchSize;

            public TestEngine(ITradingLogger logger) : base(logger, "TestEngine")
            {
            }

            public void SetupProcessor(Func<TestInput, TestOutput> processor)
            {
                _processor = processor;
                _asyncProcessor = null;
                _batchProcessor = null;
            }

            public void SetupProcessor(Func<TestInput, Task<TestOutput>> processor)
            {
                _processor = null;
                _asyncProcessor = processor;
                _batchProcessor = null;
            }

            public void SetupBatchProcessor(Func<IEnumerable<TestInput>, IEnumerable<TradingResult<TestOutput>>> processor)
            {
                _batchProcessor = processor;
                _processor = null;
                _asyncProcessor = null;
            }

            public void SetMaxConcurrency(int max)
            {
                MaxConcurrency = max;
            }

            public void SetProcessingTimeout(int seconds)
            {
                ProcessingTimeoutSeconds = seconds;
            }

            public void EnableBatchProcessing(bool enable, int batchSize = 100)
            {
                _useBatchProcessing = enable;
                _batchSize = batchSize;
            }

            protected override async Task<TradingResult<TestOutput>> ProcessItemAsync(
                TestInput input, 
                CancellationToken cancellationToken)
            {
                try
                {
                    if (_asyncProcessor != null)
                    {
                        var result = await _asyncProcessor(input);
                        return TradingResult<TestOutput>.Success(result);
                    }
                    else if (_processor != null)
                    {
                        var result = _processor(input);
                        return TradingResult<TestOutput>.Success(result);
                    }
                    else
                    {
                        return TradingResult<TestOutput>.Success(
                            new TestOutput { Id = input.Id, Result = "Default" });
                    }
                }
                catch (Exception ex)
                {
                    return TradingResult<TestOutput>.Failure(
                        TradingError.ErrorCodes.SystemError,
                        $"Processing failed for item {input.Id}",
                        ex);
                }
            }

            protected override Task<IEnumerable<TradingResult<TestOutput>>> ProcessBatchAsync(
                IEnumerable<TestInput> inputs,
                CancellationToken cancellationToken)
            {
                if (_batchProcessor != null)
                {
                    var results = _batchProcessor(inputs);
                    return Task.FromResult(results);
                }
                
                // Fall back to base implementation
                return base.ProcessBatchAsync(inputs, cancellationToken);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}