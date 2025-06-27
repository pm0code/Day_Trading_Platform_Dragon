#!/usr/bin/env node

import { OptimizedAnalysisEngine } from '../src/analyzers/OptimizedAnalysisEngine.js';
import { loadConfig } from '../src/config/ConfigLoader.js';
import { createLogger } from '../src/core/mcp-logger/index.js';
import * as fs from 'fs/promises';
import * as path from 'path';

const logger = createLogger('analyze-file');

async function main() {
  const filePath = process.argv[2];
  
  if (!filePath) {
    console.error('Usage: npx tsx scripts/analyze-file.ts <file-path>');
    process.exit(1);
  }

  try {
    // Check file exists
    await fs.access(filePath);
    
    // Read file content
    const content = await fs.readFile(filePath, 'utf-8');
    const ext = path.extname(filePath).slice(1);
    
    // Initialize engine
    const engine = new OptimizedAnalysisEngine();
    const config = await loadConfig();
    await engine.initialize(config);
    
    console.log(`\n🔍 Analyzing ${filePath}...\n`);
    
    // Analyze
    const startTime = performance.now();
    const result = await engine.analyze({
      code: content,
      language: ext,
      path: filePath
    });
    const duration = performance.now() - startTime;
    
    // Display results
    if (result.issues.length === 0) {
      console.log('✅ No issues found!');
    } else {
      const errors = result.issues.filter(i => i.severity === 'error');
      const warnings = result.issues.filter(i => i.severity === 'warning');
      const info = result.issues.filter(i => i.severity === 'info');
      
      result.issues.forEach(issue => {
        const icon = issue.severity === 'error' ? '✗' : 
                    issue.severity === 'warning' ? '⚠' : 'ℹ';
        console.log(`${icon} Line ${issue.line}: ${issue.message} [${issue.rule}]`);
      });
      
      console.log(`\nSummary: ${errors.length} errors, ${warnings.length} warnings, ${info.length} info`);
    }
    
    // Financial issues for trading projects
    const financialIssues = result.issues.filter(i => 
      i.rule?.includes('financial') || 
      i.rule?.includes('decimal') ||
      i.message.includes('decimal')
    );
    
    if (financialIssues.length > 0) {
      console.log(`\n💰 Financial Issues: ${financialIssues.length} found`);
      financialIssues.forEach(issue => {
        console.log(`  - Line ${issue.line}: ${issue.message}`);
      });
    }
    
    // Performance issues
    const perfIssues = result.issues.filter(i => 
      i.rule?.includes('performance') || 
      i.rule?.includes('latency') ||
      i.message.includes('hot path') ||
      i.message.includes('LINQ')
    );
    
    if (perfIssues.length > 0) {
      console.log(`\n⚡ Performance Issues: ${perfIssues.length} found`);
      perfIssues.forEach(issue => {
        console.log(`  - Line ${issue.line}: ${issue.message}`);
      });
    }
    
    console.log(`\n⏱️  Analysis completed in ${duration.toFixed(2)}ms`);
    
    // Exit with error code if errors found
    if (result.issues.some(i => i.severity === 'error')) {
      process.exit(1);
    }
    
  } catch (error) {
    console.error('Error:', error);
    process.exit(1);
  }
}

main();