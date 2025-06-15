namespace TradingPlatform.FixEngine.Models;

/// <summary>
/// FIX message types for ultra-low latency trading operations
/// Based on FIX 4.2+ specifications with extensions for electronic trading
/// </summary>
public static class FixMessageTypes
{
    // Session-level messages
    public const string Heartbeat = "0";
    public const string TestRequest = "1";
    public const string ResendRequest = "2";
    public const string Reject = "3";
    public const string SequenceReset = "4";
    public const string Logout = "5";
    public const string Logon = "A";
    
    // Application-level messages - Trading
    public const string NewOrderSingle = "D";
    public const string OrderCancelRequest = "F";
    public const string OrderCancelReplaceRequest = "G";
    public const string ExecutionReport = "8";
    public const string OrderCancelReject = "9";
    
    // Market Data messages
    public const string MarketDataRequest = "V";
    public const string MarketDataSnapshotFullRefresh = "W";
    public const string MarketDataIncrementalRefresh = "X";
    public const string MarketDataRequestReject = "Y";
    
    // Quote messages
    public const string Quote = "S";
    public const string QuoteRequest = "R";
    public const string QuoteCancel = "Z";
    public const string QuoteStatusReport = "AI";
    
    // Mass operations for high-frequency trading
    public const string MassQuote = "i";
    public const string BusinessMessageReject = "j";
    public const string OrderMassStatusRequest = "AF";
    public const string OrderMassCancelRequest = "q";
    
    // Security definition and status
    public const string SecurityDefinitionRequest = "c";
    public const string SecurityDefinition = "d";
    public const string SecurityStatusRequest = "e";
    public const string SecurityStatus = "f";
    
    // Trading session messages
    public const string TradingSessionStatusRequest = "g";
    public const string TradingSessionStatus = "h";
    
    // News and announcements
    public const string News = "B";
    
    // Cross-order messages for block trading
    public const string CrossOrderCancelReplaceRequest = "t";
    public const string CrossOrderCancelRequest = "u";
    
    // Settlement and allocation
    public const string AllocationInstruction = "J";
    public const string AllocationInstructionAck = "P";
    public const string AllocationReport = "AS";
    
    /// <summary>
    /// Determines if message type requires immediate processing for latency-critical operations
    /// </summary>
    public static bool IsLatencyCritical(string msgType)
    {
        return msgType switch
        {
            NewOrderSingle or 
            OrderCancelRequest or 
            OrderCancelReplaceRequest or 
            ExecutionReport or 
            MarketDataIncrementalRefresh or 
            MarketDataSnapshotFullRefresh or 
            Quote or 
            MassQuote => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Determines if message type is session-level (administrative)
    /// </summary>
    public static bool IsSessionLevel(string msgType)
    {
        return msgType switch
        {
            Heartbeat or 
            TestRequest or 
            ResendRequest or 
            Reject or 
            SequenceReset or 
            Logout or 
            Logon => true,
            _ => false
        };
    }
}