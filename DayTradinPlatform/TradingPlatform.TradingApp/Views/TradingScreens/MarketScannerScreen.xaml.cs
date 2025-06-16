using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using TradingPlatform.TradingApp.Models;
using TradingPlatform.TradingApp.Services;
using TradingPlatform.Logging.Interfaces;

namespace TradingPlatform.TradingApp.Views.TradingScreens;

public sealed partial class MarketScannerScreen : Window
{
    private readonly ITradingLogger _logger;
    private readonly IMonitorService _monitorService;
    private readonly TradingScreenType _screenType = TradingScreenType.MarketScanner;
    
    public ObservableCollection<ScannerResult> ScanResults { get; } = new();
    public ObservableCollection<EconomicEvent> EconomicEvents { get; } = new();
    private readonly DispatcherTimer _scanTimer;
    private readonly DispatcherTimer _newsTimer;
    
    public MarketScannerScreen(ITradingLogger logger, IMonitorService monitorService)
    {
        _logger = logger;
        _monitorService = monitorService;
        
        this.InitializeComponent();
        
        this.Title = "Market Scanner - Trading Platform";
        this.ExtendsContentIntoTitleBar = true;
        
        this.SizeChanged += OnSizeChanged;
        this.Moved += OnMoved;
        this.Closed += OnClosed;
        
        // Initialize timers
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Scan every 30 seconds
        };
        _scanTimer.Tick += ScanTimer_Tick;
        
        _newsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(2) // Update news every 2 minutes
        };
        _newsTimer.Tick += NewsTimer_Tick;
        
        _logger.LogInformation("Market scanner screen initialized", _screenType.ToString());
        
        InitializeScreen();
    }
    
    private async void InitializeScreen()
    {
        try
        {
            await RestoreWindowPositionAsync();
            InitializeScannerResults();
            InitializeEconomicEvents();
            InitializeNewsItems();
            
            // Start timers if auto-scan is enabled
            if (AutoScanToggle.IsChecked == true)
            {
                _scanTimer.Start();
            }
            _newsTimer.Start();
            
            _logger.LogInformation("Market scanner screen initialization completed", _screenType.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize market scanner screen", _screenType.ToString(), ex);
        }
    }
    
    private void InitializeScannerResults()
    {
        // Sample volume spike alerts for day trading
        ScanResults.Add(new ScannerResult
        {
            Symbol = "TSLA",
            Price = "$245.67",
            Change = "+3.25%",
            ChangeColor = "Green",
            Volume = "2.5M",
            AvgVolume = "850K",
            VolumeRatio = "2.94x",
            VolumeRatioColor = "Red"
        });
        
        ScanResults.Add(new ScannerResult
        {
            Symbol = "NVDA",
            Price = "$875.23",
            Change = "+2.15%",
            ChangeColor = "Green",
            Volume = "1.8M",
            AvgVolume = "750K",
            VolumeRatio = "2.40x",
            VolumeRatioColor = "Orange"
        });
        
        ScanResults.Add(new ScannerResult
        {
            Symbol = "AMD",
            Price = "$142.89",
            Change = "-1.85%",
            ChangeColor = "Red",
            Volume = "3.2M",
            AvgVolume = "1.2M",
            VolumeRatio = "2.67x",
            VolumeRatioColor = "Red"
        });
        
        ScanResults.Add(new ScannerResult
        {
            Symbol = "AAPL",
            Price = "$190.45",
            Change = "+0.95%",
            ChangeColor = "Green",
            Volume = "1.5M",
            AvgVolume = "650K",
            VolumeRatio = "2.31x",
            VolumeRatioColor = "Orange"
        });
        
        ScannerResults.ItemsSource = ScanResults;
        AlertCount.Text = $"Alerts: {ScanResults.Count}";
    }
    
    private void InitializeEconomicEvents()
    {
        // Sample economic events for today
        EconomicEvents.Add(new EconomicEvent
        {
            Time = "8:30 AM",
            Event = "Initial Jobless Claims",
            Impact = "HIGH",
            ImpactColor = "Red",
            Status = "Pending"
        });
        
        EconomicEvents.Add(new EconomicEvent
        {
            Time = "10:00 AM",
            Event = "Consumer Confidence",
            Impact = "MEDIUM",
            ImpactColor = "Orange",
            Status = "Pending"
        });
        
        EconomicEvents.Add(new EconomicEvent
        {
            Time = "2:00 PM",
            Event = "FOMC Meeting Minutes",
            Impact = "HIGH",
            ImpactColor = "Red",
            Status = "Pending"
        });
        
        EconomicEvents.ItemsSource = EconomicEvents;
    }
    
    private void InitializeNewsItems()
    {
        // Create sample news items
        var newsItems = new[]
        {
            new { Title = "Fed Signals Potential Rate Cut", Time = "2 min ago", Impact = "HIGH" },
            new { Title = "Tesla Reports Record Q4 Deliveries", Time = "15 min ago", Impact = "MEDIUM" },
            new { Title = "Tech Sector Leads Market Rally", Time = "32 min ago", Impact = "MEDIUM" },
            new { Title = "Oil Prices Surge on Supply Concerns", Time = "45 min ago", Impact = "HIGH" },
            new { Title = "GDP Growth Exceeds Expectations", Time = "1 hr ago", Impact = "HIGH" }
        };
        
        foreach (var news in newsItems)
        {
            var newsCard = CreateNewsCard(news.Title, news.Time, news.Impact);
            NewsPanel.Children.Add(newsCard);
        }
    }
    
    private Border CreateNewsCard(string title, string time, string impact)
    {
        var impactColor = impact == "HIGH" ? "Red" : "Orange";
        
        var card = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };
        
        var stackPanel = new StackPanel();
        
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        
        var detailsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        
        var timeBlock = new TextBlock
        {
            Text = time,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        };
        
        var impactBlock = new TextBlock
        {
            Text = impact,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[impactColor == "Red" ? "SystemFillColorCriticalBrush" : "SystemFillColorCautionBrush"]
        };
        
        detailsPanel.Children.Add(timeBlock);
        detailsPanel.Children.Add(impactBlock);
        
        stackPanel.Children.Add(titleBlock);
        stackPanel.Children.Add(detailsPanel);
        
        card.Child = stackPanel;
        
        return card;
    }
    
    private void RefreshScanButton_Click(object sender, RoutedEventArgs e)
    {
        PerformScan();
    }
    
    private void PerformScan()
    {
        LastScanTime.Text = $"Last Scan: {DateTime.Now:HH:mm:ss}";
        
        // TODO: Implement actual market scanning logic
        // For now, just update the timestamp
        
        _logger.LogInformation("Market scan performed", _screenType.ToString(), new Dictionary<string, object>
        {
            ["ScanType"] = ScanTypeSelector.SelectedItem?.ToString() ?? "Unknown",
            ["MinVolume"] = MinVolumeFilter.Value,
            ["ResultCount"] = ScanResults.Count
        });
    }
    
    private void ScanTimer_Tick(object sender, object e)
    {
        if (AutoScanToggle.IsChecked == true)
        {
            PerformScan();
        }
    }
    
    private void NewsTimer_Tick(object sender, object e)
    {
        // TODO: Implement news feed updates
        _logger.LogInformation("News feed updated", _screenType.ToString());
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
        _scanTimer?.Stop();
        _newsTimer?.Stop();
        await SaveCurrentPositionAsync();
        _logger.LogInformation("Market scanner screen closed", _screenType.ToString());
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
            _logger.LogError("Failed to restore window position for market scanner screen", 
                _screenType.ToString(), ex);
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
            _logger.LogError("Failed to save window position for market scanner screen", 
                _screenType.ToString(), ex);
        }
    }
}

public class ScannerResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string ChangeColor { get; set; } = "Black";
    public string Volume { get; set; } = string.Empty;
    public string AvgVolume { get; set; } = string.Empty;
    public string VolumeRatio { get; set; } = string.Empty;
    public string VolumeRatioColor { get; set; } = "Black";
}

public class EconomicEvent
{
    public string Time { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string ImpactColor { get; set; } = "Black";
    public string Status { get; set; } = string.Empty;
}