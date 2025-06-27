#!/usr/bin/env node
import fs from 'fs';
import path from 'path';
import { glob } from 'glob';

async function fixImports() {
  // Find all TypeScript files
  const files = await glob('src/**/*.ts');
  
  for (const file of files) {
    let content = fs.readFileSync(file, 'utf-8');
    let modified = false;
    
    // Fix relative imports that are missing .js extension
    const importRegex = /from\s+['"](\.\.?\/[^'"]+)(?<!\.js)['"]/g;
    content = content.replace(importRegex, (match, importPath) => {
      // Skip if it already has an extension
      if (importPath.endsWith('.js') || importPath.endsWith('.json')) {
        return match;
      }
      
      modified = true;
      return match.replace(importPath, `${importPath}.js`);
    });
    
    if (modified) {
      fs.writeFileSync(file, content);
      console.log(`Fixed imports in: ${file}`);
    }
  }
}

fixImports().catch(console.error);