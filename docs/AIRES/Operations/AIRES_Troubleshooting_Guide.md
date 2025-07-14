# AIRES Troubleshooting Guide

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Complete Troubleshooting Reference

## Table of Contents

1. [Troubleshooting Overview](#troubleshooting-overview)
2. [Common Issues](#common-issues)
3. [Startup Problems](#startup-problems)
4. [Processing Issues](#processing-issues)
5. [AI Model Problems](#ai-model-problems)
6. [Infrastructure Issues](#infrastructure-issues)
7. [Performance Problems](#performance-problems)
8. [Diagnostic Commands](#diagnostic-commands)

## Troubleshooting Overview

This guide provides systematic approaches to diagnose and resolve AIRES issues. Always follow the diagnostic steps before attempting fixes.

### Troubleshooting Principles

1. **Check Logs First**: Most issues are explained in logs
2. **Verify Configuration**: Many issues are config-related
3. **Test Components Individually**: Isolate the problem
4. **Document Findings**: Help future troubleshooting

### Quick Diagnostic Checklist

```bash
# 1. Is AIRES running?
ps aux | grep "dotnet.*BuildTools"

# 2. Check recent errors
tail -50 aires.log | grep ERROR

# 3. Verify infrastructure
docker ps | grep -E "kafka|postgres"
curl http://localhost:11434/api/tags

# 4. Check disk space
df -h | grep -E "/$|input|output"

# 5. Verify configuration
cat appsettings.json | jq '.ConnectionStrings'
```

## Common Issues

### Issue: AIRES Not Processing Files

**Symptoms**:
- Files remain in input directory
- No new booklets generated
- No processing activity in logs

**Diagnostic Steps**:
```bash
# 1. Check if AIRES is monitoring
tail -f aires.log | grep -E "FileWatcher|Detected|Processing"

# 2. Verify input directory
ls -la /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/input/

# 3. Check file permissions
ls -la /path/to/input/test_file.txt

# 4. Verify file filter
grep FileFilter appsettings.json
```

**Solutions**:

1. **Wrong Directory**:
   ```bash
   # Update configuration
   export AI__BuildOutputWatchDirectory="/correct/path"
   # Restart AIRES
   ```

2. **File Permissions**:
   ```bash
   chmod 644 /path/to/input/*.txt
   chown aires:aires /path/to/input/*.txt
   ```

3. **File Filter Mismatch**:
   ```json
   // Update appsettings.json
   {
     "Monitoring": {
       "FileFilter": "*.txt"  // or "*.log"
     }
   }
   ```

4. **Files in Subdirectories**:
   ```bash
   # Move files to root of input directory
   mv /input/Error/*.txt /input/
   ```

### Issue: Booklets Not Generated

**Symptoms**:
- Files disappear from input
- No booklets in output directory
- Processing starts but doesn't complete

**Diagnostic Steps**:
```bash
# 1. Check for processing errors
grep -A 10 -B 10 "ERROR" aires.log | tail -50

# 2. Verify AI services
curl http://localhost:11434/api/tags
echo $GEMINI_API_KEY

# 3. Check database
psql -U aires_user -d aires_dev -c "SELECT * FROM FileProcessingRecords ORDER BY DetectedAt DESC LIMIT 5;"

# 4. Check Kafka
kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group mistral-worker-group
```

**Solutions**:

1. **AI Service Down**:
   ```bash
   # Restart Ollama
   systemctl restart ollama
   # or
   ollama serve
   
   # Verify model loaded
   ollama run mistral:7b-instruct-q4_K_M "test"
   ```

2. **API Key Issues**:
   ```bash
   # Set correct API key
   export GEMINI_API_KEY="your-valid-key"
   
   # Test API
   curl -X POST \
     "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$GEMINI_API_KEY" \
     -H 'Content-Type: application/json' \
     -d '{"contents":[{"parts":[{"text":"test"}]}]}'
   ```

3. **Database Issues**:
   ```sql
   -- Check stuck records
   SELECT FileName, State, ErrorMessage 
   FROM FileProcessingRecords 
   WHERE State NOT IN (4, 5, 8)  -- Not completed/archived
   ORDER BY DetectedAt DESC;
   
   -- Reset stuck files
   UPDATE FileProcessingRecords 
   SET State = 0 
   WHERE State = 3 AND ProcessingStartedAt < NOW() - INTERVAL '1 hour';
   ```

## Startup Problems

### Issue: AIRES Won't Start

**Symptoms**:
- Process exits immediately
- No output or error messages
- Service fails to start

**Diagnostic Steps**:
```bash
# 1. Run in foreground
cd /path/to/aires
dotnet run

# 2. Check build
dotnet build

# 3. Verify dependencies
dotnet restore

# 4. Check configuration
dotnet run -- --validate-config
```

**Solutions**:

1. **Missing Dependencies**:
   ```bash
   # Install .NET SDK
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --version 8.0
   
   # Restore packages
   dotnet restore
   ```

2. **Configuration Errors**:
   ```json
   // Minimal valid appsettings.json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information"
       }
     },
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=aires;Username=aires;Password=aires"
     }
   }
   ```

3. **Port Already in Use**:
   ```bash
   # Find process using port
   lsof -i :5000
   
   # Use different port
   export ASPNETCORE_URLS="http://localhost:5001"
   ```

### Issue: Configuration Not Loading

**Symptoms**:
- Default values used instead of configured
- "Configuration not found" errors
- Features not working as expected

**Diagnostic Steps**:
```bash
# 1. Check file exists
ls -la appsettings.json
ls -la /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/config/watchdog.ini

# 2. Validate JSON
jq . appsettings.json

# 3. Check environment variables
env | grep -E "GEMINI|AIRES|ConnectionStrings"
```

**Solutions**:

1. **Fix JSON Syntax**:
   ```bash
   # Validate and format
   jq . appsettings.json > appsettings.json.tmp
   mv appsettings.json.tmp appsettings.json
   ```

2. **Set Working Directory**:
   ```bash
   cd /path/to/aires/src
   dotnet run
   ```

3. **Use Absolute Paths**:
   ```json
   {
     "AI": {
       "BookletOutputDirectory": "/absolute/path/to/output"
     }
   }
   ```

## Processing Issues

### Issue: Files Stuck in Processing

**Symptoms**:
- Files remain in "InProgress" state
- No completion or error
- Processing seems frozen

**Diagnostic Steps**:
```bash
# 1. Check file states
psql -U aires_user -d aires_dev -c \
  "SELECT FileName, State, ProcessingStartedAt, ErrorMessage 
   FROM FileProcessingRecords 
   WHERE State = 3;"

# 2. Check for deadlocks
grep -i "deadlock\|timeout" aires.log

# 3. Monitor AI calls
tail -f aires.log | grep -E "Mistral|DeepSeek|Gemini"
```

**Solutions**:

1. **Reset Stuck Files**:
   ```sql
   -- Mark for retry
   UPDATE FileProcessingRecords 
   SET State = 0, RetryCount = RetryCount + 1 
   WHERE State = 3 
   AND ProcessingStartedAt < NOW() - INTERVAL '30 minutes';
   ```

2. **Increase Timeouts**:
   ```json
   {
     "AIModels": {
       "Mistral": {
         "Timeout": 300  // 5 minutes
       }
     }
   }
   ```

3. **Clear Queue**:
   ```bash
   # Reset Kafka consumer group
   kafka-consumer-groups.sh \
     --bootstrap-server localhost:9092 \
     --group mistral-worker-group \
     --reset-offsets \
     --to-earliest \
     --execute \
     --topic ai-input-errors
   ```

### Issue: Duplicate Processing

**Symptoms**:
- Same file processed multiple times
- Duplicate booklets generated
- Warnings about existing files

**Diagnostic Steps**:
```bash
# 1. Check for duplicate records
psql -U aires_user -d aires_dev -c \
  "SELECT FileName, COUNT(*) 
   FROM FileProcessingRecords 
   GROUP BY FileName 
   HAVING COUNT(*) > 1;"

# 2. Check file watcher
grep "OnFileCreated.*triggered" aires.log | tail -20
```

**Solutions**:

1. **Fix Atomic Acquisition**:
   ```csharp
   // Ensure atomic file acquisition in code
   if (!_fileStates.TryAdd(fileName, new FileTrackingInfo()))
   {
       LogWarning($"File {fileName} already being processed");
       return;
   }
   ```

2. **Clean Duplicate Records**:
   ```sql
   -- Remove duplicates, keep oldest
   DELETE FROM FileProcessingRecords a
   USING FileProcessingRecords b
   WHERE a.Id > b.Id 
   AND a.FileName = b.FileName;
   ```

## AI Model Problems

### Issue: Ollama Not Responding

**Symptoms**:
- "Connection refused" errors
- Timeouts on Mistral calls
- Ollama service down

**Diagnostic Steps**:
```bash
# 1. Check Ollama service
systemctl status ollama
curl http://localhost:11434/api/tags

# 2. Check if model loaded
ollama list

# 3. Test model directly
ollama run mistral:7b-instruct-q4_K_M "Hello"
```

**Solutions**:

1. **Start Ollama**:
   ```bash
   # Start service
   ollama serve &
   
   # Or with systemd
   sudo systemctl start ollama
   ```

2. **Pull Missing Model**:
   ```bash
   ollama pull mistral:7b-instruct-q4_K_M
   ```

3. **Memory Issues**:
   ```bash
   # Check available memory
   free -h
   
   # Use smaller model
   ollama pull mistral:7b-instruct-q2_K
   ```

### Issue: Gemini API Errors

**Symptoms**:
- 401 Unauthorized
- 429 Rate limit exceeded
- 500 Internal server errors

**Diagnostic Steps**:
```bash
# 1. Test API key
curl -X POST \
  "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$GEMINI_API_KEY" \
  -H 'Content-Type: application/json' \
  -d '{"contents":[{"parts":[{"text":"test"}]}]}' \
  -v

# 2. Check rate limits
grep "Gemini.*429" aires.log | wc -l
```

**Solutions**:

1. **Fix API Key**:
   ```bash
   # Verify key format
   echo $GEMINI_API_KEY | wc -c  # Should be ~40 chars
   
   # Set correct key
   export GEMINI_API_KEY="AIza..."
   ```

2. **Handle Rate Limits**:
   ```json
   {
     "AIModels": {
       "Gemini": {
         "RateLimitDelay": 2000,  // ms between requests
         "MaxRetries": 5
       }
     }
   }
   ```

3. **Use Fallback**:
   ```bash
   # Configure alternative model
   export AI__FallbackModel="ollama:gemma2"
   ```

## Infrastructure Issues

### Issue: Kafka Connection Failed

**Symptoms**:
- "Broker transport failure" errors
- Consumer not receiving messages
- Producer timeouts

**Diagnostic Steps**:
```bash
# 1. Check Kafka status
docker ps | grep kafka
kafka-broker-api-versions.sh --bootstrap-server localhost:9092

# 2. List topics
kafka-topics.sh --list --bootstrap-server localhost:9092

# 3. Check consumer groups
kafka-consumer-groups.sh --list --bootstrap-server localhost:9092
```

**Solutions**:

1. **Start Kafka**:
   ```bash
   docker-compose up -d zookeeper kafka
   
   # Wait for startup
   sleep 30
   
   # Create topics
   kafka-topics.sh --create \
     --topic ai-input-errors \
     --bootstrap-server localhost:9092 \
     --partitions 3 \
     --replication-factor 1
   ```

2. **Fix Connection String**:
   ```json
   {
     "Kafka": {
       "BootstrapServers": "localhost:9092"
     }
   }
   ```

3. **Reset Consumer Group**:
   ```bash
   kafka-consumer-groups.sh \
     --bootstrap-server localhost:9092 \
     --group mistral-worker-group \
     --delete
   ```

### Issue: Database Connection Failed

**Symptoms**:
- "Connection refused" to PostgreSQL
- "Database does not exist" errors
- Migration failures

**Diagnostic Steps**:
```bash
# 1. Check PostgreSQL
docker ps | grep postgres
psql -U postgres -h localhost -c "SELECT 1;"

# 2. Check database exists
psql -U postgres -h localhost -l | grep aires

# 3. Test connection
psql -U aires_user -d aires_dev -h localhost -c "SELECT NOW();"
```

**Solutions**:

1. **Start PostgreSQL**:
   ```bash
   docker run -d \
     --name postgres \
     -e POSTGRES_USER=aires_user \
     -e POSTGRES_PASSWORD=aires_pass \
     -e POSTGRES_DB=aires_dev \
     -p 5432:5432 \
     postgres:15
   ```

2. **Create Database**:
   ```sql
   -- As postgres user
   CREATE DATABASE aires_dev;
   CREATE USER aires_user WITH PASSWORD 'aires_pass';
   GRANT ALL PRIVILEGES ON DATABASE aires_dev TO aires_user;
   ```

3. **Run Migrations**:
   ```bash
   cd /path/to/aires/src
   dotnet ef database update
   ```

## Performance Problems

### Issue: Slow Processing

**Symptoms**:
- Files take >10 minutes to process
- High CPU/memory usage
- System appears sluggish

**Diagnostic Steps**:
```bash
# 1. Check resource usage
top -p $(pgrep -f "MarketAnalyzer.BuildTools")

# 2. Analyze processing times
grep "Processing completed in" aires.log | \
  awk '{print $NF}' | sort -n | tail -20

# 3. Check concurrent processing
grep "concurrent.*processing" aires.log
```

**Solutions**:

1. **Reduce Concurrency**:
   ```json
   {
     "FileProcessing": {
       "MaxConcurrentProcessing": 2  // Reduce from 5
     }
   }
   ```

2. **Optimize AI Calls**:
   ```json
   {
     "AIModels": {
       "Mistral": {
         "MaxTokens": 2048,  // Reduce from 4096
         "Temperature": 0.1   // More deterministic
       }
     }
   }
   ```

3. **Enable Caching**:
   ```json
   {
     "Caching": {
       "EnableAIResponseCache": true,
       "CacheDurationMinutes": 60
     }
   }
   ```

### Issue: Memory Leaks

**Symptoms**:
- Memory usage grows continuously
- Out of memory errors
- Process crashes after running for hours

**Diagnostic Steps**:
```bash
# 1. Monitor memory over time
while true; do
  ps -p $(pgrep -f "MarketAnalyzer.BuildTools") -o rss= | \
    awk '{print strftime("%H:%M:%S"), $1/1024 "MB"}'
  sleep 60
done

# 2. Check for large objects
dotnet-dump collect -p $(pgrep -f "MarketAnalyzer.BuildTools")
dotnet-dump analyze core_file
```

**Solutions**:

1. **Force Garbage Collection**:
   ```bash
   export DOTNET_gcServer=true
   export DOTNET_GCHeapHardLimit=1073741824  # 1GB
   ```

2. **Restart Periodically**:
   ```bash
   # Add to cron
   0 3 * * * systemctl restart aires
   ```

3. **Fix Code Issues**:
   ```csharp
   // Ensure proper disposal
   using var httpClient = new HttpClient();
   // Instead of creating new clients repeatedly
   ```

## Diagnostic Commands

### Complete System Check

```bash
#!/bin/bash
# aires-diagnostic.sh

echo "=== AIRES Diagnostic Report ==="
echo "Generated: $(date)"
echo

echo "1. Process Status:"
if pgrep -f "MarketAnalyzer.BuildTools" > /dev/null; then
    PID=$(pgrep -f "MarketAnalyzer.BuildTools")
    echo "✅ Running (PID: $PID)"
    ps -p $PID -o pid,vsz,rss,pcpu,pmem,etime
else
    echo "❌ Not running"
fi
echo

echo "2. Recent Errors:"
grep "ERROR" aires.log | tail -5
echo

echo "3. Infrastructure:"
echo -n "PostgreSQL: "
pg_isready -h localhost -p 5432 && echo "✅ Ready" || echo "❌ Down"

echo -n "Kafka: "
timeout 5 kafka-broker-api-versions.sh --bootstrap-server localhost:9092 > /dev/null 2>&1 && \
  echo "✅ Ready" || echo "❌ Down"

echo -n "Ollama: "
curl -s http://localhost:11434/api/tags > /dev/null && echo "✅ Ready" || echo "❌ Down"
echo

echo "4. Disk Space:"
df -h | grep -E "/$|input|output"
echo

echo "5. Processing Stats:"
echo "Files in input: $(ls /path/to/input/*.txt 2>/dev/null | wc -l)"
echo "Booklets today: $(ls /path/to/booklets/$(date +%Y-%m-%d)/*.md 2>/dev/null | wc -l)"
echo

echo "6. Configuration:"
echo "Input Dir: $(grep BuildOutputWatchDirectory appsettings.json | cut -d'"' -f4)"
echo "Output Dir: $(grep BookletOutputDirectory appsettings.json | cut -d'"' -f4)"
echo "Max Concurrent: $(grep MaxConcurrentProcessing appsettings.json | grep -o '[0-9]*')"
```

### Quick Fix Script

```bash
#!/bin/bash
# aires-quick-fix.sh

echo "Attempting AIRES quick fix..."

# 1. Clear stuck files
psql -U aires_user -d aires_dev -c \
  "UPDATE FileProcessingRecords SET State = 0 WHERE State = 3;"

# 2. Restart services
systemctl restart aires

# 3. Verify
sleep 10
if pgrep -f "MarketAnalyzer.BuildTools" > /dev/null; then
    echo "✅ AIRES is running"
    
    # 4. Test with sample file
    echo "CS0246: Type or namespace not found" > /tmp/test_error.txt
    mv /tmp/test_error.txt /path/to/input/
    echo "📝 Test file submitted"
else
    echo "❌ AIRES failed to start"
fi
```

---

**Next**: [Current Status](../Status/AIRES_Current_Status.md)