#!/bin/bash
set -e
if [ -z "$1" ]; then
    echo "Error: Image tag is required."
    echo "Usage: $0 <tag>"
    exit 1
fi
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --provenance=false --sbom=false -t jchristn77/verbex-dashboard:$1 -t jchristn77/verbex-dashboard:latest --push dashboard

# Pull the just-pushed images into the local registry (registry pull, not a second cloud build)
docker pull jchristn77/verbex-dashboard:$1
docker pull jchristn77/verbex-dashboard:latest
