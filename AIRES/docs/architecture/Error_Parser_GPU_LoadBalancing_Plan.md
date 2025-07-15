# AIRES Enhancement Plan: Error Parsing & GPU Load Balancing

## 1. Enhanced Error Parsing Architecture

### Current State
- Only parses CS compiler errors using single regex pattern
- Limited to format: `FilePath(line,col): error|warning CODE: Message`

### Target State
- Parse ALL .NET build error types:
  - **CS Errors**: CSxxxx (C# compiler)
  - **MSB Errors**: MSBxxxx (MSBuild - ranges 1xxx-6xxx)
  - **NETSDK Errors**: NETSDKxxxx (.NET SDK)
  - **General Errors**: Other build tool errors

### Architecture Design

```
┌─────────────────────────────────────────────────────────┐
│                   ParseCompilerErrorsHandler            │
│                         (Facade)                        │
├─────────────────────────────────────────────────────────┤
│  - Uses ErrorParserFactory to get appropriate parsers  │
│  - Orchestrates parsing with multiple parsers          │
│  - Returns unified ParseCompilerErrorsResponse         │
└────────────────┬───────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│                   ErrorParserFactory                    │
├─────────────────────────────────────────────────────────┤
│  - Analyzes error line format                          │
│  - Returns appropriate IErrorParser implementation     │
└────────────────┬───────────────────────────────────────┘
                 │
    ┌────────────┴────────────┬────────────┬─────────────┐
    ▼                         ▼            ▼             ▼
┌─────────┐           ┌─────────────┐ ┌─────────┐ ┌──────────┐
│CSharpError│         │MSBuildError│ │NetSdk   │ │General   │
│Parser    │         │Parser      │ │Error    │ │Error     │
│         │         │            │ │Parser   │ │Parser    │
└─────────┘         └─────────────┘ └─────────┘ └──────────┘
```

### Implementation Classes

1. **Core Domain Models**
```csharp
public record BuildError(
    string Type,        // "CS", "MSB", "NETSDK", etc.
    string Code,        // "CS0029", "MSB3644", etc.
    string Message,
    string Severity,    // "error", "warning"
    ErrorLocation Location,
    string RawText
);
```

2. **Parser Interface**
```csharp
public interface IErrorParser
{
    bool CanParse(string errorLine);
    IEnumerable<BuildError> ParseErrors(IEnumerable<string> lines);
}
```

3. **Specific Parsers**
- `CSharpErrorParser`: Handles CS errors
- `MSBuildErrorParser`: Handles MSB errors
- `NetSdkErrorParser`: Handles NETSDK errors
- `GeneralErrorParser`: Fallback parser

## 2. GPU Load Balancing Architecture

### Current State
- Single Ollama instance using only GPU1
- No load distribution
- Inefficient GPU utilization

### Target State
- 2 Ollama instances: GPU0 (port 11434), GPU1 (port 11435)
- Load balancer distributing requests
- Health monitoring and failover

### Architecture Design

```
┌─────────────────────────────────────────────────────────┐
│              OllamaLoadBalancerService                  │
├─────────────────────────────────────────────────────────┤
│  - Manages multiple Ollama instances                    │
│  - Distributes requests (round-robin)                  │
│  - Health checks instances                             │
│  - Handles failover                                    │
└────────────────┬───────────────────────────────────────┘
                 │
    ┌────────────┴────────────┐
    ▼                         ▼
┌─────────────┐         ┌─────────────┐
│  Ollama     │         │  Ollama     │
│  Instance 1 │         │  Instance 2 │
│  GPU0:11434 │         │  GPU1:11435 │
└─────────────┘         └─────────────┘
```

### Implementation Components

1. **Load Balancer Service**
```csharp
public class OllamaLoadBalancerService : AIRESServiceBase
{
    private readonly List<OllamaInstance> _instances;
    private int _currentIndex = 0;
    
    public async Task<string> GenerateAsync(GenerateRequest request)
    {
        var instance = GetNextHealthyInstance();
        return await instance.GenerateAsync(request);
    }
}
```

2. **Instance Management**
```csharp
public class OllamaInstance
{
    public int GpuId { get; init; }
    public int Port { get; init; }
    public string BaseUrl => $"http://localhost:{Port}";
    public bool IsHealthy { get; set; }
}
```

3. **Configuration**
```ini
[Ollama.LoadBalancing]
Instance1.GPU=0
Instance1.Port=11434
Instance2.GPU=1
Instance2.Port=11435
HealthCheckInterval=30
```

## Implementation Steps

### Phase 1: Error Parser Enhancement
1. Create new error parser classes
2. Implement IErrorParser interface
3. Create ErrorParserFactory
4. Update ParseCompilerErrorsHandler to use new parsers
5. Add comprehensive unit tests

### Phase 2: GPU Load Balancing
1. Create OllamaLoadBalancerService
2. Implement instance management
3. Add health checking
4. Update AI services to use load balancer
5. Add configuration support

## Benefits
- **Comprehensive Error Coverage**: Handle all .NET build errors
- **Better GPU Utilization**: Use both GPUs efficiently
- **Improved Performance**: Parallel processing on multiple GPUs
- **Fault Tolerance**: Failover if one GPU/instance fails
- **Extensibility**: Easy to add new error parsers or GPU instances