namespace TradingPlatform.GPU.Infrastructure;

/// <summary>
/// Simple logger for GPU operations
/// </summary>
public class SimpleLogger
{
    private static SimpleLogger? _instance;
    public static SimpleLogger Instance => _instance ??= new SimpleLogger();
    
    public void LogInfo(string eventCode, string message, object? additionalData = null)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] INFO [{eventCode}] {message}");
        if (additionalData != null)
        {
            Console.WriteLine($"  Data: {System.Text.Json.JsonSerializer.Serialize(additionalData)}");
        }
    }
    
    public void LogDebug(string eventCode, string message, object? additionalData = null)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] DEBUG [{eventCode}] {message}");
        if (additionalData != null)
        {
            Console.WriteLine($"  Data: {System.Text.Json.JsonSerializer.Serialize(additionalData)}");
        }
    }
    
    public void LogWarning(string eventCode, string message, object? additionalData = null)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WARN [{eventCode}] {message}");
        if (additionalData != null)
        {
            Console.WriteLine($"  Data: {System.Text.Json.JsonSerializer.Serialize(additionalData)}");
        }
    }
    
    public void LogError(string eventCode, string message, Exception? ex = null)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] ERROR [{eventCode}] {message}");
        if (ex != null)
        {
            Console.WriteLine($"  Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"  Stack: {ex.StackTrace}");
        }
    }
}