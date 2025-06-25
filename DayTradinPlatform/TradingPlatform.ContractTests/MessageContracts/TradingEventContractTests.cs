using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using TradingPlatform.Messaging.Events;
using TradingPlatform.UnitTests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ContractTests.MessageContracts
{
    /// <summary>
    /// Contract tests for trading event message schemas
    /// </summary>
    public class TradingEventContractTests : CanonicalTestBase
    {
        private readonly JSchema _tradingEventSchema;

        public TradingEventContractTests(ITestOutputHelper output) : base(output)
        {
            // Define the expected schema for TradingEvent
            _tradingEventSchema = JSchema.Parse(@"
            {
                'type': 'object',
                'required': ['EventType', 'Symbol', 'Timestamp'],
                'properties': {
                    'EventType': {
                        'type': 'string',
                        'enum': ['SignalGenerated', 'OrderPlaced', 'OrderExecuted', 'OrderCancelled', 
                                 'PositionOpened', 'PositionClosed', 'RiskAlert', 'GoldenRuleViolation',
                                 'MarketDataUpdate', 'SystemAlert']
                    },
                    'Symbol': {
                        'type': 'string',
                        'minLength': 1,
                        'maxLength': 10,
                        'pattern': '^[A-Z0-9]+$'
                    },
                    'Timestamp': {
                        'type': 'string',
                        'format': 'date-time'
                    },
                    'CorrelationId': {
                        'type': ['string', 'null'],
                        'format': 'uuid'
                    },
                    'Version': {
                        'type': 'string',
                        'pattern': '^\\d+\\.\\d+$'
                    },
                    'Source': {
                        'type': ['string', 'null']
                    },
                    'Data': {
                        'type': ['object', 'null']
                    }
                },
                'additionalProperties': false
            }");
        }

        [Fact]
        public void TradingEvent_Serialization_MeetsContract()
        {
            // Arrange
            var tradingEvent = new TradingEvent
            {
                EventType = TradingEventType.OrderPlaced,
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                Version = "1.0",
                Source = "OrderExecutionEngine",
                Data = new Dictionary<string, object>
                {
                    ["OrderId"] = "ORD123",
                    ["Quantity"] = 100,
                    ["Price"] = 150.50m
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(tradingEvent);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(_tradingEventSchema).Should().BeTrue();
        }

        [Theory]
        [InlineData(TradingEventType.SignalGenerated)]
        [InlineData(TradingEventType.OrderExecuted)]
        [InlineData(TradingEventType.RiskAlert)]
        [InlineData(TradingEventType.GoldenRuleViolation)]
        public void TradingEvent_AllEventTypes_AreValid(TradingEventType eventType)
        {
            // Arrange
            var tradingEvent = new TradingEvent
            {
                EventType = eventType,
                Symbol = "TSLA",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var json = JsonConvert.SerializeObject(tradingEvent);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(_tradingEventSchema).Should().BeTrue();
        }

        [Fact]
        public void TradingEvent_MissingRequiredFields_FailsValidation()
        {
            // Arrange - Missing Symbol
            var invalidEvent = new
            {
                EventType = "OrderPlaced",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var json = JsonConvert.SerializeObject(invalidEvent);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(_tradingEventSchema).Should().BeFalse();
        }

        [Theory]
        [InlineData("a", false)] // Too short
        [InlineData("VERYLONGSYMBOL", false)] // Too long
        [InlineData("lower", false)] // Lowercase not allowed
        [InlineData("AAPL", true)] // Valid
        [InlineData("BRK.B", false)] // Special character
        [InlineData("123", true)] // Numbers allowed
        public void TradingEvent_SymbolValidation_EnforcesPattern(string symbol, bool shouldBeValid)
        {
            // Arrange
            var tradingEvent = new TradingEvent
            {
                EventType = TradingEventType.OrderPlaced,
                Symbol = symbol,
                Timestamp = DateTime.UtcNow
            };

            // Act
            var json = JsonConvert.SerializeObject(tradingEvent);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(_tradingEventSchema).Should().Be(shouldBeValid);
        }

        [Fact]
        public void TradingEvent_BackwardCompatibility_V1ToV2()
        {
            // Arrange - V1 format (without Version field)
            var v1Event = @"{
                'EventType': 'OrderPlaced',
                'Symbol': 'AAPL',
                'Timestamp': '2024-01-25T10:00:00Z'
            }";

            // Act
            var jObject = JObject.Parse(v1Event);
            
            // Make schema backward compatible by not requiring Version
            var backwardCompatibleSchema = JSchema.Parse(_tradingEventSchema.ToString());
            var required = backwardCompatibleSchema.Required as IList<string>;
            required?.Remove("Version");

            // Assert
            jObject.IsValid(backwardCompatibleSchema).Should().BeTrue();
        }

        [Fact]
        public void OrderPlacedEventData_MeetsContract()
        {
            // Arrange
            var orderDataSchema = JSchema.Parse(@"
            {
                'type': 'object',
                'required': ['OrderId', 'OrderType', 'Side', 'Quantity', 'Price'],
                'properties': {
                    'OrderId': { 'type': 'string' },
                    'OrderType': { 
                        'type': 'string',
                        'enum': ['Market', 'Limit', 'Stop', 'StopLimit']
                    },
                    'Side': {
                        'type': 'string',
                        'enum': ['Buy', 'Sell']
                    },
                    'Quantity': {
                        'type': 'number',
                        'minimum': 0.01
                    },
                    'Price': {
                        'type': 'number',
                        'minimum': 0
                    },
                    'StopPrice': {
                        'type': ['number', 'null'],
                        'minimum': 0
                    },
                    'TimeInForce': {
                        'type': ['string', 'null'],
                        'enum': ['Day', 'GTC', 'IOC', 'FOK', null]
                    }
                }
            }");

            var orderData = new Dictionary<string, object>
            {
                ["OrderId"] = "ORD123",
                ["OrderType"] = "Limit",
                ["Side"] = "Buy",
                ["Quantity"] = 100,
                ["Price"] = 150.50m,
                ["TimeInForce"] = "Day"
            };

            // Act
            var json = JsonConvert.SerializeObject(orderData);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(orderDataSchema).Should().BeTrue();
        }

        [Fact]
        public void GoldenRuleViolationEventData_MeetsContract()
        {
            // Arrange
            var violationDataSchema = JSchema.Parse(@"
            {
                'type': 'object',
                'required': ['RuleNumber', 'RuleName', 'Severity', 'Message'],
                'properties': {
                    'RuleNumber': {
                        'type': 'integer',
                        'minimum': 1,
                        'maximum': 12
                    },
                    'RuleName': { 'type': 'string' },
                    'Severity': {
                        'type': 'string',
                        'enum': ['Info', 'Warning', 'Critical']
                    },
                    'Message': { 'type': 'string' },
                    'Impact': {
                        'type': ['number', 'null'],
                        'minimum': 0,
                        'maximum': 1
                    },
                    'Recommendation': { 'type': ['string', 'null'] }
                }
            }");

            var violationData = new Dictionary<string, object>
            {
                ["RuleNumber"] = 1,
                ["RuleName"] = "Never Risk More Than 2% Per Trade",
                ["Severity"] = "Critical",
                ["Message"] = "Position risk exceeds 2% limit",
                ["Impact"] = 0.8,
                ["Recommendation"] = "Reduce position size to comply with risk limits"
            };

            // Act
            var json = JsonConvert.SerializeObject(violationData);
            var jObject = JObject.Parse(json);

            // Assert
            jObject.IsValid(violationDataSchema).Should().BeTrue();
        }

        [Fact]
        public void TradingEvent_SchemaEvolution_SupportsNewFields()
        {
            // Arrange - Extended event with new fields
            var extendedEvent = new
            {
                // Required fields
                EventType = "OrderPlaced",
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow,
                
                // Optional standard fields
                Version = "2.0",
                CorrelationId = Guid.NewGuid().ToString(),
                
                // New fields (should be in Data object)
                Data = new
                {
                    OrderId = "ORD123",
                    ClientId = "CLIENT456", // New field
                    Tags = new[] { "momentum", "breakout" }, // New field
                    Metadata = new { Source = "API", Region = "US" } // New field
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(extendedEvent);
            var jObject = JObject.Parse(json);

            // Assert
            // Base schema should still validate (new fields are in Data)
            jObject.IsValid(_tradingEventSchema).Should().BeTrue();
        }
    }
}