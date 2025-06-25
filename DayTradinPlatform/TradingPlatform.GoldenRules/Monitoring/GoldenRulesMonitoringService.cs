using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Services;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.TimeSeries.Models;

namespace TradingPlatform.GoldenRules.Monitoring
{
    /// <summary>
    /// Background service that monitors Golden Rules compliance in real-time
    /// </summary>
    public class GoldenRulesMonitoringService : BackgroundService, IGoldenRulesMonitor
    {
        private readonly IGoldenRulesEngine _goldenRulesEngine;
        private readonly ITimeSeriesService _timeSeriesService;
        private readonly ICanonicalMessageQueue _messageQueue;
        private readonly ITradingLogger _logger;
        
        private Timer? _complianceCheckTimer;
        private Timer? _reportingTimer;
        private bool _isMonitoring;
        
        public bool IsMonitoring => _isMonitoring;
        
        public event EventHandler<RuleViolation>? OnRuleViolation;
        public event EventHandler<GoldenRulesAssessment>? OnComplianceImproved;

        public GoldenRulesMonitoringService(
            IGoldenRulesEngine goldenRulesEngine,
            ITimeSeriesService timeSeriesService,
            ICanonicalMessageQueue messageQueue,
            ITradingLogger logger)
        {
            _goldenRulesEngine = goldenRulesEngine ?? throw new ArgumentNullException(nameof(goldenRulesEngine));
            _timeSeriesService = timeSeriesService ?? throw new ArgumentNullException(nameof(timeSeriesService));
            _messageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartMonitoringAsync(stoppingToken);

            // Keep service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            await StopMonitoringAsync();
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (_isMonitoring)
                return;

            _logger.LogInfo("Starting Golden Rules monitoring service");
            
            // Subscribe to rule violation events
            await SubscribeToViolationEventsAsync(cancellationToken);
            
            // Start compliance check timer (every 30 seconds)
            _complianceCheckTimer = new Timer(
                async _ => await CheckComplianceAsync(cancellationToken),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));
            
            // Start reporting timer (every hour)
            _reportingTimer = new Timer(
                async _ => await GeneratePeriodicReportAsync(cancellationToken),
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(1));
            
            _isMonitoring = true;
            _logger.LogInfo("Golden Rules monitoring started");
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
                return;

            _logger.LogInfo("Stopping Golden Rules monitoring service");
            
            _complianceCheckTimer?.Dispose();
            _reportingTimer?.Dispose();
            
            // Generate final report
            await GenerateFinalReportAsync();
            
            _isMonitoring = false;
            _logger.LogInfo("Golden Rules monitoring stopped");
        }

        private async Task SubscribeToViolationEventsAsync(CancellationToken cancellationToken)
        {
            await _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "golden-rules-assessments",
                "monitoring-service",
                async (assessment) =>
                {
                    await ProcessAssessmentEventAsync(assessment, cancellationToken);
                    return true;
                },
                new SubscriptionOptions
                {
                    ConsumerName = "golden-rules-monitor",
                    MaxRetries = 3
                },
                cancellationToken);
        }

        private async Task ProcessAssessmentEventAsync(
            Dictionary<string, object> assessmentData,
            CancellationToken cancellationToken)
        {
            try
            {
                var symbol = assessmentData["Symbol"]?.ToString() ?? "";
                var overallCompliance = Convert.ToBoolean(assessmentData["OverallCompliance"]);
                var blockingViolations = Convert.ToInt32(assessmentData["BlockingViolations"]);

                if (!overallCompliance || blockingViolations > 0)
                {
                    // Get detailed violations
                    var violationsResult = await _goldenRulesEngine.GetSessionViolationsAsync(
                        DateTime.UtcNow.AddMinutes(-1),
                        cancellationToken);

                    if (violationsResult.IsSuccess)
                    {
                        foreach (var violation in violationsResult.Value.Where(v => v.Symbol == symbol))
                        {
                            OnRuleViolation?.Invoke(this, violation);
                            await SendViolationAlertAsync(violation, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing assessment event", ex);
            }
        }

        private async Task CheckComplianceAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get current compliance status
                var complianceResult = await _goldenRulesEngine.GetComplianceStatusAsync(cancellationToken);
                
                if (!complianceResult.IsSuccess)
                    return;

                var compliance = complianceResult.Value;
                
                // Check for critical violations
                var criticalRules = compliance.Where(r => 
                    r.Value.ComplianceRate < 0.5m && 
                    r.Value.EvaluationCount > 10);

                foreach (var rule in criticalRules)
                {
                    await SendComplianceAlertAsync(rule.Value, cancellationToken);
                }

                // Store compliance metrics
                await StoreComplianceMetricsAsync(compliance, cancellationToken);
                
                // Check for improvements
                await CheckForImprovementsAsync(compliance, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during compliance check", ex);
            }
        }

        private async Task GeneratePeriodicReportAsync(CancellationToken cancellationToken)
        {
            try
            {
                var now = DateTime.UtcNow;
                var report = await _goldenRulesEngine.GenerateSessionReportAsync(
                    now.AddHours(-1),
                    now,
                    cancellationToken);

                if (report.IsSuccess)
                {
                    await PublishReportAsync(report.Value, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating periodic report", ex);
            }
        }

        private async Task GenerateFinalReportAsync()
        {
            try
            {
                var sessionStart = DateTime.UtcNow.Date;
                var report = await _goldenRulesEngine.GenerateSessionReportAsync(
                    sessionStart,
                    DateTime.UtcNow,
                    CancellationToken.None);

                if (report.IsSuccess)
                {
                    _logger.LogInfo("Golden Rules session summary",
                        additionalData: new
                        {
                            TotalEvaluations = report.Value.TotalTradesEvaluated,
                            TradesBlocked = report.Value.TradesBlocked,
                            OverallCompliance = report.Value.OverallComplianceRate,
                            Violations = report.Value.Violations.Count,
                            SessionPnL = report.Value.SessionPnL
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating final report", ex);
            }
        }

        private async Task SendViolationAlertAsync(RuleViolation violation, CancellationToken cancellationToken)
        {
            var alert = new AlertPoint
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "GOLDEN_RULE_VIOLATION",
                Severity = violation.Severity.ToString(),
                Symbol = violation.Symbol,
                Message = $"Rule {violation.RuleNumber} violated: {violation.Description}",
                Component = "GoldenRulesMonitor",
                Source = "MonitoringService",
                Context = new Dictionary<string, string>
                {
                    ["RuleNumber"] = violation.RuleNumber.ToString(),
                    ["RuleName"] = violation.RuleName,
                    ["ViolationId"] = violation.ViolationId
                }
            };

            await _timeSeriesService.WritePointAsync(alert, cancellationToken);
            
            _logger.LogWarning($"Golden Rule violation: {violation.RuleName}",
                additionalData: new
                {
                    Rule = violation.RuleNumber,
                    Symbol = violation.Symbol,
                    Severity = violation.Severity,
                    Description = violation.Description
                });
        }

        private async Task SendComplianceAlertAsync(RuleComplianceStats stats, CancellationToken cancellationToken)
        {
            var alert = new AlertPoint
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "LOW_COMPLIANCE",
                Severity = "WARNING",
                Message = $"Rule {stats.RuleNumber} compliance below 50%: {stats.ComplianceRate:P1}",
                Component = "GoldenRulesMonitor",
                Source = "MonitoringService",
                Context = new Dictionary<string, string>
                {
                    ["RuleNumber"] = stats.RuleNumber.ToString(),
                    ["RuleName"] = stats.RuleName,
                    ["ComplianceRate"] = stats.ComplianceRate.ToString("P1"),
                    ["Violations"] = stats.FailCount.ToString()
                }
            };

            await _timeSeriesService.WritePointAsync(alert, cancellationToken);
        }

        private async Task StoreComplianceMetricsAsync(
            Dictionary<int, RuleComplianceStats> compliance,
            CancellationToken cancellationToken)
        {
            var metrics = new PerformanceMetricsPoint
            {
                Component = "GoldenRules",
                Operation = "Compliance",
                Source = "MonitoringService",
                Timestamp = DateTime.UtcNow
            };

            foreach (var rule in compliance)
            {
                metrics.CustomMetrics[$"Rule{rule.Key}_ComplianceRate"] = (long)(rule.Value.ComplianceRate * 100);
                metrics.CustomMetrics[$"Rule{rule.Key}_Evaluations"] = rule.Value.EvaluationCount;
                metrics.CustomMetrics[$"Rule{rule.Key}_Violations"] = rule.Value.FailCount;
            }

            var overallCompliance = compliance.Values.Any() 
                ? compliance.Values.Average(r => r.ComplianceRate) 
                : 1m;
            
            metrics.CustomMetrics["OverallComplianceRate"] = (long)(overallCompliance * 100);

            await _timeSeriesService.WritePointAsync(metrics, cancellationToken);
        }

        private async Task CheckForImprovementsAsync(
            Dictionary<int, RuleComplianceStats> currentCompliance,
            CancellationToken cancellationToken)
        {
            // Get historical compliance
            var historicalMetrics = await _timeSeriesService.GetRangeAsync<PerformanceMetricsPoint>(
                "system_performance",
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow.AddMinutes(-30),
                new Dictionary<string, string> 
                { 
                    ["component"] = "GoldenRules",
                    ["operation"] = "Compliance"
                },
                limit: 1,
                cancellationToken: cancellationToken);

            if (!historicalMetrics.IsSuccess || !historicalMetrics.Value.Any())
                return;

            var historical = historicalMetrics.Value.First();
            var currentOverall = currentCompliance.Values.Any() 
                ? currentCompliance.Values.Average(r => r.ComplianceRate) 
                : 1m;
            
            var historicalOverall = historical.CustomMetrics.ContainsKey("OverallComplianceRate")
                ? historical.CustomMetrics["OverallComplianceRate"] / 100m
                : 0m;

            // Check for significant improvement (10% or more)
            if (currentOverall > historicalOverall + 0.1m)
            {
                var assessment = new GoldenRulesAssessment
                {
                    OverallCompliance = true,
                    ConfidenceScore = currentOverall,
                    Recommendation = $"Compliance improved from {historicalOverall:P0} to {currentOverall:P0}"
                };

                OnComplianceImproved?.Invoke(this, assessment);
                
                _logger.LogInfo("Golden Rules compliance improved",
                    additionalData: new
                    {
                        PreviousCompliance = historicalOverall,
                        CurrentCompliance = currentOverall,
                        Improvement = currentOverall - historicalOverall
                    });
            }
        }

        private async Task PublishReportAsync(GoldenRulesSessionReport report, CancellationToken cancellationToken)
        {
            var reportData = new Dictionary<string, object>
            {
                ["SessionId"] = report.SessionId,
                ["Period"] = $"{report.SessionStart:HH:mm} - {report.SessionEnd:HH:mm}",
                ["TotalEvaluations"] = report.TotalTradesEvaluated,
                ["TradesBlocked"] = report.TradesBlocked,
                ["OverallCompliance"] = report.OverallComplianceRate,
                ["Violations"] = report.Violations.Count,
                ["SessionPnL"] = report.SessionPnL
            };

            await _messageQueue.PublishAsync(
                "golden-rules-reports",
                reportData,
                MessagePriority.Low,
                cancellationToken);
        }
    }
}