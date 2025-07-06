using System;
using System.Collections.Generic;

namespace TradingPlatform.ML.Configuration
{
    /// <summary>
    /// Configuration for ML inference engine with ONNX Runtime support
    /// </summary>
    public class MLInferenceConfiguration
    {
        /// <summary>
        /// Gets or sets the execution provider for ONNX Runtime (CPU, CUDA, DirectML, etc.)
        /// </summary>
        public ExecutionProvider Provider { get; set; } = ExecutionProvider.CUDA;

        /// <summary>
        /// Gets or sets the GPU device ID for CUDA execution
        /// </summary>
        public int GpuDeviceId { get; set; } = 0;

        /// <summary>
        /// Gets or sets the maximum batch size for inference
        /// </summary>
        public int MaxBatchSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets the inference timeout in milliseconds
        /// </summary>
        public int InferenceTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets whether to enable ONNX Runtime profiling
        /// </summary>
        public bool EnableProfiling { get; set; } = false;

        /// <summary>
        /// Gets or sets the graph optimization level
        /// </summary>
        public GraphOptimizationLevel OptimizationLevel { get; set; } = GraphOptimizationLevel.All;

        /// <summary>
        /// Gets or sets the inter-op parallelism threads
        /// </summary>
        public int InterOpNumThreads { get; set; } = 0; // 0 = use default

        /// <summary>
        /// Gets or sets the intra-op parallelism threads
        /// </summary>
        public int IntraOpNumThreads { get; set; } = 0; // 0 = use default

        /// <summary>
        /// Gets or sets whether to enable memory pattern optimization
        /// </summary>
        public bool EnableMemoryPattern { get; set; } = true;

        /// <summary>
        /// Gets or sets the path to ONNX models directory
        /// </summary>
        public string ModelsPath { get; set; } = "Models/ONNX";

        /// <summary>
        /// Gets or sets model-specific configurations
        /// </summary>
        public Dictionary<string, ModelConfiguration> ModelConfigs { get; set; } = new();

        /// <summary>
        /// Gets or sets the warmup iterations for each model
        /// </summary>
        public int WarmupIterations { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to use IO binding for better GPU performance
        /// </summary>
        public bool UseIoBinding { get; set; } = true;

        /// <summary>
        /// Gets or sets the CUDA memory arena configuration
        /// </summary>
        public CudaMemoryArenaConfig CudaMemoryArena { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to enable TensorRT execution provider
        /// </summary>
        public bool EnableTensorRT { get; set; } = false;

        /// <summary>
        /// Gets or sets TensorRT configuration if enabled
        /// </summary>
        public TensorRTConfig TensorRTConfig { get; set; } = new();
    }

    /// <summary>
    /// Execution provider options for ONNX Runtime
    /// </summary>
    public enum ExecutionProvider
    {
        CPU,
        CUDA,
        DirectML,
        TensorRT,
        OpenVINO,
        NNAPI,
        CoreML
    }

    /// <summary>
    /// Graph optimization levels
    /// </summary>
    public enum GraphOptimizationLevel
    {
        None = 0,
        Basic = 1,
        Extended = 2,
        All = 99
    }

    /// <summary>
    /// Configuration for individual ML models
    /// </summary>
    public class ModelConfiguration
    {
        /// <summary>
        /// Gets or sets the model file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model version
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets input tensor specifications
        /// </summary>
        public List<TensorSpec> InputSpecs { get; set; } = new();

        /// <summary>
        /// Gets or sets output tensor specifications
        /// </summary>
        public List<TensorSpec> OutputSpecs { get; set; } = new();

        /// <summary>
        /// Gets or sets model-specific preprocessing configuration
        /// </summary>
        public PreprocessingConfig Preprocessing { get; set; } = new();

        /// <summary>
        /// Gets or sets model-specific postprocessing configuration
        /// </summary>
        public PostprocessingConfig Postprocessing { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this model supports batching
        /// </summary>
        public bool SupportsBatching { get; set; } = true;

        /// <summary>
        /// Gets or sets the preferred batch size for this model
        /// </summary>
        public int PreferredBatchSize { get; set; } = 16;

        /// <summary>
        /// Gets or sets model metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Tensor specification for model inputs/outputs
    /// </summary>
    public class TensorSpec
    {
        /// <summary>
        /// Gets or sets the tensor name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tensor shape (-1 for dynamic dimensions)
        /// </summary>
        public int[] Shape { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Gets or sets the tensor data type
        /// </summary>
        public TensorDataType DataType { get; set; } = TensorDataType.Float32;

        /// <summary>
        /// Gets or sets whether this tensor is optional
        /// </summary>
        public bool IsOptional { get; set; } = false;
    }

    /// <summary>
    /// Tensor data types
    /// </summary>
    public enum TensorDataType
    {
        Float32,
        Float16,
        Int32,
        Int64,
        Int8,
        UInt8,
        Bool,
        String
    }

    /// <summary>
    /// Preprocessing configuration
    /// </summary>
    public class PreprocessingConfig
    {
        /// <summary>
        /// Gets or sets whether to normalize inputs
        /// </summary>
        public bool Normalize { get; set; } = true;

        /// <summary>
        /// Gets or sets normalization mean values
        /// </summary>
        public float[] Mean { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Gets or sets normalization standard deviation values
        /// </summary>
        public float[] Std { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Gets or sets custom preprocessing pipeline
        /// </summary>
        public string CustomPipeline { get; set; } = string.Empty;
    }

    /// <summary>
    /// Postprocessing configuration
    /// </summary>
    public class PostprocessingConfig
    {
        /// <summary>
        /// Gets or sets the activation function to apply
        /// </summary>
        public string ActivationFunction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets confidence threshold
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets top-k results to return
        /// </summary>
        public int TopK { get; set; } = 10;

        /// <summary>
        /// Gets or sets custom postprocessing pipeline
        /// </summary>
        public string CustomPipeline { get; set; } = string.Empty;
    }

    /// <summary>
    /// CUDA memory arena configuration
    /// </summary>
    public class CudaMemoryArenaConfig
    {
        /// <summary>
        /// Gets or sets the initial chunk size in bytes
        /// </summary>
        public long InitialChunkSizeBytes { get; set; } = 1024 * 1024 * 256; // 256MB

        /// <summary>
        /// Gets or sets the maximum memory size in bytes
        /// </summary>
        public long MaxMemSizeBytes { get; set; } = 0; // 0 = unlimited

        /// <summary>
        /// Gets or sets the arena extend strategy
        /// </summary>
        public ArenaExtendStrategy ExtendStrategy { get; set; } = ArenaExtendStrategy.NextPowerOfTwo;

        /// <summary>
        /// Gets or sets whether to use CUDA unified memory
        /// </summary>
        public bool UseUnifiedMemory { get; set; } = false;
    }

    /// <summary>
    /// Arena extend strategies
    /// </summary>
    public enum ArenaExtendStrategy
    {
        NextPowerOfTwo,
        SameAsRequested
    }

    /// <summary>
    /// TensorRT configuration
    /// </summary>
    public class TensorRTConfig
    {
        /// <summary>
        /// Gets or sets the maximum workspace size in bytes
        /// </summary>
        public long MaxWorkspaceSize { get; set; } = 1L << 30; // 1GB

        /// <summary>
        /// Gets or sets whether to enable FP16 mode
        /// </summary>
        public bool EnableFP16 { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable INT8 mode
        /// </summary>
        public bool EnableINT8 { get; set; } = false;

        /// <summary>
        /// Gets or sets the calibration table name for INT8
        /// </summary>
        public string INT8CalibrationTableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to use native TensorRT calibration table
        /// </summary>
        public bool UseNativeTensorRTCalibrationTable { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum subgraph size for TensorRT
        /// </summary>
        public int MinSubgraphSize { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to force sequential engine build
        /// </summary>
        public bool ForceSequentialEngineBuild { get; set; } = false;

        /// <summary>
        /// Gets or sets the engine cache directory
        /// </summary>
        public string EngineCacheDir { get; set; } = "TensorRTCache";

        /// <summary>
        /// Gets or sets whether to enable engine caching
        /// </summary>
        public bool EnableEngineCaching { get; set; } = true;
    }
}