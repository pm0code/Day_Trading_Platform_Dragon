# AIRES Operations Manual

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Complete Operations Guide

## Table of Contents

1. [Operations Overview](#operations-overview)
2. [Starting and Stopping AIRES](#starting-and-stopping-aires)
3. [Monitoring AIRES](#monitoring-aires)
4. [Maintenance Tasks](#maintenance-tasks)
5. [Backup and Recovery](#backup-and-recovery)
6. [Performance Tuning](#performance-tuning)
7. [Security Operations](#security-operations)
8. [Incident Response](#incident-response)

## Operations Overview

AIRES is designed for 24/7 autonomous operation with minimal manual intervention. This manual covers all operational aspects of running AIRES in production.

### Operational Principles

1. **Autonomous by Design**: Self-monitoring and self-healing
2. **Observable**: Comprehensive logging and metrics
3. **Resilient**: Handles failures gracefully
4. **Scalable**: Supports horizontal scaling

### Key Operational Metrics

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Availability | 99.9% | < 99.5% |
| Processing Time | < 5 min | > 10 min |
| Error Rate | < 1% | > 5% |
| Queue Depth | < 100 | > 500 |

## Starting and Stopping AIRES

### Starting AIRES

#### Method 1: Direct Execution
```bash
cd /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/DevTools/BuildTools/src

# Foreground execution (for testing)
dotnet run

# Background execution (production)
nohup dotnet run > aires.log 2>&1 &

# Save PID for later
echo $! > aires.pid
```

#### Method 2: Systemd Service (Linux)
```bash
# Create service file
sudo nano /etc/systemd/system/aires.service
```

```ini
[Unit]
Description=AIRES - AI Error Resolution System
After=network.target postgresql.service kafka.service

[Service]
Type=simple
User=aires
WorkingDirectory=/opt/aires
ExecStart=/usr/bin/dotnet /opt/aires/MarketAnalyzer.BuildTools.dll
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="GEMINI_API_KEY=your-key"

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start
sudo systemctl enable aires
sudo systemctl start aires
sudo systemctl status aires
```

#### Method 3: Docker Container
```bash
# Build image
docker build -t aires:latest .

# Run container
docker run -d \
  --name aires \
  -e GEMINI_API_KEY=$GEMINI_API_KEY \
  -e DEEPSEEK_API_KEY=$DEEPSEEK_API_KEY \
  -v /path/to/input:/app/input \
  -v /path/to/output:/app/output \
  --network host \
  aires:latest
```

### Stopping AIRES

#### Graceful Shutdown
```bash
# If running with PID file
kill -SIGTERM $(cat aires.pid)

# If using systemd
sudo systemctl stop aires

# If using Docker
docker stop aires --time=30
```

#### Emergency Stop
```bash
# Force kill (use only if graceful shutdown fails)
kill -9 $(cat aires.pid)

# Or find and kill
pkill -f "MarketAnalyzer.BuildTools"
```

### Verifying Startup

```bash
# Check process
ps aux | grep -E "dotnet.*BuildTools"

# Check logs
tail -f aires.log | grep -E "Started|Ready|Error"

# Check health endpoint (if configured)
curl http://localhost:5000/health

# Check input directory monitoring
ls -la /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/input/
```

## Monitoring AIRES

### Process Monitoring

#### Check Process Status
```bash
#!/bin/bash
# aires-status.sh

PID=$(pgrep -f "MarketAnalyzer.BuildTools")
if [ -z "$PID" ]; then
    echo "❌ AIRES is NOT running"
    exit 1
else
    echo "✅ AIRES is running (PID: $PID)"
    
    # Check resource usage
    ps -p $PID -o pid,vsz,rss,pcpu,pmem,comm
    
    # Check open files
    lsof -p $PID | wc -l
fi
```

#### Monitor Resource Usage
```bash
# Real-time monitoring
top -p $(pgrep -f "MarketAnalyzer.BuildTools")

# Or using htop
htop -p $(pgrep -f "MarketAnalyzer.BuildTools")

# Memory usage over time
while true; do
    ps -p $(pgrep -f "MarketAnalyzer.BuildTools") -o rss= | \
    awk '{print strftime("%Y-%m-%d %H:%M:%S"), $1/1024 " MB"}'
    sleep 60
done
```

### Log Monitoring

#### Real-time Log Monitoring
```bash
# Follow main log
tail -f aires.log

# Monitor errors only
tail -f aires.log | grep -E "ERROR|CRITICAL|Exception"

# Monitor processing activity
tail -f aires.log | grep -E "Processing|Completed|Generated"

# Structured log parsing (if using JSON logs)
tail -f aires.log | jq 'select(.Level >= "Warning")'
```

#### Log Analysis
```bash
# Count errors by type
grep "ERROR" aires.log | awk -F'[' '{print $3}' | sort | uniq -c | sort -nr

# Processing statistics
grep "Booklet generated" aires.log | wc -l
echo "Total booklets: $(ls /path/to/booklets/*/*.md | wc -l)"

# Average processing time
grep "Processing completed in" aires.log | \
awk '{sum+=$5; count++} END {print "Average:", sum/count, "seconds"}'
```

### Queue Monitoring

#### Kafka Queue Depth
```bash
# Check consumer lag
kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 \
  --describe \
  --group aires-worker-group

# Monitor all AIRES topics
for topic in $(kafka-topics.sh --list --bootstrap-server localhost:9092 | grep aires); do
    echo "Topic: $topic"
    kafka-run-class.sh kafka.tools.GetOffsetShell \
      --broker-list localhost:9092 \
      --topic $topic \
      --time -1
done
```

### Database Monitoring

#### PostgreSQL Health
```bash
# Check connections
psql -U aires_user -d aires_dev -c \
  "SELECT count(*) as connections, state 
   FROM pg_stat_activity 
   WHERE datname = 'aires_dev' 
   GROUP BY state;"

# Check table sizes
psql -U aires_user -d aires_dev -c \
  "SELECT schemaname, tablename, 
          pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
   FROM pg_tables 
   WHERE schemaname = 'public' 
   ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;"

# Check slow queries
psql -U aires_user -d aires_dev -c \
  "SELECT query, mean_exec_time, calls 
   FROM pg_stat_statements 
   WHERE mean_exec_time > 1000 
   ORDER BY mean_exec_time DESC 
   LIMIT 10;"
```

### AI Model Monitoring

#### Ollama Status
```bash
# Check Ollama service
curl http://localhost:11434/api/tags

# Check loaded models
ollama list

# Monitor model usage
curl http://localhost:11434/api/show -d '{"name":"mistral:7b-instruct-q4_K_M"}'
```

#### API Health Checks
```bash
# Gemini API status
curl -X POST \
  "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$GEMINI_API_KEY" \
  -H 'Content-Type: application/json' \
  -d '{"contents":[{"parts":[{"text":"test"}]}]}'

# Check rate limits
# (Response headers include X-RateLimit-Remaining)
```

## Maintenance Tasks

### Daily Maintenance

#### 1. Check System Health
```bash
#!/bin/bash
# daily-health-check.sh

echo "=== AIRES Daily Health Check ==="
echo "Date: $(date)"

# Process check
if pgrep -f "MarketAnalyzer.BuildTools" > /dev/null; then
    echo "✅ AIRES process: Running"
else
    echo "❌ AIRES process: NOT RUNNING"
fi

# Disk space
echo -e "\nDisk Usage:"
df -h | grep -E "/$|input|output"

# Database size
echo -e "\nDatabase Size:"
psql -U aires_user -d aires_dev -t -c \
  "SELECT pg_size_pretty(pg_database_size('aires_dev'));"

# Booklet count
echo -e "\nBooklets Generated Today:"
find /path/to/booklets/$(date +%Y-%m-%d) -name "*.md" | wc -l

# Error count
echo -e "\nErrors in last 24h:"
grep -c "ERROR" aires.log
```

#### 2. Archive Old Files
```bash
#!/bin/bash
# archive-old-files.sh

ARCHIVE_DIR="/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/input/Archive"
DAYS_TO_KEEP=7

# Archive processed files older than 7 days
find $ARCHIVE_DIR -type f -mtime +$DAYS_TO_KEEP -exec rm {} \;

# Compress old booklets
find /path/to/booklets -name "*.md" -mtime +30 -exec gzip {} \;
```

### Weekly Maintenance

#### 1. Database Maintenance
```sql
-- Vacuum and analyze tables
VACUUM ANALYZE FileProcessingRecords;
VACUUM ANALYZE OutboxMessages;

-- Reindex if needed
REINDEX TABLE FileProcessingRecords;

-- Clean old records
DELETE FROM FileProcessingRecords 
WHERE ProcessingCompletedAt < NOW() - INTERVAL '30 days';
```

#### 2. Log Rotation
```bash
# Rotate logs
mv aires.log aires.log.$(date +%Y%m%d)
gzip aires.log.$(date +%Y%m%d)

# Signal process to reopen log file
kill -USR1 $(cat aires.pid)

# Clean old logs
find . -name "aires.log.*.gz" -mtime +30 -delete
```

#### 3. Performance Review
```bash
# Generate weekly stats
echo "=== AIRES Weekly Performance Report ==="
echo "Week ending: $(date)"

# Processing stats
echo -e "\nFiles Processed:"
grep -c "Processing completed" aires.log*

# Average processing time
grep "Processing completed in" aires.log* | \
  awk '{sum+=$NF; count++} END {print "Average time:", sum/count, "seconds"}'

# Error rate
total=$(grep -c "Processing" aires.log*)
errors=$(grep -c "ERROR" aires.log*)
echo "Error rate: $(echo "scale=2; $errors*100/$total" | bc)%"
```

### Monthly Maintenance

#### 1. Full System Backup
```bash
#!/bin/bash
# monthly-backup.sh

BACKUP_DIR="/backup/aires/$(date +%Y%m)"
mkdir -p $BACKUP_DIR

# Database backup
pg_dump -U aires_user -d aires_dev | gzip > $BACKUP_DIR/database.sql.gz

# Configuration backup
tar czf $BACKUP_DIR/config.tar.gz \
  /path/to/appsettings.json \
  /path/to/watchdog.ini

# Booklets backup
tar czf $BACKUP_DIR/booklets.tar.gz /path/to/booklets/

echo "Backup completed: $BACKUP_DIR"
```

#### 2. Security Updates
```bash
# Update dependencies
cd /path/to/aires
dotnet list package --outdated
dotnet add package [package-name] --version [latest]

# Rebuild and test
dotnet build
dotnet test
```

## Backup and Recovery

### Backup Strategy

#### Automated Backups
```bash
# Create backup script
cat > /opt/aires/backup.sh << 'EOF'
#!/bin/bash
BACKUP_ROOT="/backup/aires"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="$BACKUP_ROOT/$DATE"

mkdir -p $BACKUP_DIR

# Stop AIRES gracefully
systemctl stop aires

# Backup database
pg_dump -U aires_user -d aires_dev > $BACKUP_DIR/database.sql

# Backup files
tar czf $BACKUP_DIR/booklets.tar.gz /path/to/booklets/
tar czf $BACKUP_DIR/config.tar.gz /path/to/config/

# Restart AIRES
systemctl start aires

# Clean old backups (keep 30 days)
find $BACKUP_ROOT -type d -mtime +30 -exec rm -rf {} \;

echo "Backup completed: $BACKUP_DIR"
EOF

chmod +x /opt/aires/backup.sh

# Schedule with cron
echo "0 2 * * * /opt/aires/backup.sh" | crontab -
```

### Recovery Procedures

#### Database Recovery
```bash
# Stop AIRES
systemctl stop aires

# Drop and recreate database
psql -U postgres -c "DROP DATABASE IF EXISTS aires_dev;"
psql -U postgres -c "CREATE DATABASE aires_dev OWNER aires_user;"

# Restore from backup
psql -U aires_user -d aires_dev < /backup/aires/20250113/database.sql

# Restart AIRES
systemctl start aires
```

#### Full System Recovery
```bash
#!/bin/bash
# disaster-recovery.sh

BACKUP_DATE=$1
if [ -z "$BACKUP_DATE" ]; then
    echo "Usage: $0 BACKUP_DATE"
    exit 1
fi

BACKUP_DIR="/backup/aires/$BACKUP_DATE"
if [ ! -d "$BACKUP_DIR" ]; then
    echo "Backup not found: $BACKUP_DIR"
    exit 1
fi

# Stop services
systemctl stop aires

# Restore database
psql -U postgres -c "DROP DATABASE IF EXISTS aires_dev;"
psql -U postgres -c "CREATE DATABASE aires_dev OWNER aires_user;"
psql -U aires_user -d aires_dev < $BACKUP_DIR/database.sql

# Restore files
tar xzf $BACKUP_DIR/config.tar.gz -C /
tar xzf $BACKUP_DIR/booklets.tar.gz -C /

# Start services
systemctl start aires

echo "Recovery completed from $BACKUP_DATE"
```

## Performance Tuning

### Database Optimization

```sql
-- Optimize PostgreSQL for AIRES workload
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET work_mem = '16MB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';

-- Create indexes for common queries
CREATE INDEX idx_file_processing_state ON FileProcessingRecords(State);
CREATE INDEX idx_file_processing_date ON FileProcessingRecords(DetectedAt);
CREATE INDEX idx_outbox_unprocessed ON OutboxMessages(ProcessedAt) 
  WHERE ProcessedAt IS NULL;

-- Reload configuration
SELECT pg_reload_conf();
```

### Application Tuning

#### Memory Optimization
```bash
# Set .NET garbage collection mode
export DOTNET_gcServer=true
export DOTNET_GCHeapCount=4

# Run with specific memory limits
dotnet run --property:ServerGarbageCollection=true
```

#### Concurrency Tuning
```json
{
  "FileProcessing": {
    "MaxConcurrentProcessing": 5,
    "ProcessingTimeout": 300,
    "UseMemoryCache": true
  },
  "Kafka": {
    "Consumer": {
      "MaxPollRecords": 20,
      "FetchMinBytes": 1024,
      "FetchMaxWaitMs": 500
    }
  }
}
```

### AI Model Optimization

```bash
# Preload models in Ollama
ollama run mistral:7b-instruct-q4_K_M "test"

# Increase Ollama memory
export OLLAMA_NUM_PARALLEL=4
export OLLAMA_MAX_LOADED_MODELS=2

# Optimize API calls
# Use connection pooling and keep-alive
```

## Security Operations

### Access Control

```bash
# Create dedicated user
sudo useradd -m -s /bin/bash aires
sudo usermod -aG docker aires

# Set file permissions
chmod 750 /opt/aires
chown -R aires:aires /opt/aires

# Restrict input directory
chmod 775 /path/to/input
setfacl -m u:aires:rwx /path/to/input
```

### Secret Management

```bash
# Use environment file
cat > /opt/aires/.env << EOF
GEMINI_API_KEY=your-secure-key
DEEPSEEK_API_KEY=your-secure-key
DB_PASSWORD=secure-password
EOF

chmod 600 /opt/aires/.env
chown aires:aires /opt/aires/.env

# In systemd service
EnvironmentFile=/opt/aires/.env
```

### Security Monitoring

```bash
# Monitor authentication attempts
grep "authentication" aires.log | tail -20

# Check for suspicious file access
auditctl -w /opt/aires -p wa -k aires_access
ausearch -k aires_access

# Network monitoring
netstat -tulpn | grep $(pgrep -f "MarketAnalyzer.BuildTools")
```

## Incident Response

### Common Issues and Resolution

#### Issue: AIRES Not Processing Files
```bash
# Diagnosis
1. Check process is running
2. Check input directory permissions
3. Check file filter configuration
4. Review logs for errors

# Resolution
systemctl restart aires
# or
chmod 775 /path/to/input
```

#### Issue: High Memory Usage
```bash
# Diagnosis
ps aux | grep -E "dotnet.*BuildTools"
cat /proc/$(pgrep -f "MarketAnalyzer.BuildTools")/status | grep Vm

# Resolution
# Increase memory limits or reduce concurrency
export DOTNET_GCHeapHardLimit=1073741824  # 1GB
```

#### Issue: Kafka Connection Issues
```bash
# Diagnosis
kafka-broker-api-versions.sh --bootstrap-server localhost:9092

# Resolution
# Restart Kafka
docker-compose restart kafka
# Update connection string in appsettings.json
```

### Emergency Procedures

#### Complete System Restart
```bash
#!/bin/bash
# emergency-restart.sh

echo "Starting AIRES emergency restart..."

# Stop all services
systemctl stop aires
docker-compose down

# Clear temporary data
rm -f /tmp/aires-*
rm -f /var/lock/aires.*

# Start infrastructure
docker-compose up -d postgres kafka
sleep 30

# Start AIRES
systemctl start aires

# Verify
sleep 10
if pgrep -f "MarketAnalyzer.BuildTools" > /dev/null; then
    echo "✅ AIRES restarted successfully"
else
    echo "❌ AIRES failed to start - check logs"
fi
```

### Incident Reporting

```markdown
## Incident Report Template

**Incident ID**: INC-YYYY-MM-DD-###
**Date/Time**: 
**Severity**: Critical/High/Medium/Low
**Reporter**: 

### Summary
Brief description of the incident

### Impact
- Services affected
- Duration
- Data loss (if any)

### Root Cause
Detailed explanation of what caused the incident

### Resolution
Steps taken to resolve the issue

### Prevention
Measures to prevent recurrence

### Lessons Learned
Key takeaways from the incident
```

---

**Next**: [Troubleshooting Guide](AIRES_Troubleshooting_Guide.md)