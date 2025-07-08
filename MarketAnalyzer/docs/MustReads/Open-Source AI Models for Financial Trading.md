# Open-Source AI Models for Financial Trading

| \# | Model | Origin \& Format | Key Capabilities | License | Security / Limitations | Financial Use Example |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| 1 | **Prophet** | Meta (GitHub facebook/prophet). Python + R; wheels \& ONNX converters available[1][2] | Additive time-series forecasting with holiday \& seasonality support; handles missing data/outliers | MIT[3][4] | Mature (>19 k ⭐); minimal deps (Stan, NumPy). No built-in GPU; not ideal for intraday tick data | Portfolio P\&L forecasting \& sales/stock-price studies in banking teams since 2017[5] |
| 2 | **NeuralProphet** | Community (ourownstory/​neural_prophet). PyTorch[6] | Adds AR lags, covariates \& deep layers to Prophet; interpretable components; multi-seasonal | MIT[7] | Active Slack; beta status—model drift warnings. Needs GPU for large horizons | Silver price prediction tutorial incl. volatility features[8] |
| 3 | **GluonTS (DeepAR, N-HiTS, etc.)** | AWS Labs (Apache MXNet / PyTorch)[9][10] | Toolkit of probabilistic forecasters (DeepAR, Transformer, N-HiTS) + evaluation utilities | Apache 2.0[11] | Backed by Amazon research; heavy MXNet deps; requires tuning to curb over-fitting | Energy-demand and supply-chain demand forecasts in SageMaker example[12] |
| 4 | **PyTorch-Forecasting (TFT, N-BEATS, N-HiTS)** | Open-source (pytorch-forecasting)[13] | Ready-to-use neural models incl. Temporal Fusion Transformer, N-BEATS, covariate handling; interpretability dashboards | MIT[14] | Relies on PyTorch Lightning; memory intensive for long horizons; monitor GPU VRAM | TFT used for Indonesian stock-price prediction, SMAPE 0.2% on IDX data[15] |
| 5 | **AutoGluon-TimeSeries** | AWS AI (autogluon)[16][17] | AutoML ensembles of statistical + DL models (ETS, DeepAR, Chronos, LightGBM); zero-shot option | Apache 2.0[18][19] | Automated CV, but user should lock look-back window to avoid data leakage[20]; checksums in wheels | 3-line demo producing SKU-level demand forecasts for retail planning[17] |
| 6 | **Chronos (pre-trained)** | Amazon Science (chronos-forecasting)[21] | Zero-shot transformer pretrained on 400+ corpora; fast quantile forecasts; ONNX export | Apache 2.0 (repo)[21] | Model weights ~120 MB; pretrained on generic series—fine-tune to avoid domain shift | Bolt variant yields 5% lower MAE than DeepAR on equities minute bars[21] |
| 7 | **N-BEATS / N-HiTS** | ServiceNow Research (N-BEATS repo) \& PyTorch-Forecasting[22][23] | Forward-backward residual MLP stacks; interpretable trend/seasonality basis | MIT[23][24] | Competitive (M4 winner); large ensembles can be resource heavy | S\&P 500 price forecasting beats LSTM / GRU in RMSE benchmarks[25] |
| 8 | **LSTNet** | Research (CMU/​NYU). Multiple PyTorch repos[26][27] | CNN + GRU network capturing short- \& long-term patterns; skip connections | MIT[28] | Original code MXNet; community ports vary in quality; limited GPU support for >1 M series | Crypto-exchange price prediction notebook with LSTNet beats ARIMA in RSE[29] |
| 9 | **FinRL** | AI4Finance Foundation (FinRL)[30] | End-to-end deep-RL library (DQN, PPO, SAC, ensemble); market simulators; risk metrics | MIT[31] | Strong academic backing; be cautious of back-test over-fitting; paper-trading API sandbox[32] | Training PPO agent on Dow-30 achieved 1.5× Sharpe vs. S\&P benchmark[33] |
| 10 | **TensorTrade** | TensorTrade-org[34] | Modular RL framework (Gym envs, OMS, live \& simulated exchanges) for signal generation \& portfolio/risk mgmt | Apache 2.0[35][36] | In beta; users must secure broker keys; relies on TensorFlow-2 \& Pandas | Demo on AWS SageMaker trains DQN to trade BTC/USD with rule-based slippage[37] |

### How to Choose

1. **Forecasting \& Anomaly Detection** – Prophet/NeuralProphet for fast baselines; AutoGluon or PyTorch-Forecasting for advanced ensembles and explainability; Chronos for low-latency inference.
2. **Risk \& Signal Generation** – Combine GluonTS or TFT forecasts with FinRL/TensorTrade RL agents to incorporate risk-adjusted actions.
3. **Enterprise Security** – All listed projects use permissive MIT/Apache 2.0 licenses permitting commercial modification. Review supply-chain CVEs and pin dependency versions for reproducible builds.

These models cover the full workflow—from generating high-quality forecasts to deploying reinforcement-learning traders—while remaining free, open-source and suitable for commercial integration.

