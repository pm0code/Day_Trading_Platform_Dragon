using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Unified configuration service that combines encrypted API keys with regular configuration
    /// </summary>
    public class ConfigurationService : CanonicalServiceBase, IConfigurationService
    {
        private readonly EncryptedConfiguration _encryptedConfig;
        private readonly IConfiguration _configuration;
        
        public ConfigurationService(
            ILogger<ConfigurationService> logger,
            EncryptedConfiguration encryptedConfig,
            IConfiguration configuration) : base(logger)
        {
            _encryptedConfig = encryptedConfig;
            _configuration = configuration;
        }

        public override async Task<TradingResult> InitializeAsync()
        {
            using var operation = BeginOperation(OperationContext("Initializing configuration service"));
            
            try
            {
                // Initialize encrypted configuration (will run first-time setup if needed)
                var result = await _encryptedConfig.InitializeAsync();
                
                if (!result.IsSuccess)
                {
                    return operation.Failed(result.ErrorMessage!);
                }
                
                return operation.Succeeded("Configuration service initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize configuration service");
                return operation.Failed($"Initialization failed: {ex.Message}");
            }
        }

        #region API Keys (Encrypted)

        public string AlphaVantageApiKey => _encryptedConfig.GetApiKey("AlphaVantage");
        
        public string FinnhubApiKey => _encryptedConfig.GetApiKey("Finnhub");
        
        public string? IexCloudApiKey => _encryptedConfig.HasApiKey("IexCloud") 
            ? _encryptedConfig.GetApiKey("IexCloud") 
            : null;
        
        public string? PolygonApiKey => _encryptedConfig.HasApiKey("Polygon")
            ? _encryptedConfig.GetApiKey("Polygon")
            : null;

        #endregion

        #region API Endpoints (Not Sensitive)

        public string AlphaVantageBaseUrl => _configuration["ApiEndpoints:AlphaVantage"] 
            ?? "https://www.alphavantage.co/query";
            
        public string FinnhubBaseUrl => _configuration["ApiEndpoints:Finnhub"]
            ?? "https://finnhub.io/api/v1";
            
        public string IexCloudBaseUrl => _configuration["ApiEndpoints:IexCloud"]
            ?? "https://cloud.iexapis.com/stable";

        #endregion

        #region Rate Limits (Not Sensitive)

        public int AlphaVantageRequestsPerMinute => 
            _configuration.GetValue<int>("RateLimits:AlphaVantage:PerMinute", 5);
            
        public int FinnhubRequestsPerMinute => 
            _configuration.GetValue<int>("RateLimits:Finnhub:PerMinute", 60);
            
        public int IexCloudRequestsPerMinute =>
            _configuration.GetValue<int>("RateLimits:IexCloud:PerMinute", 100);

        #endregion

        #region Trading Configuration (Not Sensitive)

        public decimal MaxDailyLossPercent =>
            _configuration.GetValue<decimal>("TradingConfiguration:MaxDailyLossPercent", 0.06m);
            
        public decimal MaxRiskPerTradePercent =>
            _configuration.GetValue<decimal>("TradingConfiguration:MaxRiskPerTradePercent", 0.02m);
            
        public decimal MaxPositionSizePercent =>
            _configuration.GetValue<decimal>("TradingConfiguration:MaxPositionSizePercent", 0.25m);
            
        public decimal DefaultStopLossPercent =>
            _configuration.GetValue<decimal>("TradingConfiguration:DefaultStopLossPercent", 0.02m);

        #endregion

        protected override Task OnStartAsync()
        {
            _logger.LogInformation("Configuration service started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync()
        {
            _logger.LogInformation("Configuration service stopped");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Configuration service interface
    /// </summary>
    public interface IConfigurationService
    {
        // API Keys
        string AlphaVantageApiKey { get; }
        string FinnhubApiKey { get; }
        string? IexCloudApiKey { get; }
        string? PolygonApiKey { get; }
        
        // API Endpoints
        string AlphaVantageBaseUrl { get; }
        string FinnhubBaseUrl { get; }
        string IexCloudBaseUrl { get; }
        
        // Rate Limits
        int AlphaVantageRequestsPerMinute { get; }
        int FinnhubRequestsPerMinute { get; }
        int IexCloudRequestsPerMinute { get; }
        
        // Trading Configuration
        decimal MaxDailyLossPercent { get; }
        decimal MaxRiskPerTradePercent { get; }
        decimal MaxPositionSizePercent { get; }
        decimal DefaultStopLossPercent { get; }
    }
}