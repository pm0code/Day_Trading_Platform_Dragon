// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\ILogger.cs
namespace TradingPlatform.Core.Interfaces
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
        void LogDebug(string message);
        void LogTrace(string message);
        void LogTrade(string symbol, decimal price, int quantity, string action);
        void LogPerformance(string operation, TimeSpan duration);
    }
}
// 14 lines
