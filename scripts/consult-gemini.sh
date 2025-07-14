#!/bin/bash

# Gemini AI Consultation Script for Architectural Decisions
# Usage: ./consult-gemini.sh "Your architectural question here"

GEMINI_API_KEY="AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs"
GEMINI_URL="https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$GEMINI_API_KEY"

# Check if question is provided
if [ -z "$1" ]; then
    echo "Usage: $0 \"Your architectural question here\""
    exit 1
fi

QUESTION="$1"

# Predefined architectural consultation prompt template
PROMPT="You are an expert C# architect specializing in financial trading systems, Domain-Driven Design (DDD), and high-performance computing. 

Context: I am building AIRES (AI Error Resolution System), a development tool that uses a 4-stage AI pipeline (Mistral, DeepSeek, CodeGemma, Gemma2) to autonomously analyze and resolve compiler errors.

Architectural Question: $QUESTION

Please provide:
1. **Architectural Analysis**: Evaluate the design approach
2. **Best Practices**: Relevant C# and .NET patterns
3. **Potential Issues**: What problems might arise?
4. **Recommended Solution**: Concrete implementation guidance
5. **Alternative Approaches**: Other viable options
6. **Performance Considerations**: Impact on system performance
7. **Maintainability**: Long-term maintenance implications

Focus on practical, implementable solutions following SOLID principles and canonical patterns."

# Create JSON payload
JSON_PAYLOAD=$(jq -n \
  --arg prompt "$PROMPT" \
  '{
    "contents": [{
      "parts": [{
        "text": $prompt
      }]
    }],
    "generationConfig": {
      "temperature": 0.2,
      "maxOutputTokens": 2048
    }
  }')

# Make API call
echo "🤖 Consulting Gemini AI for architectural guidance..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

RESPONSE=$(curl -s "$GEMINI_URL" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d "$JSON_PAYLOAD")

# Extract and display response
echo "$RESPONSE" | jq -r '.candidates[0].content.parts[0].text' 2>/dev/null || {
    echo "❌ Failed to get response from Gemini"
    echo "Raw response: $RESPONSE"
    exit 1
}

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ Gemini consultation complete"