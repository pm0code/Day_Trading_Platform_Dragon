namespace TradingPlatform.DisplayManagement.Models;

/// <summary>
/// Detailed information about a system GPU for DRAGON trading platform monitor configuration
/// </summary>
public record GpuInfo
{
    /// <summary>
    /// GPU display name (e.g., "NVIDIA GeForce RTX 4080")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// GPU driver version
    /// </summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>
    /// Video memory in gigabytes
    /// </summary>
    public double VideoMemoryGB { get; init; }

    /// <summary>
    /// Maximum display outputs supported by this GPU
    /// </summary>
    public int MaxDisplayOutputs { get; init; }

    /// <summary>
    /// Whether this GPU is currently active and available
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// GPU vendor identifier (PNP Device ID)
    /// </summary>
    public string VendorId { get; init; } = string.Empty;

    /// <summary>
    /// GPU device identifier
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// When this GPU information was last detected
    /// </summary>
    public DateTime LastDetected { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Estimated GPU generation/performance tier
    /// </summary>
    public GpuPerformanceTier PerformanceTier { get; init; } = GpuPerformanceTier.Unknown;

    /// <summary>
    /// Whether this GPU supports hardware acceleration for trading applications
    /// </summary>
    public bool SupportsHardwareAcceleration { get; init; } = true;
}

/// <summary>
/// GPU performance assessment for trading workloads
/// </summary>
public record GpuPerformanceAssessment
{
    /// <summary>
    /// Overall performance rating for multi-monitor trading
    /// </summary>
    public PerformanceRating OverallRating { get; init; } = PerformanceRating.Poor;

    /// <summary>
    /// Recommended maximum number of monitors for optimal performance
    /// </summary>
    public int RecommendedMonitors { get; init; } = 1;

    /// <summary>
    /// Total video memory across all GPUs in GB
    /// </summary>
    public double TotalVideoMemoryGB { get; init; }

    /// <summary>
    /// Primary GPU name used for the assessment
    /// </summary>
    public string PrimaryGpuName { get; init; } = string.Empty;

    /// <summary>
    /// Description of trading workload support capability
    /// </summary>
    public string TradingWorkloadSupport { get; init; } = string.Empty;

    /// <summary>
    /// List of performance limitations or considerations
    /// </summary>
    public List<string> Limitations { get; init; } = new();

    /// <summary>
    /// Suggested optimal resolution per monitor
    /// </summary>
    public string OptimalResolutionSuggestion { get; init; } = string.Empty;

    /// <summary>
    /// Whether the current configuration supports ultra-low latency requirements
    /// </summary>
    public bool SupportsUltraLowLatency { get; init; }

    /// <summary>
    /// Assessment timestamp
    /// </summary>
    public DateTime AssessmentTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Validation result for a proposed monitor configuration
/// </summary>
public record MonitorConfigurationValidation
{
    /// <summary>
    /// Whether the configuration is fully supported by current hardware
    /// </summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>
    /// Number of monitors in the proposed configuration
    /// </summary>
    public int MonitorCount { get; init; }

    /// <summary>
    /// Hardware-recommended maximum monitors
    /// </summary>
    public int RecommendedMaximum { get; init; }

    /// <summary>
    /// Configuration warnings (performance concerns, etc.)
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Configuration errors (unsupported features, etc.)
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Estimated performance impact of this configuration
    /// </summary>
    public PerformanceImpact PerformanceImpact { get; init; } = PerformanceImpact.None;

    /// <summary>
    /// Suggestions for optimizing the configuration
    /// </summary>
    public List<string> OptimizationSuggestions { get; init; } = new();

    /// <summary>
    /// Whether this configuration is recommended for day trading
    /// </summary>
    public bool RecommendedForTrading { get; init; } = true;

    /// <summary>
    /// Validation timestamp
    /// </summary>
    public DateTime ValidationTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Performance rating scale for GPU capabilities
/// </summary>
public enum PerformanceRating
{
    /// <summary>
    /// Poor performance - limited multi-monitor capability
    /// </summary>
    Poor = 1,

    /// <summary>
    /// Fair performance - adequate for basic trading setups
    /// </summary>
    Fair = 2,

    /// <summary>
    /// Good performance - suitable for professional trading
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent performance - optimal for high-frequency trading
    /// </summary>
    Excellent = 4
}

/// <summary>
/// GPU performance tier classification
/// </summary>
public enum GpuPerformanceTier
{
    /// <summary>
    /// Unknown or unclassified GPU
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Entry-level integrated graphics
    /// </summary>
    IntegratedBasic = 1,

    /// <summary>
    /// Advanced integrated graphics (Intel Iris, AMD Vega)
    /// </summary>
    IntegratedAdvanced = 2,

    /// <summary>
    /// Entry-level dedicated GPU
    /// </summary>
    DedicatedEntry = 3,

    /// <summary>
    /// Mid-range dedicated GPU
    /// </summary>
    DedicatedMidRange = 4,

    /// <summary>
    /// High-end dedicated GPU
    /// </summary>
    DedicatedHighEnd = 5,

    /// <summary>
    /// Enthusiast/Professional GPU
    /// </summary>
    Enthusiast = 6,

    /// <summary>
    /// Professional workstation GPU
    /// </summary>
    Professional = 7
}

/// <summary>
/// Performance impact assessment for monitor configurations
/// </summary>
public enum PerformanceImpact
{
    /// <summary>
    /// No expected performance impact
    /// </summary>
    None = 0,

    /// <summary>
    /// Minimal performance impact
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Moderate performance impact - may affect trading application responsiveness
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Significant performance impact - may cause lag during trading operations
    /// </summary>
    Significant = 3,

    /// <summary>
    /// Severe performance impact - not recommended for trading
    /// </summary>
    Severe = 4
}

/// <summary>
/// Monitor selection recommendation for DRAGON trading platform
/// </summary>
public record MonitorSelectionRecommendation
{
    /// <summary>
    /// Recommended number of monitors for optimal trading performance
    /// </summary>
    public int RecommendedMonitorCount { get; init; } = 1;

    /// <summary>
    /// Maximum supported monitors by current hardware
    /// </summary>
    public int MaximumSupportedMonitors { get; init; } = 1;

    /// <summary>
    /// Optimal monitor resolution for the recommended configuration
    /// </summary>
    public Resolution OptimalResolution { get; init; } = new(1920, 1080);

    /// <summary>
    /// Suggested trading screen layout for the recommended monitor count
    /// </summary>
    public List<TradingScreenLayout> SuggestedLayout { get; init; } = new();

    /// <summary>
    /// Performance expectations for this recommendation
    /// </summary>
    public string PerformanceExpectation { get; init; } = string.Empty;

    /// <summary>
    /// Hardware requirements to achieve this recommendation
    /// </summary>
    public List<string> HardwareRequirements { get; init; } = new();

    /// <summary>
    /// Alternative configurations if current hardware is insufficient
    /// </summary>
    public List<string> AlternativeConfigurations { get; init; } = new();
}

/// <summary>
/// Trading screen layout configuration
/// </summary>
public record TradingScreenLayout
{
    /// <summary>
    /// Type of trading screen
    /// </summary>
    public TradingScreenType ScreenType { get; init; }

    /// <summary>
    /// Monitor index (0-based) for this screen
    /// </summary>
    public int MonitorIndex { get; init; }

    /// <summary>
    /// Priority level for this screen (higher = more important)
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Description of what this screen displays
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Whether this screen requires high refresh rate
    /// </summary>
    public bool RequiresHighRefreshRate { get; init; }

    /// <summary>
    /// Minimum resolution recommended for this screen type
    /// </summary>
    public Resolution MinimumResolution { get; init; } = Resolution.FullHD;
}

/// <summary>
/// Resolution specification
/// </summary>
public record Resolution(int Width, int Height)
{
    /// <summary>
    /// Common resolution display name
    /// </summary>
    public string DisplayName => $"{Width}x{Height}";

    /// <summary>
    /// Total pixel count
    /// </summary>
    public long TotalPixels => (long)Width * Height;

    /// <summary>
    /// Aspect ratio
    /// </summary>
    public double AspectRatio => Width / (double)Height;

    /// <summary>
    /// Whether this is a high-resolution display
    /// </summary>
    public bool IsHighResolution => TotalPixels >= 2073600; // 1440p+

    /// <summary>
    /// Common resolution names
    /// </summary>
    public static readonly Resolution HD = new(1280, 720);
    public static readonly Resolution FullHD = new(1920, 1080);
    public static readonly Resolution QHD = new(2560, 1440);
    public static readonly Resolution UHD4K = new(3840, 2160);
    public static readonly Resolution UHD5K = new(5120, 2880);
    public static readonly Resolution UHD8K = new(7680, 4320);
}
