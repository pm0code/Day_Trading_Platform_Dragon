#!/bin/bash

# DRAGON Platform Deployment Script
# Optimized for Intel i9-14900K + 32GB DDR5 + Dual NVIDIA RTX

set -e

echo "🐉 DRAGON Trading Platform Deployment Script"
echo "================================================="

# Check if running on DRAGON platform
if ! grep -q "Intel.*i9-14900K" /proc/cpuinfo; then
    echo "⚠️  Warning: This deployment is optimized for Intel i9-14900K"
    echo "Current CPU: $(grep 'model name' /proc/cpuinfo | head -1 | cut -d: -f2 | xargs)"
fi

# Check memory (should be 32GB)
TOTAL_MEM=$(grep MemTotal /proc/meminfo | awk '{print $2}')
TOTAL_MEM_GB=$((TOTAL_MEM / 1024 / 1024))
echo "📊 System Memory: ${TOTAL_MEM_GB}GB"

if [ $TOTAL_MEM_GB -lt 30 ]; then
    echo "⚠️  Warning: DRAGON platform expected to have 32GB+ RAM"
fi

# Check for NVIDIA GPUs
if command -v nvidia-smi &> /dev/null; then
    echo "🖥️  NVIDIA GPU Configuration:"
    nvidia-smi --query-gpu=name,memory.total --format=csv,noheader,nounits
else
    echo "⚠️  Warning: NVIDIA drivers not detected (GPU acceleration disabled)"
fi

# Create high-performance storage directories
echo "📁 Creating optimized storage directories..."
sudo mkdir -p /mnt/dragon-ssd/redis-data
sudo mkdir -p /mnt/dragon-ssd/timescale-data
sudo chown -R $USER:$USER /mnt/dragon-ssd/

# Set high-performance kernel parameters for trading
echo "⚡ Configuring high-performance kernel parameters..."
sudo sysctl -w net.core.rmem_default=262144
sudo sysctl -w net.core.rmem_max=16777216
sudo sysctl -w net.core.wmem_default=262144
sudo sysctl -w net.core.wmem_max=16777216
sudo sysctl -w net.ipv4.tcp_rmem="4096 87380 16777216"
sudo sysctl -w net.ipv4.tcp_wmem="4096 65536 16777216"
sudo sysctl -w net.core.netdev_max_backlog=5000
sudo sysctl -w vm.swappiness=1
sudo sysctl -w vm.dirty_ratio=15
sudo sysctl -w vm.dirty_background_ratio=5

# Set CPU governor to performance
echo "🔧 Setting CPU governor to performance mode..."
for cpu in /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor; do
    if [ -w "$cpu" ]; then
        echo performance | sudo tee "$cpu" > /dev/null
    fi
done

# Check Docker and Docker Compose
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Check for .env file
if [ ! -f .env ]; then
    echo "📋 Creating .env file from template..."
    cp .env.example .env
    echo "⚠️  Please edit .env file with your API keys before continuing."
    echo "Press Enter when ready to continue..."
    read
fi

# Build all images
echo "🔨 Building Docker images..."
docker-compose -f docker-compose.dragon.yml build --parallel

# Start infrastructure services first
echo "🚀 Starting infrastructure services..."
docker-compose -f docker-compose.dragon.yml up -d redis timescaledb

# Wait for infrastructure to be ready
echo "⏳ Waiting for infrastructure services..."
sleep 30

# Verify Redis is ready
echo "🔍 Verifying Redis connectivity..."
docker exec dragon-trading-redis redis-cli ping

# Verify TimescaleDB is ready
echo "🔍 Verifying TimescaleDB connectivity..."
docker exec dragon-trading-timescaledb pg_isready -U trading_user -d trading_platform

# Start trading services
echo "🚀 Starting trading microservices..."
docker-compose -f docker-compose.dragon.yml up -d gateway marketdata strategy risk papertrading

# Wait for services to initialize
echo "⏳ Waiting for trading services to initialize..."
sleep 45

# Health check all services
echo "🏥 Performing health checks..."
services=("gateway:5000" "marketdata:5002" "strategy:5003" "risk:5004" "papertrading:5005")

for service in "${services[@]}"; do
    name=$(echo $service | cut -d: -f1)
    port=$(echo $service | cut -d: -f2)
    
    echo -n "Checking $name ($port)... "
    if curl -f -s "http://localhost:$port/health" > /dev/null; then
        echo "✅ Healthy"
    else
        echo "❌ Unhealthy"
    fi
done

# Display system status
echo ""
echo "🐉 DRAGON Trading Platform Status"
echo "=================================="
docker-compose -f docker-compose.dragon.yml ps

echo ""
echo "📊 Resource Usage:"
docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}"

echo ""
echo "🌐 Service URLs:"
echo "  Gateway:      http://localhost:5000"
echo "  Market Data:  http://localhost:5002"
echo "  Strategy:     http://localhost:5003"
echo "  Risk Mgmt:    http://localhost:5004"
echo "  Paper Trade:  http://localhost:5005"
echo "  Redis:        localhost:6379"
echo "  TimescaleDB:  localhost:5432"

echo ""
echo "🎯 Performance Optimization Tips:"
echo "  - Monitor CPU cores 4-7 (P-Cores) for critical processes"
echo "  - Watch memory usage (target <24GB used)"
echo "  - Check Redis latency: redis-cli --latency"
echo "  - Monitor network: iftop or nethogs"

echo ""
echo "✅ DRAGON Platform deployment complete!"
echo "🚀 Ready for ultra-low latency trading operations"