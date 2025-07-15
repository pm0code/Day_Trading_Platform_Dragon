#!/bin/bash
# Script to run AIRES Health API

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
WEBAPI_DIR="$PROJECT_ROOT/src/AIRES.WebAPI"

echo "Starting AIRES Health API..."
echo "Project root: $PROJECT_ROOT"
echo "WebAPI directory: $WEBAPI_DIR"

# Check if project directory exists
if [ ! -d "$WEBAPI_DIR" ]; then
    echo "Error: WebAPI directory not found at $WEBAPI_DIR"
    exit 1
fi

# Change to WebAPI directory
cd "$WEBAPI_DIR"

# Copy aires.ini if it exists in the parent directories
if [ -f "$PROJECT_ROOT/aires.ini" ]; then
    echo "Copying aires.ini from project root..."
    cp "$PROJECT_ROOT/aires.ini" .
elif [ -f "$PROJECT_ROOT/../aires.ini" ]; then
    echo "Copying aires.ini from parent directory..."
    cp "$PROJECT_ROOT/../aires.ini" .
fi

# Run the API
echo "Starting health monitoring API on http://localhost:5000 and https://localhost:5001"
echo "Swagger UI will be available at http://localhost:5000/swagger"
echo "Health endpoint: http://localhost:5000/api/health"
echo ""
echo "Press Ctrl+C to stop..."

# Set environment variable for development
export ASPNETCORE_ENVIRONMENT=Development

# Run the API
dotnet run