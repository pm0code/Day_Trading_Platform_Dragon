// TradingPlatform.TradingApp.Views.TradingScreens.LogAnalyzerScreen.xaml.cs
// AI-Powered Log Analyzer UI with real-time streaming and performance monitoring
// Integrates with Enhanced TradingLogOrchestrator for comprehensive log analysis

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.TradingApp.Services;
using TradingPlatform.TradingApp.Models;

namespace TradingPlatform.TradingApp.Views.TradingScreens;

/// <summary>
/// AI-Powered Log Analyzer Screen with real-time monitoring and intelligent analysis
/// Provides comprehensive log visualization, performance tracking, and anomaly detection
/// </summary>
public sealed partial class LogAnalyzerScreen : Window
{
    #region Fields and Properties
    
    private readonly ObservableCollection<LogDisplayItem> _logItems = new();
    private readonly ObservableCollection<AlertDisplayItem> _alertItems = new();
    private readonly ITradingLogger _logger;
    private readonly ILogAnalyticsService _analyticsService;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private DispatcherTimer? _metricsUpdateTimer;
    private DispatcherTimer? _chartUpdateTimer;
    
    // Performance tracking
    private int _totalLogCount = 0;
    private DateTime _lastUpdateTime = DateTime.Now;
    private readonly Queue<double> _latencyHistory = new();
    private readonly Queue<double> _throughputHistory = new();
    
    // Configuration
    private LoggingConfiguration _currentConfig;
    
    #endregion
    
    #region Constructor and Initialization
    
    public LogAnalyzerScreen(ILogAnalyticsService analyticsService)
    {
        this.InitializeComponent();
        _logger = EnhancedTradingLogOrchestrator.Instance;
        _analyticsService = analyticsService;
        _currentConfig = EnhancedTradingLogOrchestrator.Instance.GetConfiguration();
        
        InitializeUI();
        InitializeAnalyticsService();
        InitializeWebSocketConnection();
        StartPerformanceMonitoring();
        
        _logger.LogInfo("Log Analyzer Screen initialized", new { WindowSize = "1920x1080", Features = "AI Analysis, Real-time Streaming" });
    }
    
    private void InitializeUI()
    {
        // Bind collections to UI controls
        LogStreamList.ItemsSource = _logItems;
        AlertsList.ItemsSource = _alertItems;
        
        // Initialize configuration controls
        UpdateConfigurationUI();
        
        // Add sample alerts for demonstration
        AddSampleAlerts();
        
        // Initialize pattern analysis
        InitializePatternAnalysis();
    }
    
    private void InitializeAnalyticsService()
    {
        // Subscribe to analytics service events
        _analyticsService.AlertTriggered += OnAlertTriggered;
        _analyticsService.PatternDetected += OnPatternDetected;
        _analyticsService.PerformanceAnalysis += OnPerformanceAnalysis;
    }
    
    private void UpdateConfigurationUI()
    {
        TradingThresholdBox.Value = _currentConfig.Thresholds.TradingOperationMicroseconds;
        AiSensitivityBox.Value = _currentConfig.AI.AnomalySensitivity;
        EnableMethodLoggingBox.IsChecked = _currentConfig.EnableMethodLifecycleLogging;
        EnableParameterLoggingBox.IsChecked = _currentConfig.EnableParameterLogging;
        
        // Update log scope selector
        LogScopeSelector.SelectedIndex = _currentConfig.Scope switch
        {
            LoggingScope.Critical => 0,
            LoggingScope.ProjectSpecific => 1,
            LoggingScope.All => 2,
            _ => 0
        };
    }
    
    #endregion
    
    #region WebSocket Connection for Real-time Streaming
    
    private async void InitializeWebSocketConnection()
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();
            
            var uri = new Uri($"ws://localhost:{_currentConfig.Streaming.StreamingPort}/logs/");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            
            ConnectionStatusText.Text = "🟢 Connected to Log Stream";
            
            // Start receiving messages
            _ = Task.Run(ReceiveLogMessages);
            
            _logger.LogInfo("WebSocket connection established", new { Port = _currentConfig.Streaming.StreamingPort });
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "🔴 Connection Failed";
            TradingLogOrchestrator.Instance.LogError("Failed to connect to log stream", ex);
        }
    }
    
    private async Task ReceiveLogMessages()
    {
        var buffer = new byte[4096];
        
        try
        {
            while (_webSocket?.State == WebSocketState.Open && 
                   !_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                var result = await _webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var logEntries = JsonSerializer.Deserialize<LogEntry[]>(json);
                    
                    if (logEntries != null)
                    {
                        await DispatcherQueue.TryEnqueue(() =>
                        {
                            ProcessIncomingLogEntries(logEntries);
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DispatcherQueue.TryEnqueue(() =>
            {
                ConnectionStatusText.Text = "🔴 Connection Lost";
                TradingLogOrchestrator.Instance.LogError("WebSocket connection error", ex);
            });
        }
    }
    
    private async void ProcessIncomingLogEntries(LogEntry[] entries)
    {
        foreach (var entry in entries)
        {
            // Apply current filters
            if (!ShouldDisplayLogEntry(entry)) continue;
            
            // Analyze log entry with AI service
            try
            {
                var analysisResult = await _analyticsService.AnalyzeLogEntry(entry);
                var displayItem = CreateLogDisplayItem(analysisResult.Entry);
                _logItems.Insert(0, displayItem); // Add to top of list
                
                // Process intelligent alerts if any
                if (analysisResult.Severity >= LogSeverity.High)
                {
                    ProcessIntelligentAlert(analysisResult);
                }
            }
            catch (Exception ex)
            {
                // Fallback to basic processing if AI analysis fails
                var displayItem = CreateLogDisplayItem(entry);
                _logItems.Insert(0, displayItem);
                ProcessPotentialAlert(entry);
                TradingLogOrchestrator.Instance.LogError("Failed to analyze log entry with AI service", ex);
            }
            
            // Limit display to 1000 items for performance
            while (_logItems.Count > 1000)
            {
                _logItems.RemoveAt(_logItems.Count - 1);
            }
            
            // Update metrics
            _totalLogCount++;
            UpdatePerformanceMetrics(entry);
        }
        
        // Update UI counters
        LogCountText.Text = $"Logs: {_totalLogCount:N0}";
        
        // Update processing rate
        var elapsed = (DateTime.Now - _lastUpdateTime).TotalSeconds;
        if (elapsed > 1.0)
        {
            var rate = entries.Length / elapsed;
            ProcessingRateText.Text = $"Processing: {rate:F0} logs/sec";
            _lastUpdateTime = DateTime.Now;
        }
    }
    
    #endregion
    
    #region Log Display and Filtering
    
    private bool ShouldDisplayLogEntry(LogEntry entry)
    {
        // Apply log level filter
        var selectedLevel = LogLevelFilter.SelectedIndex switch
        {
            1 => LogLevel.Critical,
            2 => LogLevel.Error,
            3 => LogLevel.Warning,
            4 => LogLevel.Info,
            5 => LogLevel.Debug,
            _ => LogLevel.Debug // Show all
        };
        
        if (entry.Level < selectedLevel) return false;
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var searchTerm = SearchBox.Text.ToLowerInvariant();
            if (!entry.Message.ToLowerInvariant().Contains(searchTerm) &&
                !entry.Source.MethodName?.ToLowerInvariant().Contains(searchTerm) == true &&
                !entry.Source.Project?.ToLowerInvariant().Contains(searchTerm) == true)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private LogDisplayItem CreateLogDisplayItem(LogEntry entry)
    {
        return new LogDisplayItem
        {
            Timestamp = entry.Timestamp.ToString("HH:mm:ss.fff"),
            Level = entry.Level.ToString().ToUpper(),
            LevelColor = GetLevelColor(entry.Level),
            Source = $"{entry.Source.Project?.Split('.').LastOrDefault()}.{entry.Source.MethodName}",
            Message = entry.Message,
            AnomalyScore = entry.AnomalyScore?.ToString("F2") ?? "--",
            AnomalyColor = GetAnomalyColor(entry.AnomalyScore),
            FullEntry = entry
        };
    }
    
    private SolidColorBrush GetLevelColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),     // Red
            LogLevel.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 69, 0)),       // Orange Red
            LogLevel.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),    // Orange
            LogLevel.Info => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 123, 255)),       // Blue
            LogLevel.Debug => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),    // Gray
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
        };
    }
    
    private SolidColorBrush GetAnomalyColor(double? score)
    {
        if (!score.HasValue) return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
        
        return score.Value switch
        {
            >= 0.8 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),      // High - Red
            >= 0.6 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),    // Medium - Orange
            >= 0.3 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 0)),    // Low - Yellow
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0))            // Normal - Green
        };
    }
    
    #endregion
    
    #region Performance Monitoring and Metrics
    
    private void StartPerformanceMonitoring()
    {
        _metricsUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _metricsUpdateTimer.Tick += UpdatePerformanceMetricsDisplay;
        _metricsUpdateTimer.Start();
        
        _chartUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _chartUpdateTimer.Tick += UpdatePerformanceChart;
        _chartUpdateTimer.Start();
    }
    
    private void UpdatePerformanceMetrics(LogEntry entry)
    {
        // Extract performance data from log entry
        if (entry.Performance != null)
        {
            var durationMs = entry.Performance.DurationMilliseconds ?? 0;
            
            // Add to history for trending
            _latencyHistory.Enqueue(durationMs);
            while (_latencyHistory.Count > 60) // Keep 60 data points
            {
                _latencyHistory.Dequeue();
            }
        }
        
        // Update throughput history
        _throughputHistory.Enqueue(_totalLogCount);
        while (_throughputHistory.Count > 60)
        {
            _throughputHistory.Dequeue();
        }
    }
    
    private void UpdatePerformanceMetricsDisplay(object? sender, object e)
    {
        // Update trading latency (simulated with real data)
        var avgLatency = _latencyHistory.Count > 0 ? _latencyHistory.Average() : 0;
        TradingLatencyText.Text = $"{avgLatency * 1000:F0}μs";
        TradingLatencyText.Foreground = avgLatency < 0.1 ? 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)) : 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
        
        // Update order execution (simulated)
        var orderLatency = avgLatency * 1.1; // Slightly higher than trading latency
        OrderExecutionText.Text = $"{orderLatency * 1000:F0}μs";
        OrderExecutionText.Foreground = orderLatency < 0.075 ? 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)) : 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
        
        // Update system health (simulated)
        var random = new Random();
        var healthScore = 95 + random.NextDouble() * 5;
        SystemHealthText.Text = $"{healthScore:F0}%";
        
        // Update AI anomaly score (average of recent entries)
        var recentAnomalyScores = _logItems.Take(10)
            .Where(item => item.FullEntry.AnomalyScore.HasValue)
            .Select(item => item.FullEntry.AnomalyScore!.Value);
        
        var avgAnomalyScore = recentAnomalyScores.Any() ? recentAnomalyScores.Average() : 0.1;
        AnomalyScoreText.Text = $"{avgAnomalyScore:F2}";
        AnomalyScoreText.Foreground = avgAnomalyScore < 0.3 ? 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 0, 128)) : 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
    }
    
    private void UpdatePerformanceChart(object? sender, object e)
    {
        // In a real implementation, this would draw performance charts
        // For now, we'll update the placeholder text
        var chartText = $"📈 Real-time Performance Chart - {DateTime.Now:HH:mm:ss}";
        
        // Clear existing chart content
        PerformanceChart.Children.Clear();
        
        // Add updated chart placeholder
        var textBlock = new TextBlock
        {
            Text = chartText,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
        };
        Canvas.SetLeft(textBlock, 50);
        Canvas.SetTop(textBlock, 50);
        PerformanceChart.Children.Add(textBlock);
    }
    
    #endregion
    
    #region Alert Processing and Management
    
    private void ProcessPotentialAlert(LogEntry entry)
    {
        // Check if entry should generate an alert
        if (entry.AlertPriority == null && entry.Level < LogLevel.Warning) return;
        
        var alert = new AlertDisplayItem
        {
            Title = GenerateAlertTitle(entry),
            Description = entry.Message,
            Severity = entry.AlertPriority?.ToString() ?? entry.Level.ToString(),
            SeverityColor = GetSeverityColor(entry.AlertPriority ?? (AlertPriority)((int)entry.Level)),
            Timestamp = entry.Timestamp.ToString("HH:mm:ss")
        };
        
        _alertItems.Insert(0, alert);
        
        // Limit alerts display
        while (_alertItems.Count > 50)
        {
            _alertItems.RemoveAt(_alertItems.Count - 1);
        }
        
        // Update alert badge
        AlertBadge.Text = _alertItems.Count(a => 
            a.Severity == "High" || a.Severity == "Critical").ToString();
    }
    
    private string GenerateAlertTitle(LogEntry entry)
    {
        if (entry.Performance?.PerformanceDeviation > 2.0)
            return "Performance Degradation Detected";
        
        if (entry.Trading != null)
            return "Trading Operation Alert";
        
        if (entry.Level >= LogLevel.Error)
            return "System Error Alert";
        
        return "Anomaly Detected";
    }
    
    private SolidColorBrush GetSeverityColor(AlertPriority priority)
    {
        return priority switch
        {
            AlertPriority.Critical or AlertPriority.Emergency => 
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),      // Red
            AlertPriority.High => 
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),    // Orange
            AlertPriority.Medium => 
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 0)),    // Yellow
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 123, 255))    // Blue
        };
    }
    
    private void ProcessIntelligentAlert(LogAnalysisResult analysisResult)
    {
        var alert = new AlertDisplayItem
        {
            Title = $"AI Alert: {analysisResult.Severity}",
            Description = string.Join(", ", analysisResult.Recommendations.Take(2)),
            Severity = analysisResult.Severity.ToString(),
            SeverityColor = GetSeverityColorFromAnalysis(analysisResult.Severity),
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        
        _alertItems.Insert(0, alert);
        
        // Update alert badge
        UpdateAlertBadge();
    }

    private SolidColorBrush GetSeverityColorFromAnalysis(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),
            LogSeverity.High => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),
            LogSeverity.Medium => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 0)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 123, 255))
        };
    }
    
    private void UpdateAlertBadge()
    {
        var highSeverityCount = _alertItems.Count(a => 
            a.Severity == "High" || a.Severity == "Critical");
        AlertBadge.Text = highSeverityCount.ToString();
    }

    #region Analytics Event Handlers
    
    private async void OnAlertTriggered(object? sender, AlertTriggeredEventArgs e)
    {
        await DispatcherQueue.TryEnqueue(() =>
        {
            var alertItem = new AlertDisplayItem
            {
                Title = e.Alert.Title,
                Description = e.Alert.Description,
                Severity = e.Alert.Severity.ToString(),
                SeverityColor = GetSeverityColorFromAnalysis(e.Alert.Severity),
                Timestamp = e.Alert.Timestamp.ToString("HH:mm:ss")
            };
            
            _alertItems.Insert(0, alertItem);
            UpdateAlertBadge();
        });
    }
    
    private async void OnPatternDetected(object? sender, PatternDetectedEventArgs e)
    {
        await DispatcherQueue.TryEnqueue(() =>
        {
            // Update pattern analysis display
            var patternText = $"🔍 Pattern Detected: {e.Pattern.Type} (Confidence: {e.Pattern.Confidence:P0})";
            
            // Find and update existing pattern card or add new one
            UpdatePatternDisplay(e.Pattern);
        });
    }
    
    private async void OnPerformanceAnalysis(object? sender, PerformanceAnalysisEventArgs e)
    {
        await DispatcherQueue.TryEnqueue(() =>
        {
            // Update performance metrics display with AI insights
            UpdatePerformanceInsights(e.Insights);
        });
    }
    
    private void UpdatePatternDisplay(LogPattern pattern)
    {
        // Create or update pattern card in the UI
        var patternCard = CreatePatternCard(
            $"🔍 {pattern.Type}",
            pattern.Description,
            $"Confidence: {pattern.Confidence:P0} | Detected: {pattern.DetectedAt:HH:mm:ss}"
        );
        
        // Add to pattern analysis panel (replace existing similar patterns)
        if (PatternAnalysisPanel.Children.Count >= 4)
        {
            PatternAnalysisPanel.Children.RemoveAt(PatternAnalysisPanel.Children.Count - 1);
        }
        PatternAnalysisPanel.Children.Insert(0, patternCard);
    }
    
    private void UpdatePerformanceInsights(PerformanceInsights insights)
    {
        // Update performance metrics with AI insights
        var insightText = $"AI Insights: {insights.TotalOperations} ops, {insights.ErrorRate:P2} error rate";
        
        // Update any performance display elements with the insights
        if (insights.BottleneckOperations.Any())
        {
            var bottleneck = insights.BottleneckOperations.First();
            var bottleneckCard = CreatePatternCard(
                "⚠️ Performance Bottleneck",
                $"Operation: {bottleneck.OperationName}",
                $"Avg: {bottleneck.AverageExecutionTime:F2}μs | Impact: {bottleneck.TotalTimePercent:F1}%"
            );
            
            // Add bottleneck info to pattern panel
            PatternAnalysisPanel.Children.Insert(0, bottleneckCard);
        }
    }
    
    #endregion

    private void AddSampleAlerts()
    {
        _alertItems.Add(new AlertDisplayItem
        {
            Title = "Order Execution Latency Spike",
            Description = "Order execution exceeded 150μs threshold for AAPL orders",
            Severity = "High",
            SeverityColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),
            Timestamp = DateTime.Now.AddMinutes(-2).ToString("HH:mm:ss")
        });
        
        _alertItems.Add(new AlertDisplayItem
        {
            Title = "Unusual Trading Volume Pattern",
            Description = "AI detected anomalous volume patterns in market data processing",
            Severity = "Medium",
            SeverityColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 0)),
            Timestamp = DateTime.Now.AddMinutes(-5).ToString("HH:mm:ss")
        });
        
        _alertItems.Add(new AlertDisplayItem
        {
            Title = "Risk Calculation Performance",
            Description = "Risk engine processing time exceeded 200μs for portfolio updates",
            Severity = "Low",
            SeverityColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 123, 255)),
            Timestamp = DateTime.Now.AddMinutes(-8).ToString("HH:mm:ss")
        });
        
        AlertBadge.Text = "1"; // High severity count
    }
    
    #endregion
    
    #region Pattern Analysis
    
    private void InitializePatternAnalysis()
    {
        PatternAnalysisPanel.Children.Add(CreatePatternCard(
            "🔄 Method Call Patterns",
            "Analyzing execution frequency and performance trends across trading methods",
            "Normal patterns detected"
        ));
        
        PatternAnalysisPanel.Children.Add(CreatePatternCard(
            "⚡ Performance Trends",
            "Monitoring latency trends and threshold violations",
            "2% performance improvement vs yesterday"
        ));
        
        PatternAnalysisPanel.Children.Add(CreatePatternCard(
            "🎯 Trading Efficiency",
            "Analyzing order execution rates and slippage patterns",
            "Fill rate: 99.8% | Avg slippage: 0.02%"
        ));
        
        PatternAnalysisPanel.Children.Add(CreatePatternCard(
            "🧠 AI Insights",
            "Machine learning analysis of log patterns and anomalies",
            "Model confidence: 94% | Next update: 2h"
        ));
    }
    
    private Border CreatePatternCard(string title, string description, string status)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 128, 128, 128)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4)
        };
        
        var stackPanel = new StackPanel();
        
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
        
        var descBlock = new TextBlock
        {
            Text = description,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        
        var statusBlock = new TextBlock
        {
            Text = status,
            FontSize = 10,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0))
        };
        
        stackPanel.Children.Add(titleBlock);
        stackPanel.Children.Add(descBlock);
        stackPanel.Children.Add(statusBlock);
        
        border.Child = stackPanel;
        return border;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void LogScopeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogScopeSelector.SelectedIndex >= 0)
        {
            var newScope = LogScopeSelector.SelectedIndex switch
            {
                0 => LoggingScope.Critical,
                1 => LoggingScope.ProjectSpecific,
                2 => LoggingScope.All,
                _ => LoggingScope.Critical
            };
            
            _logger.LogInfo("Log scope changed", new { NewScope = newScope, PreviousScope = _currentConfig.Scope });
        }
    }
    
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Trigger re-filtering of displayed logs
        // In a real implementation, this would efficiently filter the display
    }
    
    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        _logItems.Clear();
        _totalLogCount = 0;
        LogCountText.Text = "Logs: 0";
        _logger.LogInfo("Log display cleared by user");
    }
    
    private void RealTimeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            InitializeWebSocketConnection();
        }
        _logger.LogInfo("Real-time streaming enabled");
    }
    
    private void RealTimeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disabled real-time", CancellationToken.None);
        ConnectionStatusText.Text = "🔴 Real-time Disabled";
        _logger.LogInfo("Real-time streaming disabled");
    }
    
    private async void ExportLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON Files", new[] { ".json" });
            picker.FileTypeChoices.Add("CSV Files", new[] { ".csv" });
            picker.SuggestedFileName = $"TradingLogs_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // Export logic would be implemented here
                _logger.LogInfo("Log export initiated", new { FileName = file.Name, Format = file.FileType });
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to export logs", ex);
        }
    }
    
    private void LogStreamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogStreamList.SelectedItem is LogDisplayItem selectedLog)
        {
            // Show detailed log information - could open a popup or details panel
            _logger.LogInfo("Log entry selected for detailed view", 
                new { LogId = selectedLog.FullEntry.Id, Level = selectedLog.Level });
        }
    }
    
    private void ApplyConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Update configuration based on UI controls
            _currentConfig.Thresholds.TradingOperationMicroseconds = (long)TradingThresholdBox.Value;
            _currentConfig.AI.AnomalySensitivity = AiSensitivityBox.Value;
            _currentConfig.EnableMethodLifecycleLogging = EnableMethodLoggingBox.IsChecked ?? false;
            _currentConfig.EnableParameterLogging = EnableParameterLoggingBox.IsChecked ?? false;
            
            // Apply configuration to orchestrator
            EnhancedTradingLogOrchestrator.Instance.UpdateConfiguration(_currentConfig);
            
            _logger.LogInfo("Configuration updated", new 
            { 
                TradingThreshold = _currentConfig.Thresholds.TradingOperationMicroseconds,
                AiSensitivity = _currentConfig.AI.AnomalySensitivity,
                MethodLogging = _currentConfig.EnableMethodLifecycleLogging,
                ParameterLogging = _currentConfig.EnableParameterLogging
            });
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to apply configuration", ex);
        }
    }
    
    #endregion
    
    #region Cleanup
    
    public void Dispose()
    {
        _metricsUpdateTimer?.Stop();
        _chartUpdateTimer?.Stop();
        _cancellationTokenSource?.Cancel();
        _webSocket?.CloseAsync(WebSocketCloseStatus.GoingAway, "Window closing", CancellationToken.None);
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
        
        // Unsubscribe from analytics events
        if (_analyticsService != null)
        {
            _analyticsService.AlertTriggered -= OnAlertTriggered;
            _analyticsService.PatternDetected -= OnPatternDetected;
            _analyticsService.PerformanceAnalysis -= OnPerformanceAnalysis;
        }
        
        _logger.LogInfo("Log Analyzer Screen disposed");
    }
    
    #endregion
}

#region Display Models

/// <summary>
/// Display model for log entries in the UI
/// </summary>
public class LogDisplayItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public SolidColorBrush LevelColor { get; set; } = new(Windows.UI.Color.FromArgb(255, 128, 128, 128));
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AnomalyScore { get; set; } = string.Empty;
    public SolidColorBrush AnomalyColor { get; set; } = new(Windows.UI.Color.FromArgb(255, 128, 128, 128));
    public LogEntry FullEntry { get; set; } = new();
}

/// <summary>
/// Display model for alerts in the UI
/// </summary>
public class AlertDisplayItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public SolidColorBrush SeverityColor { get; set; } = new(Windows.UI.Color.FromArgb(255, 128, 128, 128));
    public string Timestamp { get; set; } = string.Empty;
}

#endregion