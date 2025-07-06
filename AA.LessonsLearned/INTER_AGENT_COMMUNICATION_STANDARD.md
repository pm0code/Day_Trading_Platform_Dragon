# Inter-Agent Communication Standard - Complete Guide

**Version**: 2.0  
**Last Updated**: 2025-07-02  
**Status**: MANDATORY for all agents

## 🚨 CRITICAL: Agent Name Display Fix

**If your messages show as "unknown" or "Redis channel" in AgentHub, you MUST include the `agent` field in all messages. See Message Format section below.**

## Overview

This guide explains how agents communicate with each other using Redis pub/sub channels. All agents in the AgentHub ecosystem MUST follow these standards for consistent, reliable inter-agent messaging.

## Quick Start for Your Agent

### 1. Add to Your CLAUDE.md

```markdown
## Agent Identity and Communication

### My Agent Identity
**AGENT NAME**: [Your Agent Name]  <!-- CRITICAL: Use exact name below -->
**PRIMARY CHANNEL**: [youragent]:*  <!-- Your channel prefix -->

<!-- Known Agent Names:
- MCP Agent (mcp:*)
- Document Manager (docs:*)
- Linear Agent (linear:*)
- Day Trading Agent (daytrading:*)
- VM Suite Agent (vmsuite:*)
-->
```

### 2. Publishing Messages (Three Ways)

#### Option 1: Use the Utility (RECOMMENDED)
```bash
# Always include your agent name as the third parameter
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "channel:type" "Your message" "Your Agent Name"

# Example:
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "mcp:notification" "Analysis complete" "MCP Agent"
```

#### Option 2: Direct Redis with Correct Format
```bash
redis-cli PUBLISH "channel:type" '{"agent": "Your Agent Name", "from": "Your Agent Name", "to": "target", "type": "notification", "message": "Your message", "timestamp": "2025-07-02T20:00:00Z"}'
```

#### Option 3: In Your Code
```javascript
const Redis = require('ioredis');
const redis = new Redis();

async function sendMessage(channel, message) {
  const payload = {
    // BOTH fields required to work everywhere
    agent: 'Your Agent Name',  // CRITICAL: For AgentHub display
    from: 'Your Agent Name',   // For inter-agent standard
    to: 'target-agent',
    type: 'notification',
    subject: message.subject,
    message: message.content,
    timestamp: new Date().toISOString(),
    metadata: {
      priority: 'medium',
      requires_response: false
    }
  };
  
  await redis.publish(channel, JSON.stringify(payload));
}
```

### 3. Subscribe to Messages
```javascript
// Subscribe to ALL channels for system awareness
subscriber.psubscribe('*:*');

subscriber.on('pmessage', (pattern, channel, message) => {
  try {
    const parsed = JSON.parse(message);
    
    // Check if message is for you
    if (parsed.to === 'Your Agent Name' || parsed.to === 'all') {
      handleMessage(parsed);
    }
  } catch (error) {
    console.error('Failed to parse message:', error);
  }
});
```

## Complete Message Format

### Required Fields
```json
{
  "agent": "Source Agent Name",     // CRITICAL: Your agent's display name
  "from": "Source Agent Name",      // Sender identification
  "to": "Target Agent Name",        // Recipient (or "all" for broadcast)
  "type": "notification",           // notification|request|response|error
  "message": "Message content",     // The actual message
  "timestamp": "2025-07-02T20:00:00Z"  // ISO 8601 format
}
```

### Full Format with Optional Fields
```json
{
  "agent": "Source Agent Name",
  "from": "Source Agent Name",
  "to": "Target Agent Name",
  "type": "request|response|notification|error",
  "subject": "Brief subject line",
  "message": "Detailed message content",
  "timestamp": "2025-07-02T20:00:00Z",
  "metadata": {
    "priority": "high|medium|low",
    "requires_response": true,
    "correlation_id": "unique-id-for-tracking",
    "response_channel": "channel:response",
    "timeout_ms": 30000,
    "file_path": "/path/to/shared/file",
    "error_code": "SPECIFIC_ERROR",
    "retry_count": 0
  }
}
```

## Channel Naming Convention

Each agent has designated channels:
- `agent:*` - General agent communication
- `linear:*` - Linear Bug Tracker agent
- `mcp:*` - MCP Analyzer agent
- `docs:*` - Documentation agent
- `daytrading:*` - Day Trading agent
- `vmsuite:*` - VMSuite agent
- `alert:*` or `alerts:*` - System-wide alerts
- `agent:broadcast` - Broadcast to all agents

## Message Types

### 1. Notification (One-way information)
```json
{
  "agent": "MCP Agent",
  "from": "MCP Agent",
  "to": "all",
  "type": "notification",
  "subject": "Analysis Complete",
  "message": "Code analysis completed with 0 critical issues",
  "timestamp": "2025-07-02T20:00:00Z"
}
```

### 2. Request (Expects response)
```json
{
  "agent": "Linear Agent",
  "from": "Linear Agent",
  "to": "MCP Agent",
  "type": "request",
  "subject": "Analyze Code",
  "message": "Please analyze /src/api for security issues",
  "timestamp": "2025-07-02T20:00:00Z",
  "metadata": {
    "requires_response": true,
    "correlation_id": "req-123456",
    "response_channel": "linear:response",
    "timeout_ms": 30000
  }
}
```

### 3. Response (Reply to request)
```json
{
  "agent": "MCP Agent",
  "from": "MCP Agent",
  "to": "Linear Agent",
  "type": "response",
  "subject": "Re: Analyze Code",
  "message": "Analysis complete. Found 2 medium issues.",
  "timestamp": "2025-07-02T20:05:00Z",
  "metadata": {
    "correlation_id": "req-123456",
    "status": "success"
  }
}
```

### 4. Error (Problem notification)
```json
{
  "agent": "Document Manager",
  "from": "Document Manager",
  "to": "Linear Agent",
  "type": "error",
  "subject": "Database Connection Failed",
  "message": "Cannot connect to PostgreSQL on port 5432",
  "timestamp": "2025-07-02T20:10:00Z",
  "metadata": {
    "error_code": "DB_CONNECTION_FAILED",
    "recoverable": true,
    "retry_count": 3
  }
}
```

## Implementation Examples

### Node.js Complete Example
```javascript
const Redis = require('ioredis');
const publisher = new Redis();
const subscriber = new Redis();

// Configuration
const MY_AGENT_NAME = 'MCP Agent';  // YOUR AGENT NAME HERE
const MY_CHANNELS = ['mcp:*', 'agent:*', 'alert:*'];

// Subscribe to channels
MY_CHANNELS.forEach(channel => subscriber.psubscribe(channel));

// Also subscribe to all channels for awareness
subscriber.psubscribe('*:*');

// Handle incoming messages
subscriber.on('pmessage', async (pattern, channel, message) => {
  try {
    const msg = JSON.parse(message);
    console.log(`[${channel}] From ${msg.from}: ${msg.message}`);
    
    // Check if message is for us
    if (msg.to === MY_AGENT_NAME || msg.to === 'all') {
      await handleMessage(msg, channel);
    }
  } catch (error) {
    console.error('Message parse error:', error);
  }
});

// Send a message
async function sendMessage(channel, to, type, subject, content, metadata = {}) {
  const message = {
    agent: MY_AGENT_NAME,  // CRITICAL for AgentHub
    from: MY_AGENT_NAME,
    to: to,
    type: type,
    subject: subject,
    message: content,
    timestamp: new Date().toISOString(),
    metadata: metadata
  };
  
  await publisher.publish(channel, JSON.stringify(message));
  console.log(`Sent ${type} to ${channel}`);
}

// Handle different message types
async function handleMessage(msg, channel) {
  switch (msg.type) {
    case 'request':
      // Process request and send response
      const result = await processRequest(msg);
      await sendMessage(
        msg.metadata.response_channel || `${msg.from.toLowerCase()}:response`,
        msg.from,
        'response',
        `Re: ${msg.subject}`,
        result,
        { correlation_id: msg.metadata.correlation_id }
      );
      break;
      
    case 'notification':
      console.log(`Notification: ${msg.subject} - ${msg.message}`);
      break;
      
    case 'error':
      console.error(`Error from ${msg.from}: ${msg.message}`);
      break;
  }
}
```

### Python Example
```python
import redis
import json
from datetime import datetime

r = redis.Redis(host='localhost', port=6379)
pubsub = r.pubsub()

# Configuration
MY_AGENT_NAME = 'Document Manager'  # YOUR AGENT NAME HERE

def send_message(channel, to, msg_type, subject, content, metadata=None):
    message = {
        'agent': MY_AGENT_NAME,  # CRITICAL for AgentHub
        'from': MY_AGENT_NAME,
        'to': to,
        'type': msg_type,
        'subject': subject,
        'message': content,
        'timestamp': datetime.utcnow().isoformat() + 'Z',
        'metadata': metadata or {}
    }
    
    r.publish(channel, json.dumps(message))
    print(f"Sent {msg_type} to {channel}")

# Subscribe and listen
pubsub.psubscribe('docs:*', 'agent:*', '*:*')
for message in pubsub.listen():
    if message['type'] == 'pmessage':
        try:
            data = json.loads(message['data'])
            if data['to'] == MY_AGENT_NAME or data['to'] == 'all':
                handle_message(data)
        except:
            pass
```

## Best Practices

### 1. Always Include Agent Name
```javascript
// ❌ WRONG - Will show as "unknown"
const message = {
  from: "MCP Agent",
  message: "Hello"
};

// ✅ CORRECT - Will show agent name
const message = {
  agent: "MCP Agent",  // CRITICAL!
  from: "MCP Agent",
  message: "Hello"
};
```

### 2. Use Correlation IDs for Request/Response
```javascript
// Request
const correlationId = `req-${Date.now()}-${Math.random()}`;

// Response - include same correlation ID
metadata: { correlation_id: correlationId }
```

### 3. Subscribe to All Channels
- Subscribe to `*:*` for system awareness
- Filter messages in your handler
- Log all activity for debugging

### 4. Handle Errors Gracefully
- Always wrap JSON.parse in try-catch
- Send error responses for failed requests
- Include enough context in error messages

## Testing Your Integration

### 1. Test Your Agent Name Display
```bash
# Test that your name appears correctly
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "youragent:test" "Test message" "Your Agent Name"
```

### 2. Test Publishing
```bash
# Direct Redis test
redis-cli PUBLISH "youragent:test" '{"agent": "Your Agent Name", "from": "Your Agent Name", "to": "test", "type": "notification", "message": "Test 123", "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"}'
```

### 3. Test Subscribing
```bash
# Terminal 1 - Subscribe
redis-cli PSUBSCRIBE "youragent:*"

# Terminal 2 - Send test message
# (use commands from step 2)
```

### 4. Monitor All Traffic
```bash
# Use the monitoring tool
node /home/nader/my_projects/CS/linear-bug-tracker/src/agenthub/webhook-bridge/redis-monitor.js
```

## Channel Mapping to AgentHub

Messages are automatically forwarded to Mattermost:
- `agent:*` → #agenthub-agents
- `linear:*` → #agenthub-linear-tracker
- `mcp:*` → #agenthub-mcp-server
- `docs:*` → #agenthub-docs
- `alert:*` → #agenthub-alerts

## Troubleshooting

### "Unknown" or "Redis Channel" in AgentHub
- **Cause**: Missing `agent` field in message
- **Fix**: Include `agent: "Your Agent Name"` in every message

### Messages Not Received
1. Check Redis: `redis-cli PING`
2. Verify channel pattern matches
3. Check webhook bridge: `pm2 status`
4. Use monitoring tool to see all traffic

### Message Parse Errors
- Validate JSON format
- Check all required fields present
- Verify timestamp is ISO 8601
- Use proper quotes in bash commands

## Common Patterns

### Broadcast to All Agents
```javascript
await sendMessage('agent:broadcast', 'all', 'notification', 
  'System Maintenance', 'Restarting in 5 minutes');
```

### Request with Timeout
```javascript
const timeoutMs = 30000;
const correlationId = `req-${Date.now()}`;

// Set timeout handler
setTimeout(() => {
  console.error(`Request ${correlationId} timed out`);
}, timeoutMs);

// Send request
await sendMessage('target:request', 'Target Agent', 'request',
  'Need data', 'Please send latest stats', {
    correlation_id: correlationId,
    timeout_ms: timeoutMs
  });
```

### File Sharing
```javascript
// 1. Save file
await fs.writeFile('/docs/agents/shared/data.json', data);

// 2. Notify with path
await sendMessage('target:notification', 'Target Agent', 'notification',
  'Data ready', 'File available at shared location', {
    file_path: '/docs/agents/shared/data.json',
    file_size: data.length
  });
```

---

**Remember**: The `agent` field is CRITICAL. Without it, your messages will show as "unknown" in AgentHub!