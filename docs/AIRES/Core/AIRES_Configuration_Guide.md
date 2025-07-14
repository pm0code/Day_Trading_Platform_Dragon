# AIRES Configuration Guide

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Complete Configuration Reference

## Table of Contents

1. [Configuration Overview](#configuration-overview)
2. [Primary Configuration (watchdog.ini)](#primary-configuration-watchdogini)
3. [Application Settings (appsettings.json)](#application-settings-appsettingsjson)
4. [Environment Variables](#environment-variables)
5. [AI Model Configuration](#ai-model-configuration)
6. [Kafka Configuration](#kafka-configuration)
7. [Database Configuration](#database-configuration)
8. [Advanced Configuration](#advanced-configuration)

## Configuration Overview

AIRES uses a hierarchical configuration system with multiple sources:

1. **watchdog.ini** - Primary configuration for file monitoring
2. **appsettings.json** - Application-level settings
3. **Environment Variables** - Override any setting
4. **Default Values** - Fallback for missing configuration

### Configuration Priority

```
Environment Variables (Highest)
         ↓
    appsettings.json
         ↓
    watchdog.ini
         ↓
   Default Values (Lowest)
```

## Primary Configuration (watchdog.ini)

**Location**: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/config/watchdog.ini`

### File Structure

```ini
; AIRES Watchdog Configuration
; Last Updated: 2025-01-13

[System]
LogLevel=Information
MaxSimultaneousProjects=5
EnableGlobalMonitoring=true

[AI]
EnableAIAnalysis=true
EnableBookletGeneration=true
BookletOutputDirectory=/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/docs/error-booklets
BuildOutputWatchDirectory=/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/input
ProcessingTimeout=300
MaxRetryAttempts=3

[Monitoring]
EnableAutonomousMonitoring=true
FileFilter=*.txt
PollingIntervalSeconds=10
MaxConcurrentProcessing=3
StaleFileThresholdMinutes=30

[Database]
ConnectionString=Host=localhost;Database=aires;Username=aires;Password=aires123
EnableMigrations=true
CommandTimeout=30

[Kafka]
BootstrapServers=localhost:9092
SecurityProtocol=PLAINTEXT
GroupIdPrefix=aires
EnableAutoCommit=false
```

### Key Settings Explained

| Setting | Purpose | Default | Valid Values |
|---------|---------|---------|--------------|
| LogLevel | Logging verbosity | Information | Trace, Debug, Information, Warning, Error, Critical |
| EnableAIAnalysis | Enable AI pipeline | true | true/false |
| BookletOutputDirectory | Where booklets are saved | - | Valid directory path |
| BuildOutputWatchDirectory | Input file directory | - | Valid directory path |
| FileFilter | File pattern to monitor | *.txt | Any file pattern (*.log, *.txt) |
| MaxConcurrentProcessing | Parallel file processing | 3 | 1-10 |
| ProcessingTimeout | AI timeout (seconds) | 300 | 60-600 |

## Application Settings (appsettings.json)

**Location**: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/DevTools/BuildTools/src/appsettings.json`

### Complete Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "MarketAnalyzer.BuildTools": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=aires_dev;Username=aires_user;Password=aires_pass;Include Error Detail=true"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topics": {
      "FileAcquired": "file-acquired-for-ai",
      "ErrorAnalysis": "ai-input-errors",
      "Stage1Completed": "ai-stage1-completed",
      "Stage2Completed": "ai-stage2-completed",
      "Stage3Completed": "ai-stage3-completed",
      "BookletGenerated": "booklet-generation-completed",
      "DeadLetter": "aires-dead-letter-queue"
    },
    "ConsumerGroups": {
      "MistralWorker": "mistral-worker-group",
      "DeepSeekWorker": "deepseek-worker-group",
      "CodeGemmaWorker": "codegemma-worker-group",
      "Gemma2Worker": "gemma2-worker-group"
    },
    "Producer": {
      "Acks": "All",
      "Retries": 3,
      "RetryBackoffMs": 100,
      "BatchSize": 16384,
      "LingerMs": 10
    },
    "Consumer": {
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false,
      "SessionTimeoutMs": 30000,
      "MaxPollIntervalMs": 300000
    }
  },
  "AIModels": {
    "Mistral": {
      "BaseUrl": "http://localhost:11434",
      "Model": "mistral:7b-instruct-q4_K_M",
      "Temperature": 0.3,
      "MaxTokens": 4096,
      "SystemPrompt": "You are an expert at analyzing Microsoft documentation for C# compiler errors. Provide structured analysis in JSON format."
    },
    "DeepSeek": {
      "ApiKey": "${DEEPSEEK_API_KEY}",
      "BaseUrl": "https://api.deepseek.com/v1",
      "Model": "deepseek-coder",
      "Temperature": 0.5,
      "MaxTokens": 4096,
      "SystemPrompt": "You are a C# expert analyzing code context around compiler errors. Focus on root causes and debugging approaches."
    },
    "Gemini": {
      "ApiKey": "${GEMINI_API_KEY}",
      "Model": "gemini-2.5-flash",
      "Temperature": 0.4,
      "MaxTokens": 8192,
      "SystemPrompt": "You are validating code against MarketAnalyzer canonical patterns and architectural standards."
    }
  },
  "OutboxRelay": {
    "ProcessorId": "aires-outbox-processor",
    "PollingInterval": "00:00:05",
    "BatchSize": 100,
    "MaxRetries": 3
  },
  "FileProcessing": {
    "ArchiveDirectory": "./Archive",
    "ErrorDirectory": "./Error",
    "MaxFileSize": 10485760,
    "SupportedExtensions": [".txt", ".log", ".out"],
    "FileRetentionDays": 30
  },
  "HealthChecks": {
    "DatabaseCheck": {
      "Enabled": true,
      "Interval": "00:00:30"
    },
    "KafkaCheck": {
      "Enabled": true,
      "Topic": "health-check-topic"
    },
    "OllamaCheck": {
      "Enabled": true,
      "Endpoint": "http://localhost:11434/api/tags"
    }
  }
}
```

## Environment Variables

### Required Environment Variables

```bash
# AI API Keys
export GEMINI_API_KEY="your-gemini-api-key"
export DEEPSEEK_API_KEY="your-deepseek-api-key"

# Database (overrides appsettings.json)
export ConnectionStrings__DefaultConnection="Host=prod-db;Database=aires;Username=aires;Password=secure"

# Kafka (overrides appsettings.json)
export Kafka__BootstrapServers="kafka1:9092,kafka2:9092,kafka3:9092"

# Logging
export Logging__LogLevel__Default="Debug"
```

### Optional Environment Variables

```bash
# Performance tuning
export FileProcessing__MaxConcurrentProcessing="5"
export AIModels__Mistral__Temperature="0.2"
export Kafka__Consumer__MaxPollIntervalMs="600000"

# Directory overrides
export AI__BookletOutputDirectory="/custom/output/path"
export AI__BuildOutputWatchDirectory="/custom/input/path"

# Feature flags
export AI__EnableAIAnalysis="true"
export Monitoring__EnableAutonomousMonitoring="true"
```

## AI Model Configuration

### Mistral Configuration

```json
{
  "Mistral": {
    "BaseUrl": "http://localhost:11434",
    "Model": "mistral:7b-instruct-q4_K_M",
    "Temperature": 0.3,
    "MaxTokens": 4096,
    "SystemPrompt": "...",
    "ResponseFormat": "json",
    "Timeout": 120,
    "RetryPolicy": {
      "MaxAttempts": 3,
      "BackoffMultiplier": 2,
      "InitialDelay": 1000
    }
  }
}
```

**Key Parameters**:
- **Temperature**: 0.3 (focused, deterministic responses)
- **Model**: Quantized 4-bit version for efficiency
- **ResponseFormat**: JSON for structured parsing

### DeepSeek Configuration

```json
{
  "DeepSeek": {
    "ApiKey": "${DEEPSEEK_API_KEY}",
    "BaseUrl": "https://api.deepseek.com/v1",
    "Model": "deepseek-coder",
    "Temperature": 0.5,
    "MaxTokens": 4096,
    "RateLimit": {
      "RequestsPerMinute": 60,
      "TokensPerMinute": 40000
    }
  }
}
```

**Rate Limiting**:
- Automatic retry with backoff
- Queue overflow protection
- Token budget management

### Gemini Configuration

```json
{
  "Gemini": {
    "ApiKey": "${GEMINI_API_KEY}",
    "Model": "gemini-2.5-flash",
    "Temperature": 0.4,
    "MaxTokens": 8192,
    "SafetySettings": [
      {
        "category": "HARM_CATEGORY_DANGEROUS_CONTENT",
        "threshold": "BLOCK_NONE"
      }
    ]
  }
}
```

**Safety Settings**: Disabled for code analysis (no harmful content in compiler errors)

## Kafka Configuration

### Topic Configuration

```json
{
  "Topics": {
    "FileAcquired": {
      "Name": "file-acquired-for-ai",
      "Partitions": 3,
      "ReplicationFactor": 1,
      "RetentionMs": 604800000
    },
    "ErrorAnalysis": {
      "Name": "ai-input-errors",
      "Partitions": 6,
      "ReplicationFactor": 1,
      "RetentionMs": 604800000
    }
  }
}
```

### Consumer Configuration

```json
{
  "Consumer": {
    "AutoOffsetReset": "Earliest",
    "EnableAutoCommit": false,
    "SessionTimeoutMs": 30000,
    "MaxPollIntervalMs": 300000,
    "IsolationLevel": "ReadCommitted",
    "MaxPollRecords": 10
  }
}
```

**Important Settings**:
- **EnableAutoCommit**: false (manual commit for reliability)
- **MaxPollIntervalMs**: 5 minutes (allows for slow AI processing)
- **IsolationLevel**: ReadCommitted (transactional guarantees)

### Producer Configuration

```json
{
  "Producer": {
    "Acks": "All",
    "Retries": 3,
    "RetryBackoffMs": 100,
    "BatchSize": 16384,
    "LingerMs": 10,
    "CompressionType": "Snappy",
    "EnableIdempotence": true
  }
}
```

**Reliability Features**:
- **Acks**: All (wait for all replicas)
- **EnableIdempotence**: Exactly-once semantics
- **CompressionType**: Snappy for efficiency

## Database Configuration

### Connection String Format

```
Host=localhost;Port=5432;Database=aires_dev;Username=aires_user;Password=aires_pass;Include Error Detail=true;Command Timeout=30;Maximum Pool Size=100
```

### Connection Parameters

| Parameter | Purpose | Default | Recommended |
|-----------|---------|---------|-------------|
| Host | PostgreSQL server | localhost | Your DB server |
| Port | PostgreSQL port | 5432 | 5432 |
| Database | Database name | aires_dev | aires_prod |
| Username | DB user | aires_user | Service account |
| Password | DB password | - | Strong password |
| Include Error Detail | Detailed errors | false | true (dev), false (prod) |
| Command Timeout | Query timeout | 30 | 30-60 |
| Maximum Pool Size | Connection pool | 100 | Based on load |

### Migration Settings

```json
{
  "Database": {
    "EnableAutoMigration": true,
    "MigrationTimeout": 300,
    "BackupBeforeMigration": true,
    "MigrationHistoryTable": "__AIRESMigrationHistory"
  }
}
```

## Advanced Configuration

### Performance Tuning

```ini
[Performance]
; File Processing
MaxConcurrentFiles=5
FileProcessingTimeout=300
UseMemoryCache=true
CacheSizeMB=512

; AI Pipeline
EnableParallelAI=true
MaxParallelAIRequests=3
AIRequestTimeout=120
EnableAIResponseCache=true

; Database
ConnectionPoolSize=50
CommandTimeout=30
EnableQueryCache=true
```

### Monitoring Configuration

```ini
[Monitoring]
; Metrics
EnableMetrics=true
MetricsPort=9090
MetricsPath=/metrics

; Tracing
EnableTracing=true
TracingEndpoint=http://localhost:4317
TracingSampleRate=0.1

; Logging
LogToFile=true
LogFilePath=./logs/aires.log
MaxLogFileSize=100MB
MaxLogFiles=10
```

### Security Configuration

```ini
[Security]
; API Security
RequireApiKey=true
ApiKeyHeader=X-AIRES-API-Key
EnableRateLimiting=true
RateLimitPerMinute=60

; File System Security
RestrictFileAccess=true
AllowedFileExtensions=.txt,.log,.out
MaxFileSizeBytes=10485760
ScanForMalware=false

; Network Security
EnableTLS=true
TLSCertPath=./certs/aires.crt
TLSKeyPath=./certs/aires.key
```

## Configuration Validation

### Startup Validation

AIRES validates configuration on startup:

```csharp
// Automatic validation checks:
- Input directory exists and is readable
- Output directory exists and is writable
- Kafka connection successful
- Database connection successful
- AI models accessible
- Required environment variables set
```

### Configuration Health Check

```bash
# Test configuration
cd /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/DevTools/BuildTools/src
dotnet run -- --validate-config

# Expected output:
✅ Configuration loaded successfully
✅ Input directory accessible
✅ Output directory writable
✅ Kafka connection verified
✅ Database connection verified
✅ Ollama accessible
✅ Gemini API key valid
```

### Common Configuration Issues

1. **Missing API Keys**
   ```bash
   export GEMINI_API_KEY="your-key"
   export DEEPSEEK_API_KEY="your-key"
   ```

2. **Directory Permissions**
   ```bash
   chmod 755 /path/to/input
   chmod 755 /path/to/output
   ```

3. **Kafka Not Running**
   ```bash
   docker-compose up -d kafka zookeeper
   ```

4. **Database Connection Failed**
   ```bash
   psql -h localhost -U aires_user -d aires_dev
   ```

## Configuration Best Practices

1. **Use Environment Variables for Secrets**
   - Never commit API keys
   - Use key vault in production
   - Rotate keys regularly

2. **Tune for Your Workload**
   - Adjust concurrent processing
   - Set appropriate timeouts
   - Monitor resource usage

3. **Enable Monitoring**
   - Use structured logging
   - Export metrics
   - Set up alerts

4. **Regular Backups**
   - Backup configuration files
   - Document custom settings
   - Version control changes

---

**Next**: [Development Guide](../Implementation/AIRES_Development_Guide.md)