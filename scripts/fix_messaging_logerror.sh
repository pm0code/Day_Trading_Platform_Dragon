#!/bin/bash
# Fix LogError parameter order in Messaging project

echo "Fixing LogError parameter order in Messaging project..."

# Fix RedisMessageBus.cs
sed -i 's/LogError("Redis error publishing to stream {Stream}", stream, ex)/LogError("Redis error publishing to stream {Stream}", ex, "Redis publish", null, null, new { Stream = stream })/g' TradingPlatform.Messaging/Services/RedisMessageBus.cs

sed -i 's/LogError("JSON serialization error for stream {Stream}", stream, ex)/LogError("JSON serialization error for stream {Stream}", ex, "JSON serialization", null, null, new { Stream = stream })/g' TradingPlatform.Messaging/Services/RedisMessageBus.cs

sed -i 's/LogError("Redis error subscribing to stream {Stream}", stream, ex)/LogError("Redis error subscribing to stream {Stream}", ex, "Redis subscribe", null, null, new { Stream = stream })/g' TradingPlatform.Messaging/Services/RedisMessageBus.cs

sed -i 's/LogError("Redis error in consume loop for {ConsumerGroup}:{ConsumerName} on {Stream}", consumerGroup, consumerName, stream, ex)/LogError("Redis error in consume loop for {ConsumerGroup}:{ConsumerName} on {Stream}", ex, "Redis consume", null, null, new { ConsumerGroup = consumerGroup, ConsumerName = consumerName, Stream = stream })/g' TradingPlatform.Messaging/Services/RedisMessageBus.cs

# Fix ServiceCollectionExtensions.cs  
sed -i 's/LogError("Redis connection failed: {EndPoint} - {FailureType}", endpoint, failureType)/LogError("Redis connection failed", null, "Redis connection", null, null, new { EndPoint = endpoint?.ToString(), FailureType = failureType.ToString() })/g' TradingPlatform.Messaging/Extensions/ServiceCollectionExtensions.cs

sed -i 's/LogError("Redis error occurred: {EndPoint}", endpoint, ex)/LogError("Redis error occurred", ex, "Redis operation", null, null, new { EndPoint = endpoint?.ToString() })/g' TradingPlatform.Messaging/Extensions/ServiceCollectionExtensions.cs

sed -i 's/LogError("Redis internal error: {Origin}", origin, ex)/LogError("Redis internal error", ex, "Redis internal", null, null, new { Origin = origin })/g' TradingPlatform.Messaging/Extensions/ServiceCollectionExtensions.cs

# Fix LogInformation calls - these should be LogInfo
sed -i 's/LogInformation(/LogInfo(/g' TradingPlatform.Messaging/Extensions/ServiceCollectionExtensions.cs

# Fix LogWarning calls that have wrong parameter order
sed -i 's/LogWarning("{LatencyMs}ms Redis latency for stream {Stream}", latencyMs, stream)/LogWarning("High Redis latency detected", "{LatencyMs}ms for stream {Stream}", "Consider scaling Redis", new { LatencyMs = latencyMs, Stream = stream })/g' TradingPlatform.Messaging/Services/RedisMessageBus.cs

echo "Fixing complete!"