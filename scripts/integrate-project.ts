#!/usr/bin/env node

import * as fs from 'fs/promises';
import * as path from 'path';
import { execSync } from 'child_process';
import { createInterface } from 'readline';
import { logger } from '../src/core/mcp-logger/index.js';

const rl = createInterface({
  input: process.stdin,
  output: process.stdout
});

const question = (query: string): Promise<string> => 
  new Promise(resolve => rl.question(query, resolve));

// Available tool sets for different project types
const TOOL_PROFILES = {
  trading: {
    name: 'Day Trading Platform',
    tools: [
      'analyze',
      'validateFinancialLogic',
      'validateCSharpFinancials',
      'analyzeLatency',
      'checkSecurity',
      'analyzeScalability',
      'validateArchitectureChecklist'
    ],
    criticalRules: [
      'decimal-for-money',
      'no-async-blocking',
      'order-validation-required',
      'enforce-risk-limits'
    ]
  },
  webapp: {
    name: 'Web Application',
    tools: [
      'analyze',
      'checkSecurity',
      'analyzeTestCoverage',
      'detectDuplication',
      'architecture',
      'analyzeScalability'
    ],
    criticalRules: [
      'no-sql-injection',
      'no-xss',
      'secure-headers',
      'input-validation'
    ]
  },
  api: {
    name: 'REST API',
    tools: [
      'analyze',
      'checkSecurity',
      'analyzeLatency',
      'analyzeScalability',
      'validateArchitectureChecklist',
      'analyzeObservability'
    ],
    criticalRules: [
      'api-versioning',
      'rate-limiting',
      'authentication-required',
      'input-validation'
    ]
  },
  microservice: {
    name: 'Microservice',
    tools: [
      'analyze',
      'architecture',
      'analyzeObservability',
      'analyzeLogging',
      'analyzeScalability',
      'checkSecurity'
    ],
    criticalRules: [
      'circuit-breaker',
      'retry-policy',
      'distributed-tracing',
      'health-checks'
    ]
  },
  library: {
    name: 'Library/Package',
    tools: [
      'analyze',
      'detectDuplication',
      'calculateMetrics',
      'analyzeTestCoverage',
      'architecture'
    ],
    criticalRules: [
      'public-api-docs',
      'semantic-versioning',
      'no-breaking-changes',
      'test-coverage-80'
    ]
  },
  custom: {
    name: 'Custom Configuration',
    tools: [],
    criticalRules: []
  }
};

// All available tools
const ALL_TOOLS = [
  'analyze',
  'findPatterns',
  'checkSecurity',
  'suggest',
  'architecture',
  'calculateMetrics',
  'detectDuplication',
  'analyzeTestCoverage',
  'identifyDeadCode',
  'validateFinancialLogic',
  'analyzeLatency',
  'analyzeCSharpAsync',
  'analyzeWindowsPackaging',
  'validateCSharpFinancials',
  'analyzeLogging',
  'analyzeAIInterfaces',
  'analyzeResourceEfficiency',
  'analyzeScalability',
  'analyzeObservability',
  'validateArchitectureChecklist',
  'validateCanonicalPatterns',
  'explainCode',
  'generateTests',
  'subscribeToAnalysis'
];

async function main() {
  console.log('🚀 MCP Code Analyzer - Project Integration Setup\n');
  
  try {
    // Get project path
    const projectPath = await question('Enter project path (or press Enter for current directory): ');
    const targetPath = projectPath || process.cwd();
    
    // Verify path exists
    try {
      await fs.access(targetPath);
    } catch {
      console.error(`❌ Path does not exist: ${targetPath}`);
      process.exit(1);
    }
    
    // Get project name
    const projectName = await question('Enter project name: ');
    
    // Select profile
    console.log('\nSelect project type:');
    const profileKeys = Object.keys(TOOL_PROFILES);
    profileKeys.forEach((key, index) => {
      console.log(`  ${index + 1}. ${TOOL_PROFILES[key as keyof typeof TOOL_PROFILES].name}`);
    });
    
    const profileChoice = await question('\nEnter choice (1-6): ');
    const profileKey = profileKeys[parseInt(profileChoice) - 1] as keyof typeof TOOL_PROFILES;
    const profile = TOOL_PROFILES[profileKey];
    
    let selectedTools = [...profile.tools];
    let criticalRules = [...profile.criticalRules];
    
    // Custom tool selection
    if (profileKey === 'custom' || await question('\nCustomize tool selection? (y/N): ') === 'y') {
      console.log('\nAvailable tools:');
      ALL_TOOLS.forEach((tool, index) => {
        console.log(`  ${index + 1}. ${tool}`);
      });
      
      const toolIndices = await question('\nEnter tool numbers separated by commas (e.g., 1,3,5): ');
      selectedTools = toolIndices.split(',').map(i => ALL_TOOLS[parseInt(i.trim()) - 1]).filter(Boolean);
    }
    
    // Get language
    const language = await question('\nPrimary language (typescript/javascript/csharp/java/python/go): ');
    
    // Create configuration
    const analyzerPath = path.resolve(__dirname, '..');
    const config = {
      name: projectName,
      path: targetPath,
      profile: profileKey,
      language,
      tools: selectedTools,
      criticalRules,
      analyzer: {
        path: analyzerPath,
        autoStart: true
      },
      ignore: [
        '**/node_modules/**',
        '**/bin/**',
        '**/obj/**',
        '**/dist/**',
        '**/.git/**'
      ]
    };
    
    // Create .mcp directory
    const mcpDir = path.join(targetPath, '.mcp');
    await fs.mkdir(mcpDir, { recursive: true });
    
    // Write config
    const configPath = path.join(mcpDir, 'analyzer.config.json');
    await fs.writeFile(configPath, JSON.stringify(config, null, 2));
    console.log(`\n✅ Created config: ${configPath}`);
    
    // Update package.json if it exists
    const packageJsonPath = path.join(targetPath, 'package.json');
    try {
      await fs.access(packageJsonPath);
      const packageJson = JSON.parse(await fs.readFile(packageJsonPath, 'utf-8'));
      
      packageJson.scripts = packageJson.scripts || {};
      packageJson.scripts['analyze'] = `cd ${analyzerPath} && npm run analyze:project -- --config ${configPath}`;
      packageJson.scripts['analyze:watch'] = `cd ${analyzerPath} && npm run start -- --subscribe ${targetPath} --config ${configPath} --watch`;
      packageJson.scripts['analyze:report'] = `cd ${analyzerPath} && npm run report -- --project ${projectName}`;
      
      await fs.writeFile(packageJsonPath, JSON.stringify(packageJson, null, 2));
      console.log('✅ Updated package.json with analysis scripts');
    } catch {
      // Not a Node.js project, create standalone scripts
      const scriptsDir = path.join(targetPath, '.mcp', 'scripts');
      await fs.mkdir(scriptsDir, { recursive: true });
      
      // Create analyze script
      const analyzeScript = `#!/bin/bash
cd "${analyzerPath}"
npm run analyze:project -- --config "${configPath}" "$@"
`;
      await fs.writeFile(path.join(scriptsDir, 'analyze.sh'), analyzeScript, { mode: 0o755 });
      
      // Create watch script
      const watchScript = `#!/bin/bash
cd "${analyzerPath}"
npm run start -- --subscribe "${targetPath}" --config "${configPath}" --watch "$@"
`;
      await fs.writeFile(path.join(scriptsDir, 'watch.sh'), watchScript, { mode: 0o755 });
      
      console.log('✅ Created analysis scripts in .mcp/scripts/');
    }
    
    // Create Git hooks if .git exists
    const gitDir = path.join(targetPath, '.git');
    try {
      await fs.access(gitDir);
      
      if (await question('\nInstall Git hooks? (Y/n): ') !== 'n') {
        // Pre-commit hook
        const preCommitHook = `#!/bin/bash
# MCP Code Analyzer pre-commit hook

echo "🔍 Running code analysis..."

cd "${analyzerPath}"
RESULT=$(npm run analyze:changed -- --project "${targetPath}" --json 2>/dev/null)

if echo "$RESULT" | grep -q '"severity":"error"'; then
  echo "❌ Critical issues found!"
  echo "$RESULT" | jq '.issues[] | select(.severity=="error") | "\\(.file):\\(.line) \\(.message)"'
  exit 1
fi

echo "✅ Code analysis passed"
`;
        await fs.writeFile(path.join(gitDir, 'hooks', 'pre-commit'), preCommitHook, { mode: 0o755 });
        
        // Pre-push hook
        const prePushHook = `#!/bin/bash
# MCP Code Analyzer pre-push hook

echo "🚀 Running full project analysis..."

cd "${analyzerPath}"
npm run analyze:project -- --config "${configPath}" --fail-on error

if [ $? -ne 0 ]; then
  echo "❌ Push blocked due to critical issues"
  exit 1
fi

echo "✅ All checks passed"
`;
        await fs.writeFile(path.join(gitDir, 'hooks', 'pre-push'), prePushHook, { mode: 0o755 });
        
        console.log('✅ Installed Git hooks');
      }
    } catch {
      // No .git directory
    }
    
    // Create VS Code settings if .vscode exists or user wants it
    if (await question('\nCreate VS Code integration? (Y/n): ') !== 'n') {
      const vscodeDir = path.join(targetPath, '.vscode');
      await fs.mkdir(vscodeDir, { recursive: true });
      
      const vscodeSettings = {
        "mcp.servers": {
          "code-analyzer": {
            "command": "node",
            "args": [`${analyzerPath}/dist/server.js`],
            "env": {
              "PROJECT_CONFIG": configPath
            }
          }
        },
        "mcp.autoAnalyze": true,
        "mcp.analyzeOnSave": true,
        "mcp.showInlineWarnings": true,
        "mcp.selectedTools": selectedTools
      };
      
      await fs.writeFile(
        path.join(vscodeDir, 'mcp-analyzer.json'), 
        JSON.stringify(vscodeSettings, null, 2)
      );
      console.log('✅ Created VS Code settings');
    }
    
    // Show summary
    console.log('\n' + '='.repeat(60));
    console.log('✅ Integration Complete!\n');
    console.log(`Project: ${projectName}`);
    console.log(`Path: ${targetPath}`);
    console.log(`Profile: ${profile.name}`);
    console.log(`Tools: ${selectedTools.join(', ')}`);
    console.log('\nUsage:');
    
    if (await fs.access(packageJsonPath).then(() => true).catch(() => false)) {
      console.log('  npm run analyze        # Run full analysis');
      console.log('  npm run analyze:watch  # Start real-time monitoring');
      console.log('  npm run analyze:report # Generate HTML report');
    } else {
      console.log('  .mcp/scripts/analyze.sh        # Run full analysis');
      console.log('  .mcp/scripts/watch.sh          # Start real-time monitoring');
    }
    
    console.log('\nThe analyzer will automatically check for:');
    criticalRules.forEach(rule => console.log(`  - ${rule}`));
    
    // Start analyzer now?
    if (await question('\n\nStart analyzer now? (Y/n): ') !== 'n') {
      console.log('\n🚀 Starting analyzer...\n');
      execSync(`cd "${analyzerPath}" && npm run start -- --subscribe "${targetPath}" --config "${configPath}"`, {
        stdio: 'inherit'
      });
    }
    
  } catch (error) {
    console.error('Error:', error);
  } finally {
    rl.close();
  }
}

// Run the script
main().catch(console.error);