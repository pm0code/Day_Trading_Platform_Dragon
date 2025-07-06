using ILGPU.Runtime;

namespace TradingPlatform.GPU.Models;

/// <summary>
/// GPU device information for trading platform
/// </summary>
public record GpuDeviceInfo
{
    public string Name { get; init; } = string.Empty;
    public AcceleratorType Type { get; init; }
    public long MemoryGB { get; init; }
    public int MaxThreadsPerGroup { get; init; }
    public int WarpSize { get; init; }
    public bool IsRtx { get; init; }
    public int Score { get; init; }
}