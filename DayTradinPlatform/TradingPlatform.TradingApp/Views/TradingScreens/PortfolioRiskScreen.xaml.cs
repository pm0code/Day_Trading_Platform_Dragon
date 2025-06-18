using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using TradingPlatform.TradingApp.Models;
using TradingPlatform.TradingApp.Services;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Views.TradingScreens;

public sealed partial class PortfolioRiskScreen : Window
{
    private readonly ITradingLogger _logger;
    private readonly IMonitorService _monitorService;
    private readonly TradingScreenType _screenType = TradingScreenType.PortfolioRisk;
    
    public ObservableCollection<PositionData> Positions { get; } = new();
    private readonly DispatcherTimer _updateTimer;
    
    public PortfolioRiskScreen(ITradingLogger logger, IMonitorService monitorService)
    {
        _logger = logger;
        _monitorService = monitorService;
        
        this.InitializeComponent();
        
        this.Title = "Portfolio Risk - Trading Platform";
        this.ExtendsContentIntoTitleBar = true;
        
        this.SizeChanged += OnSizeChanged;
        this.Moved += OnMoved;
        this.Closed += OnClosed;
        
        // Initialize update timer
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        
        TradingLogOrchestrator.Instance.LogInfo("Portfolio risk screen initialized", _screenType.ToString());
        
        InitializeScreen();
    }
    
    private async void InitializeScreen()
    {
        try
        {
            await RestoreWindowPositionAsync();
            InitializePositions();
            UpdateRiskMetrics();
            
            _updateTimer.Start();
            
            TradingLogOrchestrator.Instance.LogInfo("Portfolio risk screen initialization completed", _screenType.ToString());
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to initialize portfolio risk screen", _screenType.ToString(), ex);
        }
    }
    
    private void InitializePositions()
    {
        // Sample position data for day trading
        Positions.Add(new PositionData
        {
            Symbol = "SPY",
            Shares = "200",
            AvgPrice = "$424.50",
            LastPrice = "$425.75",
            PnL = "+$250.00",
            PnLColor = "Green",
            PercentChange = "+0.29%",
            PercentChangeColor = "Green"
        });
        
        Positions.Add(new PositionData
        {
            Symbol = "QQQ",
            Shares = "100",
            AvgPrice = "$367.25",
            LastPrice = "$366.10",
            PnL = "-$115.00",
            PnLColor = "Red",
            PercentChange = "-0.31%",
            PercentChangeColor = "Red"
        });
        
        Positions.Add(new PositionData
        {
            Symbol = "AAPL",
            Shares = "50",
            AvgPrice = "$189.45",
            LastPrice = "$190.80",
            PnL = "+$67.50",
            PnLColor = "Green",
            PercentChange = "+0.71%",
            PercentChangeColor = "Green"
        });
        
        PositionsList.ItemsSource = Positions;
    }
    
    private void UpdateRiskMetrics()
    {
        // Update P&L metrics
        var totalPnL = CalculateTotalPnL();
        var dailyPnL = CalculateDailyPnL();
        
        TotalPnL.Text = totalPnL >= 0 ? $"+${totalPnL:F2}" : $"-${Math.Abs(totalPnL):F2}";
        TotalPnL.Foreground = totalPnL >= 0 ? 
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] :
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        
        DailyPnL.Text = dailyPnL >= 0 ? $"+${dailyPnL:F2}" : $"-${Math.Abs(dailyPnL):F2}";
        DailyPnL.Foreground = dailyPnL >= 0 ? 
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] :
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        
        // Update risk progress bars
        UpdateRiskProgressBars();
        
        TradingLogOrchestrator.Instance.LogInfo("Risk metrics updated", _screenType.ToString(), new Dictionary<string, object>
        {
            ["TotalPnL"] = totalPnL,
            ["DailyPnL"] = dailyPnL,
            ["PositionCount"] = Positions.Count
        });
    }
    
    private void UpdateRiskProgressBars()
    {
        // Daily loss limit: $500 of $2000 = 25%
        DailyLossProgress.Value = 25;
        
        // Position size risk: 15% of account
        PositionSizeProgress.Value = 15;
        
        // Sector concentration: 35%
        SectorConcentrationProgress.Value = 35;
        
        // Update risk colors based on thresholds
        UpdateRiskColors();
    }
    
    private void UpdateRiskColors()
    {
        // Daily loss limit coloring
        if (DailyLossProgress.Value > 80)
            DailyLossProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        else if (DailyLossProgress.Value > 60)
            DailyLossProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
        else
            DailyLossProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        
        // Position size risk coloring
        if (PositionSizeProgress.Value > 50)
            PositionSizeProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        else if (PositionSizeProgress.Value > 30)
            PositionSizeProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
        else
            PositionSizeProgress.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
    }
    
    private decimal CalculateTotalPnL()
    {
        // Calculate from position data
        decimal total = 0;
        foreach (var position in Positions)
        {
            if (decimal.TryParse(position.PnL.Replace("+", "").Replace("$", ""), out var pnl))
            {
                total += pnl;
            }
        }
        return total;
    }
    
    private decimal CalculateDailyPnL()
    {
        // For now, return same as total - in real implementation this would be day-specific
        return CalculateTotalPnL();
    }
    
    private void UpdateTimer_Tick(object sender, object e)
    {
        // Update timestamp
        LastRiskUpdate.Text = $"Risk Updated: {DateTime.Now:HH:mm:ss}";
        
        // Periodically update risk metrics (every 30 seconds)
        if (DateTime.Now.Second % 30 == 0)
        {
            UpdateRiskMetrics();
        }
    }
    
    private async void OnSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        await SaveCurrentPositionAsync();
    }
    
    private async void OnMoved(object sender, object e)
    {
        await SaveCurrentPositionAsync();
    }
    
    private async void OnClosed(object sender, WindowEventArgs e)
    {
        _updateTimer?.Stop();
        await SaveCurrentPositionAsync();
        TradingLogOrchestrator.Instance.LogInfo("Portfolio risk screen closed", _screenType.ToString());
    }
    
    private async Task RestoreWindowPositionAsync()
    {
        try
        {
            var savedPosition = await _monitorService.GetSavedWindowPositionAsync(_screenType);
            if (savedPosition != null)
            {
                var assignedMonitor = await _monitorService.GetAssignedMonitorAsync(_screenType);
                if (assignedMonitor != null)
                {
                    var appWindow = this.AppWindow;
                    if (appWindow != null)
                    {
                        var position = new Windows.Graphics.PointInt32(
                            assignedMonitor.X + savedPosition.X,
                            assignedMonitor.Y + savedPosition.Y
                        );
                        
                        var size = new Windows.Graphics.SizeInt32(
                            savedPosition.Width,
                            savedPosition.Height
                        );
                        
                        appWindow.Move(position);
                        appWindow.Resize(size);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to restore window position for portfolio risk screen", _screenType.ToString(), ex);
        }
    }
    
    private async Task SaveCurrentPositionAsync()
    {
        try
        {
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                var assignedMonitor = await _monitorService.GetAssignedMonitorAsync(_screenType);
                if (assignedMonitor != null)
                {
                    var position = appWindow.Position;
                    var size = appWindow.Size;
                    
                    var relativeX = position.X - assignedMonitor.X;
                    var relativeY = position.Y - assignedMonitor.Y;
                    
                    var positionInfo = new WindowPositionInfo
                    {
                        ScreenType = _screenType,
                        MonitorId = assignedMonitor.MonitorId,
                        X = relativeX,
                        Y = relativeY,
                        Width = size.Width,
                        Height = size.Height,
                        WindowState = WindowState.Normal,
                        LastSaved = DateTime.UtcNow
                    };
                    
                    await _monitorService.SaveWindowPositionAsync(positionInfo);
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to save window position for portfolio risk screen", _screenType.ToString(), ex);
        }
    }
}

public class PositionData
{
    public string Symbol { get; set; } = string.Empty;
    public string Shares { get; set; } = string.Empty;
    public string AvgPrice { get; set; } = string.Empty;
    public string LastPrice { get; set; } = string.Empty;
    public string PnL { get; set; } = string.Empty;
    public string PnLColor { get; set; } = "Black";
    public string PercentChange { get; set; } = string.Empty;
    public string PercentChangeColor { get; set; } = "Black";
}