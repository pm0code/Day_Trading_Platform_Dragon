using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.GoldenRules.Rules;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.TimeSeries.Models;

namespace TradingPlatform.GoldenRules.Engine
{
    /// <summary>
    /// Canonical implementation of the 12 Golden Rules trading engine.
    /// Enforces trading discipline and risk management through systematic rule evaluation.
    /// </summary>
    public class CanonicalGoldenRulesEngine : CanonicalServiceBase, IGoldenRulesEngine
    {
        private readonly GoldenRulesEngineConfig _config;
        private readonly Dictionary<int, IGoldenRuleEvaluator> _ruleEvaluators;
        private readonly ITimeSeriesService _timeSeriesService;
        private readonly ICanonicalMessageQueue _messageQueue;
        
        private readonly ConcurrentDictionary<string, GoldenRulesAssessment> _lastAssessments;
        private readonly ConcurrentDictionary<string, RuleViolation> _sessionViolations;
        private readonly ConcurrentDictionary<int, RuleComplianceStats> _ruleStats;
        
        private long _totalEvaluations;
        private long _totalViolations;
        private long _totalBlockedTrades;
        private DateTime _sessionStartTime;

        public CanonicalGoldenRulesEngine(
            IOptions<GoldenRulesEngineConfig> config,
            ITradingLogger logger,
            ITimeSeriesService timeSeriesService,
            ICanonicalMessageQueue messageQueue)
            : base(logger, "GoldenRulesEngine")
        {
            _config = config.Value ?? throw new ArgumentNullException(nameof(config));
            _timeSeriesService = timeSeriesService ?? throw new ArgumentNullException(nameof(timeSeriesService));
            _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
            
            _lastAssessments = new ConcurrentDictionary<string, GoldenRulesAssessment>();
            _sessionViolations = new ConcurrentDictionary<string, RuleViolation>();
            _ruleStats = new ConcurrentDictionary<int, RuleComplianceStats>();
            
            // Initialize rule evaluators
            _ruleEvaluators = InitializeRuleEvaluators();
            _sessionStartTime = DateTime.UtcNow;
        }

        #region Rule Evaluation

        public async Task<TradingResult<GoldenRulesAssessment>> EvaluateTradeAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            if (!_config.Enabled)
            {
                return TradingResult<GoldenRulesAssessment>.Success(CreatePassingAssessment(symbol));
            }

            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    Interlocked.Increment(ref _totalEvaluations);

                    var assessment = new GoldenRulesAssessment
                    {
                        Symbol = symbol,
                        MarketContext = new Dictionary<string, object>
                        {
                            ["OrderType"] = orderType,
                            ["Side"] = side,
                            ["Quantity"] = quantity,
                            ["Price"] = price,
                            ["MarketTrend"] = marketConditions.Trend,
                            ["Volatility"] = marketConditions.Volatility
                        }
                    };

                    // Evaluate each enabled rule
                    var evaluationTasks = _ruleEvaluators
                        .Where(r => IsRuleEnabled(r.Key))
                        .Select(async r =>
                        {
                            try
                            {
                                var result = await r.Value.EvaluateAsync(
                                    symbol, orderType, side, quantity, price,
                                    positionContext, marketConditions, cancellationToken);
                                
                                UpdateRuleStats(r.Key, result);
                                return result;
                            }
                            catch (Exception ex)
                            {
                                LogError($"Error evaluating rule {r.Key}", ex);
                                return CreateFailedRuleResult(r.Key, r.Value.RuleName, ex.Message);
                            }
                        });

                    assessment.RuleResults = (await Task.WhenAll(evaluationTasks)).ToList();
                    
                    // Calculate overall compliance
                    CalculateOverallCompliance(assessment);
                    
                    // Record violations
                    await RecordViolationsAsync(assessment, symbol);
                    
                    // Store assessment
                    _lastAssessments[symbol] = assessment;
                    
                    // Publish assessment event
                    await PublishAssessmentEventAsync(assessment, cancellationToken);
                    
                    stopwatch.Stop();
                    
                    LogInfo($"Golden Rules evaluation completed for {symbol}",
                        additionalData: new
                        {
                            Symbol = symbol,
                            OverallCompliance = assessment.OverallCompliance,
                            ConfidenceScore = assessment.ConfidenceScore,
                            PassingRules = assessment.PassingRules,
                            FailingRules = assessment.FailingRules,
                            EvaluationTimeMs = stopwatch.ElapsedMilliseconds
                        });

                    return TradingResult<GoldenRulesAssessment>.Success(assessment);
                },
                nameof(EvaluateTradeAsync));
        }

        public async Task<TradingResult<bool>> ValidateTradeAsync(
            string symbol,
            OrderType orderType,
            OrderSide side,
            decimal quantity,
            decimal price,
            CancellationToken cancellationToken = default)
        {
            // Quick validation using cached data
            var marketConditions = await GetMarketConditionsAsync(symbol, cancellationToken);
            var positionContext = await GetPositionContextAsync(symbol, cancellationToken);
            
            var assessmentResult = await EvaluateTradeAsync(
                symbol, orderType, side, quantity, price,
                positionContext, marketConditions, cancellationToken);
            
            if (!assessmentResult.IsSuccess)
                return TradingResult<bool>.Failure(assessmentResult.Error!);
            
            var assessment = assessmentResult.Value;
            var isValid = assessment.OverallCompliance && 
                         assessment.BlockingViolations == 0 &&
                         assessment.ConfidenceScore >= _config.MinimumComplianceScore;
            
            if (!isValid)
            {
                Interlocked.Increment(ref _totalBlockedTrades);
                LogWarning($"Trade blocked for {symbol} due to Golden Rules violations",
                    additionalData: new
                    {
                        Symbol = symbol,
                        BlockingViolations = assessment.BlockingViolations,
                        ConfidenceScore = assessment.ConfidenceScore,
                        FailingRules = string.Join(", ", assessment.RuleResults
                            .Where(r => !r.IsPassing)
                            .Select(r => $"Rule {r.RuleNumber}"))
                    });
            }
            
            return TradingResult<bool>.Success(isValid);
        }

        #endregion

        #region Compliance Monitoring

        public async Task<TradingResult<Dictionary<int, RuleComplianceStats>>> GetComplianceStatusAsync(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield(); // Async for consistency
                    
                    var stats = new Dictionary<int, RuleComplianceStats>();
                    
                    foreach (var ruleStat in _ruleStats)
                    {
                        stats[ruleStat.Key] = new RuleComplianceStats
                        {
                            RuleNumber = ruleStat.Value.RuleNumber,
                            RuleName = ruleStat.Value.RuleName,
                            EvaluationCount = ruleStat.Value.EvaluationCount,
                            PassCount = ruleStat.Value.PassCount,
                            FailCount = ruleStat.Value.FailCount,
                            ComplianceRate = ruleStat.Value.ComplianceRate,
                            AverageScore = ruleStat.Value.AverageScore,
                            ViolationTimes = new List<DateTime>(ruleStat.Value.ViolationTimes)
                        };
                    }
                    
                    return TradingResult<Dictionary<int, RuleComplianceStats>>.Success(stats);
                },
                nameof(GetComplianceStatusAsync));
        }

        public async Task<TradingResult<List<RuleViolation>>> GetSessionViolationsAsync(
            DateTime? since = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();
                    
                    var violations = _sessionViolations.Values.ToList();
                    
                    if (since.HasValue)
                    {
                        violations = violations.Where(v => v.ViolationTime >= since.Value).ToList();
                    }
                    
                    return TradingResult<List<RuleViolation>>.Success(
                        violations.OrderByDescending(v => v.ViolationTime).ToList());
                },
                nameof(GetSessionViolationsAsync));
        }

        public async Task<TradingResult<GoldenRulesSessionReport>> GenerateSessionReportAsync(
            DateTime sessionStart,
            DateTime sessionEnd,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var report = new GoldenRulesSessionReport
                    {
                        SessionId = Guid.NewGuid().ToString(),
                        SessionStart = sessionStart,
                        SessionEnd = sessionEnd,
                        TotalTradesEvaluated = (int)_totalEvaluations,
                        TradesBlocked = (int)_totalBlockedTrades,
                        TradesExecuted = (int)(_totalEvaluations - _totalBlockedTrades),
                        RuleStats = new Dictionary<int, RuleComplianceStats>(_ruleStats),
                        Violations = _sessionViolations.Values
                            .Where(v => v.ViolationTime >= sessionStart && v.ViolationTime <= sessionEnd)
                            .ToList()
                    };
                    
                    // Calculate overall compliance rate
                    var totalPass = _ruleStats.Values.Sum(r => r.PassCount);
                    var totalEval = _ruleStats.Values.Sum(r => r.EvaluationCount);
                    report.OverallComplianceRate = totalEval > 0 ? (decimal)totalPass / totalEval : 1m;
                    
                    // Get P&L from time series
                    var pnlResult = await GetSessionPnLAsync(sessionStart, sessionEnd, cancellationToken);
                    report.SessionPnL = pnlResult;
                    
                    // Store report in time series
                    await StoreSessionReportAsync(report, cancellationToken);
                    
                    return TradingResult<GoldenRulesSessionReport>.Success(report);
                },
                nameof(GenerateSessionReportAsync));
        }

        #endregion

        #region Configuration Management

        public async Task<TradingResult> UpdateRuleConfigurationAsync(
            int ruleNumber,
            GoldenRuleConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_ruleEvaluators.ContainsKey(ruleNumber))
                    {
                        return TradingResult.Failure("INVALID_RULE", $"Rule {ruleNumber} does not exist");
                    }
                    
                    // Update configuration
                    var existingConfig = _config.RuleConfigs.FirstOrDefault(r => r.RuleNumber == ruleNumber);
                    if (existingConfig != null)
                    {
                        _config.RuleConfigs.Remove(existingConfig);
                    }
                    
                    _config.RuleConfigs.Add(configuration);
                    
                    LogInfo($"Updated configuration for Rule {ruleNumber}",
                        additionalData: new
                        {
                            RuleNumber = ruleNumber,
                            Enabled = configuration.Enabled,
                            Severity = configuration.Severity
                        });
                    
                    await Task.CompletedTask;
                    return TradingResult.Success();
                },
                nameof(UpdateRuleConfigurationAsync));
        }

        public async Task<TradingResult> OverrideViolationAsync(
            string violationId,
            string reason,
            string authorizedBy,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_sessionViolations.TryGetValue(violationId, out var violation))
                    {
                        return TradingResult.Failure("VIOLATION_NOT_FOUND", $"Violation {violationId} not found");
                    }
                    
                    violation.WasOverridden = true;
                    violation.OverrideReason = $"{reason} (by {authorizedBy})";
                    
                    LogWarning($"Violation {violationId} overridden",
                        additionalData: new
                        {
                            ViolationId = violationId,
                            Rule = violation.RuleName,
                            Reason = reason,
                            AuthorizedBy = authorizedBy
                        });
                    
                    await Task.CompletedTask;
                    return TradingResult.Success();
                },
                nameof(OverrideViolationAsync));
        }

        #endregion

        #region Recommendations

        public async Task<TradingResult<List<string>>> GetRecommendationsAsync(
            string symbol,
            PositionContext positionContext,
            MarketConditions marketConditions,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var allRecommendations = new List<string>();
                    
                    // Get recommendations from each rule
                    foreach (var evaluator in _ruleEvaluators.Values)
                    {
                        try
                        {
                            var recommendations = await evaluator.GetRecommendationsAsync(
                                positionContext, marketConditions, cancellationToken);
                            allRecommendations.AddRange(recommendations);
                        }
                        catch (Exception ex)
                        {
                            LogError($"Error getting recommendations from rule {evaluator.RuleNumber}", ex);
                        }
                    }
                    
                    // Add overall recommendations
                    if (_lastAssessments.TryGetValue(symbol, out var lastAssessment))
                    {
                        if (lastAssessment.ConfidenceScore < 0.6m)
                        {
                            allRecommendations.Insert(0, "⚠️ Low confidence score. Wait for better setup");
                        }
                        
                        if (lastAssessment.BlockingViolations > 0)
                        {
                            allRecommendations.Insert(0, "🛑 Blocking violations detected. Do not trade");
                        }
                    }
                    
                    return TradingResult<List<string>>.Success(allRecommendations.Distinct().ToList());
                },
                nameof(GetRecommendationsAsync));
        }

        #endregion

        #region Private Methods

        private Dictionary<int, IGoldenRuleEvaluator> InitializeRuleEvaluators()
        {
            var evaluators = new Dictionary<int, IGoldenRuleEvaluator>
            {
                [1] = new Rule01_CapitalPreservation(),
                [2] = new Rule02_TradingDiscipline(),
                [3] = new Rule03_CutLossesQuickly(),
                [4] = new Rule04_LetWinnersRun(),
                [5] = new Rule05_TradeWithTrend(),
                [6] = new Rule06_HighProbabilitySetups(),
                [7] = new Rule07_ProperPositionSizing(),
                [8] = new Rule08_ControlDailyLosses(),
                [9] = new Rule09_ContinuousLearning(),
                [10] = new Rule10_MasterPsychology(),
                [11] = new Rule11_UnderstandMarketStructure(),
                [12] = new Rule12_WorkLifeBalance()
            };
            
            // Initialize stats for each rule
            foreach (var evaluator in evaluators)
            {
                _ruleStats[evaluator.Key] = new RuleComplianceStats
                {
                    RuleNumber = evaluator.Key,
                    RuleName = evaluator.Value.RuleName
                };
            }
            
            return evaluators;
        }

        private bool IsRuleEnabled(int ruleNumber)
        {
            var config = _config.RuleConfigs.FirstOrDefault(r => r.RuleNumber == ruleNumber);
            return config?.Enabled ?? true; // Default to enabled
        }

        private void CalculateOverallCompliance(GoldenRulesAssessment assessment)
        {
            assessment.PassingRules = assessment.RuleResults.Count(r => r.IsPassing);
            assessment.FailingRules = assessment.RuleResults.Count(r => !r.IsPassing);
            assessment.BlockingViolations = assessment.RuleResults.Count(r => 
                !r.IsPassing && r.Severity == RuleSeverity.Blocking);
            
            // Overall compliance requires no blocking violations
            assessment.OverallCompliance = assessment.BlockingViolations == 0;
            
            // Calculate confidence score
            if (assessment.RuleResults.Any())
            {
                assessment.ConfidenceScore = assessment.RuleResults.Average(r => r.ComplianceScore);
            }
            else
            {
                assessment.ConfidenceScore = 1m;
            }
            
            // Generate recommendation
            if (assessment.BlockingViolations > 0)
            {
                assessment.Recommendation = "DO NOT TRADE - Blocking violations detected";
            }
            else if (assessment.ConfidenceScore < 0.6m)
            {
                assessment.Recommendation = "CAUTION - Low confidence score";
            }
            else if (assessment.ConfidenceScore < 0.8m)
            {
                assessment.Recommendation = "PROCEED WITH CAUTION - Moderate confidence";
            }
            else
            {
                assessment.Recommendation = "TRADE APPROVED - High confidence";
            }
        }

        private void UpdateRuleStats(int ruleNumber, RuleEvaluationResult result)
        {
            if (_ruleStats.TryGetValue(ruleNumber, out var stats))
            {
                stats.EvaluationCount++;
                if (result.IsPassing)
                {
                    stats.PassCount++;
                }
                else
                {
                    stats.FailCount++;
                    stats.ViolationTimes.Add(result.EvaluatedAt);
                    Interlocked.Increment(ref _totalViolations);
                }
                
                stats.ComplianceRate = stats.EvaluationCount > 0 
                    ? (decimal)stats.PassCount / stats.EvaluationCount 
                    : 0;
                
                // Update average score (simplified moving average)
                stats.AverageScore = ((stats.AverageScore * (stats.EvaluationCount - 1)) + 
                                     result.ComplianceScore) / stats.EvaluationCount;
            }
        }

        private RuleEvaluationResult CreateFailedRuleResult(int ruleNumber, string ruleName, string error)
        {
            return new RuleEvaluationResult
            {
                RuleNumber = ruleNumber,
                RuleName = ruleName,
                IsPassing = false,
                ComplianceScore = 0,
                Reason = $"Evaluation error: {error}",
                Severity = RuleSeverity.Warning
            };
        }

        private GoldenRulesAssessment CreatePassingAssessment(string symbol)
        {
            return new GoldenRulesAssessment
            {
                Symbol = symbol,
                OverallCompliance = true,
                ConfidenceScore = 1m,
                PassingRules = _ruleEvaluators.Count,
                FailingRules = 0,
                BlockingViolations = 0,
                Recommendation = "Golden Rules engine disabled - trade allowed"
            };
        }

        private async Task RecordViolationsAsync(GoldenRulesAssessment assessment, string symbol)
        {
            foreach (var result in assessment.RuleResults.Where(r => !r.IsPassing))
            {
                var violation = new RuleViolation
                {
                    RuleNumber = result.RuleNumber,
                    RuleName = result.RuleName,
                    Symbol = symbol,
                    Severity = result.Severity,
                    Description = result.Reason,
                    ViolationTime = DateTime.UtcNow,
                    CorrectiveAction = GetCorrectiveAction(result.RuleNumber)
                };
                
                _sessionViolations[violation.ViolationId] = violation;
                
                // Store in time series
                await StoreViolationAsync(violation);
            }
        }

        private string GetCorrectiveAction(int ruleNumber)
        {
            return ruleNumber switch
            {
                1 => "Reduce position size to comply with 1% risk rule",
                2 => "Review trading plan and ensure systematic approach",
                3 => "Set appropriate stop loss before entry",
                4 => "Let winning positions run with trailing stop",
                5 => "Wait for trade aligned with the trend",
                6 => "Wait for high-probability setup with multiple confirmations",
                7 => "Adjust position size based on account equity and risk",
                8 => "Stop trading for the day - daily loss limit reached",
                9 => "Review recent trades and identify lessons learned",
                10 => "Take a break to regain emotional control",
                11 => "Wait for optimal market session and structure",
                12 => "Take a break - maintain sustainable trading habits",
                _ => "Review rule requirements and adjust trade parameters"
            };
        }

        private async Task<MarketConditions> GetMarketConditionsAsync(string symbol, CancellationToken cancellationToken)
        {
            // Get latest market data from time series
            var marketDataResult = await _timeSeriesService.GetLatestAsync<MarketDataPoint>(
                "market_data",
                new Dictionary<string, string> { ["symbol"] = symbol },
                cancellationToken);
            
            if (marketDataResult.IsSuccess && marketDataResult.Value != null)
            {
                var data = marketDataResult.Value;
                return new MarketConditions
                {
                    Symbol = symbol,
                    Price = data.Price,
                    Bid = data.Bid,
                    Ask = data.Ask,
                    Volume = data.Volume,
                    DayHigh = data.High,
                    DayLow = data.Low,
                    OpenPrice = data.Open,
                    ATR = CalculateATR(data),
                    Volatility = CalculateVolatility(data),
                    Trend = DetermineTrend(data),
                    RelativeVolume = data.Volume / 1000000m, // Simplified
                    Session = DetermineMarketSession()
                };
            }
            
            // Return default conditions if no data
            return new MarketConditions { Symbol = symbol };
        }

        private async Task<PositionContext> GetPositionContextAsync(string symbol, CancellationToken cancellationToken)
        {
            // Get position data from time series
            var positionResult = await _timeSeriesService.GetLatestAsync<PositionPoint>(
                "positions",
                new Dictionary<string, string> { ["symbol"] = symbol },
                cancellationToken);
            
            var context = new PositionContext
            {
                Symbol = symbol,
                AccountBalance = 100000m, // TODO: Get from account service
                BuyingPower = 400000m,
                DayTradeCount = 0 // TODO: Get from trading history
            };
            
            if (positionResult.IsSuccess && positionResult.Value != null)
            {
                var position = positionResult.Value;
                context.Quantity = position.Quantity;
                context.EntryPrice = position.AveragePrice;
                context.CurrentPrice = position.CurrentPrice;
                context.UnrealizedPnL = position.UnrealizedPnL;
                context.RealizedPnL = position.RealizedPnL;
            }
            
            return context;
        }

        private decimal CalculateATR(MarketDataPoint data)
        {
            // Simplified ATR calculation
            return (data.High - data.Low) * 0.7m;
        }

        private decimal CalculateVolatility(MarketDataPoint data)
        {
            // Simplified volatility calculation
            return (data.High - data.Low) / data.Price;
        }

        private TrendDirection DetermineTrend(MarketDataPoint data)
        {
            var change = (data.Price - data.Open) / data.Open;
            
            if (change > 0.02m) return TrendDirection.StrongUptrend;
            if (change > 0.005m) return TrendDirection.Uptrend;
            if (change < -0.02m) return TrendDirection.StrongDowntrend;
            if (change < -0.005m) return TrendDirection.Downtrend;
            return TrendDirection.Sideways;
        }

        private MarketSession DetermineMarketSession()
        {
            var now = DateTime.UtcNow;
            var easternTime = TimeZoneInfo.ConvertTimeFromUtc(now, 
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            
            var time = easternTime.TimeOfDay;
            
            if (time < new TimeSpan(9, 30, 0)) return MarketSession.PreMarket;
            if (time < new TimeSpan(10, 0, 0)) return MarketSession.MarketOpen;
            if (time < new TimeSpan(15, 0, 0)) return MarketSession.RegularHours;
            if (time < new TimeSpan(16, 0, 0)) return MarketSession.PowerHour;
            if (time <= new TimeSpan(16, 30, 0)) return MarketSession.MarketClose;
            return MarketSession.AfterHours;
        }

        private async Task<decimal> GetSessionPnLAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            // Query P&L from time series
            var pnlResult = await _timeSeriesService.GetRangeAsync<PositionPoint>(
                "positions",
                start,
                end,
                cancellationToken: cancellationToken);
            
            if (pnlResult.IsSuccess && pnlResult.Value.Any())
            {
                return pnlResult.Value.Sum(p => p.RealizedPnL);
            }
            
            return 0m;
        }

        private async Task StoreViolationAsync(RuleViolation violation)
        {
            var alertPoint = new AlertPoint
            {
                AlertId = violation.ViolationId,
                AlertType = "GOLDEN_RULE_VIOLATION",
                Severity = violation.Severity.ToString(),
                Symbol = violation.Symbol,
                Message = violation.Description,
                Component = "GoldenRulesEngine",
                Source = ServiceName,
                Context = new Dictionary<string, string>
                {
                    ["RuleNumber"] = violation.RuleNumber.ToString(),
                    ["RuleName"] = violation.RuleName,
                    ["CorrectiveAction"] = violation.CorrectiveAction
                }
            };
            
            await _timeSeriesService.WritePointAsync(alertPoint);
        }

        private async Task StoreSessionReportAsync(GoldenRulesSessionReport report, CancellationToken cancellationToken)
        {
            var statsPoint = new TradingStatsPoint
            {
                Period = "session",
                TotalTrades = report.TotalTradesEvaluated,
                WinningTrades = report.TradesExecuted,
                LosingTrades = report.TradesBlocked,
                TotalPnL = report.SessionPnL,
                Source = ServiceName,
                Timestamp = report.SessionEnd
            };
            
            statsPoint.Tags["SessionId"] = report.SessionId;
            statsPoint.Tags["ComplianceRate"] = report.OverallComplianceRate.ToString("F2");
            
            await _timeSeriesService.WritePointAsync(statsPoint, cancellationToken);
        }

        private async Task PublishAssessmentEventAsync(GoldenRulesAssessment assessment, CancellationToken cancellationToken)
        {
            if (!_config.EnableRealTimeAlerts)
                return;
            
            var eventData = new Dictionary<string, object>
            {
                ["Symbol"] = assessment.Symbol,
                ["OverallCompliance"] = assessment.OverallCompliance,
                ["ConfidenceScore"] = assessment.ConfidenceScore,
                ["PassingRules"] = assessment.PassingRules,
                ["FailingRules"] = assessment.FailingRules,
                ["BlockingViolations"] = assessment.BlockingViolations,
                ["Recommendation"] = assessment.Recommendation
            };
            
            await _messageQueue.PublishAsync(
                "golden-rules-assessments",
                eventData,
                MessagePriority.Normal,
                cancellationToken);
        }

        #endregion

        #region Lifecycle

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing Golden Rules Engine",
                additionalData: new
                {
                    Enabled = _config.Enabled,
                    StrictMode = _config.StrictMode,
                    MinComplianceScore = _config.MinimumComplianceScore,
                    RuleCount = _ruleEvaluators.Count
                });
            
            await Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _sessionStartTime = DateTime.UtcNow;
            LogInfo("Golden Rules Engine started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Golden Rules Engine stopped",
                additionalData: new
                {
                    TotalEvaluations = _totalEvaluations,
                    TotalViolations = _totalViolations,
                    TotalBlockedTrades = _totalBlockedTrades,
                    SessionDuration = DateTime.UtcNow - _sessionStartTime
                });
            
            return Task.CompletedTask;
        }

        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
            OnCheckHealthAsync(CancellationToken cancellationToken)
        {
            var details = new Dictionary<string, object>
            {
                ["Enabled"] = _config.Enabled,
                ["TotalEvaluations"] = _totalEvaluations,
                ["TotalViolations"] = _totalViolations,
                ["TotalBlockedTrades"] = _totalBlockedTrades,
                ["ActiveRules"] = _ruleEvaluators.Count(r => IsRuleEnabled(r.Key)),
                ["SessionDuration"] = DateTime.UtcNow - _sessionStartTime
            };
            
            var tsHealth = await _timeSeriesService.IsHealthyAsync(cancellationToken);
            details["TimeSeriesHealthy"] = tsHealth.Value;
            
            var isHealthy = _config.Enabled && tsHealth.Value;
            var message = isHealthy ? "Golden Rules Engine healthy" : "Engine unhealthy";
            
            return (isHealthy, message, details);
        }

        public async Task<TradingResult<bool>> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            var health = await CheckHealthAsync(cancellationToken);
            return TradingResult<bool>.Success(health.IsHealthy);
        }

        #endregion
    }
}