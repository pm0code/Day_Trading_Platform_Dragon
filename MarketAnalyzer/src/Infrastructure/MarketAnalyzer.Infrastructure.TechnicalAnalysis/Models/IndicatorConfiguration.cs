namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Models;

/// <summary>
/// Configuration for technical indicators.
/// </summary>
public class IndicatorConfiguration
{
    /// <summary>
    /// Gets or sets the indicator name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the calculation period.
    /// </summary>
    public int Period { get; set; }

    /// <summary>
    /// Gets or sets additional parameters for the indicator.
    /// </summary>
    public Dictionary<string, object> Parameters { get; } = new();

    /// <summary>
    /// Gets or sets whether the indicator is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache time-to-live in seconds.
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether calculation is hot (stable).
    /// </summary>
    public bool IsHot { get; set; }
}