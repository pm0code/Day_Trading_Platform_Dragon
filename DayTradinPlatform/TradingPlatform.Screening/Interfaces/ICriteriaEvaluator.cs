// File: TradingPlatform.Screening.Interfaces\ICriteriaEvaluator.cs

using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using ScreeningCriteriaResult = TradingPlatform.Screening.Models.CriteriaResult;

namespace TradingPlatform.Screening.Interfaces
{
    /// <summary>
    /// Defines the contract for all criteria evaluators.
    /// All implementations must return mathematically correct, standards-compliant ScreeningCriteriaResult objects.
    /// </summary>
    public interface ICriteriaEvaluator
    {
        /// <summary>
        /// Evaluates the given market data and trading criteria, returning a unified ScreeningCriteriaResult.
        /// </summary>
        Task<ScreeningCriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria);
    }
}

// Total Lines: 18
