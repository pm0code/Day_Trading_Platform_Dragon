// File: TradingPlatform.Core\Interfaces\ICriteriaEvaluator.cs

using System.Threading.Tasks;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Core.Interfaces
{
    /// <summary>
    /// Interface for criteria evaluation
    /// </summary>
    public interface ICriteriaEvaluator
    {
        /// <summary>
        /// Evaluates market data against trading criteria
        /// </summary>
        Task<CriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria);
    }
}