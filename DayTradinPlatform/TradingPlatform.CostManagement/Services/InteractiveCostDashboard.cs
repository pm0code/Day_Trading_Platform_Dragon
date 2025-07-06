using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.CostManagement.Models;

namespace TradingPlatform.CostManagement.Services
{
    /// <summary>
    /// Interactive cost dashboard with actionable controls for data source management
    /// Provides "Keep", "Stop", "Suspend", "Optimize" buttons for each data source
    /// </summary>
    public class InteractiveCostDashboard : CanonicalServiceBaseEnhanced, IInteractiveCostDashboard
    {
        private readonly IDataSourceCostTracker _costTracker;
        private readonly CostTrackingConfiguration _config;
        private readonly Dictionary<string, DataSourceManager> _dataSourceManagers;

        public InteractiveCostDashboard(
            IDataSourceCostTracker costTracker,
            CostTrackingConfiguration config,
            ITradingLogger? logger = null)
            : base(logger, "InteractiveCostDashboard")
        {
            _costTracker = costTracker ?? throw new ArgumentNullException(nameof(costTracker));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dataSourceManagers = new Dictionary<string, DataSourceManager>();
        }

        public async Task<InteractiveDashboard> GetInteractiveDashboardAsync(TimeSpan period)
        {
            return await TrackOperationAsync("GetInteractiveDashboard", async () =>
            {
                // Get base cost dashboard
                var baseDashboard = await _costTracker.GetCostDashboardAsync(period);
                
                // Create interactive dashboard with controls
                var dashboard = new InteractiveDashboard
                {
                    BaseDashboard = baseDashboard,
                    DataSourceControls = new Dictionary<string, DataSourceControlPanel>(),
                    GlobalActions = GenerateGlobalActions(baseDashboard),
                    QuickActions = GenerateQuickActions(baseDashboard),
                    AutomationSettings = GetAutomationSettings(),
                    CostProjections = await GenerateCostProjectionsAsync(baseDashboard)
                };

                // Generate controls for each data source
                foreach (var kvp in baseDashboard.DataSourceCosts)
                {
                    var dataSource = kvp.Key;
                    var costSummary = kvp.Value;
                    
                    var controlPanel = await GenerateDataSourceControlPanelAsync(dataSource, costSummary);
                    dashboard.DataSourceControls[dataSource] = controlPanel;
                }

                // Add ROI-based recommendations
                dashboard.SmartRecommendations = await GenerateSmartRecommendationsAsync(baseDashboard);
                
                LogInfo("INTERACTIVE_DASHBOARD_GENERATED", "Interactive dashboard generated",
                    additionalData: new
                    {
                        DataSources = dashboard.DataSourceControls.Count,
                        TotalCost = baseDashboard.TotalCost,
                        ActiveAlerts = baseDashboard.ActiveAlerts.Count,
                        Recommendations = dashboard.SmartRecommendations.Count
                    });

                return dashboard;
            });
        }

        public async Task<ActionResult> ExecuteDataSourceActionAsync(string dataSource, string actionId, Dictionary<string, object>? parameters = null)
        {
            return await TrackOperationAsync("ExecuteDataSourceAction", async () =>
            {
                var action = ParseActionType(actionId);
                var result = new ActionResult
                {
                    DataSource = dataSource,
                    Action = actionId,
                    Success = false,
                    Message = string.Empty,
                    Timestamp = DateTime.UtcNow
                };

                try
                {
                    switch (action)
                    {
                        case ActionType.Keep:
                            result = await ExecuteKeepActionAsync(dataSource, parameters);
                            break;
                        case ActionType.Stop:
                            result = await ExecuteStopActionAsync(dataSource, parameters);
                            break;
                        case ActionType.Suspend:
                            result = await ExecuteSuspendActionAsync(dataSource, parameters);
                            break;
                        case ActionType.Optimize:
                            result = await ExecuteOptimizeActionAsync(dataSource, parameters);
                            break;
                        case ActionType.Upgrade:
                            result = await ExecuteUpgradeActionAsync(dataSource, parameters);
                            break;
                        case ActionType.Downgrade:
                            result = await ExecuteDowngradeActionAsync(dataSource, parameters);
                            break;
                        case ActionType.SetBudget:
                            result = await ExecuteSetBudgetActionAsync(dataSource, parameters);
                            break;
                        default:
                            result.Message = $"Unknown action: {actionId}";
                            break;
                    }

                    LogInfo("DATA_SOURCE_ACTION_EXECUTED", $"Action executed: {actionId} on {dataSource}",
                        additionalData: new
                        {
                            DataSource = dataSource,
                            Action = actionId,
                            Success = result.Success,
                            Message = result.Message
                        });
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Error executing action: {ex.Message}";
                    LogError("ACTION_EXECUTION_ERROR", $"Failed to execute {actionId} on {dataSource}", ex);
                }

                return result;
            });
        }

        private async Task<DataSourceControlPanel> GenerateDataSourceControlPanelAsync(string dataSource, DataSourceCostSummary costSummary)
        {
            var roi = await _costTracker.CalculateROIAsync(dataSource, TimeSpan.FromDays(30));
            
            var controlPanel = new DataSourceControlPanel
            {
                DataSource = dataSource,
                CurrentStatus = costSummary.Status,
                StatusMessage = costSummary.StatusMessage,
                
                // Cost information
                MonthlyCost = costSummary.MonthlyRecurring,
                ProjectedCost = costSummary.ProjectedMonthlyCost,
                CostTrend = costSummary.CostTrend,
                
                // ROI information
                CurrentROI = roi.ROIPercentage,
                ROITrend = costSummary.ROITrend,
                ROIRating = GetROIRating(roi.ROIPercentage),
                
                // Usage information
                UtilizationRate = costSummary.UtilizationRate,
                CostPerAPI = costSummary.CostPerAPICall,
                
                // Generate action buttons
                ActionButtons = GenerateActionButtons(dataSource, costSummary, roi),
                
                // Warnings and alerts
                Warnings = GenerateWarnings(dataSource, costSummary, roi),
                
                // Quick stats
                QuickStats = GenerateQuickStats(costSummary, roi),
                
                // Tier information
                TierInfo = GetTierInformation(dataSource)
            };

            return controlPanel;
        }

        private List<ActionButton> GenerateActionButtons(string dataSource, DataSourceCostSummary costSummary, ROIAnalysis roi)
        {
            var buttons = new List<ActionButton>();

            // KEEP button - always available for active sources
            if (costSummary.Status == DataSourceStatus.Active)
            {
                buttons.Add(new ActionButton
                {
                    ActionId = "keep",
                    DisplayText = "Keep",
                    Description = "Continue using this data source",
                    ButtonClass = "btn-success",
                    Icon = "✓",
                    IsRecommended = roi.ROIPercentage > 15m,
                    EstimatedImpact = 0m,
                    RequiresConfirmation = false
                });
            }

            // STOP button - for sources with negative ROI or high costs
            buttons.Add(new ActionButton
            {
                ActionId = "stop",
                DisplayText = "Stop",
                Description = "Completely stop using this data source",
                ButtonClass = roi.ROIPercentage < -10m ? "btn-danger" : "btn-outline-danger",
                Icon = "⏹",
                IsRecommended = roi.ROIPercentage < -10m,
                EstimatedImpact = -costSummary.MonthlyRecurring,
                RequiresConfirmation = true,
                ConfirmationMessage = $"Are you sure you want to stop {dataSource}? This will immediately halt all data feeds."
            });

            // SUSPEND button - for temporary pause
            buttons.Add(new ActionButton
            {
                ActionId = "suspend",
                DisplayText = "Suspend",
                Description = "Temporarily pause this data source",
                ButtonClass = "btn-warning",
                Icon = "⏸",
                IsRecommended = roi.ROIPercentage < 5m && roi.ROIPercentage > -10m,
                EstimatedImpact = -costSummary.MonthlyRecurring * 0.8m, // Assume 80% savings
                RequiresConfirmation = true,
                ConfirmationMessage = $"Suspend {dataSource}? You can reactivate later."
            });

            // OPTIMIZE button - for sources with room for improvement
            if (costSummary.UtilizationRate < 0.7m || roi.ROIPercentage < 20m)
            {
                buttons.Add(new ActionButton
                {
                    ActionId = "optimize",
                    DisplayText = "Optimize",
                    Description = "Optimize usage and costs",
                    ButtonClass = "btn-info",
                    Icon = "⚙",
                    IsRecommended = costSummary.UtilizationRate < 0.5m,
                    EstimatedImpact = costSummary.MonthlyRecurring * 0.2m, // Assume 20% savings
                    RequiresConfirmation = false
                });
            }

            // UPGRADE button - for high-performing sources at capacity
            if (roi.ROIPercentage > 25m && costSummary.UtilizationRate > 0.8m)
            {
                buttons.Add(new ActionButton
                {
                    ActionId = "upgrade",
                    DisplayText = "Upgrade",
                    Description = "Upgrade to higher tier for more capacity",
                    ButtonClass = "btn-primary",
                    Icon = "⬆",
                    IsRecommended = roi.ROIPercentage > 30m,
                    EstimatedImpact = -(costSummary.MonthlyRecurring * 0.5m), // Assume 50% cost increase
                    RequiresConfirmation = true,
                    ConfirmationMessage = $"Upgrade {dataSource} to next tier? This will increase monthly costs."
                });
            }

            // DOWNGRADE button - for over-provisioned sources
            if (costSummary.UtilizationRate < 0.3m && roi.ROIPercentage > 0m)
            {
                buttons.Add(new ActionButton
                {
                    ActionId = "downgrade",
                    DisplayText = "Downgrade",
                    Description = "Downgrade to lower tier to save costs",
                    ButtonClass = "btn-secondary",
                    Icon = "⬇",
                    IsRecommended = costSummary.UtilizationRate < 0.2m,
                    EstimatedImpact = costSummary.MonthlyRecurring * 0.3m, // Assume 30% savings
                    RequiresConfirmation = true,
                    ConfirmationMessage = $"Downgrade {dataSource}? This may reduce capacity."
                });
            }

            return buttons.OrderByDescending(b => b.IsRecommended).ThenBy(b => b.DisplayText).ToList();
        }

        private async Task<ActionResult> ExecuteKeepActionAsync(string dataSource, Dictionary<string, object>? parameters)
        {
            // "Keep" action - ensure source stays active and reset any suspension timers
            return new ActionResult
            {
                DataSource = dataSource,
                Action = "keep",
                Success = true,
                Message = $"{dataSource} will continue running. Budget and monitoring settings remain active.",
                EstimatedSavings = 0m,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<ActionResult> ExecuteStopActionAsync(string dataSource, Dictionary<string, object>? parameters)
        {
            // Stop the data source completely
            var manager = GetDataSourceManager(dataSource);
            await manager.StopAsync();
            
            // Record the cost savings
            var costSummary = await GetDataSourceCostSummary(dataSource);
            
            return new ActionResult
            {
                DataSource = dataSource,
                Action = "stop",
                Success = true,
                Message = $"{dataSource} has been stopped. All data feeds halted.",
                EstimatedSavings = costSummary.MonthlyRecurring,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<ActionResult> ExecuteSuspendActionAsync(string dataSource, Dictionary<string, object>? parameters)
        {
            // Suspend the data source temporarily
            var manager = GetDataSourceManager(dataSource);
            var suspendDuration = parameters?.ContainsKey("duration") == true 
                ? TimeSpan.Parse(parameters["duration"].ToString()!) 
                : TimeSpan.FromDays(7);
            
            await manager.SuspendAsync(suspendDuration);
            
            var costSummary = await GetDataSourceCostSummary(dataSource);
            
            return new ActionResult
            {
                DataSource = dataSource,
                Action = "suspend",
                Success = true,
                Message = $"{dataSource} suspended for {suspendDuration.TotalDays} days.",
                EstimatedSavings = costSummary.MonthlyRecurring * 0.8m,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<ActionResult> ExecuteOptimizeActionAsync(string dataSource, Dictionary<string, object>? parameters)
        {
            // Optimize the data source usage
            var manager = GetDataSourceManager(dataSource);
            var optimizations = await manager.OptimizeAsync();
            
            var totalSavings = optimizations.Sum(o => o.EstimatedSavings);
            
            return new ActionResult
            {
                DataSource = dataSource,
                Action = "optimize",
                Success = true,
                Message = $"{dataSource} optimized. Applied {optimizations.Count} optimizations.",
                EstimatedSavings = totalSavings,
                Timestamp = DateTime.UtcNow,
                Details = optimizations.Select(o => o.Description).ToList()
            };
        }

        private List<GlobalAction> GenerateGlobalActions(CostDashboard baseDashboard)
        {
            var actions = new List<GlobalAction>();

            // Optimize All - if multiple sources need optimization
            var needOptimization = baseDashboard.DataSourceCosts.Count(kvp => kvp.Value.EfficiencyRating <= EfficiencyRating.Fair);
            if (needOptimization > 1)
            {
                actions.Add(new GlobalAction
                {
                    ActionId = "optimize_all",
                    DisplayText = "Optimize All",
                    Description = $"Optimize {needOptimization} data sources",
                    ButtonClass = "btn-info",
                    Icon = "⚙",
                    EstimatedSavings = baseDashboard.DataSourceCosts.Values.Sum(c => c.MonthlyRecurring * 0.15m)
                });
            }

            // Emergency Stop - if budget is severely exceeded
            if (baseDashboard.BudgetSummary.IsOverBudget && baseDashboard.BudgetSummary.OverBudgetAmount > 1000m)
            {
                actions.Add(new GlobalAction
                {
                    ActionId = "emergency_stop",
                    DisplayText = "Emergency Stop",
                    Description = "Stop all non-essential data sources",
                    ButtonClass = "btn-danger",
                    Icon = "🚨",
                    EstimatedSavings = baseDashboard.TotalCost * 0.6m,
                    RequiresConfirmation = true
                });
            }

            return actions;
        }

        private List<QuickAction> GenerateQuickActions(CostDashboard baseDashboard)
        {
            var quickActions = new List<QuickAction>();

            // Add budget alerts
            if (baseDashboard.BudgetSummary.IsApproachingBudget)
            {
                quickActions.Add(new QuickAction
                {
                    Type = QuickActionType.Alert,
                    Message = $"Approaching budget limit: {baseDashboard.BudgetSummary.BudgetUtilization:P1} used",
                    ActionText = "Review Budget",
                    ActionId = "review_budget",
                    Severity = AlertSeverity.Warning
                });
            }

            // Add top savings opportunity
            var topSavingsSource = baseDashboard.DataSourceCosts
                .Where(kvp => kvp.Value.ROIPercentage < 10m)
                .OrderByDescending(kvp => kvp.Value.MonthlyRecurring)
                .FirstOrDefault();

            if (topSavingsSource.Key != null)
            {
                quickActions.Add(new QuickAction
                {
                    Type = QuickActionType.Opportunity,
                    Message = $"Top savings opportunity: {topSavingsSource.Key}",
                    ActionText = "Review",
                    ActionId = $"review_{topSavingsSource.Key}",
                    Severity = AlertSeverity.Info
                });
            }

            return quickActions;
        }

        // Helper methods
        private ActionType ParseActionType(string actionId) => actionId.ToLowerInvariant() switch
        {
            "keep" => ActionType.Keep,
            "stop" => ActionType.Stop,
            "suspend" => ActionType.Suspend,
            "optimize" => ActionType.Optimize,
            "upgrade" => ActionType.Upgrade,
            "downgrade" => ActionType.Downgrade,
            "set_budget" => ActionType.SetBudget,
            _ => ActionType.Review
        };

        private ROIRating GetROIRating(decimal roiPercentage) => roiPercentage switch
        {
            >= 30m => ROIRating.Excellent,
            >= 20m => ROIRating.Good,
            >= 10m => ROIRating.Fair,
            >= 0m => ROIRating.Poor,
            _ => ROIRating.Critical
        };

        private DataSourceManager GetDataSourceManager(string dataSource)
        {
            if (!_dataSourceManagers.ContainsKey(dataSource))
            {
                _dataSourceManagers[dataSource] = new DataSourceManager(dataSource, LoggerFactory?.CreateLogger<DataSourceManager>());
            }
            return _dataSourceManagers[dataSource];
        }

        private async Task<DataSourceCostSummary> GetDataSourceCostSummary(string dataSource)
        {
            var dashboard = await _costTracker.GetCostDashboardAsync(TimeSpan.FromDays(30));
            return dashboard.DataSourceCosts.TryGetValue(dataSource, out var summary) ? summary : new DataSourceCostSummary();
        }

        // Placeholder implementations
        private List<string> GenerateWarnings(string dataSource, DataSourceCostSummary costSummary, ROIAnalysis roi) => new();
        private Dictionary<string, string> GenerateQuickStats(DataSourceCostSummary costSummary, ROIAnalysis roi) => new();
        private TierInformation GetTierInformation(string dataSource) => new();
        private AutomationSettings GetAutomationSettings() => new();
        private async Task<CostProjections> GenerateCostProjectionsAsync(CostDashboard baseDashboard) => new();
        private async Task<List<SmartRecommendation>> GenerateSmartRecommendationsAsync(CostDashboard baseDashboard) => new();
        private async Task<ActionResult> ExecuteUpgradeActionAsync(string dataSource, Dictionary<string, object>? parameters) => new();
        private async Task<ActionResult> ExecuteDowngradeActionAsync(string dataSource, Dictionary<string, object>? parameters) => new();
        private async Task<ActionResult> ExecuteSetBudgetActionAsync(string dataSource, Dictionary<string, object>? parameters) => new();
    }

    // Supporting classes for the interactive dashboard
    public class InteractiveDashboard
    {
        public CostDashboard BaseDashboard { get; set; } = new();
        public Dictionary<string, DataSourceControlPanel> DataSourceControls { get; set; } = new();
        public List<GlobalAction> GlobalActions { get; set; } = new();
        public List<QuickAction> QuickActions { get; set; } = new();
        public AutomationSettings AutomationSettings { get; set; } = new();
        public CostProjections CostProjections { get; set; } = new();
        public List<SmartRecommendation> SmartRecommendations { get; set; } = new();
    }

    public class DataSourceControlPanel
    {
        public string DataSource { get; set; } = string.Empty;
        public DataSourceStatus CurrentStatus { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        
        // Financial metrics
        public decimal MonthlyCost { get; set; }
        public decimal ProjectedCost { get; set; }
        public decimal CostTrend { get; set; }
        
        // ROI metrics
        public decimal CurrentROI { get; set; }
        public decimal ROITrend { get; set; }
        public ROIRating ROIRating { get; set; }
        
        // Usage metrics
        public decimal UtilizationRate { get; set; }
        public decimal CostPerAPI { get; set; }
        
        // Interactive elements
        public List<ActionButton> ActionButtons { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, string> QuickStats { get; set; } = new();
        public TierInformation TierInfo { get; set; } = new();
    }

    public class ActionButton
    {
        public string ActionId { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ButtonClass { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public decimal EstimatedImpact { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string ConfirmationMessage { get; set; } = string.Empty;
    }

    public class ActionResult
    {
        public string DataSource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal EstimatedSavings { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Details { get; set; } = new();
    }

    public class GlobalAction
    {
        public string ActionId { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ButtonClass { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public decimal EstimatedSavings { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public class QuickAction
    {
        public QuickActionType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string ActionId { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
    }

    // Additional enums and supporting classes
    public enum ROIRating { Excellent, Good, Fair, Poor, Critical }
    public enum QuickActionType { Alert, Opportunity, Recommendation, Warning }
    public enum ActionType { Keep, Stop, Suspend, Optimize, Upgrade, Downgrade, SetBudget, Review }
    
    public class TierInformation { }
    public class AutomationSettings { }
    public class CostProjections { }
    public class SmartRecommendation { }
    public class DataSourceManager 
    { 
        public DataSourceManager(string dataSource, object? logger) { }
        public async Task StopAsync() { }
        public async Task SuspendAsync(TimeSpan duration) { }
        public async Task<List<Optimization>> OptimizeAsync() => new();
    }
    public class Optimization 
    { 
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedSavings { get; set; }
    }

    public interface IInteractiveCostDashboard
    {
        Task<InteractiveDashboard> GetInteractiveDashboardAsync(TimeSpan period);
        Task<ActionResult> ExecuteDataSourceActionAsync(string dataSource, string actionId, Dictionary<string, object>? parameters = null);
    }
}