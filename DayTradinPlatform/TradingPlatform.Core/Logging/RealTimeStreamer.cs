// TradingPlatform.Core.Logging.RealTimeStreamer - REAL-TIME LOG STREAMING
// WebSocket streaming for live log monitoring and analysis
// Integration with Log Analyzer UI for multi-monitor display

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Net;
using System.Text;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Real-time log streaming service for live monitoring
/// Provides WebSocket endpoints for Log Analyzer UI integration
/// </summary>
internal class RealTimeStreamer : IDisposable
{
    private readonly StreamingConfiguration _config;
    private readonly ConcurrentDictionary<string, WebSocket> _connectedClients = new();
    private readonly ConcurrentQueue<LogEntry> _streamBuffer = new();
    private readonly Timer _streamTimer;
    private HttpListener? _httpListener;
    private CancellationTokenSource _cancellationTokenSource = new();

    public RealTimeStreamer(StreamingConfiguration config)
    {
        _config = config;
        
        if (_config.EnableWebSocketStreaming)
        {
            _streamTimer = new Timer(ProcessStreamBuffer, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }
    }

    public void Start()
    {
        if (!_config.EnableWebSocketStreaming)
            return;

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_config.StreamingPort}/logs/");
            _httpListener.Start();
            
            Task.Run(AcceptWebSocketConnections);
            Console.WriteLine($"Real-time log streaming started on port {_config.StreamingPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start real-time streaming: {ex.Message}");
        }
    }

    public void StreamLogEntry(LogEntry entry)
    {
        if (!_config.EnableWebSocketStreaming || _connectedClients.IsEmpty)
            return;

        _streamBuffer.Enqueue(entry);
        
        // Limit buffer size
        while (_streamBuffer.Count > _config.StreamingBufferSize)
        {
            _streamBuffer.TryDequeue(out _);
        }
    }

    private async Task AcceptWebSocketConnections()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    var clientId = Guid.NewGuid().ToString();
                    
                    _connectedClients.TryAdd(clientId, webSocketContext.WebSocket);
                    
                    // Handle client connection
                    _ = Task.Run(() => HandleWebSocketClient(clientId, webSocketContext.WebSocket));
                    
                    Console.WriteLine($"WebSocket client connected: {clientId}");
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting WebSocket connection: {ex.Message}");
            }
        }
    }

    private async Task HandleWebSocketClient(string clientId, WebSocket webSocket)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client requested close", _cancellationTokenSource.Token);
                    break;
                }
                
                // Handle client messages (e.g., filtering requests)
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessClientMessage(clientId, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling WebSocket client {clientId}: {ex.Message}");
        }
        finally
        {
            _connectedClients.TryRemove(clientId, out _);
            Console.WriteLine($"WebSocket client disconnected: {clientId}");
        }
    }

    private async Task ProcessClientMessage(string clientId, string message)
    {
        // Process filtering or configuration messages from clients
        // For now, just acknowledge
        await Task.CompletedTask;
    }

    private void ProcessStreamBuffer(object? state)
    {
        if (_connectedClients.IsEmpty || _streamBuffer.IsEmpty)
            return;

        var entries = new List<LogEntry>();
        while (entries.Count < 100 && _streamBuffer.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        if (entries.Count == 0)
            return;

        var json = System.Text.Json.JsonSerializer.Serialize(entries);
        var buffer = Encoding.UTF8.GetBytes(json);

        // Send to all connected clients
        var clientsToRemove = new List<string>();
        
        foreach (var kvp in _connectedClients)
        {
            try
            {
                if (kvp.Value.State == WebSocketState.Open)
                {
                    _ = kvp.Value.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    clientsToRemove.Add(kvp.Key);
                }
            }
            catch
            {
                clientsToRemove.Add(kvp.Key);
            }
        }

        // Remove disconnected clients
        foreach (var clientId in clientsToRemove)
        {
            _connectedClients.TryRemove(clientId, out _);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _streamTimer?.Dispose();
        _httpListener?.Stop();
        _httpListener?.Close();
        
        // Close all WebSocket connections
        foreach (var kvp in _connectedClients)
        {
            try
            {
                if (kvp.Value.State == WebSocketState.Open)
                {
                    kvp.Value.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutdown", CancellationToken.None).Wait(1000);
                }
                kvp.Value.Dispose();
            }
            catch { }
        }
        
        _cancellationTokenSource.Dispose();
    }
}
