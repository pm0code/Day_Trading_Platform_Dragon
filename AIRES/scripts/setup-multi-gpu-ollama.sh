#!/bin/bash

# Multi-GPU Ollama Setup Script for AIRES
# This script sets up multiple Ollama instances for GPU load balancing

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== AIRES Multi-GPU Ollama Setup ===${NC}"
echo

# Function to check if Ollama is running on a port
check_ollama_port() {
    local port=$1
    if curl -s "http://localhost:$port/api/tags" >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Function to start Ollama on a specific GPU and port
start_ollama_instance() {
    local gpu_id=$1
    local port=$2
    local name="ollama-gpu$gpu_id"
    
    echo -e "${YELLOW}Starting Ollama on GPU $gpu_id (port $port)...${NC}"
    
    # Check if already running
    if check_ollama_port $port; then
        echo -e "${GREEN}✓ Ollama already running on port $port${NC}"
        return 0
    fi
    
    # Create systemd service file
    cat > "/tmp/${name}.service" << EOF
[Unit]
Description=Ollama GPU$gpu_id Instance
After=network.target

[Service]
Type=simple
Environment="CUDA_VISIBLE_DEVICES=$gpu_id"
Environment="OLLAMA_HOST=0.0.0.0:$port"
Environment="OLLAMA_MODELS=/usr/share/ollama/.ollama/models"
Environment="OLLAMA_KEEP_ALIVE=5m"
ExecStart=/usr/local/bin/ollama serve
Restart=always
RestartSec=10
User=$USER

[Install]
WantedBy=default.target
EOF

    # For testing, start in background
    CUDA_VISIBLE_DEVICES=$gpu_id OLLAMA_HOST=0.0.0.0:$port ollama serve > /tmp/${name}.log 2>&1 &
    local pid=$!
    echo $pid > /tmp/${name}.pid
    
    # Wait for startup
    echo -n "Waiting for Ollama to start..."
    for i in {1..30}; do
        if check_ollama_port $port; then
            echo -e " ${GREEN}✓${NC}"
            echo -e "${GREEN}✓ Ollama started on GPU $gpu_id (port $port, PID: $pid)${NC}"
            return 0
        fi
        echo -n "."
        sleep 1
    done
    
    echo -e " ${RED}✗${NC}"
    echo -e "${RED}Failed to start Ollama on GPU $gpu_id${NC}"
    return 1
}

# Function to pull required models
pull_models() {
    local port=$1
    shift
    local models=("$@")
    
    echo -e "${YELLOW}Pulling models on port $port...${NC}"
    
    for model in "${models[@]}"; do
        echo -n "  Pulling $model..."
        if OLLAMA_HOST=localhost:$port ollama pull "$model" >/dev/null 2>&1; then
            echo -e " ${GREEN}✓${NC}"
        else
            echo -e " ${YELLOW}(may already exist)${NC}"
        fi
    done
}

# Main setup
main() {
    echo "1. Detecting GPUs..."
    
    # Check for nvidia-smi
    if ! command -v nvidia-smi &> /dev/null; then
        echo -e "${RED}Error: nvidia-smi not found. Please install NVIDIA drivers.${NC}"
        exit 1
    fi
    
    # Get GPU count
    GPU_COUNT=$(nvidia-smi --query-gpu=count --format=csv,noheader | head -1)
    echo -e "${GREEN}✓ Found $GPU_COUNT GPU(s)${NC}"
    
    # Display GPU info
    echo
    echo "GPU Information:"
    nvidia-smi --query-gpu=index,name,memory.total --format=csv
    echo
    
    # Start Ollama instances
    echo "2. Starting Ollama instances..."
    
    # GPU 0 - Port 11434 (default)
    start_ollama_instance 0 11434
    
    # GPU 1 - Port 11435
    if [ $GPU_COUNT -gt 1 ]; then
        start_ollama_instance 1 11435
    fi
    
    # Pull models
    echo
    echo "3. Configuring models..."
    
    # Models for GPU 0 (RTX 3060 Ti - 8GB)
    GPU0_MODELS=("mistral:7b-instruct-q4_K_M" "deepseek-coder:6.7b")
    pull_models 11434 "${GPU0_MODELS[@]}"
    
    # Models for GPU 1 (RTX 4070 Ti - 12GB)
    if [ $GPU_COUNT -gt 1 ]; then
        GPU1_MODELS=("codegemma:7b" "gemma2:9b")
        pull_models 11435 "${GPU1_MODELS[@]}"
    fi
    
    # Verify setup
    echo
    echo "4. Verifying setup..."
    
    echo -e "${YELLOW}GPU 0 (Port 11434):${NC}"
    if check_ollama_port 11434; then
        echo -e "  ${GREEN}✓ Ollama running${NC}"
        OLLAMA_HOST=localhost:11434 ollama list 2>/dev/null | head -5
    else
        echo -e "  ${RED}✗ Ollama not running${NC}"
    fi
    
    if [ $GPU_COUNT -gt 1 ]; then
        echo
        echo -e "${YELLOW}GPU 1 (Port 11435):${NC}"
        if check_ollama_port 11435; then
            echo -e "  ${GREEN}✓ Ollama running${NC}"
            OLLAMA_HOST=localhost:11435 ollama list 2>/dev/null | head -5
        else
            echo -e "  ${RED}✗ Ollama not running${NC}"
        fi
    fi
    
    # Update AIRES configuration
    echo
    echo "5. Updating AIRES configuration..."
    
    CONFIG_FILE="../config/aires.ini"
    if [ -f "$CONFIG_FILE" ]; then
        # Enable GPU load balancing
        sed -i 's/EnableGpuLoadBalancing = false/EnableGpuLoadBalancing = true/' "$CONFIG_FILE"
        echo -e "${GREEN}✓ Updated aires.ini to enable GPU load balancing${NC}"
        
        # Also update the bin config
        if [ -f "../bin/config/aires.ini" ]; then
            cp "$CONFIG_FILE" "../bin/config/aires.ini"
            echo -e "${GREEN}✓ Updated bin/config/aires.ini${NC}"
        fi
    else
        echo -e "${YELLOW}Warning: aires.ini not found${NC}"
    fi
    
    echo
    echo -e "${GREEN}=== Setup Complete ===${NC}"
    echo
    echo "To stop Ollama instances:"
    echo "  kill \$(cat /tmp/ollama-gpu0.pid)"
    if [ $GPU_COUNT -gt 1 ]; then
        echo "  kill \$(cat /tmp/ollama-gpu1.pid)"
    fi
    echo
    echo "To make permanent, copy service files:"
    echo "  sudo cp /tmp/ollama-gpu*.service /etc/systemd/system/"
    echo "  sudo systemctl daemon-reload"
    echo "  sudo systemctl enable ollama-gpu0 ollama-gpu1"
    echo "  sudo systemctl start ollama-gpu0 ollama-gpu1"
}

# Run main function
main