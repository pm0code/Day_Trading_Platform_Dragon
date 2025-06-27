#!/usr/bin/env node

import { spawn } from 'child_process';
import { resolve } from 'path';

const ANALYZER_DIR = '/home/nader/my_projects/C#/mcp-code-analyzer';
const PROJECT_DIR = '/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform';

console.log('🚀 MCP Code Analyzer - Direct Execution');
console.log('======================================');
console.log('');

// Run the analyzer using npx tsx with proper working directory
const analyze = spawn('npx', ['tsx', 'scripts/analyze-file.ts', `${PROJECT_DIR}/TradingPlatform.Core/Mathematics/FinancialMath.cs`], {
  cwd: ANALYZER_DIR,
  stdio: 'inherit',
  shell: true
});

analyze.on('close', (code) => {
  if (code === 0) {
    console.log('\n✅ Analysis completed successfully!');
    console.log('\nTo analyze more files or the entire project, you can:');
    console.log(`1. cd ${ANALYZER_DIR}`);
    console.log(`2. npx tsx scripts/analyze-file.ts <file-path>`);
    console.log(`3. Or use: find ${PROJECT_DIR} -name "*.cs" -exec npx tsx scripts/analyze-file.ts {} \\;`);
  } else {
    console.error(`\n❌ Analysis failed with code ${code}`);
  }
});