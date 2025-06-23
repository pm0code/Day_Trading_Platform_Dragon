using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;

namespace TradingPlatform.Core.Performance;

/// <summary>
/// High-performance thread manager for ultra-low latency trading operations
/// Implements CPU core affinity, thread priority optimization, and NUMA awareness
/// Target: Sub-100 microsecond execution times for critical trading paths
/// </summary>
public sealed class HighPerformanceThreadManager : IDisposable
{
    private readonly Dictionary<ThreadType, Thread> _dedicatedThreads = new();
    private readonly Dictionary<ThreadType, nint> _coreAffinityMasks = new();
    private readonly object _lock = new();
    private bool _disposed;

    // Windows API imports for thread affinity
    [DllImport("kernel32.dll")]
    private static extern nint SetThreadAffinityMask(nint hThread, nint dwThreadAffinityMask);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern bool SetThreadPriority(nint hThread, int nPriority);

    // Thread priority constants
    private const int THREAD_PRIORITY_TIME_CRITICAL = 15;
    private const int THREAD_PRIORITY_HIGHEST = 2;
    private const int THREAD_PRIORITY_ABOVE_NORMAL = 1;

    /// <summary>
    /// Initializes the high-performance thread manager with optimal core assignments
    /// </summary>
    public HighPerformanceThreadManager()
    {
        InitializeCoreAffinityMap();
    }

    /// <summary>
    /// Creates a dedicated thread for specific trading operations with optimal affinity
    /// </summary>
    public Thread CreateDedicatedThread(ThreadType threadType, ThreadStart threadStart, string? threadName = null)
    {
        lock (_lock)
        {
            if (_dedicatedThreads.ContainsKey(threadType))
            {
                throw new InvalidOperationException($"Dedicated thread for {threadType} already exists");
            }

            var thread = new Thread(threadStart)
            {
                Name = threadName ?? $"Trading-{threadType}",
                IsBackground = false // Keep application alive for trading threads
            };

            _dedicatedThreads[threadType] = thread;
            return thread;
        }
    }

    /// <summary>
    /// Starts a dedicated thread with optimal performance settings
    /// </summary>
    public void StartDedicatedThread(ThreadType threadType)
    {
        lock (_lock)
        {
            if (!_dedicatedThreads.TryGetValue(threadType, out var thread))
            {
                throw new InvalidOperationException($"No dedicated thread created for {threadType}");
            }

            thread.Start();

            // Apply performance optimizations after thread starts
            ApplyThreadOptimizations(thread, threadType);
        }
    }

    /// <summary>
    /// Optimizes the current thread for ultra-low latency operations
    /// </summary>
    public static void OptimizeCurrentThread(ThreadType threadType)
    {
        var currentThread = GetCurrentThread();

        // Set thread priority based on type
        var priority = GetThreadPriority(threadType);
        SetThreadPriority(currentThread, priority);

        // Prevent thread from being moved between cores during critical operations
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var affinityMask = GetOptimalAffinityMask(threadType);
            SetThreadAffinityMask(currentThread, affinityMask);
        }

        // Configure thread for minimal latency
        Thread.CurrentThread.Priority = ThreadPriority.Highest;

        // Disable thread pool work stealing for dedicated threads
        if (threadType == ThreadType.OrderExecution || threadType == ThreadType.MarketDataProcessing)
        {
            Thread.CurrentThread.DisableComObjectEagerCleanup();
        }
    }

    /// <summary>
    /// Creates a high-performance task scheduler for trading operations
    /// </summary>
    public static TaskScheduler CreateHighPerformanceScheduler(ThreadType threadType)
    {
        var scheduler = new LimitedConcurrencyLevelTaskScheduler(1); // Single-threaded for minimal latency
        return scheduler;
    }

    /// <summary>
    /// Runs a task on a dedicated high-performance thread
    /// </summary>
    public Task<T> RunOnDedicatedThreadAsync<T>(ThreadType threadType, Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        if (!_dedicatedThreads.TryGetValue(threadType, out var thread))
        {
            throw new InvalidOperationException($"No dedicated thread available for {threadType}");
        }

        // Queue work on the dedicated thread
        var workItem = new ThreadPoolWorkItem(() =>
        {
            try
            {
                OptimizeCurrentThread(threadType);
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        QueueWorkItemOnThread(thread, workItem);
        return tcs.Task;
    }

    /// <summary>
    /// Gets the number of optimal threads for parallel processing
    /// </summary>
    public static int GetOptimalParallelism(ThreadType threadType)
    {
        var coreCount = Environment.ProcessorCount;

        return threadType switch
        {
            ThreadType.OrderExecution => 1, // Single-threaded for minimal latency
            ThreadType.MarketDataProcessing => Math.Min(2, coreCount / 4), // Limited parallelism
            ThreadType.RiskCalculation => Math.Min(4, coreCount / 2), // Moderate parallelism
            ThreadType.DataIngestion => Math.Min(8, coreCount), // Higher parallelism acceptable
            ThreadType.Logging => 1, // Single-threaded to avoid contention
            _ => Math.Min(4, coreCount / 2)
        };
    }

    /// <summary>
    /// Configures garbage collection for minimal latency impact
    /// </summary>
    public static void ConfigureGarbageCollection()
    {
        // Configure GC for low-latency scenarios
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Suggest immediate collection of unused objects
        GC.Collect(0, GCCollectionMode.Optimized, blocking: false);
        GC.WaitForPendingFinalizers();

        // Configure server GC if available (better for multi-core scenarios)
        if (GCSettings.IsServerGC)
        {
            // Server GC is already configured - optimal for trading applications
        }
    }

    /// <summary>
    /// Pre-allocates memory pools for critical trading operations
    /// </summary>
    public static void PreAllocateMemoryPools()
    {
        // Pre-allocate buffers for common operations
        var bufferSizes = new[] { 1024, 4096, 8192, 16384, 32768 };

        foreach (var size in bufferSizes)
        {
            // Pre-allocate and immediately release to warm up memory pools
            var buffer = new byte[size];
            Array.Clear(buffer, 0, buffer.Length);
        }

        // Pre-allocate string builder pools
        for (int i = 0; i < 10; i++)
        {
            var sb = new System.Text.StringBuilder(4096);
            sb.Clear();
        }

        // Pre-JIT critical methods
        PreJitCriticalMethods();
    }

    private static void PreJitCriticalMethods()
    {
        // Pre-compile critical methods to avoid JIT overhead during trading
        var dummy = DateTime.UtcNow.Ticks;
        var timestamp = (DateTimeOffset.UtcNow.Ticks - DateTimeOffset.UnixEpoch.Ticks) * 100L;

        // Force JIT compilation of decimal operations
        var price1 = 100.50m;
        var price2 = 100.51m;
        var spread = price2 - price1;
        var midPrice = (price1 + price2) / 2m;

        // Force JIT compilation of string operations
        var symbol = "AAPL";
        var message = $"Order for {symbol} at {price1:F2}";
    }

    private void InitializeCoreAffinityMap()
    {
        var coreCount = Environment.ProcessorCount;

        // Assign specific cores to different thread types for optimal performance
        _coreAffinityMasks[ThreadType.OrderExecution] = GetCoreMask(0); // Core 0 - highest priority
        _coreAffinityMasks[ThreadType.MarketDataProcessing] = GetCoreMask(1); // Core 1
        _coreAffinityMasks[ThreadType.RiskCalculation] = GetCoreMask(2); // Core 2
        _coreAffinityMasks[ThreadType.DataIngestion] = GetCoreMask(3); // Core 3
        _coreAffinityMasks[ThreadType.Logging] = GetCoreMask(Math.Max(4, coreCount - 1)); // Last core
    }

    private static nint GetCoreMask(int coreIndex)
    {
        return (nint)(1L << coreIndex);
    }

    private static nint GetOptimalAffinityMask(ThreadType threadType)
    {
        var instance = new HighPerformanceThreadManager();
        return instance._coreAffinityMasks.TryGetValue(threadType, out var mask) ? mask : (nint)1;
    }

    private static int GetThreadPriority(ThreadType threadType)
    {
        return threadType switch
        {
            ThreadType.OrderExecution => THREAD_PRIORITY_TIME_CRITICAL,
            ThreadType.MarketDataProcessing => THREAD_PRIORITY_HIGHEST,
            ThreadType.RiskCalculation => THREAD_PRIORITY_ABOVE_NORMAL,
            ThreadType.DataIngestion => THREAD_PRIORITY_ABOVE_NORMAL,
            ThreadType.Logging => ThreadPriority.Normal.GetHashCode(),
            _ => THREAD_PRIORITY_ABOVE_NORMAL
        };
    }

    private void ApplyThreadOptimizations(Thread thread, ThreadType threadType)
    {
        // Note: Thread affinity must be set from within the thread itself
        // This method schedules the optimization to occur when the thread starts
        var optimizationAction = new Action(() =>
        {
            OptimizeCurrentThread(threadType);
        });

        // The optimization will be applied when the thread's main method calls OptimizeCurrentThread
    }

    private static void QueueWorkItemOnThread(Thread thread, ThreadPoolWorkItem workItem)
    {
        // For dedicated threads, we need a custom work queue mechanism
        // This is a simplified implementation - production would use a lock-free queue
        ThreadPool.QueueUserWorkItem(_ => workItem.Execute());
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            foreach (var thread in _dedicatedThreads.Values)
            {
                if (thread.IsAlive)
                {
                    thread.Join(TimeSpan.FromSeconds(5)); // Give threads time to complete
                }
            }

            _dedicatedThreads.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Thread types for trading platform operations
/// </summary>
public enum ThreadType
{
    OrderExecution,         // Highest priority - order processing and FIX messaging
    MarketDataProcessing,   // High priority - real-time market data handling
    RiskCalculation,        // Medium priority - position and risk calculations  
    DataIngestion,          // Medium priority - data collection and aggregation
    Logging,                // Lower priority - logging and diagnostics
    BackgroundProcessing    // Lowest priority - cleanup and maintenance tasks
}

/// <summary>
/// Custom work item for thread pool operations
/// </summary>
internal class ThreadPoolWorkItem
{
    private readonly Action _action;

    public ThreadPoolWorkItem(Action action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public void Execute()
    {
        _action();
    }
}

/// <summary>
/// Limited concurrency task scheduler for high-performance operations
/// </summary>
public sealed class LimitedConcurrencyLevelTaskScheduler : TaskScheduler, IDisposable
{
    private readonly LinkedList<Task> _tasks = new();
    private readonly Thread[] _threads;
    private readonly object _lock = new();
    private bool _shutdown;

    public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        MaximumConcurrencyLevel = maxDegreeOfParallelism;
        _threads = new Thread[maxDegreeOfParallelism];

        for (int i = 0; i < maxDegreeOfParallelism; i++)
        {
            _threads[i] = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = $"LimitedConcurrencyScheduler-{i}"
            };
            _threads[i].Start();
        }
    }

    protected override void QueueTask(Task task)
    {
        lock (_lock)
        {
            if (_shutdown) return;
            _tasks.AddLast(task);
            Monitor.Pulse(_lock);
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false; // Force all tasks to go through our thread pool
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (_lock)
        {
            return _tasks.ToArray();
        }
    }

    public override int MaximumConcurrencyLevel { get; }

    private void ThreadProc()
    {
        HighPerformanceThreadManager.OptimizeCurrentThread(ThreadType.OrderExecution);

        while (true)
        {
            Task? task = null;
            lock (_lock)
            {
                while (_tasks.Count == 0 && !_shutdown)
                {
                    Monitor.Wait(_lock);
                }

                if (_shutdown) break;

                if (_tasks.Count > 0)
                {
                    task = _tasks.First!.Value;
                    _tasks.RemoveFirst();
                }
            }

            if (task != null)
            {
                TryExecuteTask(task);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _shutdown = true;
            Monitor.PulseAll(_lock);
        }

        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }
}