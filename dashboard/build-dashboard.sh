#!/bin/bash
if [ -z "$1" ]; then
    echo "Error: Image tag is required."
    echo "Usage: $0 <tag>"
    exit 1
fi
docker buildx build --platform linux/amd64,linux/arm64/v8 -t jchristn/verbex-dashboard:$1 --push .
