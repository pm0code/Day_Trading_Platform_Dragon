// File: TradingPlatform.Screening.Interfaces\IScreeningEngine.cs

using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Interfaces
{
    /// <summary>
    /// Defines the contract for all screening engines in the system.
    /// All implementations must use canonical models and be mathematically standards-compliant.
    /// </summary>
    public interface IScreeningEngine
    {
        Task<List<ScreeningResult>> ScreenSymbolsAsync(ScreeningRequest request);
        Task<ScreeningResult> EvaluateSymbolAsync(string symbol, TradingCriteria criteria);
        IObservable<ScreeningResult> StartRealTimeScreeningAsync(ScreeningRequest request);
        Task StopRealTimeScreeningAsync();
        Task<List<ScreeningResult>> GetScreeningHistoryAsync(string symbol, DateTime from, DateTime to);
        Task<bool> IsScreeningActiveAsync();
        Task<Dictionary<string, object>> GetScreeningMetricsAsync();
    }
}

// Total Lines: 22
