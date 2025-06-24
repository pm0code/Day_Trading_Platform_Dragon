// File: TradingPlatform.Core.Canonical\CanonicalRiskEvaluator.cs

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Foundation;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all risk evaluation components, providing standardized risk assessment
    /// with comprehensive monitoring, error handling, and compliance tracking.
    /// </summary>
    public abstract class CanonicalRiskEvaluator<TRiskContext> : CanonicalServiceBase
        where TRiskContext : class
    {
        private readonly Dictionary<string, decimal> _riskMetrics = new();
        private readonly SemaphoreSlim _evaluationSemaphore;
        private readonly int _maxConcurrentEvaluations;
        private long _totalEvaluations;
        private long _riskBreaches;
        private long _complianceViolations;
        private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

        protected CanonicalRiskEvaluator(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            string serviceName,
            int maxConcurrentEvaluations = 5)
            : base(logger, serviceName)
        {
            _maxConcurrentEvaluations = maxConcurrentEvaluations;
            _evaluationSemaphore = new SemaphoreSlim(maxConcurrentEvaluations, maxConcurrentEvaluations);
        }

        /// <summary>
        /// The type of risk being evaluated (e.g., "Market", "Credit", "Operational")
        /// </summary>
        protected abstract string RiskType { get; }

        /// <summary>
        /// Maximum acceptable risk score before triggering alerts
        /// </summary>
        protected virtual decimal MaxAcceptableRiskScore => 0.8m;

        /// <summary>
        /// Evaluates risk with canonical error handling and monitoring
        /// </summary>
        public async Task<TradingResult<RiskAssessment>> EvaluateRiskAsync(
            TRiskContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await PerformRiskEvaluationAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"Error evaluating {RiskType} risk", ex);
                return TradingResult<RiskAssessment>.Success(new RiskAssessment
                {
                    RiskType = RiskType,
                    AssessmentTime = DateTime.UtcNow,
                    RiskScore = 1.0m, // Default to maximum risk on failure
                    IsAcceptable = false,
                    RequiresImmediateAction = true,
                    Reason = $"Evaluation failed: {ex.Message}"
                });
            }
        }

        private async Task<TradingResult<RiskAssessment>> PerformRiskEvaluationAsync(
            TRiskContext context,
            CancellationToken cancellationToken,
            [CallerMemberName] string methodName = "")
        {
            await _evaluationSemaphore.WaitAsync(cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Interlocked.Increment(ref _totalEvaluations);

                // Validate context
                var validationResult = ValidateContext(context);
                if (!validationResult.IsSuccess)
                {
                    return TradingResult<RiskAssessment>.Failure(new TradingError("VALIDATION_FAILED", validationResult.Error?.Message ?? "Context validation failed"));
                }

                // Create assessment
                var assessment = new RiskAssessment
                {
                    RiskType = RiskType,
                    AssessmentTime = DateTime.UtcNow
                };

                // Perform risk evaluation
                var evaluationResult = await EvaluateRiskCoreAsync(context, assessment, cancellationToken);
                
                if (evaluationResult.IsSuccess)
                {
                    // Check risk thresholds
                    if (assessment.RiskScore > MaxAcceptableRiskScore)
                    {
                        Interlocked.Increment(ref _riskBreaches);
                        assessment.IsAcceptable = false;
                        assessment.RequiresImmediateAction = true;
                        
                        LogWarning(
                            $"{RiskType} risk breach detected",
                            "Risk threshold exceeded",
                            "Review risk parameters and reduce exposure",
                            new
                            {
                                RiskScore = assessment.RiskScore,
                                Threshold = MaxAcceptableRiskScore,
                                Context = context
                            });
                    }

                    // Check compliance
                    var complianceResult = await CheckComplianceAsync(context, assessment);
                    if (!complianceResult.IsCompliant)
                    {
                        Interlocked.Increment(ref _complianceViolations);
                        assessment.ComplianceStatus = "Non-Compliant";
                        assessment.ComplianceIssues = complianceResult.Issues;
                    }

                    // Record metrics
                    UpdateMetric($"{RiskType}.Score", assessment.RiskScore);
                    UpdateMetric($"{RiskType}.Breaches", _riskBreaches);
                    
                    // Log performance
                    Logger.LogPerformance(
                        $"{RiskType} risk evaluation",
                        stopwatch.Elapsed,
                        true,
                        throughput: CalculateThroughput(),
                        businessMetrics: new
                        {
                            RiskScore = assessment.RiskScore,
                            IsAcceptable = assessment.IsAcceptable,
                            ComplianceStatus = assessment.ComplianceStatus
                        });

                    return evaluationResult;
                }
                else
                {
                    LogError(
                        $"{RiskType} risk evaluation failed",
                        evaluationResult.Error?.Exception,
                        "Risk evaluation",
                        "Risk assessment unavailable",
                        "Check risk configuration",
                        new { ErrorMessage = evaluationResult.Error?.Message });

                    return evaluationResult;
                }
            }
            catch (Exception ex)
            {
                LogError($"{RiskType} risk evaluation error", ex);
                return TradingResult<RiskAssessment>.Failure(new TradingError("EVALUATION_ERROR", $"Risk evaluation error: {ex.Message}", ex));
            }
            finally
            {
                _evaluationSemaphore.Release();
                UpdateMetric($"{RiskType}.EvaluationDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Validates the risk context before evaluation
        /// </summary>
        protected virtual TradingResult ValidateContext(TRiskContext context)
        {
            if (context == null)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", "Risk context is null"));

            return TradingResult.Success();
        }

        /// <summary>
        /// Implement the specific risk evaluation logic
        /// </summary>
        protected abstract Task<TradingResult<RiskAssessment>> EvaluateRiskCoreAsync(
            TRiskContext context,
            RiskAssessment assessment,
            CancellationToken cancellationToken);

        /// <summary>
        /// Check compliance requirements
        /// </summary>
        protected virtual Task<ComplianceCheckResult> CheckComplianceAsync(
            TRiskContext context,
            RiskAssessment assessment)
        {
            // Default implementation - override for specific compliance checks
            return Task.FromResult(new ComplianceCheckResult { IsCompliant = true });
        }

        /// <summary>
        /// Calculate risk-adjusted metrics
        /// </summary>
        protected decimal CalculateRiskAdjustedReturn(decimal return_, decimal riskScore)
        {
            if (riskScore <= 0) return return_;
            return return_ / riskScore;
        }

        /// <summary>
        /// Calculate Sharpe ratio
        /// </summary>
        protected decimal CalculateSharpeRatio(decimal return_, decimal riskFreeRate, decimal standardDeviation)
        {
            if (standardDeviation <= 0) return 0m;
            return (return_ - riskFreeRate) / standardDeviation;
        }

        /// <summary>
        /// Calculate Value at Risk (VaR)
        /// </summary>
        protected decimal CalculateVaR(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m)
        {
            var returnsList = returns.ToList();
            if (!returnsList.Any()) return 0m;

            returnsList.Sort();
            var index = (int)Math.Ceiling((1m - confidenceLevel) * returnsList.Count) - 1;
            index = Math.Max(0, Math.Min(index, returnsList.Count - 1));

            return Math.Abs(returnsList[index]);
        }

        /// <summary>
        /// Calculate Expected Shortfall (CVaR)
        /// </summary>
        protected decimal CalculateExpectedShortfall(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m)
        {
            var returnsList = returns.ToList();
            if (!returnsList.Any()) return 0m;

            returnsList.Sort();
            var cutoff = (int)Math.Ceiling((1m - confidenceLevel) * returnsList.Count);

            if (cutoff <= 0) return 0m;

            var tailReturns = returnsList.Take(cutoff);
            return Math.Abs(tailReturns.Average());
        }

        private double CalculateThroughput()
        {
            var uptime = _uptimeStopwatch.Elapsed.TotalSeconds;
            return uptime > 0 ? _totalEvaluations / uptime : 0;
        }

        protected void RecordRiskMetric(string name, decimal value)
        {
            _riskMetrics[name] = value;
        }

        protected Dictionary<string, object?> GetServiceMetrics()
        {
            var baseMetrics = new Dictionary<string, object?>();
            
            baseMetrics["TotalEvaluations"] = _totalEvaluations;
            baseMetrics["RiskBreaches"] = _riskBreaches;
            baseMetrics["ComplianceViolations"] = _complianceViolations;
            baseMetrics["EvaluationsPerSecond"] = CalculateThroughput();
            baseMetrics["MaxConcurrentEvaluations"] = _maxConcurrentEvaluations;
            baseMetrics["CurrentConcurrentEvaluations"] = _maxConcurrentEvaluations - _evaluationSemaphore.CurrentCount;
            
            // Add risk-specific metrics
            foreach (var metric in _riskMetrics)
            {
                baseMetrics[$"Risk.{metric.Key}"] = metric.Value;
            }

            return baseMetrics;
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing {RiskType} risk evaluator");
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Starting {RiskType} risk evaluator");
            _uptimeStopwatch.Start();
            await Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Stopping {RiskType} risk evaluator");
            _uptimeStopwatch.Stop();
            await Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _evaluationSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Risk assessment result
    /// </summary>
    public class RiskAssessment
    {
        public string RiskType { get; set; } = "";
        public DateTime AssessmentTime { get; set; }
        public decimal RiskScore { get; set; }
        public bool IsAcceptable { get; set; }
        public bool RequiresImmediateAction { get; set; }
        public string Reason { get; set; } = "";
        public string ComplianceStatus { get; set; } = "Compliant";
        public List<string> ComplianceIssues { get; set; } = new();
        public Dictionary<string, decimal> Metrics { get; set; } = new();
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Compliance check result
    /// </summary>
    public class ComplianceCheckResult
    {
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
        public Dictionary<string, object> Details { get; set; } = new();
    }
}