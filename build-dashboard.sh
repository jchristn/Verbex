#!/bin/bash
if [ -z "$1" ]; then
    echo "Error: Image tag is required."
    echo "Usage: $0 <tag>"
    exit 1
fi
docker buildx build --platform linux/amd64,linux/arm64/v8 --provenance=false --sbom=false -t jchristn77/verbex-dashboard:$1 -t jchristn77/verbex-dashboard:latest --push dashboard
