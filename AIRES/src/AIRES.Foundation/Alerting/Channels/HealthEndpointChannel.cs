using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Health endpoint channel that exposes alerts via HTTP for external monitoring.
/// Provides a REST endpoint for health checks and recent alert retrieval.
/// </summary>
public class HealthEndpointChannel : AIRESServiceBase, IAlertChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    
    private readonly HttpListener? _httpListener;
    private readonly string _endpointUrl;
    private readonly int _maxRecentAlerts;
    private readonly ConcurrentQueue<AlertMessage> _recentAlerts = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private readonly Dictionary<AlertSeverity, long> _alertsBySeverity = new();
    private long _totalAlertsProcessed;
    private DateTime _lastAlertTime = DateTime.UtcNow;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    
    public string ChannelName => "HealthEndpoint";
    public AlertChannelType ChannelType => AlertChannelType.HealthEndpoint;
    public bool IsEnabled { get; private set; }
    public AlertSeverity MinimumSeverity { get; }
    
    public HealthEndpointChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(HealthEndpointChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:HealthEndpoint");
        var enabledValue = channelConfig["Enabled"];
        IsEnabled = !string.IsNullOrWhiteSpace(enabledValue) && 
                   (enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    enabledValue.Equals("1", StringComparison.OrdinalIgnoreCase)); // Disabled by default
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Information");
        
        var portValue = channelConfig["Port"];
        var port = !string.IsNullOrWhiteSpace(portValue) && int.TryParse(portValue, out var p) ? p : 9090;
        _endpointUrl = $"http://+:{port}/aires/health/";
        
        var maxAlertsValue = channelConfig["MaxRecentAlerts"];
        _maxRecentAlerts = !string.IsNullOrWhiteSpace(maxAlertsValue) && int.TryParse(maxAlertsValue, out var m) ? m : 100;
        
        // Initialize severity counters
        foreach (AlertSeverity severity in Enum.GetValues<AlertSeverity>())
        {
            _alertsBySeverity[severity] = 0;
        }
        
        // Initialize HTTP listener if enabled
        if (IsEnabled && HttpListener.IsSupported)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add(_endpointUrl);
                _httpListener.Start();
                
                // Start background listener
                _listenerCts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenForRequests(_listenerCts.Token));
                
                Logger.LogInfo($"Health endpoint started on: {_endpointUrl}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start health endpoint", ex);
                IsEnabled = false;
            }
        }
        else if (IsEnabled && !HttpListener.IsSupported)
        {
            Logger.LogWarning("Health endpoint is enabled but HttpListener is not supported on this platform.");
            IsEnabled = false;
        }
    }
    
    public async Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (!IsEnabled)
        {
            LogMethodExit();
            return;
        }
        
        if (alert.Severity < MinimumSeverity)
        {
            LogMethodExit();
            return;
        }
        
        await _updateSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Add to recent alerts queue
            _recentAlerts.Enqueue(alert);
            
            // Maintain max size
            while (_recentAlerts.Count > _maxRecentAlerts)
            {
                _recentAlerts.TryDequeue(out _);
            }
            
            // Update metrics
            _totalAlertsProcessed++;
            _alertsBySeverity[alert.Severity]++;
            _lastAlertTime = DateTime.UtcNow;
            
            Logger.LogDebug($"Alert added to health endpoint: {alert.Id}");
        }
        finally
        {
            _updateSemaphore.Release();
            LogMethodExit();
        }
    }
    
    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(IsEnabled && _httpListener != null && _httpListener.IsListening);
    }
    
    public async Task<Dictionary<string, object>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["ChannelName"] = ChannelName,
            ["EndpointUrl"] = _endpointUrl,
            ["IsListening"] = _httpListener?.IsListening ?? false,
            ["TotalAlertsProcessed"] = _totalAlertsProcessed,
            ["RecentAlertsCount"] = _recentAlerts.Count,
            ["LastAlertTime"] = _lastAlertTime,
            ["MaxRecentAlerts"] = _maxRecentAlerts
        };
        
        // Add severity breakdown
        await _updateSemaphore.WaitAsync();
        try
        {
            foreach (var kvp in _alertsBySeverity)
            {
                metrics[$"Alerts_{kvp.Key}"] = kvp.Value;
            }
        }
        finally
        {
            _updateSemaphore.Release();
        }
        
        return metrics;
    }
    
    private async Task ListenForRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
        {
            try
            {
                var contextTask = _httpListener.GetContextAsync();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                
                var context = await contextTask;
                
                // Handle request asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequest(context);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error handling health endpoint request", ex);
                    }
                }, CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                // Operation aborted (listener stopped)
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in health endpoint listener", ex);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
    
    private async Task HandleRequest(HttpListenerContext context)
    {
        var response = context.Response;
        
        try
        {
            if (context.Request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }
            
            var path = context.Request.Url?.AbsolutePath ?? "/";
            
            if (path.EndsWith("/health/"))
            {
                // Health check endpoint
                await SendHealthResponse(response);
            }
            else if (path.EndsWith("/health/alerts"))
            {
                // Recent alerts endpoint
                await SendAlertsResponse(response);
            }
            else if (path.EndsWith("/health/metrics"))
            {
                // Metrics endpoint
                await SendMetricsResponse(response);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error processing health endpoint request", ex);
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            response.Close();
        }
    }
    
    private async Task SendHealthResponse(HttpListenerResponse response)
    {
        await _updateSemaphore.WaitAsync();
        try
        {
            var health = new
            {
                status = DetermineHealthStatus(),
                timestamp = DateTime.UtcNow,
                alertsSummary = new
                {
                    total = _totalAlertsProcessed,
                    recent = _recentAlerts.Count,
                    critical = _alertsBySeverity[AlertSeverity.Critical],
                    error = _alertsBySeverity[AlertSeverity.Error],
                    warning = _alertsBySeverity[AlertSeverity.Warning],
                    information = _alertsBySeverity[AlertSeverity.Information]
                },
                lastAlertTime = _lastAlertTime
            };
            
            var json = JsonSerializer.Serialize(health, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);
            
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)HttpStatusCode.OK;
            
            await response.OutputStream.WriteAsync(buffer);
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }
    
    private async Task SendAlertsResponse(HttpListenerResponse response)
    {
        await _updateSemaphore.WaitAsync();
        try
        {
            var alerts = _recentAlerts.ToArray();
            var json = JsonSerializer.Serialize(alerts, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);
            
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)HttpStatusCode.OK;
            
            await response.OutputStream.WriteAsync(buffer);
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }
    
    private async Task SendMetricsResponse(HttpListenerResponse response)
    {
        var metrics = await GetMetricsAsync();
        var json = JsonSerializer.Serialize(metrics, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);
        
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        response.StatusCode = (int)HttpStatusCode.OK;
        
        await response.OutputStream.WriteAsync(buffer);
    }
    
    private string DetermineHealthStatus()
    {
        if (_alertsBySeverity[AlertSeverity.Critical] > 0)
            return "Critical";
        if (_alertsBySeverity[AlertSeverity.Error] > 5)
            return "Unhealthy";
        if (_alertsBySeverity[AlertSeverity.Warning] > 10)
            return "Degraded";
        return "Healthy";
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _listenerCts?.Cancel();
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore shutdown errors
            }
            
            _httpListener?.Stop();
            _httpListener?.Close();
            _updateSemaphore?.Dispose();
            _listenerCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}