#!/usr/bin/env node

const { execSync } = require('child_process');
const path = require('path');

const MCP_ANALYZER_DIR = '/home/nader/my_projects/C#/mcp-code-analyzer';
const TRADING_PROJECT_DIR = '/home/nader/my_projects/C#/DayTradingPlatform';

console.log('🚀 MCP Code Analyzer - Day Trading Platform Analysis');
console.log('==================================================');
console.log('');

try {
    // Change to MCP analyzer directory and run the analysis
    const command = `cd ${MCP_ANALYZER_DIR} && npm run build && npx tsx scripts/analyze-file.ts ${TRADING_PROJECT_DIR}/DayTradinPlatform/TradingPlatform.Core/Models/Security.cs`;
    
    console.log('Analyzing a sample file first to test the setup...\n');
    
    const output = execSync(command, { 
        encoding: 'utf8',
        stdio: 'inherit'
    });
    
    console.log('\n✅ MCP Code Analyzer is working! You can now run full analysis.');
    console.log('To analyze the entire project, run:');
    console.log(`cd ${MCP_ANALYZER_DIR} && ./scripts/analyze-trading.sh ${TRADING_PROJECT_DIR}`);
    
} catch (error) {
    console.error('❌ Error running MCP Code Analyzer:', error.message);
    console.log('\nPlease ensure:');
    console.log('1. MCP Code Analyzer is installed at:', MCP_ANALYZER_DIR);
    console.log('2. Dependencies are installed (npm install)');
    console.log('3. The project is built (npm run build)');
    process.exit(1);
}