#!/bin/bash
set -e

if [ -z "$1" ]; then
    echo "Error: Image tag is required."
    echo "Usage: $0 <tag>"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

bash ./build-dashboard.sh "$1"
bash ./build-server.sh "$1"
