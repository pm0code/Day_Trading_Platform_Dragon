# Claude Checklist Quick Reference Card for Users

## 🎯 Purpose
Quick prompts to trigger Claude's holistic thinking without explicitly mentioning the checklist.

---

## 🔴 BEFORE ANY CODING TASK

### Copy-Paste This Block:
```
Before starting:
1. Why does this issue exist?
2. Check if solutions exist in base classes
3. What established pattern should you follow?
4. Show me your research
```

---

## 🟡 TASK-SPECIFIC TRIGGERS

### For Compilation Errors:
```
"For each error:
- Explain why it exists
- Check existing patterns first
- Show me similar implementations"
```

### For New Features:
```
"Before implementing:
- Search for similar features
- What pattern will you follow?
- Check CanonicalServiceBase"
```

### For Bug Fixes:
```
"First understand:
- Root cause analysis
- System-wide impact
- Existing solutions"
```

### For Refactoring:
```
"Document:
- Current architecture
- Ripple effects
- Pattern compliance"
```

---

## 🟢 CHECKPOINT QUESTIONS

### Every 5-10 Changes:
```
"Status check:
- Any warnings?
- Pattern consistency?
- DRY violations?"
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