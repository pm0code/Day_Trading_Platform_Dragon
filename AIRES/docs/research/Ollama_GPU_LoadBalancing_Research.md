# Ollama GPU Load Balancing Research

## Executive Summary
AIRES currently uses only GPU1 for Ollama inference. Research shows Ollama doesn't automatically load balance across GPUs. Multiple instance approach with CUDA_VISIBLE_DEVICES is recommended.

## Current State Problems
1. Only GPU1 utilized, GPU0 idle
2. No redundancy if GPU1 fails
3. Inefficient resource utilization
4. No parallel processing capability

## Ollama GPU Behavior

### Default Behavior
- Ollama uses all visible GPUs but doesn't load balance
- Models loaded on single GPU by default (v0.1.32+)
- No automatic request distribution

### CUDA_VISIBLE_DEVICES
- Environment variable controls GPU visibility
- Format: `CUDA_VISIBLE_DEVICES=0` or `CUDA_VISIBLE_DEVICES=0,1`
- Must be set before Ollama starts

## Recommended Solution: Multi-Instance Approach

### Architecture
```
Client -> Load Balancer -> Ollama Instance 1 (GPU0:11434)
                        -> Ollama Instance 2 (GPU1:11435)
```

### Implementation Steps

1. **Start Multiple Instances**
```bash
# Instance 1 - GPU0
CUDA_VISIBLE_DEVICES=0 OLLAMA_HOST=0.0.0.0:11434 ollama serve

# Instance 2 - GPU1
CUDA_VISIBLE_DEVICES=1 OLLAMA_HOST=0.0.0.0:11435 ollama serve
```

2. **Load Balancer Requirements**
- Health check each instance
- Distribute requests (round-robin or least-loaded)
- Handle instance failures
- Monitor GPU utilization

3. **Service Management**
- SystemD service per instance
- Automatic restart on failure
- Resource monitoring

## Configuration Example

### SystemD Service (GPU0)
```ini
[Unit]
Description=Ollama GPU0 Instance
After=network.target

[Service]
Type=simple
Environment="CUDA_VISIBLE_DEVICES=0"
Environment="OLLAMA_HOST=0.0.0.0:11434"
ExecStart=/usr/local/bin/ollama serve
Restart=always

[Install]
WantedBy=multi-user.target
```

### C# Load Balancer Design
```csharp
public class OllamaLoadBalancer
{
    private readonly List<OllamaInstance> instances = new()
    {
        new() { GpuId = 0, Port = 11434, BaseUrl = "http://localhost:11434" },
        new() { GpuId = 1, Port = 11435, BaseUrl = "http://localhost:11435" }
    };
    
    public async Task<string> GenerateAsync(request)
    {
        var instance = SelectHealthyInstance();
        return await instance.GenerateAsync(request);
    }
}
```

## Performance Considerations

1. **Model Loading**: Each instance loads models independently
2. **Memory Usage**: Models duplicated across instances
3. **Network Overhead**: Minimal for localhost connections
4. **Failover Time**: Sub-second with proper health checks

## Alternative Approaches (Not Recommended)

1. **Single Instance Multi-GPU**: Ollama doesn't efficiently distribute
2. **GPU Scheduling**: Complex, requires custom CUDA programming
3. **Container Orchestration**: Overkill for 2 GPUs

## References
- GitHub: ollama/ollama issues #1813, #3095, #7768
- Medium: "How to run Ollama on specific GPU(s)"
- Ollama docs: GPU configuration