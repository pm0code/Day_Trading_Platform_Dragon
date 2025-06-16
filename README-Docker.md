# Docker Deployment Guide - DRAGON Trading Platform

## Overview

Complete Docker containerization for the DRAGON Trading Platform, optimized for Intel i9-14900K + 32GB DDR5 + Dual NVIDIA RTX setup.

## Quick Start

### 1. DRAGON Platform Deployment (Recommended)
```bash
# Deploy optimized for DRAGON hardware
./scripts/deploy-dragon.sh
```

### 2. Standard Deployment
```bash
# Copy environment template
cp .env.example .env

# Edit with your API keys
nano .env

# Deploy standard configuration
docker-compose up -d
```

## Architecture

### Microservices
- **Gateway** (Port 5000/5001): API Gateway with load balancing
- **MarketData** (Port 5002): High-frequency data ingestion
- **StrategyEngine** (Port 5003): Trading strategy execution
- **RiskManagement** (Port 5004): Real-time risk monitoring
- **PaperTrading** (Port 5005): Order execution simulation

### Infrastructure
- **Redis** (Port 6379): Message streams and caching
- **TimescaleDB** (Port 5432): Time-series financial data
- **Grafana** (Port 3000): Monitoring dashboard (optional)

## DRAGON Platform Optimizations

### Hardware Configuration
- **CPU**: Intel Core i9-14900K (24 cores: 8 P-Cores + 16 E-Cores)
- **Memory**: 32GB DDR5-3200 (3733.3 MHz max)
- **GPU**: Dual NVIDIA RTX (Primary + RTX 3060 Ti)
- **Network**: **Mellanox 10 Gigabit Ethernet** (ultra-low latency)

### Resource Allocation
```yaml
Gateway:      2-4 CPU cores, 2-4GB RAM
MarketData:   3-6 CPU cores, 4-6GB RAM
Strategy:     4-8 CPU cores, 4-8GB RAM
Risk:         2-4 CPU cores, 2-4GB RAM
PaperTrading: 3-6 CPU cores, 3-6GB RAM
Redis:        2-4 CPU cores, 4-5GB RAM
TimescaleDB:  4-8 CPU cores, 8-12GB RAM
```

### Performance Features
- **Low-latency GC**: SustainedLowLatency mode
- **High-performance Redis**: 8 I/O threads, optimized memory
- **TimescaleDB tuning**: 8GB shared buffers, 24GB cache
- **Mellanox 10GbE optimization**: Ultra-low latency network stack
- **Network features**: 9000 MTU jumbo frames, TCP BBR, disabled offloading
- **CPU governor**: Performance mode
- **Kernel parameters**: Optimized for 10Gb/s trading
- **Interrupt affinity**: E-cores for network processing

## Environment Configuration

### Required Variables (.env)
```bash
# Database
DB_PASSWORD=TradingPlatform2025!

# External APIs
ALPHAVANTAGE_API_KEY=your_key_here
FINNHUB_API_KEY=your_key_here

# DRAGON Platform
DRAGON_PLATFORM_ENABLED=true
CPU_CORE_AFFINITY=4,5,6,7
MEMORY_LIMIT_GB=32
```

### Optional Variables
```bash
# Trading Limits
INITIAL_CAPITAL=100000
MAX_POSITION_SIZE=1000000
MAX_DAILY_LOSS=50000

# Performance
REDIS_MAX_MEMORY=4gb
MARKET_DATA_CACHE_SIZE=2000000
STRATEGY_EXECUTION_TIMEOUT=30000
```

## Deployment Commands

### DRAGON Platform
```bash
# Deploy with DRAGON optimizations
docker-compose -f docker-compose.dragon.yml up -d

# Monitor performance
docker stats --no-stream

# Check health
curl http://localhost:5000/health
```

### Standard Platform
```bash
# Deploy standard configuration
docker-compose up -d

# View logs
docker-compose logs -f

# Scale services
docker-compose up -d --scale marketdata=2
```

## Health Monitoring

### Service Health Checks
```bash
# All services
for port in 5000 5002 5003 5004 5005; do
  curl -f "http://localhost:$port/health" && echo " ✅ Port $port OK"
done

# Infrastructure
docker exec dragon-trading-redis redis-cli ping
docker exec dragon-trading-timescaledb pg_isready -U trading_user
```

### Performance Monitoring
```bash
# Redis latency
docker exec dragon-trading-redis redis-cli --latency

# Database performance
docker exec dragon-trading-timescaledb pg_stat_activity

# System resources
htop
iftop
```

## Storage Management

### Persistent Volumes
- **redis_data**: Redis persistence
- **timescale_data**: Financial time-series data
- **grafana_data**: Monitoring dashboards
- **prometheus_data**: Metrics storage

### DRAGON Storage Optimization
```bash
# High-performance storage paths
/mnt/dragon-ssd/redis-data
/mnt/dragon-ssd/timescale-data

# Ensure SSD optimization
sudo mount -o noatime,discard /dev/nvme0n1p1 /mnt/dragon-ssd
```

## Network Configuration

### DRAGON Network Settings
- **Primary**: Mellanox 10 Gigabit Ethernet
- **MTU**: 9000 (jumbo frames for maximum throughput)
- **Bridge**: dragon-trading-platform
- **Ports**: 5000-5005, 6379, 5432
- **Latency target**: <50μs for market data feeds
- **TCP optimization**: BBR congestion control, disabled timestamps

### Firewall Rules
```bash
# Allow trading platform ports
sudo ufw allow 5000:5005/tcp
sudo ufw allow 6379/tcp
sudo ufw allow 5432/tcp
```

## Security

### Container Security
- **Non-root users**: All services run as `appuser`
- **Read-only filesystem**: Where possible
- **Resource limits**: CPU and memory constraints
- **Network isolation**: Dedicated bridge network

### Secrets Management
```bash
# Store secrets securely
echo "DB_PASSWORD=..." | sudo tee /etc/trading-platform/.env
sudo chmod 600 /etc/trading-platform/.env
```

## Troubleshooting

### Common Issues

#### Service Won't Start
```bash
# Check logs
docker-compose logs [service-name]

# Check resource usage
docker stats

# Verify dependencies
docker-compose ps
```

#### Performance Issues
```bash
# Check CPU governor
cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor

# Monitor network
ss -tuln | grep -E '(5000|5002|5003|5004|5005|6379|5432)'

# Check memory pressure
free -h
cat /proc/meminfo | grep -E '(MemAvailable|SwapTotal)'
```

#### Redis Connection Issues
```bash
# Test Redis connectivity
docker exec dragon-trading-redis redis-cli ping

# Check Redis logs
docker logs dragon-trading-redis

# Monitor Redis performance
docker exec dragon-trading-redis redis-cli info stats
```

### DRAGON Platform Diagnostics
```bash
# CPU performance check
lscpu | grep -E '(Model name|CPU\(s\)|Thread\(s\)|Core\(s\))'

# Memory configuration
dmidecode --type 17 | grep -E '(Size|Speed|Type:|Manufacturer)'

# GPU status
nvidia-smi --query-gpu=name,memory.total,temperature.gpu --format=csv

# Mellanox 10GbE network status
ethtool enp5s0 | grep -E '(Speed|Duplex|Link)'
ip -s link show enp5s0
cat /proc/interrupts | grep enp5s0
```

### Mellanox 10GbE Optimization
```bash
# Run dedicated Mellanox optimization
sudo ./scripts/optimize-mellanox-10gbe.sh

# Verify network performance
ping -c 10 -i 0.1 [market_data_server]
iperf3 -c [market_data_server] -t 30

# Monitor network performance
watch 'cat /proc/net/dev'
watch 'cat /proc/interrupts | grep enp'
```

## Scaling

### Horizontal Scaling
```bash
# Scale market data service
docker-compose up -d --scale marketdata=3

# Scale strategy engine
docker-compose up -d --scale strategy=2
```

### Vertical Scaling (DRAGON)
```yaml
# Increase resource limits in docker-compose.dragon.yml
deploy:
  resources:
    limits:
      cpus: '8.0'
      memory: 12G
```

## Monitoring Integration

### Grafana Dashboard
```bash
# Start monitoring stack
docker-compose --profile monitoring up -d

# Access dashboard
open http://localhost:3000
# Login: admin / admin
```

### Prometheus Metrics
```bash
# View metrics
curl http://localhost:9090/metrics

# Query API
curl 'http://localhost:9090/api/v1/query?query=up'
```

## Maintenance

### Updates
```bash
# Pull latest images
docker-compose pull

# Restart with new images
docker-compose up -d --force-recreate
```

### Backup
```bash
# Backup data volumes
docker run --rm -v redis_data:/data -v $(pwd):/backup ubuntu tar czf /backup/redis-backup.tar.gz /data
docker run --rm -v timescale_data:/data -v $(pwd):/backup ubuntu tar czf /backup/timescale-backup.tar.gz /data
```

### Cleanup
```bash
# Remove stopped containers
docker container prune

# Remove unused images
docker image prune

# Remove unused volumes
docker volume prune
```

---

## Support

For DRAGON platform-specific issues, ensure:
1. Intel i9-14900K CPU governor set to "performance"
2. 32GB DDR5 memory properly configured
3. NVIDIA drivers installed and updated
4. High-performance storage (NVMe SSD) mounted correctly
5. **Mellanox 10GbE optimization script executed**
6. Network interrupt affinity configured (E-cores 8-11)
7. Jumbo frames (9000 MTU) enabled
8. TCP offloading disabled for predictable latency

**Performance Targets**: 
- Order execution: <100μs order-to-wire
- Network latency: <50μs for market data feeds  
- Throughput: Up to 10Gbps sustained
- Packet rate: >1M packets/second