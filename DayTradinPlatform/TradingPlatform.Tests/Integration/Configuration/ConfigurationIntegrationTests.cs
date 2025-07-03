using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Integration.Configuration
{
    /// <summary>
    /// Integration tests for configuration service with encrypted keys
    /// </summary>
    public class ConfigurationIntegrationTests : IntegrationTestBase
    {
        private ConfigurationService _configService;
        private EncryptedConfiguration _encryptedConfig;
        
        public ConfigurationIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ApiEndpoints:AlphaVantage"] = "https://test.alphavantage.co",
                    ["ApiEndpoints:Finnhub"] = "https://test.finnhub.io",
                    ["RateLimits:AlphaVantage:PerMinute"] = "10",
                    ["RateLimits:Finnhub:PerMinute"] = "120",
                    ["TradingConfiguration:MaxDailyLossPercent"] = "0.05",
                    ["TradingConfiguration:MaxRiskPerTradePercent"] = "0.015"
                })
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<EncryptedConfiguration>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<IConfigurationService>(
                provider => provider.GetRequiredService<ConfigurationService>());
            services.AddSingleton(MockLogger.Object);
        }

        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _encryptedConfig = ServiceProvider.GetRequiredService<EncryptedConfiguration>();
            _configService = ServiceProvider.GetRequiredService<ConfigurationService>();
            
            // Create test encrypted configuration
            await CreateTestEncryptedConfig();
        }

        #region Service Integration Tests

        [Fact]
        public async Task ConfigurationService_LoadsBothSources_Successfully()
        {
            // Arrange
            Output.WriteLine("=== Testing Configuration Service Integration ==");

            // Act
            var result = await _configService.InitializeAsync();

            // Assert
            Assert.True(result.IsSuccess);
            
            // Verify encrypted keys loaded
            Assert.Equal("TestAlphaVantageKey123", _configService.AlphaVantageApiKey);
            Assert.Equal("TestFinnhubKey456", _configService.FinnhubApiKey);
            
            // Verify regular config loaded
            Assert.Equal("https://test.alphavantage.co", _configService.AlphaVantageBaseUrl);
            Assert.Equal("https://test.finnhub.io", _configService.FinnhubBaseUrl);
            Assert.Equal(10, _configService.AlphaVantageRequestsPerMinute);
            Assert.Equal(120, _configService.FinnhubRequestsPerMinute);
            
            Output.WriteLine("✓ Both encrypted and regular configuration loaded successfully");
        }

        [Fact]
        public async Task ConfigurationService_OptionalKeys_HandledCorrectly()
        {
            // Arrange & Act
            await _configService.InitializeAsync();

            // Assert - Optional keys
            Assert.Equal("TestIexKey789", _configService.IexCloudApiKey);
            Assert.Null(_configService.PolygonApiKey); // Not configured
            
            Output.WriteLine("✓ Optional API keys handled correctly");
        }

        [Fact]
        public async Task ConfigurationService_TradingConfig_LoadsDefaults()
        {
            // Arrange & Act
            await _configService.InitializeAsync();

            // Assert
            Assert.Equal(0.05m, _configService.MaxDailyLossPercent); // From config
            Assert.Equal(0.015m, _configService.MaxRiskPerTradePercent); // From config
            Assert.Equal(0.25m, _configService.MaxPositionSizePercent); // Default
            Assert.Equal(0.02m, _configService.DefaultStopLossPercent); // Default
            
            Output.WriteLine("✓ Trading configuration with defaults loaded correctly");
        }

        #endregion

        #region Provider Integration Tests

        [Fact]
        public async Task DataProviders_UseConfigurationService_Successfully()
        {
            // Arrange
            await _configService.InitializeAsync();
            
            // Simulate provider usage
            var alphaVantageUrl = $"{_configService.AlphaVantageBaseUrl}?apikey={_configService.AlphaVantageApiKey}";
            var finnhubUrl = $"{_configService.FinnhubBaseUrl}?token={_configService.FinnhubApiKey}";
            
            // Assert
            Assert.Contains("TestAlphaVantageKey123", alphaVantageUrl);
            Assert.Contains("TestFinnhubKey456", finnhubUrl);
            Assert.Contains("test.alphavantage.co", alphaVantageUrl);
            Assert.Contains("test.finnhub.io", finnhubUrl);
            
            Output.WriteLine("✓ Data providers can use configuration service");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ConfigurationService_MissingEncryptedConfig_Fails()
        {
            // Arrange - Delete encrypted config
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform",
                "config.encrypted"
            );
            
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            // Act
            var service = new ConfigurationService(
                Mock.Of<ILogger<ConfigurationService>>(),
                new EncryptedConfiguration(Mock.Of<ILogger<EncryptedConfiguration>>()),
                ServiceProvider.GetRequiredService<IConfiguration>()
            );
            
            var result = await service.InitializeAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Output.WriteLine($"✓ Missing config handled: {result.ErrorMessage}");
        }

        #endregion

        #region Lifecycle Tests

        [Fact]
        public async Task ConfigurationService_StartStop_LogsCorrectly()
        {
            // Arrange
            await _configService.InitializeAsync();

            // Act
            await _configService.StartAsync();
            await Task.Delay(100);
            await _configService.StopAsync();

            // Assert - Check logs
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
            
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
                
            Output.WriteLine("✓ Service lifecycle methods work correctly");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task ConfigurationService_ConcurrentAccess_PerformsWell()
        {
            // Arrange
            await _configService.InitializeAsync();
            var tasks = new List<Task<string>>();
            
            Output.WriteLine("=== Testing Concurrent Configuration Access ==");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act - 1000 concurrent reads
            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(Task.Run(() => _configService.AlphaVantageApiKey));
            }
            
            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // Assert
            Assert.All(results, r => Assert.Equal("TestAlphaVantageKey123", r));
            Output.WriteLine($"✓ 1000 concurrent reads completed in {sw.ElapsedMilliseconds}ms");
            Assert.True(sw.ElapsedMilliseconds < 100, "Concurrent access should be fast");
        }

        #endregion

        #region Helper Methods

        private async Task CreateTestEncryptedConfig()
        {
            var testData = new Dictionary<string, string>
            {
                { "AlphaVantage", "TestAlphaVantageKey123" },
                { "Finnhub", "TestFinnhubKey456" },
                { "IexCloud", "TestIexKey789" }
            };

            // Create encrypted config (similar to unit test helper)
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            var protectedKey = ProtectedData.Protect(
                aes.Key,
                aes.IV,
                DataProtectionScope.CurrentUser
            );

            var keyData = new
            {
                ProtectedKey = Convert.ToBase64String(protectedKey),
                IV = Convert.ToBase64String(aes.IV)
            };
            
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform"
            );
            Directory.CreateDirectory(appDataPath);
            
            var keyPath = Path.Combine(appDataPath, "config.key");
            var configPath = Path.Combine(appDataPath, "config.encrypted");
            
            await File.WriteAllTextAsync(keyPath, JsonSerializer.Serialize(keyData));

            var jsonData = JsonSerializer.Serialize(testData);
            var encrypted = EncryptStringToBytes(jsonData, aes.Key, aes.IV);
            await File.WriteAllBytesAsync(configPath, encrypted);
            
            Output.WriteLine("Created test encrypted configuration");
        }

        private byte[] EncryptStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt);

            swEncrypt.Write(plainText);
            swEncrypt.Flush();
            csEncrypt.FlushFinalBlock();

            return msEncrypt.ToArray();
        }

        #endregion
    }
}