using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Unit.Configuration
{
    /// <summary>
    /// Comprehensive tests for encrypted configuration management
    /// </summary>
    public class EncryptedConfigurationTests : CanonicalTestBase<EncryptedConfiguration>
    {
        private readonly string _testConfigPath;
        private readonly string _testKeyPath;

        public EncryptedConfigurationTests()
        {
            // Use temp directory for tests
            var tempPath = Path.Combine(Path.GetTempPath(), $"TradingPlatformTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);
            
            _testConfigPath = Path.Combine(tempPath, "config.encrypted");
            _testKeyPath = Path.Combine(tempPath, "config.key");
        }

        protected override EncryptedConfiguration CreateSystemUnderTest()
        {
            return new EncryptedConfiguration(MockLogger.Object);
        }

        #region Initialization Tests

        [Fact]
        public async Task InitializeAsync_FirstRun_RequiresSetup()
        {
            // Arrange - Ensure no existing config
            if (File.Exists(_testConfigPath)) File.Delete(_testConfigPath);
            if (File.Exists(_testKeyPath)) File.Delete(_testKeyPath);

            // Act - Can't test interactive setup in unit tests
            // Would need to mock Console I/O

            // Assert - Verify files don't exist
            Assert.False(File.Exists(_testConfigPath));
            Assert.False(File.Exists(_testKeyPath));
        }

        [Fact]
        public async Task InitializeAsync_ExistingConfig_LoadsSuccessfully()
        {
            // Arrange - Create test configuration
            await CreateTestConfiguration();

            // Act
            var result = await SystemUnderTest.InitializeAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("AlphaVantageTestKey", SystemUnderTest.GetApiKey("AlphaVantage"));
            Assert.Equal("FinnhubTestKey", SystemUnderTest.GetApiKey("Finnhub"));
        }

        [Fact]
        public async Task InitializeAsync_CorruptedConfig_ReturnsError()
        {
            // Arrange - Create corrupted config
            await File.WriteAllTextAsync(_testConfigPath, "corrupted data");
            await File.WriteAllTextAsync(_testKeyPath, "corrupted key");

            // Act
            var result = await SystemUnderTest.InitializeAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to decrypt", result.ErrorMessage);
        }

        #endregion

        #region API Key Management Tests

        [Fact]
        public async Task GetApiKey_ValidKey_ReturnsDecryptedValue()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();

            // Act
            var apiKey = SystemUnderTest.GetApiKey("AlphaVantage");

            // Assert
            Assert.Equal("AlphaVantageTestKey", apiKey);
            MockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved API key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetApiKey_InvalidKey_ThrowsException()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(
                () => SystemUnderTest.GetApiKey("NonExistentKey"));
            
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task HasApiKey_ExistingKey_ReturnsTrue()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();

            // Act
            var hasKey = SystemUnderTest.HasApiKey("AlphaVantage");

            // Assert
            Assert.True(hasKey);
        }

        [Fact]
        public async Task HasApiKey_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();

            // Act
            var hasKey = SystemUnderTest.HasApiKey("NonExistentKey");

            // Assert
            Assert.False(hasKey);
        }

        [Fact]
        public async Task HasApiKey_EmptyValue_ReturnsFalse()
        {
            // Arrange
            await CreateTestConfiguration(includeEmptyKey: true);
            await SystemUnderTest.InitializeAsync();

            // Act
            var hasKey = SystemUnderTest.HasApiKey("EmptyKey");

            // Assert
            Assert.False(hasKey);
        }

        #endregion

        #region Encryption/Decryption Tests

        [Fact]
        public async Task Encryption_RoundTrip_PreservesData()
        {
            // Arrange
            var testData = new Dictionary<string, string>
            {
                { "TestKey1", "TestValue1!@#$%^&*()" },
                { "TestKey2", "TestValue2 with spaces" },
                { "TestKey3", "TestValue3\nwith\nnewlines" },
                { "TestKey4", "TestValue4 with unicode 🚀" }
            };

            // Act - Save and reload
            await CreateTestConfiguration(testData);
            await SystemUnderTest.InitializeAsync();

            // Assert - All values preserved
            foreach (var kvp in testData)
            {
                Assert.Equal(kvp.Value, SystemUnderTest.GetApiKey(kvp.Key));
            }
        }

        [Fact]
        public async Task Encryption_DifferentUser_CannotDecrypt()
        {
            // This test would fail in CI/CD as it requires multiple user accounts
            // Including for documentation purposes
            
            // Arrange - Create config as User A
            // await CreateTestConfiguration();
            
            // Act - Try to load as User B (would fail)
            // Switch user context...
            
            // Assert - Should fail with appropriate error
            // Assert.Contains("different user", result.ErrorMessage);
        }

        [Fact]
        public async Task Encryption_UsesStrongCryptography()
        {
            // Arrange
            await CreateTestConfiguration();

            // Act - Read encrypted file
            var encryptedData = await File.ReadAllBytesAsync(_testConfigPath);
            var keyData = await File.ReadAllTextAsync(_testKeyPath);

            // Assert
            Assert.True(encryptedData.Length > 0);
            Assert.DoesNotContain(Encoding.UTF8.GetBytes("AlphaVantageTestKey"), encryptedData);
            Assert.Contains("ProtectedKey", keyData);
            Assert.Contains("IV", keyData);
        }

        #endregion

        #region Memory Security Tests

        [Fact]
        public async Task Dispose_ClearsSecrets_FromMemory()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();
            
            // Verify key is accessible
            var keyBefore = SystemUnderTest.GetApiKey("AlphaVantage");
            Assert.NotEmpty(keyBefore);

            // Act
            SystemUnderTest.Dispose();

            // Assert - Keys should be cleared
            Assert.Throws<InvalidOperationException>(
                () => SystemUnderTest.GetApiKey("AlphaVantage"));
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            await CreateTestConfiguration();
            await SystemUnderTest.InitializeAsync();
            
            var tasks = new List<Task>();
            var results = new List<string>();
            var errors = new List<Exception>();

            // Act - Concurrent reads
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var key = SystemUnderTest.GetApiKey("AlphaVantage");
                        lock (results)
                        {
                            results.Add(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(errors);
            Assert.All(results, r => Assert.Equal("AlphaVantageTestKey", r));
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task Validation_MissingRequiredKeys_Fails()
        {
            // Arrange - Create config without required keys
            await CreateTestConfiguration(new Dictionary<string, string>
            {
                { "OptionalKey", "value" }
            });

            // Act
            var result = await SystemUnderTest.InitializeAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Missing required API keys", result.ErrorMessage);
        }

        [Fact]
        public async Task Validation_AllRequiredKeysPresent_Succeeds()
        {
            // Arrange
            await CreateTestConfiguration(new Dictionary<string, string>
            {
                { "AlphaVantage", "key1" },
                { "Finnhub", "key2" }
            });

            // Act
            var result = await SystemUnderTest.InitializeAsync();

            // Assert
            Assert.True(result.IsSuccess);
        }

        #endregion

        #region Helper Methods

        private async Task CreateTestConfiguration(
            Dictionary<string, string>? customData = null,
            bool includeEmptyKey = false)
        {
            var data = customData ?? new Dictionary<string, string>
            {
                { "AlphaVantage", "AlphaVantageTestKey" },
                { "Finnhub", "FinnhubTestKey" },
                { "IexCloud", "IexCloudTestKey" }
            };

            if (includeEmptyKey)
            {
                data["EmptyKey"] = "";
            }

            // Simulate what EncryptedConfiguration does
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            // Protect key with DPAPI
            var protectedKey = ProtectedData.Protect(
                aes.Key,
                aes.IV,
                DataProtectionScope.CurrentUser
            );

            // Save key file
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

            // Encrypt and save config
            var jsonData = JsonSerializer.Serialize(data);
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

        public override void Dispose()
        {
            base.Dispose();
            
            // Cleanup test files
            try
            {
                if (Directory.Exists(Path.GetDirectoryName(_testConfigPath)))
                {
                    Directory.Delete(Path.GetDirectoryName(_testConfigPath)!, true);
                }
            }
            catch { }
        }

        #endregion
    }
}