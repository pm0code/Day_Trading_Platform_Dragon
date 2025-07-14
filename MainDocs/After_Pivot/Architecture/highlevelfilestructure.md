
MarketAnalyzer/
├── src/
│   ├── Foundation/
│   │   ├── MarketAnalyzer.Foundation/          # Canonical patterns, base classes
│   │   └── MarketAnalyzer.Foundation.Tests/    
│   ├── Domain/
│   │   ├── MarketAnalyzer.Domain/              # Core business logic
│   │   └── MarketAnalyzer.Domain.Tests/        
│   ├── Infrastructure/
│   │   ├── MarketAnalyzer.Infrastructure.MarketData/
│   │   ├── MarketAnalyzer.Infrastructure.TechnicalAnalysis/
│   │   ├── MarketAnalyzer.Infrastructure.AI/
│   │   ├── MarketAnalyzer.Infrastructure.Storage/
│   │   └── MarketAnalyzer.Infrastructure.Caching/
│   ├── Application/
│   │   ├── MarketAnalyzer.Application/         # Use cases, orchestration
│   │   └── MarketAnalyzer.Application.Tests/   
│   └── Presentation/
│       ├── MarketAnalyzer.Desktop/             # WinUI 3 application
│       └── MarketAnalyzer.Desktop.Tests/      
|
| 
├─ AIACBWD  <-- AIACBWD.WatchdogConfiguration.cs  must be part of this code structure
	|─ whatever...
	|─ whatever..
	|─ AIRES   <-- AIRES.WatchdogConfiguration.cs must be part of this code structure
	|	|─ whatever..
	|	|─ whatever..
	|	|─ whatever..
	|─ whatever..
	|─ whatever..
	|─ whatever..