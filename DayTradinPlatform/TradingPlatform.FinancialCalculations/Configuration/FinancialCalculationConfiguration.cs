// TradingPlatform.FinancialCalculations.Configuration.FinancialCalculationConfiguration
// Configuration classes for financial calculation services

using TradingPlatform.FinancialCalculations.Models;

namespace TradingPlatform.FinancialCalculations.Configuration;

/// <summary>
/// Comprehensive configuration for financial calculation services
/// </summary>
public class FinancialCalculationConfiguration
{
    public string ServiceName { get; set; } = "FinancialCalculationService";
    public bool EnableDetailedLogging { get; set; } = true;
    public bool EnableAuditTrail { get; set; } = true;
    public bool EnablePerformanceMonitoring { get; set; } = true;
    
    public DecimalPrecisionConfiguration DecimalPrecision { get; set; } = new();
    public GpuConfiguration GpuConfiguration { get; set; } = new();
    public CacheConfiguration CacheConfiguration { get; set; } = new();
    public ComplianceConfiguration ComplianceConfiguration { get; set; } = new();
    public PerformanceConfiguration PerformanceThresholds { get; set; } = new();
    public RiskConfiguration RiskConfiguration { get; set; } = new();
    public ValidationConfiguration ValidationConfiguration { get; set; } = new();
    
    /// <summary>
    /// Convert configuration to safe string representation (no sensitive data)
    /// </summary>
    public string ToSafeString()
    {
        return $"ServiceName={ServiceName}, EnableGPU={GpuConfiguration.EnableGpuAcceleration}, " +
               $"DecimalPrecision={DecimalPrecision.DefaultPrecision}, " +
               $"CacheSize={CacheConfiguration.MaxCacheSize}";
    }
}

/// <summary>
/// Decimal precision configuration for financial calculations
/// </summary>
public class DecimalPrecisionConfiguration
{
    public int DefaultPrecision { get; set; } = 4;
    public int MaxPrecision { get; set; } = 10;
    public int MinPrecision { get; set; } = 2;
    public RegulatoryRoundingMode DefaultRoundingMode { get; set; } = RegulatoryRoundingMode.BankersRounding;
    
    // Currency-specific precision settings
    public Dictionary<string, int> CurrencyPrecisionOverrides { get; set; } = new()
    {
        { "USD", 2 },
        { "EUR", 2 },
        { "GBP", 2 },
        { "JPY", 0 },
        { "BTC", 8 },
        { "ETH", 6 }
    };
    
    // Calculation-specific precision settings
    public Dictionary<string, int> CalculationPrecisionOverrides { get; set; } = new()
    {
        { "PortfolioValue", 2 },
        { "PnL", 2 },
        { "OptionPrice", 4 },
        { "Volatility", 6 },
        { "InterestRate", 8 },
        { "RiskMetrics", 6 }
    };
}

/// <summary>
/// GPU acceleration configuration
/// </summary>
public class GpuConfiguration
{
    public bool EnableGpuAcceleration { get; set; } = true;
    public bool PreferCudaOverOpenCL { get; set; } = true;
    public bool EnableMultiGpu { get; set; } = true;
    public int MaxGpuMemoryUsageMB { get; set; } = 2048;
    public int BatchSizeThreshold { get; set; } = 1000;
    public TimeSpan GpuInitializationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableGpuMemoryPooling { get; set; } = true;
    public bool EnableAsyncGpuOperations { get; set; } = true;
    
    // Fallback configuration
    public bool EnableCpuFallback { get; set; } = true;
    public int CpuFallbackThreads { get; set; } = Environment.ProcessorCount;
    
    // Performance thresholds for GPU vs CPU decision
    public int MinDataSizeForGpu { get; set; } = 100;
    public double GpuCpuPerformanceRatio { get; set; } = 2.0; // GPU should be at least 2x faster
}

/// <summary>
/// Result caching configuration
/// </summary>
public class CacheConfiguration
{
    public bool EnableCaching { get; set; } = true;
    public int MaxCacheSize { get; set; } = 10000;
    public TimeSpan DefaultCacheExpiry { get; set; } = TimeSpan.FromMinutes(15);
    public bool EnableDistributedCache { get; set; } = false;
    public string? RedisConnectionString { get; set; }
    
    // Cache expiry overrides for different calculation types
    public Dictionary<string, TimeSpan> CacheExpiryOverrides { get; set; } = new()
    {
        { "PortfolioValue", TimeSpan.FromMinutes(5) },
        { "MarketData", TimeSpan.FromMinutes(1) },
        { "OptionPrice", TimeSpan.FromMinutes(10) },
        { "RiskMetrics", TimeSpan.FromMinutes(30) },
        { "PerformanceAttribution", TimeSpan.FromHours(1) }
    };
    
    // Cache size limits by calculation type
    public Dictionary<string, int> CacheSizeLimits { get; set; } = new()
    {
        { "PortfolioValue", 1000 },
        { "MarketData", 5000 },
        { "OptionPrice", 2000 },
        { "RiskMetrics", 500 },
        { "PerformanceAttribution", 100 }
    };
}

/// <summary>
/// Regulatory compliance configuration
/// </summary>
public class ComplianceConfiguration
{
    public bool EnableSOXCompliance { get; set; } = true;
    public bool EnableMiFIDCompliance { get; set; } = true;
    public bool EnableBaselCompliance { get; set; } = true;
    public bool EnableGDPRCompliance { get; set; } = true;
    
    // Audit trail configuration
    public bool EnableAuditTrail { get; set; } = true;
    public TimeSpan AuditTrailRetention { get; set; } = TimeSpan.FromDays(2555); // 7 years
    public bool EnableAuditTrailEncryption { get; set; } = true;
    public bool EnableAuditTrailSigning { get; set; } = true;
    
    // Data retention policies
    public TimeSpan CalculationResultRetention { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan PerformanceMetricsRetention { get; set; } = TimeSpan.FromDays(90);
    public TimeSpan ErrorLogRetention { get; set; } = TimeSpan.FromDays(30);
    
    // Regulatory reporting
    public bool EnableRegulatoryReporting { get; set; } = true;
    public string RegulatoryReportingPath { get; set; } = "./reports/regulatory";
    public TimeSpan ReportingFrequency { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Performance thresholds and monitoring configuration
/// </summary>
public class PerformanceConfiguration
{
    public double MaxLatencyMs { get; set; } = 1000.0;
    public double MaxMemoryUsageMB { get; set; } = 1024.0;
    public double MaxCpuUsagePercent { get; set; } = 80.0;
    public int MaxConcurrentCalculations { get; set; } = 100;
    
    // Performance monitoring
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public TimeSpan PerformanceMetricsInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int PerformanceHistoryRetentionDays { get; set; } = 30;
    
    // Alerting thresholds
    public double LatencyAlertThresholdMs { get; set; } = 5000.0;
    public double MemoryAlertThresholdMB { get; set; } = 2048.0;
    public double ErrorRateAlertThresholdPercent { get; set; } = 5.0;
    
    // Load balancing
    public bool EnableLoadBalancing { get; set; } = true;
    public int LoadBalancingQueueSize { get; set; } = 1000;
    public TimeSpan LoadBalancingTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Risk calculation specific configuration
/// </summary>
public class RiskConfiguration
{
    // VaR calculation settings
    public double DefaultVaRConfidenceLevel { get; set; } = 0.95;
    public int DefaultVaRHistoryDays { get; set; } = 252; // 1 year of trading days
    public int DefaultMonteCarloSimulations { get; set; } = 10000;
    public int MaxMonteCarloSimulations { get; set; } = 100000;
    
    // Risk limits
    public decimal MaxPortfolioVaR { get; set; } = 0.05m; // 5% of portfolio value
    public decimal MaxPositionWeight { get; set; } = 0.10m; // 10% of portfolio
    public decimal MaxSectorExposure { get; set; } = 0.30m; // 30% of portfolio
    public decimal MaxLeverageRatio { get; set; } = 2.0m;
    
    // Stress testing
    public bool EnableStressTesting { get; set; } = true;
    public List<StressTestScenario> StressTestScenarios { get; set; } = new()
    {
        new StressTestScenario { Name = "Market Crash", MarketShock = -0.20m },
        new StressTestScenario { Name = "Interest Rate Spike", InterestRateShock = 0.02m },
        new StressTestScenario { Name = "Volatility Spike", VolatilityShock = 0.50m }
    };
    
    // Correlation settings
    public int CorrelationHistoryDays { get; set; } = 60;
    public double MinCorrelationConfidence { get; set; } = 0.80;
    public bool EnableDynamicCorrelation { get; set; } = true;
}

/// <summary>
/// Input validation configuration
/// </summary>
public class ValidationConfiguration
{
    public bool EnableStrictValidation { get; set; } = true;
    public bool EnableDataSanityChecks { get; set; } = true;
    public bool EnableOutlierDetection { get; set; } = true;
    
    // Validation thresholds
    public decimal MaxPriceValue { get; set; } = 1000000m;
    public decimal MaxQuantity { get; set; } = 10000000m;
    public decimal MaxPortfolioValue { get; set; } = 1000000000m;
    public double MaxVolatility { get; set; } = 5.0; // 500%
    public double MaxInterestRate { get; set; } = 0.50; // 50%
    
    // Outlier detection
    public double OutlierThresholdStdDev { get; set; } = 3.0;
    public int MinDataPointsForOutlierDetection { get; set; } = 20;
    public bool EnableOutlierRemoval { get; set; } = false; // Just flag, don't remove
    
    // Data quality checks
    public bool EnableMissingDataChecks { get; set; } = true;
    public double MaxMissingDataRatio { get; set; } = 0.05; // 5% missing data allowed
    public bool EnableDataFreshnesChecks { get; set; } = true;
    public TimeSpan MaxDataAge { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Stress test scenario configuration
/// </summary>
public class StressTestScenario
{
    public string Name { get; set; } = string.Empty;
    public decimal MarketShock { get; set; }
    public decimal InterestRateShock { get; set; }
    public decimal VolatilityShock { get; set; }
    public decimal CurrencyShock { get; set; }
    public Dictionary<string, decimal> SectorShocks { get; set; } = new();
    public Dictionary<string, decimal> CustomShocks { get; set; } = new();
}