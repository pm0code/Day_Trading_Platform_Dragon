# TradingPlatform.ML - Machine Learning Module

## Overview

The TradingPlatform.ML module provides AI/ML capabilities for the Day Trading Platform, implementing predictive models for price forecasting, pattern recognition, and stock ranking.

## Features

### Implemented (Task 11.1 ✅)
- **ML.NET Integration**: Core ML framework with XGBoost, FastTree support
- **ONNX Runtime**: Model interoperability and cross-platform inference
- **TensorFlow.NET**: Deep learning capabilities for LSTM implementation
- **Canonical ML Service**: Base class for consistent ML service patterns
- **Feature Engineering**: Technical indicators, microstructure features, time features
- **Model Interfaces**: Standardized contracts for all ML models

### Planned Features
- **XGBoost Price Prediction** (Task 11.2-11.8)
- **LSTM Pattern Recognition** (Task 12)
- **Random Forest Stock Ranking** (Task 13)
- **GPU Acceleration** (Task 16)
- **Real-time Inference Pipeline** (<50ms latency)
- **Model Explainability** (SHAP/LIME)

## Architecture

```
TradingPlatform.ML/
├── Interfaces/          # Model contracts and interfaces
│   └── IMLModel.cs     # Base interfaces for all models
├── Models/             # Model definitions and data structures
│   └── ModelDefinitions.cs
├── Services/           # ML service implementations
│   └── CanonicalMLService.cs
├── Features/           # Feature engineering pipelines
│   └── FeatureEngineering.cs
├── Training/           # Model training implementations
├── Inference/          # Real-time inference engines
├── Data/              # Dataset management
└── Utilities/         # Helper functions
```

## Dependencies

- **ML.NET 3.0.1**: Core ML framework
- **TensorFlow.NET 0.150.0**: Deep learning
- **ONNX Runtime 1.17.1**: Model interoperability
- **Accord.NET 3.8.0**: Additional ML algorithms
- **Microsoft.Data.Analysis**: Data manipulation

## Usage

### Feature Extraction
```csharp
var features = FeatureEngineering.ExtractTechnicalFeatures(marketData, currentIndex);
var microFeatures = FeatureEngineering.ExtractMicrostructureFeatures(snapshot, recentData);
var timeFeatures = FeatureEngineering.ExtractTimeFeatures(DateTime.Now);
```

### Model Training (Coming Soon)
```csharp
var model = new XGBoostPriceModel();
var result = await model.TrainAsync(dataset, options);
```

### Prediction (Coming Soon)
```csharp
var prediction = await model.PredictAsync(input);
Console.WriteLine($"Predicted price: {prediction.PredictedPrice:C}");
Console.WriteLine($"Confidence: {prediction.Confidence:P}");
```

## Performance Requirements

- **Inference Latency**: <50ms for real-time predictions
- **Training**: GPU-accelerated for large datasets
- **Memory**: Efficient feature extraction with minimal allocations
- **Throughput**: 1000+ predictions/second

## Next Steps

1. Implement XGBoost model training pipeline (Task 11.3-11.4)
2. Create data preprocessing and normalization (Task 11.3)
3. Build model serving infrastructure (Task 11.6)
4. Implement real-time inference engine (Task 11.7)

## Contributing

Follow the canonical patterns established in the codebase. All ML services should inherit from `CanonicalMLService` for consistent behavior.