# Claude Checklist Quick Reference Card for Users

## 🎯 Purpose
Quick prompts to trigger Claude's holistic thinking without explicitly mentioning the checklist.

---

## 🔴 BEFORE ANY CODING TASK

### Copy-Paste This Block:
```
Before starting:  
1. Why does this issue or task exist? Clarify intent and context  
2. Check if solutions already exist in base classes or shared utilities  
3. What established architectural or design pattern applies?  
4. Show me your research (industry standards, references, docs)  
5. Search the codebase for related implementations or overlaps  
6. Identify dependencies, side effects, and system-wide impact  
7. Ensure alignment with existing logging, error handling, and telemetry patterns  
8. Validate test coverage and testability expectations before coding  
9. Confirm performance, resilience, and observability considerations are accounted for  

```

---

## 🟡 TASK-SPECIFIC TRIGGERS

### For Compilation Errors:
```
For each error:  
- Explain the cause clearly and concisely  
- Check if the fix aligns with existing code patterns  
- Show similar working implementations or references  
- Validate that the proposed fix won’t introduce side effects  

```

### For New Features:
```
Before implementing:
	- Research industry trends and 2025 state-of-the-art practices for this feature
	- Search for similar functionality in open-source projects and documentation
	- Scan the existing codebase for related or duplicate logic
	- Review CanonicalServiceBase for alignment, reuse, or extension
	- Ensure plans include structured logging, diagnostics, exception handling
	- Ensure that AI and GPU are used within applicable and logical services 
	- Validate support for high availability, fault tolerance, and graceful degradation
	- Confirm observability hooks: telemetry, metrics, tracing, and health checks
	- Evaluate performance impact and thread-safety or concurrency considerations
	- Consider testability: unit tests, mocks, and integration test strategy
	- Document your research in a timestamped document properly
	- Identify and justify the design/architectural pattern to be used (e.g., CQRS, DDD, event-driven, etc.)
```

### For Bug Fixes:
```
First, understand:  
- Root cause and triggering conditions  
- Steps to reliably reproduce the issue (if possible)  
- Scope and system-wide impact (dependencies, shared components)  
- Whether similar bugs or fixes already exist (codebase, changelogs, issue tracker)  
- Available logging, telemetry, and diagnostics that confirm the issue  
- Whether the bug indicates deeper architectural, performance, or regression risk  
- Any missing tests or coverage gaps that allowed the bug to go undetected  
  

```

### For Refactoring:
```
Document:  
- Current architecture and flow before changes  
- Intended improvements and reasons for refactoring  
- Potential ripple effects across modules and dependencies  
- Compliance with established patterns and architectural principles  
- Impact on performance, stability, and readability  
- Existing test coverage and new tests required  
- Logging, monitoring, and error-handling adjustments (if any)  

```
Claude being lazy:

Our policy is 0/0 meaning 0 errors and 0 warnings before any new development.
  please respect the policy and proceed!





---

## 🟢 CHECKPOINT QUESTIONS

### Every 5-10 Changes:
```
Status Check

- All canonical implementations followed?  
- Code organization clean (models, methods, classes, folders)?  
- Logging, debugging, and error handling in place and consistent?  
- Resilience and health monitoring mechanisms present?  
- No circular references or hidden dependencies?  
- No build errors or runtime warnings?  
- Consistent use of architectural and design patterns?  
- No DRY violations or code duplication?  
- Naming conventions and formatting aligned with standards?  
- Dependencies and libraries properly managed and justified?  
- All changes documented and traceable?  
```

### When Claude Seems Rushed:
```
"Stop. Are you following established patterns?"
```

### When Adding Methods:
```
"Did you check if this exists in base classes?"
```

### When Creating Services:
```
"Show me which base class you're extending and why"
```

---

## 🔵 RED FLAG INTERVENTIONS

### If Claude adds Dispose/Close/Cleanup:
```
"Red flag: Check base class first"
```

### If Claude copy-pastes code:
```
"Red flag: Extract to shared location"
```

### If Claude makes assumptions:
```
"Red flag: Verify with research"
```

### If Claude rushes to fix:
```
"Red flag: Understand before implementing"
```

---

## 📋 QUICK CORRECTION PHRASES

### Pattern Violations:
```
"This violates DRY. Find the existing implementation."
```

### Missing Context:
```
"You're treating this as isolated. Show me the system impact."
```

### Incomplete Analysis:
```
"Multi-level solution: immediate, tactical, strategic?"
```

### Assumption Making:
```
"Don't assume. Research and verify."
```

---

## 💡 BEHAVIORAL TRIGGERS

### To Activate Research Mode:
```
"What similar patterns exist in the codebase?"
```

### To Activate Analysis Mode:
```
"Trace the data flow and dependencies"
```

### To Activate Verification Mode:
```
"Prove this doesn't already exist"
```

### To Activate Reflection Mode:
```
"What would the checklist say about this?"
```

---

## 🎪 PROGRESSIVE DIFFICULTY

### Beginner Mode:
```
"Check base classes, then implement"
```

### Intermediate Mode:
```
"Holistic analysis first, then code"
```

### Advanced Mode:
```
"What would you verify before implementing?"
```

### Expert Mode:
```
*Say nothing - Claude should self-check*
```

---

## ⚡ QUICK WINS

### Start of Session:
```
"What are three things you'll check before coding today?"
```

### Mid-Session Reality Check:
```
"Are you following your own patterns?"
```

### End of Session:
```
"What checklist items did you miss today?"
```

---

## 🚨 EMERGENCY STOPS

### When Claude is clearly violating patterns:
```
"STOP. Checklist section 4.1"
```

### When Claude is overengineering:
```
"STOP. Is this already solved?"
```

### When Claude loses context:
```
"STOP. Re-read the architecture"
```

---

## 📊 EFFECTIVENESS METRICS

Track if Claude:
- ✓ Checks before implementing
- ✓ Mentions base classes unprompted
- ✓ Shows pattern research
- ✓ Provides multi-level solutions
- ✓ Catches own violations

---

## 🎬 SAMPLE CONVERSATION FLOW

**Bad Flow:**
```
User: "Fix these errors"
Claude: *immediately starts fixing*
User: *watches mistakes happen*
```

**Good Flow:**
```
User: "Fix these errors - what's your analysis?"
Claude: "Let me check existing patterns first..."
User: "Good. Proceed."
```

---

## 📌 PRINT THIS SECTION

### The Universal Prompt:
```
"Before you code:
1. Check if it exists
2. Follow the pattern
3. Think system-wide
4. Then implement"
```

### The Universal Correction:
```
"You violated DRY. Where's the existing implementation?"
```

### The Universal Praise:
```
"Good pattern research. Proceed."
```

---

*Last Updated: 2025-07-07*  
*Purpose: Help users guide Claude without micromanaging*