using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TradingPlatform.AlternativeData.Interfaces;
using TradingPlatform.AlternativeData.Models;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.CostManagement.Services;
using TradingPlatform.ML.Services;

namespace TradingPlatform.AlternativeData.Services;

public class AlternativeDataHub : CanonicalServiceBase, IAlternativeDataHub
{
    private readonly ConcurrentDictionary<string, IAlternativeDataProvider> _providers;
    private readonly ConcurrentDictionary<string, IAIModelService> _aiModels;
    private readonly DataSourceCostTracker _costTracker;
    private readonly InteractiveCostDashboard _costDashboard;
    private readonly MLInferenceService _mlInferenceService;
    private readonly AlternativeDataConfiguration _config;
    private readonly IDataProcessingPipeline _processingPipeline;
    private readonly ISignalValidationService _signalValidation;
    private readonly ConcurrentDictionary<string, AlternativeDataSignal> _activeSignals;
    private readonly ConcurrentDictionary<string, DateTime> _providerLastActivity;

    public AlternativeDataHub(
        ITradingLogger logger,
        IOptions<AlternativeDataConfiguration> config,
        DataSourceCostTracker costTracker,
        InteractiveCostDashboard costDashboard,
        MLInferenceService mlInferenceService,
        IDataProcessingPipeline processingPipeline,
        ISignalValidationService signalValidation)
        : base(logger, "ALTERNATIVE_DATA_HUB")
    {
        _config = config.Value;
        _costTracker = costTracker;
        _costDashboard = costDashboard;
        _mlInferenceService = mlInferenceService;
        _processingPipeline = processingPipeline;
        _signalValidation = signalValidation;
        _providers = new ConcurrentDictionary<string, IAlternativeDataProvider>();
        _aiModels = new ConcurrentDictionary<string, IAIModelService>();
        _activeSignals = new ConcurrentDictionary<string, AlternativeDataSignal>();
        _providerLastActivity = new ConcurrentDictionary<string, DateTime>();
    }

    protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        using var operation = StartOperation("OnInitializeAsync");

        try
        {
            // Initialize AI models based on open-source models document recommendations
            await InitializeAIModelsAsync(cancellationToken);
            
            // Initialize cost management
            await InitializeCostManagementAsync(cancellationToken);
            
            // Start background services
            _ = Task.Run(() => SignalMonitoringLoop(cancellationToken), cancellationToken);
            _ = Task.Run(() => CostOptimizationLoop(cancellationToken), cancellationToken);
            _ = Task.Run(() => ProviderHealthMonitoringLoop(cancellationToken), cancellationToken);

            LogInfo("Alternative Data Hub initialized successfully", new 
            { 
                providersRegistered = _providers.Count,
                aiModelsLoaded = _aiModels.Count
            });

            operation.SetSuccess();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            LogError("Failed to initialize Alternative Data Hub", ex);
            return TradingResult<bool>.Failure($"Initialization failed: {ex.Message}");
        }
    }

    public async Task<TradingResult<AlternativeDataResponse>> RequestDataAsync(
        AlternativeDataRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry(new { request.RequestId, request.DataType });

        try
        {
            // Validate request and check budget constraints
            var validationResult = await ValidateRequestAsync(request, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return TradingResult<AlternativeDataResponse>.Failure(validationResult.ErrorMessage!);
            }

            // Check cost dashboard for budget status
            var costStatus = await _costDashboard.GetDataSourceStatusAsync(request.DataType.ToString(), cancellationToken);
            if (costStatus.IsSuccess && costStatus.Data?.Status == "suspended")
            {
                return TradingResult<AlternativeDataResponse>.Failure("Data source is currently suspended due to budget constraints");
            }

            // Find appropriate providers for the data type
            var eligibleProviders = _providers.Values
                .Where(p => p.DataType == request.DataType && p.IsActive)
                .ToList();

            if (!eligibleProviders.Any())
            {
                return TradingResult<AlternativeDataResponse>.Failure($"No active providers found for data type: {request.DataType}");
            }

            // Estimate costs across providers and select optimal one
            var providerCosts = new Dictionary<string, decimal>();
            foreach (var provider in eligibleProviders)
            {
                var costEstimate = await provider.EstimateCostAsync(request, cancellationToken);
                if (costEstimate.IsSuccess)
                {
                    providerCosts[provider.ProviderId] = costEstimate.Data!;
                }
            }

            var selectedProvider = SelectOptimalProvider(eligibleProviders, providerCosts, request);
            if (selectedProvider == null)
            {
                return TradingResult<AlternativeDataResponse>.Failure("No suitable provider found within budget constraints");
            }

            // Execute request through selected provider
            var response = await selectedProvider.GetDataAsync(request, cancellationToken);
            
            if (response.IsSuccess)
            {
                // Validate signals using multiple quality checks
                var validatedSignals = await ValidateSignalsAsync(response.Data!.Signals, cancellationToken);
                
                // Store active signals for monitoring
                foreach (var signal in validatedSignals)
                {
                    _activeSignals.TryAdd(signal.SignalId, signal);
                }

                // Update provider activity tracking
                _providerLastActivity.AddOrUpdate(selectedProvider.ProviderId, DateTime.UtcNow, (_, __) => DateTime.UtcNow);

                // Enhance response with cost dashboard insights
                response.Data = response.Data! with 
                { 
                    Signals = validatedSignals,
                    Metadata = response.Data!.Metadata.Concat(new[]
                    {
                        new KeyValuePair<string, object>("selectedProvider", selectedProvider.ProviderId),
                        new KeyValuePair<string, object>("estimatedCost", providerCosts.GetValueOrDefault(selectedProvider.ProviderId, 0m)),
                        new KeyValuePair<string, object>("validatedSignalCount", validatedSignals.Count),
                        new KeyValuePair<string, object>("aiModelsUsed", GetUsedAIModels(request.DataType))
                    }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };

                LogInfo("Data request completed successfully", new 
                { 
                    request.RequestId,
                    selectedProvider = selectedProvider.ProviderId,
                    signalCount = validatedSignals.Count,
                    cost = response.Data!.ProcessingCost
                });
            }

            operation.SetSuccess();
            return response;
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            LogError("Failed to process data request", ex, new { request.RequestId });
            return TradingResult<AlternativeDataResponse>.Failure($"Request processing failed: {ex.Message}");
        }
    }

    public async Task<TradingResult<List<AlternativeDataSignal>>> GetActiveSignalsAsync(
        List<string>? symbols = null,
        List<AlternativeDataType>? dataTypes = null,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetActiveSignalsAsync");

        try
        {
            var activeSignals = _activeSignals.Values.Where(signal =>
            {
                var symbolMatch = symbols == null || symbols.Contains(signal.Symbol);
                var typeMatch = dataTypes == null || dataTypes.Contains(signal.DataType);
                var timeMatch = signal.Timestamp > DateTime.UtcNow.AddHours(-24); // Active for 24 hours
                
                return symbolMatch && typeMatch && timeMatch;
            }).ToList();

            // Enrich signals with real-time confidence updates using ML models
            foreach (var signal in activeSignals)
            {
                var enhancedSignal = await EnhanceSignalWithMLInsights(signal, cancellationToken);
                if (enhancedSignal.IsSuccess)
                {
                    _activeSignals.TryUpdate(signal.SignalId, enhancedSignal.Data!, signal);
                }
            }

            operation.SetSuccess();
            return TradingResult<List<AlternativeDataSignal>>.Success(activeSignals);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<List<AlternativeDataSignal>>.Failure($"Failed to get active signals: {ex.Message}");
        }
    }

    public async Task<TradingResult<AlternativeDataMetrics>> GetMetricsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetMetricsAsync");

        try
        {
            var start = startTime ?? DateTime.UtcNow.AddDays(-1);
            var end = endTime ?? DateTime.UtcNow;

            // Aggregate metrics from cost tracker and providers
            var requestsByDataType = new Dictionary<string, int>();
            var costsByProvider = new Dictionary<string, decimal>();
            var qualityScoresByProvider = new Dictionary<string, decimal>();
            var processingTimesByModel = new Dictionary<string, TimeSpan>();

            // Get cost data from cost tracker
            var costData = await _costTracker.GetCostSummaryAsync(start, end);
            if (costData.IsSuccess && costData.Data != null)
            {
                foreach (var cost in costData.Data.DataSourceCosts)
                {
                    costsByProvider[cost.Key] = cost.Value.TotalCost;
                }
            }

            // Get provider health data
            foreach (var provider in _providers.Values)
            {
                var health = await provider.GetHealthAsync(cancellationToken);
                if (health.IsSuccess)
                {
                    qualityScoresByProvider[provider.ProviderId] = health.Data!.SuccessRate;
                }
            }

            // Count requests by data type from active signals
            foreach (var signal in _activeSignals.Values.Where(s => s.Timestamp >= start && s.Timestamp <= end))
            {
                var dataTypeStr = signal.DataType.ToString();
                requestsByDataType[dataTypeStr] = requestsByDataType.GetValueOrDefault(dataTypeStr, 0) + 1;
            }

            // Get AI model performance metrics
            foreach (var model in _aiModels.Values)
            {
                processingTimesByModel[model.ModelName] = TimeSpan.FromMilliseconds(100); // Mock data
            }

            var metrics = new AlternativeDataMetrics
            {
                MetricsTime = DateTime.UtcNow,
                RequestsByDataType = requestsByDataType,
                CostsByProvider = costsByProvider,
                QualityScoresByProvider = qualityScoresByProvider,
                ProcessingTimesByModel = processingTimesByModel,
                TotalDailyCost = costsByProvider.Values.Sum(),
                TotalSignalsGenerated = _activeSignals.Count,
                AverageSignalConfidence = _activeSignals.Values.Any() ? _activeSignals.Values.Average(s => s.Confidence) : 0,
                GPUUtilizationPercentage = await GetGPUUtilizationAsync(cancellationToken)
            };

            operation.SetSuccess();
            return TradingResult<AlternativeDataMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<AlternativeDataMetrics>.Failure($"Failed to get metrics: {ex.Message}");
        }
    }

    public async Task<TradingResult<List<DataProviderHealth>>> GetProviderHealthAsync(
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetProviderHealthAsync");

        try
        {
            var healthStatuses = new List<DataProviderHealth>();

            var healthTasks = _providers.Values.Select(async provider =>
            {
                try
                {
                    var health = await provider.GetHealthAsync(cancellationToken);
                    return health.IsSuccess ? health.Data : null;
                }
                catch (Exception ex)
                {
                    LogWarning($"Health check failed for provider {provider.ProviderId}", ex);
                    return new DataProviderHealth
                    {
                        ProviderId = provider.ProviderId,
                        IsHealthy = false,
                        LastCheckTime = DateTime.UtcNow,
                        ResponseTime = TimeSpan.MaxValue,
                        RequestsInLastHour = 0,
                        FailuresInLastHour = 1,
                        SuccessRate = 0,
                        AverageCost = 0,
                        HealthIssue = ex.Message
                    };
                }
            });

            var results = await Task.WhenAll(healthTasks);
            healthStatuses.AddRange(results.Where(h => h != null)!);

            operation.SetSuccess();
            return TradingResult<List<DataProviderHealth>>.Success(healthStatuses);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<List<DataProviderHealth>>.Failure($"Failed to get provider health: {ex.Message}");
        }
    }

    public async Task<TradingResult<bool>> RegisterProviderAsync(
        IAlternativeDataProvider provider,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("RegisterProviderAsync", new { provider.ProviderId });

        try
        {
            // Validate provider configuration
            var configValidation = await provider.ValidateConfigurationAsync(cancellationToken);
            if (!configValidation.IsSuccess)
            {
                return TradingResult<bool>.Failure($"Provider configuration validation failed: {configValidation.ErrorMessage}");
            }

            // Register with cost tracker
            await _costTracker.RegisterDataSourceAsync(provider.ProviderId, new CostManagement.Models.DataSourceBudget
            {
                MonthlyLimit = _config.Cost.MonthlyBudget / _providers.Count, // Distribute budget evenly
                AlertThreshold = _config.Cost.CostAlertThreshold,
                HardLimit = _config.Cost.MonthlyBudget / _providers.Count * 1.2m
            });

            _providers.TryAdd(provider.ProviderId, provider);
            _providerLastActivity.TryAdd(provider.ProviderId, DateTime.UtcNow);

            LogInfo("Provider registered successfully", new { provider.ProviderId, provider.DataType });

            operation.SetSuccess();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<bool>.Failure($"Failed to register provider: {ex.Message}");
        }
    }

    public async Task<TradingResult<bool>> UnregisterProviderAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("UnregisterProviderAsync", new { providerId });

        try
        {
            var removed = _providers.TryRemove(providerId, out var provider);
            if (removed && provider != null)
            {
                _providerLastActivity.TryRemove(providerId, out _);
                
                // Clean up related signals
                var signalsToRemove = _activeSignals.Values
                    .Where(s => s.Source == providerId)
                    .Select(s => s.SignalId)
                    .ToList();

                foreach (var signalId in signalsToRemove)
                {
                    _activeSignals.TryRemove(signalId, out _);
                }

                LogInfo("Provider unregistered successfully", new { providerId, signalsRemoved = signalsToRemove.Count });
            }

            operation.SetSuccess();
            return TradingResult<bool>.Success(removed);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<bool>.Failure($"Failed to unregister provider: {ex.Message}");
        }
    }

    private async Task InitializeAIModelsAsync(CancellationToken cancellationToken)
    {
        // Based on the open-source models document, initialize key models for different tasks
        var modelConfigs = new[]
        {
            // Time series forecasting models
            new { Name = "Prophet", Type = "forecasting", Priority = 1 },
            new { Name = "NeuralProphet", Type = "forecasting", Priority = 2 },
            new { Name = "Chronos", Type = "forecasting", Priority = 3 },
            
            // Deep learning models
            new { Name = "N-BEATS", Type = "deep_learning", Priority = 1 },
            new { Name = "TFT", Type = "transformer", Priority = 2 },
            
            // Reinforcement learning
            new { Name = "FinRL", Type = "rl_trading", Priority = 1 },
            new { Name = "TensorTrade", Type = "rl_framework", Priority = 2 },
            
            // AutoML
            new { Name = "AutoGluon-TimeSeries", Type = "automl", Priority = 1 }
        };

        foreach (var modelConfig in modelConfigs.OrderBy(m => m.Priority))
        {
            try
            {
                if (_config.AIModels.TryGetValue(modelConfig.Name, out var config))
                {
                    var modelService = CreateAIModelService(modelConfig.Name, modelConfig.Type, config);
                    if (modelService != null)
                    {
                        var initResult = await modelService.InitializeAsync(config, cancellationToken);
                        if (initResult.IsSuccess)
                        {
                            _aiModels.TryAdd(modelConfig.Name, modelService);
                            LogInfo($"AI model {modelConfig.Name} initialized successfully");
                        }
                        else
                        {
                            LogWarning($"Failed to initialize AI model {modelConfig.Name}: {initResult.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Exception during AI model {modelConfig.Name} initialization", ex);
            }
        }
    }

    private IAIModelService? CreateAIModelService(string modelName, string modelType, AIModelConfig config)
    {
        // Factory method to create appropriate AI model services
        // In a real implementation, this would instantiate the actual model services
        return modelType switch
        {
            "forecasting" when modelName == "Prophet" => null, // Would return new ProphetTimeSeriesService(),
            "forecasting" when modelName == "NeuralProphet" => null, // Would return new NeuralProphetService(),
            "rl_trading" when modelName == "FinRL" => null, // Would return new FinRLTradingService(),
            _ => null
        };
    }

    private async Task InitializeCostManagementAsync(CancellationToken cancellationToken)
    {
        // Initialize cost tracking configuration for each data type
        var dataTypes = Enum.GetValues<AlternativeDataType>();
        
        foreach (var dataType in dataTypes)
        {
            await _costTracker.RegisterDataSourceAsync(dataType.ToString(), new CostManagement.Models.DataSourceBudget
            {
                MonthlyLimit = _config.Cost.MonthlyBudget / dataTypes.Length,
                AlertThreshold = _config.Cost.CostAlertThreshold,
                HardLimit = _config.Cost.MonthlyBudget / dataTypes.Length * 1.2m
            });
        }
    }

    private async Task<TradingResult<bool>> ValidateRequestAsync(AlternativeDataRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RequestId))
            return TradingResult<bool>.Failure("Request ID is required");

        if (!request.Symbols.Any())
            return TradingResult<bool>.Failure("At least one symbol is required");

        if (request.EndTime <= request.StartTime)
            return TradingResult<bool>.Failure("End time must be after start time");

        // Check budget constraints
        if (request.MaxCost.HasValue)
        {
            var providerCosts = await EstimateRequestCostAsync(request, cancellationToken);
            if (providerCosts > request.MaxCost.Value)
            {
                return TradingResult<bool>.Failure($"Estimated cost ${providerCosts:F2} exceeds maximum budget ${request.MaxCost:F2}");
            }
        }

        return TradingResult<bool>.Success(true);
    }

    private IAlternativeDataProvider? SelectOptimalProvider(
        List<IAlternativeDataProvider> eligibleProviders,
        Dictionary<string, decimal> providerCosts,
        AlternativeDataRequest request)
    {
        // Multi-criteria selection based on cost, quality, and availability
        return eligibleProviders
            .Where(p => !request.MaxCost.HasValue || providerCosts.GetValueOrDefault(p.ProviderId, decimal.MaxValue) <= request.MaxCost.Value)
            .OrderBy(p => providerCosts.GetValueOrDefault(p.ProviderId, decimal.MaxValue))
            .ThenByDescending(p => GetProviderQualityScore(p.ProviderId))
            .FirstOrDefault();
    }

    private decimal GetProviderQualityScore(string providerId)
    {
        // Mock quality scoring - in reality would use historical performance data
        return providerId switch
        {
            "satellite_provider" => 0.85m,
            "social_media_provider" => 0.78m,
            _ => 0.5m
        };
    }

    private async Task<List<AlternativeDataSignal>> ValidateSignalsAsync(
        List<AlternativeDataSignal> signals,
        CancellationToken cancellationToken)
    {
        var validatedSignals = new List<AlternativeDataSignal>();

        foreach (var signal in signals)
        {
            var validation = await _signalValidation.ValidateSignalAsync(signal, cancellationToken);
            if (validation.IsSuccess && validation.Data!.IsValid)
            {
                validatedSignals.Add(signal);
            }
        }

        return validatedSignals;
    }

    private async Task<TradingResult<AlternativeDataSignal>> EnhanceSignalWithMLInsights(
        AlternativeDataSignal signal,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use ML inference to enhance signal confidence and predictions
            var inputData = PrepareSignalDataForML(signal);
            var prediction = await _mlInferenceService.PredictAsync("signal_enhancement", inputData, new[] { 1, inputData.Length });

            if (prediction.IsSuccess && prediction.Data != null)
            {
                var enhancedConfidence = Math.Min(1.0m, signal.Confidence * (decimal)prediction.Data.Confidence);
                var enhancedSignal = signal with { Confidence = enhancedConfidence };
                
                return TradingResult<AlternativeDataSignal>.Success(enhancedSignal);
            }

            return TradingResult<AlternativeDataSignal>.Success(signal);
        }
        catch (Exception ex)
        {
            LogWarning("Failed to enhance signal with ML insights", ex, new { signal.SignalId });
            return TradingResult<AlternativeDataSignal>.Success(signal);
        }
    }

    private float[] PrepareSignalDataForML(AlternativeDataSignal signal)
    {
        // Convert signal data to ML input format
        return new[]
        {
            (float)signal.Confidence,
            (float)signal.SignalStrength,
            (float)(signal.PredictedPriceImpact ?? 0),
            (float)(DateTime.UtcNow - signal.Timestamp).TotalMinutes,
            (float)signal.DataType
        };
    }

    private async Task<decimal> EstimateRequestCostAsync(AlternativeDataRequest request, CancellationToken cancellationToken)
    {
        var totalCost = 0m;

        foreach (var provider in _providers.Values.Where(p => p.DataType == request.DataType))
        {
            var cost = await provider.EstimateCostAsync(request, cancellationToken);
            if (cost.IsSuccess)
            {
                totalCost = Math.Min(totalCost == 0 ? decimal.MaxValue : totalCost, cost.Data!);
            }
        }

        return totalCost;
    }

    private List<string> GetUsedAIModels(AlternativeDataType dataType)
    {
        return dataType switch
        {
            AlternativeDataType.SatelliteImagery => new[] { "Prophet", "NeuralProphet" }.ToList(),
            AlternativeDataType.SocialMediaSentiment => new[] { "FinRL", "Catalyst NLP" }.ToList(),
            AlternativeDataType.EconomicIndicator => new[] { "Chronos", "AutoGluon-TimeSeries" }.ToList(),
            _ => new[] { "ONNX Runtime" }.ToList()
        };
    }

    private async Task<int> GetGPUUtilizationAsync(CancellationToken cancellationToken)
    {
        // Mock GPU utilization - in reality would query actual GPU metrics
        await Task.Delay(10, cancellationToken);
        return new Random().Next(20, 80);
    }

    private async Task SignalMonitoringLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Remove expired signals
                var expiredSignals = _activeSignals.Values
                    .Where(s => s.Timestamp < DateTime.UtcNow.AddHours(-24))
                    .Select(s => s.SignalId)
                    .ToList();

                foreach (var signalId in expiredSignals)
                {
                    _activeSignals.TryRemove(signalId, out _);
                }

                if (expiredSignals.Any())
                {
                    LogInfo($"Removed {expiredSignals.Count} expired signals");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
            }
            catch (Exception ex)
            {
                LogError("Error in signal monitoring loop", ex);
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }
    }

    private async Task CostOptimizationLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check for cost optimization opportunities
                var dashboard = await _costDashboard.GetInteractiveDashboardAsync(TimeSpan.FromDays(30));
                
                if (dashboard.IsSuccess && dashboard.Data != null)
                {
                    foreach (var (dataSource, controls) in dashboard.Data.DataSourceControls)
                    {
                        var recommendedAction = controls.ActionButtons.FirstOrDefault(b => b.IsRecommended);
                        if (recommendedAction?.ActionId == "optimize")
                        {
                            LogInfo($"Auto-optimization recommended for {dataSource}", new { action = recommendedAction.Description });
                            // In production, could automatically execute optimization
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            }
            catch (Exception ex)
            {
                LogError("Error in cost optimization loop", ex);
                await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);
            }
        }
    }

    private async Task ProviderHealthMonitoringLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var healthResults = await GetProviderHealthAsync(cancellationToken);
                
                if (healthResults.IsSuccess)
                {
                    var unhealthyProviders = healthResults.Data!.Where(h => !h.IsHealthy).ToList();
                    
                    foreach (var unhealthyProvider in unhealthyProviders)
                    {
                        LogWarning($"Provider {unhealthyProvider.ProviderId} is unhealthy", 
                            new { issue = unhealthyProvider.HealthIssue, successRate = unhealthyProvider.SuccessRate });
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);
            }
            catch (Exception ex)
            {
                LogError("Error in provider health monitoring loop", ex);
                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
            }
        }
    }
}