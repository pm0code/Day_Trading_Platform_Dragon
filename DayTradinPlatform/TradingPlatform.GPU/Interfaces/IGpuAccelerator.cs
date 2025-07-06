// Temporarily removed Core dependency
using TradingPlatform.GPU.Models;

namespace TradingPlatform.GPU.Interfaces;

/// <summary>
/// Interface for GPU-accelerated operations in the trading platform
/// </summary>
public interface IGpuAccelerator : IDisposable
{
    /// <summary>
    /// Gets information about the current GPU device
    /// </summary>
    GpuDeviceInfo DeviceInfo { get; }

    /// <summary>
    /// Checks if GPU acceleration is available
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    /// Performs batch technical indicator calculations on GPU
    /// </summary>
    Task<TechnicalIndicatorResults> CalculateTechnicalIndicatorsAsync(
        string[] symbols,
        decimal[][] prices,
        int[] periods,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs parallel screening of stocks based on criteria
    /// </summary>
    Task<ScreeningResults> ScreenStocksAsync(
        object[] stocks,
        ScreeningCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates risk metrics in parallel
    /// </summary>
    Task<RiskMetrics[]> CalculateRiskMetricsAsync(
        object[] portfolios,
        object marketData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs Monte Carlo simulations for option pricing
    /// </summary>
    Task<MonteCarloResults> RunMonteCarloSimulationAsync(
        object[] options,
        int simulations,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Results from technical indicator calculations
/// </summary>
public record TechnicalIndicatorResults
{
    public string[] Symbols { get; init; } = Array.Empty<string>();
    public Dictionary<string, decimal[]> SMA { get; init; } = new();
    public Dictionary<string, decimal[]> EMA { get; init; } = new();
    public Dictionary<string, decimal[]> RSI { get; init; } = new();
    public Dictionary<string, decimal[]> MACD { get; init; } = new();
    public Dictionary<string, decimal[]> BollingerBands { get; init; } = new();
    public TimeSpan CalculationTime { get; init; }
}

/// <summary>
/// Stock screening results
/// </summary>
public record ScreeningResults
{
    public string[] MatchingSymbols { get; init; } = Array.Empty<string>();
    public Dictionary<string, ScreeningScore> Scores { get; init; } = new();
    public int TotalScreened { get; init; }
    public TimeSpan ScreeningTime { get; init; }
}

/// <summary>
/// Individual stock screening score
/// </summary>
public record ScreeningScore
{
    public decimal TotalScore { get; init; }
    public Dictionary<string, decimal> CriteriaScores { get; init; } = new();
    public bool PassedAllCriteria { get; init; }
}

/// <summary>
/// Risk metrics calculation results
/// </summary>
public record RiskMetrics
{
    public string PortfolioId { get; init; } = string.Empty;
    public decimal ValueAtRisk { get; init; }
    public decimal ExpectedShortfall { get; init; }
    public decimal Beta { get; init; }
    public decimal Sharpe { get; init; }
    public decimal MaxDrawdown { get; init; }
    public Dictionary<string, decimal> PositionRisks { get; init; } = new();
}

/// <summary>
/// Monte Carlo simulation results
/// </summary>
public record MonteCarloResults
{
    public Dictionary<string, OptionPrice> OptionPrices { get; init; } = new();
    public Dictionary<string, decimal[]> PricePaths { get; init; } = new();
    public int SimulationsRun { get; init; }
    public TimeSpan SimulationTime { get; init; }
}

/// <summary>
/// Option pricing result
/// </summary>
public record OptionPrice
{
    public decimal Price { get; init; }
    public decimal Delta { get; init; }
    public decimal Gamma { get; init; }
    public decimal Theta { get; init; }
    public decimal Vega { get; init; }
    public decimal Rho { get; init; }
    public decimal StandardError { get; init; }
}

/// <summary>
/// Screening criteria for stock selection
/// </summary>
public record ScreeningCriteria
{
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public decimal? MinVolume { get; init; }
    public decimal? MinMarketCap { get; init; }
    public decimal? MinPriceChange { get; init; }
    public decimal? MaxPERatio { get; init; }
    public decimal? MinDividendYield { get; init; }
    public TechnicalCriteria? Technical { get; init; }
}

/// <summary>
/// Technical analysis criteria
/// </summary>
public record TechnicalCriteria
{
    public bool? AboveSMA50 { get; init; }
    public bool? AboveSMA200 { get; init; }
    public decimal? MinRSI { get; init; }
    public decimal? MaxRSI { get; init; }
    public bool? MACDBullishCrossover { get; init; }
    public bool? BollingerBandSqueeze { get; init; }
}