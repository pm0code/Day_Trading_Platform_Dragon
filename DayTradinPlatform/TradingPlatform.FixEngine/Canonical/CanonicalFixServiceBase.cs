using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;

namespace TradingPlatform.FixEngine.Canonical
{
    /// <summary>
    /// Base class for all FIX protocol services providing canonical implementation patterns.
    /// Ensures consistent logging, error handling, and lifecycle management across all FIX components.
    /// </summary>
    /// <remarks>
    /// All FIX services MUST extend this base class to ensure compliance with mandatory standards.
    /// Provides automatic method logging, health checks, and metrics collection.
    /// </remarks>
    public abstract class CanonicalFixServiceBase : CanonicalServiceBase
    {
        private readonly IFixPerformanceMonitor _performanceMonitor;
        
        /// <summary>
        /// Initializes a new instance of the CanonicalFixServiceBase class.
        /// </summary>
        /// <param name="logger">The trading logger instance for structured logging</param>
        /// <param name="serviceName">The name of the FIX service for identification</param>
        /// <param name="performanceMonitor">Optional performance monitor for latency tracking</param>
        protected CanonicalFixServiceBase(
            ITradingLogger logger, 
            string serviceName,
            IFixPerformanceMonitor? performanceMonitor = null) 
            : base(logger, $"FIX.{serviceName}")
        {
            _performanceMonitor = performanceMonitor ?? new NullFixPerformanceMonitor();
        }
        
        /// <summary>
        /// Validates FIX message checksum according to FIX protocol standards.
        /// </summary>
        /// <param name="message">The FIX message bytes to validate</param>
        /// <returns>True if checksum is valid, false otherwise</returns>
        protected bool ValidateChecksum(ReadOnlySpan<byte> message)
        {
            LogMethodEntry();
            
            try
            {
                if (message.Length < 7) // Minimum FIX message length
                {
                    _logger.LogWarning("FIX message too short for checksum validation");
                    LogMethodExit();
                    return false;
                }
                
                // FIX checksum calculation
                int checksum = 0;
                int checksumFieldStart = message.Length - 7; // "10=XXX\x01"
                
                for (int i = 0; i < checksumFieldStart; i++)
                {
                    checksum += message[i];
                }
                
                checksum %= 256;
                
                // Extract expected checksum from message
                var expectedChecksum = ParseChecksum(message.Slice(checksumFieldStart));
                
                var isValid = checksum == expectedChecksum;
                
                if (!isValid)
                {
                    _logger.LogWarning("FIX checksum validation failed. Expected: {Expected}, Actual: {Actual}", 
                        expectedChecksum, checksum);
                }
                
                LogMethodExit();
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating FIX checksum");
                LogMethodExit();
                return false;
            }
        }
        
        /// <summary>
        /// Starts a performance monitoring activity for FIX operations.
        /// </summary>
        /// <param name="operationName">The name of the operation being monitored</param>
        /// <returns>A disposable activity that records timing when disposed</returns>
        protected IDisposable StartActivity(string operationName)
        {
            return _performanceMonitor.StartActivity($"{ServiceName}.{operationName}");
        }
        
        /// <summary>
        /// Records a FIX-specific metric value.
        /// </summary>
        /// <param name="metricName">The name of the metric</param>
        /// <param name="value">The metric value</param>
        /// <param name="tags">Optional tags for the metric</param>
        protected void RecordMetric(string metricName, decimal value, params (string Key, string Value)[] tags)
        {
            _performanceMonitor.RecordMetric($"{ServiceName}.{metricName}", value, tags);
            
            _logger.LogDebug("FIX metric recorded: {Metric}={Value}", metricName, value);
        }
        
        /// <summary>
        /// Gets the current hardware timestamp for ultra-precise timing.
        /// </summary>
        /// <returns>Hardware timestamp in microseconds</returns>
        protected long GetHardwareTimestamp()
        {
            return _performanceMonitor.GetHardwareTimestamp();
        }
        
        /// <summary>
        /// Performs FIX-specific health checks.
        /// </summary>
        /// <returns>Health check result indicating service status</returns>
        public new async Task<ServiceHealthCheck> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                var baseHealth = await base.CheckHealthAsync(cancellationToken);
                if (!baseHealth.IsHealthy)
                {
                    LogMethodExit();
                    return baseHealth;
                }
                
                // Add FIX-specific health checks
                var fixHealthResult = await CheckFixHealthAsync();
                
                // Update the base health check with FIX-specific details
                if (!fixHealthResult.IsHealthy)
                {
                    baseHealth.IsHealthy = false;
                    baseHealth.HealthMessage = fixHealthResult.Message;
                    baseHealth.Details = fixHealthResult.Details;
                }
                
                LogMethodExit();
                return baseHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FIX health check failed");
                LogMethodExit();
                return new ServiceHealthCheck
                {
                    ServiceName = ServiceName,
                    CurrentState = ServiceState,
                    IsHealthy = false,
                    HealthMessage = $"FIX health check error: {ex.Message}",
                    CheckedAt = DateTime.UtcNow
                };
            }
        }
        
        /// <summary>
        /// Override this method to implement FIX-specific health checks.
        /// </summary>
        /// <returns>Health check result for FIX components</returns>
        protected virtual Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> CheckFixHealthAsync()
        {
            return Task.FromResult((true, "FIX service operational", (Dictionary<string, object>?)null));
        }
        
        /// <summary>
        /// Parses checksum value from FIX message tail.
        /// </summary>
        private int ParseChecksum(ReadOnlySpan<byte> checksumField)
        {
            // Parse "10=XXX\x01" format
            if (checksumField.Length >= 7 && 
                checksumField[0] == '1' && 
                checksumField[1] == '0' && 
                checksumField[2] == '=')
            {
                int checksum = 0;
                checksum += (checksumField[3] - '0') * 100;
                checksum += (checksumField[4] - '0') * 10;
                checksum += (checksumField[5] - '0');
                return checksum;
            }
            
            return -1;
        }
    }
    
    /// <summary>
    /// Performance monitor interface for FIX operations.
    /// </summary>
    public interface IFixPerformanceMonitor
    {
        IDisposable StartActivity(string operationName);
        void RecordMetric(string metricName, decimal value, params (string Key, string Value)[] tags);
        long GetHardwareTimestamp();
    }
    
    /// <summary>
    /// Null implementation of performance monitor for when monitoring is disabled.
    /// </summary>
    internal class NullFixPerformanceMonitor : IFixPerformanceMonitor
    {
        private sealed class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
        
        public IDisposable StartActivity(string operationName) => new NullDisposable();
        public void RecordMetric(string metricName, decimal value, params (string Key, string Value)[] tags) { }
        public long GetHardwareTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    }
}