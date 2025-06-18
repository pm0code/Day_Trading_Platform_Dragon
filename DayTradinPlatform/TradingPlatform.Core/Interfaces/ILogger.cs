// TradingPlatform.Core.Interfaces.ILogger - Comprehensive Logging Interface
// Enhanced for maximum operational intelligence, troubleshooting, and performance monitoring

using System.Runtime.CompilerServices;

namespace TradingPlatform.Core.Interfaces
{
    /// <summary>
    /// Comprehensive logging interface designed for day trading platform operations.
    /// Provides rich context, performance metrics, and actionable troubleshooting information.
    /// All log entries include automatic method/class context, timestamps, and relevant metadata.
    /// </summary>
    public interface ILogger
    {
        #region Core Logging Methods with Rich Context
        
        /// <summary>
        /// Logs informational message with full method context and execution metadata
        /// </summary>
        /// <param name="message">Descriptive message about the operation</param>
        /// <param name="additionalData">Optional structured data for enhanced context</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        /// <param name="sourceFilePath">Auto-populated source file path</param>
        /// <param name="sourceLineNumber">Auto-populated source line number</param>
        void LogInfo(string message, 
                    object? additionalData = null,
                    [CallerMemberName] string memberName = "",
                    [CallerFilePath] string sourceFilePath = "",
                    [CallerLineNumber] int sourceLineNumber = 0);
        
        /// <summary>
        /// Logs warning with context and potential impact assessment
        /// </summary>
        /// <param name="message">Warning description</param>
        /// <param name="impact">Potential business/operational impact</param>
        /// <param name="recommendedAction">Suggested remediation steps</param>
        /// <param name="additionalData">Structured context data</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        /// <param name="sourceFilePath">Auto-populated source file path</param>
        /// <param name="sourceLineNumber">Auto-populated source line number</param>
        void LogWarning(string message,
                       string? impact = null,
                       string? recommendedAction = null, 
                       object? additionalData = null,
                       [CallerMemberName] string memberName = "",
                       [CallerFilePath] string sourceFilePath = "",
                       [CallerLineNumber] int sourceLineNumber = 0);
        
        /// <summary>
        /// Logs error with comprehensive diagnostic information
        /// </summary>
        /// <param name="message">Error description</param>
        /// <param name="exception">Exception details if available</param>
        /// <param name="operationContext">What operation was being performed</param>
        /// <param name="userImpact">How this affects trading operations</param>
        /// <param name="troubleshootingHints">Specific guidance for resolution</param>
        /// <param name="additionalData">Structured diagnostic data</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        /// <param name="sourceFilePath">Auto-populated source file path</param>
        /// <param name="sourceLineNumber">Auto-populated source line number</param>
        void LogError(string message,
                     Exception? exception = null,
                     string? operationContext = null,
                     string? userImpact = null,
                     string? troubleshootingHints = null,
                     object? additionalData = null,
                     [CallerMemberName] string memberName = "",
                     [CallerFilePath] string sourceFilePath = "",
                     [CallerLineNumber] int sourceLineNumber = 0);
        
        #endregion
        
        #region Trading-Specific Operations
        
        /// <summary>
        /// Logs trading operations with comprehensive audit trail information
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="action">Trading action (BUY/SELL/CANCEL/MODIFY)</param>
        /// <param name="quantity">Number of shares/contracts</param>
        /// <param name="price">Execution or limit price</param>
        /// <param name="orderId">Unique order identifier</param>
        /// <param name="strategy">Trading strategy that generated this action</param>
        /// <param name="executionTime">Time to execute operation</param>
        /// <param name="marketConditions">Relevant market context</param>
        /// <param name="riskMetrics">Risk assessment data</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogTrade(string symbol,
                     string action,
                     decimal quantity,
                     decimal price,
                     string? orderId = null,
                     string? strategy = null,
                     TimeSpan? executionTime = null,
                     object? marketConditions = null,
                     object? riskMetrics = null,
                     [CallerMemberName] string memberName = "");
        
        /// <summary>
        /// Logs position changes with full audit trail
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="oldPosition">Previous position size</param>
        /// <param name="newPosition">New position size</param>
        /// <param name="reason">Reason for position change</param>
        /// <param name="pnlImpact">P&L impact of the change</param>
        /// <param name="riskImpact">Risk impact assessment</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogPositionChange(string symbol,
                              decimal oldPosition,
                              decimal newPosition,
                              string reason,
                              decimal? pnlImpact = null,
                              object? riskImpact = null,
                              [CallerMemberName] string memberName = "");
        
        #endregion
        
        #region Performance & Health Monitoring
        
        /// <summary>
        /// Logs performance metrics with comprehensive timing and resource usage
        /// </summary>
        /// <param name="operation">Name of the operation being measured</param>
        /// <param name="duration">Execution time</param>
        /// <param name="success">Whether operation completed successfully</param>
        /// <param name="throughput">Operations per second if applicable</param>
        /// <param name="resourceUsage">Memory/CPU usage if relevant</param>
        /// <param name="businessMetrics">Trading-specific metrics</param>
        /// <param name="comparisonTarget">Performance target for comparison</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogPerformance(string operation,
                           TimeSpan duration,
                           bool success = true,
                           double? throughput = null,
                           object? resourceUsage = null,
                           object? businessMetrics = null,
                           TimeSpan? comparisonTarget = null,
                           [CallerMemberName] string memberName = "");
        
        /// <summary>
        /// Logs system health indicators and operational status
        /// </summary>
        /// <param name="component">System component being monitored</param>
        /// <param name="status">Current health status</param>
        /// <param name="metrics">Relevant health metrics</param>
        /// <param name="alerts">Any active alerts or concerns</param>
        /// <param name="recommendedActions">Suggested maintenance actions</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogHealth(string component,
                      string status,
                      object? metrics = null,
                      string[]? alerts = null,
                      string[]? recommendedActions = null,
                      [CallerMemberName] string memberName = "");
        
        #endregion
        
        #region Risk & Compliance
        
        /// <summary>
        /// Logs risk events and compliance monitoring
        /// </summary>
        /// <param name="riskType">Type of risk (Position, Market, Operational)</param>
        /// <param name="severity">Risk severity level</param>
        /// <param name="description">Detailed risk description</param>
        /// <param name="currentExposure">Current risk exposure</param>
        /// <param name="riskLimit">Applicable risk limit</param>
        /// <param name="mitigationActions">Actions taken or recommended</param>
        /// <param name="regulatoryImplications">Compliance considerations</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogRisk(string riskType,
                    string severity,
                    string description,
                    decimal? currentExposure = null,
                    decimal? riskLimit = null,
                    string[]? mitigationActions = null,
                    string? regulatoryImplications = null,
                    [CallerMemberName] string memberName = "");
        
        #endregion
        
        #region Data Pipeline & Market Data
        
        /// <summary>
        /// Logs data pipeline operations with transformation details
        /// </summary>
        /// <param name="pipeline">Pipeline name/identifier</param>
        /// <param name="stage">Current processing stage</param>
        /// <param name="recordsProcessed">Number of records processed</param>
        /// <param name="dataQuality">Data quality metrics</param>
        /// <param name="latencyMetrics">Processing latency information</param>
        /// <param name="errors">Any errors encountered</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogDataPipeline(string pipeline,
                            string stage,
                            int recordsProcessed,
                            object? dataQuality = null,
                            object? latencyMetrics = null,
                            string[]? errors = null,
                            [CallerMemberName] string memberName = "");
        
        /// <summary>
        /// Logs market data events with quality and timing information
        /// </summary>
        /// <param name="symbol">Market symbol</param>
        /// <param name="dataType">Type of market data (Quote, Trade, News)</param>
        /// <param name="source">Data provider source</param>
        /// <param name="latency">Data delivery latency</param>
        /// <param name="quality">Data quality assessment</param>
        /// <param name="volume">Data volume/size</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogMarketData(string symbol,
                          string dataType,
                          string source,
                          TimeSpan? latency = null,
                          string? quality = null,
                          object? volume = null,
                          [CallerMemberName] string memberName = "");
        
        #endregion
        
        #region Method Lifecycle (Entry/Exit)
        
        /// <summary>
        /// Logs method entry with parameters for execution tracing
        /// </summary>
        /// <param name="parameters">Method parameters (will be serialized)</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        /// <param name="sourceFilePath">Auto-populated source file path</param>
        void LogMethodEntry(object? parameters = null,
                           [CallerMemberName] string memberName = "",
                           [CallerFilePath] string sourceFilePath = "");
        
        /// <summary>
        /// Logs method exit with results and execution metrics
        /// </summary>
        /// <param name="result">Method result (will be serialized)</param>
        /// <param name="executionTime">Method execution time</param>
        /// <param name="success">Whether method completed successfully</param>
        /// <param name="memberName">Auto-populated calling method name</param>
        void LogMethodExit(object? result = null,
                          TimeSpan? executionTime = null,
                          bool success = true,
                          [CallerMemberName] string memberName = "");
        
        #endregion
    }
}
// 14 lines
