#!/bin/bash

# Comprehensive wrapper script for MCP Code Analyzer
MCP_ANALYZER_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
TRADING_PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform"

echo "🚀 MCP Code Analyzer - Day Trading Platform Analysis"
echo "=================================================="
echo ""

# Check if MCP analyzer exists
if [ ! -d "$MCP_ANALYZER_DIR" ]; then
    echo "❌ MCP Code Analyzer not found at: $MCP_ANALYZER_DIR"
    exit 1
fi

# Run the analyzer using Node.js directly with proper module configuration
cd "$MCP_ANALYZER_DIR" && NODE_OPTIONS="--experimental-modules --experimental-specifier-resolution=node" npx tsx --tsconfig ./tsconfig.json - << 'EOF'
import { OptimizedAnalysisEngine } from './src/analyzers/OptimizedAnalysisEngine.js';
import { loadConfig } from './src/config/ConfigLoader.js';
import * as fs from 'fs/promises';
import * as path from 'path';
import { glob } from 'glob';

const projectPath = '/home/nader/my_projects/C#/DayTradingPlatform';

async function analyzeProject() {
  try {
    const engine = new OptimizedAnalysisEngine();
    await engine.initialize(await loadConfig());
    
    // Find all C# files
    const files = await glob('**/*.cs', {
      cwd: projectPath,
      ignore: ['**/bin/**', '**/obj/**', '**/node_modules/**']
    });
    
    console.log(`Found ${files.length} C# files to analyze\n`);
    
    const summary = {
      totalFiles: files.length,
      filesWithIssues: 0,
      totalIssues: 0,
      criticalIssues: 0,
      financialIssues: 0,
      performanceIssues: 0,
      securityIssues: 0
    };
    
    for (const file of files) {
      const filePath = path.join(projectPath, file);
      const content = await fs.readFile(filePath, 'utf-8');
      
      const result = await engine.analyze({
        code: content,
        language: 'csharp',
        path: filePath
      });
      
      if (result.issues.length > 0) {
        summary.filesWithIssues++;
        summary.totalIssues += result.issues.length;
        
        console.log(`\n📄 ${file}:`);
        
        result.issues.forEach(issue => {
          const icon = issue.severity === 'error' ? '✗' : 
                      issue.severity === 'warning' ? '⚠' : 'ℹ';
          console.log(`  ${icon} Line ${issue.line}: ${issue.message}`);
          
          // Categorize issues
          if (issue.severity === 'error') summary.criticalIssues++;
          if (issue.rule?.includes('financial') || issue.message.includes('decimal')) {
            summary.financialIssues++;
          }
          if (issue.message.includes('performance') || issue.message.includes('LINQ')) {
            summary.performanceIssues++;
          }
          if (issue.rule?.includes('security')) {
            summary.securityIssues++;
          }
        });
      }
    }
    
    // Print summary
    console.log('\n' + '='.repeat(60));
    console.log('📊 ANALYSIS SUMMARY\n');
    console.log(`Total Files Analyzed: ${summary.totalFiles}`);
    console.log(`Files with Issues: ${summary.filesWithIssues}`);
    console.log(`Total Issues Found: ${summary.totalIssues}`);
    console.log('');
    console.log(`🚨 Critical Issues: ${summary.criticalIssues}`);
    console.log(`💰 Financial Issues: ${summary.financialIssues}`);
    console.log(`⚡ Performance Issues: ${summary.performanceIssues}`);
    console.log(`🔒 Security Issues: ${summary.securityIssues}`);
    
    if (summary.criticalIssues > 0) {
      console.log('\n❌ Critical issues found! Fix these before deployment.');
      process.exit(1);
    } else if (summary.totalIssues > 0) {
      console.log('\n⚠️  Non-critical issues found. Review and fix as needed.');
    } else {
      console.log('\n✅ No issues found! Your code meets all standards.');
    }
  } catch (error) {
    console.error('Error during analysis:', error);
    process.exit(1);
  }
}

analyzeProject();
EOF