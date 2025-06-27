#!/usr/bin/env node
/**
 * Migration script to replace ALL logging with MCPLogger
 * This ensures MCPLogger is the ONLY logger used throughout the codebase
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from 'fs';
import { join, relative, dirname } from 'path';

interface MigrationRule {
  pattern: RegExp;
  replacement: string | ((match: string, ...args: any[]) => string);
  description: string;
}

const MIGRATION_RULES: MigrationRule[] = [
  // Replace winston logger imports
  {
    pattern: /import\s*{\s*logger\s*}\s*from\s*['"]\.\.?\/.*logger(\.js)?['"];?/g,
    replacement: (match: string) => {
      // Extract the current file's import path depth
      const depth = (match.match(/\.\./g) || []).length;
      const pathToCore = depth > 0 ? '../'.repeat(depth) + 'core/mcp-logger' : './core/mcp-logger';
      return `import { logger } from '${pathToCore}';`;
    },
    description: 'Replace winston logger imports with MCPLogger'
  },
  
  // Replace createLogger imports
  {
    pattern: /import\s*{\s*createLogger\s*}\s*from\s*['"]\.\.?\/.*logger(\.js)?['"];?/g,
    replacement: (match: string) => {
      const depth = (match.match(/\.\./g) || []).length;
      const pathToCore = depth > 0 ? '../'.repeat(depth) + 'core/mcp-logger' : './core/mcp-logger';
      return `import { createLogger } from '${pathToCore}';`;
    },
    description: 'Replace createLogger imports'
  },
  
  // Replace comprehensiveLogger usage
  {
    pattern: /import\s*{\s*MCPLogger\s*}\s*from\s*['"]\.\.?\/.*comprehensiveLogger(\.js)?['"];?/g,
    replacement: (match: string) => {
      const depth = (match.match(/\.\./g) || []).length;
      const pathToCore = depth > 0 ? '../'.repeat(depth) + 'core/mcp-logger' : './core/mcp-logger';
      return `import { MCPLogger, createLogger } from '${pathToCore}';`;
    },
    description: 'Replace comprehensiveLogger imports'
  },
  
  // Replace any winston.createLogger calls
  {
    pattern: /winston\.createLogger\(/g,
    replacement: 'createLogger(',
    description: 'Replace winston.createLogger calls'
  },
  
  // Add proper context to createLogger calls
  {
    pattern: /createLogger\(\s*\)/g,
    replacement: (match: string, offset: number, fullString: string) => {
      // Try to extract component name from file path or class name
      const fileName = fullString.match(/export\s+class\s+(\w+)/)?.[1] || 
                      fullString.match(/export\s+function\s+(\w+)/)?.[1] ||
                      'Component';
      return `createLogger('${fileName}')`;
    },
    description: 'Add component context to createLogger'
  },
  
  // Replace console.log with logger
  {
    pattern: /console\.(log|info)\(/g,
    replacement: 'logger.info(',
    description: 'Replace console.log/info with logger.info'
  },
  
  // Replace console.error with logger
  {
    pattern: /console\.error\(/g,
    replacement: 'logger.error(',
    description: 'Replace console.error with logger.error'
  },
  
  // Replace console.warn with logger
  {
    pattern: /console\.warn\(/g,
    replacement: 'logger.warn(',
    description: 'Replace console.warn with logger.warn'
  },
  
  // Replace console.debug with logger
  {
    pattern: /console\.debug\(/g,
    replacement: 'logger.debug(',
    description: 'Replace console.debug with logger.debug'
  }
];

function getRelativePathToCore(fromPath: string): string {
  const srcIndex = fromPath.indexOf('/src/');
  if (srcIndex === -1) return './core/mcp-logger';
  
  const relativePath = fromPath.substring(srcIndex + 5);
  const depth = relativePath.split('/').length - 1;
  
  if (depth === 0) return './core/mcp-logger';
  return '../'.repeat(depth) + 'core/mcp-logger';
}

function migrateFile(filePath: string): boolean {
  // Skip certain files
  if (filePath.includes('node_modules') || 
      filePath.includes('dist') ||
      filePath.includes('mcp-logger') ||
      filePath.includes('.test.') ||
      filePath.includes('.spec.')) {
    return false;
  }
  
  // Only process TypeScript files
  if (!filePath.endsWith('.ts') && !filePath.endsWith('.js')) {
    return false;
  }
  
  let content = readFileSync(filePath, 'utf-8');
  const originalContent = content;
  let modified = false;
  
  // Apply migration rules
  for (const rule of MIGRATION_RULES) {
    const matches = content.match(rule.pattern);
    if (matches) {
      console.log(`  Applying: ${rule.description} (${matches.length} matches)`);
      content = content.replace(rule.pattern, rule.replacement as any);
      modified = true;
    }
  }
  
  // Add logger import if file uses logging but doesn't import it
  if (modified && !content.includes("from '../core/mcp-logger'") && !content.includes("from './core/mcp-logger'")) {
    const pathToCore = getRelativePathToCore(filePath);
    const importStatement = `import { logger } from '${pathToCore}';\n`;
    
    // Add after other imports
    const lastImportMatch = content.match(/import[^;]+;(?=\n)/g);
    if (lastImportMatch) {
      const lastImport = lastImportMatch[lastImportMatch.length - 1];
      const lastImportIndex = content.lastIndexOf(lastImport);
      content = content.slice(0, lastImportIndex + lastImport.length) + 
                '\n' + importStatement + 
                content.slice(lastImportIndex + lastImport.length);
    } else {
      // Add at the beginning if no imports found
      content = importStatement + '\n' + content;
    }
  }
  
  // Write back if modified
  if (content !== originalContent) {
    writeFileSync(filePath, content);
    console.log(`✅ Migrated: ${relative(process.cwd(), filePath)}`);
    return true;
  }
  
  return false;
}

function walkDirectory(dir: string, callback: (filePath: string) => void) {
  const files = readdirSync(dir);
  
  for (const file of files) {
    const fullPath = join(dir, file);
    const stat = statSync(fullPath);
    
    if (stat.isDirectory()) {
      // Skip certain directories
      if (!['node_modules', 'dist', '.git', 'logs'].includes(file)) {
        walkDirectory(fullPath, callback);
      }
    } else {
      callback(fullPath);
    }
  }
}

function main() {
  console.log('🚀 Starting migration to MCPLogger...\n');
  
  const srcDir = join(process.cwd(), 'src');
  let migrated = 0;
  let skipped = 0;
  let errors = 0;
  
  walkDirectory(srcDir, (filePath) => {
    try {
      if (migrateFile(filePath)) {
        migrated++;
      } else {
        skipped++;
      }
    } catch (error) {
      console.error(`❌ Error migrating ${filePath}:`, error);
      errors++;
    }
  });
  
  console.log('\n📊 Migration Summary:');
  console.log(`  ✅ Migrated: ${migrated} files`);
  console.log(`  ⏭️  Skipped: ${skipped} files`);
  console.log(`  ❌ Errors: ${errors} files`);
  
  if (errors > 0) {
    console.log('\n⚠️  Some files had errors. Please review them manually.');
  }
  
  console.log('\n✅ Migration complete!');
  console.log('\n📝 Next steps:');
  console.log('  1. Run "npm install" to ensure all dependencies are installed');
  console.log('  2. Run "npm run build" to check for compilation errors');
  console.log('  3. Run tests to ensure everything works correctly');
  console.log('  4. Review the changes to ensure correctness');
}

// Run the migration
main();