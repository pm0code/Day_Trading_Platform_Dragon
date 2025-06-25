using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.GoldenRules.Models;

namespace TradingPlatform.GoldenRules.Interfaces
{
    /// <summary>
    /// Interface for the 12 Golden Rules trading engine
    /// </summary>
    public interface IGoldenRulesEngine
    {
        /// <summary>
        /// Evaluates a potential trade against all Golden Rules
        /// </summary>
        Task<TradingResult<GoldenRulesAssessment>> EvaluateTradeAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a trade should be allowed based on Golden Rules
        /// </summary>
        Task<TradingResult<bool>> ValidateTradeAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current compliance status for all rules
        /// </summary>
        Task<TradingResult<Dictionary<int, RuleComplianceStats>>> GetComplianceStatusAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets violations for the current trading session
        /// </summary>
        Task<TradingResult<List<RuleViolation>>> GetSessionViolationsAsync(
            DateTime? since = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a session compliance report
        /// </summary>
        Task<TradingResult<GoldenRulesSessionReport>> GenerateSessionReportAsync(
            DateTime sessionStart,
            DateTime sessionEnd,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates rule configuration
        /// </summary>
        Task<TradingResult> UpdateRuleConfigurationAsync(
            int ruleNumber,
            GoldenRuleConfiguration configuration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Overrides a rule violation (requires authorization)
        /// </summary>
        Task<TradingResult> OverrideViolationAsync(
            string violationId,
            string reason,
            string authorizedBy,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets real-time rule recommendations
        /// </summary>
        Task<TradingResult<List<string>>> GetRecommendationsAsync(
            string symbol,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if engine is healthy and operational
        /// </summary>
        Task<TradingResult<bool>> IsHealthyAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for individual Golden Rule evaluators
    /// </summary>
    public interface IGoldenRuleEvaluator
    {
        /// <summary>
        /// The rule number (1-12)
        /// </summary>
        int RuleNumber { get; }

        /// <summary>
        /// The rule name
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// The rule category
        /// </summary>
        RuleCategory Category { get; }

        /// <summary>
        /// Evaluates the rule
        /// </summary>
        Task<RuleEvaluationResult> EvaluateAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets rule-specific recommendations
        /// </summary>
        Task<List<string>> GetRecommendationsAsync(
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for Golden Rules monitoring service
    /// </summary>
    public interface IGoldenRulesMonitor
    {
        /// <summary>
        /// Starts monitoring compliance
        /// </summary>
        Task StartMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops monitoring
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Gets current monitoring status
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Event raised when a rule violation occurs
        /// </summary>
        event EventHandler<RuleViolation> OnRuleViolation;

        /// <summary>
        /// Event raised when compliance improves
        /// </summary>
        event EventHandler<GoldenRulesAssessment> OnComplianceImproved;
    }
}