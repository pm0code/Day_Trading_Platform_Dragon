// File: TradingPlatform.Screening\Models\ScreeningSummary.cs

using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// Represents a summary of a screening operation, aggregating results and statistics.
    /// All numeric values use decimal for precision, and all collections are strongly typed.
    /// </summary>
    public class ScreeningSummary
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public int TotalSymbolsScreened { get; set; }
        public int SymbolsMeetingCriteria { get; set; }
        public decimal AverageScore { get; set; }
        public List<ScreeningResult> TopResults { get; set; } = new();
        public Dictionary<AlertLevel, int> AlertDistribution { get; set; } = new();
        public List<string> ErrorMessages { get; set; } = new();

        /// <summary>
        /// Creates a ScreeningSummary from a list of ScreeningResult objects.
        /// </summary>
        public static ScreeningSummary CreateFromResults(List<ScreeningResult> results)
        {
            if (results == null || !results.Any())
                return new ScreeningSummary { ErrorMessages = { "No results provided." } };


            var summary = new ScreeningSummary
            {
                StartTime = results.Min(r => r.ScreenedAt),
                EndTime = results.Max(r => r.ScreenedAt),
                TotalSymbolsScreened = results.Count,
                SymbolsMeetingCriteria = results.Count(r => r.MeetsCriteria),
                AverageScore = results.Average(r => r.OverallScore),
                TopResults = results.OrderByDescending(r => r.OverallScore).Take(10).ToList(), // Top 10 by default
                AlertDistribution = results.GroupBy(r => r.AlertLevel)
                                          .ToDictionary(g => g.Key, g => g.Count())
            };

            return summary;
        }
    }
}

// Total Lines: 47
