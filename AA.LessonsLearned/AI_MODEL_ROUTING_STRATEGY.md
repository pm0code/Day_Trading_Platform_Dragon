# AI Model Routing Strategy
**Last Updated**: July 11, 2025  
**Agent**: tradingagent

## Model Selection by Task Type

### 🏦 FINANCIAL QUESTIONS → Gemini (ONLY)
**When**: Any question about financial standards, monetary calculations, decimal vs double
**Why**: Gemini provides authoritative, clear answers aligned with our standards
**Example**: "Is using double for market cap a violation?" → Gemini only

### 🐛 CODE FIXES → DeepSeek-Coder / CodeGemma
**When**: Fixing syntax errors, implementing methods, code generation
**Why**: Specialized for code analysis and generation

### 📊 STATISTICAL/MATH → Gemma2 / Mistral
**When**: Statistical calculations, mathematical algorithms
**Why**: Good at reasoning about non-financial numerical methods

### ✅ QUICK VALIDATION → Mistral
**When**: Simple yes/no checks, basic validation
**Why**: Fast, lightweight model

### 🏗️ ARCHITECTURE → Gemini
**When**: Design decisions, architectural questions
**Why**: Best understanding of complex systems

## Key Learning (July 11, 2025)

When analyzing financial safety violations, I wasted time asking multiple Ollama models:
- Gemma2: Gave confusing "FALSE" with contradictory explanation  
- DeepSeek: Gave vague, non-committal answer
- Gemini: Gave clear "YES" - violation confirmed

**Lesson**: For financial questions, go straight to Gemini. Don't waste time with local models.

## Updated Workflow

```python
if question_type == "financial":
    answer = gemini.ask(question)  # ONLY Gemini
elif question_type == "code_fix":
    answer = deepseek_coder.generate(context)
elif question_type == "architecture":
    answer = gemini.ask(question)
else:
    answer = select_best_model(question_type)
```

## Time Savings

- Before: Ask 3-4 models, get mixed answers, confusion
- After: Ask Gemini directly for financial questions, get authoritative answer
- Result: 5 minutes → 30 seconds