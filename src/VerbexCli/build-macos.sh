#!/usr/bin/env bash
# Build script for macOS platforms (x64 and ARM64)

set -e

CONFIGURATION="${1:-Release}"
OUTPUT_DIR="${2:-artifacts}"

echo "================================"
echo "Building VerbexCli for macOS"
echo "================================"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_OUTPUT_DIR="$SCRIPT_DIR/$OUTPUT_DIR"
PLATFORMS=("osx-x64" "osx-arm64")

# Create output directory
mkdir -p "$ROOT_OUTPUT_DIR"

for PLATFORM in "${PLATFORMS[@]}"; do
    echo "Building for $PLATFORM..."

    OUTPUT_PATH="$ROOT_OUTPUT_DIR/$PLATFORM"

    # Clean previous build
    if [ -d "$OUTPUT_PATH" ]; then
        rm -rf "$OUTPUT_PATH"
    fi

    # Build command
    dotnet publish \
        -c "$CONFIGURATION" \
        -r "$PLATFORM" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true \
        -p:EnableCompressionInSingleFile=true \
        -o "$OUTPUT_PATH"

    if [ $? -eq 0 ]; then
        echo "✓ Successfully built $PLATFORM"

        # Show output file info
        EXE_PATH="$OUTPUT_PATH/vbx"
        if [ -f "$EXE_PATH" ]; then
            SIZE=$(ls -lh "$EXE_PATH" | awk '{print $5}')
            echo "  Output: $EXE_PATH ($SIZE)"
            # Make executable
            chmod +x "$EXE_PATH"
        fi
    else
        echo "✗ Failed to build $PLATFORM"
        exit 1
    fi

    echo ""
done

# Optional: Create universal binary if both builds succeeded
OSX_X64_PATH="$ROOT_OUTPUT_DIR/osx-x64/vbx"
OSX_ARM64_PATH="$ROOT_OUTPUT_DIR/osx-arm64/vbx"
UNIVERSAL_PATH="$ROOT_OUTPUT_DIR/osx-universal/vbx"

if [ -f "$OSX_X64_PATH" ] && [ -f "$OSX_ARM64_PATH" ]; then
    echo "Creating universal binary..."
    mkdir -p "$ROOT_OUTPUT_DIR/osx-universal"

    if command -v lipo &> /dev/null; then
        lipo -create "$OSX_X64_PATH" "$OSX_ARM64_PATH" -output "$UNIVERSAL_PATH"
        chmod +x "$UNIVERSAL_PATH"
        SIZE=$(ls -lh "$UNIVERSAL_PATH" | awk '{print $5}')
        echo "✓ Universal binary created: $UNIVERSAL_PATH ($SIZE)"
    else
        echo "⚠ lipo not available, skipping universal binary creation"
    fi
    echo ""
fi

echo "================================"
echo "macOS build complete!"
echo "Output directory: $ROOT_OUTPUT_DIR"
echo "================================"