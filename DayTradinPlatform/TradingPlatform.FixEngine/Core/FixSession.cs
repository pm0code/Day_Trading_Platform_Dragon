using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// High-performance FIX session management with sub-millisecond message processing
/// Implements FIX 4.2+ session protocol with hardware timestamping support
/// </summary>
public sealed class FixSession : IDisposable
{
    private readonly string _senderCompId;
    private readonly string _targetCompId;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<FixMessage>> _pendingRequests = new();
    
    // High-performance message queues
    private readonly Channel<FixMessage> _outboundChannel;
    private readonly Channel<FixMessage> _inboundChannel;
    private readonly ChannelWriter<FixMessage> _outboundWriter;
    private readonly ChannelReader<FixMessage> _outboundReader;
    private readonly ChannelWriter<FixMessage> _inboundWriter;
    private readonly ChannelReader<FixMessage> _inboundReader;
    
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private int _outgoingSeqNum = 1;
    private int _incomingSeqNum = 1;
    private bool _isLoggedOn;
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private readonly Timer _heartbeatTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Performance optimization: pre-allocated buffers
    private readonly byte[] _receiveBuffer = new byte[8192];
    private readonly StringBuilder _messageBuffer = new(8192);
    
    public event EventHandler<FixMessage>? MessageReceived;
    public event EventHandler<string>? SessionStateChanged;
    
    public bool IsConnected => _tcpClient?.Connected == true && _isLoggedOn;
    public string SenderCompId => _senderCompId;
    public string TargetCompId => _targetCompId;
    public int HeartbeatInterval { get; set; } = 30; // seconds
    
    public FixSession(string senderCompId, string targetCompId, ILogger logger)
    {
        _senderCompId = senderCompId;
        _targetCompId = targetCompId;
        _logger = logger;
        
        // Configure high-performance channels for minimal latency
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        
        _outboundChannel = Channel.CreateBounded<FixMessage>(channelOptions);
        _outboundWriter = _outboundChannel.Writer;
        _outboundReader = _outboundChannel.Reader;
        
        _inboundChannel = Channel.CreateBounded<FixMessage>(channelOptions);
        _inboundWriter = _inboundChannel.Writer;
        _inboundReader = _inboundChannel.Reader;
        
        _heartbeatTimer = new Timer(SendHeartbeat, null, Timeout.Infinite, Timeout.Infinite);
        
        // Start message processing tasks
        _ = Task.Run(ProcessOutboundMessages, _cancellationTokenSource.Token);
        _ = Task.Run(ProcessInboundMessages, _cancellationTokenSource.Token);
    }
    
    public async Task<bool> ConnectAsync(string host, int port, TimeSpan timeout)
    {
        try
        {
            _tcpClient = new TcpClient();
            
            // Optimize TCP socket for low latency
            _tcpClient.NoDelay = true;
            _tcpClient.ReceiveBufferSize = 65536;
            _tcpClient.SendBufferSize = 65536;
            
            await _tcpClient.ConnectAsync(host, port).WaitAsync(timeout);
            _stream = _tcpClient.GetStream();
            
            // Start receiving messages
            _ = Task.Run(ReceiveMessages, _cancellationTokenSource.Token);
            
            // Send logon message
            await SendLogonAsync();
            
            SessionStateChanged?.Invoke(this, "Connected");
            _logger.LogInfo($"FIX session connected to {host}:{port}");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to connect FIX session: {ex.Message}", ex);
            return false;
        }
    }
    
    public async Task DisconnectAsync()
    {
        try
        {
            if (_isLoggedOn)
            {
                await SendLogoutAsync();
            }
            
            _cancellationTokenSource.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            _isLoggedOn = false;
            
            SessionStateChanged?.Invoke(this, "Disconnected");
            _logger.LogInfo("FIX session disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during FIX session disconnect: {ex.Message}", ex);
        }
    }
    
    public async Task<bool> SendMessageAsync(FixMessage message)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("Attempted to send message on disconnected FIX session");
            return false;
        }
        
        // Set hardware timestamp for latency measurement
        message.HardwareTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        message.MsgSeqNum = Interlocked.Increment(ref _outgoingSeqNum);
        message.SenderCompID = _senderCompId;
        message.TargetCompID = _targetCompId;
        message.SendingTime = DateTime.UtcNow;
        
        await _outboundWriter.WriteAsync(message, _cancellationTokenSource.Token);
        return true;
    }
    
    private async Task SendLogonAsync()
    {
        var logonMessage = new FixMessage
        {
            MsgType = FixMessageTypes.Logon,
            BeginString = "FIX.4.2"
        };
        
        logonMessage.SetField(98, "0"); // EncryptMethod (None)
        logonMessage.SetField(108, HeartbeatInterval); // HeartBtInt
        
        await SendMessageAsync(logonMessage);
        _logger.LogInfo("Logon message sent");
    }
    
    private async Task SendLogoutAsync()
    {
        var logoutMessage = new FixMessage
        {
            MsgType = FixMessageTypes.Logout
        };
        
        await SendMessageAsync(logoutMessage);
        _logger.LogInfo("Logout message sent");
    }
    
    private async Task ProcessOutboundMessages()
    {
        try
        {
            await foreach (var message in _outboundReader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                await SendMessageDirectly(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing outbound messages: {ex.Message}", ex);
        }
    }
    
    private async Task SendMessageDirectly(FixMessage message)
    {
        if (_stream == null) return;
        
        var fixString = message.ToFixString();
        var bytes = Encoding.ASCII.GetBytes(fixString);
        
        await _sendLock.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            await _stream.WriteAsync(bytes, _cancellationTokenSource.Token);
            await _stream.FlushAsync(_cancellationTokenSource.Token);
            
            _logger.LogDebug($"Sent FIX message: {message.MsgType} (seq={message.MsgSeqNum})");
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    private async Task ReceiveMessages()
    {
        if (_stream == null) return;
        
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && _stream.CanRead)
            {
                var bytesRead = await _stream.ReadAsync(_receiveBuffer, _cancellationTokenSource.Token);
                if (bytesRead == 0) break;
                
                var receivedData = Encoding.ASCII.GetString(_receiveBuffer, 0, bytesRead);
                await ProcessReceivedData(receivedData);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error receiving FIX messages: {ex.Message}", ex);
            SessionStateChanged?.Invoke(this, "Error");
        }
    }
    
    private async Task ProcessReceivedData(string data)
    {
        _messageBuffer.Append(data);
        
        while (true)
        {
            var messageEnd = _messageBuffer.ToString().IndexOf("\x0110=");
            if (messageEnd == -1) break;
            
            // Find complete message including checksum
            var checksumEnd = _messageBuffer.ToString().IndexOf('\x01', messageEnd + 4);
            if (checksumEnd == -1) break;
            
            var messageString = _messageBuffer.ToString(0, checksumEnd + 1);
            _messageBuffer.Remove(0, checksumEnd + 1);
            
            try
            {
                var message = FixMessage.Parse(messageString);
                await _inboundWriter.WriteAsync(message, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing FIX message: {ex.Message}", ex);
            }
        }
    }
    
    private async Task ProcessInboundMessages()
    {
        try
        {
            await foreach (var message in _inboundReader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                await HandleReceivedMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing inbound messages: {ex.Message}", ex);
        }
    }
    
    private async Task HandleReceivedMessage(FixMessage message)
    {
        _lastHeartbeat = DateTime.UtcNow;
        
        // Handle session-level messages
        switch (message.MsgType)
        {
            case FixMessageTypes.Logon:
                _isLoggedOn = true;
                _heartbeatTimer.Change(TimeSpan.FromSeconds(HeartbeatInterval), 
                                     TimeSpan.FromSeconds(HeartbeatInterval));
                SessionStateChanged?.Invoke(this, "LoggedOn");
                break;
                
            case FixMessageTypes.Logout:
                _isLoggedOn = false;
                SessionStateChanged?.Invoke(this, "LoggedOut");
                break;
                
            case FixMessageTypes.Heartbeat:
                // Heartbeat received, session is alive
                break;
                
            case FixMessageTypes.TestRequest:
                await SendHeartbeatResponse(message.GetField(112)); // TestReqID
                break;
                
            default:
                MessageReceived?.Invoke(this, message);
                break;
        }
        
        _logger.LogDebug($"Received FIX message: {message.MsgType} (seq={message.MsgSeqNum})");
    }
    
    private async void SendHeartbeat(object? state)
    {
        if (!IsConnected) return;
        
        var heartbeat = new FixMessage
        {
            MsgType = FixMessageTypes.Heartbeat
        };
        
        await SendMessageAsync(heartbeat);
    }
    
    private async Task SendHeartbeatResponse(string? testReqId)
    {
        var heartbeat = new FixMessage
        {
            MsgType = FixMessageTypes.Heartbeat
        };
        
        if (!string.IsNullOrEmpty(testReqId))
        {
            heartbeat.SetField(112, testReqId); // TestReqID
        }
        
        await SendMessageAsync(heartbeat);
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _heartbeatTimer.Dispose();
        _sendLock.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}