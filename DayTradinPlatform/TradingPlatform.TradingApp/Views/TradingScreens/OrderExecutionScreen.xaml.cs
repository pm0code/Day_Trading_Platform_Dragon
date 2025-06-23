using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using TradingPlatform.TradingApp.Models;
using TradingPlatform.TradingApp.Services;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Views.TradingScreens;

public sealed partial class OrderExecutionScreen : Window
{
    private readonly Core.Interfaces.ITradingLogger _logger;
    private readonly IMonitorService _monitorService;
    private readonly TradingScreenType _screenType = TradingScreenType.OrderExecution;
    
    public ObservableCollection<MarketDepthLevel> BidLevels { get; } = new();
    public ObservableCollection<MarketDepthLevel> AskLevels { get; } = new();
    
    public OrderExecutionScreen(Core.Interfaces.ITradingLogger logger, IMonitorService monitorService)
    {
        _logger = logger;
        _monitorService = monitorService;
        
        this.InitializeComponent();
        
        this.Title = "Order Execution - Trading Platform";
        this.ExtendsContentIntoTitleBar = true;
        
        this.SizeChanged += OnSizeChanged;
        this.Moved += OnMoved;
        this.Closed += OnClosed;
        
        TradingLogOrchestrator.Instance.LogInfo("Order execution screen initialized", _screenType.ToString());
        
        InitializeScreen();
    }
    
    private async void InitializeScreen()
    {
        try
        {
            await RestoreWindowPositionAsync();
            InitializeMarketDepth();
            
            TradingLogOrchestrator.Instance.LogInfo("Order execution screen initialization completed", _screenType.ToString());
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to initialize order execution screen", _screenType.ToString(), ex);
        }
    }
    
    private void InitializeMarketDepth()
    {
        // Sample market depth data
        for (int i = 0; i < 10; i++)
        {
            BidLevels.Add(new MarketDepthLevel 
            { 
                Price = $"{125.50 - (i * 0.01):F2}", 
                Size = $"{(1000 + i * 100)}", 
                DepthBarWidth = 100 - (i * 8) 
            });
            
            AskLevels.Add(new MarketDepthLevel 
            { 
                Price = $"{125.51 + (i * 0.01):F2}", 
                Size = $"{(1000 + i * 100)}", 
                DepthBarWidth = 100 - (i * 8) 
            });
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
        await SaveCurrentPositionAsync();
        TradingLogOrchestrator.Instance.LogInfo("Order execution screen closed", _screenType.ToString());
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
            TradingLogOrchestrator.Instance.LogError("Failed to restore window position for order execution screen", _screenType.ToString(), ex);
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
            TradingLogOrchestrator.Instance.LogError("Failed to save window position for order execution screen", _screenType.ToString(), ex);
        }
    }
}

public class MarketDepthLevel
{
    public string Price { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public double DepthBarWidth { get; set; }
}