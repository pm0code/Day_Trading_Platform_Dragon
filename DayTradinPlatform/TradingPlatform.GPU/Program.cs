using System.Diagnostics;
using TradingPlatform.GPU.Services;
using TradingPlatform.GPU.Interfaces;

namespace TradingPlatform.GPU;

/// <summary>
/// Test program to demonstrate GPU acceleration capabilities
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Day Trading Platform GPU Acceleration Test ===\n");
        
        using var accelerator = new GpuAccelerator();
        
        // Display GPU information
        Console.WriteLine($"GPU Available: {accelerator.IsGpuAvailable}");
        Console.WriteLine($"Device: {accelerator.DeviceInfo.Name}");
        Console.WriteLine($"Type: {accelerator.DeviceInfo.Type}");
        Console.WriteLine($"Memory: {accelerator.DeviceInfo.MemoryGB}GB");
        Console.WriteLine($"Is RTX: {accelerator.DeviceInfo.IsRtx}");
        Console.WriteLine($"Max Threads: {accelerator.DeviceInfo.MaxThreadsPerGroup}");
        Console.WriteLine($"Warp Size: {accelerator.DeviceInfo.WarpSize}\n");
        
        // Test technical indicators
        await TestTechnicalIndicators(accelerator);
        
        // Test stock screening
        await TestStockScreening(accelerator);
        
        Console.WriteLine("\n=== GPU Acceleration Test Complete ===");
    }
    
    static async Task TestTechnicalIndicators(GpuAccelerator accelerator)
    {
        Console.WriteLine("--- Testing Technical Indicators ---");
        
        // Generate test data
        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
        var numPrices = 1000;
        var random = new Random(42);
        
        var prices = new decimal[symbols.Length][];
        for (int i = 0; i < symbols.Length; i++)
        {
            prices[i] = new decimal[numPrices];
            decimal basePrice = 100m + i * 50m;
            
            for (int j = 0; j < numPrices; j++)
            {
                // Random walk
                basePrice += (decimal)(random.NextDouble() - 0.5) * 2m;
                prices[i][j] = Math.Max(1m, basePrice);
            }
        }
        
        var periods = new[] { 20, 50, 200 };
        
        // Measure GPU performance
        var gpuStopwatch = Stopwatch.StartNew();
        var results = await accelerator.CalculateTechnicalIndicatorsAsync(symbols, prices, periods);
        gpuStopwatch.Stop();
        
        Console.WriteLine($"GPU Calculation Time: {gpuStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Symbols Processed: {results.Symbols.Length}");
        
        // Display sample results
        var symbol = symbols[0];
        Console.WriteLine($"\nSample Results for {symbol}:");
        Console.WriteLine($"  Last SMA: {results.SMA[symbol][numPrices - 1]:F2}");
        Console.WriteLine($"  Last EMA: {results.EMA[symbol][numPrices - 1]:F2}");
        Console.WriteLine($"  Last RSI: {results.RSI[symbol][numPrices - 1]:F2}");
        
        // Compare with CPU (simplified calculation)
        var cpuStopwatch = Stopwatch.StartNew();
        for (int s = 0; s < symbols.Length; s++)
        {
            for (int i = 20; i < numPrices; i++)
            {
                decimal sum = 0;
                for (int j = 0; j < 20; j++)
                {
                    sum += prices[s][i - j];
                }
                var sma = sum / 20m;
            }
        }
        cpuStopwatch.Stop();
        
        Console.WriteLine($"\nCPU Calculation Time (SMA only): {cpuStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"GPU Speedup: {(double)cpuStopwatch.ElapsedMilliseconds / gpuStopwatch.ElapsedMilliseconds:F2}x\n");
    }
    
    static async Task TestStockScreening(GpuAccelerator accelerator)
    {
        Console.WriteLine("--- Testing Stock Screening ---");
        
        // Generate test stock data
        var numStocks = 10000;
        var stocks = new object[numStocks]; // Placeholder objects
        
        var criteria = new ScreeningCriteria
        {
            MinPrice = 10m,
            MaxPrice = 500m,
            MinVolume = 1_000_000m,
            MinMarketCap = 1_000_000_000m
        };
        
        // Measure GPU performance
        var gpuStopwatch = Stopwatch.StartNew();
        var results = await accelerator.ScreenStocksAsync(stocks, criteria);
        gpuStopwatch.Stop();
        
        Console.WriteLine($"GPU Screening Time: {gpuStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Stocks Screened: {results.TotalScreened}");
        Console.WriteLine($"Matches Found: {results.MatchingSymbols.Length}");
        Console.WriteLine($"Throughput: {numStocks / (gpuStopwatch.ElapsedMilliseconds / 1000.0):F0} stocks/second");
    }
}