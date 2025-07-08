# GPU Acceleration Frameworks for .NET 8.0 in 2025 - Research Report

## Executive Summary

This research evaluates the current state-of-the-art GPU acceleration frameworks for .NET 8.0 in 2025, with a focus on their suitability for financial calculations in a day trading platform. Based on extensive research, **ILGPU** and **ComputeSharp** emerge as the two most viable options, while several previously popular frameworks have been discontinued or lack modern .NET support.

## Framework Analysis

### 1. CUDA.NET / ManagedCuda

**Current Status**: Actively maintained but with licensing concerns

**Key Findings**:
- **Compatibility**: Officially supports .NET Core 3.1+, .NET Framework 4.8
- **.NET 8.0 Support**: Not explicitly documented, but likely compatible due to .NET's backward compatibility
- **CUDA Support**: Updated regularly to latest CUDA versions (currently CUDA 12.8)
- **Licensing**: Changed to dual-license GPLv3/commercial starting with CUDA 12
- **Commercial Use**: Requires commercial license (contact: managedcuda@articimaging.eu)

**Pros**:
- Direct CUDA access for maximum performance
- 10+ years of active development
- Platform independent (Windows/Linux)

**Cons**:
- GPLv3 license requires commercial licensing for proprietary software
- Limited to NVIDIA GPUs only
- .NET 8.0 compatibility not officially confirmed

**Verdict**: Viable but expensive for commercial use due to licensing requirements

### 2. ILGPU ⭐ RECOMMENDED

**Current Status**: Actively maintained, open-source, production-ready

**Key Findings**:
- **Compatibility**: Supports .NET 5.0+, .NET Standard 2.1, .NET Framework 4.7.1
- **.NET 8.0 Support**: Compatible through .NET Standard 2.1 support
- **GPU Support**: CUDA (NVIDIA), OpenCL (AMD/Intel), CPU fallback
- **Licensing**: University of Illinois/NCSA Open Source License (permissive)
- **Commercial Use**: Free for all uses

**Pros**:
- Multi-vendor GPU support (NVIDIA, AMD, Intel)
- CPU accelerator for debugging
- No native dependencies (pure C#)
- Excellent debugging capabilities
- Active community support via Discord
- Backed by G-Research

**Cons**:
- No direct support for reference types (planned for future)
- Lambda functions not yet supported
- Learning curve for kernel programming

**Performance Features**:
- Inline PTX assembly support
- Multi-dimensional arrays
- LibDevice bindings for optimized math on NVIDIA GPUs
- Shared memory and atomics support

**Verdict**: Best overall choice for cross-platform GPU acceleration

### 3. ComputeSharp ⭐ RECOMMENDED

**Current Status**: Actively maintained, production-ready

**Key Findings**:
- **Compatibility**: .NET Standard 2.0, .NET 6+, likely .NET 8.0 compatible
- **Version**: 3.2.0 (latest on NuGet)
- **GPU Support**: DirectX 12 compute shaders (Windows only)
- **Licensing**: Not specified in research (check GitHub)
- **Commercial Use**: Used in production by several projects

**Pros**:
- Write shaders entirely in C#
- Source generation at build time
- No need to learn HLSL
- Supports both compute and pixel shaders
- XAML controls for UWP/WinUI 3
- Excellent for Windows-specific applications

**Cons**:
- Windows-only (DirectX 12 dependency)
- Limited to DirectX 12 compatible GPUs
- May have overhead compared to native CUDA

**Use Cases**:
- Image processing
- Parallel computations
- Real-time visualizations
- Graphics-heavy applications

**Verdict**: Excellent for Windows-exclusive applications

### 4. Alea GPU

**Current Status**: DISCONTINUED

**Key Findings**:
- Last updated: 2017
- Company (QuantAlea) appears defunct
- Doesn't support RTX 30xx series or newer
- Website and social media accounts gone

**Verdict**: Do not use - completely abandoned

### 5. GPU.NET and Alternatives

**Current Status**: Various alternatives exist

**Key Alternatives Found**:
- **Hybridizer**: Compiler from Altimesh for C# to GPU compilation
- **OpenCL.NET**: Basic OpenCL bindings for .NET
- Various smaller libraries with limited support

**Verdict**: None as mature as ILGPU or ComputeSharp

## Critical Consideration: Decimal Financial Calculations

### The Precision Challenge

GPU hardware is fundamentally designed for floating-point operations (FP32, FP16) rather than decimal arithmetic. This presents a significant challenge for financial calculations that require exact decimal precision.

### Recommended Solutions:

1. **Fixed-Point Arithmetic**: Implement custom fixed-point math using integers
2. **Scaled Integer Operations**: Convert decimals to scaled integers (e.g., cents instead of dollars)
3. **Hybrid Approach**: Use GPU for parallel screening, CPU for final precise calculations
4. **Custom Kernels**: Write specialized kernels that maintain precision requirements

### Example Approach:
```csharp
// Convert decimal to scaled integer for GPU processing
decimal price = 123.45m;
long scaledPrice = (long)(price * 10000); // 4 decimal places precision

// Process on GPU with integer operations
// ...

// Convert back to decimal
decimal result = scaledPrice / 10000m;
```

## Platform Support Summary

| Framework | NVIDIA | AMD | Intel | Windows | Linux | macOS |
|-----------|---------|-----|-------|---------|--------|--------|
| ManagedCuda | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ |
| ILGPU | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| ComputeSharp | ✅* | ✅* | ✅* | ✅ | ❌ | ❌ |

*Through DirectX 12 support

## Recommendations for Day Trading Platform

### Primary Recommendation: ILGPU

**Rationale**:
1. **Cross-platform GPU support** ensures flexibility in deployment
2. **Permissive licensing** perfect for commercial use
3. **Active development** and community support
4. **CPU fallback** enables development without GPU
5. **Production-ready** with proven track record

### Secondary Option: ComputeSharp (Windows-only)

**Use if**:
- Platform is Windows-exclusive
- DirectX 12 integration is beneficial
- Easier learning curve is priority

### Implementation Strategy:

1. **Use ILGPU for**:
   - Parallel technical indicator calculations
   - Large-scale data screening
   - Pattern recognition algorithms
   - Monte Carlo simulations

2. **Maintain CPU pathways for**:
   - Final decimal precision calculations
   - Order execution logic
   - Risk management calculations
   - Regulatory compliance computations

3. **Hybrid Architecture**:
   ```
   Data Ingestion → GPU Screening (ILGPU) → CPU Validation → Trading Decision
   ```

## Performance Expectations

Based on research:
- Modern CPUs: ~6 GFLOPS
- Modern GPUs: ~6 TFLOPS (1000x theoretical improvement)
- Realistic speedup: 10-100x for well-optimized parallel workloads

## Next Steps

1. **Proof of Concept**: Build simple ILGPU test with financial calculations
2. **Benchmark**: Compare GPU vs CPU for typical screening operations
3. **Precision Testing**: Validate decimal handling strategies
4. **Integration Planning**: Design GPU acceleration points in architecture

## Resources

- ILGPU Documentation: https://ilgpu.net/
- ILGPU GitHub: https://github.com/m4rs-mt/ILGPU
- ComputeSharp GitHub: https://github.com/Sergio0694/ComputeSharp
- ILGPU Discord: Available through official website

## Conclusion

For the Day Trading Platform Dragon project, **ILGPU** provides the best combination of features, performance, licensing, and community support for GPU acceleration in .NET 8.0. Its cross-platform capabilities and permissive licensing make it ideal for commercial financial applications, despite the need for custom solutions to handle decimal precision requirements.