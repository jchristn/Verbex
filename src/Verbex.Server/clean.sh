#!/bin/bash
# Clean script for Verbex.Server
# Removes logs directory, verbex.json, data directory, and verbex.db

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

if [ -f "verbex.db" ]; then
    echo "Deleting verbex.db..."
    rm -f "verbex.db"
    rm -f "verbex.db-shm"
    rm -f "verbex.db-wal"
fi

echo "Clean complete."
