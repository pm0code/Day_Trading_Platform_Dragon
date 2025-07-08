using System.ComponentModel.DataAnnotations;

namespace MarketAnalyzer.Infrastructure.AI.Configuration;

/// <summary>
/// Configuration options for AI/ML inference using industry-standard libraries.
/// Based on research from ML_Inference_Acceleration_Options_2025.md.
/// </summary>
public class AIOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "AI";

    /// <summary>
    /// Gets or sets the path where ONNX models are stored.
    /// </summary>
    [Required]
    public string ModelStoragePath { get; set; } = "models";

    /// <summary>
    /// Gets or sets the preferred execution provider.
    /// Options: CPU, CUDA, DirectML, TensorRT, OpenVINO
    /// </summary>
    public ExecutionProvider PreferredExecutionProvider { get; set; } = ExecutionProvider.CPU;

    /// <summary>
    /// Gets or sets the GPU device ID to use (0-based).
    /// </summary>
    [Range(0, 7)]
    public int GpuDeviceId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of intra-op threads for CPU execution.
    /// Recommended: Physical cores / 2 for Intel i9-14900K = 16
    /// </summary>
    [Range(1, 64)]
    public int IntraOpNumThreads { get; set; } = 16;

    /// <summary>
    /// Gets or sets the number of inter-op threads for CPU execution.
    /// </summary>
    [Range(1, 16)]
    public int InterOpNumThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether to enable CPU spinning for low latency.
    /// </summary>
    public bool EnableCpuSpinning { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable memory pattern optimization.
    /// </summary>
    public bool EnableMemoryPattern { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable profiling for performance analysis.
    /// </summary>
    public bool EnableProfiling { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum model cache size in MB.
    /// </summary>
    [Range(100, 10000)]
    public int MaxModelCacheSizeMB { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the model warm-up iteration count.
    /// </summary>
    [Range(0, 100)]
    public int ModelWarmUpIterations { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to use TensorRT optimization for NVIDIA GPUs.
    /// </summary>
    public bool EnableTensorRTOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the TensorRT max workspace size in MB.
    /// </summary>
    [Range(256, 8192)]
    public int TensorRTMaxWorkspaceSizeMB { get; set; } = 2048;

    /// <summary>
    /// Gets or sets whether to enable FP16 precision for GPU inference.
    /// </summary>
    public bool EnableFP16 { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for inference.
    /// </summary>
    [Range(1, 1024)]
    public int DefaultBatchSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the inference timeout in milliseconds.
    /// </summary>
    [Range(10, 60000)]
    public int InferenceTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to preload models on startup.
    /// </summary>
    public bool PreloadModelsOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of models to preload.
    /// </summary>
    public List<string> ModelsToPreload { get; } = new();
}

/// <summary>
/// Supported execution providers for ONNX Runtime.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>
    /// CPU execution (default, always available)
    /// </summary>
    CPU = 0,

    /// <summary>
    /// NVIDIA CUDA execution (RTX 4070 Ti, RTX 3060 Ti)
    /// </summary>
    CUDA = 1,

    /// <summary>
    /// DirectML execution (AMD GPUs, Intel Arc)
    /// </summary>
    DirectML = 2,

    /// <summary>
    /// NVIDIA TensorRT execution (lowest latency)
    /// </summary>
    TensorRT = 3,

    /// <summary>
    /// Intel OpenVINO execution (Intel CPUs/GPUs)
    /// </summary>
    OpenVINO = 4
}