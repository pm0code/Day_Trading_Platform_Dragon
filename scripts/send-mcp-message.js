const redis = require('redis');

async function sendMessage() {
    const client = redis.createClient({
        host: 'localhost',
        port: 6379
    });

    await client.connect();

    const message = {
        agent: 'tradingagent',
        from: 'tradingagent',
        to: 'mcp-agent',
        type: 'query',
        subject: 'Logging Design Documentation',
        message: 'Hi MCP agent! I understand you have worked on logging design for the Day Trading Platform. Could you please share the design documents and any recommendations for the logging implementation? I am currently evaluating ETW (Event Tracing for Windows) for zero-allocation logging in the critical trading path. Your insights would be very valuable.',
        timestamp: new Date().toISOString()
    };

    await client.publish('mcp-agent:message', JSON.stringify(message));
    console.log('Message sent to MCP agent');

    // Also send to agenthub
    await client.publish('agenthub:message', JSON.stringify(message));
    console.log('Message sent to agenthub');

    await client.quit();
}

sendMessage().catch(console.error);