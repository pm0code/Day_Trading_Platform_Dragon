using System;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Core.Logging
{
    /// <summary>
    /// Default implementation of ITradingLogger for the trading platform
    /// </summary>
    public class TradingLogger : ITradingLogger
    {
        private readonly ILogger<TradingLogger>? _logger;
        
        public TradingLogger(ILogger<TradingLogger>? logger = null)
        {
            _logger = logger;
        }
        
        public void LogMethodEntry(
            object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            _logger?.LogDebug($"[METHOD ENTRY] {memberName} - Parameters: {parameters}");
        }

        public void LogMethodExit(
            object? result = null,
            TimeSpan? duration = null,
            bool success = true,
            [CallerMemberName] string memberName = "")
        {
            var status = success ? "SUCCESS" : "FAILED";
            var durationStr = duration?.TotalMilliseconds.ToString("F2") ?? "N/A";
            _logger?.LogDebug($"[METHOD EXIT] {memberName} - Status: {status}, Duration: {durationStr}ms, Result: {result}");
        }

        public void LogInfo(
            string message,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _logger?.LogInformation($"[{memberName}] {message} - Data: {additionalData}");
        }

        public void LogDebug(
            string message,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _logger?.LogDebug($"[{memberName}] {message} - Data: {additionalData}");
        }

        public void LogWarning(
            string message,
            string? impact = null,
            string? troubleshooting = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _logger?.LogWarning($"[{memberName}] {message} - Impact: {impact}, Troubleshooting: {troubleshooting}, Data: {additionalData}");
        }

        public void LogError(
            string message,
            Exception? exception = null,
            string? operationContext = null,
            string? userImpact = null,
            string? troubleshootingHints = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _logger?.LogError(exception, $"[{memberName}] {message} - Context: {operationContext}, Impact: {userImpact}, Hints: {troubleshootingHints}, Data: {additionalData}");
        }
        
        public void LogTrade(
            string tradeType,
            string symbol,
            decimal quantity,
            decimal price,
            string? orderId = null,
            string? venue = null,
            TimeSpan? executionTime = null,
            object? fees = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogInformation($"[TRADE] {tradeType} {symbol} - Qty: {quantity}, Price: {price}, OrderId: {orderId}, Venue: {venue}, ExecTime: {executionTime?.TotalMilliseconds}ms");
        }

        public void LogPositionChange(
            string symbol,
            decimal oldPosition,
            decimal newPosition,
            string changeReason,
            decimal? pnl = null,
            object? additionalData = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogInformation($"[POSITION] {symbol} - Old: {oldPosition}, New: {newPosition}, Reason: {changeReason}, PnL: {pnl}");
        }

        public void LogPerformance(
            string operationName,
            TimeSpan duration,
            bool success,
            double? throughput = null,
            object? metrics = null,
            object? additionalData = null,
            TimeSpan? warningThreshold = null,
            [CallerMemberName] string memberName = "")
        {
            if (warningThreshold.HasValue && duration > warningThreshold.Value)
            {
                _logger?.LogWarning($"[PERFORMANCE] {operationName} - Duration: {duration.TotalMilliseconds}ms, Success: {success}, Throughput: {throughput}");
            }
            else
            {
                _logger?.LogInformation($"[PERFORMANCE] {operationName} - Duration: {duration.TotalMilliseconds}ms, Success: {success}, Throughput: {throughput}");
            }
        }

        public void LogHealth(
            string componentName,
            string status,
            object? metrics = null,
            string[]? dependencies = null,
            string[]? issues = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogInformation($"[HEALTH] {componentName} - Status: {status}, Dependencies: {dependencies?.Length ?? 0}, Issues: {issues?.Length ?? 0}");
        }

        public void LogRisk(
            string riskType,
            string severity,
            string description,
            decimal? value = null,
            decimal? threshold = null,
            string[]? mitigations = null,
            string? correlationId = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogWarning($"[RISK] {riskType} - Severity: {severity}, Value: {value}, Threshold: {threshold}, Description: {description}");
        }

        public void LogDataPipeline(
            string pipelineName,
            string stage,
            int recordsProcessed,
            object? inputMetrics = null,
            object? outputMetrics = null,
            string[]? errors = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogInformation($"[PIPELINE] {pipelineName}.{stage} - Records: {recordsProcessed}, Errors: {errors?.Length ?? 0}");
        }

        public void LogMarketData(
            string dataType,
            string symbol,
            string action,
            TimeSpan? latency = null,
            string? source = null,
            object? data = null,
            [CallerMemberName] string memberName = "")
        {
            _logger?.LogDebug($"[MARKET DATA] {dataType} {symbol} - Action: {action}, Latency: {latency?.TotalMilliseconds}ms, Source: {source}");
        }
    }
}