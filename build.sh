#!/bin/bash
# Build script for FactorioModManager
# Usage: ./build.sh [Debug|Release]

set -e

CONFIGURATION="${1:-Release}"

# Validate configuration
if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
    echo "Error: Invalid configuration. Use 'Debug' or 'Release'"
    exit 1
fi

echo "========================================"
echo "Building FactorioModManager"
echo "Configuration: $CONFIGURATION"
echo "========================================"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed or not in PATH"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Using .NET SDK version: $DOTNET_VERSION"

# Restore dependencies
echo ""
echo "Restoring dependencies..."
dotnet restore

# Build the solution
echo ""
echo "Building solution..."
dotnet build --configuration "$CONFIGURATION" --no-restore

echo ""
echo "========================================"
echo "Build completed successfully!"
echo "Configuration: $CONFIGURATION"
echo "Output directory: UI/bin/$CONFIGURATION/net9.0/"
echo "========================================"
