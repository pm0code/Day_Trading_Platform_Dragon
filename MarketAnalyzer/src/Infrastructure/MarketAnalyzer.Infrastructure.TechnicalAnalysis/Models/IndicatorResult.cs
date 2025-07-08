namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Models;

/// <summary>
/// Result of an indicator calculation.
/// </summary>
public class IndicatorResult
{
    /// <summary>
    /// Gets or sets the symbol for which the indicator was calculated.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the indicator name.
    /// </summary>
    public string IndicatorName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the calculated value.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the calculation.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the calculation is hot (stable).
    /// </summary>
    public bool IsHot { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the result.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Gets or sets the calculation time in milliseconds.
    /// </summary>
    public long CalculationTimeMs { get; set; }
}