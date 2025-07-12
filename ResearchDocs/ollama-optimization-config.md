# Ollama Optimization Configuration for MCP Code Analyzer

## System Specifications
- **CPU**: AMD Ryzen 9 5900X (12 cores, 24 threads)
- **GPU**: NVIDIA RTX 3060 (12GB VRAM)
- **RAM**: (Assumed 32GB+)

## Optimization Strategy

### 1. Environment Variables for Optimal Performance

```bash
# CPU Optimization - Use all available threads
export OLLAMA_NUM_THREADS=24

# GPU Acceleration - Force GPU usage
export CUDA_VISIBLE_DEVICES=0
export OLLAMA_GPU_LAYERS=999  # Load all layers to GPU

# Memory Management
export OLLAMA_MAX_LOADED_MODELS=1  # Only keep 1 model in memory
export OLLAMA_KEEP_ALIVE=30s       # Unload models after 30s idle

# Context Optimization for Code Analysis
export OLLAMA_NUM_CTX=2048         # Reduced context for faster processing

# KV Cache Optimization
export OLLAMA_KV_CACHE_TYPE="q8_0"
export OLLAMA_FLASH_ATTENTION=1

# Parallel Processing
export OLLAMA_PARALLEL=2           # Allow 2 parallel requests
```

### 2. Model-Specific Optimizations

#### For Code Analysis (qwen2.5-coder:32b)
```bash
# Use quantized version for better GPU performance
ollama pull qwen2.5-coder:32b-q4_K_M  # 4-bit quantized version

# Or create custom modelfile
cat > Modelfile.codeanalysis << EOF
FROM qwen2.5-coder:32b-q4_K_M
PARAMETER num_ctx 2048
PARAMETER num_gpu 999
PARAMETER temperature 0.1
PARAMETER top_p 0.9
PARAMETER repeat_penalty 1.1
PARAMETER num_thread 24
EOF

ollama create qwen-coder-optimized -f Modelfile.codeanalysis
```

#### For Quick Checks (stable-code:3b)
```bash
cat > Modelfile.quickcheck << EOF
FROM stable-code:3b
PARAMETER num_ctx 1024
PARAMETER num_gpu 999
PARAMETER temperature 0.1
PARAMETER num_thread 12
EOF

ollama create stable-code-quick -f Modelfile.quickcheck
```

### 3. Ollama Service Optimization

Create systemd override for Ollama service:
```bash
sudo mkdir -p /etc/systemd/system/ollama.service.d/
sudo tee /etc/systemd/system/ollama.service.d/override.conf << EOF
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
Environment="OLLAMA_MODELS=/usr/share/ollama/.ollama/models"
Environment="OLLAMA_NUM_THREADS=24"
Environment="OLLAMA_GPU_LAYERS=999"
Environment="CUDA_VISIBLE_DEVICES=0"
Environment="OLLAMA_FLASH_ATTENTION=1"
EOF

sudo systemctl daemon-reload
sudo systemctl restart ollama
```

### 4. Code Analysis Specific Settings

For our canonical logging validation use case:

```javascript
// Optimized request configuration
const ollamaConfig = {
  model: 'qwen-coder-optimized',  // Use our optimized model
  options: {
    // Performance settings
    num_ctx: 2048,          // Smaller context for code snippets
    num_gpu: 999,           // Use all GPU layers
    num_thread: 24,         // Use all CPU threads
    
    // Quality settings for code analysis
    temperature: 0.1,       // Very low for consistent analysis
    top_p: 0.95,           // High precision
    top_k: 40,             // Limit token selection
    repeat_penalty: 1.1,    // Avoid repetition
    
    // Speed optimizations
    num_predict: 1000,      // Limit output length
    stop: ["SCORE:", "```"], // Stop tokens for structured output
    
    // Batching
    num_batch: 512,         // Larger batch size for GPU
  }
};
```

### 5. Pre-warming Strategy

```javascript
// Pre-warm models on startup
async function prewarmModels() {
  const models = ['qwen-coder-optimized', 'stable-code-quick'];
  
  for (const model of models) {
    await axios.post('http://localhost:11434/api/generate', {
      model: model,
      prompt: "// Test",
      options: { num_predict: 1 }
    });
  }
}
```

### 6. Monitoring Script

```bash
#!/bin/bash
# Monitor Ollama performance

while true; do
  clear
  echo "=== OLLAMA PERFORMANCE MONITOR ==="
  echo
  echo "GPU Usage:"
  nvidia-smi --query-gpu=utilization.gpu,memory.used,memory.free --format=csv
  echo
  echo "CPU Usage:"
  ps aux | grep ollama | grep -v grep | awk '{print "CPU: " $3 "%, MEM: " $4 "%"}'
  echo
  echo "Active Models:"
  curl -s http://localhost:11434/api/ps | jq -r '.models[]?.name // "None"'
  echo
  sleep 2
done
```

## Expected Performance Improvements

With these optimizations:
- **GPU Utilization**: Should increase from 16% to 60-80% during inference
- **Response Time**: 50-70% faster for code analysis
- **Memory Efficiency**: Better model loading/unloading
- **Parallel Processing**: Handle multiple validation requests

## Implementation Priority

1. **IMMEDIATE**: Set environment variables
2. **HIGH**: Create optimized modelfiles
3. **MEDIUM**: Update service configuration
4. **LOW**: Implement monitoring

## Validation Prompt Optimization

For canonical logging validation, use structured prompts:

```
INSTRUCTION: Analyze TypeScript canonical logging compliance.
OUTPUT FORMAT: JSON
FIELDS: score(0-100), issues[], methods_analyzed, critical_violations
MAX_TOKENS: 500
CONTEXT_SIZE: 2048
```

This reduces processing time while maintaining accuracy.