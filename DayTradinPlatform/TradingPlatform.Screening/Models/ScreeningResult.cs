// File: TradingPlatform.Screening.Models\ScreeningResult.cs

using TradingPlatform.Core.Models;

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// Represents the result of screening a single symbol, aggregating all criteria results and scoring.
    /// All references to CriteriaResult and AlertLevel are to the canonical models in CriteriaResult.cs.
    /// </summary>
    public class ScreeningResult
    {
        public string Symbol { get; set; } = string.Empty;
        public MarketData MarketData { get; set; } = null!;
        public bool MeetsCriteria { get; set; }
        public decimal OverallScore { get; set; } // 0.0 to 1.0
        public List<CriteriaResult> CriteriaResults { get; set; } = new();
        public AlertLevel AlertLevel { get; set; } = AlertLevel.None;
        public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan ProcessingTime { get; set; }
        public string MarketCode { get; set; } = "US";
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
        public List<string> FailedCriteria { get; set; } = new();
        public List<string> PassedCriteria { get; set; } = new();
        public string RecommendedAction { get; set; } = "WATCH"; // "BUY", "SELL", "WATCH", "IGNORE"
        public decimal ConfidenceLevel { get; set; } // 0.0 to 1.0
    }
}

// Total Lines: 27
