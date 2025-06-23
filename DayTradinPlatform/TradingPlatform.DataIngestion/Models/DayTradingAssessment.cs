using System;
using System.Collections.Generic;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Comprehensive assessment of a stock's suitability for day trading.
    /// Evaluates multiple criteria to determine if a stock meets day trading requirements.
    /// </summary>
    public class DayTradingAssessment
    {
        /// <summary>
        /// Stock symbol being assessed.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Overall suitability score (0-100).
        /// Higher scores indicate better day trading candidates.
        /// </summary>
        public decimal OverallScore { get; set; }

        /// <summary>
        /// Liquidity assessment based on volume and spread.
        /// </summary>
        public LiquidityAssessment Liquidity { get; set; } = new();

        /// <summary>
        /// Volatility assessment for profit potential.
        /// </summary>
        public VolatilityAssessment Volatility { get; set; } = new();

        /// <summary>
        /// Price action quality assessment.
        /// </summary>
        public PriceActionAssessment PriceAction { get; set; } = new();

        /// <summary>
        /// News and catalyst assessment.
        /// </summary>
        public CatalystAssessment Catalysts { get; set; } = new();

        /// <summary>
        /// Technical indicator assessment.
        /// </summary>
        public TechnicalAssessment Technical { get; set; } = new();

        /// <summary>
        /// Risk level classification.
        /// Values: "Low", "Medium", "High", "Very High"
        /// </summary>
        public string RiskLevel { get; set; } = "Medium";

        /// <summary>
        /// Recommended position size as percentage of capital.
        /// </summary>
        public decimal RecommendedPositionSize { get; set; }

        /// <summary>
        /// List of specific warnings or concerns.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// List of positive factors supporting trading.
        /// </summary>
        public List<string> PositiveFactors { get; set; } = new();

        /// <summary>
        /// Indicates if the stock meets minimum day trading criteria.
        /// </summary>
        public bool MeetsCriteria { get; set; }

        /// <summary>
        /// Suggested entry strategies based on assessment.
        /// </summary>
        public List<string> EntryStrategies { get; set; } = new();

        /// <summary>
        /// Suggested exit strategies based on assessment.
        /// </summary>
        public List<string> ExitStrategies { get; set; } = new();

        /// <summary>
        /// Optimal trading time windows based on historical patterns.
        /// </summary>
        public List<TradingWindow> OptimalTradingWindows { get; set; } = new();

        /// <summary>
        /// Timestamp of when the assessment was performed.
        /// </summary>
        public DateTime AssessmentTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Market condition at time of assessment.
        /// Values: "Bullish", "Bearish", "Neutral", "Volatile"
        /// </summary>
        public string MarketCondition { get; set; } = "Neutral";
    }

    /// <summary>
    /// Liquidity assessment details.
    /// </summary>
    public class LiquidityAssessment
    {
        public decimal Score { get; set; }
        public long AverageVolume { get; set; }
        public long CurrentVolume { get; set; }
        public decimal BidAskSpread { get; set; }
        public decimal SpreadPercentage { get; set; }
        public int MarketDepth { get; set; }
        public bool HasSufficientLiquidity { get; set; }
        public string LiquidityGrade { get; set; } = "C";
    }

    /// <summary>
    /// Volatility assessment details.
    /// </summary>
    public class VolatilityAssessment
    {
        public decimal Score { get; set; }
        public decimal IntradayVolatility { get; set; }
        public decimal AverageTrueRange { get; set; }
        public decimal AtrPercentage { get; set; }
        public decimal BetaCoefficient { get; set; }
        public bool IsVolatilityOptimal { get; set; }
        public string VolatilityGrade { get; set; } = "C";
    }

    /// <summary>
    /// Price action assessment details.
    /// </summary>
    public class PriceActionAssessment
    {
        public decimal Score { get; set; }
        public string TrendDirection { get; set; } = "Neutral";
        public decimal TrendStrength { get; set; }
        public bool HasCleanPriceAction { get; set; }
        public int RecentGapCount { get; set; }
        public decimal AverageGapSize { get; set; }
        public List<string> Patterns { get; set; } = new();
        public string PriceActionGrade { get; set; } = "C";
    }

    /// <summary>
    /// Catalyst assessment details.
    /// </summary>
    public class CatalystAssessment
    {
        public decimal Score { get; set; }
        public int RecentNewsCount { get; set; }
        public string NewsSentiment { get; set; } = "Neutral";
        public bool HasEarningsCatalyst { get; set; }
        public bool HasMajorCatalyst { get; set; }
        public List<string> ActiveCatalysts { get; set; } = new();
        public string CatalystGrade { get; set; } = "C";
    }

    /// <summary>
    /// Technical indicator assessment details.
    /// </summary>
    public class TechnicalAssessment
    {
        public decimal Score { get; set; }
        public string RsiStatus { get; set; } = "Neutral";
        public string MacdStatus { get; set; } = "Neutral";
        public string MovingAverageStatus { get; set; } = "Neutral";
        public int BullishSignals { get; set; }
        public int BearishSignals { get; set; }
        public int NeutralSignals { get; set; }
        public string TechnicalGrade { get; set; } = "C";
    }

    /// <summary>
    /// Optimal trading time window.
    /// </summary>
    public class TradingWindow
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string WindowType { get; set; } = "Regular";
        public decimal HistoricalWinRate { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}