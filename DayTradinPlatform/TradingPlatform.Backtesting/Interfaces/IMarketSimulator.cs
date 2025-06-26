using System;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Common;

namespace TradingPlatform.Backtesting.Interfaces
{
    /// <summary>
    /// Interface for simulating realistic market conditions during backtesting
    /// </summary>
    public interface IMarketSimulator
    {
        /// <summary>
        /// Simulate order fill with realistic market conditions
        /// </summary>
        Task<TradingResult<OrderFillResult>> SimulateOrderFillAsync(
            Order order,
            MarketSnapshot snapshot,
            OrderBook orderBook,
            MarketConditions conditions);

        /// <summary>
        /// Calculate slippage for an order
        /// </summary>
        decimal CalculateSlippage(
            Order order,
            MarketSnapshot snapshot,
            LiquidityProfile liquidity,
            MarketVolatility volatility);

        /// <summary>
        /// Calculate transaction costs including fees and commissions
        /// </summary>
        TransactionCosts CalculateTransactionCosts(
            Order order,
            OrderFillResult fill,
            BrokerageConfig brokerage);

        /// <summary>
        /// Calculate market impact of large orders
        /// </summary>
        MarketImpact CalculateMarketImpact(
            Order order,
            MarketDepth depth,
            LiquidityProfile liquidity);

        /// <summary>
        /// Determine if order would be rejected
        /// </summary>
        OrderRejectionResult CheckOrderRejection(
            Order order,
            MarketSnapshot snapshot,
            PortfolioState portfolio,
            RiskLimits limits);

        /// <summary>
        /// Reconstruct order book from historical data
        /// </summary>
        Task<OrderBook> ReconstructOrderBookAsync(
            string symbol,
            DateTime timestamp,
            IHistoricalDataProvider dataProvider);

        /// <summary>
        /// Get market conditions at specific time
        /// </summary>
        Task<MarketConditions> GetMarketConditionsAsync(
            DateTime timestamp,
            IHistoricalDataProvider dataProvider);
    }

    /// <summary>
    /// Result of order fill simulation
    /// </summary>
    public class OrderFillResult
    {
        public string OrderId { get; set; }
        public string Symbol { get; set; }
        public OrderStatus Status { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Slippage { get; set; }
        public TransactionCosts Costs { get; set; }
        public DateTime FillTime { get; set; }
        public List<PartialFill> PartialFills { get; set; }
        public string ExecutionVenue { get; set; }
    }

    /// <summary>
    /// Transaction cost breakdown
    /// </summary>
    public class TransactionCosts
    {
        public decimal Commission { get; set; }
        public decimal ExchangeFees { get; set; }
        public decimal SECFees { get; set; }
        public decimal SpreadCost { get; set; }
        public decimal MarketImpactCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    /// <summary>
    /// Market impact analysis
    /// </summary>
    public class MarketImpact
    {
        public decimal TemporaryImpact { get; set; }
        public decimal PermanentImpact { get; set; }
        public decimal TotalImpact { get; set; }
        public decimal ImpactDuration { get; set; }
        public decimal RecoveryTime { get; set; }
    }

    /// <summary>
    /// Current market conditions
    /// </summary>
    public class MarketConditions
    {
        public MarketRegime Regime { get; set; }
        public decimal Volatility { get; set; }
        public decimal Liquidity { get; set; }
        public decimal SpreadWidening { get; set; }
        public bool IsHoliday { get; set; }
        public MarketPhase Phase { get; set; }
        public Dictionary<string, decimal> SectorVolatility { get; set; }
    }

    public enum MarketPhase
    {
        PreMarket,
        MarketOpen,
        RegularHours,
        MarketClose,
        AfterHours
    }
}