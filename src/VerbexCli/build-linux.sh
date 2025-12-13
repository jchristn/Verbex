#!/usr/bin/env bash
# Build script for Linux platforms (x64 and ARM64)

set -e

CONFIGURATION="${1:-Release}"
OUTPUT_DIR="${2:-artifacts}"

echo "================================"
echo "Building VerbexCli for Linux"
echo "================================"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_OUTPUT_DIR="$SCRIPT_DIR/$OUTPUT_DIR"
PLATFORMS=("linux-x64" "linux-arm64")

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

echo "================================"
echo "Linux build complete!"
echo "Output directory: $ROOT_OUTPUT_DIR"
echo "================================"