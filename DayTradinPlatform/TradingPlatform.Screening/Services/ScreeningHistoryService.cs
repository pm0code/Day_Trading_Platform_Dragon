// File: TradingPlatform.Screening.Services\ScreeningHistoryService.cs

using System.Collections.Concurrent;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Services
{
    /// <summary>
    /// Provides in-memory, standards-compliant storage and retrieval of historical screening results.
    /// All persisted data uses the unified CriteriaResult and ScreeningResult models.
    /// </summary>
    public class ScreeningHistoryService
    {
        // Key: Symbol, Value: List of screening results (most recent last)
        private readonly ConcurrentDictionary<string, List<ScreeningResult>> _history = new();

        /// <summary>
        /// Stores a new screening result for the given symbol.
        /// </summary>
        public void StoreResult(string symbol, ScreeningResult result)
        {
            if (string.IsNullOrWhiteSpace(symbol) || result == null)
                throw new ArgumentException("Symbol and result must be non-null.");

            _history.AddOrUpdate(
                symbol,
                _ => new List<ScreeningResult> { result },
                (_, list) =>
                {
                    list.Add(result);
                    // Optional: Limit history depth for memory management
                    if (list.Count > 1000) list.RemoveAt(0);
                    return list;
                });
        }

        /// <summary>
        /// Retrieves historical screening results for a symbol, optionally within a date range.
        /// </summary>
        public List<ScreeningResult> GetHistory(string symbol, DateTime? from = null, DateTime? to = null)
        {
            if (!_history.TryGetValue(symbol, out var list))
                return new List<ScreeningResult>();

            var filtered = list.AsEnumerable();
            if (from.HasValue)
                filtered = filtered.Where(r => r.ScreenedAt >= from.Value);
            if (to.HasValue)
                filtered = filtered.Where(r => r.ScreenedAt <= to.Value);

            return filtered.ToList();
        }

        /// <summary>
        /// Returns the most recent screening result for a symbol.
        /// </summary>
        public ScreeningResult GetLatest(string symbol)
        {
            if (!_history.TryGetValue(symbol, out var list) || !list.Any())
                return null;
            return list.Last();
        }

        /// <summary>
        /// Returns the number of stored results for a symbol.
        /// </summary>
        public int GetHistoryCount(string symbol)
        {
            return _history.TryGetValue(symbol, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Clears all historical data for a symbol.
        /// </summary>
        public void ClearHistory(string symbol)
        {
            _history.TryRemove(symbol, out _);
        }

        /// <summary>
        /// Lists all symbols with stored history.
        /// </summary>
        public IEnumerable<string> ListSymbols() => _history.Keys.ToList();
    }
}

// Total Lines: 61
