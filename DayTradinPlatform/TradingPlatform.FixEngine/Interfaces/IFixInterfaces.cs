using System;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.FixEngine.Services
{
    /// <summary>
    /// Main interface for FIX engine operations.
    /// </summary>
    public interface IFixEngineService
    {
        /// <summary>
        /// Sends a new order through FIX protocol.
        /// </summary>
        Task<TradingResult<FixOrder>> SendOrderAsync(
            Trading.OrderRequest request,
            IProgress<Models.OrderExecutionProgress>? progress = null);
        
        /// <summary>
        /// Cancels an existing order.
        /// </summary>
        Task<TradingResult> CancelOrderAsync(string clOrdId, string? sessionId = null);
        
        /// <summary>
        /// Subscribes to market data for specified symbols.
        /// </summary>
        Task<TradingResult> SubscribeMarketDataAsync(string[] symbols, string? sessionId = null);
        
        /// <summary>
        /// Initializes the FIX engine.
        /// </summary>
        Task<TradingResult> InitializeAsync();
        
        /// <summary>
        /// Starts the FIX engine.
        /// </summary>
        Task<TradingResult> StartAsync();
        
        /// <summary>
        /// Stops the FIX engine.
        /// </summary>
        Task<TradingResult> StopAsync();
    }
    
    /// <summary>
    /// Interface for managing FIX sessions.
    /// </summary>
    public interface IFixSessionManager
    {
        /// <summary>
        /// Initializes the session manager.
        /// </summary>
        Task<TradingResult> InitializeAsync();
        
        /// <summary>
        /// Starts all configured sessions.
        /// </summary>
        Task<TradingResult> StartAllSessionsAsync();
        
        /// <summary>
        /// Stops all active sessions.
        /// </summary>
        Task<TradingResult> StopAllSessionsAsync();
        
        /// <summary>
        /// Gets an active session by ID.
        /// </summary>
        Task<TradingResult<FixSession>> GetActiveSessionAsync(string? sessionId = null);
        
        /// <summary>
        /// Gets the count of active sessions.
        /// </summary>
        Task<TradingResult<int>> GetActiveSessionCountAsync();
        
        /// <summary>
        /// Creates a new session with the specified configuration.
        /// </summary>
        Task<TradingResult<FixSession>> CreateSessionAsync(FixSessionConfig config);
        
        /// <summary>
        /// Sends a message through the specified session.
        /// </summary>
        Task<TradingResult> SendMessageAsync(string sessionId, FixMessage message);
    }
    
    /// <summary>
    /// Interface for managing FIX orders.
    /// </summary>
    public interface IFixOrderManager
    {
        /// <summary>
        /// Initializes the order manager.
        /// </summary>
        Task<TradingResult> InitializeAsync();
        
        /// <summary>
        /// Creates and sends a new order.
        /// </summary>
        Task<TradingResult<FixOrder>> CreateAndSendOrderAsync(
            FixSession session,
            Trading.OrderRequest request,
            IProgress<Models.OrderExecutionProgress>? progress = null);
        
        /// <summary>
        /// Cancels an existing order.
        /// </summary>
        Task<TradingResult> CancelOrderAsync(string clOrdId, string? sessionId = null);
        
        /// <summary>
        /// Replaces an existing order.
        /// </summary>
        Task<TradingResult<FixOrder>> ReplaceOrderAsync(
            string originalClOrdId,
            Trading.OrderRequest newRequest,
            string? sessionId = null);
        
        /// <summary>
        /// Gets order status.
        /// </summary>
        Task<TradingResult<FixOrder>> GetOrderStatusAsync(string clOrdId);
        
        /// <summary>
        /// Processes an execution report.
        /// </summary>
        Task<TradingResult> ProcessExecutionReportAsync(FixExecutionReport report);
    }
    
    /// <summary>
    /// Interface for managing FIX market data.
    /// </summary>
    public interface IFixMarketDataManager
    {
        /// <summary>
        /// Initializes the market data manager.
        /// </summary>
        Task<TradingResult> InitializeAsync();
        
        /// <summary>
        /// Subscribes to market data for symbols.
        /// </summary>
        Task<TradingResult> SubscribeAsync(string[] symbols, string? sessionId = null);
        
        /// <summary>
        /// Unsubscribes from market data.
        /// </summary>
        Task<TradingResult> UnsubscribeAsync(string[] symbols, string? sessionId = null);
        
        /// <summary>
        /// Processes market data update.
        /// </summary>
        Task<TradingResult> ProcessMarketDataUpdateAsync(FixMessage message);
    }
    
    /// <summary>
    /// Interface for processing FIX messages.
    /// </summary>
    public interface IFixMessageProcessor
    {
        /// <summary>
        /// Processes the next available message.
        /// </summary>
        Task ProcessNextMessageAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Processes a specific FIX message.
        /// </summary>
        Task<TradingResult> ProcessMessageAsync(FixMessage message);
        
        /// <summary>
        /// Registers a message handler for a specific message type.
        /// </summary>
        void RegisterHandler(string messageType, Func<FixMessage, Task<TradingResult>> handler);
    }
    
    /// <summary>
    /// Represents an active FIX session.
    /// </summary>
    public class FixSession
    {
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the session configuration.
        /// </summary>
        public FixSessionConfig Config { get; set; } = new();
        
        /// <summary>
        /// Gets or sets whether the session is connected.
        /// </summary>
        public bool IsConnected { get; set; }
        
        /// <summary>
        /// Gets or sets the current outgoing sequence number.
        /// </summary>
        public int OutgoingSequenceNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the current incoming sequence number.
        /// </summary>
        public int IncomingSequenceNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the last heartbeat time.
        /// </summary>
        public DateTime LastHeartbeatTime { get; set; }
        
        /// <summary>
        /// Gets or sets the session start time.
        /// </summary>
        public DateTime SessionStartTime { get; set; }
    }
}