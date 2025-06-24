// File: TradingPlatform.Core\Models\CriteriaResult.cs

using System;
using System.Collections.Generic;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Result of a criteria evaluation
    /// </summary>
    public class CriteriaResult
    {
        public string CriteriaName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public decimal Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime EvaluatedAt { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}