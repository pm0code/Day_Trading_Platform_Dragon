// File: TradingPlatform.Core.Canonical\CanonicalSettingsService.cs

using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Foundation;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical implementation of settings management with validation, hot-reload support,
    /// and comprehensive monitoring of configuration changes.
    /// </summary>
    public class CanonicalSettingsService : CanonicalServiceBase, ISettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<TradingPlatformSettings> _optionsMonitor;
        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
        private readonly Dictionary<string, object> _settingsCache = new();
        private readonly List<IDisposable> _changeTokenRegistrations = new();
        
        private TradingPlatformSettings _currentSettings;
        private long _configurationReloads;
        private long _settingsValidations;
        private long _settingsUpdates;
        private DateTime _lastReloadTime = DateTime.UtcNow;

        public CanonicalSettingsService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IOptionsMonitor<TradingPlatformSettings> optionsMonitor)
            : base(serviceProvider.GetRequiredService<ITradingLogger>(), "SettingsService")
        {
            _configuration = configuration;
            _optionsMonitor = optionsMonitor;
            _currentSettings = _optionsMonitor.CurrentValue;

            // Log entry into constructor
            LogInfo("Initializing CanonicalSettingsService");
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("OnInitializeAsync - Entry");
            
            try
            {
                // Base initialization is handled automatically

                // Validate initial settings
                var validationResult = await ValidateSettingsAsync(_currentSettings);
                if (!validationResult.IsSuccess)
                {
                    LogError("Initial settings validation failed", 
                        null,
                        "Settings validation",
                        "Service may not function correctly",
                        "Check settings configuration",
                        new { Errors = validationResult.Error?.Message });
                }

                // Register for configuration changes
                var changeToken = _optionsMonitor.OnChange(async (settings, name) =>
                {
                    await HandleSettingsChangeAsync(settings, name);
                });
                _changeTokenRegistrations.Add(changeToken);

                // Load API key validation results
                await ValidateApiKeysAsync();

                LogInfo("Settings service initialized successfully",
                    new 
                    { 
                        FinnhubEnabled = _currentSettings.Api.Finnhub.Enabled,
                        AlphaVantageEnabled = _currentSettings.Api.AlphaVantage.Enabled,
                        CacheEnabled = _currentSettings.Cache.Enabled
                    });
            }
            catch (Exception ex)
            {
                LogError("InitializeServiceAsync failed", ex);
                throw;
            }
            finally
            {
                LogInfo("OnInitializeAsync - Exit");
            }
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("OnStartAsync - Entry");
            
            // Settings service doesn't have any specific start logic
            await Task.CompletedTask;
            
            LogInfo("OnStartAsync - Exit");
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("OnStopAsync - Entry");
            
            try
            {
                // Dispose of change token registrations
                foreach (var registration in _changeTokenRegistrations)
                {
                    registration?.Dispose();
                }
                _changeTokenRegistrations.Clear();
                
                await Task.CompletedTask;
            }
            finally
            {
                LogInfo("OnStopAsync - Exit");
            }
        }

        /// <summary>
        /// Get current settings with monitoring
        /// </summary>
        public async Task<TradingPlatformSettings> GetSettingsAsync()
        {
            LogDebug("GetSettingsAsync - Entry");
            
            try
            {
                await _updateSemaphore.WaitAsync();
                try
                {
                    UpdateMetric("SettingsAccess", 1);
                    return _currentSettings;
                }
                finally
                {
                    _updateSemaphore.Release();
                    LogDebug("GetSettingsAsync - Exit");
                }
            }
            catch (Exception ex)
            {
                LogError("Error getting settings", ex);
                return new TradingPlatformSettings();
            }
        }

        /// <summary>
        /// Get specific setting value with type safety
        /// </summary>
        public async Task<T> GetSettingAsync<T>(string key, T defaultValue = default!)
        {
            LogDebug($"GetSettingAsync<{typeof(T).Name}> - Entry", new { Key = key });
            
            try
            {
                await _updateSemaphore.WaitAsync();
                try
                {
                    if (_settingsCache.TryGetValue(key, out var cachedValue) && cachedValue is T typedValue)
                    {
                        UpdateMetric("SettingsCacheHit", 1);
                        return typedValue;
                    }

                    var value = _configuration.GetValue<T>(key, defaultValue);
                    _settingsCache[key] = value!;
                    
                    UpdateMetric("SettingsCacheMiss", 1);
                    
                    LogDebug($"Retrieved setting: {key} = {value}");
                    
                    return value;
                }
                finally
                {
                    _updateSemaphore.Release();
                    LogDebug($"GetSettingAsync<{typeof(T).Name}> - Exit");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting setting {key}", ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Update settings with validation and monitoring
        /// </summary>
        public async Task<TradingResult> UpdateSettingsAsync(TradingPlatformSettings newSettings)
        {
            LogInfo("UpdateSettingsAsync - Entry");
            
            try
            {
                await _updateSemaphore.WaitAsync();
                try
                {
                    // Validate new settings
                    var validationResult = await ValidateSettingsAsync(newSettings);
                    if (!validationResult.IsSuccess)
                    {
                        LogWarning("Settings validation failed", 
                            "Settings update rejected",
                            "Review validation errors",
                            new { Errors = validationResult.Error?.Message });
                        return validationResult;
                    }

                    // Create backup of current settings
                    var previousSettings = JsonSerializer.Serialize(_currentSettings);

                    // Update settings
                    _currentSettings = newSettings;
                    _settingsCache.Clear();
                    
                    Interlocked.Increment(ref _settingsUpdates);
                    _lastReloadTime = DateTime.UtcNow;

                    // Log configuration change
                    LogInfo("Settings updated successfully",
                        new
                        {
                            PreviousSettings = previousSettings,
                            NewSettings = JsonSerializer.Serialize(newSettings),
                            UpdateCount = _settingsUpdates
                        });

                    // Validate API keys with new settings
                    await ValidateApiKeysAsync();

                    // Publish settings change event
                    await PublishSettingsChangeEventAsync();

                    UpdateMetric("SettingsUpdates", _settingsUpdates);

                    return TradingResult.Success();
                }
                finally
                {
                    _updateSemaphore.Release();
                    LogInfo("UpdateSettingsAsync - Exit");
                }
            }
            catch (Exception ex)
            {
                LogError("Error updating settings", ex);
                return TradingResult.Failure(new TradingError("UPDATE_FAILED", $"Settings update failed: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Validate settings comprehensively
        /// </summary>
        public async Task<TradingResult> ValidateSettingsAsync(TradingPlatformSettings settings)
        {
            LogDebug("ValidateSettingsAsync - Entry");
            
            return await Task.Run(() =>
            {
                try
                {
                    var errors = new List<string>();

                    // Validate API settings
                    if (settings.Api.Finnhub.Enabled && string.IsNullOrWhiteSpace(settings.Api.Finnhub.ApiKey))
                    {
                        errors.Add("Finnhub API key is required when enabled");
                    }

                    if (settings.Api.AlphaVantage.Enabled && string.IsNullOrWhiteSpace(settings.Api.AlphaVantage.ApiKey))
                    {
                        errors.Add("AlphaVantage API key is required when enabled");
                    }

                    // Validate rate limits
                    if (settings.Api.Finnhub.RateLimitPerMinute <= 0)
                    {
                        errors.Add("Finnhub rate limit must be positive");
                    }

                    if (settings.Api.AlphaVantage.RateLimitPerMinute <= 0)
                    {
                        errors.Add("AlphaVantage rate limit must be positive");
                    }

                    // Validate risk settings
                    if (settings.Risk.MaxPositionSizePercent <= 0 || settings.Risk.MaxPositionSizePercent > 1)
                    {
                        errors.Add("Max position size must be between 0 and 100%");
                    }

                    if (settings.Risk.MaxDrawdownPercent <= 0 || settings.Risk.MaxDrawdownPercent > 1)
                    {
                        errors.Add("Max drawdown must be between 0 and 100%");
                    }

                    // Validate screening settings
                    if (settings.Screening.DefaultMinPrice < 0)
                    {
                        errors.Add("Minimum price cannot be negative");
                    }

                    if (settings.Screening.DefaultMaxPrice <= settings.Screening.DefaultMinPrice)
                    {
                        errors.Add("Maximum price must be greater than minimum price");
                    }

                    Interlocked.Increment(ref _settingsValidations);
                    UpdateMetric("SettingsValidations", _settingsValidations);

                    if (errors.Any())
                    {
                        LogWarning("Settings validation found errors", 
                            "Invalid settings detected",
                            "Fix validation errors",
                            new { ErrorCount = errors.Count, Errors = errors });
                        return TradingResult.Failure(new TradingError("VALIDATION_FAILED", $"Validation errors: {string.Join("; ", errors)}"));
                    }

                    LogDebug("Settings validation passed");
                    return TradingResult.Success();
                }
                finally
                {
                    LogDebug("ValidateSettingsAsync - Exit");
                }
            });
        }

        /// <summary>
        /// Reload settings from configuration
        /// </summary>
        public async Task<TradingResult> ReloadSettingsAsync()
        {
            LogInfo("ReloadSettingsAsync - Entry");
            
            try
            {
                await _updateSemaphore.WaitAsync();
                try
                {
                    _currentSettings = _optionsMonitor.CurrentValue;
                    _settingsCache.Clear();
                    
                    Interlocked.Increment(ref _configurationReloads);
                    _lastReloadTime = DateTime.UtcNow;

                    var validationResult = await ValidateSettingsAsync(_currentSettings);
                    
                    LogInfo("Settings reloaded",
                        new 
                        { 
                            ReloadCount = _configurationReloads,
                            ValidationPassed = validationResult.IsSuccess
                        });

                    UpdateMetric("ConfigurationReloads", _configurationReloads);

                    return validationResult;
                }
                finally
                {
                    _updateSemaphore.Release();
                    LogInfo("ReloadSettingsAsync - Exit");
                }
            }
            catch (Exception ex)
            {
                LogError("Error reloading settings", ex);
                return TradingResult.Failure(new TradingError("RELOAD_FAILED", $"Settings reload failed: {ex.Message}", ex));
            }
        }

        private async Task HandleSettingsChangeAsync(TradingPlatformSettings newSettings, string? name)
        {
            LogInfo("Configuration change detected", new { Name = name });

            await _updateSemaphore.WaitAsync();
            try
            {
                _currentSettings = newSettings;
                _settingsCache.Clear();
                
                Interlocked.Increment(ref _configurationReloads);
                _lastReloadTime = DateTime.UtcNow;

                await ValidateApiKeysAsync();
                await PublishSettingsChangeEventAsync();
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        private async Task ValidateApiKeysAsync()
        {
            LogDebug("ValidateApiKeysAsync - Entry");
            
            try
            {
                // This would normally validate the API keys
                // For now, just log that we would validate them
                if (_currentSettings.Api.Finnhub.Enabled)
                {
                    LogInfo("Finnhub API key configured", 
                        new { KeyLength = _currentSettings.Api.Finnhub.ApiKey?.Length ?? 0 });
                }

                if (_currentSettings.Api.AlphaVantage.Enabled)
                {
                    LogInfo("AlphaVantage API key configured", 
                        new { KeyLength = _currentSettings.Api.AlphaVantage.ApiKey?.Length ?? 0 });
                }

                await Task.CompletedTask;
            }
            finally
            {
                LogDebug("ValidateApiKeysAsync - Exit");
            }
        }

        private async Task PublishSettingsChangeEventAsync()
        {
            LogDebug("Publishing settings change event");
            
            // Would publish to message bus here
            await Task.CompletedTask;
        }

        protected Dictionary<string, object?> GetCustomMetrics()
        {
            var metrics = new Dictionary<string, object?>();
            
            metrics["ConfigurationReloads"] = _configurationReloads;
            metrics["SettingsValidations"] = _settingsValidations;
            metrics["SettingsUpdates"] = _settingsUpdates;
            metrics["LastReloadTime"] = _lastReloadTime;
            metrics["CachedSettings"] = _settingsCache.Count;
            metrics["MinutesSinceLastReload"] = (DateTime.UtcNow - _lastReloadTime).TotalMinutes;

            return metrics;
        }

        protected override void Dispose(bool disposing)
        {
            LogInfo("Dispose - Entry", new { Disposing = disposing });
            
            if (disposing)
            {
                foreach (var registration in _changeTokenRegistrations)
                {
                    registration?.Dispose();
                }
                _changeTokenRegistrations.Clear();
                
                _updateSemaphore?.Dispose();
            }
            
            base.Dispose(disposing);
            LogInfo("Dispose - Exit");
        }
    }

    /// <summary>
    /// Interface for settings service
    /// </summary>
    public interface ISettingsService
    {
        Task<TradingPlatformSettings> GetSettingsAsync();
        Task<T> GetSettingAsync<T>(string key, T defaultValue = default!);
        Task<TradingResult> UpdateSettingsAsync(TradingPlatformSettings newSettings);
        Task<TradingResult> ValidateSettingsAsync(TradingPlatformSettings settings);
        Task<TradingResult> ReloadSettingsAsync();
    }
}