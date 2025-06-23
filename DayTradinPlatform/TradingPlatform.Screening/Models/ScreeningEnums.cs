using System;

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// Defines the operational mode for screening activities.
    /// </summary>
    public enum ScreeningMode
    {
        /// <summary>
        /// Real-time screening with continuous updates
        /// </summary>
        RealTime,
        
        /// <summary>
        /// Batch screening at scheduled intervals
        /// </summary>
        Batch,
        
        /// <summary>
        /// Historical screening for backtesting
        /// </summary>
        Historical,
        
        /// <summary>
        /// On-demand single screening
        /// </summary>
        OnDemand
    }

    /// <summary>
    /// Defines the status of a screening operation
    /// </summary>
    public enum ScreeningStatus
    {
        /// <summary>
        /// Screening is pending execution
        /// </summary>
        Pending,
        
        /// <summary>
        /// Screening is currently running
        /// </summary>
        Running,
        
        /// <summary>
        /// Screening completed successfully
        /// </summary>
        Completed,
        
        /// <summary>
        /// Screening failed with errors
        /// </summary>
        Failed,
        
        /// <summary>
        /// Screening was cancelled by user
        /// </summary>
        Cancelled
    }
}