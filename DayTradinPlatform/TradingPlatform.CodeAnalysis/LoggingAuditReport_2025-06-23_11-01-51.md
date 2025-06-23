# Trading Platform - Comprehensive Logging Audit Report
Generated: 2025-06-23 11:01:51

## Summary
- Projects Analyzed: 18
- Files Analyzed: 231
- Total Violations: 226

## Violations by Type
- StringInterpolation: 226

## Violations by Project
- TradingPlatform.DataIngestion: 85
- TradingPlatform.FixEngine: 63
- TradingPlatform.Logging: 30
- TradingPlatform.Screening: 20
- TradingPlatform.Database: 16
- TradingPlatform.Core: 8
- TradingPlatform.Messaging: 4

## Detailed Violations
### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Instrumentation/MethodInstrumentationInterceptor.cs:284
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogError(
                $"Method {context.MethodName} failed after {executionMicroseconds}μs",
                exception,
                context.MethodName, // operationContext
                "Method execution failed", // userImpact
                "Check method implementation and input parameters", // troubleshootingHints
                new { 
                    ExecutionTime = $"{executionMicroseconds}μs",
                    CorrelationId = context.CorrelationId,
                    ReturnValue = returnValue
                }, // additionalData
                context.MethodName, // memberName
                context.SourceFilePath, // sourceFilePath
                context.SourceLineNumber // sourceLineNumber
            )`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Instrumentation/MethodInstrumentationInterceptor.cs:314
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogWarning(
                    $"Method {context.MethodName} exceeded performance threshold: {executionMicroseconds}μs",
                    "Performance degradation detected", // impact
                    "Review method implementation for optimization opportunities", // recommendedAction
                    new { 
                        Method = context.MethodName,
                        ExecutionMicroseconds = executionMicroseconds,
                        Threshold = context.InstrumentationInfo.Attribute?.ExpectedMaxExecutionMicroseconds ?? 1000
                    }, // additionalData
                    context.MethodName // memberName (CallerMemberName)
                )`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Instrumentation/MethodInstrumentationInterceptor.cs:336
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo(
            $"Trading operation: {context.MethodName} | Category: {attr.Category} | Status: {(exception == null ? "Success" : "Failed")} | Execution: {executionMicroseconds}μs",
            new
            {
                OperationCategory = attr.Category,
                AffectsPositions = attr.AffectsPositions,
                InvolvesRisk = attr.InvolvesRisk,
                RequiresCompliance = attr.RequiresComplianceReporting,
                BusinessImpact = attr.BusinessImpact,
                ExecutionTime = $"{executionMicroseconds}μs",
                CorrelationId = context.CorrelationId,
                Status = exception == null ? "Success" : "Failed",
                Exception = exception?.Message
            },
            context.MethodName,
            context.SourceFilePath,
            context.SourceLineNumber
        )`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Models/MarketConfiguration.cs:32
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid MarketOpen ({value}) after MarketClose ({MarketClose}) for {MarketCode}. Setting MarketOpen to default (9:30 AM).")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Models/MarketConfiguration.cs:51
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid MarketClose ({value}) before MarketOpen ({MarketOpen}) for {MarketCode}. Setting MarketClose to default (4:00 PM).")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Models/MarketConfiguration.cs:76
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid configuration for {MarketCode}: MarketOpen is after MarketClose.")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Models/MarketData.cs:42
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid Bid ({value}) > Ask ({Ask}) for {Symbol}. Setting Bid to Ask.")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Models/MarketData.cs:61
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid Ask ({value}) < Bid ({Bid}) for {Symbol}. Setting Ask to Bid.")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:83
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to queue market data record: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:103
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to queue execution record: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:123
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to queue performance metric: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:159
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to retrieve market data for {symbol}: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:197
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to retrieve execution history: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:236
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to calculate latency metrics: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:270
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing market data batches: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:302
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing execution batches: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:334
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing performance batches: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:346
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} market data records")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:350
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to flush market data batch: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:362
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} execution records")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:366
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to flush execution batch: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:378
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} performance metrics")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:382
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to flush performance batch: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/Services/HighPerformanceDataService.cs:396
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"HighPerformanceDataService disposed. Total inserts - Market Data: {_marketDataInsertCount}, Executions: {_executionInsertCount}, Performance: {_performanceInsertCount}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:40
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching real-time data for {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:47
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Real-time data for {symbol} retrieved from cache.")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:68
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error fetching real-time data from AlphaVantage for {symbol}: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:76
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to deserialize AlphaVantage response for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:85
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved real-time data for {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:90
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching real-time data for {symbol} from AlphaVantage", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:97
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:102
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Historical data for {symbol} retrieved from cache")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:120
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to fetch historical data for {symbol}: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:128
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No historical data available for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:140
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Retrieved {historicalData.Count} historical records for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:146
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching historical data for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:153
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching batch real-time data for {symbols.Count} symbols from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:169
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {results.Count}/{symbols.Count} real-time quotes from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:175
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Setting up real-time subscription for {symbol} using AlphaVantage polling")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:196
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error in real-time subscription for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:214
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching market data for {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:222
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching batch quotes for {symbols.Count} symbols from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:230
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching {days} days of data for {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:261
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching quote for {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/AlphaVantageProvider.cs:289
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error mapping AlphaVantage quote data for {quote?.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:38
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching quote for {symbol} from Finnhub")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:45
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Quote for {symbol} retrieved from cache.")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:65
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to get Finnhub quote for {symbol}: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:73
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to deserialize Finnhub quote response for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:82
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved Finnhub quote for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:87
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching quote for {symbol} from Finnhub", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:94
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching batch quotes for {symbols.Count} symbols from Finnhub")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:109
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {results.Count}/{symbols.Count} quotes from Finnhub")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:115
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching candle data for {symbol} from Finnhub")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:142
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to get Finnhub candle data for {symbol}: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:149
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No candle data available for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:168
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching candle data for {symbol} from Finnhub", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:175
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching historical data for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:203
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching stock symbols for exchange: {exchange}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:208
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Stock symbols for {exchange} retrieved from cache")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:224
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to get stock symbols from Finnhub: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:233
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Retrieved {symbols.Count} symbols for exchange {exchange}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:239
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching stock symbols for {exchange}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:266
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Failed to get market status from Finnhub: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:276
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Market status: {(isOpen ? "Open" : "Closed")}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:289
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching sentiment data for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:311
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Failed to get sentiment data for {symbol}: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:325
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching sentiment for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:332
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Fetching market news for category: {category}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:352
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Failed to get market news: {response.ErrorMessage}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:361
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Retrieved {newsItems.Count} news items")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/FinnhubProvider.cs:367
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Exception while fetching market news", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:52
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Aggregating data from {providerName}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:59
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Null data received from {providerName}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:78
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Unknown data type from {providerName}: {typeof(T).Name}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:91
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid market data from {providerName}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:97
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error aggregating data from {providerName}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:106
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Aggregating batch of {dataList?.Count ?? 0} items from {providerName}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:124
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully aggregated {results.Count}/{dataList.Count} items from {providerName}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:130
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Aggregating multi-provider data for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:139
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Data discrepancies detected for {symbol}: {string.Join(", ", qualityReport.Issues)}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:158
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Using fallback data for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:162
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"No valid data available for {symbol} from any provider")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:185
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Recorded failure for provider {providerName}. Consecutive failures: {_consecutiveFailures[providerName]}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:203
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Provider {providerName} recovered from failure state")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:231
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Invalid market data for {marketData?.Symbol}: Price={marketData?.Price}, Volume={marketData?.Volume}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:311
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cache hit for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:316
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cache miss for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:327
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cached data for {symbol} with expiration {expiration.TotalMinutes} minutes")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:374
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Getting real-time data for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:381
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {symbol} from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:387
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Falling back to Finnhub for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:393
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Successfully retrieved {symbol} from Finnhub fallback")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:398
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to retrieve data for {symbol} from all providers")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:404
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Getting quotes for {symbols.Count} symbols")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:415
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Retrieved {alphaVantageResults.Count} quotes from AlphaVantage")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:428
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Retrieved {finnhubResults.Count} quotes from Finnhub")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:432
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Total retrieved: {results.Count}/{symbols.Count} quotes")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:442
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Provider {providerName} is in circuit breaker state")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:457
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error retrieving data from {providerName}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:502
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error mapping AlphaVantage quote for {quote?.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Providers/MarketDataAggregator.cs:526
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error mapping Finnhub quote for {quote?.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:35
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cache hit for key: {key}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:39
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cache miss for key: {key}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:47
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Attempted to cache null value for key: {key}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:59
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cached value for key: {key}, expires in: {expiration}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:65
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Removed cache entry for key: {key}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:72
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Clearing market data cache for: {marketCode}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/Services/CacheService.cs:81
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Cache existence check for key: {key} = {exists}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:79
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"FixEngine initialization: VenueCount={config.VenueConfigs.Count}, CorrelationId={correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:81
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Initializing FIX Engine with {config.VenueConfigs.Count} venues | CorrelationId: {correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:101
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"FIX Engine initialization completed successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:113
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"FixEngine initialization failure: Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:115
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to initialize FIX Engine after {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:140
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Order submission: OrderId={orderId}, Symbol={request.Symbol}, Side={request.Side}, Quantity={request.Quantity}, CorrelationId={correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:167
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Order submission failure: OrderId={orderId}, Venue={optimalVenue}, Symbol={request.Symbol}, ErrorType=VenueUnavailable")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:207
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Order submitted: {orderId} -> {clOrdId} via {optimalVenue} in {stopwatch.Elapsed.TotalMicroseconds:F2}μs | CorrelationId: {correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:217
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Order submission failure: OrderId={orderId}, Symbol={request.Symbol}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:219
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to submit order: {orderId} after {stopwatch.Elapsed.TotalMicroseconds:F2}μs | CorrelationId: {correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:240
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Order not found for cancellation: {orderId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:245
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to cancel order: {orderId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:275
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Order not found for modification: {orderId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:280
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to modify order: {orderId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:306
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Market data subscriptions: {successCount}/{results.Length} for {string.Join(",", symbols)}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:312
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to subscribe to market data for symbols: {string.Join(", ", symbols)}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:336
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Market data unsubscriptions: {successCount}/{results.Length} for {string.Join(",", symbols)}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:342
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to unsubscribe from market data for symbols: {string.Join(", ", symbols)}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:391
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Venue initialization: VenueName={venueName}, Host={config.Host}, Port={config.Port}, CorrelationId={correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:393
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Initializing venue: {venueName} at {config.Host}:{config.Port} | CorrelationId: {correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:452
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Successfully connected to venue: {venueName} in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:475
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, ErrorType=ConnectionFailure, CorrelationId={correlationId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:486
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:488
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to initialize venue: {venueName} after {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:552
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Order status changed: {order.ClOrdId} -> {order.Status}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:557
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Order rejected: {reject.ClOrdId} - {reject.RejectReason}: {reject.RejectText}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:562
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Subscription status changed: {status}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:567
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Venue {venue} status changed: {status}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixEngine.cs:587
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Venue {venue} is disconnected, attempting reconnection")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:108
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"FIX session connected to {host}:{port}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:114
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to connect FIX session: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:138
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error during FIX session disconnect: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:203
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing outbound messages: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:220
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Sent FIX message: {message.MsgType} (seq={message.MsgSeqNum})")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:249
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error receiving FIX messages: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:277
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error parsing FIX message: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:297
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing inbound messages: {ex.Message}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/FixSession.cs:333
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Received FIX message: {message.MsgType} (seq={message.MsgSeqNum})")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:88
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Market data subscription requested for {symbol}, RequestId: {requestId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:96
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Failed to send market data request for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:103
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error subscribing to market data for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:116
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No active subscription found for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:140
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Market data unsubscribed for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:148
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error unsubscribing from market data for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:190
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing market data message: {message.MsgType}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:328
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Market data request rejected - RequestId: {mdReqId}, Reason: {rejectReason}, Text: {text}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/MarketDataManager.cs:349
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Stale subscription detected for {subscription.Symbol}, resubscribing...")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:80
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Order submitted: {clOrdId} - {order.Symbol} {order.Side} {order.Quantity}@{order.Price}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:96
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error submitting order: {clOrdId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:108
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Cannot cancel order - not found: {clOrdId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:114
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Cannot cancel order in status: {order.Status}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:137
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Cancel request sent for order: {clOrdId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:145
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error cancelling order: {clOrdId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:213
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Order replace request sent: {origClOrdId} -> {newClOrdId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:223
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error replacing order: {origClOrdId}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:265
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Mass cancel request sent - Symbol: {symbol ?? "ALL"}, Side: {side?.ToString() ?? "ALL"}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:320
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error processing order message: {message.MsgType}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:365
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Execution received: {clOrdId} - {execution.Quantity}@{execution.Price}, Status: {order.Status}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:386
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Order cancel rejected: {clOrdId}, Reason: {cxlRejReason}, Text: {text}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Core/OrderManager.cs:515
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Order timeout detected: {order.ClOrdId}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Trading/OrderRouter.cs:92
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No active session for venue {optimalVenue}, trying fallback")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Trading/OrderRouter.cs:106
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_logger.LogInfo($"Order routed to {optimalVenue} in {routingLatency / 1000.0:F1}μs")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/Trading/OrderRouter.cs:286
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No endpoint configuration found for venue: {venue}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/PerformanceLogger.cs:102
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Latency percentiles for {operation}",
            new { P50 = p50.TotalMilliseconds, P95 = p95.TotalMilliseconds, P99 = p99.TotalMilliseconds })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/PerformanceLogger.cs:108
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"High P99 latency for {operation}",
                impact: $"Performance degradation: {p99.TotalMilliseconds}ms P99 latency",
                recommendedAction: "Investigate performance bottleneck")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/PerformanceLogger.cs:163
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Performance threshold exceeded for {operationName}",
                impact: $"Performance degradation: {duration.TotalMilliseconds}ms vs {thresholds.Warning.TotalMilliseconds}ms threshold",
                recommendedAction: "Investigate performance bottleneck",
                additionalData: new { OperationName = operationName, Duration = duration, Threshold = thresholds.Warning, Severity = severity, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:164
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Order rejected: {orderId} for {symbol}", 
            impact: "Trading operation failed", 
            recommendedAction: "Review order parameters and retry",
            additionalData: new { OrderId = orderId, Symbol = symbol, Reason = reason, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:172
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Order cancelled: {orderId} for {symbol} - {reason}",
            new { OrderId = orderId, Symbol = symbol, Reason = reason, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:198
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Strategy signal: {strategyName} generated {signal} for {symbol}",
            new { StrategyName = strategyName, Symbol = symbol, Signal = signal, Confidence = confidence, Reason = reason, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:209
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Strategy performance: {strategyName}",
            new { StrategyName = strategyName, PnL = pnl, SharpeRatio = sharpeRatio, TradesCount = tradesCount, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:229
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Compliance check passed: {complianceType} - {details}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:233
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Compliance check failed: {complianceType} - {details}",
                impact: "Regulatory compliance violation",
                recommendedAction: "Review compliance requirements immediately")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:246
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Latency violation: {operation}",
            impact: $"Performance degradation: {actualLatency.TotalMicroseconds}μs vs {expectedLatency.TotalMicroseconds}μs target",
            recommendedAction: "Investigate performance bottleneck",
            additionalData: new { Operation = operation, ActualLatency = actualLatency, ExpectedLatency = expectedLatency, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:275
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"Trading error in {operation}", exception, operation, "Trading operations impacted", 
            "Check system status and retry operation", context)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:281
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"CRITICAL ERROR in {operation}", exception, operation, "System functionality severely impacted", 
            "Immediate investigation required - escalate to operations team", context)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:287
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Business rule violation: {rule}",
            impact: "Business logic constraint violated",
            recommendedAction: "Review business rules and data integrity",
            additionalData: new { Rule = rule, Details = details, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:295
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"DEBUG: {message}", context, callerName)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:300
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"State transition: {entity} {fromState} → {toState}",
            new { Entity = entity, FromState = fromState, ToState = toState, Reason = reason, CorrelationId = correlationId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:306
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Configuration: {component}", configuration)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:333
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"Database operation failed: {operation} on {table}", 
                operationContext: $"Database {operation}",
                userImpact: "Data operation failed",
                troubleshootingHints: errorMessage ?? "Check database connectivity and permissions",
                additionalData: new { Operation = operation, Table = table, Parameters = parameters })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:356
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Thread operation: {operation}",
            new { Operation = operation, ThreadId = threadId, ThreadName = threadName, Context = context })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:371
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"File operation: {operation} on {filePath}",
                new { Operation = operation, FilePath = filePath, FileSize = fileSize })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:376
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"File operation failed: {operation} on {filePath}",
                operationContext: $"File {operation}",
                troubleshootingHints: errorMessage ?? "Check file permissions and disk space",
                additionalData: new { Operation = operation, FilePath = filePath, FileSize = fileSize })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:392
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"Network operation failed: {operation} to {endpoint}",
                operationContext: $"Network {operation}",
                troubleshootingHints: errorMessage ?? "Check network connectivity",
                additionalData: new { Operation = operation, Endpoint = endpoint, BytesTransferred = bytesTransferred })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:432
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Application event: {eventType} - {description}", metadata)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:437
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"User action: {action} by {userId}",
            new { UserId = userId, Action = action, Parameters = parameters, SessionId = sessionId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:443
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Component lifecycle: {component} → {state}",
            new { Component = component, State = state, Reason = reason, Configuration = configuration })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:451
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Message queue operation: {operation} on {queue}",
                new { Operation = operation, Queue = queue, MessageId = messageId, Message = message })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:456
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"Message queue operation failed: {operation} on {queue}",
                operationContext: $"Message queue {operation}",
                troubleshootingHints: errorMessage ?? "Check message queue connectivity and permissions",
                additionalData: new { Operation = operation, Queue = queue, MessageId = messageId })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:472
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogError($"Scheduled task failed: {taskName}",
                operationContext: "Scheduled task execution",
                troubleshootingHints: errorMessage ?? "Check task configuration and dependencies",
                additionalData: new { TaskName = taskName, ScheduledTime = scheduledTime, ActualTime = actualTime })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:483
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Validation passed: {validationType}", new { Input = input }, callerName)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:487
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogWarning($"Validation failed: {validationType}",
                impact: "Data validation constraint violated",
                recommendedAction: "Review input data and validation rules",
                additionalData: new { Input = input, Errors = errors }, callerName)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/Services/TradingLogger.cs:496
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `_orchestrator.LogInfo($"Configuration change: {component}.{setting}",
            new { Component = component, Setting = setting, OldValue = oldValue, NewValue = newValue, ChangedBy = changedBy })`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Services/RedisMessageBus.cs:74
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"Publish latency exceeded 1ms: {stopwatch.Elapsed.TotalMilliseconds}ms for stream {stream}", 
                    impact: "Performance degradation",
                    recommendedAction: "Consider scaling or optimizing")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Services/RedisMessageBus.cs:79
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Published message {messageId} to stream {stream} in {stopwatch.Elapsed.TotalMicroseconds}μs")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Services/RedisMessageBus.cs:199
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Processed message {messageId} in {processingStopwatch.Elapsed.TotalMicroseconds}μs")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Services/RedisMessageBus.cs:283
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Health check completed: {isHealthy}, latency: {latency.TotalMilliseconds}ms")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/GapCriteria.cs:57
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Gap evaluation for {marketData.Symbol}: Gap={gapPercent:F2}%, Score={result.Score:F2}, Passed={result.Passed}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/GapCriteria.cs:62
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error evaluating gap criteria for {marketData.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/NewsCriteria.cs:70
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"News evaluation for {symbol}: Sentiment={sentimentScore:F2}, Catalyst={hasCatalyst}, Score={result.Score:F2}, Passed={result.Passed}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/NewsCriteria.cs:75
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error evaluating news criteria for {symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/PriceCriteria.cs:66
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Price evaluation for {marketData.Symbol}: Price=${price:F2}, Score={result.Score:F2}, Passed={result.Passed}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/PriceCriteria.cs:71
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error evaluating price criteria for {marketData.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolatilityCriteria.cs:67
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Volatility evaluation for {marketData.Symbol}: ATR=${atr:F2}, Score={result.Score:F2}, Passed={result.Passed}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolatilityCriteria.cs:72
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error evaluating volatility criteria for {marketData?.Symbol ?? "N/A"}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolumeCriteria.cs:64
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Volume evaluation for {marketData.Symbol}: Score={result.Score:F2}, Passed={result.Passed}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolumeCriteria.cs:69
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error evaluating volume criteria for {marketData.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/RealTimeScreeningEngine.cs:47
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Starting batch screening for {request.Symbols.Count} symbols")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/RealTimeScreeningEngine.cs:61
**Type**: StringInterpolation
**Method**: LogWarning
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogWarning($"No market data available for {symbol}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/RealTimeScreeningEngine.cs:86
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Batch screening completed: {finalResults.Count}/{request.Symbols.Count} symbols passed in {stopwatch.ElapsedMilliseconds}ms")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/RealTimeScreeningEngine.cs:99
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Starting real-time screening for {request.Symbols.Count} symbols with {request.UpdateInterval.TotalSeconds}s interval")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/ScreeningOrchestrator.cs:61
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Orchestrated evaluation for {marketData.Symbol}: Score={result.OverallScore:F2}, Passed={result.MeetsCriteria}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/ScreeningOrchestrator.cs:66
**Type**: StringInterpolation
**Method**: LogError
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogError($"Error orchestrating criteria evaluation for {marketData.Symbol}", ex)`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Indicators/TechnicalIndicators.cs:44
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"RSI calculated: {rsi:F2}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Indicators/TechnicalIndicators.cs:58
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"SMAs calculated: SMA20={sma20:F2}, SMA50={sma50:F2}")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Indicators/TechnicalIndicators.cs:78
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"Bollinger position: {position:F2} (Price: {marketData.Price:F2}, Upper: {upperBand:F2}, Lower: {lowerBand:F2})")`

### /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Services/CriteriaConfigurationService.cs:46
**Type**: StringInterpolation
**Method**: LogInfo
**Description**: Using string interpolation instead of structured logging parameters
**Current Code**: `TradingLogOrchestrator.Instance.LogInfo($"No criteria found for key '{key}'. Returning default criteria.")`

