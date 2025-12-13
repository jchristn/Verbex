#!/bin/bash
# Clean script for Verbex.Server
# Removes logs directory, verbex.json, and data directory

echo "Cleaning Verbex.Server..."

if [ -d "logs" ]; then
    echo "Deleting logs directory..."
    rm -rf "logs"
fi

if [ -f "verbex.json" ]; then
    echo "Deleting verbex.json..."
    rm -f "verbex.json"
fi

if [ -d "data" ]; then
    echo "Deleting data directory..."
    rm -rf "data"
fi

echo "Clean complete."
