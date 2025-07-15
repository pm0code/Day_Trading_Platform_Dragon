#!/bin/bash
# Fix member ordering in TestHttpMessageHandler and TestCompositionRoot

echo "=== Fixing member ordering (SA1202) ==="

# For TestHttpMessageHandler, we need to move public methods before protected SendAsync
# The structure should be:
# 1. Private fields
# 2. Public properties
# 3. Public methods
# 4. Protected methods
# 5. Private methods

# For TestCompositionRoot, we need to move CreateDefaultTestServiceProvider before private methods

# Since the files have been heavily modified, let's check the current state and fix manually
echo "Current member ordering issues:"
dotnet build --no-restore 2>&1 | grep "SA1202"

echo ""
echo "This requires manual reordering of members. The correct order is:"
echo "1. Fields (private, then protected, then public)"
echo "2. Constructors"
echo "3. Properties (public, then protected, then private)"
echo "4. Methods (public, then protected, then private)"
echo ""
echo "For TestHttpMessageHandler:"
echo "- Move ClearRecordedRequests, VerifyRequest, GetRequestCount, GetLastRequest BEFORE SendAsync"
echo ""
echo "For TestCompositionRoot:"
echo "- Move CreateDefaultTestServiceProvider BEFORE all private methods"