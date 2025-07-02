using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using TradingPlatform.Core.Utilities;
using TradingPlatform.Core.Canonical;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Services;
using Polly;
using Polly.CircuitBreaker;

namespace TradingPlatform.Benchmarks;

[Config(typeof(Config))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class CustomVsStandardBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddColumn(BenchmarkDotNet.Columns.StatisticColumn.P90);
            AddColumn(BenchmarkDotNet.Columns.StatisticColumn.P95);
            AddColumn(BenchmarkDotNet.Columns.StatisticColumn.P99);
        }
    }

    #region Rate Limiting Benchmarks
    
    private ApiRateLimiter _customRateLimiter = null!;
    private RateLimiter _standardRateLimiter = null!;
    private IAsyncPolicy<bool> _pollyRateLimiter = null!;
    private readonly string _providerId = "TestProvider";
    
    [GlobalSetup(Target = nameof(CustomRateLimiting) + "," + nameof(StandardRateLimiting) + "," + nameof(PollyRateLimiting))]
    public void SetupRateLimiting()
    {
        // Custom implementation
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _customRateLimiter = new ApiRateLimiter(memoryCache);
        
        // Standard .NET 7+ RateLimiter
        _standardRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 5,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 5,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
        
        // Polly Rate Limiting
        _pollyRateLimiter = Policy
            .HandleResult<bool>(r => !r)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (result, duration) => { },
                onReset: () => { });
    }
    
    [Benchmark(Baseline = true)]
    public async Task<bool> CustomRateLimiting()
    {
        return await _customRateLimiter.TryAcquireAsync(_providerId, 5);
    }
    
    [Benchmark]
    public async Task<bool> StandardRateLimiting()
    {
        using var lease = await _standardRateLimiter.AcquireAsync(1);
        return lease.IsAcquired;
    }
    
    [Benchmark]
    public async Task<bool> PollyRateLimiting()
    {
        try
        {
            return await _pollyRateLimiter.ExecuteAsync(async () => 
            {
                await Task.Yield();
                return true;
            });
        }
        catch (CircuitBreakerRejectedException)
        {
            return false;
        }
    }
    
    #endregion

    #region Object Pooling Benchmarks
    
    private HighPerformancePool<TestObject> _customPool = null!;
    private ObjectPool<TestObject> _standardPool = null!;
    private ArrayPool<byte> _arrayPool = null!;
    
    private class TestObject
    {
        public byte[] Buffer { get; set; } = new byte[4096];
        public int Value { get; set; }
        
        public void Reset()
        {
            Value = 0;
            Array.Clear(Buffer, 0, Buffer.Length);
        }
    }
    
    [GlobalSetup(Target = nameof(CustomObjectPool) + "," + nameof(StandardObjectPool) + "," + nameof(ArrayPooling))]
    public void SetupObjectPooling()
    {
        // Custom pool
        _customPool = new HighPerformancePool<TestObject>(
            factory: () => new TestObject(),
            reset: obj => obj.Reset(),
            maxSize: 1000);
        
        // Standard ObjectPool
        var provider = new DefaultObjectPoolProvider();
        _standardPool = provider.Create(new TestObjectPoolPolicy());
        
        // ArrayPool for byte arrays
        _arrayPool = ArrayPool<byte>.Shared;
        
        // Pre-warm pools
        for (int i = 0; i < 100; i++)
        {
            var obj1 = _customPool.Rent();
            _customPool.Return(obj1);
            
            var obj2 = _standardPool.Get();
            _standardPool.Return(obj2);
            
            var arr = _arrayPool.Rent(4096);
            _arrayPool.Return(arr);
        }
    }
    
    private class TestObjectPoolPolicy : IPooledObjectPolicy<TestObject>
    {
        public TestObject Create() => new TestObject();
        public bool Return(TestObject obj)
        {
            obj.Reset();
            return true;
        }
    }
    
    [Benchmark(Baseline = true)]
    public void CustomObjectPool()
    {
        var obj = _customPool.Rent();
        obj.Value = 42;
        _customPool.Return(obj);
    }
    
    [Benchmark]
    public void StandardObjectPool()
    {
        var obj = _standardPool.Get();
        obj.Value = 42;
        _standardPool.Return(obj);
    }
    
    [Benchmark]
    public void ArrayPooling()
    {
        var buffer = _arrayPool.Rent(4096);
        buffer[0] = 42;
        _arrayPool.Return(buffer, clearArray: false);
    }
    
    #endregion

    #region Decimal Math Benchmarks
    
    private readonly decimal[] _testValues = { 100.50m, 25.75m, 9.99m, 144.44m, 2.718m };
    
    [Benchmark(Baseline = true)]
    public decimal CustomDecimalSqrt()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += DecimalMathCanonical.Sqrt(value);
        }
        return sum;
    }
    
    [Benchmark]
    public decimal DecimalExSqrt()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += DecimalEx.Sqrt(value);
        }
        return sum;
    }
    
    [Benchmark]
    public decimal MathNetDoubleSqrt()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += (decimal)Math.Sqrt((double)value);
        }
        return sum;
    }
    
    [Benchmark(Baseline = true)]
    public decimal CustomDecimalLog()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += DecimalMathCanonical.Log(value);
        }
        return sum;
    }
    
    [Benchmark]
    public decimal DecimalExLog()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += DecimalEx.Log(value);
        }
        return sum;
    }
    
    [Benchmark]
    public decimal MathNetDoubleLog()
    {
        decimal sum = 0;
        foreach (var value in _testValues)
        {
            sum += (decimal)Math.Log((double)value);
        }
        return sum;
    }
    
    #endregion

    #region Lock-Free Queue Benchmarks
    
    private LockFreeQueue<int> _customQueue = null!;
    private Channel<int> _channel = null!;
    private ConcurrentQueue<int> _concurrentQueue = null!;
    private BlockingCollection<int> _blockingCollection = null!;
    
    [GlobalSetup(Target = nameof(CustomLockFreeQueue) + "," + nameof(ChannelQueue) + "," + 
                           nameof(ConcurrentQueueBench) + "," + nameof(BlockingCollectionQueue))]
    public void SetupQueues()
    {
        _customQueue = new LockFreeQueue<int>();
        
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });
        
        _concurrentQueue = new ConcurrentQueue<int>();
        _blockingCollection = new BlockingCollection<int>(1000);
    }
    
    [Benchmark(Baseline = true)]
    public async Task CustomLockFreeQueue()
    {
        _customQueue.Enqueue(42);
        _customQueue.TryDequeue(out _);
    }
    
    [Benchmark]
    public async Task ChannelQueue()
    {
        await _channel.Writer.WriteAsync(42);
        await _channel.Reader.ReadAsync();
    }
    
    [Benchmark]
    public void ConcurrentQueueBench()
    {
        _concurrentQueue.Enqueue(42);
        _concurrentQueue.TryDequeue(out _);
    }
    
    [Benchmark]
    public void BlockingCollectionQueue()
    {
        _blockingCollection.TryAdd(42);
        _blockingCollection.TryTake(out _);
    }
    
    #endregion

    #region Caching Benchmarks
    
    private ICacheService _customCache = null!;
    private IMemoryCache _standardCache = null!;
    private readonly string _cacheKey = "test-key";
    private readonly object _cacheValue = new { Price = 100.50m, Volume = 1000 };
    
    [GlobalSetup(Target = nameof(CustomCaching) + "," + nameof(StandardMemoryCache))]
    public void SetupCaching()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        var logger = new ConsoleLogger();
        _customCache = new CacheService_Canonical(memoryCache, logger);
        
        _standardCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
    }
    
    [Benchmark(Baseline = true)]
    public async Task<object?> CustomCaching()
    {
        await _customCache.SetAsync(_cacheKey, _cacheValue, TimeSpan.FromMinutes(5));
        return await _customCache.GetAsync<object>(_cacheKey);
    }
    
    [Benchmark]
    public object? StandardMemoryCache()
    {
        _standardCache.Set(_cacheKey, _cacheValue, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });
        return _standardCache.Get<object>(_cacheKey);
    }
    
    #endregion

    #region HTTP Retry Benchmarks
    
    private readonly HttpClient _httpClient = new HttpClient();
    private IAsyncPolicy<HttpResponseMessage> _pollyPolicy = null!;
    private int _attemptCount = 0;
    
    [GlobalSetup(Target = nameof(CustomHttpRetry) + "," + nameof(PollyHttpRetry))]
    public void SetupHttpRetry()
    {
        // Polly retry policy
        _pollyPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retry, context) => { });
    }
    
    [Benchmark(Baseline = true)]
    public async Task<bool> CustomHttpRetry()
    {
        _attemptCount = 0;
        try
        {
            // Simulate the custom retry logic from CanonicalProvider
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                _attemptCount++;
                if (_attemptCount >= 2) // Succeed on second attempt
                    return true;
                
                if (attempt < 3)
                    await Task.Delay(1000 * attempt); // Exponential backoff
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    [Benchmark]
    public async Task<bool> PollyHttpRetry()
    {
        _attemptCount = 0;
        try
        {
            var response = await _pollyPolicy.ExecuteAsync(async () =>
            {
                _attemptCount++;
                if (_attemptCount >= 2) // Succeed on second attempt
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            });
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    #endregion

    // Simple console logger for testing
    private class ConsoleLogger : ITradingLogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogDebug(string message) { }
        public void LogCritical(string message, Exception? ex = null) { }
    }
}

// Extension for DecimalEx library (placeholder - actual library would be referenced)
public static class DecimalEx
{
    public static decimal Sqrt(decimal x) => DecimalMathCanonical.Sqrt(x);
    public static decimal Log(decimal x) => DecimalMathCanonical.Log(x);
}

public class DefaultObjectPoolProvider
{
    public ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy) where T : class
    {
        return new DefaultObjectPool<T>(policy);
    }
}

public interface IPooledObjectPolicy<T>
{
    T Create();
    bool Return(T obj);
}

public interface ObjectPool<T> where T : class
{
    T Get();
    void Return(T obj);
}

public class DefaultObjectPool<T> : ObjectPool<T> where T : class
{
    private readonly IPooledObjectPolicy<T> _policy;
    private readonly ConcurrentBag<T> _items = new();
    
    public DefaultObjectPool(IPooledObjectPolicy<T> policy)
    {
        _policy = policy;
    }
    
    public T Get()
    {
        if (_items.TryTake(out var item))
            return item;
        return _policy.Create();
    }
    
    public void Return(T obj)
    {
        if (_policy.Return(obj))
            _items.Add(obj);
    }
}