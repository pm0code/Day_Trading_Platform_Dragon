using TradingPlatform.Core.Models;

namespace TradingPlatform.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for canonical criteria evaluators.
    /// </summary>
    public interface ICanonicalCriteriaEvaluator
    {
        /// <summary>
        /// Evaluates the given market data and trading criteria, returning a CriteriaResult.
        /// </summary>
        Task<CriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria);
    }
}