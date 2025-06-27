#!/usr/bin/env node
/**
 * Migration script to replace all winston logger usage with MCPLogger
 * This ensures MCPLogger is the ONLY logger used throughout the codebase
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from 'fs';
import { join, extname } from 'path';

const MIGRATIONS = [
  {
    // Replace winston logger imports
    pattern: /import\s*{\s*logger\s*}\s*from\s*['"]\.\.?\/.*logger\.js['"];?/g,
    replacement: "import { CanonicalLogger } from '$RELATIVE_PATH/utils/canonicalLogger.js';"
  },
  {
    // Replace createLogger imports
    pattern: /import\s*{\s*createLogger\s*}\s*from\s*['"]\.\.?\/.*logger\.js['"];?/g,
    replacement: "import { createCanonicalLogger } from '$RELATIVE_PATH/utils/canonicalLogger.js';"
  },
  {
    // Replace logger.info calls
    pattern: /logger\.info\(/g,
    replacement: "CanonicalLogger.info("
  },
  {
    // Replace logger.warn calls
    pattern: /logger\.warn\(/g,
    replacement: "CanonicalLogger.warn("
  },
  {
    // Replace logger.error calls
    pattern: /logger\.error\(/g,
    replacement: "CanonicalLogger.error("
  },
  {
    // Replace logger.debug calls
    pattern: /logger\.debug\(/g,
    replacement: "CanonicalLogger.debug("
  },
  {
    // Replace createLogger calls
    pattern: /createLogger\(/g,
    replacement: "createCanonicalLogger({ component: "
  },
  {
    // Add closing brace for createCanonicalLogger
    pattern: /createCanonicalLogger\({ component: ([^)]+)\)/g,
    replacement: "createCanonicalLogger({ component: $1 })"
  }
];

function getRelativePath(from: string, to: string): string {
  const fromParts = from.split('/').filter(Boolean);
  const toParts = to.split('/').filter(Boolean);
  
  // Remove common parts
  let commonIndex = 0;
  while (commonIndex < fromParts.length && 
         commonIndex < toParts.length && 
         fromParts[commonIndex] === toParts[commonIndex]) {
    commonIndex++;
  }
  
  // Build relative path
  const upCount = fromParts.length - commonIndex - 1;
  const relativeParts = [];
  
  for (let i = 0; i < upCount; i++) {
    relativeParts.push('..');
  }
  
  if (relativeParts.length === 0) {
    relativeParts.push('.');
  }
  
  return relativeParts.join('/');
}

function migrateFile(filePath: string, baseDir: string): boolean {
  if (!filePath.endsWith('.ts') && !filePath.endsWith('.js')) {
    return false;
  }
  
  // Skip canonicalLogger.ts itself
  if (filePath.includes('canonicalLogger')) {
    return false;
  }
  
  let content = readFileSync(filePath, 'utf-8');
  const originalContent = content;
  
  // Calculate relative path from this file to utils directory
  const relativePath = getRelativePath(
    filePath.replace(baseDir, ''),
    'src/utils'
  );
  
  // Apply migrations
  for (const migration of MIGRATIONS) {
    if (migration.replacement.includes('$RELATIVE_PATH')) {
      const replacement = migration.replacement.replace('$RELATIVE_PATH', relativePath);
      content = content.replace(migration.pattern, replacement);
    } else {
      content = content.replace(migration.pattern, migration.replacement);
    }
  }
  
  // Check if file was modified
  if (content !== originalContent) {
    writeFileSync(filePath, content);
    console.log(`✅ Migrated: ${filePath}`);
    return true;
  }
  
  return false;
}

function walkDirectory(dir: string, baseDir: string, stats: { migrated: number, skipped: number }) {
  const files = readdirSync(dir);
  
  for (const file of files) {
    const fullPath = join(dir, file);
    const stat = statSync(fullPath);
    
    if (stat.isDirectory()) {
      // Skip node_modules and dist directories
      if (!file.includes('node_modules') && !file.includes('dist')) {
        walkDirectory(fullPath, baseDir, stats);
      }
    } else {
      if (migrateFile(fullPath, baseDir)) {
        stats.migrated++;
      } else {
        stats.skipped++;
      }
    }
  }
}

// Run migration
console.log('🚀 Starting migration to canonical MCPLogger...\n');

const baseDir = join(process.cwd(), 'src');
const stats = { migrated: 0, skipped: 0 };

walkDirectory(baseDir, baseDir, stats);

console.log('\n📊 Migration Summary:');
console.log(`Files migrated: ${stats.migrated}`);
console.log(`Files skipped: ${stats.skipped}`);
console.log('\n✅ Migration complete!');
console.log('\n⚠️  Remember to:');
console.log('1. Run npm run build to check for any compilation errors');
console.log('2. Run tests to ensure everything still works');
console.log('3. Review the changes to ensure correctness');