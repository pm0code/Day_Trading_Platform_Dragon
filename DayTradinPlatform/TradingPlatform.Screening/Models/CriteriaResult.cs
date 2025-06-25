// File: TradingPlatform.Screening.Models\CriteriaResult.cs

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// The single authoritative result model for all criteria evaluations.
    /// </summary>
    public class CriteriaResult
    {
        public string CriteriaName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public decimal Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new();
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
        public decimal Confidence { get; set; } = 1.0m;
        public AlertLevel AlertLevel { get; set; } = AlertLevel.None;
    }

    /// <summary>
    /// Alert levels for criteria results.
    /// </summary>
    public enum AlertLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}

// Total Lines: 30
