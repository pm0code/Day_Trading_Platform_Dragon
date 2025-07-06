# Standard Tools and Patterns - MANDATORY GUIDELINES

**🚨 CRITICAL REQUIREMENT**: This document outlines MANDATORY standards that ALL agents and developers MUST follow. Using proprietary/custom solutions when industry standards exist is NOT acceptable.

## 🔴 MANDATORY RESEARCH REQUIREMENT

### Before Building ANYTHING, You MUST:

1. **STOP AND RESEARCH FIRST** (Minimum 2-4 hours)
   - Search for existing solutions on npm, GitHub, awesome lists
   - Check npm trends for popularity and maintenance
   - Read recent (2024-2025) community discussions
   - Compare at least 3 alternatives
   - Document your findings

2. **CREATE A RESEARCH REPORT** including:
   - Problem statement
   - Existing solutions found
   - Pros/cons of each solution
   - Community sentiment and adoption
   - Your recommendation with justification

3. **READ COMPLETE DOCUMENTATION** (MANDATORY):
   - Download and read the ENTIRE documentation for chosen component
   - Understand ALL features and configuration options
   - Review code examples and best practices
   - Check migration guides if replacing existing code
   - **GUESSING HOW THINGS WORK IS FORBIDDEN**

4. **GET APPROVAL** before proceeding:
   - Share research findings with team
   - Demonstrate understanding of documentation
   - Justify if proposing custom solution
   - Wait for explicit approval
   - Document the decision

**⚠️ FAILURE TO RESEARCH = WASTED WORK = UNACCEPTABLE**
**🚫 GUESSING API USAGE = CANONICAL VIOLATION = NOT ACCEPTABLE**

## 🚨 Core Principle: Research First, Build Second

### The Research-First Workflow:
```
1. Identify need/requirement
2. STOP - Don't code yet!
3. Research existing solutions (2-4 hours minimum)
4. Document findings
5. Choose standard solution OR justify custom
6. READ COMPLETE DOCUMENTATION (no guessing!)
7. Get approval
8. Only THEN start implementation
```

### Research Checklist (MANDATORY):
- [ ] Searched npm for existing packages
- [ ] Checked GitHub awesome lists
- [ ] Read recent blog posts/articles (2024-2025)
- [ ] Checked Stack Overflow discussions
- [ ] Reviewed npm trends/downloads
- [ ] Compared at least 3 alternatives
- [ ] **READ COMPLETE DOCUMENTATION** for chosen tool
- [ ] **NO GUESSING** - understood every API/config option
- [ ] Documented all findings
- [ ] Got approval before coding

## 📋 Mandatory Standards by Category

### Documentation Systems
❌ **DO NOT**: Build custom documentation portals
✅ **RESEARCH FIRST, then USE**: 
- **Docusaurus** (Meta) - For full documentation sites
- **Starlight** (Astro) - Modern alternative, better performance
- **VitePress** - For lightweight Vue-based docs
- **MkDocs** - For Python projects
- **Nextra** - For Next.js-based docs

**2025 Update**: Research Starlight vs Docusaurus based on your needs

### Version Management
❌ **DO NOT**: Write custom version tracking systems
✅ **RESEARCH FIRST, then USE**:
- **Semantic Release** - Automated versioning from commits
- **Changesets** - For monorepos and libraries
- **Standard-version** - Conventional commits + CHANGELOG
- **Release-it** - Interactive release management

### Document Export/Conversion
❌ **DO NOT**: Implement custom converters
✅ **RESEARCH FIRST, then USE**:
- **Pandoc** - Universal document converter
- **Puppeteer/Playwright** - HTML to PDF
- **epub-gen** - For EPUB generation
- **Unified ecosystem** (remark/rehype) - For markdown processing

### Progress Tracking/CLI UX
❌ **DO NOT**: Build custom progress systems
✅ **RESEARCH FIRST, then USE**:
- **ora** - Terminal spinners
- **cli-progress** - Progress bars
- **listr2** - Task lists with progress
- **ink** - React for CLIs

### File Watching
❌ **DO NOT**: Implement custom file watchers
✅ **RESEARCH FIRST, then USE**:
- **chokidar** - Cross-platform file watching
- **nodemon** - For development
- **watchman** - Facebook's file watching service
- **p-debounce** - For debouncing file events

### Search Functionality
❌ **DO NOT**: Build custom search engines
✅ **RESEARCH FIRST, then USE**:
- **Algolia DocSearch** - For documentation
- **Lunr.js** - Client-side search
- **FlexSearch** - Fast, memory-efficient search
- **Fuse.js** - Fuzzy search

### Build Systems/Incremental Builds
❌ **DO NOT**: Create custom build orchestration
✅ **RESEARCH FIRST, then USE**:
- **Turbo** - Monorepo build system
- **Nx** - Smart build system
- **Bazel** - Google's build system
- **Make** - Classic dependency management

### State Management
❌ **DO NOT**: Build custom state systems
✅ **RESEARCH FIRST, then USE**:
- **Redux** - For complex state
- **Zustand** - Lightweight alternative
- **Valtio** - Proxy-based state
- **XState** - State machines

### WebSocket/Real-time
❌ **DO NOT**: Implement custom WebSocket handling
✅ **RESEARCH FIRST, then USE**:
- **Socket.io** - With fallbacks
- **uWebSockets.js** - For 10x performance (2025)
- **ws** - Pure WebSocket
- **Pusher/Ably** - Managed real-time

### Session Management
❌ **DO NOT**: Build custom session managers
✅ **RESEARCH FIRST, then USE**:
- **iron-session** - Stateless, cookie-based (2025 recommendation)
- **express-session** - Traditional server-side sessions
- **connect-redis** - For Redis session storage

### Rate Limiting
❌ **DO NOT**: Implement custom rate limiters
✅ **RESEARCH FIRST, then USE**:
- **rate-limiter-flexible** - Most powerful solution
- **express-rate-limit** - Simple Express middleware
- **bottleneck** - Generic rate limiter

### Job Queues/Workers
❌ **DO NOT**: Build custom job queues
✅ **RESEARCH FIRST, then USE**:
- **BullMQ** - Redis-based queues (NOT Bull - it's deprecated!)
- **Bree** - Worker threads with cron
- **Agenda** - MongoDB-backed jobs

### Connection Pooling
❌ **DO NOT**: Write custom connection pools
✅ **RESEARCH FIRST, then USE**:
- **tarn.js** - Used by Knex (2025 recommendation)
- **generic-pool** - Generic resource pooling
- Driver-specific pools (pg-pool, etc.)

### Event Systems
❌ **DO NOT**: Create custom event emitters
✅ **RESEARCH FIRST, then USE**:
- **EventEmitter3** - Performance-focused (2025)
- **EventEmitter2** - Feature-rich (wildcards, TTL)
- **mitt** - Tiny 2KB event emitter

### Testing
❌ **DO NOT**: Create custom test runners
✅ **RESEARCH FIRST, then USE**:
- **Jest** - For unit/integration tests
- **Vitest** - Vite-native testing
- **Playwright** - E2E testing
- **Cypress** - E2E with good DX

### Logging
❌ **DO NOT**: Build custom loggers (except MCPLogger which is our standard)
✅ **RESEARCH FIRST, then USE**:
- **MCPLogger** - Our canonical logger
- **winston** - If MCPLogger not available
- **pino** - High-performance logging
- **bunyan** - Structured logging

### Process Management
❌ **DO NOT**: Implement custom process managers
✅ **RESEARCH FIRST, then USE**:
- **PM2** - Production process manager
- **systemd** - Linux system service
- **Docker** - Containerization
- **Kubernetes** - Container orchestration

### Configuration Management
❌ **DO NOT**: Build custom config systems
✅ **RESEARCH FIRST, then USE**:
- **dotenv** - Environment variables
- **config** - Hierarchical configurations
- **convict** - Configuration with validation
- **cosmiconfig** - Find and load configuration

### API Documentation
❌ **DO NOT**: Write custom API doc generators
✅ **RESEARCH FIRST, then USE**:
- **OpenAPI/Swagger** - API specification
- **TypeDoc** - TypeScript documentation
- **JSDoc** - JavaScript documentation
- **AsyncAPI** - For event-driven APIs

### Database/ORM
❌ **DO NOT**: Write custom database abstractions
✅ **RESEARCH FIRST, then USE**:
- **Prisma** - Modern ORM
- **TypeORM** - TypeScript ORM
- **Sequelize** - Promise-based ORM
- **Knex** - SQL query builder

### Authentication
❌ **DO NOT**: Implement custom auth
✅ **RESEARCH FIRST, then USE**:
- **Passport.js** - Authentication middleware
- **Auth0** - Managed authentication
- **Firebase Auth** - Google's auth service
- **Clerk** - Modern auth solution

## 🎯 Decision Framework

### MANDATORY Research Steps:

1. **Identify the Problem**
   ```
   What exactly are we trying to solve?
   What are the requirements?
   What constraints exist?
   ```

2. **Research Phase (2-4 hours minimum)**
   ```
   - Search: "best [category] library 2025"
   - Check: npmtrends.com for comparisons
   - Read: Recent blog posts and tutorials
   - Review: GitHub issues and discussions
   - Compare: Download stats, maintenance, features
   ```

3. **Document Findings**
   ```markdown
   ## Research Report: [Feature Name]
   
   ### Problem Statement
   [What we're trying to solve]
   
   ### Solutions Evaluated
   1. **Library A**
      - Pros: [list]
      - Cons: [list]
      - Downloads: X/week
      - Last updated: date
      - Documentation URL: [link]
      - Documentation read: ✅ Complete
   
   2. **Library B**
      - Pros: [list]
      - Cons: [list]
      - Downloads: X/week
      - Last updated: date
      - Documentation URL: [link]
      - Documentation read: ✅ Complete
   
   ### Recommendation
   [Your choice and why]
   
   ### Documentation Summary
   - Key APIs understood: [list main functions]
   - Configuration options: [list key configs]
   - Best practices noted: [list important patterns]
   ```

4. **Get Approval**
   - Present research to team
   - Discuss trade-offs
   - Document decision
   - Get explicit approval

## 📝 Example: What Happens When You Don't Research

### ❌ WRONG Approach (Documentation System Disaster):
```typescript
// Developer spent 40+ hours building:
- Custom HTML/CSS/JS portal
- Hand-rolled search implementation  
- Manual version tracking system
- Custom markdown converters
- Proprietary export system

// Result: 
- Missing features that Docusaurus has
- Maintenance nightmare
- Security vulnerabilities
- Poor performance
- No community support
```

### ✅ RIGHT Approach (What should have happened):
```bash
# Step 1: Research (2 hours)
- Found Docusaurus, VitePress, MkDocs
- Compared features, popularity, maintenance
- Read community feedback
- Chose Docusaurus for React ecosystem

# Step 2: Implementation (2 hours)
npx create-docusaurus@latest docs classic --typescript

# Result:
- Professional documentation site
- Algolia search integration
- Versioning out of the box
- i18n support
- Plugin ecosystem
- Community themes
- Regular updates
```

## 🚨 Consequences of Not Researching First

1. **Wasted Development Time**: 40+ hours vs 2 hours
2. **Maintenance Burden**: Ongoing custom code maintenance
3. **Missing Features**: Standards have features you didn't consider
4. **Security Risks**: Custom code = more vulnerabilities
5. **Onboarding Difficulty**: New devs must learn proprietary systems
6. **Integration Problems**: Custom solutions don't integrate
7. **Technical Debt**: Accumulates rapidly with custom code

## 📋 Pre-Implementation Checklist (MANDATORY)

**DO NOT START CODING** until ALL boxes are checked:

- [ ] I have identified the exact problem to solve
- [ ] I have spent 2-4 hours researching solutions
- [ ] I have evaluated at least 3 existing options
- [ ] I have checked npm downloads and maintenance status
- [ ] I have read recent (2024-2025) community discussions
- [ ] I have documented all findings in a research report
- [ ] I have presented findings to the team
- [ ] I have received written approval for my choice
- [ ] If building custom, I have exceptional justification
- [ ] I understand the long-term maintenance implications

## 🔄 If You've Already Built Custom

Don't panic, but DO:

1. **Stop adding features** to custom solution
2. **Document what exists** thoroughly
3. **Research standard alternatives** NOW
4. **Plan migration path** (incremental is fine)
5. **Learn from the mistake** and share learnings
6. **Migrate systematically** to standard solution
7. **REMOVE DEAD CODE** after each successful migration (MANDATORY)

## 🧹 Dead Code Removal (MANDATORY AFTER MIGRATION)

### The Rule:
**Dead code removal MUST happen after EVERY successful migration. Do NOT postpone it.**

### What Constitutes Dead Code:
1. **Old implementations** replaced by standard tools
2. **Backup files** (.backup, .old, .deprecated)
3. **Commented-out code** from previous versions
4. **Unused imports** and dependencies
5. **Test files** for removed features
6. **Configuration** for deprecated systems
7. **Documentation** for removed features

### Dead Code Removal Process:
1. **Complete the migration** to standard tool
2. **Verify new implementation** works correctly
3. **Run all tests** to ensure nothing breaks
4. **Identify all related old code**:
   ```bash
   # Find backup files
   find . -name "*.backup" -o -name "*.old" -o -name "*.deprecated"
   
   # Find TODO/DEPRECATED comments
   grep -r "TODO.*remove\|DEPRECATED\|LEGACY\|OLD_"
   
   # Use git to find deleted imports
   git diff --name-only | xargs grep -l "no longer used"
   ```
5. **Remove dead code immediately**:
   - Delete old implementation files
   - Remove unused dependencies from package.json
   - Clean up imports and exports
   - Delete related test files
   - Update documentation
6. **Commit the cleanup** separately:
   ```bash
   git add -A
   git commit -m "cleanup: Remove dead code after [feature] migration to [standard tool]"
   ```

### Why Immediate Removal is MANDATORY:
1. **Confusion Prevention**: Dead code confuses future developers
2. **Maintenance Overhead**: Dead code still appears in searches
3. **False Dependencies**: Package.json bloat with unused deps
4. **Testing Burden**: Dead tests may still run and fail
5. **Security Risk**: Unmaintained code can have vulnerabilities
6. **Build Size**: Dead code increases bundle/build size

### Examples of Post-Migration Cleanup:
```bash
# After migrating from custom logger to Winston:
rm -rf src/custom-logger/
rm -rf tests/custom-logger/
npm uninstall custom-logger-deps
# Update all imports to use new logger

# After migrating from Bull to BullMQ:
rm -rf src/workers/bull-legacy/
rm -rf config/bull.config.js
npm uninstall bull
# Remove Bull-specific types and interfaces

# After migrating custom state to Zustand:
rm -rf src/state/custom-state-manager/
rm -rf docs/custom-state-api.md
# Remove all CustomState type definitions
```

### The "No Postponement" Rule:
- ❌ "We'll clean it up later" - NOT ACCEPTABLE
- ❌ "Keep it for reference" - Use git history instead
- ❌ "Might need it again" - You won't, you have standards now
- ✅ "Migration complete, dead code removed" - THIS IS THE WAY

## 📚 Research Resources (USE THESE!)

- [Awesome Lists](https://github.com/sindresorhus/awesome) - Curated lists by category
- [NPM Trends](https://npmtrends.com) - Compare package popularity
- [State of JS](https://stateofjs.com) - Annual JavaScript ecosystem survey
- [ThoughtWorks Tech Radar](https://www.thoughtworks.com/radar) - Technology trends
- [Best of JS](https://bestofjs.org) - JavaScript library rankings
- [LibHunt](https://www.libhunt.com) - Compare similar libraries

## 🎓 Real Lessons Learned

### Documentation System Fiasco:
- **Time Wasted**: 40+ hours building custom
- **Could Have Used**: Docusaurus (2 hour setup)
- **Missing Features**: Search, versioning, i18n, plugins
- **Maintenance Cost**: 20+ hours/month ongoing

### Custom Event System Mistake:
- **Time Wasted**: 20+ hours on TypedEventEmitter
- **Could Have Used**: EventEmitter2 with TypeScript
- **Missing Features**: Wildcards, TTL, async events
- **Bugs Introduced**: Memory leaks, race conditions

### Worker Pool Disaster:
- **Time Wasted**: 30+ hours custom implementation
- **Could Have Used**: BullMQ
- **Missing Features**: Retries, scheduling, monitoring
- **Problems**: Crashes, memory issues, no scaling

## 🚫 The "NO GUESSING" Rule - CANONICAL VIOLATION

### What Constitutes Guessing (ALL FORBIDDEN):

1. **API Guessing**
   - ❌ "I think this function probably takes these parameters"
   - ❌ "This should work like the other library I used"
   - ❌ "Let me try different combinations until it works"
   - ✅ "The documentation says this function takes X, Y, Z parameters"

2. **Configuration Guessing**
   - ❌ "These config options look reasonable"
   - ❌ "I'll figure out the options as I go"
   - ❌ "This worked in another project"
   - ✅ "I've read all configuration options in the docs"

3. **Pattern Guessing**
   - ❌ "This is probably how they want it structured"
   - ❌ "Most libraries work this way"
   - ❌ "The examples don't cover this, so I'll improvise"
   - ✅ "The best practices guide specifically shows this pattern"

### Consequences of Guessing:

1. **Immediate Issues**:
   - Wrong API usage leads to bugs
   - Missed configuration causes failures
   - Incorrect patterns create tech debt

2. **Long-term Problems**:
   - Future developers inherit broken code
   - Updates break guessed implementations
   - Performance issues from wrong usage

### The Documentation Rule:

**BEFORE writing ANY code using a library:**
1. Read the **Getting Started** guide completely
2. Review **API Reference** for all methods you'll use
3. Check **Configuration** documentation thoroughly
4. Study **Examples** and **Best Practices**
5. Review **Common Pitfalls** or **FAQ** sections
6. Check **Migration Guides** if updating

**If documentation is unclear:**
- Check GitHub issues for clarification
- Look for official examples
- Ask in community forums
- **NEVER GUESS** - always verify

### Example of Proper Documentation Usage:

```javascript
// ❌ WRONG - Guessing based on function name
const result = await someLibrary.process(data, { fast: true });

// ✅ RIGHT - After reading docs
// Documentation reference: https://lib.com/api#process
// process(data: ProcessData, options: ProcessOptions): Promise<ProcessResult>
// ProcessOptions = { mode: 'fast' | 'accurate', timeout?: number }
const result = await someLibrary.process(data, { 
  mode: 'fast',  // Documented option, not guessed
  timeout: 5000  // Optional, but documented default is 30000
});
```

## 🔴 FINAL REMINDER

**RESEARCH FIRST, BUILD SECOND, READ DOCS COMPLETELY, NEVER GUESS**

Your job is to solve business problems efficiently, not to reinvent existing solutions. Every hour spent building something that already exists is an hour stolen from building unique value.

**Before you write a single line of code, ask yourself:**
1. Has someone already solved this?
2. Have I researched thoroughly?
3. Have I documented my findings?
4. Have I gotten approval?

If any answer is "no", STOP and do the research first.

**Remember**: The most expensive code is the code you shouldn't have written.