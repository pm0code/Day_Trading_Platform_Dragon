using System;
using System.Collections.Generic;

namespace TradingPlatform.FixEngine.Models
{
    /// <summary>
    /// Represents a FIX protocol message with all required fields and metadata.
    /// Uses decimal for all financial values per mandatory standards.
    /// </summary>
    public class FixMessage
    {
        /// <summary>
        /// Gets or sets the message type (tag 35).
        /// </summary>
        public string MessageType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the sender CompID (tag 49).
        /// </summary>
        public string SenderCompId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the target CompID (tag 56).
        /// </summary>
        public string TargetCompId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the message sequence number (tag 34).
        /// </summary>
        public int SequenceNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the sending time (tag 52) in UTC.
        /// </summary>
        public DateTime SendingTime { get; set; }
        
        /// <summary>
        /// Gets the message fields dictionary.
        /// </summary>
        public Dictionary<int, string> Fields { get; } = new();
        
        /// <summary>
        /// Gets the raw message bytes if available.
        /// </summary>
        public byte[]? RawMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the hardware timestamp in microseconds.
        /// </summary>
        public long HardwareTimestamp { get; set; }
    }
    
    /// <summary>
    /// Represents a FIX order with all financial values as decimal.
    /// </summary>
    public class FixOrder
    {
        /// <summary>
        /// Gets or sets the client order ID (tag 11).
        /// </summary>
        public string ClOrdId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the order ID assigned by exchange (tag 37).
        /// </summary>
        public string? OrderId { get; set; }
        
        /// <summary>
        /// Gets or sets the symbol (tag 55).
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the order side (tag 54).
        /// </summary>
        public OrderSide Side { get; set; }
        
        /// <summary>
        /// Gets or sets the order type (tag 40).
        /// </summary>
        public OrderType OrderType { get; set; }
        
        /// <summary>
        /// Gets or sets the order quantity (tag 38).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal Quantity { get; set; }
        
        /// <summary>
        /// Gets or sets the limit price (tag 44).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal? Price { get; set; }
        
        /// <summary>
        /// Gets or sets the stop price (tag 99).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal? StopPrice { get; set; }
        
        /// <summary>
        /// Gets or sets the executed quantity (tag 14).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal ExecutedQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the average execution price (tag 6).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal? AveragePrice { get; set; }
        
        /// <summary>
        /// Gets or sets the order status (tag 39).
        /// </summary>
        public OrderStatus Status { get; set; }
        
        /// <summary>
        /// Gets or sets the time in force (tag 59).
        /// </summary>
        public TimeInForce TimeInForce { get; set; }
        
        /// <summary>
        /// Gets or sets the order creation timestamp.
        /// </summary>
        public DateTime CreateTime { get; set; }
        
        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTime? UpdateTime { get; set; }
        
        /// <summary>
        /// Gets or sets the hardware timestamp in microseconds.
        /// </summary>
        public long HardwareTimestamp { get; set; }
        
        /// <summary>
        /// Gets or sets MiFID II algorithm ID (tag 7928).
        /// </summary>
        public string? AlgorithmId { get; set; }
        
        /// <summary>
        /// Gets or sets MiFID II trading capacity (tag 1815).
        /// </summary>
        public TradingCapacity? TradingCapacity { get; set; }
    }
    
    /// <summary>
    /// Represents a FIX session configuration.
    /// </summary>
    public class FixSessionConfig
    {
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the sender CompID.
        /// </summary>
        public string SenderCompId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the target CompID.
        /// </summary>
        public string TargetCompId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the FIX version (e.g., "FIX.4.4").
        /// </summary>
        public string FixVersion { get; set; } = "FIX.4.4";
        
        /// <summary>
        /// Gets or sets the host address.
        /// </summary>
        public string Host { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the port number.
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// Gets or sets whether to use TLS (mandatory for 2025).
        /// </summary>
        public bool UseTls { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the TLS certificate path.
        /// </summary>
        public string? TlsCertificatePath { get; set; }
        
        /// <summary>
        /// Gets or sets the heartbeat interval in seconds.
        /// </summary>
        public int HeartbeatInterval { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets whether to reset sequence numbers on logon.
        /// </summary>
        public bool ResetOnLogon { get; set; }
        
        /// <summary>
        /// Gets or sets the message store path.
        /// </summary>
        public string? MessageStorePath { get; set; }
    }
    
    /// <summary>
    /// Order side enumeration (FIX tag 54).
    /// </summary>
    public enum OrderSide
    {
        Buy = '1',
        Sell = '2',
        BuyMinus = '3',
        SellPlus = '4',
        SellShort = '5',
        SellShortExempt = '6',
        Undisclosed = '7',
        Cross = '8',
        CrossShort = '9'
    }
    
    /// <summary>
    /// Order type enumeration (FIX tag 40).
    /// </summary>
    public enum OrderType
    {
        Market = '1',
        Limit = '2',
        Stop = '3',
        StopLimit = '4',
        MarketOnClose = '5',
        WithOrWithout = '6',
        LimitOrBetter = '7',
        LimitWithOrWithout = '8',
        OnBasis = '9',
        OnClose = 'A',
        LimitOnClose = 'B',
        ForexMarket = 'C',
        PreviouslyQuoted = 'D',
        PreviouslyIndicated = 'E',
        ForexLimit = 'F',
        ForexSwap = 'G',
        ForexPreviouslyQuoted = 'H',
        Funari = 'I',
        MarketIfTouched = 'J',
        MarketWithLeftOverAsLimit = 'K',
        PreviousFundValuationPoint = 'L',
        NextFundValuationPoint = 'M',
        Pegged = 'P'
    }
    
    /// <summary>
    /// Order status enumeration (FIX tag 39).
    /// </summary>
    public enum OrderStatus
    {
        New = '0',
        PartiallyFilled = '1',
        Filled = '2',
        DoneForDay = '3',
        Canceled = '4',
        Replaced = '5',
        PendingCancel = '6',
        Stopped = '7',
        Rejected = '8',
        Suspended = '9',
        PendingNew = 'A',
        Calculated = 'B',
        Expired = 'C',
        AcceptedForBidding = 'D',
        PendingReplace = 'E'
    }
    
    /// <summary>
    /// Time in force enumeration (FIX tag 59).
    /// </summary>
    public enum TimeInForce
    {
        Day = '0',
        GoodTillCancel = '1',
        AtTheOpening = '2',
        ImmediateOrCancel = '3',
        FillOrKill = '4',
        GoodTillCrossing = '5',
        GoodTillDate = '6',
        AtTheClose = '7'
    }
    
    /// <summary>
    /// MiFID II trading capacity (FIX tag 1815).
    /// </summary>
    public enum TradingCapacity
    {
        Principal = 'P',
        RisklessPrincipal = 'R',
        AnyOtherCapacity = 'A'
    }
    
    /// <summary>
    /// Represents a FIX execution report.
    /// </summary>
    public class FixExecutionReport
    {
        /// <summary>
        /// Gets or sets the execution ID (tag 17).
        /// </summary>
        public string ExecId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the client order ID (tag 11).
        /// </summary>
        public string ClOrdId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the order ID (tag 37).
        /// </summary>
        public string OrderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the execution type (tag 150).
        /// </summary>
        public ExecType ExecType { get; set; }
        
        /// <summary>
        /// Gets or sets the order status (tag 39).
        /// </summary>
        public OrderStatus OrderStatus { get; set; }
        
        /// <summary>
        /// Gets or sets the symbol (tag 55).
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the side (tag 54).
        /// </summary>
        public OrderSide Side { get; set; }
        
        /// <summary>
        /// Gets or sets the last executed quantity (tag 32).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal LastQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the last executed price (tag 31).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal LastPrice { get; set; }
        
        /// <summary>
        /// Gets or sets the cumulative quantity (tag 14).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal CumulativeQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the average price (tag 6).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal AveragePrice { get; set; }
        
        /// <summary>
        /// Gets or sets the leaves quantity (tag 151).
        /// MANDATORY: Uses decimal for financial precision.
        /// </summary>
        public decimal LeavesQuantity { get; set; }
        
        /// <summary>
        /// Gets or sets the transaction time (tag 60).
        /// </summary>
        public DateTime TransactionTime { get; set; }
        
        /// <summary>
        /// Gets or sets the text/reject reason (tag 58).
        /// </summary>
        public string? Text { get; set; }
        
        /// <summary>
        /// Gets or sets the hardware timestamp in microseconds.
        /// </summary>
        public long HardwareTimestamp { get; set; }
    }
    
    /// <summary>
    /// Execution type enumeration (FIX tag 150).
    /// </summary>
    public enum ExecType
    {
        New = '0',
        PartialFill = '1',
        Fill = '2',
        DoneForDay = '3',
        Canceled = '4',
        Replace = '5',
        PendingCancel = '6',
        Stopped = '7',
        Rejected = '8',
        Suspended = '9',
        PendingNew = 'A',
        Calculated = 'B',
        Expired = 'C',
        Restated = 'D',
        PendingReplace = 'E',
        Trade = 'F',
        TradeCorrect = 'G',
        TradeCancel = 'H',
        OrderStatus = 'I'
    }
}