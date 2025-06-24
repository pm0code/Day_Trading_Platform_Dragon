// File: TradingPlatform.Screening.Interfaces\ICriteriaEvaluator.cs

using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Interfaces
{
    /// <summary>
    /// Defines the contract for all criteria evaluators.
    /// All implementations must return mathematically correct, standards-compliant CriteriaResult objects.
    /// </summary>
    public interface ICriteriaEvaluator
    {
        /// <summary>
        /// Evaluates the given market data and trading criteria, returning a unified CriteriaResult.
        /// </summary>
        Task<TradingPlatform.Screening.Models.CriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria);
    }
}

// Total Lines: 18
