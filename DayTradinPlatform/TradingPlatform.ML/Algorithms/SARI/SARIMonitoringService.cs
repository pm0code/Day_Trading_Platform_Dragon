using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.IO;

namespace TradingPlatform.ML.Algorithms.SARI
{
    /// <summary>
    /// Comprehensive SARI monitoring service for real-time dashboard visualization
    /// Provides streaming data, historical tracking, and analytics
    /// </summary>
    public class SARIMonitoringService : CanonicalServiceBase
    {
        private readonly SARICalculator _sariCalculator;
        private readonly StressScenarioLibrary _scenarioLibrary;
        private readonly StressPropagationEngine _propagationEngine;
        private readonly IMarketDataService _marketDataService;
        private readonly IHubContext<SARIMonitoringHub> _hubContext;
        
        // Real-time streaming
        private readonly Subject<SARIMonitoringUpdate> _sariUpdateStream;
        private readonly Subject<SARIAlert> _alertStream;
        
        // Historical data storage
        private readonly ConcurrentDictionary<string, CircularBuffer<SARIHistoricalPoint>> _historicalData;
        private readonly ConcurrentDictionary<string, SARIAggregatedMetrics> _aggregatedMetrics;
        private readonly ConcurrentDictionary<string, RiskLevelTransition> _riskTransitions;
        
        // Monitoring configuration
        private SARIMonitoringConfiguration _configuration;
        private Timer _monitoringTimer;
        private readonly SemaphoreSlim _calculationSemaphore;
        
        // Cache for performance
        private readonly Dictionary<TimeGranularity, TimeSpan> _aggregationWindows;
        private DateTime _lastCalculationTime;
        private SARIResult _lastSARIResult;

        public SARIMonitoringService(
            SARICalculator sariCalculator,
            StressScenarioLibrary scenarioLibrary,
            StressPropagationEngine propagationEngine,
            IMarketDataService marketDataService,
            IHubContext<SARIMonitoringHub> hubContext,
            ICanonicalLogger logger) : base(logger)
        {
            _sariCalculator = sariCalculator ?? throw new ArgumentNullException(nameof(sariCalculator));
            _scenarioLibrary = scenarioLibrary ?? throw new ArgumentNullException(nameof(scenarioLibrary));
            _propagationEngine = propagationEngine ?? throw new ArgumentNullException(nameof(propagationEngine));
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            
            _sariUpdateStream = new Subject<SARIMonitoringUpdate>();
            _alertStream = new Subject<SARIAlert>();
            
            _historicalData = new ConcurrentDictionary<string, CircularBuffer<SARIHistoricalPoint>>();
            _aggregatedMetrics = new ConcurrentDictionary<string, SARIAggregatedMetrics>();
            _riskTransitions = new ConcurrentDictionary<string, RiskLevelTransition>();
            
            _calculationSemaphore = new SemaphoreSlim(1, 1);
            
            InitializeAggregationWindows();
        }

        protected override async Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Initializing SARI Monitoring Service");
            
            try
            {
                // Load configuration
                _configuration = await LoadConfigurationAsync(cancellationToken);
                
                // Initialize historical data buffers
                InitializeHistoricalBuffers();
                
                // Setup real-time streams
                SetupRealTimeStreams();
                
                // Initialize aggregation metrics
                await InitializeAggregationMetricsAsync(cancellationToken);
                
                LogInfo("SARI Monitoring Service initialized successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize SARI Monitoring Service", ex);
                return TradingResult.Failure($"Initialization failed: {ex.Message}");
            }
        }

        protected override async Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Starting SARI Monitoring Service");
            
            try
            {
                // Start monitoring timer
                _monitoringTimer = new Timer(
                    async _ => await MonitoringCycleAsync(),
                    null,
                    TimeSpan.Zero,
                    _configuration.MonitoringInterval);
                
                // Start alert monitoring
                _ = Task.Run(() => MonitorAlertsAsync(cancellationToken), cancellationToken);
                
                LogInfo("SARI Monitoring Service started successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Failed to start SARI Monitoring Service", ex);
                return TradingResult.Failure($"Start failed: {ex.Message}");
            }
        }

        protected override async Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Stopping SARI Monitoring Service");
            
            try
            {
                _monitoringTimer?.Dispose();
                
                // Complete streams
                _sariUpdateStream.OnCompleted();
                _alertStream.OnCompleted();
                
                // Save state
                await SaveStateAsync(cancellationToken);
                
                LogInfo("SARI Monitoring Service stopped successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error stopping SARI Monitoring Service", ex);
                return TradingResult.Failure($"Stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get real-time SARI monitoring data for dashboard
        /// </summary>
        public async Task<TradingResult<SARIMonitoringData>> GetMonitoringDataAsync(
            string portfolioId,
            TimeGranularity granularity = TimeGranularity.OneMinute,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogDebug($"Getting monitoring data for portfolio {portfolioId} at {granularity} granularity");
            
            try
            {
                var monitoringData = new SARIMonitoringData
                {
                    PortfolioId = portfolioId,
                    Timestamp = DateTime.UtcNow,
                    Granularity = granularity
                };
                
                // Get current SARI data
                if (_lastSARIResult != null && (DateTime.UtcNow - _lastCalculationTime).TotalMinutes < 5)
                {
                    monitoringData.CurrentSARI = MapToMonitoringModel(_lastSARIResult);
                }
                else
                {
                    // Trigger new calculation if data is stale
                    var portfolio = await LoadPortfolioAsync(portfolioId, cancellationToken);
                    var marketContext = await GetMarketContextAsync(cancellationToken);
                    
                    var sariResult = await _sariCalculator.CalculateSARIAsync(
                        portfolio, marketContext, null, cancellationToken);
                    
                    if (sariResult.IsSuccess)
                    {
                        _lastSARIResult = sariResult.Data;
                        _lastCalculationTime = DateTime.UtcNow;
                        monitoringData.CurrentSARI = MapToMonitoringModel(sariResult.Data);
                    }
                }
                
                // Get historical data
                monitoringData.HistoricalData = GetHistoricalData(portfolioId, granularity);
                
                // Get aggregated metrics
                var metricsKey = $"{portfolioId}_{granularity}";
                if (_aggregatedMetrics.TryGetValue(metricsKey, out var metrics))
                {
                    monitoringData.AggregatedMetrics = metrics;
                }
                
                // Get recent alerts
                monitoringData.RecentAlerts = await GetRecentAlertsAsync(portfolioId, 50, cancellationToken);
                
                // Get risk transitions
                monitoringData.RiskTransitions = GetRiskTransitions(portfolioId);
                
                // Get scenario breakdown
                monitoringData.ScenarioBreakdown = GetScenarioBreakdown();
                
                // Get stress metrics
                monitoringData.StressMetrics = await GetStressMetricsAsync(portfolioId, cancellationToken);
                
                LogInfo($"Retrieved monitoring data for portfolio {portfolioId}");
                LogMethodExit();
                return TradingResult<SARIMonitoringData>.Success(monitoringData);
            }
            catch (Exception ex)
            {
                LogError($"Error getting monitoring data for portfolio {portfolioId}", ex);
                return TradingResult<SARIMonitoringData>.Failure($"Failed to get monitoring data: {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribe to real-time SARI updates
        /// </summary>
        public IObservable<SARIMonitoringUpdate> SubscribeToUpdates(string portfolioId = null)
        {
            LogMethodEntry();
            LogDebug($"Subscribing to SARI updates for portfolio: {portfolioId ?? "all"}");
            
            var stream = portfolioId != null
                ? _sariUpdateStream.Where(u => u.PortfolioId == portfolioId)
                : _sariUpdateStream;
            
            LogMethodExit();
            return stream;
        }

        /// <summary>
        /// Subscribe to SARI alerts
        /// </summary>
        public IObservable<SARIAlert> SubscribeToAlerts(string portfolioId = null, AlertSeverity? minSeverity = null)
        {
            LogMethodEntry();
            LogDebug($"Subscribing to SARI alerts for portfolio: {portfolioId ?? "all"}, min severity: {minSeverity}");
            
            var stream = _alertStream.AsObservable();
            
            if (portfolioId != null)
                stream = stream.Where(a => a.PortfolioId == portfolioId);
            
            if (minSeverity.HasValue)
                stream = stream.Where(a => a.Severity >= minSeverity.Value);
            
            LogMethodExit();
            return stream;
        }

        /// <summary>
        /// Get time-series SARI analytics
        /// </summary>
        public async Task<TradingResult<SARIAnalytics>> GetAnalyticsAsync(
            string portfolioId,
            DateTime startDate,
            DateTime endDate,
            AnalyticsOptions options = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Getting SARI analytics for portfolio {portfolioId} from {startDate} to {endDate}");
            
            try
            {
                options ??= new AnalyticsOptions();
                
                var analytics = new SARIAnalytics
                {
                    PortfolioId = portfolioId,
                    StartDate = startDate,
                    EndDate = endDate,
                    GeneratedAt = DateTime.UtcNow
                };
                
                // Get historical data for analysis
                var historicalPoints = GetHistoricalDataRange(portfolioId, startDate, endDate);
                
                if (!historicalPoints.Any())
                {
                    LogWarning($"No historical data found for portfolio {portfolioId} in specified range");
                    return TradingResult<SARIAnalytics>.Success(analytics);
                }
                
                // Calculate statistics
                analytics.Statistics = CalculateStatistics(historicalPoints);
                
                // Trend analysis
                if (options.IncludeTrendAnalysis)
                {
                    analytics.TrendAnalysis = CalculateTrendAnalysis(historicalPoints);
                }
                
                // Scenario analysis
                if (options.IncludeScenarioAnalysis)
                {
                    analytics.ScenarioAnalysis = await AnalyzeScenarioContributionsAsync(
                        portfolioId, historicalPoints, cancellationToken);
                }
                
                // Risk level analysis
                if (options.IncludeRiskLevelAnalysis)
                {
                    analytics.RiskLevelAnalysis = AnalyzeRiskLevels(historicalPoints);
                }
                
                // Forecasting
                if (options.IncludeForecasting)
                {
                    analytics.Forecast = await GenerateForecastAsync(
                        portfolioId, historicalPoints, options.ForecastHorizon, cancellationToken);
                }
                
                // Correlation analysis
                if (options.IncludeCorrelations)
                {
                    analytics.Correlations = await AnalyzeCorrelationsAsync(
                        portfolioId, historicalPoints, cancellationToken);
                }
                
                LogInfo($"Generated analytics for portfolio {portfolioId} with {historicalPoints.Count} data points");
                LogMethodExit();
                return TradingResult<SARIAnalytics>.Success(analytics);
            }
            catch (Exception ex)
            {
                LogError($"Error generating analytics for portfolio {portfolioId}", ex);
                return TradingResult<SARIAnalytics>.Failure($"Analytics generation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Export monitoring data
        /// </summary>
        public async Task<TradingResult<string>> ExportDataAsync(
            string portfolioId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Exporting SARI data for portfolio {portfolioId} from {startDate} to {endDate} as {format}");
            
            try
            {
                var historicalData = GetHistoricalDataRange(portfolioId, startDate, endDate);
                
                if (!historicalData.Any())
                {
                    LogWarning("No data to export");
                    return TradingResult<string>.Failure("No data found for specified range");
                }
                
                switch (format)
                {
                    case ExportFormat.CSV:
                        await ExportToCsvAsync(historicalData, filePath, cancellationToken);
                        break;
                    
                    case ExportFormat.JSON:
                        await ExportToJsonAsync(historicalData, filePath, cancellationToken);
                        break;
                    
                    case ExportFormat.Excel:
                        await ExportToExcelAsync(historicalData, filePath, cancellationToken);
                        break;
                    
                    default:
                        return TradingResult<string>.Failure($"Unsupported export format: {format}");
                }
                
                LogInfo($"Successfully exported {historicalData.Count} data points to {filePath}");
                LogMethodExit();
                return TradingResult<string>.Success(filePath);
            }
            catch (Exception ex)
            {
                LogError($"Error exporting data for portfolio {portfolioId}", ex);
                return TradingResult<string>.Failure($"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Trigger manual SARI recalculation
        /// </summary>
        public async Task<TradingResult<SARIMonitoringUpdate>> TriggerRecalculationAsync(
            string portfolioId,
            SARIParameters parameters = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Triggering manual SARI recalculation for portfolio {portfolioId}");
            
            try
            {
                await _calculationSemaphore.WaitAsync(cancellationToken);
                
                var portfolio = await LoadPortfolioAsync(portfolioId, cancellationToken);
                var marketContext = await GetMarketContextAsync(cancellationToken);
                
                var sariResult = await _sariCalculator.CalculateSARIAsync(
                    portfolio, marketContext, parameters, cancellationToken);
                
                if (!sariResult.IsSuccess)
                {
                    return TradingResult<SARIMonitoringUpdate>.Failure(sariResult.ErrorMessage);
                }
                
                var update = await ProcessSARIResultAsync(portfolioId, sariResult.Data, cancellationToken);
                
                LogInfo($"Manual recalculation completed for portfolio {portfolioId}");
                LogMethodExit();
                return TradingResult<SARIMonitoringUpdate>.Success(update);
            }
            catch (Exception ex)
            {
                LogError($"Error in manual recalculation for portfolio {portfolioId}", ex);
                return TradingResult<SARIMonitoringUpdate>.Failure($"Recalculation failed: {ex.Message}");
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }

        // Private helper methods

        private async Task MonitoringCycleAsync()
        {
            LogDebug("Starting monitoring cycle");
            
            try
            {
                // Get all active portfolios
                var portfolios = await GetActivePortfoliosAsync();
                
                foreach (var portfolioId in portfolios)
                {
                    try
                    {
                        await _calculationSemaphore.WaitAsync();
                        
                        var portfolio = await LoadPortfolioAsync(portfolioId);
                        var marketContext = await GetMarketContextAsync();
                        
                        var sariResult = await _sariCalculator.CalculateSARIAsync(
                            portfolio, marketContext, null);
                        
                        if (sariResult.IsSuccess)
                        {
                            await ProcessSARIResultAsync(portfolioId, sariResult.Data);
                        }
                        else
                        {
                            LogWarning($"SARI calculation failed for portfolio {portfolioId}: {sariResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error processing portfolio {portfolioId} in monitoring cycle", ex);
                    }
                    finally
                    {
                        _calculationSemaphore.Release();
                    }
                }
                
                LogDebug("Monitoring cycle completed");
            }
            catch (Exception ex)
            {
                LogError("Error in monitoring cycle", ex);
            }
        }

        private async Task<SARIMonitoringUpdate> ProcessSARIResultAsync(
            string portfolioId,
            SARIResult result,
            CancellationToken cancellationToken = default)
        {
            LogDebug($"Processing SARI result for portfolio {portfolioId}");
            
            // Create monitoring update
            var update = new SARIMonitoringUpdate
            {
                PortfolioId = portfolioId,
                Timestamp = result.Timestamp,
                SARIIndex = result.SARIIndex,
                RiskLevel = result.RiskLevel,
                MarketRegime = result.MarketRegime,
                ScenarioContributions = result.ScenarioResults.ToDictionary(
                    s => s.ScenarioName,
                    s => s.WeightedImpact),
                ComponentContributions = result.ComponentContributions,
                TimeHorizonResults = result.TimeHorizonResults,
                TopRecommendations = result.Recommendations
                    .OrderByDescending(r => r.Priority)
                    .Take(5)
                    .Select(r => new MonitoringRecommendation
                    {
                        Action = r.Action,
                        Details = r.Details,
                        Priority = r.Priority
                    })
                    .ToList()
            };
            
            // Store historical point
            var historicalPoint = new SARIHistoricalPoint
            {
                Timestamp = result.Timestamp,
                SARIIndex = result.SARIIndex,
                RiskLevel = result.RiskLevel,
                MarketRegime = result.MarketRegime,
                ScenarioContributions = update.ScenarioContributions,
                ComponentContributions = update.ComponentContributions
            };
            
            var buffer = _historicalData.GetOrAdd(portfolioId, 
                _ => new CircularBuffer<SARIHistoricalPoint>(_configuration.HistoricalBufferSize));
            buffer.Add(historicalPoint);
            
            // Update aggregated metrics
            await UpdateAggregatedMetricsAsync(portfolioId, result, cancellationToken);
            
            // Check for risk level transitions
            CheckRiskLevelTransition(portfolioId, result);
            
            // Generate alerts if needed
            await GenerateAlertsAsync(portfolioId, result, cancellationToken);
            
            // Publish update
            _sariUpdateStream.OnNext(update);
            
            // Send to SignalR hub
            await _hubContext.Clients.Group($"portfolio_{portfolioId}")
                .SendAsync("SARIUpdate", update, cancellationToken);
            
            LogDebug($"Processed SARI result for portfolio {portfolioId}");
            return update;
        }

        private void CheckRiskLevelTransition(string portfolioId, SARIResult result)
        {
            var transition = _riskTransitions.GetOrAdd(portfolioId, _ => new RiskLevelTransition());
            
            if (transition.CurrentLevel != result.RiskLevel)
            {
                transition.PreviousLevel = transition.CurrentLevel;
                transition.CurrentLevel = result.RiskLevel;
                transition.TransitionTime = result.Timestamp;
                transition.TransitionCount++;
                
                LogInfo($"Risk level transition for portfolio {portfolioId}: " +
                       $"{transition.PreviousLevel} -> {transition.CurrentLevel}");
            }
        }

        private async Task GenerateAlertsAsync(
            string portfolioId,
            SARIResult result,
            CancellationToken cancellationToken)
        {
            var alerts = new List<SARIAlert>();
            
            // Critical risk level alert
            if (result.RiskLevel == RiskLevel.Critical)
            {
                alerts.Add(new SARIAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    PortfolioId = portfolioId,
                    Timestamp = DateTime.UtcNow,
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.RiskLevel,
                    Title = "Critical Risk Level Reached",
                    Message = $"Portfolio has reached critical risk level with SARI index of {result.SARIIndex:F4}",
                    Data = new Dictionary<string, object>
                    {
                        ["SARIIndex"] = result.SARIIndex,
                        ["RiskLevel"] = result.RiskLevel
                    }
                });
            }
            
            // High impact scenario alerts
            foreach (var scenario in result.ScenarioResults.Where(s => s.WeightedImpact > 0.15f))
            {
                alerts.Add(new SARIAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    PortfolioId = portfolioId,
                    Timestamp = DateTime.UtcNow,
                    Severity = AlertSeverity.High,
                    Type = AlertType.ScenarioImpact,
                    Title = $"High Impact Scenario: {scenario.ScenarioName}",
                    Message = $"Scenario {scenario.ScenarioName} shows weighted impact of {scenario.WeightedImpact:P}",
                    Data = new Dictionary<string, object>
                    {
                        ["ScenarioId"] = scenario.ScenarioId,
                        ["WeightedImpact"] = scenario.WeightedImpact,
                        ["StressLoss"] = scenario.StressLoss
                    }
                });
            }
            
            // Rapid SARI increase alert
            var recentHistory = GetRecentHistory(portfolioId, TimeSpan.FromMinutes(30));
            if (recentHistory.Count > 5)
            {
                var avgSARI = recentHistory.Take(recentHistory.Count - 1).Average(h => h.SARIIndex);
                var increase = (result.SARIIndex - avgSARI) / avgSARI;
                
                if (increase > 0.20f) // 20% increase
                {
                    alerts.Add(new SARIAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        PortfolioId = portfolioId,
                        Timestamp = DateTime.UtcNow,
                        Severity = AlertSeverity.High,
                        Type = AlertType.RapidChange,
                        Title = "Rapid SARI Increase Detected",
                        Message = $"SARI index increased by {increase:P} in the last 30 minutes",
                        Data = new Dictionary<string, object>
                        {
                            ["PreviousAverage"] = avgSARI,
                            ["CurrentValue"] = result.SARIIndex,
                            ["IncreasePercent"] = increase
                        }
                    });
                }
            }
            
            // Publish alerts
            foreach (var alert in alerts)
            {
                _alertStream.OnNext(alert);
                
                await _hubContext.Clients.Group($"portfolio_{portfolioId}")
                    .SendAsync("SARIAlert", alert, cancellationToken);
                
                LogWarning($"Generated alert: {alert.Title} for portfolio {portfolioId}");
            }
        }

        private async Task UpdateAggregatedMetricsAsync(
            string portfolioId,
            SARIResult result,
            CancellationToken cancellationToken)
        {
            foreach (var granularity in Enum.GetValues<TimeGranularity>())
            {
                var key = $"{portfolioId}_{granularity}";
                var metrics = _aggregatedMetrics.GetOrAdd(key, _ => new SARIAggregatedMetrics
                {
                    PortfolioId = portfolioId,
                    Granularity = granularity
                });
                
                var window = _aggregationWindows[granularity];
                var historicalData = GetRecentHistory(portfolioId, window);
                
                if (historicalData.Any())
                {
                    metrics.AverageSARI = historicalData.Average(h => h.SARIIndex);
                    metrics.MinSARI = historicalData.Min(h => h.SARIIndex);
                    metrics.MaxSARI = historicalData.Max(h => h.SARIIndex);
                    metrics.StdDevSARI = CalculateStandardDeviation(historicalData.Select(h => h.SARIIndex));
                    metrics.DataPoints = historicalData.Count;
                    metrics.LastUpdated = DateTime.UtcNow;
                    
                    // Risk level distribution
                    metrics.RiskLevelDistribution = historicalData
                        .GroupBy(h => h.RiskLevel)
                        .ToDictionary(g => g.Key, g => (float)g.Count() / historicalData.Count);
                    
                    // Scenario contribution averages
                    var allScenarios = historicalData
                        .SelectMany(h => h.ScenarioContributions.Keys)
                        .Distinct()
                        .ToList();
                    
                    metrics.AverageScenarioContributions = new Dictionary<string, float>();
                    foreach (var scenario in allScenarios)
                    {
                        var contributions = historicalData
                            .Where(h => h.ScenarioContributions.ContainsKey(scenario))
                            .Select(h => h.ScenarioContributions[scenario])
                            .ToList();
                        
                        if (contributions.Any())
                        {
                            metrics.AverageScenarioContributions[scenario] = contributions.Average();
                        }
                    }
                }
            }
        }

        private List<SARIHistoricalPoint> GetHistoricalData(string portfolioId, TimeGranularity granularity)
        {
            if (!_historicalData.TryGetValue(portfolioId, out var buffer))
                return new List<SARIHistoricalPoint>();
            
            var window = _aggregationWindows[granularity];
            var cutoff = DateTime.UtcNow - window;
            
            return buffer.GetAll()
                .Where(p => p.Timestamp >= cutoff)
                .OrderBy(p => p.Timestamp)
                .ToList();
        }

        private List<SARIHistoricalPoint> GetHistoricalDataRange(
            string portfolioId,
            DateTime startDate,
            DateTime endDate)
        {
            if (!_historicalData.TryGetValue(portfolioId, out var buffer))
                return new List<SARIHistoricalPoint>();
            
            return buffer.GetAll()
                .Where(p => p.Timestamp >= startDate && p.Timestamp <= endDate)
                .OrderBy(p => p.Timestamp)
                .ToList();
        }

        private List<SARIHistoricalPoint> GetRecentHistory(string portfolioId, TimeSpan window)
        {
            if (!_historicalData.TryGetValue(portfolioId, out var buffer))
                return new List<SARIHistoricalPoint>();
            
            var cutoff = DateTime.UtcNow - window;
            return buffer.GetAll()
                .Where(p => p.Timestamp >= cutoff)
                .OrderBy(p => p.Timestamp)
                .ToList();
        }

        private SARIStatistics CalculateStatistics(List<SARIHistoricalPoint> data)
        {
            if (!data.Any())
                return new SARIStatistics();
            
            var sariValues = data.Select(d => d.SARIIndex).ToList();
            
            return new SARIStatistics
            {
                Mean = sariValues.Average(),
                Median = CalculateMedian(sariValues),
                StdDev = CalculateStandardDeviation(sariValues),
                Min = sariValues.Min(),
                Max = sariValues.Max(),
                Percentile95 = CalculatePercentile(sariValues, 0.95f),
                Percentile99 = CalculatePercentile(sariValues, 0.99f),
                Skewness = CalculateSkewness(sariValues),
                Kurtosis = CalculateKurtosis(sariValues),
                DataPoints = data.Count
            };
        }

        private float CalculateStandardDeviation(IEnumerable<float> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            
            var avg = list.Average();
            var sum = list.Sum(v => Math.Pow(v - avg, 2));
            return (float)Math.Sqrt(sum / (list.Count - 1));
        }

        private float CalculateMedian(List<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            
            if (n % 2 == 0)
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
            else
                return sorted[n / 2];
        }

        private float CalculatePercentile(List<float> values, float percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            
            if (n == 0) return 0;
            if (n == 1) return sorted[0];
            
            float position = (n - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            
            if (lower == upper)
                return sorted[lower];
            
            float weight = position - lower;
            return sorted[lower] * (1 - weight) + sorted[upper] * weight;
        }

        private float CalculateSkewness(List<float> values)
        {
            if (values.Count < 3) return 0;
            
            var mean = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            
            if (stdDev == 0) return 0;
            
            var n = values.Count;
            var sum = values.Sum(v => Math.Pow((v - mean) / stdDev, 3));
            
            return (float)(n * sum / ((n - 1) * (n - 2)));
        }

        private float CalculateKurtosis(List<float> values)
        {
            if (values.Count < 4) return 0;
            
            var mean = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            
            if (stdDev == 0) return 0;
            
            var n = values.Count;
            var sum = values.Sum(v => Math.Pow((v - mean) / stdDev, 4));
            
            return (float)(n * (n + 1) * sum / ((n - 1) * (n - 2) * (n - 3)) - 
                          3 * Math.Pow(n - 1, 2) / ((n - 2) * (n - 3)));
        }

        private SARITrendAnalysis CalculateTrendAnalysis(List<SARIHistoricalPoint> data)
        {
            var analysis = new SARITrendAnalysis();
            
            if (data.Count < 2)
                return analysis;
            
            // Linear regression for trend
            var x = Enumerable.Range(0, data.Count).Select(i => (float)i).ToList();
            var y = data.Select(d => d.SARIIndex).ToList();
            
            var n = data.Count;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumX2 = x.Sum(xi => xi * xi);
            
            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;
            
            analysis.TrendSlope = slope;
            analysis.TrendIntercept = intercept;
            analysis.TrendDirection = slope > 0.001f ? TrendDirection.Increasing :
                                     slope < -0.001f ? TrendDirection.Decreasing :
                                     TrendDirection.Stable;
            
            // Calculate R-squared
            var yMean = y.Average();
            var ssTotal = y.Sum(yi => Math.Pow(yi - yMean, 2));
            var ssResidual = x.Zip(y, (xi, yi) => Math.Pow(yi - (slope * xi + intercept), 2)).Sum();
            analysis.RSquared = (float)(1 - ssResidual / ssTotal);
            
            // Moving averages
            if (data.Count >= 20)
            {
                analysis.MA20 = data.TakeLast(20).Average(d => d.SARIIndex);
            }
            if (data.Count >= 50)
            {
                analysis.MA50 = data.TakeLast(50).Average(d => d.SARIIndex);
            }
            
            // Volatility
            analysis.Volatility = CalculateStandardDeviation(y);
            
            return analysis;
        }

        private SARIMonitoringModel MapToMonitoringModel(SARIResult result)
        {
            return new SARIMonitoringModel
            {
                Timestamp = result.Timestamp,
                SARIIndex = result.SARIIndex,
                RiskLevel = result.RiskLevel,
                MarketRegime = result.MarketRegime,
                ScenarioBreakdown = result.ScenarioResults
                    .OrderByDescending(s => s.WeightedImpact)
                    .Select(s => new ScenarioContribution
                    {
                        ScenarioName = s.ScenarioName,
                        WeightedImpact = s.WeightedImpact,
                        Probability = s.Probability,
                        StressLoss = s.StressLoss
                    })
                    .ToList(),
                ComponentBreakdown = result.ComponentContributions
                    .Select(c => new ComponentContribution
                    {
                        ComponentName = c.Key,
                        Contribution = c.Value
                    })
                    .ToList(),
                TimeHorizonResults = result.TimeHorizonResults,
                TopRecommendations = result.Recommendations
                    .Take(5)
                    .Select(r => new MonitoringRecommendation
                    {
                        Action = r.Action,
                        Details = r.Details,
                        Priority = r.Priority
                    })
                    .ToList()
            };
        }

        private void InitializeAggregationWindows()
        {
            _aggregationWindows = new Dictionary<TimeGranularity, TimeSpan>
            {
                [TimeGranularity.OneMinute] = TimeSpan.FromHours(1),
                [TimeGranularity.FiveMinute] = TimeSpan.FromHours(4),
                [TimeGranularity.FifteenMinute] = TimeSpan.FromHours(12),
                [TimeGranularity.Hourly] = TimeSpan.FromDays(2),
                [TimeGranularity.Daily] = TimeSpan.FromDays(30)
            };
        }

        private void InitializeHistoricalBuffers()
        {
            // Buffers will be created on-demand for each portfolio
            LogDebug("Historical buffers initialized");
        }

        private void SetupRealTimeStreams()
        {
            // Setup stream error handling
            _sariUpdateStream
                .Retry(3)
                .Subscribe(
                    update => LogDebug($"SARI update published for portfolio {update.PortfolioId}"),
                    error => LogError("Error in SARI update stream", error as Exception));
            
            _alertStream
                .Retry(3)
                .Subscribe(
                    alert => LogDebug($"Alert published: {alert.Title} for portfolio {alert.PortfolioId}"),
                    error => LogError("Error in alert stream", error as Exception));
        }

        private async Task<SARIMonitoringConfiguration> LoadConfigurationAsync(CancellationToken cancellationToken)
        {
            // In production, load from configuration service
            return new SARIMonitoringConfiguration
            {
                MonitoringInterval = TimeSpan.FromMinutes(1),
                HistoricalBufferSize = 10000,
                AlertThresholds = new Dictionary<RiskLevel, float>
                {
                    [RiskLevel.High] = 0.20f,
                    [RiskLevel.VeryHigh] = 0.35f,
                    [RiskLevel.Critical] = 0.50f
                },
                EnableRealTimeStreaming = true,
                EnableHistoricalTracking = true,
                EnableAlerts = true
            };
        }

        private async Task InitializeAggregationMetricsAsync(CancellationToken cancellationToken)
        {
            // Initialize metrics structures
            await Task.CompletedTask;
        }

        private async Task MonitorAlertsAsync(CancellationToken cancellationToken)
        {
            // Continuous alert monitoring loop
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    // Additional alert checks can be performed here
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError("Error in alert monitoring", ex);
                }
            }
        }

        private async Task SaveStateAsync(CancellationToken cancellationToken)
        {
            // Save current state for recovery
            LogDebug("Saving monitoring service state");
            await Task.CompletedTask;
        }

        private async Task<List<string>> GetActivePortfoliosAsync(CancellationToken cancellationToken = default)
        {
            // In production, get from portfolio service
            return new List<string> { "default", "aggressive", "conservative" };
        }

        private async Task<Portfolio> LoadPortfolioAsync(string portfolioId, CancellationToken cancellationToken = default)
        {
            // In production, load from portfolio service
            return new Portfolio
            {
                Id = portfolioId,
                Name = $"Portfolio {portfolioId}",
                Holdings = new Dictionary<string, PortfolioHolding>()
            };
        }

        private async Task<MarketContext> GetMarketContextAsync(CancellationToken cancellationToken = default)
        {
            // In production, get from market data service
            return new MarketContext
            {
                Timestamp = DateTime.UtcNow,
                MarketRegime = MarketRegime.Normal,
                MarketTrend = MarketTrend.Neutral,
                MarketVolatility = 0.15f,
                MarketLiquidity = 0.85f
            };
        }

        private async Task<List<SARIAlert>> GetRecentAlertsAsync(
            string portfolioId,
            int count,
            CancellationToken cancellationToken)
        {
            // In production, retrieve from alert storage
            return new List<SARIAlert>();
        }

        private List<RiskLevelTransition> GetRiskTransitions(string portfolioId)
        {
            if (_riskTransitions.TryGetValue(portfolioId, out var transition))
            {
                return new List<RiskLevelTransition> { transition };
            }
            return new List<RiskLevelTransition>();
        }

        private ScenarioBreakdown GetScenarioBreakdown()
        {
            if (_lastSARIResult == null)
                return new ScenarioBreakdown();
            
            return new ScenarioBreakdown
            {
                Scenarios = _lastSARIResult.ScenarioResults
                    .OrderByDescending(s => s.WeightedImpact)
                    .Select(s => new ScenarioDetail
                    {
                        ScenarioId = s.ScenarioId,
                        ScenarioName = s.ScenarioName,
                        Probability = s.Probability,
                        Weight = s.Weight,
                        StressLoss = s.StressLoss,
                        WeightedImpact = s.WeightedImpact,
                        TimeToRecovery = s.TimeToRecovery
                    })
                    .ToList(),
                TotalImpact = _lastSARIResult.ScenarioResults.Sum(s => s.WeightedImpact)
            };
        }

        private async Task<PortfolioStressMetrics> GetStressMetricsAsync(
            string portfolioId,
            CancellationToken cancellationToken)
        {
            // Calculate aggregated stress metrics
            var metrics = new PortfolioStressMetrics
            {
                PortfolioId = portfolioId,
                CalculatedAt = DateTime.UtcNow
            };
            
            if (_lastSARIResult != null)
            {
                metrics.MaxDrawdown = _lastSARIResult.ScenarioResults.Max(s => s.StressLoss);
                metrics.ExpectedShortfall = CalculateExpectedShortfall(_lastSARIResult.ScenarioResults);
                metrics.StressVaR95 = CalculateStressVaR(_lastSARIResult.ScenarioResults, 0.95f);
                metrics.StressVaR99 = CalculateStressVaR(_lastSARIResult.ScenarioResults, 0.99f);
            }
            
            return metrics;
        }

        private float CalculateExpectedShortfall(List<ScenarioResult> scenarios)
        {
            var sortedLosses = scenarios
                .OrderByDescending(s => s.StressLoss)
                .ToList();
            
            // Take worst 5% of scenarios
            var tailCount = Math.Max(1, (int)(sortedLosses.Count * 0.05));
            return sortedLosses.Take(tailCount).Average(s => s.StressLoss);
        }

        private float CalculateStressVaR(List<ScenarioResult> scenarios, float confidence)
        {
            var sortedLosses = scenarios
                .OrderByDescending(s => s.StressLoss)
                .Select(s => s.StressLoss)
                .ToList();
            
            var index = (int)((1 - confidence) * sortedLosses.Count);
            return sortedLosses.ElementAtOrDefault(index);
        }

        private async Task<ScenarioAnalysis> AnalyzeScenarioContributionsAsync(
            string portfolioId,
            List<SARIHistoricalPoint> historicalPoints,
            CancellationToken cancellationToken)
        {
            var analysis = new ScenarioAnalysis();
            
            // Get all unique scenarios
            var allScenarios = historicalPoints
                .SelectMany(h => h.ScenarioContributions.Keys)
                .Distinct()
                .ToList();
            
            foreach (var scenario in allScenarios)
            {
                var contributions = historicalPoints
                    .Where(h => h.ScenarioContributions.ContainsKey(scenario))
                    .Select(h => new { h.Timestamp, Contribution = h.ScenarioContributions[scenario] })
                    .OrderBy(x => x.Timestamp)
                    .ToList();
                
                if (!contributions.Any()) continue;
                
                var scenarioStats = new ScenarioStatistics
                {
                    ScenarioName = scenario,
                    AverageContribution = contributions.Average(c => c.Contribution),
                    MaxContribution = contributions.Max(c => c.Contribution),
                    MinContribution = contributions.Min(c => c.Contribution),
                    StdDevContribution = CalculateStandardDeviation(contributions.Select(c => c.Contribution)),
                    Frequency = (float)contributions.Count / historicalPoints.Count,
                    TrendSlope = CalculateScenarioTrend(contributions)
                };
                
                analysis.ScenarioStatistics.Add(scenarioStats);
            }
            
            // Sort by average contribution
            analysis.ScenarioStatistics = analysis.ScenarioStatistics
                .OrderByDescending(s => s.AverageContribution)
                .ToList();
            
            return analysis;
        }

        private float CalculateScenarioTrend(List<(DateTime Timestamp, float Contribution)> contributions)
        {
            if (contributions.Count < 2) return 0;
            
            var x = Enumerable.Range(0, contributions.Count).Select(i => (float)i).ToList();
            var y = contributions.Select(c => c.Contribution).ToList();
            
            var n = contributions.Count;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumX2 = x.Sum(xi => xi * xi);
            
            return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        }

        private RiskLevelAnalysis AnalyzeRiskLevels(List<SARIHistoricalPoint> historicalPoints)
        {
            var analysis = new RiskLevelAnalysis();
            
            // Calculate time spent in each risk level
            var riskLevelGroups = historicalPoints
                .GroupBy(h => h.RiskLevel)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var total = historicalPoints.Count;
            
            foreach (var level in Enum.GetValues<RiskLevel>())
            {
                var count = riskLevelGroups.GetValueOrDefault(level, 0);
                analysis.TimeInLevel[level] = (float)count / total;
            }
            
            // Calculate transitions
            for (int i = 1; i < historicalPoints.Count; i++)
            {
                if (historicalPoints[i].RiskLevel != historicalPoints[i - 1].RiskLevel)
                {
                    var transition = $"{historicalPoints[i - 1].RiskLevel}->{historicalPoints[i].RiskLevel}";
                    analysis.Transitions[transition] = analysis.Transitions.GetValueOrDefault(transition, 0) + 1;
                }
            }
            
            // Average SARI by risk level
            foreach (var group in historicalPoints.GroupBy(h => h.RiskLevel))
            {
                analysis.AverageSARIByLevel[group.Key] = group.Average(h => h.SARIIndex);
            }
            
            return analysis;
        }

        private async Task<SARIForecast> GenerateForecastAsync(
            string portfolioId,
            List<SARIHistoricalPoint> historicalPoints,
            int horizonDays,
            CancellationToken cancellationToken)
        {
            var forecast = new SARIForecast
            {
                HorizonDays = horizonDays,
                GeneratedAt = DateTime.UtcNow
            };
            
            // Simple exponential smoothing forecast
            if (historicalPoints.Count < 10)
            {
                forecast.ForecastValues = new List<float>();
                return forecast;
            }
            
            var alpha = 0.3f; // Smoothing parameter
            var lastValue = historicalPoints.Last().SARIIndex;
            var trend = CalculateTrendAnalysis(historicalPoints).TrendSlope;
            
            forecast.ForecastValues = new List<float>();
            forecast.ConfidenceIntervals = new List<(float Lower, float Upper)>();
            
            var stdDev = CalculateStandardDeviation(historicalPoints.Select(h => h.SARIIndex));
            
            for (int i = 1; i <= horizonDays; i++)
            {
                var forecastValue = lastValue + (trend * i);
                forecast.ForecastValues.Add(forecastValue);
                
                // Confidence intervals widen with horizon
                var intervalWidth = stdDev * 1.96f * (float)Math.Sqrt(i);
                forecast.ConfidenceIntervals.Add((forecastValue - intervalWidth, forecastValue + intervalWidth));
            }
            
            forecast.ExpectedValue = forecast.ForecastValues.Average();
            forecast.Uncertainty = stdDev * (float)Math.Sqrt(horizonDays);
            
            return forecast;
        }

        private async Task<Dictionary<string, float>> AnalyzeCorrelationsAsync(
            string portfolioId,
            List<SARIHistoricalPoint> historicalPoints,
            CancellationToken cancellationToken)
        {
            var correlations = new Dictionary<string, float>();
            
            // Correlate SARI with market indicators
            // In production, would fetch market data and calculate actual correlations
            
            correlations["VIX"] = 0.75f; // Example: High correlation with volatility
            correlations["SPX"] = -0.45f; // Negative correlation with market
            correlations["DXY"] = 0.30f; // Some correlation with dollar strength
            
            return correlations;
        }

        private async Task ExportToCsvAsync(
            List<SARIHistoricalPoint> data,
            string filePath,
            CancellationToken cancellationToken)
        {
            var lines = new List<string>
            {
                "Timestamp,SARIIndex,RiskLevel,MarketRegime,TopScenario,TopScenarioImpact"
            };
            
            foreach (var point in data)
            {
                var topScenario = point.ScenarioContributions
                    .OrderByDescending(s => s.Value)
                    .FirstOrDefault();
                
                lines.Add($"{point.Timestamp:yyyy-MM-dd HH:mm:ss},{point.SARIIndex:F4}," +
                         $"{point.RiskLevel},{point.MarketRegime}," +
                         $"{topScenario.Key},{topScenario.Value:F4}");
            }
            
            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }

        private async Task ExportToJsonAsync(
            List<SARIHistoricalPoint> data,
            string filePath,
            CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        private async Task ExportToExcelAsync(
            List<SARIHistoricalPoint> data,
            string filePath,
            CancellationToken cancellationToken)
        {
            // In production, use a library like ClosedXML or EPPlus
            // For now, export as CSV with .xlsx extension warning
            LogWarning("Excel export not fully implemented, exporting as CSV format");
            await ExportToCsvAsync(data, filePath.Replace(".xlsx", ".csv"), cancellationToken);
        }
    }

    // Supporting classes and DTOs

    public class SARIMonitoringData
    {
        public string PortfolioId { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeGranularity Granularity { get; set; }
        public SARIMonitoringModel CurrentSARI { get; set; }
        public List<SARIHistoricalPoint> HistoricalData { get; set; }
        public SARIAggregatedMetrics AggregatedMetrics { get; set; }
        public List<SARIAlert> RecentAlerts { get; set; }
        public List<RiskLevelTransition> RiskTransitions { get; set; }
        public ScenarioBreakdown ScenarioBreakdown { get; set; }
        public PortfolioStressMetrics StressMetrics { get; set; }
    }

    public class SARIMonitoringModel
    {
        public DateTime Timestamp { get; set; }
        public float SARIIndex { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public List<ScenarioContribution> ScenarioBreakdown { get; set; }
        public List<ComponentContribution> ComponentBreakdown { get; set; }
        public Dictionary<TimeHorizon, float> TimeHorizonResults { get; set; }
        public List<MonitoringRecommendation> TopRecommendations { get; set; }
    }

    public class SARIMonitoringUpdate
    {
        public string PortfolioId { get; set; }
        public DateTime Timestamp { get; set; }
        public float SARIIndex { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public Dictionary<string, float> ScenarioContributions { get; set; }
        public Dictionary<string, float> ComponentContributions { get; set; }
        public Dictionary<TimeHorizon, float> TimeHorizonResults { get; set; }
        public List<MonitoringRecommendation> TopRecommendations { get; set; }
    }

    public class SARIHistoricalPoint
    {
        public DateTime Timestamp { get; set; }
        public float SARIIndex { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public Dictionary<string, float> ScenarioContributions { get; set; }
        public Dictionary<string, float> ComponentContributions { get; set; }
    }

    public class SARIAlert
    {
        public string Id { get; set; }
        public string PortfolioId { get; set; }
        public DateTime Timestamp { get; set; }
        public AlertSeverity Severity { get; set; }
        public AlertType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
    }

    public class SARIAggregatedMetrics
    {
        public string PortfolioId { get; set; }
        public TimeGranularity Granularity { get; set; }
        public float AverageSARI { get; set; }
        public float MinSARI { get; set; }
        public float MaxSARI { get; set; }
        public float StdDevSARI { get; set; }
        public int DataPoints { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<RiskLevel, float> RiskLevelDistribution { get; set; }
        public Dictionary<string, float> AverageScenarioContributions { get; set; }
    }

    public class RiskLevelTransition
    {
        public RiskLevel PreviousLevel { get; set; }
        public RiskLevel CurrentLevel { get; set; }
        public DateTime TransitionTime { get; set; }
        public int TransitionCount { get; set; }
    }

    public class ScenarioContribution
    {
        public string ScenarioName { get; set; }
        public float WeightedImpact { get; set; }
        public float Probability { get; set; }
        public float StressLoss { get; set; }
    }

    public class ComponentContribution
    {
        public string ComponentName { get; set; }
        public float Contribution { get; set; }
    }

    public class MonitoringRecommendation
    {
        public string Action { get; set; }
        public string Details { get; set; }
        public RecommendationPriority Priority { get; set; }
    }

    public class ScenarioBreakdown
    {
        public List<ScenarioDetail> Scenarios { get; set; } = new();
        public float TotalImpact { get; set; }
    }

    public class ScenarioDetail
    {
        public string ScenarioId { get; set; }
        public string ScenarioName { get; set; }
        public float Probability { get; set; }
        public float Weight { get; set; }
        public float StressLoss { get; set; }
        public float WeightedImpact { get; set; }
        public float TimeToRecovery { get; set; }
    }

    public class PortfolioStressMetrics
    {
        public string PortfolioId { get; set; }
        public DateTime CalculatedAt { get; set; }
        public float MaxDrawdown { get; set; }
        public float ExpectedShortfall { get; set; }
        public float StressVaR95 { get; set; }
        public float StressVaR99 { get; set; }
        public Dictionary<string, float> ScenarioDrawdowns { get; set; }
    }

    public class SARIAnalytics
    {
        public string PortfolioId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public SARIStatistics Statistics { get; set; }
        public SARITrendAnalysis TrendAnalysis { get; set; }
        public ScenarioAnalysis ScenarioAnalysis { get; set; }
        public RiskLevelAnalysis RiskLevelAnalysis { get; set; }
        public SARIForecast Forecast { get; set; }
        public Dictionary<string, float> Correlations { get; set; }
    }

    public class SARIStatistics
    {
        public float Mean { get; set; }
        public float Median { get; set; }
        public float StdDev { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public float Percentile95 { get; set; }
        public float Percentile99 { get; set; }
        public float Skewness { get; set; }
        public float Kurtosis { get; set; }
        public int DataPoints { get; set; }
    }

    public class SARITrendAnalysis
    {
        public float TrendSlope { get; set; }
        public float TrendIntercept { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public float RSquared { get; set; }
        public float MA20 { get; set; }
        public float MA50 { get; set; }
        public float Volatility { get; set; }
    }

    public class ScenarioAnalysis
    {
        public List<ScenarioStatistics> ScenarioStatistics { get; set; } = new();
    }

    public class ScenarioStatistics
    {
        public string ScenarioName { get; set; }
        public float AverageContribution { get; set; }
        public float MaxContribution { get; set; }
        public float MinContribution { get; set; }
        public float StdDevContribution { get; set; }
        public float Frequency { get; set; }
        public float TrendSlope { get; set; }
    }

    public class RiskLevelAnalysis
    {
        public Dictionary<RiskLevel, float> TimeInLevel { get; set; } = new();
        public Dictionary<string, int> Transitions { get; set; } = new();
        public Dictionary<RiskLevel, float> AverageSARIByLevel { get; set; } = new();
    }

    public class SARIForecast
    {
        public int HorizonDays { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<float> ForecastValues { get; set; }
        public List<(float Lower, float Upper)> ConfidenceIntervals { get; set; }
        public float ExpectedValue { get; set; }
        public float Uncertainty { get; set; }
    }

    public class AnalyticsOptions
    {
        public bool IncludeTrendAnalysis { get; set; } = true;
        public bool IncludeScenarioAnalysis { get; set; } = true;
        public bool IncludeRiskLevelAnalysis { get; set; } = true;
        public bool IncludeForecasting { get; set; } = true;
        public bool IncludeCorrelations { get; set; } = true;
        public int ForecastHorizon { get; set; } = 7; // days
    }

    public class SARIMonitoringConfiguration
    {
        public TimeSpan MonitoringInterval { get; set; }
        public int HistoricalBufferSize { get; set; }
        public Dictionary<RiskLevel, float> AlertThresholds { get; set; }
        public bool EnableRealTimeStreaming { get; set; }
        public bool EnableHistoricalTracking { get; set; }
        public bool EnableAlerts { get; set; }
    }

    public enum TimeGranularity
    {
        OneMinute,
        FiveMinute,
        FifteenMinute,
        Hourly,
        Daily
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum AlertType
    {
        RiskLevel,
        ScenarioImpact,
        RapidChange,
        ThresholdBreach,
        SystemError
    }

    public enum TrendDirection
    {
        Decreasing,
        Stable,
        Increasing
    }

    public enum ExportFormat
    {
        CSV,
        JSON,
        Excel
    }

    public enum TimeHorizon
    {
        OneDay,
        OneWeek,
        OneMonth,
        ThreeMonth,
        SixMonth
    }

    // Utility classes

    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;
        private readonly object _lock = new object();

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_tail] = item;
                _tail = (_tail + 1) % _buffer.Length;
                
                if (_count < _buffer.Length)
                {
                    _count++;
                }
                else
                {
                    _head = (_head + 1) % _buffer.Length;
                }
            }
        }

        public List<T> GetAll()
        {
            lock (_lock)
            {
                var result = new List<T>(_count);
                var index = _head;
                
                for (int i = 0; i < _count; i++)
                {
                    result.Add(_buffer[index]);
                    index = (index + 1) % _buffer.Length;
                }
                
                return result;
            }
        }
    }

    // SignalR Hub
    public class SARIMonitoringHub : Hub
    {
        private readonly ICanonicalLogger _logger;

        public SARIMonitoringHub(ICanonicalLogger logger)
        {
            _logger = logger;
        }

        public async Task JoinPortfolioGroup(string portfolioId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"portfolio_{portfolioId}");
            _logger.LogDebug($"Client {Context.ConnectionId} joined portfolio group {portfolioId}");
        }

        public async Task LeavePortfolioGroup(string portfolioId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"portfolio_{portfolioId}");
            _logger.LogDebug($"Client {Context.ConnectionId} left portfolio group {portfolioId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogDebug($"Client {Context.ConnectionId} disconnected");
            await base.OnDisconnectedAsync(exception);
        }
    }
}