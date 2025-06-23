using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.DisplayManagement.Services;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Views.Settings;

/// <summary>
/// Monitor selection and configuration view for DRAGON trading platform
/// Provides GPU detection and multi-monitor setup optimization
/// </summary>
public sealed partial class MonitorSelectionView : UserControl
{
    private readonly ILogger _logger;
    private readonly IGpuDetectionService _gpuDetectionService;
    private readonly IMonitorDetectionService _monitorDetectionService;
    
    private List<GpuInfo> _detectedGpus = new();
    private List<MonitorConfiguration> _connectedMonitors = new();
    private MonitorSelectionRecommendation _currentRecommendation = new();
    private MultiMonitorConfiguration _currentConfiguration = new();

    public MonitorSelectionView()
    {
        this.InitializeComponent();
        
        // Get services from DI container (assuming they're registered)
        var serviceProvider = App.Current.Services;
        _logger = serviceProvider.GetRequiredService<ILogger>();
        _gpuDetectionService = serviceProvider.GetRequiredService<IGpuDetectionService>();
        _monitorDetectionService = serviceProvider.GetRequiredService<IMonitorDetectionService>();
        
        _ = InitializeAsync();
    }

    /// <summary>
    /// Initialize the monitor selection view with GPU and monitor detection
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Initializing DRAGON monitor selection interface");
            
            await DetectSystemCapabilitiesAsync();
            await LoadSavedConfigurationAsync();
            UpdateUI();
            
            TradingLogOrchestrator.Instance.LogInfo("Monitor selection interface initialized successfully");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to initialize monitor selection interface");
            ShowErrorMessage("Failed to initialize monitor detection. Please check your system configuration.");
        }
    }

    /// <summary>
    /// Detect GPU capabilities and connected monitors
    /// </summary>
    private async Task DetectSystemCapabilitiesAsync()
    {
        // Detect GPUs
        _detectedGpus = await _gpuDetectionService.GetGpuInformationAsync();
        
        // Detect connected monitors
        _connectedMonitors = await _monitorDetectionService.GetConnectedMonitorsAsync();
        
        // Get recommendations
        _currentRecommendation = await _monitorDetectionService.GetMonitorRecommendationAsync();
        
        // Update slider maximum based on GPU capabilities
        this.DispatcherQueue.TryEnqueue(() =>
        {
            MonitorCountSlider.Maximum = _currentRecommendation.MaximumSupportedMonitors;
            MonitorCountSlider.Value = Math.Min(_connectedMonitors.Count, _currentRecommendation.RecommendedMonitorCount);
        });
    }

    /// <summary>
    /// Load previously saved monitor configuration
    /// </summary>
    private async Task LoadSavedConfigurationAsync()
    {
        var savedConfig = await _monitorDetectionService.LoadMonitorConfigurationAsync();
        if (savedConfig != null)
        {
            _currentConfiguration = savedConfig;
            TradingLogOrchestrator.Instance.LogInfo("Loaded saved monitor configuration with {MonitorCount} monitors", 
                savedConfig.Monitors.Count);
        }
        else
        {
            // Create default configuration
            _currentConfiguration = new MultiMonitorConfiguration
            {
                Monitors = _connectedMonitors,
                AutoAssignScreens = true,
                RememberWindowPositions = true
            };
        }
    }

    /// <summary>
    /// Update the UI with current system information
    /// </summary>
    private void UpdateUI()
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            UpdateGpuInfoDisplay();
            UpdateRecommendationDisplay();
            UpdateMonitorCountDisplay();
            UpdatePerformanceIndicator();
            UpdateScreenAssignmentGrid();
        });
    }

    /// <summary>
    /// Update GPU information display
    /// </summary>
    private void UpdateGpuInfoDisplay()
    {
        GpuInfoPanel.Children.Clear();

        if (!_detectedGpus.Any())
        {
            var errorText = new TextBlock
            {
                Text = "❌ No GPUs detected",
                Style = (Style)Resources["ErrorTextStyle"]
            };
            GpuInfoPanel.Children.Add(errorText);
            return;
        }

        foreach (var gpu in _detectedGpus)
        {
            var gpuPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            
            // GPU name and status
            var nameText = new TextBlock
            {
                Text = $"🎮 {gpu.Name}",
                Style = (Style)Resources["ValueTextStyle"],
                FontSize = 14
            };
            gpuPanel.Children.Add(nameText);
            
            // GPU specs
            var specsText = new TextBlock
            {
                Text = $"VRAM: {gpu.VideoMemoryGB:F1}GB • Max Outputs: {gpu.MaxDisplayOutputs} • Status: {(gpu.IsActive ? "Active" : "Inactive")}",
                Style = (Style)Resources["SubHeaderTextStyle"],
                FontSize = 12
            };
            gpuPanel.Children.Add(specsText);

            GpuInfoPanel.Children.Add(gpuPanel);
        }

        // Total system capability
        var totalVram = _detectedGpus.Sum(g => g.VideoMemoryGB);
        var totalOutputs = _detectedGpus.Sum(g => g.MaxDisplayOutputs);
        
        var totalPanel = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGreen) { Opacity = 0.2 },
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 8, 0, 0)
        };
        
        var totalText = new TextBlock
        {
            Text = $"🚀 Total System: {totalVram:F1}GB VRAM • {totalOutputs} Monitor Outputs",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        };
        totalPanel.Child = totalText;
        GpuInfoPanel.Children.Add(totalPanel);
    }

    /// <summary>
    /// Update recommendation display based on GPU assessment
    /// </summary>
    private void UpdateRecommendationDisplay()
    {
        RecommendationPanel.Children.Clear();

        // Recommended monitor count
        var countPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        countPanel.Children.Add(new TextBlock
        {
            Text = "Recommended Monitors:",
            Style = (Style)Resources["SubHeaderTextStyle"]
        });
        countPanel.Children.Add(new TextBlock
        {
            Text = $"{_currentRecommendation.RecommendedMonitorCount} monitors",
            Style = (Style)Resources["ValueTextStyle"]
        });
        RecommendationPanel.Children.Add(countPanel);

        // Optimal resolution
        var resolutionPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        resolutionPanel.Children.Add(new TextBlock
        {
            Text = "Optimal Resolution:",
            Style = (Style)Resources["SubHeaderTextStyle"]
        });
        resolutionPanel.Children.Add(new TextBlock
        {
            Text = _currentRecommendation.OptimalResolution.DisplayName,
            Style = (Style)Resources["ValueTextStyle"]
        });
        RecommendationPanel.Children.Add(resolutionPanel);

        // Performance expectation
        var perfText = new TextBlock
        {
            Text = _currentRecommendation.PerformanceExpectation,
            Style = (Style)Resources["SubHeaderTextStyle"],
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        RecommendationPanel.Children.Add(perfText);
    }

    /// <summary>
    /// Update monitor count display and status
    /// </summary>
    private void UpdateMonitorCountDisplay()
    {
        var count = (int)MonitorCountSlider.Value;
        MonitorCountText.Text = $"{count} Monitor{(count > 1 ? "s" : "")}";
        
        var isRdpSession = IsRunningViaRdp();
        var connectedCount = _connectedMonitors.Count;
        
        var status = count switch
        {
            var c when isRdpSession && c == 1 => " (RDP - Current Available)",
            var c when isRdpSession && c > 1 => " (RDP - Simulated for Testing)",
            var c when c == connectedCount => " (Currently Connected)",
            var c when c == _currentRecommendation.RecommendedMonitorCount => " (Hardware Recommended)",
            var c when c > _currentRecommendation.MaximumSupportedMonitors => " (⚠️ Exceeds GPU Capacity)",
            _ => ""
        };
        MonitorCountStatus.Text = status;
        
        // Update connection status panel
        UpdateConnectionStatusPanel();
    }
    
    /// <summary>
    /// Update the connection status panel based on current session type
    /// </summary>
    private void UpdateConnectionStatusPanel()
    {
        var isRdpSession = IsRunningViaRdp();
        var maxSupported = _currentRecommendation.MaximumSupportedMonitors;
        
        if (isRdpSession)
        {
            ConnectionStatusTitle.Text = "🌐 RDP Session Detected";
            ConnectionStatusDescription.Text = $"Connected via Remote Desktop. Hardware supports up to {maxSupported} monitors, but only 1 display is available in this RDP session. Use the slider to preview different monitor configurations for when you connect directly to the DRAGON system.";
            ConnectionStatusPanel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 45, 55, 72)); // Blue tint
            ConnectionStatusPanel.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 74, 85, 104));
        }
        else
        {
            ConnectionStatusTitle.Text = "🖥️ Direct Hardware Connection";
            ConnectionStatusDescription.Text = $"Connected directly to DRAGON system. {_connectedMonitors.Count} monitor(s) currently connected. Hardware supports up to {maxSupported} monitors total.";
            ConnectionStatusPanel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 26, 75, 58)); // Green tint
            ConnectionStatusPanel.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 212, 170));
        }
    }
    
    /// <summary>
    /// Detect if running via RDP session
    /// </summary>
    private bool IsRunningViaRdp()
    {
        try
        {
            var sessionName = Environment.GetEnvironmentVariable("SESSIONNAME");
            return !string.IsNullOrEmpty(sessionName) && 
                   (sessionName.StartsWith("RDP-", StringComparison.OrdinalIgnoreCase) ||
                    !sessionName.Equals("Console", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update performance indicator based on selected monitor count
    /// </summary>
    private void UpdatePerformanceIndicator()
    {
        var selectedCount = (int)MonitorCountSlider.Value;
        var recommended = _currentRecommendation.RecommendedMonitorCount;
        var maximum = _currentRecommendation.MaximumSupportedMonitors;
        var isRdpSession = IsRunningViaRdp();

        string title, description;
        Microsoft.UI.Colors backgroundColor, borderColor;

        if (isRdpSession && selectedCount > 1)
        {
            title = "🧪 Testing Mode - RDP Session";
            description = $"Simulating {selectedCount} monitor configuration for testing. When connected directly to DRAGON hardware, this setup would provide {GetPerformanceDescription(selectedCount, recommended, maximum)}";
            backgroundColor = Microsoft.UI.Colors.DarkBlue;
            borderColor = Microsoft.UI.Colors.CornflowerBlue;
        }
        else if (selectedCount <= recommended)
        {
            title = "✅ Optimal Performance Expected";
            description = isRdpSession ? 
                "Current RDP session using 1 monitor. This configuration would provide excellent trading performance on direct hardware connection." :
                "This configuration provides excellent trading performance with your current GPU setup.";
            backgroundColor = Microsoft.UI.Colors.DarkGreen;
            borderColor = Microsoft.UI.Colors.Green;
        }
        else if (selectedCount <= maximum)
        {
            title = "⚠️ Moderate Performance Impact";
            description = "Performance may be reduced during high market activity. Consider optimizing resolution settings.";
            backgroundColor = Microsoft.UI.Colors.DarkOrange;
            borderColor = Microsoft.UI.Colors.Orange;
        }
        else
        {
            title = "❌ Performance Issues Expected";
            description = "This configuration exceeds GPU capabilities and may cause significant lag during trading.";
            backgroundColor = Microsoft.UI.Colors.DarkRed;
            borderColor = Microsoft.UI.Colors.Red;
        }

        PerformanceTitle.Text = title;
        PerformanceDescription.Text = description;
        PerformanceIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor) { Opacity = 0.3 };
        PerformanceIndicator.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
    }
    
    /// <summary>
    /// Get performance description for a given monitor count
    /// </summary>
    private string GetPerformanceDescription(int count, int recommended, int maximum)
    {
        return count switch
        {
            var c when c <= recommended => "excellent performance",
            var c when c <= maximum => "moderate performance with some impact during high activity",
            _ => "significant performance issues"
        };
    }

    /// <summary>
    /// Update screen assignment grid based on selected monitor count
    /// </summary>
    private void UpdateScreenAssignmentGrid()
    {
        ScreenAssignmentGrid.Children.Clear();
        ScreenAssignmentGrid.RowDefinitions.Clear();

        var selectedCount = (int)MonitorCountSlider.Value;
        var tradingScreens = Enum.GetValues<TradingScreenType>().Take(selectedCount).ToList();

        for (int i = 0; i < selectedCount; i++)
        {
            ScreenAssignmentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var panel = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Monitor label
            var monitorLabel = new TextBlock
            {
                Text = $"Monitor {i + 1}:",
                Style = (Style)Resources["SubHeaderTextStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(monitorLabel, 0);
            panel.Children.Add(monitorLabel);
            
            // Screen type selection
            var screenComboBox = new ComboBox
            {
                ItemsSource = Enum.GetValues<TradingScreenType>().Select(t => new { Value = t, Display = GetScreenTypeDisplay(t) }),
                DisplayMemberPath = "Display",
                SelectedValuePath = "Value",
                SelectedValue = i < tradingScreens.Count ? tradingScreens[i] : TradingScreenType.PrimaryCharting,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(screenComboBox, 1);
            panel.Children.Add(screenComboBox);
            
            // Description
            var description = new TextBlock
            {
                Text = GetScreenTypeDescription(i < tradingScreens.Count ? tradingScreens[i] : TradingScreenType.PrimaryCharting),
                Style = (Style)Resources["SubHeaderTextStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(description, 2);
            panel.Children.Add(description);
            
            Grid.SetRow(panel, i);
            ScreenAssignmentGrid.Children.Add(panel);
        }
    }

    #region Event Handlers

    private async void RefreshGpu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Refreshing GPU detection");
            await DetectSystemCapabilitiesAsync();
            UpdateUI();
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to refresh GPU detection");
            ShowErrorMessage("Failed to refresh GPU information.");
        }
    }

    private void MonitorCountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateMonitorCountDisplay();
        UpdatePerformanceIndicator();
        UpdateScreenAssignmentGrid();
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Saving monitor configuration");
            
            var selectedCount = (int)MonitorCountSlider.Value;
            _currentConfiguration.Monitors = _connectedMonitors.Take(selectedCount).ToList();
            
            await _monitorDetectionService.SaveMonitorConfigurationAsync(_currentConfiguration);
            
            ShowSuccessMessage($"Configuration saved: {selectedCount} monitor setup");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to save monitor configuration");
            ShowErrorMessage("Failed to save configuration.");
        }
    }

    private async void TestConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Testing monitor configuration");
            
            var validation = await _monitorDetectionService.ValidateAndOptimizeConfigurationAsync(_currentConfiguration);
            
            var message = validation.IsSupported 
                ? $"✅ Configuration is supported!\n\nWarnings: {validation.Warnings.Count}\nSuggestions: {validation.OptimizationSuggestions.Count}"
                : $"❌ Configuration has issues:\n\n{string.Join("\n", validation.Errors)}";
            
            ShowInfoMessage(message);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to test monitor configuration");
            ShowErrorMessage("Failed to test configuration.");
        }
    }

    private async void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Resetting to default monitor configuration");
            
            MonitorCountSlider.Value = Math.Min(_connectedMonitors.Count, _currentRecommendation.RecommendedMonitorCount);
            
            _currentConfiguration = new MultiMonitorConfiguration
            {
                Monitors = _connectedMonitors,
                AutoAssignScreens = true,
                RememberWindowPositions = true
            };
            
            UpdateUI();
            ShowSuccessMessage("Configuration reset to recommended defaults");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to reset configuration");
            ShowErrorMessage("Failed to reset configuration.");
        }
    }

    #endregion

    #region Helper Methods

    private string GetScreenTypeDisplay(TradingScreenType screenType)
    {
        return screenType switch
        {
            TradingScreenType.PrimaryCharting => "📈 Primary Charts",
            TradingScreenType.OrderExecution => "⚡ Order Execution",
            TradingScreenType.PortfolioRisk => "📊 Portfolio & Risk",
            TradingScreenType.MarketScanner => "🔍 Market Scanner",
            _ => screenType.ToString()
        };
    }

    private string GetScreenTypeDescription(TradingScreenType screenType)
    {
        return screenType switch
        {
            TradingScreenType.PrimaryCharting => "Technical analysis, candlestick charts, indicators",
            TradingScreenType.OrderExecution => "Order entry, Level II market depth, trade execution",
            TradingScreenType.PortfolioRisk => "P&L tracking, position management, risk metrics",
            TradingScreenType.MarketScanner => "Stock screeners, news feeds, market alerts",
            _ => "General trading interface"
        };
    }

    private void ShowSuccessMessage(string message)
    {
        // In a real implementation, you would show a proper message dialog
        TradingLogOrchestrator.Instance.LogInfo("Success: {Message}", message);
    }

    private void ShowErrorMessage(string message)
    {
        // In a real implementation, you would show a proper error dialog
        TradingLogOrchestrator.Instance.LogError("Error: {Message}", message);
    }

    private void ShowInfoMessage(string message)
    {
        // In a real implementation, you would show a proper info dialog
        TradingLogOrchestrator.Instance.LogInfo("Info: {Message}", message);
    }

    #endregion
}