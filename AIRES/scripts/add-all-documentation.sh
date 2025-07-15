#!/bin/bash
# Add missing documentation to fix SA1611 and SA1616

echo "=== Adding missing documentation ==="

# Find all methods that need parameter documentation
echo "Files with missing parameter documentation (SA1611):"
dotnet build --no-restore 2>&1 | grep "SA1611" | cut -d'(' -f1 | sort -u

# Find all methods that need return documentation
echo ""
echo "Files with missing return documentation (SA1616):"
dotnet build --no-restore 2>&1 | grep "SA1616" | cut -d'(' -f1 | sort -u

# Let's analyze which files need the most documentation
echo ""
echo "Documentation errors by file:"
dotnet build --no-restore 2>&1 | grep -E "SA1611|SA1616" | cut -d'(' -f1 | sort | uniq -c | sort -nr

echo ""
echo "Total documentation errors: $(dotnet build --no-restore 2>&1 | grep -E 'SA1611|SA1616' | wc -l)"

echo ""
echo "To fix these, we need to:"
echo "1. Add <param> tags for all method parameters (SA1611)"
echo "2. Add descriptive text in <returns> tags (SA1616)"
echo "3. Focus on TestHttpMessageHandler.cs and TestCompositionRoot.cs first as they have the most errors"