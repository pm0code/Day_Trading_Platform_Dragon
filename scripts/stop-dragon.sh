#!/bin/bash

# DRAGON Platform Stop Script
# Gracefully stops all trading services

set -e

echo "🐉 DRAGON Trading Platform - Graceful Shutdown"
echo "==============================================="

# Stop trading services first (reverse order)
echo "🛑 Stopping trading microservices..."
docker-compose -f docker-compose.dragon.yml stop papertrading strategy risk marketdata gateway

# Wait for graceful shutdown
echo "⏳ Waiting for services to stop gracefully..."
sleep 10

# Stop infrastructure services
echo "🛑 Stopping infrastructure services..."
docker-compose -f docker-compose.dragon.yml stop timescaledb redis

# Show final status
echo ""
echo "📊 Final Status:"
docker-compose -f docker-compose.dragon.yml ps

echo ""
echo "✅ DRAGON Platform shutdown complete"
echo "💾 Data preserved in volumes"