# TradingPlatform.GPU

GPU acceleration module for the Day Trading Platform using ILGPU framework.

## Overview

This project provides GPU-accelerated financial calculations leveraging the two RTX GPUs available on the DRAGON machine. It uses ILGPU for cross-platform GPU support and implements high-performance kernels for technical analysis, screening, and risk calculations.

## Architecture

### Core Components

1. **GpuContext** - Manages GPU device selection and initialization
   - Automatically detects and prioritizes RTX GPUs
   - Falls back to CPU if no GPU available
   - Provides memory allocation utilities

2. **FinancialKernels** - GPU kernels for financial calculations
   - Simple Moving Average (SMA)
   - Exponential Moving Average (EMA)
   - Relative Strength Index (RSI)
   - Bollinger Bands
   - Stock screening
   - Portfolio VaR calculation
   - Monte Carlo simulation

3. **GpuAccelerator** - High-level service interface
   - Technical indicator batch calculation
   - Parallel stock screening
   - Risk metrics computation
   - Monte Carlo option pricing

### Decimal Precision Strategy

Since GPUs don't support decimal types natively, we use scaled integer arithmetic:
- All decimal values are scaled by 10,000 (4 decimal places)
- Calculations performed using 64-bit integers
- Results converted back to decimals on CPU

Example:
```csharp
decimal price = 123.45m;
long scaledPrice = (long)(price * 10000); // 1234500
// GPU processes scaledPrice
decimal result = scaledPrice / 10000m;
```

## Usage

### Basic Initialization

```csharp
using var accelerator = new GpuAccelerator();

if (accelerator.IsGpuAvailable)
{
    Console.WriteLine($"Using GPU: {accelerator.DeviceInfo.Name}");
}
```

### Technical Indicators

```csharp
var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
var prices = new decimal[][] { /* price arrays */ };
var periods = new[] { 20, 50, 200 };

var results = await accelerator.CalculateTechnicalIndicatorsAsync(
    symbols, prices, periods);

foreach (var symbol in symbols)
{
    var sma = results.SMA[symbol];
    var ema = results.EMA[symbol];
    var rsi = results.RSI[symbol];
}
```

### Stock Screening

```csharp
var criteria = new ScreeningCriteria
{
    MinPrice = 10m,
    MaxPrice = 1000m,
    MinVolume = 1_000_000m,
    MinMarketCap = 1_000_000_000m
};

var results = await accelerator.ScreenStocksAsync(stocks, criteria);
Console.WriteLine($"Found {results.MatchingSymbols.Length} stocks matching criteria");
```

## Performance Expectations

Based on RTX GPU capabilities:
- Technical indicators: 10-100x speedup vs CPU
- Stock screening: 50-200x speedup for large datasets
- Monte Carlo: 100-1000x speedup for parallel simulations

Actual performance depends on:
- Data size and complexity
- GPU memory bandwidth
- Kernel optimization
- CPU-GPU transfer overhead

## Integration Points

### With Core Services

```csharp
// In ScreeningEngine
public class GpuEnhancedScreeningEngine : CanonicalEngine<ScreeningRequest, ScreeningResult>
{
    private readonly IGpuAccelerator _gpu;
    
    protected override async Task<ScreeningResult> ProcessAsync(ScreeningRequest request)
    {
        if (_gpu.IsGpuAvailable && request.Stocks.Length > 1000)
        {
            // Use GPU for large datasets
            return await _gpu.ScreenStocksAsync(request.Stocks, request.Criteria);
        }
        
        // Fall back to CPU for small datasets
        return await base.ProcessAsync(request);
    }
}
```

### With Paper Trading

```csharp
// In RiskCalculator
public class GpuRiskCalculator : IRiskCalculator
{
    private readonly IGpuAccelerator _gpu;
    
    public async Task<RiskMetrics> CalculatePortfolioRiskAsync(Portfolio portfolio)
    {
        if (_gpu.IsGpuAvailable)
        {
            var results = await _gpu.CalculateRiskMetricsAsync(
                new[] { portfolio }, 
                _marketData);
            return results[0];
        }
        
        // CPU fallback
        return CalculateCpuRisk(portfolio);
    }
}
```

## Future Enhancements

1. **Additional Kernels**
   - MACD calculation
   - Pattern recognition
   - Correlation matrices
   - Greeks calculation

2. **Optimization**
   - Kernel fusion for multiple indicators
   - Shared memory optimization
   - Async multi-GPU support
   - Custom PTX assembly for critical paths

3. **ML Integration**
   - TensorRT integration for inference
   - Neural network training
   - Time series prediction

## Dependencies

- ILGPU 1.5.1 - GPU compiler and runtime
- ILGPU.Algorithms 1.5.1 - Algorithm library
- .NET 8.0 - Target framework

## Notes

- Always dispose GpuAccelerator to free GPU resources
- Monitor GPU memory usage for large datasets
- Consider batch processing to minimize CPU-GPU transfers
- Test CPU fallback paths for reliability