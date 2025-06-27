#!/bin/bash

# Quick script to analyze Day Trading project
ANALYZER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TRADING_PROJECT="${1:-/path/to/day-trading-project}"

echo "🚀 MCP Code Analyzer - Day Trading Platform Analysis"
echo "=================================================="
echo ""

# Check if project path provided
if [ "$1" == "" ]; then
    echo "Usage: $0 /path/to/day-trading-project"
    echo ""
    echo "This will analyze your C# trading project for:"
    echo "  ✓ Financial precision (decimal vs float/double)"
    echo "  ✓ HFT performance issues (LINQ in hot paths)"
    echo "  ✓ Order validation requirements"
    echo "  ✓ Risk management checks"
    echo "  ✓ Async/blocking operations"
    echo ""
    exit 1
fi

# Check if project exists
if [ ! -d "$TRADING_PROJECT" ]; then
    echo "❌ Project directory not found: $TRADING_PROJECT"
    exit 1
fi

cd "$ANALYZER_DIR"

# Build if needed
if [ ! -d "dist" ]; then
    echo "Building analyzer..."
    npm run build
fi

echo "Analyzing: $TRADING_PROJECT"
echo ""

# Run comprehensive analysis
npx tsx - << 'EOF'
import { OptimizedAnalysisEngine } from './src/analyzers/OptimizedAnalysisEngine.js';
import { loadConfig } from './src/config/ConfigLoader.js';
import * as fs from 'fs/promises';
import * as path from 'path';
import { glob } from 'glob';

const projectPath = process.argv[2] || process.env.TRADING_PROJECT;

async function analyzeProject() {
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
}

analyzeProject().catch(console.error);
EOF