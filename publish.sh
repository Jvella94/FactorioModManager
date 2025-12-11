#!/bin/bash
# Publish script for FactorioModManager
# Usage: ./publish.sh [Platform] [Configuration] [OutputPath]
# Example: ./publish.sh linux-x64 Release ./publish

set -e

PLATFORM="${1:-linux-x64}"
CONFIGURATION="${2:-Release}"
OUTPUT_PATH="${3:-./publish}"

# Validate platform
case "$PLATFORM" in
    win-x64|linux-x64|osx-x64|osx-arm64)
        ;;
    *)
        echo "Error: Invalid platform. Use 'win-x64', 'linux-x64', 'osx-x64', or 'osx-arm64'"
        exit 1
        ;;
esac

# Validate configuration
if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
    echo "Error: Invalid configuration. Use 'Debug' or 'Release'"
    exit 1
fi

echo "========================================"
echo "Publishing FactorioModManager"
echo "Platform: $PLATFORM"
echo "Configuration: $CONFIGURATION"
echo "Output Path: $OUTPUT_PATH"
echo "========================================"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed or not in PATH"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Using .NET SDK version: $DOTNET_VERSION"

# Create output directory
FULL_OUTPUT_PATH="$OUTPUT_PATH/$PLATFORM"
if [ -d "$FULL_OUTPUT_PATH" ]; then
    echo "Cleaning existing output directory: $FULL_OUTPUT_PATH"
    rm -rf "$FULL_OUTPUT_PATH"
fi

# Publish the application
echo ""
echo "Publishing application..."
dotnet publish UI/FactorioModManager.csproj \
    --configuration "$CONFIGURATION" \
    --runtime "$PLATFORM" \
    --self-contained true \
    --output "$FULL_OUTPUT_PATH"

echo ""
echo "========================================"
echo "Publish completed successfully!"
echo "Platform: $PLATFORM"
echo "Configuration: $CONFIGURATION"
echo "Output directory: $FULL_OUTPUT_PATH"
echo "========================================"

# Create archive
echo ""
read -p "Create archive? (y/n) [default: n]: " CREATE_ARCHIVE
if [[ "$CREATE_ARCHIVE" == "y" || "$CREATE_ARCHIVE" == "Y" ]]; then
    ARCHIVE_NAME="FactorioModManager-$PLATFORM"
    
    if [[ "$PLATFORM" == "win-x64" ]]; then
        ARCHIVE_PATH="$OUTPUT_PATH/$ARCHIVE_NAME.zip"
        echo "Creating ZIP archive: $ARCHIVE_PATH"
        cd "$FULL_OUTPUT_PATH" || exit 1
        zip -r "../$ARCHIVE_NAME.zip" ./* || { echo "Error: Failed to create ZIP archive"; cd - > /dev/null; exit 1; }
        cd - > /dev/null || exit 1
    else
        ARCHIVE_PATH="$OUTPUT_PATH/$ARCHIVE_NAME.tar.gz"
        echo "Creating TAR.GZ archive: $ARCHIVE_PATH"
        tar -czf "$ARCHIVE_PATH" -C "$FULL_OUTPUT_PATH" . || { echo "Error: Failed to create TAR.GZ archive"; exit 1; }
    fi
    
    if [ -f "$ARCHIVE_PATH" ]; then
        echo "Archive created successfully: $ARCHIVE_PATH"
    else
        echo "Error: Archive file was not created"
        exit 1
    fi
fi
