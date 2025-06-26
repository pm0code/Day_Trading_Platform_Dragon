using System;
using System.Collections.Generic;

namespace TradingPlatform.ML.Common
{
    /// <summary>
    /// Portfolio definition for ML algorithms
    /// </summary>
    public class Portfolio
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, PortfolioHolding> Holdings { get; set; } = new();
        public decimal TotalValue { get; set; }
        public decimal CashBalance { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Individual holding in a portfolio
    /// </summary>
    public class PortfolioHolding
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue => Quantity * CurrentPrice;
        public decimal UnrealizedPnL => (CurrentPrice - AveragePrice) * Quantity;
        public decimal Weight { get; set; } // Portfolio weight
        public string AssetClass { get; set; } = "Equity";
        public string Sector { get; set; } = "Unknown";
        public Dictionary<string, object> Attributes { get; set; } = new();
    }
}