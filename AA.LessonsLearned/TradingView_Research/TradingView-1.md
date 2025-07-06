TradingView offers a suite of Application Programming Interfaces (APIs) primarily designed for brokers and developers to integrate TradingView's powerful charting and trading functionalities into their own applications and platforms. It's important to note that TradingView does not offer a simple, public REST or WebSocket API for individual retail traders to programmatically access market data or execute trades directly. Instead, their APIs are geared towards more extensive integrations.

Here's a detailed breakdown of TradingView's API offerings:

### Charting Library API
The **Charting Library** is a standalone solution that allows you to embed and customize TradingView's advanced charts directly into your own web application.

* **Offerings**:
    * Display highly interactive and customizable financial charts.
    * Access a vast array of drawing tools and technical indicators.
    * Customize the user interface, themes, and chart layouts.
    * Handle user interactions through a rich set of events and methods.

* **Endpoints**: The Charting Library itself does not have traditional REST or WebSocket endpoints that you call. Instead, you interact with it through a JavaScript API within your application. You are responsible for providing the data to the library.

* **Programming Language**: The Charting Library is written in **JavaScript**. You can also find a TypeScript definition file for easier integration.

* **Documentation**:
    * **API Reference**: [https://www.tradingview.com/charting-library-docs/latest/api/](https://www.tradingview.com/charting-library-docs/latest/api/)
    * **Getting Started**: [https://www.tradingview.com/charting-library-docs/](https://www.tradingview.com/charting-library-docs/)

* **Pricing**: Access to the Charting Library is not publicly priced and is available for commercial use. You need to contact TradingView directly to obtain a license.

---
### Broker Integration API
This API is designed for brokers who want to connect their backend systems to the TradingView platform, allowing TradingView users to trade with that broker directly from the TradingView interface.

* **Offerings**:
    * Enable order placement (market, limit, etc.) directly from TradingView charts.
    * Provide real-time market data, including quotes and depth of market.
    * Manage multiple user accounts and trading configurations.

* **Endpoints (REST API)**: Brokers must implement a series of REST endpoints that TradingView's servers will call. These include:
    * `/accounts`: Get a list of user accounts.
    * `/config`: Retrieve localized configuration settings.
    * `/instruments`: Get the list of tradable instruments.
    * `/orders`: Place, modify, or cancel orders.
    * `/ordersHistory`: Get the history of orders.
    * `/positions`: Get, modify, or close positions.
    * `/quotes`: Retrieve current bid/ask prices.
    * `/history`: Provide historical bar data.
    * A full list of required and optional endpoints is available in the documentation.

* **Streaming**: For real-time updates, the Broker Integration API uses HTTP streaming, not WebSockets. TradingView's client maintains a persistent connection to a `/streaming` endpoint to receive real-time data.

* **Programming Language**: Since brokers implement the server-side endpoints, they can use any programming language for their backend (e.g., Python, Java, Node.js, etc.).

* **Documentation**:
    * **Broker Integration Manual**: [https://www.tradingview.com/broker-api-docs/](https://www.tradingview.com/broker-api-docs/)
    * **REST API Specification**: [https://www.tradingview.com/broker-api-docs/rest-api-spec/](https://www.tradingview.com/broker-api-docs/rest-api-spec/)

* **Pricing**: This is a partnership agreement between TradingView and the brokerage. Pricing and terms are not publicly available and are negotiated directly.

---
### Lightweight Charts™
This is a free, open-source library for creating simple, interactive financial charts. It's a less feature-rich alternative to the full Charting Library.

* **Offerings**:
    * Create fast and responsive financial charts.
    * Basic customization of chart appearance.
    * Ideal for applications that need simple and performant charts without the extensive features of the full library.

* **Endpoints**: Similar to the Charting Library, you interact with it via a JavaScript API. You are responsible for providing the data.

* **Programming Language**: **JavaScript**.

* **Documentation**:
    * **Lightweight Charts™ Documentation**: [https://tradingview.github.io/lightweight-charts/](https://tradingview.github.io/lightweight-charts/)

* **Pricing**: **Free and open-source**.

---
### Webhooks
For users with a paid TradingView plan, webhooks provide a way to send POST requests to a specified URL when an alert is triggered. This is the closest thing to a real-time data push that an individual user can configure without a full brokerage integration.

* **Offerings**:
    * Send a notification with the alert message to a URL you control.
    * Can be used to trigger actions in other applications, such as a trading bot.

* **Endpoints**: You provide a URL endpoint that can accept a POST request. TradingView will send the request to this endpoint.

* **Programming Language**: You can process the incoming webhook request using any backend programming language (e.g., Python with Flask, Node.js with Express).

* **Documentation**:
    * **About Webhooks**: [https://www.tradingview.com/support/solutions/43000529348-about-webhooks/](https://www.tradingview.com/support/solutions/43000529348-about-webhooks/)

* **Pricing**: Available with paid TradingView user plans, starting from the Pro plan.

### Unofficial APIs
It's worth noting that there are unofficial, third-party libraries and APIs that attempt to scrape or interface with TradingView's data. These are not officially supported by TradingView, and their reliability and legality can be questionable. An example is the `tradingview-api` found on GitHub. Using such libraries is at your own risk.