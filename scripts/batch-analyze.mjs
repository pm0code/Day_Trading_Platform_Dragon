#!/usr/bin/env node

import { spawn } from 'child_process';
import { resolve, relative } from 'path';
import { readdir, stat, writeFile } from 'fs/promises';
import { join } from 'path';

const ANALYZER_DIR = '/home/nader/my_projects/C#/mcp-code-analyzer';
const PROJECT_DIR = '/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform';
const OUTPUT_FILE = '/home/nader/my_projects/C#/DayTradingPlatform/mcp-analysis-results.json';

// Recursively find all C# files
async function findCsFiles(dir, files = []) {
  const entries = await readdir(dir, { withFileTypes: true });
  
  for (const entry of entries) {
    const fullPath = join(dir, entry.name);
    
    if (entry.isDirectory() && !entry.name.includes('bin') && !entry.name.includes('obj')) {
      await findCsFiles(fullPath, files);
    } else if (entry.isFile() && entry.name.endsWith('.cs')) {
      files.push(fullPath);
    }
  }
  
  return files;
}

// Analyze a single file
function analyzeFile(filePath) {
  return new Promise((resolve, reject) => {
    let output = '';
    const relativePath = relative(PROJECT_DIR, filePath);
    
    const analyze = spawn('npx', ['tsx', 'scripts/analyze-file.ts', filePath], {
      cwd: ANALYZER_DIR,
      shell: true
    });
    
    analyze.stdout.on('data', (data) => {
      output += data.toString();
    });
    
    analyze.stderr.on('data', (data) => {
      output += data.toString();
    });
    
    analyze.on('close', (code) => {
      // Parse output for issues
      const lines = output.split('\n');
      const issues = [];
      let summary = { errors: 0, warnings: 0, info: 0 };
      
      for (const line of lines) {
        if (line.includes('Line ')) {
          const match = line.match(/([✗⚠ℹ])\s+Line\s+(\d+):\s+(.+?)(?:\s+\[(.+?)\])?$/);
          if (match) {
            const [, icon, lineNum, message, rule] = match;
            issues.push({
              severity: icon === '✗' ? 'error' : icon === '⚠' ? 'warning' : 'info',
              line: parseInt(lineNum),
              message: message.trim(),
              rule: rule || 'unknown'
            });
          }
        } else if (line.includes('Summary:')) {
          const summaryMatch = line.match(/(\d+)\s+errors?,\s+(\d+)\s+warnings?,\s+(\d+)\s+info/);
          if (summaryMatch) {
            summary = {
              errors: parseInt(summaryMatch[1]),
              warnings: parseInt(summaryMatch[2]),
              info: parseInt(summaryMatch[3])
            };
          }
        }
      }
      
      resolve({
        file: relativePath,
        issues,
        summary
      });
    });
  });
}

// Main analysis function
async function analyzeProject() {
  console.log('🚀 MCP Code Analyzer - Batch Analysis');
  console.log('=====================================\n');
  
  console.log('Finding C# files...');
  const files = await findCsFiles(PROJECT_DIR);
  console.log(`Found ${files.length} C# files\n`);
  
  const results = [];
  const totalSummary = {
    totalFiles: files.length,
    filesWithIssues: 0,
    totalErrors: 0,
    totalWarnings: 0,
    totalInfo: 0,
    criticalFiles: [],
    financialIssues: [],
    performanceIssues: [],
    securityIssues: []
  };
  
  // Analyze files in batches
  const BATCH_SIZE = 5;
  for (let i = 0; i < files.length; i += BATCH_SIZE) {
    const batch = files.slice(i, Math.min(i + BATCH_SIZE, files.length));
    console.log(`Analyzing batch ${Math.floor(i/BATCH_SIZE) + 1}/${Math.ceil(files.length/BATCH_SIZE)}...`);
    
    const batchResults = await Promise.all(batch.map(analyzeFile));
    
    for (const result of batchResults) {
      results.push(result);
      
      if (result.issues.length > 0) {
        totalSummary.filesWithIssues++;
        totalSummary.totalErrors += result.summary.errors;
        totalSummary.totalWarnings += result.summary.warnings;
        totalSummary.totalInfo += result.summary.info;
        
        // Categorize critical files
        if (result.summary.errors > 0) {
          totalSummary.criticalFiles.push({
            file: result.file,
            errorCount: result.summary.errors
          });
        }
        
        // Categorize by issue type
        for (const issue of result.issues) {
          if (issue.message.includes('decimal') || issue.message.includes('financial')) {
            totalSummary.financialIssues.push({ file: result.file, issue });
          }
          if (issue.message.includes('performance') || issue.message.includes('LINQ')) {
            totalSummary.performanceIssues.push({ file: result.file, issue });
          }
          if (issue.message.includes('security') || issue.rule.includes('security')) {
            totalSummary.securityIssues.push({ file: result.file, issue });
          }
        }
      }
    }
  }
  
  // Save results
  const output = {
    timestamp: new Date().toISOString(),
    summary: totalSummary,
    details: results
  };
  
  await writeFile(OUTPUT_FILE, JSON.stringify(output, null, 2));
  
  // Print summary
  console.log('\n' + '='.repeat(60));
  console.log('📊 ANALYSIS SUMMARY\n');
  console.log(`Total Files Analyzed: ${totalSummary.totalFiles}`);
  console.log(`Files with Issues: ${totalSummary.filesWithIssues}`);
  console.log(`Total Errors: ${totalSummary.totalErrors}`);
  console.log(`Total Warnings: ${totalSummary.totalWarnings}`);
  console.log(`Total Info: ${totalSummary.totalInfo}`);
  console.log('');
  console.log(`🚨 Critical Files: ${totalSummary.criticalFiles.length}`);
  console.log(`💰 Financial Issues: ${totalSummary.financialIssues.length}`);
  console.log(`⚡ Performance Issues: ${totalSummary.performanceIssues.length}`);
  console.log(`🔒 Security Issues: ${totalSummary.securityIssues.length}`);
  console.log('');
  console.log(`Full results saved to: ${OUTPUT_FILE}`);
}

// Run the analysis
analyzeProject().catch(console.error);