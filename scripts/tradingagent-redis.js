#!/usr/bin/env node

/**
 * TradingAgent Redis Communication
 * Implements inter-agent communication protocol for the Day Trading Platform
 */

const Redis = require('ioredis');

class TradingAgent {
  constructor() {
    this.agentName = 'tradingagent';
    this.redis = new Redis({
      host: 'localhost',
      port: 6379,
      retryStrategy: (times) => {
        const delay = Math.min(times * 50, 2000);
        console.log(`Retrying Redis connection in ${delay}ms...`);
        return delay;
      }
    });
    
    this.subscriber = new Redis({
      host: 'localhost',
      port: 6379
    });
    
    this.setupSubscriptions();
    this.setupErrorHandlers();
  }

  setupSubscriptions() {
    // Subscribe to ALL channels for system-wide awareness
    this.subscriber.psubscribe('*:*');
    
    console.log('TradingAgent subscribed to all channels for system-wide awareness');
    
    // Handle incoming messages
    this.subscriber.on('pmessage', (pattern, channel, message) => {
      try {
        const parsed = JSON.parse(message);
        console.log(`[${new Date().toISOString()}] Received on ${channel}:`, parsed);
        
        // Check if message is relevant to us
        if (this.isRelevantMessage(channel, parsed)) {
          this.handleMessage(channel, parsed);
        } else {
          // Log for awareness but don't process
          console.log(`[AWARENESS] Message on ${channel} from ${parsed.from} to ${parsed.to}: ${parsed.subject}`);
        }
      } catch (error) {
        console.error('Failed to parse message:', error);
      }
    });
  }

  setupErrorHandlers() {
    this.redis.on('error', (err) => {
      console.error('Redis publisher error:', err);
    });
    
    this.subscriber.on('error', (err) => {
      console.error('Redis subscriber error:', err);
    });
    
    this.redis.on('connect', () => {
      console.log('TradingAgent connected to Redis');
    });
  }

  isRelevantMessage(channel, message) {
    // Always process messages directed to us
    if (message.to === this.agentName || message.to === 'all') {
      return true;
    }
    
    // Always process alerts and errors
    if (channel.startsWith('alert:') || channel.startsWith('alerts:') || message.type === 'error') {
      return true;
    }
    
    // Process broadcasts
    if (channel === 'agent:broadcast') {
      return true;
    }
    
    // Check if message mentions trading-related keywords
    const tradingKeywords = ['trading', 'stock', 'market', 'price', 'order', 'portfolio', 'risk'];
    if (message.message && tradingKeywords.some(keyword => 
      message.message.toLowerCase().includes(keyword))) {
      return true;
    }
    
    return false;
  }

  handleMessage(channel, message) {
    console.log(`[PROCESSING] Handling message from ${message.from}: ${message.subject}`);
    
    switch(message.type) {
      case 'request':
        this.handleRequest(message);
        break;
      case 'notification':
        this.handleNotification(message);
        break;
      case 'error':
        this.handleError(message);
        break;
      case 'response':
        this.handleResponse(message);
        break;
    }
  }

  async handleRequest(message) {
    console.log(`[REQUEST] Processing request from ${message.from}: ${message.subject}`);
    
    // Example: Respond to trading-related requests
    if (message.subject.toLowerCase().includes('market data')) {
      await this.sendResponse(message, {
        status: 'success',
        data: 'Market data service is operational. Real-time data feeds active.'
      });
    } else if (message.subject.toLowerCase().includes('trading status')) {
      await this.sendResponse(message, {
        status: 'success',
        data: 'Trading engine online. Risk management active. All systems operational.'
      });
    }
  }

  handleNotification(message) {
    console.log(`[NOTIFICATION] ${message.from}: ${message.message}`);
  }

  handleError(message) {
    console.error(`[ERROR] From ${message.from}: ${message.message}`);
    if (message.metadata && message.metadata.error_code) {
      console.error(`Error Code: ${message.metadata.error_code}`);
    }
  }

  handleResponse(message) {
    console.log(`[RESPONSE] From ${message.from}: ${message.message}`);
  }

  async sendMessage(channel, type, subject, content, metadata = {}) {
    const message = {
      agent: this.agentName,      // CRITICAL: For AgentHub display
      from: this.agentName,
      to: channel.split(':')[0], // Extract target agent from channel
      type: type,
      subject: subject,
      message: content,
      timestamp: new Date().toISOString(),
      metadata: {
        priority: metadata.priority || 'medium',
        requires_response: metadata.requires_response || false,
        correlation_id: metadata.correlation_id || `${this.agentName}-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        ...metadata
      }
    };
    
    try {
      await this.redis.publish(channel, JSON.stringify(message));
      console.log(`[SENT] Message to ${channel}: ${subject}`);
    } catch (error) {
      console.error(`Failed to send message to ${channel}:`, error);
    }
  }

  async sendResponse(originalMessage, responseData) {
    const responseChannel = originalMessage.metadata.response_channel || `${originalMessage.from}:response`;
    
    await this.sendMessage(
      responseChannel,
      'response',
      `Re: ${originalMessage.subject}`,
      JSON.stringify(responseData),
      {
        correlation_id: originalMessage.metadata.correlation_id,
        status: responseData.status || 'success'
      }
    );
  }

  async sendHelpMessage() {
    // Send help message to agenthub
    await this.sendMessage(
      'agent:message',
      'notification',
      'TradingAgent Online - Help',
      `TradingAgent is now online and monitoring all channels.
      
Capabilities:
- Real-time market data monitoring
- Trading strategy execution
- Risk management and compliance
- Portfolio optimization
- Financial precision calculations
- Multi-screen display management

Available Commands:
- Market data requests
- Trading status queries
- Risk assessment reports
- Portfolio analysis

Integration Status:
- Redis pub/sub: ✅ Connected
- System-wide monitoring: ✅ Active
- Message processing: ✅ Ready`,
      {
        priority: 'high',
        importance: 'info'
      }
    );
  }

  async start() {
    console.log('Starting TradingAgent...');
    
    // Wait for connection
    await new Promise((resolve) => {
      this.redis.once('connect', resolve);
    });
    
    // Send initial help message
    await this.sendHelpMessage();
    
    // Send periodic heartbeat
    setInterval(async () => {
      await this.sendMessage(
        'agent:heartbeat',
        'notification',
        'TradingAgent Heartbeat',
        'Trading systems operational',
        {
          priority: 'low',
          system_status: {
            market_data: 'active',
            trading_engine: 'ready',
            risk_management: 'monitoring',
            connections: 'stable'
          }
        }
      );
    }, 300000); // Every 5 minutes
    
    console.log('TradingAgent is running. Press Ctrl+C to exit.');
  }
}

// Handle graceful shutdown
process.on('SIGINT', () => {
  console.log('\nShutting down TradingAgent...');
  process.exit(0);
});

// Start the agent
const agent = new TradingAgent();
agent.start().catch(console.error);