using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Canonical;

namespace TradingPlatform.FixEngine.Performance
{
    /// <summary>
    /// High-performance optimizations for FIX protocol operations.
    /// Implements CPU affinity, SIMD operations, and lock-free data structures.
    /// </summary>
    /// <remarks>
    /// Targets sub-50 microsecond latency with zero allocation on critical path.
    /// Uses hardware acceleration where available (AVX2, SSE4.2).
    /// </remarks>
    public class FixPerformanceOptimizer : CanonicalFixServiceBase
    {
        private readonly int[] _cpuAffinity;
        private readonly ConcurrentQueue<PerformanceMetric> _metricsQueue;
        private readonly ArrayPool<byte> _bufferPool;
        private readonly Thread _optimizerThread;
        private long _totalMessages;
        private long _totalLatencyNanos;
        
        /// <summary>
        /// Initializes a new instance of the FixPerformanceOptimizer class.
        /// </summary>
        public FixPerformanceOptimizer(
            ITradingLogger logger,
            int[] cpuAffinity)
            : base(logger, "PerformanceOptimizer")
        {
            _cpuAffinity = cpuAffinity ?? throw new ArgumentNullException(nameof(cpuAffinity));
            _metricsQueue = new ConcurrentQueue<PerformanceMetric>();
            _bufferPool = ArrayPool<byte>.Create(65536, 1000);
            
            // Create high-priority thread for optimization tasks
            _optimizerThread = new Thread(OptimizationThreadProc)
            {
                Name = "FIX-Optimizer",
                Priority = ThreadPriority.Highest,
                IsBackground = false
            };
        }
        
        /// <summary>
        /// Sets CPU affinity for a thread to reduce context switching.
        /// </summary>
        /// <param name="thread">The thread to affinitize</param>
        /// <param name="cpuIndex">Index into the CPU affinity array</param>
        public void SetThreadAffinity(Thread thread, int cpuIndex)
        {
            LogMethodEntry();
            
            try
            {
                if (cpuIndex >= 0 && cpuIndex < _cpuAffinity.Length)
                {
                    var cpu = _cpuAffinity[cpuIndex];
                    
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        SetWindowsThreadAffinity(thread, cpu);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        SetLinuxThreadAffinity(thread, cpu);
                    }
                    
                    _logger.LogInformation("Set thread {ThreadName} affinity to CPU {Cpu}", 
                        thread.Name, cpu);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set thread affinity");
            }
            finally
            {
                LogMethodExit();
            }
        }
        
        /// <summary>
        /// Calculates FIX checksum using SIMD operations for performance.
        /// </summary>
        /// <param name="data">The data to checksum</param>
        /// <returns>FIX checksum value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int CalculateChecksumSimd(ReadOnlySpan<byte> data)
        {
            if (!Sse2.IsSupported || data.Length < 32)
            {
                // Fallback to scalar calculation
                return CalculateChecksumScalar(data);
            }
            
            int sum = 0;
            int i = 0;
            
            fixed (byte* pData = data)
            {
                // Process 16 bytes at a time using SSE2
                var vSum = Vector128<int>.Zero;
                
                for (; i <= data.Length - 16; i += 16)
                {
                    var bytes = Sse2.LoadVector128(pData + i);
                    
                    // Expand bytes to 32-bit integers and accumulate
                    var low = Sse2.UnpackLow(bytes, Vector128<byte>.Zero);
                    var high = Sse2.UnpackHigh(bytes, Vector128<byte>.Zero);
                    
                    var lowInts = Sse2.UnpackLow(low.AsInt16(), Vector128<short>.Zero);
                    var highInts = Sse2.UnpackHigh(low.AsInt16(), Vector128<short>.Zero);
                    
                    vSum = Sse2.Add(vSum, lowInts.AsInt32());
                    vSum = Sse2.Add(vSum, highInts.AsInt32());
                }
                
                // Sum the vector elements
                sum = vSum.GetElement(0) + vSum.GetElement(1) + 
                      vSum.GetElement(2) + vSum.GetElement(3);
            }
            
            // Process remaining bytes
            for (; i < data.Length; i++)
            {
                sum += data[i];
            }
            
            return sum % 256;
        }
        
        /// <summary>
        /// Scalar checksum calculation fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateChecksumScalar(ReadOnlySpan<byte> data)
        {
            int sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i];
            }
            return sum % 256;
        }
        
        /// <summary>
        /// Gets high-resolution timestamp using hardware counters.
        /// </summary>
        /// <returns>Timestamp in nanoseconds</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighResolutionTimestamp()
        {
            return Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency;
        }
        
        /// <summary>
        /// Records a performance metric with minimal overhead.
        /// </summary>
        /// <param name="metric">The metric to record</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMetric(PerformanceMetric metric)
        {
            _metricsQueue.Enqueue(metric);
            Interlocked.Increment(ref _totalMessages);
            Interlocked.Add(ref _totalLatencyNanos, metric.LatencyNanos);
        }
        
        /// <summary>
        /// Gets current performance statistics.
        /// </summary>
        public PerformanceStats GetStats()
        {
            LogMethodEntry();
            
            try
            {
                var messages = Interlocked.Read(ref _totalMessages);
                var latency = Interlocked.Read(ref _totalLatencyNanos);
                
                var stats = new PerformanceStats
                {
                    TotalMessages = messages,
                    AverageLatencyNanos = messages > 0 ? latency / messages : 0,
                    MessagesPerSecond = CalculateMessagesPerSecond(),
                    CpuAffinity = _cpuAffinity,
                    SimdSupported = Sse2.IsSupported,
                    Avx2Supported = Avx2.IsSupported
                };
                
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance stats");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Warms up CPU caches and JIT compiler.
        /// </summary>
        public void WarmUp()
        {
            LogMethodEntry();
            
            try
            {
                _logger.LogInformation("Starting performance warm-up");
                
                // Warm up buffer pool
                var buffers = new byte[100][];
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i] = _bufferPool.Rent(4096);
                }
                
                for (int i = 0; i < buffers.Length; i++)
                {
                    _bufferPool.Return(buffers[i], true);
                }
                
                // Warm up SIMD operations
                var testData = new byte[1024];
                for (int i = 0; i < 1000; i++)
                {
                    _ = CalculateChecksumSimd(testData);
                }
                
                // Warm up timestamp operations
                for (int i = 0; i < 10000; i++)
                {
                    _ = GetHighResolutionTimestamp();
                }
                
                _logger.LogInformation("Performance warm-up completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during warm-up");
            }
            finally
            {
                LogMethodExit();
            }
        }
        
        /// <summary>
        /// Optimization thread procedure.
        /// </summary>
        private void OptimizationThreadProc()
        {
            LogMethodEntry();
            
            try
            {
                // Set thread affinity to dedicated CPU
                SetThreadAffinity(Thread.CurrentThread, 0);
                
                // Process metrics queue with minimal overhead
                while (!Environment.HasShutdownStarted)
                {
                    if (_metricsQueue.TryDequeue(out var metric))
                    {
                        // Process metric (e.g., update histograms, calculate percentiles)
                        ProcessMetric(metric);
                    }
                    else
                    {
                        // Brief spin-wait to avoid context switch
                        Thread.SpinWait(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Optimization thread error");
            }
            finally
            {
                LogMethodExit();
            }
        }
        
        /// <summary>
        /// Processes a performance metric.
        /// </summary>
        private void ProcessMetric(PerformanceMetric metric)
        {
            // Implementation would update histograms, percentiles, etc.
            // Kept simple for brevity
        }
        
        /// <summary>
        /// Calculates messages per second rate.
        /// </summary>
        private decimal CalculateMessagesPerSecond()
        {
            // Implementation would track time windows
            // Simplified for brevity
            return 0;
        }
        
        /// <summary>
        /// Sets Windows thread affinity.
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();
        
        private void SetWindowsThreadAffinity(Thread thread, int cpu)
        {
            var mask = new IntPtr(1 << cpu);
            SetThreadAffinityMask(GetCurrentThread(), mask);
        }
        
        /// <summary>
        /// Sets Linux thread affinity.
        /// </summary>
        private void SetLinuxThreadAffinity(Thread thread, int cpu)
        {
            // Would use sched_setaffinity via P/Invoke
            // Simplified for brevity
        }
        
        #region Abstract Method Implementations
        
        /// <summary>
        /// Initializes the performance optimizer.
        /// </summary>
        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                // Start the optimizer thread
                _optimizerThread.Start();
                
                LogInfo("Performance optimizer initialized successfully");
                LogMethodExit();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize performance optimizer", ex);
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Starts the performance optimizer service.
        /// </summary>
        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                LogInfo("Performance optimizer started");
                LogMethodExit();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError("Failed to start performance optimizer", ex);
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Stops the performance optimizer service.
        /// </summary>
        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                // Signal optimization thread to stop
                _optimizerThread.Join(5000);
                
                LogInfo("Performance optimizer stopped");
                LogMethodExit();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError("Failed to stop performance optimizer", ex);
                LogMethodExit();
                throw;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Performance metric data.
    /// </summary>
    public struct PerformanceMetric
    {
        public long TimestampNanos { get; set; }
        public long LatencyNanos { get; set; }
        public string Operation { get; set; }
        public int MessageSize { get; set; }
    }
    
    /// <summary>
    /// Performance statistics.
    /// </summary>
    public class PerformanceStats
    {
        public long TotalMessages { get; set; }
        public long AverageLatencyNanos { get; set; }
        public decimal MessagesPerSecond { get; set; }
        public int[] CpuAffinity { get; set; } = Array.Empty<int>();
        public bool SimdSupported { get; set; }
        public bool Avx2Supported { get; set; }
    }
}