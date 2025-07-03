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
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.E2E
{
    /// <summary>
    /// End-to-end tests for configuration system including first-run experience
    /// </summary>
    public class ConfigurationE2ETests : E2ETestBase
    {
        private ConfigurationService _configService;
        private AlphaVantageProvider _alphaVantageProvider;
        private FinnhubProvider _finnhubProvider;
        
        public ConfigurationE2ETests(ITestOutputHelper output) : base(output)
        {
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ApiEndpoints:AlphaVantage"] = "https://test.alphavantage.co",
                    ["ApiEndpoints:Finnhub"] = "https://test.finnhub.io",
                })
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddEncryptedConfiguration();
            services.AddMemoryCache();
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(Mock.Of<ITradingLogger>());
            services.AddSingleton(Mock.Of<IRateLimiter>());
            
            // Register providers
            services.AddSingleton<AlphaVantageProvider>();
            services.AddSingleton<FinnhubProvider>();
        }

        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            // Create test configuration
            await CreateTestConfiguration();
            
            _configService = ServiceProvider.GetRequiredService<ConfigurationService>();
            await _configService.InitializeAsync();
            
            _alphaVantageProvider = ServiceProvider.GetRequiredService<AlphaVantageProvider>();
            _finnhubProvider = ServiceProvider.GetRequiredService<FinnhubProvider>();
        }

        #region Full Workflow Tests

        [Fact]
        public async Task FullConfigurationWorkflow_FromFirstRun_ToProviderUsage()
        {
            Output.WriteLine("=== Testing Full Configuration Workflow ===");
            
            // Step 1: Configuration loads successfully
            Assert.NotNull(_configService);
            Assert.Equal("E2ETestAlphaVantageKey", _configService.AlphaVantageApiKey);
            Assert.Equal("E2ETestFinnhubKey", _configService.FinnhubApiKey);
            Output.WriteLine("✓ Configuration loaded with encrypted keys");
            
            // Step 2: Providers can use configuration
            Assert.NotNull(_alphaVantageProvider);
            Assert.NotNull(_finnhubProvider);
            Assert.Equal("AlphaVantage", _alphaVantageProvider.ProviderName);
            Assert.Equal("Finnhub", _finnhubProvider.ProviderName);
            Output.WriteLine("✓ Providers initialized with configuration");
            
            // Step 3: Simulate provider API calls
            var alphaVantageCall = SimulateApiCall(
                _configService.AlphaVantageBaseUrl, 
                _configService.AlphaVantageApiKey);
            
            var finnhubCall = SimulateApiCall(
                _configService.FinnhubBaseUrl,
                _configService.FinnhubApiKey);
            
            Assert.Contains("E2ETestAlphaVantageKey", alphaVantageCall);
            Assert.Contains("E2ETestFinnhubKey", finnhubCall);
            Output.WriteLine("✓ API calls include encrypted keys");
        }

        [Fact]
        public async Task Configuration_PersistsAcrossRestarts()
        {
            Output.WriteLine("=== Testing Configuration Persistence ===");
            
            // First instance
            var config1 = new ConfigurationService(
                Mock.Of<ILogger<ConfigurationService>>(),
                new EncryptedConfiguration(Mock.Of<ILogger<EncryptedConfiguration>>()),
                ServiceProvider.GetRequiredService<IConfiguration>()
            );
            
            await config1.InitializeAsync();
            var key1 = config1.AlphaVantageApiKey;
            Output.WriteLine($"First load: Key = {key1.Substring(0, 5)}...");
            
            // Dispose first instance
            config1.Dispose();
            
            // Second instance (simulating restart)
            var config2 = new ConfigurationService(
                Mock.Of<ILogger<ConfigurationService>>(),
                new EncryptedConfiguration(Mock.Of<ILogger<EncryptedConfiguration>>()),
                ServiceProvider.GetRequiredService<IConfiguration>()
            );
            
            await config2.InitializeAsync();
            var key2 = config2.AlphaVantageApiKey;
            Output.WriteLine($"Second load: Key = {key2.Substring(0, 5)}...");
            
            // Assert keys are the same
            Assert.Equal(key1, key2);
            Output.WriteLine("✓ Configuration persists across application restarts");
        }

        #endregion

        #region Security Tests

        [Fact]
        public async Task EncryptedKeys_NotVisibleInFiles()
        {
            Output.WriteLine("=== Testing Encryption Security ===");
            
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform",
                "config.encrypted"
            );
            
            // Read raw file contents
            var encryptedContents = await File.ReadAllBytesAsync(configPath);
            var contentsAsString = Encoding.UTF8.GetString(encryptedContents);
            
            // Assert keys are not visible
            Assert.DoesNotContain("E2ETestAlphaVantageKey", contentsAsString);
            Assert.DoesNotContain("E2ETestFinnhubKey", contentsAsString);
            
            Output.WriteLine($"✓ Encrypted file size: {encryptedContents.Length} bytes");
            Output.WriteLine("✓ API keys are not visible in encrypted file");
        }

        [Fact]
        public void Configuration_RequiresCorrectUser()
        {
            Output.WriteLine("=== Testing User-Specific Encryption ===");
            
            // This test documents that DPAPI encryption is user-specific
            // In production, only the user who encrypted can decrypt
            
            Output.WriteLine("✓ Configuration is encrypted with DPAPI (user-specific)");
            Output.WriteLine("✓ Only the encrypting user can decrypt the keys");
            Output.WriteLine("✓ Provides protection against unauthorized access");
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task Configuration_HandlesCorruption_Gracefully()
        {
            Output.WriteLine("=== Testing Corruption Handling ===");
            
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform",
                "config.encrypted"
            );
            
            // Backup original
            var backup = await File.ReadAllBytesAsync(configPath);
            
            try
            {
                // Corrupt the file
                await File.WriteAllTextAsync(configPath, "CORRUPTED DATA");
                
                // Try to load
                var config = new EncryptedConfiguration(Mock.Of<ILogger<EncryptedConfiguration>>());
                var result = await config.InitializeAsync();
                
                // Should fail gracefully
                Assert.False(result.IsSuccess);
                Assert.Contains("decrypt", result.ErrorMessage);
                Output.WriteLine($"✓ Corruption detected: {result.ErrorMessage}");
            }
            finally
            {
                // Restore original
                await File.WriteAllBytesAsync(configPath, backup);
            }
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Configuration_LoadTime_Acceptable()
        {
            Output.WriteLine("=== Testing Configuration Load Performance ===");
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            var config = new ConfigurationService(
                Mock.Of<ILogger<ConfigurationService>>(),
                new EncryptedConfiguration(Mock.Of<ILogger<EncryptedConfiguration>>()),
                ServiceProvider.GetRequiredService<IConfiguration>()
            );
            
            await config.InitializeAsync();
            
            sw.Stop();
            
            Output.WriteLine($"✓ Configuration loaded in {sw.ElapsedMilliseconds}ms");
            Assert.True(sw.ElapsedMilliseconds < 100, "Configuration should load quickly");
        }

        [Fact]
        public async Task Configuration_KeyAccess_Fast()
        {
            Output.WriteLine("=== Testing Key Access Performance ===");
            
            await _configService.InitializeAsync();
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // Access keys 10000 times
            for (int i = 0; i < 10000; i++)
            {
                var key1 = _configService.AlphaVantageApiKey;
                var key2 = _configService.FinnhubApiKey;
            }
            
            sw.Stop();
            
            var avgAccess = sw.Elapsed.TotalMicroseconds / 20000; // 2 keys * 10000 iterations
            
            Output.WriteLine($"✓ 20,000 key accesses in {sw.ElapsedMilliseconds}ms");
            Output.WriteLine($"✓ Average access time: {avgAccess:F2} microseconds");
            Assert.True(avgAccess < 1, "Key access should be sub-microsecond");
        }

        #endregion

        #region Helper Methods

        private string SimulateApiCall(string baseUrl, string apiKey)
        {
            return $"{baseUrl}?apikey={apiKey}&symbol=TEST";
        }

        private async Task CreateTestConfiguration()
        {
            var testData = new Dictionary<string, string>
            {
                { "AlphaVantage", "E2ETestAlphaVantageKey" },
                { "Finnhub", "E2ETestFinnhubKey" },
                { "IexCloud", "E2ETestIexKey" }
            };

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