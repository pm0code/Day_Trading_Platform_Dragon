# MCP Integration Standard v1.0

**Date**: 2025-07-03  
**Author**: MCP Agent  
**Status**: MANDATORY FOR ALL AGENTS

## 🚨 CRITICAL: MCP Must Work for ALL Agents - NO EXCEPTIONS!

This document defines the mandatory MCP integration standards that ALL agents must follow to ensure proper code analysis and quality enforcement across the entire system.

## Common Integration Issues and Fixes

### 1. Buffer Overflow Errors (ENOBUFS)

**Problem**: execSync calls without proper buffer size limits  
**Solution**: Always set maxBuffer to at least 10MB

```typescript
// ❌ WRONG - Will cause ENOBUFS errors
const result = execSync('command', { encoding: 'utf8' });

// ✅ CORRECT - Proper buffer management
const result = execSync('command', { 
  encoding: 'utf8',
  stdio: 'pipe',
  maxBuffer: 10 * 1024 * 1024 // 10MB buffer
});
```

### 2. Message Bus Payload Errors

**Problem**: Sending raw messages instead of MessageInput format  
**Solution**: Wrap all messages in proper MessageInput structure

```typescript
// ❌ WRONG - Raw message
await messageBus.publish(channel, { data: 'test' });

// ✅ CORRECT - Proper MessageInput format
await messageBus.publish(channel, {
  type: MessageType.EVENT,
  payload: { data: 'test' },
  metadata: {
    source: 'agent-name',
    timestamp: new Date(),
    version: '1.0'
  }
});
```

## Standard MCP Integration Pattern

### 1. Exec Utils Module

Create a standardized exec utils module in your project:

```typescript
// utils/exec.ts
import { exec, execSync, ExecSyncOptions } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

// Standard buffer size for all exec operations
export const STANDARD_BUFFER_SIZE = 10 * 1024 * 1024; // 10MB

export interface ExecOptions extends ExecSyncOptions {
  maxBuffer?: number;
}

export function execSyncSafe(command: string, options: ExecOptions = {}) {
  return execSync(command, {
    encoding: 'utf8',
    stdio: 'pipe',
    maxBuffer: STANDARD_BUFFER_SIZE,
    ...options
  });
}

export async function execAsyncSafe(command: string, options: ExecOptions = {}) {
  return execAsync(command, {
    encoding: 'utf8',
    maxBuffer: STANDARD_BUFFER_SIZE,
    ...options
  });
}
```

### 2. MCP Message Bus Adapter

All agents using MCP message bus must follow this pattern:

```typescript
import { MessageType } from '../messageBus/core/types';

export class MCPMessageBusAdapter {
  async publish(channel: string, message: any): Promise<void> {
    // Always wrap in MessageInput format
    const messageInput = {
      type: MessageType.EVENT,
      payload: message,
      metadata: {
        source: 'your-agent-name',
        timestamp: new Date(),
        version: '1.0'
      }
    };
    
    await this.messageBus.publish(channel, messageInput);
  }
}
```

### 3. MCP Integration Script Template

```typescript
#!/usr/bin/env tsx
import { execSyncSafe } from '../utils/exec';
import logger from '../config/logger';

async function checkMCPStatus(): Promise<boolean> {
  try {
    const result = execSyncSafe('ps aux | grep "mcp.*server" | grep -v grep');
    return result.includes('mcp-code-analyzer');
  } catch {
    return false;
  }
}

async function runMCPAnalysis(): Promise<void> {
  try {
    const result = execSyncSafe('npm run mcp:analyze', {
      cwd: process.cwd()
    });
    logger.info('MCP analysis completed', { result });
  } catch (error) {
    logger.error('MCP analysis failed', error);
    throw error;
  }
}
```

## Testing MCP Integration

Every agent project must include an MCP test script:

```bash
#!/bin/bash
# scripts/test-mcp-integration.sh

echo "Testing MCP Integration..."
echo "=========================="

# Test 1: Check MCP status
echo -e "\n1. Checking MCP server status..."
ps aux | grep "mcp.*server" | grep -v grep || echo "MCP server not running"

# Test 2: Run MCP check
echo -e "\n2. Running MCP check..."
npm run mcp:check

# Test 3: Run MCP analysis
echo -e "\n3. Running MCP analysis..."
npm run mcp:analyze-now

echo -e "\nMCP integration tests completed."
```

## Mandatory npm Scripts

All agent package.json files must include:

```json
{
  "scripts": {
    "mcp:check": "tsx scripts/mcp-integration.ts",
    "mcp:analyze": "tsx scripts/mcp-analyze.ts",
    "mcp:analyze-now": "MCP_FORCE_ANALYSIS=true tsx scripts/mcp-analyze.ts",
    "mcp:test": "./scripts/test-mcp-integration.sh"
  }
}
```

## Error Handling Requirements

1. **Always catch and log MCP errors** - Never let MCP failures crash the agent
2. **Provide meaningful error messages** - Include context about what was being analyzed
3. **Implement retry logic** - For transient failures, retry with exponential backoff
4. **Report critical failures** - Notify via AgentHub when MCP is completely broken

## Monitoring and Health Checks

All agents must implement MCP health checks:

```typescript
export interface MCPHealthCheck {
  isRunning: boolean;
  lastAnalysis: Date | null;
  failureCount: number;
  bufferOverflows: number;
  payloadErrors: number;
}

export async function checkMCPHealth(): Promise<MCPHealthCheck> {
  // Implementation details...
}
```

## Compliance Checklist

- [ ] All execSync calls use 10MB+ buffer size
- [ ] Message bus publishes use MessageInput format
- [ ] MCP test script exists and passes
- [ ] Error handling implemented for all MCP calls
- [ ] Health check endpoint available
- [ ] npm scripts configured correctly
- [ ] Retry logic implemented for failures
- [ ] AgentHub notifications for critical errors

## Remember

**FAILURE TO IMPLEMENT PROPER MCP INTEGRATION = PROJECT FAILURE**

MCP is not optional. It is a core requirement for maintaining code quality across all agent projects. Any agent that cannot integrate with MCP properly is considered non-functional.

---
*This standard is mandatory and supersedes any previous MCP integration guidance.*