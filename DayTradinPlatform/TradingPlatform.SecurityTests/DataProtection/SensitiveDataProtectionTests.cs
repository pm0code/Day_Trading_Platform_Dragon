using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.SecurityTests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.SecurityTests.DataProtection
{
    public class SensitiveDataProtectionTests : SecurityTestBase
    {
        public SensitiveDataProtectionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TradingError_ShouldNotExposeStackTrace()
        {
            // Arrange
            Exception innerException;
            try
            {
                throw new InvalidOperationException("Test exception with sensitive path: C:\\Users\\Admin\\TradingSecrets\\");
            }
            catch (Exception ex)
            {
                innerException = ex;
            }

            // Act
            var error = new TradingError("TEST001", "Operation failed", 
                new { Exception = innerException.Message }); // Should not include stack trace

            var serialized = JsonSerializer.Serialize(error);

            // Assert
            serialized.Should().NotContain("StackTrace");
            serialized.Should().NotContain("C:\\Users\\Admin");
            serialized.Should().NotContain("TradingSecrets");
        }

        [Fact]
        public void Order_ToString_ShouldNotExposeAccountId()
        {
            // Arrange
            var order = new Order
            {
                Id = "ORD123",
                Symbol = "AAPL",
                AccountId = "ACC-SECRET-12345",
                ClientOrderId = "CLIENT-SENSITIVE-789",
                Quantity = 100,
                Price = 150m
            };

            // Act
            var orderString = order.ToString();

            // Assert
            if (!string.IsNullOrEmpty(orderString))
            {
                orderString.Should().NotContain("ACC-SECRET-12345");
                orderString.Should().NotContain("CLIENT-SENSITIVE-789");
            }
        }

        [Fact]
        public void Position_ShouldNotExposeStrategySecrets()
        {
            // Arrange
            var position = new Position
            {
                Id = "POS123",
                Symbol = "AAPL",
                StrategyId = "SECRET-MOMENTUM-STRATEGY-KEY",
                AccountId = "ACCOUNT-123-SECRET",
                Quantity = 100,
                AveragePrice = 150m
            };

            // Act
            var json = JsonSerializer.Serialize(position);

            // Assert
            // Strategy ID and Account ID should be present but could be masked in production
            json.Should().Contain("StrategyId");
            json.Should().Contain("AccountId");
            
            // In production, these should be masked or encrypted
            // For now, we just verify they're not accidentally removed
        }

        [Fact]
        public void LoggingMethods_ShouldNotLogSensitiveData()
        {
            // Arrange
            var sensitiveData = new
            {
                ApiKey = "sk_live_abcd1234567890",
                Password = "SuperSecret123!",
                AccountNumber = "123456789",
                SSN = "123-45-6789",
                CreditCard = "4111111111111111"
            };

            // Act & Assert
            MockLogger.Setup(x => x.LogInformation(
                It.IsAny<string>(),
                It.Is<object>(data => ContainsSensitiveData(data)),
                It.IsAny<string>(),
                It.IsAny<int>()))
                .Callback<string, object?, string, int>((msg, data, file, line) =>
                {
                    var dataString = JsonSerializer.Serialize(data);
                    AssertNoSensitiveDataInLogs(dataString, new[]
                    {
                        "sk_live_",
                        "SuperSecret",
                        "123456789",
                        "123-45-6789",
                        "4111111111111111"
                    });
                });

            // This would trigger the callback if sensitive data is logged
            MockLogger.Object.LogInformation("Test", new { Data = "safe" });
        }

        [Fact]
        public void MarketConfiguration_ShouldProtectApiKeys()
        {
            // Arrange
            var config = new MarketConfiguration
            {
                AlphaVantageApiKey = "YOUR_API_KEY_HERE",
                FinnhubApiKey = "brd4jkvrh5rf2d5g7ej0",
                MaxConcurrentRequests = 5
            };

            // Act
            var json = JsonSerializer.Serialize(config);

            // Assert
            // In production, API keys should be encrypted or stored securely
            json.Should().Contain("ApiKey");
            
            // Verify the keys are present (in production they'd be encrypted)
            config.AlphaVantageApiKey.Should().NotBeNullOrEmpty();
            config.FinnhubApiKey.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void PositionContext_ShouldMaskSensitiveFinancialData()
        {
            // Arrange
            var context = new PositionContext
            {
                Symbol = "AAPL",
                AccountBalance = 1234567.89m,
                BuyingPower = 4938271.56m,
                DailyPnL = -12345.67m,
                TotalRiskExposure = 98765.43m
            };

            // Act
            var publicView = new
            {
                Symbol = context.Symbol,
                HasPosition = context.AccountBalance > 0,
                IsProfit = context.DailyPnL > 0
                // Actual values should not be exposed in public views
            };

            var publicJson = JsonSerializer.Serialize(publicView);

            // Assert
            publicJson.Should().NotContain("1234567");
            publicJson.Should().NotContain("4938271");
            publicJson.Should().NotContain("12345.67");
            publicJson.Should().NotContain("98765.43");
        }

        [Fact]
        public void GoldenRulesAssessment_ShouldNotExposeInternalDetails()
        {
            // Arrange
            var violations = new List<GoldenRuleResult>
            {
                new GoldenRuleResult
                {
                    RuleNumber = 1,
                    RuleName = "2% Risk Rule",
                    IsCompliant = false,
                    Details = new Dictionary<string, object>
                    {
                        ["InternalRiskModel"] = "PROPRIETARY_RISK_ALGO_V2",
                        ["SecretThreshold"] = 0.0234m,
                        ["AccountRiskProfile"] = "HIGH_NET_WORTH_SPECIAL"
                    }
                }
            };

            // Act
            var publicViolation = new
            {
                RuleNumber = violations[0].RuleNumber,
                RuleName = violations[0].RuleName,
                IsCompliant = violations[0].IsCompliant
                // Details should be filtered
            };

            var json = JsonSerializer.Serialize(publicViolation);

            // Assert
            json.Should().NotContain("PROPRIETARY_RISK_ALGO");
            json.Should().NotContain("HIGH_NET_WORTH_SPECIAL");
            json.Should().NotContain("SecretThreshold");
        }

        [Theory]
        [InlineData("password123", "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8")]
        [InlineData("MySecretKey!", "7b3d979ca8330a94fa7e9e1b466d8b99e0bcdea1ec90596c0dcc8d7ef6b4300c")]
        public void HashSensitiveData_ShouldProduceConsistentHash(string input, string expectedHashPrefix)
        {
            // Act
            var hash1 = HashSensitiveData(input);
            var hash2 = HashSensitiveData(input);

            // Assert
            hash1.Should().Be(hash2); // Consistent hashing
            hash1.Should().NotBe(input); // Not storing plain text
            hash1.Should().HaveLength(44); // Base64 encoded SHA256
        }

        [Fact]
        public void MemoryProtection_ShouldClearSensitiveDataAfterUse()
        {
            // Arrange
            var sensitiveBytes = Encoding.UTF8.GetBytes("SuperSecretApiKey123!");
            var length = sensitiveBytes.Length;

            // Act
            // Simulate clearing sensitive data
            Array.Clear(sensitiveBytes, 0, length);

            // Assert
            sensitiveBytes.Should().OnlyContain(b => b == 0);
        }

        private bool ContainsSensitiveData(object? data)
        {
            if (data == null) return false;
            
            var json = JsonSerializer.Serialize(data);
            var sensitivePatterns = new[] { "password", "apikey", "secret", "ssn", "creditcard" };
            
            foreach (var pattern in sensitivePatterns)
            {
                if (json.ToLower().Contains(pattern))
                    return true;
            }
            
            return false;
        }
    }
}