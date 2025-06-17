using System;

class Program 
{
    static void Main()
    {
        var now = DateTimeOffset.UtcNow;
        var ticks = now.Ticks;
        var unixTicks = DateTimeOffset.UnixEpoch.Ticks;
        var ticksSinceUnix = ticks - unixTicks;
        var hardwareTimestamp = ticksSinceUnix * 100L;
        
        var millis = now.ToUnixTimeMilliseconds();
        var expectedMin = millis * 1_000_000L;
        
        Console.WriteLine($"Current Ticks: {ticks}");
        Console.WriteLine($"Unix Epoch Ticks: {unixTicks}");
        Console.WriteLine($"Ticks since Unix: {ticksSinceUnix}");
        Console.WriteLine($"Hardware Timestamp (current impl): {hardwareTimestamp}");
        Console.WriteLine($"Expected min (test): {expectedMin}");
        Console.WriteLine($"Ratio: {(double)hardwareTimestamp / expectedMin:F2}");
        Console.WriteLine($"Hardware Timestamp > 1e18? {hardwareTimestamp > 1_000_000_000_000_000_000L}");
    }
}
EOF < /dev/null