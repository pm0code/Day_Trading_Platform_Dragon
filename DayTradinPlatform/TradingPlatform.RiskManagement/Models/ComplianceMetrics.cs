namespace TradingPlatform.RiskManagement.Models;

/// <summary>
/// Comprehensive metrics tracking for compliance monitoring service
/// </summary>
public class ComplianceMetrics
{
    /// <summary>
    /// Total number of compliance violations recorded
    /// </summary>
    public long TotalViolations { get; set; }
    
    /// <summary>
    /// Number of Pattern Day Trading violations
    /// </summary>
    public long PDTViolations { get; set; }
    
    /// <summary>
    /// Number of margin requirement violations
    /// </summary>
    public long MarginViolations { get; set; }
    
    /// <summary>
    /// Number of regulatory limit violations
    /// </summary>
    public long RegulatoryViolations { get; set; }
    
    /// <summary>
    /// Number of currently active violations (last 24 hours)
    /// </summary>
    public int ActiveViolations { get; set; }
    
    /// <summary>
    /// Total number of compliance events tracked
    /// </summary>
    public int TotalEvents { get; set; }
    
    /// <summary>
    /// Timestamp of metrics snapshot
    /// </summary>
    public DateTime Timestamp { get; set; }
}