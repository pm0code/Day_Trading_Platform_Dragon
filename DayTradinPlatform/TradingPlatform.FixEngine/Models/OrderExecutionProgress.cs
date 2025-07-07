using System;

namespace TradingPlatform.FixEngine.Models
{
    /// <summary>
    /// Represents the progress of an order execution.
    /// </summary>
    public class OrderExecutionProgress
    {
        /// <summary>
        /// Gets or sets the order identifier.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the client order identifier.
        /// </summary>
        public string ClOrdId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the total quantity of the order.
        /// </summary>
        public decimal TotalQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the filled quantity so far.
        /// </summary>
        public decimal FilledQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the percentage complete (0-100).
        /// </summary>
        public decimal PercentComplete => TotalQuantity > 0 ? (FilledQuantity / TotalQuantity) * 100 : 0;
        
        /// <summary>
        /// Gets or sets the current order status.
        /// </summary>
        public OrderStatus OrderStatus { get; set; }
        
        /// <summary>
        /// Gets or sets the last update time.
        /// </summary>
        public DateTime LastUpdateTime { get; set; }
        
        /// <summary>
        /// Gets or sets a progress message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets estimated time remaining.
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }
}