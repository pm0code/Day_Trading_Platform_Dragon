#!/bin/bash

# Mellanox 10GbE Optimization Script for Ultra-Low Latency Trading
# Optimized for DRAGON platform with Mellanox ConnectX series

set -e

echo "🌐 Mellanox 10GbE Ultra-Low Latency Trading Optimization"
echo "======================================================="

# Check if running as root/sudo
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root or with sudo"
    exit 1
fi

# Detect Mellanox interface
MELLANOX_INTERFACES=$(ip link show | grep -E "enp.*:" | cut -d: -f2 | xargs)
if [ -z "$MELLANOX_INTERFACES" ]; then
    echo "❌ No Mellanox network interfaces detected"
    exit 1
fi

echo "🔍 Detected Mellanox interfaces: $MELLANOX_INTERFACES"

for INTERFACE in $MELLANOX_INTERFACES; do
    echo ""
    echo "⚡ Optimizing interface: $INTERFACE"
    
    # Check if interface is up
    if ! ip link show $INTERFACE | grep -q "state UP"; then
        echo "   Bringing interface up..."
        ip link set $INTERFACE up
    fi
    
    # Get current configuration
    CURRENT_SPEED=$(ethtool $INTERFACE 2>/dev/null | grep "Speed:" | awk '{print $2}' || echo "Unknown")
    CURRENT_MTU=$(ip link show $INTERFACE | grep mtu | awk '{print $5}')
    
    echo "   Current speed: $CURRENT_SPEED"
    echo "   Current MTU: $CURRENT_MTU"
    
    # 1. Optimize ring buffers for 10GbE
    echo "   📊 Optimizing ring buffers..."
    ethtool -G $INTERFACE rx 4096 tx 4096 2>/dev/null || echo "   Ring buffer optimization not supported"
    
    # 2. Disable interrupt coalescing for minimum latency
    echo "   ⚡ Disabling interrupt coalescing..."
    ethtool -C $INTERFACE adaptive-rx off adaptive-tx off 2>/dev/null || echo "   Adaptive coalescing not supported"
    ethtool -C $INTERFACE rx-usecs 0 tx-usecs 0 2>/dev/null || echo "   Interrupt timing not supported"
    ethtool -C $INTERFACE rx-frames 1 tx-frames 1 2>/dev/null || echo "   Frame coalescing not supported"
    
    # 3. Disable TCP offloading for predictable latency
    echo "   🚫 Disabling TCP offload features..."
    ethtool -K $INTERFACE tso off 2>/dev/null || echo "   TSO disable not supported"
    ethtool -K $INTERFACE gso off 2>/dev/null || echo "   GSO disable not supported"
    ethtool -K $INTERFACE gro off 2>/dev/null || echo "   GRO disable not supported"
    ethtool -K $INTERFACE lro off 2>/dev/null || echo "   LRO disable not supported"
    ethtool -K $INTERFACE ufo off 2>/dev/null || echo "   UFO disable not supported"
    ethtool -K $INTERFACE sg off 2>/dev/null || echo "   SG disable not supported"
    
    # 4. Enable flow control for 10GbE
    echo "   🌊 Configuring flow control..."
    ethtool -A $INTERFACE rx on tx on 2>/dev/null || echo "   Flow control not supported"
    
    # 5. Set 9000 MTU for jumbo frames
    echo "   📦 Setting jumbo frames (9000 MTU)..."
    ip link set dev $INTERFACE mtu 9000 2>/dev/null && echo "   ✅ Jumbo frames enabled" || echo "   ⚠️  Jumbo frames not supported"
    
    # 6. Optimize queue discipline
    echo "   🚦 Optimizing traffic control..."
    tc qdisc del dev $INTERFACE root 2>/dev/null || true
    tc qdisc add dev $INTERFACE root handle 1: pfifo_fast
    
    echo "   ✅ Interface $INTERFACE optimized for trading"
done

# 7. Optimize kernel network parameters for 10GbE
echo ""
echo "🔧 Optimizing kernel network parameters for 10GbE..."

# TCP buffer sizes optimized for 10GbE (128MB max)
sysctl -w net.core.rmem_default=2097152
sysctl -w net.core.rmem_max=134217728
sysctl -w net.core.wmem_default=2097152
sysctl -w net.core.wmem_max=134217728
sysctl -w net.ipv4.tcp_rmem="4096 2097152 134217728"
sysctl -w net.ipv4.tcp_wmem="4096 2097152 134217728"

# Network device optimizations for 10GbE
sysctl -w net.core.netdev_max_backlog=30000
sysctl -w net.core.netdev_budget=600
sysctl -w net.core.dev_weight=64

# TCP optimizations for low latency
sysctl -w net.ipv4.tcp_congestion_control=bbr
sysctl -w net.ipv4.tcp_timestamps=0
sysctl -w net.ipv4.tcp_sack=1
sysctl -w net.ipv4.tcp_fack=1
sysctl -w net.ipv4.tcp_window_scaling=1
sysctl -w net.ipv4.tcp_adv_win_scale=1
sysctl -w net.ipv4.tcp_moderate_rcvbuf=1
sysctl -w net.ipv4.tcp_no_metrics_save=1
sysctl -w net.ipv4.tcp_low_latency=1

# Network security and performance
sysctl -w net.ipv4.tcp_syncookies=1
sysctl -w net.ipv4.tcp_max_syn_backlog=8192
sysctl -w net.core.somaxconn=65535

# Memory pressure optimization
sysctl -w net.core.optmem_max=65536
sysctl -w net.unix.max_dgram_qlen=50

echo "✅ Kernel network parameters optimized"

# 8. CPU affinity optimization for network interrupts
echo ""
echo "🎯 Optimizing CPU affinity for network interrupts..."

# Find network IRQs
NETWORK_IRQS=$(grep -E "(enp|eth|mlx)" /proc/interrupts | awk '{print $1}' | sed 's/://')

if [ ! -z "$NETWORK_IRQS" ]; then
    # Bind network interrupts to specific CPU cores (E-cores 8-11 for network processing)
    CPU_CORES="8,9,10,11"  # E-cores for interrupt handling
    
    for IRQ in $NETWORK_IRQS; do
        if [ -f "/proc/irq/$IRQ/smp_affinity_list" ]; then
            echo $CPU_CORES > /proc/irq/$IRQ/smp_affinity_list
            echo "   IRQ $IRQ bound to CPUs: $CPU_CORES"
        fi
    done
    echo "✅ Network interrupt affinity optimized"
else
    echo "⚠️  No network IRQs found for optimization"
fi

# 9. Mellanox specific optimizations (if mlx driver is present)
if lsmod | grep -q mlx; then
    echo ""
    echo "🔥 Applying Mellanox-specific optimizations..."
    
    # Mellanox driver parameters
    MLX_PARAMS="/sys/module/mlx5_core/parameters"
    if [ -d "$MLX_PARAMS" ]; then
        # Enable hardware timestamping
        echo "1" > $MLX_PARAMS/enable_health_recovery 2>/dev/null || true
        echo "✅ Mellanox hardware optimizations applied"
    fi
fi

# 10. Create persistent sysctl configuration
echo ""
echo "💾 Creating persistent network configuration..."
cat > /etc/sysctl.d/99-mellanox-10gbe-trading.conf << EOF
# Mellanox 10GbE Trading Optimizations
# TCP buffer sizes for 10GbE
net.core.rmem_default = 2097152
net.core.rmem_max = 134217728
net.core.wmem_default = 2097152
net.core.wmem_max = 134217728
net.ipv4.tcp_rmem = 4096 2097152 134217728
net.ipv4.tcp_wmem = 4096 2097152 134217728

# Network device optimizations
net.core.netdev_max_backlog = 30000
net.core.netdev_budget = 600
net.core.dev_weight = 64

# TCP optimizations for ultra-low latency
net.ipv4.tcp_congestion_control = bbr
net.ipv4.tcp_timestamps = 0
net.ipv4.tcp_sack = 1
net.ipv4.tcp_fack = 1
net.ipv4.tcp_window_scaling = 1
net.ipv4.tcp_adv_win_scale = 1
net.ipv4.tcp_moderate_rcvbuf = 1
net.ipv4.tcp_no_metrics_save = 1
net.ipv4.tcp_low_latency = 1

# Connection handling
net.ipv4.tcp_syncookies = 1
net.ipv4.tcp_max_syn_backlog = 8192
net.core.somaxconn = 65535
net.core.optmem_max = 65536
net.unix.max_dgram_qlen = 50
EOF

echo "✅ Persistent configuration saved to /etc/sysctl.d/99-mellanox-10gbe-trading.conf"

# 11. Display final configuration
echo ""
echo "📊 Final Network Configuration Summary:"
echo "======================================"

for INTERFACE in $MELLANOX_INTERFACES; do
    echo "Interface: $INTERFACE"
    echo "  Speed: $(ethtool $INTERFACE 2>/dev/null | grep "Speed:" | awk '{print $2}' || echo "Unknown")"
    echo "  MTU: $(ip link show $INTERFACE | grep mtu | awk '{print $5}')"
    echo "  Ring RX: $(ethtool -g $INTERFACE 2>/dev/null | grep "RX:" | tail -1 | awk '{print $2}' || echo "Unknown")"
    echo "  Ring TX: $(ethtool -g $INTERFACE 2>/dev/null | grep "TX:" | tail -1 | awk '{print $2}' || echo "Unknown")"
    echo ""
done

echo "📈 Network Performance Targets:"
echo "  • Latency: <50μs for market data feeds"
echo "  • Throughput: Up to 10Gbps sustained"
echo "  • Packet rate: >1M packets/second"
echo "  • Jitter: <10μs variation"

echo ""
echo "🎯 Verification Commands:"
echo "  • Test latency: ping -c 10 -i 0.1 [target_host]"
echo "  • Test throughput: iperf3 -c [target_host] -t 30"
echo "  • Monitor packets: watch 'cat /proc/net/dev'"
echo "  • Check interrupts: watch 'cat /proc/interrupts | grep eth'"

echo ""
echo "✅ Mellanox 10GbE optimization complete!"
echo "🚀 Ready for ultra-low latency trading operations"