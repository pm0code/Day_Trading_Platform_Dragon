# TradingPlatform.FinancialCalculations

GPU-accelerated financial calculation engine with comprehensive analytics, regulatory compliance, and audit trails.

## Overview

This project provides high-performance financial calculation services with the following capabilities:

### 🚀 Key Features

- **GPU Acceleration**: ILGPU-powered calculations with automatic CPU fallback
- **Decimal Precision**: Maintains regulatory-compliant decimal precision using scaled integer calculations
- **Comprehensive Analytics**: Portfolio metrics, risk calculations, option pricing, and technical indicators
- **Regulatory Compliance**: SOX, MiFID, Basel III, and GDPR compliance with full audit trails
- **Performance Monitoring**: Real-time performance metrics and optimization
- **Multi-Currency Support**: Currency conversion with precision controls
- **Result Caching**: Intelligent caching with configurable expiry policies

### 🏗️ Architecture

The project follows the canonical base class pattern established in the trading platform:

```
TradingPlatform.FinancialCalculations/
├── Canonical/                          # Base classes and patterns
│   └── CanonicalFinancialCalculatorBase.cs
├── Engines/                           # Calculation engines
│   ├── PortfolioCalculationEngine.cs
│   └── OptionPricingEngine.cs
├── Kernels/                           # GPU kernels
│   └── AdvancedFinancialKernels.cs
├── Models/                            # Data models
│   └── FinancialModels.cs
├── Interfaces/                        # Service interfaces
│   └── IFinancialCalculationInterfaces.cs
├── Configuration/                     # Configuration classes
│   └── FinancialCalculationConfiguration.cs
├── Compliance/                        # Regulatory compliance
│   └── ComplianceAuditor.cs
├── Validators/                        # Input validation
├── Performance/                       # Performance monitoring
└── Documentation/                     # Technical documentation
```

## Financial Calculation Engines

### 1. Portfolio Calculation Engine

Provides comprehensive portfolio analytics with GPU acceleration:

#### Core Calculations
- **Portfolio Metrics**: Total value, P&L, returns, position weights
- **Risk Metrics**: Portfolio volatility, VaR, expected shortfall, concentration risk
- **Performance Attribution**: Active return, tracking error, sector attribution
- **Multi-Currency**: Currency conversion and exposure analysis

#### GPU Acceleration
- Parallel position processing for large portfolios
- Matrix operations for correlation and covariance calculations
- Vectorized risk metric calculations

#### Example Usage
```csharp
var portfolioEngine = new PortfolioCalculationEngine(config, complianceAuditor);
await portfolioEngine.InitializeAsync();

var result = await portfolioEngine.CalculatePortfolioMetricsAsync(positions, currentPrices);
if (result.IsSuccess)
{
    var metrics = result.Data;
    Console.WriteLine($"Portfolio Value: {metrics.TotalValue:C}");
    Console.WriteLine($"Total P&L: {metrics.UnrealizedPnL:C}");
    Console.WriteLine($"Portfolio Return: {metrics.TotalReturnPercent:F2}%");
}
```

### 2. Option Pricing Engine

Supports multiple option pricing models with GPU acceleration:

#### Pricing Models
- **Black-Scholes**: Analytical solution with Greeks calculation
- **Monte Carlo**: Configurable simulations with variance reduction
- **Binomial Tree**: Multi-step tree model for American options
- **Implied Volatility**: Newton-Raphson solver

#### GPU Acceleration
- Parallel Monte Carlo simulations
- Vectorized Black-Scholes calculations
- Batch processing for option chains

#### Example Usage
```csharp
var optionEngine = new OptionPricingEngine(config, complianceAuditor);
await optionEngine.InitializeAsync();

var result = await optionEngine.CalculateBlackScholesAsync(
    symbol: "AAPL",
    optionType: OptionType.Call,
    strike: 150m,
    spotPrice: 155m,
    timeToExpiry: 0.25m,  // 3 months
    riskFreeRate: 0.05m,  // 5%
    volatility: 0.20m     // 20%
);

if (result.IsSuccess)
{
    var option = result.Data;
    Console.WriteLine($"Option Price: {option.TheoreticalPrice:F4}");
    Console.WriteLine($"Delta: {option.Delta:F4}");
    Console.WriteLine($"Gamma: {option.Gamma:F6}");
}
```

### 3. Technical Analysis Engine

GPU-accelerated technical indicators:

#### Indicators
- **Moving Averages**: SMA, EMA, LWMA
- **Momentum**: RSI, MACD, Stochastic
- **Volatility**: Bollinger Bands, ATR
- **Volume**: OBV, Volume-weighted indicators

#### GPU Acceleration
- Parallel indicator calculation across multiple securities
- Optimized sliding window calculations
- Vectorized mathematical operations

### 4. Fixed Income Engine

Bond pricing and analytics:

#### Calculations
- **Bond Pricing**: Present value, yield to maturity
- **Duration**: Macaulay and modified duration
- **Convexity**: Price sensitivity to yield changes
- **Cash Flow Analysis**: NPV, IRR calculations

## GPU Acceleration Details

### ILGPU Integration

The project uses ILGPU for GPU acceleration with the following features:

#### Automatic Device Selection
```csharp
// Automatic GPU detection and prioritization
if (cudaDevice.IsAvailable)
    accelerator = cudaDevice.CreateAccelerator();
else if (openCLDevice.IsAvailable)
    accelerator = openCLDevice.CreateAccelerator();
else
    accelerator = cpuDevice.CreateAccelerator(); // Fallback
```

#### Scaled Integer Precision
```csharp
// Convert decimal to scaled integer for GPU calculations
const long PRICE_SCALE = 10000L;     // 4 decimal places
const long RATE_SCALE = 100000000L;  // 8 decimal places

long scaledPrice = ToScaledInteger(price, 4);
decimal result = FromScaledInteger(scaledPrice, 4);
```

#### Kernel Examples
```csharp
// Portfolio metrics kernel
public static void CalculatePortfolioMetricsKernel(
    Index1D index,
    ArrayView1D<long, Stride1D.Dense> quantities,
    ArrayView1D<long, Stride1D.Dense> prices,
    ArrayView1D<long, Stride1D.Dense> marketValues)
{
    var i = index.X;
    marketValues[i] = (quantities[i] * prices[i]) / PRICE_SCALE;
}
```

### Performance Benefits

GPU acceleration provides significant performance improvements:

- **Portfolio Calculations**: 10-50x speedup for large portfolios
- **Monte Carlo Simulations**: 100-1000x speedup for option pricing
- **Technical Indicators**: 5-20x speedup for multi-security analysis
- **Risk Calculations**: 20-100x speedup for VaR and stress testing

## Regulatory Compliance

### Compliance Features

The project includes comprehensive regulatory compliance:

#### Standards Supported
- **SOX (Sarbanes-Oxley)**: Financial calculation accuracy and audit trails
- **MiFID II**: Best execution and transaction reporting
- **Basel III**: Risk management and capital adequacy
- **GDPR**: Data protection and privacy controls

#### Audit Trail
```csharp
// Automatic audit trail generation
var auditId = await complianceAuditor.StartCalculationAuditAsync(
    "PortfolioMetrics", parameters, userId);

// Calculation execution with audit tracking
var result = await calculator.CalculateAsync(parameters);

// Complete audit trail
await complianceAuditor.CompleteCalculationAuditAsync(
    auditId, result, result.IsSuccess);
```

#### Validation Rules
```csharp
// Regulatory validation rules
_validationRules.Add("PortfolioMetrics", new ComplianceValidationRule
{
    RuleName = "SOX_Decimal_Precision",
    ValidationFunction = async (parameters, result) =>
    {
        var violations = new List<string>();
        if (!ValidateDecimalPrecision(result.TotalValue, 2))
            violations.Add("Total value precision violation");
        return violations;
    }
});
```

### Data Integrity

- **Digital Signatures**: RSA signing of audit entries
- **Hash Verification**: SHA-256 hashing for data integrity
- **Encryption**: AES encryption for sensitive data
- **Retention Policies**: Configurable data retention periods

## Configuration

### Financial Calculation Configuration

```csharp
var config = new FinancialCalculationConfiguration
{
    // Decimal precision settings
    DecimalPrecision = new DecimalPrecisionConfiguration
    {
        DefaultPrecision = 4,
        DefaultRoundingMode = RegulatoryRoundingMode.BankersRounding,
        CurrencyPrecisionOverrides = new Dictionary<string, int>
        {
            { "USD", 2 }, { "EUR", 2 }, { "BTC", 8 }
        }
    },
    
    // GPU acceleration settings
    GpuConfiguration = new GpuConfiguration
    {
        EnableGpuAcceleration = true,
        BatchSizeThreshold = 1000,
        EnableMultiGpu = true,
        EnableCpuFallback = true
    },
    
    // Performance thresholds
    PerformanceThresholds = new PerformanceConfiguration
    {
        MaxLatencyMs = 1000.0,
        MaxConcurrentCalculations = 100
    },
    
    // Compliance settings
    ComplianceConfiguration = new ComplianceConfiguration
    {
        EnableSOXCompliance = true,
        EnableMiFIDCompliance = true,
        EnableAuditTrail = true,
        AuditTrailRetention = TimeSpan.FromDays(2555) // 7 years
    }
};
```

### Caching Configuration

```csharp
CacheConfiguration = new CacheConfiguration
{
    EnableCaching = true,
    MaxCacheSize = 10000,
    DefaultCacheExpiry = TimeSpan.FromMinutes(15),
    CacheExpiryOverrides = new Dictionary<string, TimeSpan>
    {
        { "PortfolioValue", TimeSpan.FromMinutes(5) },
        { "OptionPrice", TimeSpan.FromMinutes(10) },
        { "RiskMetrics", TimeSpan.FromMinutes(30) }
    }
};
```

## Performance Monitoring

### Real-Time Metrics

```csharp
// Performance tracking
var metrics = await calculator.GetPerformanceMetricsAsync();
foreach (var metric in metrics.Data)
{
    Console.WriteLine($"Operation: {metric.Key}");
    Console.WriteLine($"  Avg Latency: {metric.Value.AverageLatencyMs:F2}ms");
    Console.WriteLine($"  GPU Usage: {metric.Value.GpuUsageCount}");
    Console.WriteLine($"  CPU Usage: {metric.Value.CpuUsageCount}");
}
```

### Health Monitoring

```csharp
// Health check
var health = await calculator.CheckHealthAsync();
Console.WriteLine($"Service Status: {health.Status}");
foreach (var check in health.Checks)
{
    Console.WriteLine($"  {check.Key}: {check.Value.Status} - {check.Value.Description}");
}
```

## Error Handling and Validation

### Input Validation

```csharp
// Automatic input validation
var validationResult = await calculator.ValidateInputAsync("PortfolioMetrics", parameters);
if (!validationResult.IsSuccess)
{
    Console.WriteLine($"Validation failed: {validationResult.Error.Message}");
    return;
}
```

### Error Recovery

```csharp
// Automatic GPU/CPU fallback
try
{
    result = await CalculateWithGpuAsync(parameters);
}
catch (GpuException)
{
    Logger.LogWarning("GPU calculation failed, falling back to CPU");
    result = await CalculateWithCpuAsync(parameters);
}
```

## Integration Examples

### Dependency Injection

```csharp
// Configure services
services.AddSingleton<FinancialCalculationConfiguration>(config);
services.AddSingleton<IComplianceAuditor, ComplianceAuditor>();
services.AddTransient<IPortfolioCalculationService, PortfolioCalculationEngine>();
services.AddTransient<IOptionPricingService, OptionPricingEngine>();
```

### Usage in Trading Strategies

```csharp
public class MomentumStrategy
{
    private readonly IPortfolioCalculationService _portfolioCalc;
    private readonly IOptionPricingService _optionPricing;
    
    public async Task<TradingSignal> GenerateSignalAsync(MarketData data)
    {
        // Calculate portfolio metrics
        var portfolioResult = await _portfolioCalc.CalculatePortfolioMetricsAsync(
            positions, data.CurrentPrices);
            
        // Calculate option hedging requirements
        var hedgeOptions = await _optionPricing.CalculateBlackScholesAsync(
            data.Symbol, OptionType.Put, strike, spot, expiry, rate, vol);
            
        return new TradingSignal
        {
            Action = portfolioResult.Data.RiskMetrics.VaR95 > threshold ? 
                     TradeAction.Hedge : TradeAction.Hold,
            HedgeOption = hedgeOptions.Data
        };
    }
}
```

## Testing and Validation

### Unit Testing

```csharp
[Test]
public async Task CalculatePortfolioMetrics_ValidInput_ReturnsCorrectResults()
{
    // Arrange
    var positions = CreateTestPositions();
    var prices = CreateTestPrices();
    
    // Act
    var result = await _portfolioEngine.CalculatePortfolioMetricsAsync(positions, prices);
    
    // Assert
    Assert.IsTrue(result.IsSuccess);
    Assert.AreEqual(expectedValue, result.Data.TotalValue, 0.01m);
    Assert.IsTrue(result.Data.UsedGpuAcceleration);
}
```

### Performance Testing

```csharp
[Test]
public async Task PortfolioCalculation_LargePortfolio_MeetsPerformanceRequirements()
{
    var positions = CreateLargePortfolio(10000); // 10K positions
    var stopwatch = Stopwatch.StartNew();
    
    var result = await _portfolioEngine.CalculatePortfolioMetricsAsync(positions, prices);
    
    stopwatch.Stop();
    Assert.IsTrue(result.IsSuccess);
    Assert.Less(stopwatch.ElapsedMilliseconds, 1000); // < 1 second
}
```

### Compliance Testing

```csharp
[Test]
public async Task CalculationAuditTrail_GeneratesCompliantAuditEntry()
{
    var auditId = await _complianceAuditor.StartCalculationAuditAsync(
        "PortfolioMetrics", parameters, "testuser");
        
    var auditEntry = await _complianceAuditor.GetAuditTrailAsync(auditId);
    
    Assert.IsNotNull(auditEntry.Data.First().ComplianceHash);
    Assert.IsNotNull(auditEntry.Data.First().Metadata["DigitalSignature"]);
}
```

## Advanced Features

### Custom Kernels

```csharp
// Create custom GPU kernel
public static void CustomRiskKernel(
    Index1D index,
    ArrayView1D<long, Stride1D.Dense> returns,
    ArrayView1D<long, Stride1D.Dense> weights,
    ArrayView1D<long, Stride1D.Dense> riskMetrics)
{
    // Custom risk calculation logic
    var security = index.X;
    // ... implementation
}

// Load and execute custom kernel
var customKernel = accelerator.LoadAutoGroupedStreamKernel<...>(CustomRiskKernel);
customKernel(dataSize, returnsBuffer, weightsBuffer, riskBuffer);
```

### Multi-GPU Support

```csharp
// Automatic multi-GPU distribution
var gpuManager = new MultiGpuManager();
var tasks = positions.Chunk(1000).Select(async chunk =>
{
    var gpu = await gpuManager.GetAvailableGpuAsync();
    return await CalculateChunkAsync(chunk, gpu);
});

var results = await Task.WhenAll(tasks);
```

### Real-Time Streaming

```csharp
// Real-time calculation updates
await foreach (var marketData in marketDataStream)
{
    var result = await _portfolioEngine.CalculatePortfolioMetricsAsync(
        positions, marketData.Prices);
        
    await PublishResultAsync(result.Data);
}
```

## Deployment and Operations

### Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY . /app
WORKDIR /app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradingPlatform.FinancialCalculations.dll"]
```

### Monitoring and Alerting

```csharp
// Performance alerting
if (metrics.AverageLatencyMs > config.PerformanceThresholds.LatencyAlertThresholdMs)
{
    await alertingService.SendAlertAsync("High calculation latency detected");
}

// Compliance monitoring
if (complianceViolations.Any())
{
    await complianceService.ReportViolationsAsync(complianceViolations);
}
```

### Scaling Considerations

- **Horizontal Scaling**: Multiple calculation engine instances
- **GPU Clustering**: Distributed GPU computation
- **Caching Layers**: Redis for distributed caching
- **Load Balancing**: Request distribution across engines

## Troubleshooting

### Common Issues

1. **GPU Not Available**
   - Check CUDA/OpenCL drivers
   - Verify GPU compatibility
   - Enable CPU fallback

2. **Precision Issues**
   - Verify decimal precision configuration
   - Check scaling factor settings
   - Validate rounding mode

3. **Performance Issues**
   - Monitor GPU memory usage
   - Check batch size thresholds
   - Optimize data transfer

4. **Compliance Violations**
   - Review validation rules
   - Check audit trail configuration
   - Verify data retention policies

### Logging and Diagnostics

```csharp
// Enable detailed logging
Logger.LogLevel = LogLevel.Debug;

// Performance diagnostics
var diagnostics = await calculator.GetDiagnosticsAsync();
Console.WriteLine($"GPU Memory Usage: {diagnostics.GpuMemoryUsage}MB");
Console.WriteLine($"Cache Hit Rate: {diagnostics.CacheHitRate:P}");
```

## License and Support

This financial calculation engine is part of the TradingPlatform solution and follows the same licensing terms. For support and contributions, please refer to the main project documentation.

## Related Projects

- **TradingPlatform.Core**: Core canonical patterns and base classes
- **TradingPlatform.GPU**: GPU acceleration infrastructure
- **TradingPlatform.Common**: Shared utilities and extensions
- **TradingPlatform.Foundation**: Base interfaces and models