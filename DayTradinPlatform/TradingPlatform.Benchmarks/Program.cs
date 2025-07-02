using BenchmarkDotNet.Running;

namespace TradingPlatform.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Trading Platform Custom vs Standard Library Benchmarks");
        Console.WriteLine("=====================================================");
        Console.WriteLine();
        Console.WriteLine("This benchmark suite compares current custom implementations");
        Console.WriteLine("with standard library alternatives to evaluate if replacements");
        Console.WriteLine("would meet the ultra-low latency requirements (<100μs).");
        Console.WriteLine();
        Console.WriteLine("Benchmarks include:");
        Console.WriteLine("1. Rate Limiting: Custom vs System.Threading.RateLimiting vs Polly");
        Console.WriteLine("2. Object Pooling: Custom vs ObjectPool vs ArrayPool");
        Console.WriteLine("3. Decimal Math: Custom vs DecimalEx vs System.Math");
        Console.WriteLine("4. Lock-Free Queue: Custom vs Channel vs ConcurrentQueue");
        Console.WriteLine("5. Caching: Custom vs IMemoryCache");
        Console.WriteLine("6. HTTP Retry: Custom vs Polly");
        Console.WriteLine();
        
        // Run all benchmarks
        var summary = BenchmarkRunner.Run<CustomVsStandardBenchmarks>();
    }
}