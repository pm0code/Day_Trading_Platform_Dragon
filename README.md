# Day Trading Platform

A high-performance, institutional-grade day trading platform built with C# .NET 8.0, featuring advanced ML/AI algorithms, ultra-low latency execution, and professional multi-screen support.

## <� Target Configuration

**Primary Target: Professional Configuration (4 Monitors)**
- **CPU**: Intel i7-13700K or AMD Ryzen 7 7700X
- **RAM**: 32GB DDR4-3600 or DDR5-5600
- **GPU**: NVIDIA RTX 4060 or AMD RX 7600
- **Storage**: 1TB NVMe SSD + 2TB backup drive
- **Monitors**: 4 � 27-32" displays (4K primary, 1440p secondary)
- **Expandable**: Designed to scale up to 6+ monitors (Enterprise configuration)
- **Performance**: <50ms latency target, 10,000+ messages/second

## =� Key Features

### Core Trading Capabilities
-  Real-time market data ingestion (AlphaVantage, Finnhub)
-  Advanced screening engine with multiple criteria
-  Risk management with 12 Golden Rules implementation
-  Paper trading and backtesting engine
-  FIX protocol support for direct market access

### AI/ML Pipeline
-  **XGBoost**: Price prediction with <50ms inference
-  **LSTM**: Deep learning for pattern recognition
-  **Random Forest**: Multi-factor stock ranking
-  **RAPM**: Risk-Adjusted Profit Maximization
- =� **SARI**: Stress-Adjusted Risk Index (in progress)

### Performance Features
- High-frequency trading support
- Lock-free data structures for ultra-low latency
- GPU acceleration with CUDA
- TimescaleDB for microsecond-precision data
- Redis messaging for distributed processing

### Multi-Screen Trading Interface
- Zone-based layout (Primary, Secondary, Peripheral, Vertical)
- Cognitive load management
- Market-adaptive UI (different layouts for market hours vs after-hours)
- Voice command integration
- Professional color psychology and typography

## =� Architecture

```
                                                             
                    Multi-Screen WinUI 3 Interface            
                                                             $
                         ML/AI Pipeline                       
  XGBoost  LSTM  Random Forest  RAPM  SARI              
                                                             $
                     Core Trading Engine                      
  Screening  Risk Management  Order Execution  Backtesting
                                                             $
                    Data Ingestion Layer                      
  AlphaVantage  Finnhub  Twitter  Reddit  SEC EDGAR     
                                                             $
                 Infrastructure Services                      
  TimescaleDB  Redis  WebSocket  FIX Protocol            
                                                             
```

## =� Technology Stack

- **Platform**: .NET 8.0, C# 12
- **UI Framework**: WinUI 3
- **ML/AI**: ML.NET, TensorFlow.NET, ONNX Runtime
- **Database**: TimescaleDB, Redis
- **Messaging**: WebSocket, FIX Protocol
- **Testing**: xUnit, BenchmarkDotNet
- **DevOps**: Docker, GitHub Actions

## =� Prerequisites

- Windows 11 (x64)
- .NET 8.0 SDK
- Visual Studio Code or Visual Studio 2022
- Docker Desktop (for TimescaleDB and Redis)
- NVIDIA CUDA Toolkit (for GPU acceleration)

## =� Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/DayTradingPlatform.git
   cd DayTradingPlatform
   ```

2. **Navigate to solution directory**
   ```bash
   cd DayTradinPlatform
   ```

3. **Run the application (First-time setup)**
   ```bash
   dotnet run --project TradingPlatform.Console
   ```
   
   On first run, the application will:
   - Detect this is your first time
   - Launch the secure configuration wizard
   - Ask for your API keys (encrypted with AES-256 + DPAPI)
   - Store them securely - you'll never need to enter them again

4. **Get Free API Keys**
   - **AlphaVantage** (Required): https://www.alphavantage.co/support/#api-key
   - **Finnhub** (Required): https://finnhub.io/register
   - **IEX Cloud** (Optional): https://iexcloud.io
   - **Polygon** (Optional): https://polygon.io
   - **Twelve Data** (Optional): https://twelvedata.com

5. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the solution**
   ```bash
   dotnet build --configuration Release
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

6. **Start the platform**
   ```bash
   dotnet run --project TradingPlatform.Core
   ```

## =� Documentation

- [Product Requirements Document (PRD)](MainDocs/V1.x/PRD_Modular_High-Performance-DTP.md)
- [Engineering Design Document (EDD)](MainDocs/EDD_Modular_High-Performance-DTP.md)
- [Multi-Screen Layout Research](ResearchDocs/Multi-Screen_Layout_and_Information_Architecture_for_Day_Trading_Platforms.md)
- [Master Todo List](MainDocs/V1.x/Master_ToDo_List.md)
- [12 Golden Rules of Day Trading](MainDocs/The_12_Golden_Rulesof_Day_Trading.md)

## >� Testing

The platform includes comprehensive testing:
- Unit tests with 80%+ coverage requirement
- Integration tests for all major components
- Performance tests targeting <50ms latency
- Chaos engineering tests for resilience
- GPU acceleration benchmarks

## =' Configuration

Create `appsettings.json` in the project root:

```json
{
  "MarketData": {
    "AlphaVantage": {
      "ApiKey": "YOUR_API_KEY",
      "BaseUrl": "https://www.alphavantage.co/query"
    },
    "Finnhub": {
      "ApiKey": "YOUR_API_KEY",
      "BaseUrl": "https://finnhub.io/api/v1"
    }
  },
  "Database": {
    "TimescaleDB": {
      "ConnectionString": "Host=localhost;Database=trading;Username=trader;Password=your_password"
    },
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  }
}
```

## =� Performance Targets

- **Order Execution**: <50ms end-to-end
- **Market Data Processing**: 10,000+ messages/second
- **Chart Loading**: <2 seconds
- **Screen Switching**: Instantaneous
- **ML Inference**: <50ms per prediction

## > Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## =� License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## =O Acknowledgments

- Inspired by institutional trading platforms
- Built following the 12 Golden Rules of Day Trading
- Leveraging state-of-the-art ML/AI research
- Multi-screen layout based on professional trading desk standards

## =� Support

For support, email support@daytradingplatform.com or join our Discord community.

---

**Note**: This platform is designed for educational and professional use. Always practice responsible trading and risk management.