# AIRES - AI Error Resolution System

**Version**: 1.0  
**Platform**: Windows 11 x64  
**Framework**: .NET 8.0+  
**Status**: Production Ready

## 🎯 Overview

AIRES (AI Error Resolution System) is a completely independent, autonomous development tool that monitors build error files and automatically generates comprehensive research booklets through a 4-stage AI pipeline. It prevents the "compiler trap" by forcing developers to understand root causes before implementing fixes.

## 🚀 Key Features

- **Fully Autonomous Operation**: Configure once, run forever
- **4-Stage AI Pipeline**: Mistral → DeepSeek → CodeGemma → Gemma2
- **INI-Based Configuration**: Easy deployment for any project
- **Comprehensive Logging**: Complete observability and debugging
- **Zero Dependencies**: Completely self-contained system

## 📋 System Requirements

- **OS**: Windows 11 x64 (version 22000.0 or higher)
- **Runtime**: .NET 8.0 Runtime
- **Memory**: 4GB RAM minimum (8GB recommended)
- **Storage**: 500MB for application + space for logs/booklets
- **AI Models**: Ollama with required models installed

## 🔧 Quick Start

### 1. Install AI Models

```bash
# Install required Ollama models
ollama pull mistral:7b-instruct-q4_K_M
ollama pull deepseek-coder:6.7b-instruct
ollama pull codegemma:7b-instruct
ollama pull gemma2:9b-instruct
```

### 2. Configure AIRES

Edit `config/aires.ini`:

```ini
[Watchdog]
InputDirectory=C:\YourProject\BuildErrors
OutputDirectory=C:\YourProject\AIRESBooklets
ProcessedDirectory=C:\YourProject\BuildErrors\Processed
FailedDirectory=C:\YourProject\BuildErrors\Failed
```

### 3. Start AIRES

```bash
# Start in autonomous watchdog mode
aires start --watchdog

# Or process a specific file
aires process build_errors.txt

# Check status
aires status
```

## 🔄 Autonomous Workflow

```
1. WATCHDOG monitors InputDirectory for *.txt files
2. DETECTS new build error file
3. PIPELINE executes:
   - Mistral: Microsoft documentation research
   - DeepSeek: Code context analysis
   - CodeGemma: Pattern identification
   - Gemma2: Synthesis and integration
4. BOOKLET generated in OutputDirectory
5. FILE moved to ProcessedDirectory
```

## 📁 Project Structure

```
AIRES/
├── src/
│   ├── AIRES.Foundation/     # Core patterns and base classes
│   ├── AIRES.Core/          # Domain models and business logic
│   ├── AIRES.Watchdog/      # Autonomous file monitoring
│   ├── AIRES.Infrastructure/ # AI services, persistence
│   ├── AIRES.Application/   # Pipeline orchestration
│   └── AIRES.CLI/           # Command line interface
├── config/
│   └── aires.ini            # Configuration file
├── logs/                    # Application logs
└── docs/                    # Documentation
```

## 🛠️ Building from Source

```bash
# Clone repository
git clone https://github.com/yourusername/AIRES.git
cd AIRES

# Build solution (x64 only)
dotnet build --configuration Release --platform x64

# Run tests
dotnet test

# Publish self-contained executable
dotnet publish src/AIRES.CLI/AIRES.CLI.csproj -c Release -r win-x64 --self-contained
```

## 📊 Monitoring and Status

### Check System Status

```bash
aires status
```

Output:
```
AIRES Status Report
==================
Status: Healthy
Uptime: 2:45:30

Watchdog Status:
  Monitoring: C:\Projects\BuildErrors
  Files Detected Today: 15
  Files Processing: 1
  Last Scan: 14:32:15

Pipeline Status:
  Executions Today: 14
  Success Rate: 93%
  Average Duration: 2m 34s

AI Models:
  Mistral: Healthy (45ms avg)
  DeepSeek: Healthy (62ms avg)
  CodeGemma: Healthy (38ms avg)
  Gemma2: Healthy (125ms avg)
```

### View Logs

```bash
# Tail logs
aires logs --tail 50

# Search logs
aires logs grep "CS0117"

# Export metrics
aires metrics export --date today
```

## 🔧 Configuration Reference

Key configuration sections in `aires.ini`:

- **[Watchdog]**: Directory monitoring settings
- **[Pipeline]**: AI pipeline configuration
- **[AI]**: Model-specific settings
- **[Booklet]**: Output format and naming
- **[Logging]**: Log levels and outputs
- **[Monitoring]**: Metrics and health checks

## 🐛 Troubleshooting

### Common Issues

1. **Files not being detected**
   - Check InputDirectory path in aires.ini
   - Verify file permissions
   - Check logs for errors

2. **AI models not responding**
   - Ensure Ollama is running
   - Verify models are installed
   - Check network connectivity

3. **Booklets not generated**
   - Verify OutputDirectory exists and is writable
   - Check AI pipeline logs
   - Ensure sufficient disk space

## 📝 License

AIRES is proprietary software. All rights reserved.

## 🤝 Contributing

AIRES is currently a closed-source project. For bug reports or feature requests, please contact the development team.

## 📞 Support

For support, documentation, or questions:
- Documentation: `/docs/AIRES/`
- Logs: Check `/logs/aires-{date}.log`
- Status: Run `aires status`

---

**Remember**: AIRES is designed for complete autonomy. Configure it once, and let it prevent the compiler trap forever!