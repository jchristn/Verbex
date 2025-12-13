#!/usr/bin/env bash
# Master build script for all platforms (Windows, macOS, Linux - x64 and ARM64)

set -e

CONFIGURATION="${1:-Release}"
OUTPUT_DIR="${2:-artifacts}"

echo "========================================"
echo "Building VerbexCli for All Platforms"
echo "========================================"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_OUTPUT_DIR="$SCRIPT_DIR/$OUTPUT_DIR"
PLATFORMS=("win-x64" "win-arm64" "linux-x64" "linux-arm64" "osx-x64" "osx-arm64")

# Create output directory
mkdir -p "$ROOT_OUTPUT_DIR"

SUCCESSFUL_BUILDS=()
FAILED_BUILDS=()

for PLATFORM in "${PLATFORMS[@]}"; do
    echo "Building for $PLATFORM..."

    OUTPUT_PATH="$ROOT_OUTPUT_DIR/$PLATFORM"

    # Clean previous build
    if [ -d "$OUTPUT_PATH" ]; then
        rm -rf "$OUTPUT_PATH"
    fi

    # Build command
    if dotnet publish \
        -c "$CONFIGURATION" \
        -r "$PLATFORM" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true \
        -p:EnableCompressionInSingleFile=true \
        -o "$OUTPUT_PATH" > /dev/null 2>&1; then

        echo "✓ Successfully built $PLATFORM"
        SUCCESSFUL_BUILDS+=("$PLATFORM")

        # Show output file info
        if [[ "$PLATFORM" == win-* ]]; then
            EXE_NAME="vbx.exe"
        else
            EXE_NAME="vbx"
        fi

        EXE_PATH="$OUTPUT_PATH/$EXE_NAME"
        if [ -f "$EXE_PATH" ]; then
            SIZE=$(ls -lh "$EXE_PATH" | awk '{print $5}')
            echo "  Output: $EXE_PATH ($SIZE)"
            # Make executable for non-Windows platforms
            if [[ "$PLATFORM" != win-* ]]; then
                chmod +x "$EXE_PATH"
            fi
        fi
    else
        echo "✗ Failed to build $PLATFORM"
        FAILED_BUILDS+=("$PLATFORM")
    fi

    echo ""
done

# Summary
echo "========================================"
echo "Build Summary"
echo "========================================"
echo ""
echo "Successful builds: ${#SUCCESSFUL_BUILDS[@]}/${#PLATFORMS[@]}"
for PLATFORM in "${SUCCESSFUL_BUILDS[@]}"; do
    echo "  ✓ $PLATFORM"
done

if [ ${#FAILED_BUILDS[@]} -gt 0 ]; then
    echo ""
    echo "Failed builds: ${#FAILED_BUILDS[@]}/${#PLATFORMS[@]}"
    for PLATFORM in "${FAILED_BUILDS[@]}"; do
        echo "  ✗ $PLATFORM"
    done
fi

echo ""
echo "Output directory: $ROOT_OUTPUT_DIR"
echo "========================================"

# Exit with error if any builds failed
if [ ${#FAILED_BUILDS[@]} -gt 0 ]; then
    exit 1
fi