using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Performance;
using TradingPlatform.FixEngine.Services;

namespace TradingPlatform.FixEngine.Tests
{
    /// <summary>
    /// Performance tests for FIX engine components.
    /// Validates sub-50 microsecond latency targets.
    /// </summary>
    public class PerformanceTests
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly FixMessagePool _messagePool;
        private readonly FixMessageParser _parser;
        private readonly FixPerformanceOptimizer _optimizer;
        private readonly MemoryOptimizer _memoryOptimizer;
        
        public PerformanceTests()
        {
            _mockLogger = new Mock<ITradingLogger>();
            _messagePool = new FixMessagePool(_mockLogger.Object, 10000, 4096);
            _parser = new FixMessageParser(_mockLogger.Object, _messagePool);
            _optimizer = new FixPerformanceOptimizer(_mockLogger.Object, new[] { 0, 1, 2, 3 });
            _memoryOptimizer = new MemoryOptimizer(_mockLogger.Object, 100);
        }
        
        [Fact]
        public void MessagePool_RentReturn_ShouldBeSubMicrosecond()
        {
            // Arrange
            const int iterations = 100000;
            var stopwatch = Stopwatch.StartNew();
            
            // Act
            for (int i = 0; i < iterations; i++)
            {
                var message = _messagePool.Rent();
                _messagePool.Return(message);
            }
            
            stopwatch.Stop();
            
            // Assert
            var avgNanoseconds = stopwatch.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
            avgNanoseconds.Should().BeLessThan(1000, "Pool operations should be sub-microsecond");
            
            // Log performance
            Console.WriteLine($"Average pool operation: {avgNanoseconds:F0} nanoseconds");
        }
        
        [Fact]
        public void LockFreeQueue_EnqueueDequeue_ShouldBeSubMicrosecond()
        {
            // Arrange
            var queue = new LockFreeQueue<TestItem>(_mockLogger.Object);
            const int iterations = 100000;
            var items = new TestItem[iterations];
            
            for (int i = 0; i < iterations; i++)
            {
                items[i] = new TestItem { Value = i };
            }
            
            // Warm up
            for (int i = 0; i < 1000; i++)
            {
                queue.Enqueue(items[i]);
                queue.TryDequeue(out _);
            }
            
            // Act - Measure enqueue
            var enqueueStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                queue.Enqueue(items[i]);
            }
            enqueueStopwatch.Stop();
            
            // Act - Measure dequeue
            var dequeueStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                queue.TryDequeue(out _);
            }
            dequeueStopwatch.Stop();
            
            // Assert
            var avgEnqueueNanos = enqueueStopwatch.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
            var avgDequeueNanos = dequeueStopwatch.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
            
            avgEnqueueNanos.Should().BeLessThan(500, "Enqueue should be sub-500ns");
            avgDequeueNanos.Should().BeLessThan(500, "Dequeue should be sub-500ns");
            
            Console.WriteLine($"Avg enqueue: {avgEnqueueNanos:F0}ns, Avg dequeue: {avgDequeueNanos:F0}ns");
        }
        
        [Fact]
        public void SimdChecksum_ShouldBeFasterThanScalar()
        {
            // Arrange
            var data = new byte[1024];
            new Random(42).NextBytes(data);
            const int iterations = 100000;
            
            _optimizer.WarmUp();
            
            // Act - Scalar
            var scalarStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = CalculateChecksumScalar(data);
            }
            scalarStopwatch.Stop();
            
            // Act - SIMD
            var simdStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = _optimizer.CalculateChecksumSimd(data);
            }
            simdStopwatch.Stop();
            
            // Assert
            var speedup = scalarStopwatch.Elapsed.TotalMilliseconds / simdStopwatch.Elapsed.TotalMilliseconds;
            speedup.Should().BeGreaterThan(1.5, "SIMD should be at least 1.5x faster");
            
            Console.WriteLine($"SIMD speedup: {speedup:F2}x");
        }
        
        [Fact]
        public void MemoryOptimizer_PreAllocation_ShouldReduceGC()
        {
            // Arrange
            _memoryOptimizer.OptimizeGarbageCollector();
            var statsBefore = _memoryOptimizer.GetMemoryStats();
            
            // Act
            _memoryOptimizer.PreAllocateBuffers(1000, 4096);
            
            // Rent and return many buffers
            for (int i = 0; i < 10000; i++)
            {
                using var buffer = _memoryOptimizer.RentBuffer(4096);
                // Use buffer
                buffer.Memory.Span[0] = 1;
            }
            
            var statsAfter = _memoryOptimizer.GetMemoryStats();
            
            // Assert
            var gen0Increase = statsAfter.Gen0Collections - statsBefore.Gen0Collections;
            gen0Increase.Should().BeLessThan(5, "Pre-allocation should minimize GC");
            
            Console.WriteLine($"Gen0 collections: {gen0Increase}");
        }
        
        [Fact]
        public async Task EndToEnd_OrderProcessing_ShouldMeetLatencyTarget()
        {
            // Arrange
            const int warmupOrders = 1000;
            const int testOrders = 10000;
            var latencies = new long[testOrders];
            
            // Warm up
            for (int i = 0; i < warmupOrders; i++)
            {
                var message = CreateTestOrderMessage();
                var parsed = _parser.ParseMessage(message);
                parsed.IsSuccess.Should().BeTrue();
            }
            
            // Act
            for (int i = 0; i < testOrders; i++)
            {
                var start = _optimizer.GetHighResolutionTimestamp();
                
                // Simulate order processing
                var message = CreateTestOrderMessage();
                var parsed = _parser.ParseMessage(message);
                if (parsed.IsSuccess && parsed.Value != null)
                {
                    _messagePool.Return(parsed.Value);
                }
                
                var end = _optimizer.GetHighResolutionTimestamp();
                latencies[i] = end - start;
            }
            
            // Calculate percentiles
            Array.Sort(latencies);
            var p50 = latencies[testOrders / 2];
            var p99 = latencies[(int)(testOrders * 0.99)];
            var p999 = latencies[(int)(testOrders * 0.999)];
            
            // Assert
            p50.Should().BeLessThan(30_000, "P50 should be < 30 microseconds");
            p99.Should().BeLessThan(50_000, "P99 should be < 50 microseconds");
            p999.Should().BeLessThan(100_000, "P99.9 should be < 100 microseconds");
            
            Console.WriteLine($"Latencies - P50: {p50/1000.0:F1}μs, P99: {p99/1000.0:F1}μs, P99.9: {p999/1000.0:F1}μs");
        }
        
        private byte[] CreateTestOrderMessage()
        {
            var message = "8=FIX.4.4\x019=148\x0135=D\x0149=SENDER\x0156=TARGET\x0134=1\x01" +
                         "52=20240101-12:00:00.000\x0111=ORDER123\x0121=1\x0155=AAPL\x0154=1\x01" +
                         "60=20240101-12:00:00.000\x0140=2\x0138=100\x0144=150.50\x0159=0\x0110=123\x01";
            return System.Text.Encoding.ASCII.GetBytes(message);
        }
        
        private int CalculateChecksumScalar(ReadOnlySpan<byte> data)
        {
            int sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i];
            }
            return sum % 256;
        }
        
        private class TestItem
        {
            public int Value { get; set; }
        }
    }
    
    /// <summary>
    /// Benchmark tests for detailed performance analysis.
    /// Run with: dotnet run -c Release -- --filter "*Benchmark*"
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, targetCount: 5)]
    public class FixEngineBenchmarks
    {
        private FixMessagePool _messagePool = null!;
        private FixMessageParser _parser = null!;
        private byte[] _sampleMessage = null!;
        
        [GlobalSetup]
        public void Setup()
        {
            var mockLogger = new Mock<ITradingLogger>();
            _messagePool = new FixMessagePool(mockLogger.Object);
            _parser = new FixMessageParser(mockLogger.Object, _messagePool);
            
            _sampleMessage = System.Text.Encoding.ASCII.GetBytes(
                "8=FIX.4.4\x019=148\x0135=D\x0149=SENDER\x0156=TARGET\x0134=1\x01" +
                "52=20240101-12:00:00.000\x0111=ORDER123\x0121=1\x0155=AAPL\x0154=1\x01" +
                "60=20240101-12:00:00.000\x0140=2\x0138=100\x0144=150.50\x0159=0\x0110=123\x01");
        }
        
        [Benchmark]
        public void MessagePoolRentReturn()
        {
            var message = _messagePool.Rent();
            _messagePool.Return(message);
        }
        
        [Benchmark]
        public void ParseFixMessage()
        {
            var result = _parser.ParseMessage(_sampleMessage);
            if (result.IsSuccess && result.Value != null)
            {
                _messagePool.Return(result.Value);
            }
        }
        
        [Benchmark]
        public void BuildFixMessage()
        {
            var fields = new System.Collections.Generic.Dictionary<int, string>
            {
                [11] = "ORDER123",
                [55] = "AAPL",
                [54] = "1",
                [38] = "100",
                [44] = "150.50"
            };
            
            var result = _parser.BuildMessage("D", fields);
        }
    }
}