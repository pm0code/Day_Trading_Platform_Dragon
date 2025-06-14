# **Product Requirements Document (PRD)**

**Day Trading Stock Recommendation Platform** *Incorporating Core Day Trading Criteria & Regulatory Requirements*

---

## **1\. Executive Summary**

**Objective:** Develop a modular, real-time stock recommendation platform that identifies, screens, and monitors stocks meeting rigorous day trading criteria. The platform must incorporate industry-standard technical/fundamental filters, regulatory compliance, and risk management tools to support active traders.

**Key Features:**

- Real-time screening with **12 core day trading criteria** (volatility, liquidity, news catalysts, etc.).  
- Custom alerts, backtesting, and FINRA-compliant risk management.  
- Integration with free and premium data sources (Fidelity, Schwab, TradingView).

---

## **2\. Core Day Trading Criteria Integration**

*All criteria below must be configurable as filters in the screening engine and monitoring system.*

### **2.1 Liquidity Requirements**

- **Average Daily Volume:** ≥1M shares (user-adjustable).  
- **Intraday Volume:** \>100K shares within first 30 minutes (scaling through day).  
- **Bid-Ask Spread:** \<0.1% of stock price (real-time display).

### **2.2 Volatility Requirements**

- **Average True Range (ATR):** ≥$0.25 or ≥1.5% of price (configurable thresholds).  
- **Intraday Price Change:** ≥2% from open (pre-market and post-open filters).

### **2.3 Relative Volume**

- **Relative Volume Ratio:** ≥2x compared to 30-day average.

### **2.4 News Catalysts**

- **Integration:** Real-time news feeds (earnings, FDA approvals, mergers).  
- **Filters:** Flag stocks with news published within last 15 minutes.

### **2.5 Technical Indicators**

- **Required Filters:**  
  - RSI: \<30 (oversold) or \>70 (overbought).  
  - MACD crossovers (bullish/bearish).  
  - 9 EMA/20 SMA crossovers.  
- **Chart Patterns:** Flags, head-and-shoulders, double tops/bottoms (auto-detection).

### **2.6 Fundamental Filters (Optional)**

- **Float:** \<50M shares (low-float stocks).  
- **Price Range:** $1–$50 (user-customizable).

---

## **3\. Functional Requirements**

### **3.1 Real-Time Stock Screening**

- **Data Sources:**  
  - Free APIs: Alpha Vantage, Finnhub (real-time).  
  - Premium Integrations: TradingView, Trade Ideas (future phase).  
- **Custom Screens:**  
  - Save/load templates combining ≥3 criteria (e.g., “ATR \> $0.5 AND relative volume ≥2”).  
  - Exclude sectors or market caps (e.g., avoid penny stocks).

### **3.2 Alert System**

- **Conditions:** Price breaks key level, volume spikes, news events, or technical triggers.  
- **Channels:** In-app, email, SMS (Twilio integration).  
- **Examples:**  
  - “Alert if TSLA volume exceeds 5M shares with RSI \>70.”  
  - “Notify when AAPL has breaking news and ATR \>$1.50.”

### **3.3 Backtesting Engine**

- **Metrics:** Win rate, Sharpe ratio, max drawdown.  
- **Custom Strategies:** Test combinations of core criteria over historical data.  
- **Visualization:** Equity curve, daily P\&L heatmap.

### **3.4 Risk Management**

- **FINRA Compliance:**  
  - PDT rule warnings for accounts under $25k.  
  - Margin requirement calculators (FINRA Rule 4210).  
- **Tools:**  
  - Position sizing: `Risk per trade ≤1% of capital`.  
  - Stop-loss: 2x ATR or below support (auto-suggest).

### **3.5 User Interface**

- **Dashboard:**  
  - Real-time watchlist with volatility/volume metrics.  
  - News ticker and earnings calendar.  
- **Charting:**  
  - TradingView integration (candlestick charts, drawing tools).  
  - Overlay ATR, VWAP, and custom indicators.

### **3.6 Regulatory Compliance**

- **Pattern Day Trading (PDT):**  
  - Track round-trip trades and enforce 3-trade/week limit for sub-$25k accounts.  
- **Settlement Alerts:** Warn about T+2 violations for cash accounts.

---

## **4\. Non-Functional Requirements**

### **4.1 Performance**

- **Latency:** \<1s for screening updates.  
- **Data Accuracy:** 99.9% sync with exchange feeds.

### **4.2 Security**

- **Authentication:** OAuth 2.0 with MFA support.  
- **Data Encryption:** AES-256 for data at rest/in transit.

### **4.3 Extensibility**

- **Modular Design:**  
  - Decoupled data ingestion layer (supports Alpha Vantage → Trade Ideas migration).  
  - Plug-in system for new indicators or brokers.

---

## **5\. Regulatory & Compliance Requirements**

*(Per FINRA, SEC, and search result )*

- **PDT Rule Enforcement:** Block trades exceeding 3 round-trips/week for accounts under $25k.  
- **Margin Warnings:** Highlight risks of 4:1 intraday leverage.  
- **Disclaimers:** Display FINRA-required risk disclosures on login.

---

## **6\. Key Integration Partners**

| Component | Free Tier | Premium Tier (Future) |
| :---- | :---- | :---- |
| **Data** | Alpha Vantage, Finnhub | Trade Ideas, Bloomberg |
| **Brokerage** | Alpaca (paper trading) | Fidelity, Schwab API |
| **Charting** | TradingView (basic) | TradingView Pro |
| **News** | Benzinga API (free) | Seeking Alpha Premium |

---

## **7\. Release Criteria**

- All core criteria filters operational and user-testable.  
- Alerts trigger reliably (\<500ms latency) across email/SMS.  
- Backtesting reproduces historical outcomes within 5% accuracy.  
- FINRA compliance checks implemented for PDT and margin.

---

## **8\. Excluded Scope (V1)**

- Automated trade execution.  
- AI-driven predictive analytics.  
- Social trading features.

---

**This PRD ensures the platform aligns with industry best practices, regulatory standards, and the core criteria day traders rely on for identifying opportunities.** By modularizing data sources and screening logic, the system remains future-proof for seamless integration with premium tools as the user’s needs evolve.  
