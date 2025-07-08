# Claude Holistic Development Checklist

## Purpose
This checklist ensures Claude follows the Holistic Architecture Instruction Set and avoids violations of established patterns, particularly DRY principle and architectural consistency.

## 🔴 MANDATORY: Pre-Development Analysis

### Before Writing ANY Code:

#### 1. Local Context Analysis ✓
- [ ] Read the immediate file/class completely
- [ ] Understand the current implementation approach
- [ ] Identify local coding standards being followed
- [ ] Document what I'm about to change and why

#### 2. System-Wide Impact Investigation ✓
- [ ] **Check Base Classes**: Does functionality already exist in parent classes?
- [ ] **Check Interfaces**: What contracts must be honored?
- [ ] **Search for Patterns**: Use `Grep` to find similar implementations
- [ ] **Trace Data Flow**: Where does data come from and go to?
- [ ] **Identify Dependencies**: What will be affected by changes?

#### 3. Architectural Integrity Check ✓
- [ ] Verify alignment with CanonicalServiceBase patterns
- [ ] Confirm adherence to SOLID principles
- [ ] Check for DRY violations
- [ ] Assess impact on existing architecture

#### 4. Strategic Questions ✓
Ask myself:
- [ ] "Why does this error/issue exist?"
- [ ] "Has this been solved elsewhere in the codebase?"
- [ ] "What established pattern should I follow?"
- [ ] "Am I about to violate DRY/SOLID principles?"
- [ ] "Should I check MANDATORY_DEVELOPMENT_STANDARDS?"

## 🟡 MANDATORY: During Development

### For EVERY Error/Issue:

#### 1. Error Analysis Protocol ✓
```
STOP → UNDERSTAND → RESEARCH → VERIFY → IMPLEMENT
```
NOT: `ERROR → FIX → NEXT`

- [ ] Understand the error message completely
- [ ] Research why this error occurs
- [ ] Check if solution already exists
- [ ] Verify approach with patterns
- [ ] Then implement solution

#### 2. Pattern Verification ✓
- [ ] Am I following CanonicalServiceBase patterns?
- [ ] Is LogMethodEntry/Exit in EVERY method?
- [ ] Are all returns using TradingResult<T>?
- [ ] Is decimal used for ALL financial values?
- [ ] Is ConfigureAwait(false) on ALL awaits?

#### 3. Multi-Level Solution Strategy ✓
For each fix, consider:
- [ ] **Immediate**: Local fix with minimal disruption
- [ ] **Tactical**: Related improvements needed?
- [ ] **Strategic**: Architectural changes required?
- [ ] **Preventive**: How to prevent recurrence?

## 🟢 MANDATORY: Validation Checkpoints

### Every 5-10 Changes:
- [ ] Run build - MUST have ZERO warnings
- [ ] Check for pattern consistency
- [ ] Verify no duplicate code introduced
- [ ] Confirm following established conventions

### Before Major Implementations:
- [ ] Search for existing implementations
- [ ] Review similar services for patterns
- [ ] Check base classes for functionality
- [ ] Verify not reinventing the wheel

### When Creating New Services:
- [ ] Inherit from CanonicalServiceBase
- [ ] Implement OnInitializeAsync pattern
- [ ] Implement proper Dispose pattern (if needed beyond base)
- [ ] Add comprehensive logging
- [ ] Follow TradingResult<T> pattern

## 🔵 MANDATORY: Common Pitfalls to Avoid

### 1. The Disposal Trap
- [ ] NEVER add Dispose if base class has it
- [ ] CHECK CanonicalServiceBase FIRST
- [ ] Override Dispose(bool) if needed, not Dispose()

### 2. The Quick Fix Trap
- [ ] NEVER implement without understanding context
- [ ] ALWAYS check for existing solutions
- [ ] AVOID creating redundant functionality

### 3. The Assumption Trap
- [ ] NEVER assume the obvious fix is correct
- [ ] ALWAYS verify patterns exist
- [ ] QUESTION why the error exists

### 4. The Isolation Trap
- [ ] NEVER treat changes as isolated
- [ ] ALWAYS consider ripple effects
- [ ] THINK about system-wide impact

## 📋 Development Workflow Checklist

### Starting a New Task:
1. [ ] Read MANDATORY_DEVELOPMENT_STANDARDS
2. [ ] Review relevant existing code
3. [ ] Identify patterns to follow
4. [ ] Plan approach before coding

### Fixing Compilation Errors:
1. [ ] Understand why error exists
2. [ ] Check if already solved elsewhere
3. [ ] Follow established patterns
4. [ ] Verify fix doesn't break other code

### Adding New Features:
1. [ ] Research existing similar features
2. [ ] Follow established architecture
3. [ ] Maintain pattern consistency
4. [ ] Document any new patterns

### Before Committing:
1. [ ] ZERO warnings policy met
2. [ ] All patterns followed
3. [ ] No DRY violations
4. [ ] Tests pass (if applicable)

## 🚨 Red Flags - STOP and RECONSIDER

If you find yourself:
- Adding a method that sounds familiar → CHECK BASE CLASSES
- Copy-pasting code → EXTRACT TO SHARED LOCATION
- Fighting the framework → UNDERSTAND THE PATTERN
- Making assumptions → VERIFY WITH RESEARCH
- Fixing without understanding → STOP AND ANALYZE

## 💡 Information Gathering Protocol

When lacking context, explicitly request:
- [ ] Architecture documentation
- [ ] Related module implementations
- [ ] Configuration patterns
- [ ] Test examples
- [ ] Similar service implementations

## 📝 Documentation Requirements

After implementation:
- [ ] Document patterns followed
- [ ] Note any deviations and why
- [ ] Update this checklist if new patterns discovered
- [ ] Create journal entry for significant learnings

## 🎯 Success Criteria

Task is complete when:
- [ ] Code builds with ZERO warnings
- [ ] All patterns consistently followed
- [ ] No DRY violations introduced
- [ ] Base class functionality properly used
- [ ] Changes align with architecture

## Example Workflow

### Bad Approach:
```
See: "Field '_sessionOptions' is never disposed"
Think: "Add Dispose method"
Do: Create Dispose method
Result: Duplicate method error
```

### Good Approach:
```
See: "Field '_sessionOptions' is never disposed"
Think: "Why is disposal needed? Is it already handled?"
Check: CanonicalServiceBase for disposal pattern
Find: Base class already has Dispose
Do: Override Dispose(bool) if needed
Result: Proper pattern implementation
```

## Living Document Note

This checklist should be updated when:
- New patterns are established
- Common mistakes are identified
- Better practices are discovered
- Architectural changes occur

Last Updated: 2025-07-07
Triggered By: DRY Violation in MLInferenceService