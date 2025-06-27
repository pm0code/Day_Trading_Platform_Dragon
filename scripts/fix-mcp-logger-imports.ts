#!/usr/bin/env node
import fs from 'fs';
import { glob } from 'glob';

async function fixMCPLoggerImports() {
  // Find all TypeScript files
  const files = await glob('src/**/*.ts');
  
  for (const file of files) {
    let content = fs.readFileSync(file, 'utf-8');
    let modified = false;
    
    // Fix imports of mcp-logger that don't have /index
    content = content.replace(/from\s+['"](\.\.\/)*core\/mcp-logger\.js['"]/g, (match, dots) => {
      modified = true;
      return match.replace('mcp-logger.js', 'mcp-logger/index.js');
    });
    
    if (modified) {
      fs.writeFileSync(file, content);
      console.log(`Fixed MCP logger imports in: ${file}`);
    }
  }
}

fixMCPLoggerImports().catch(console.error);