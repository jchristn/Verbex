#!/bin/bash
if [ -z "$1" ]; then
    echo "Error: Image tag is required."
    echo "Usage: $0 <tag>"
    exit 1
fi
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --provenance=false --sbom=false -t jchristn77/verbex-server:$1 -t jchristn77/verbex-server:latest --push -f src/Verbex.Server/Dockerfile src
