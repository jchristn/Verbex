@echo off
REM Clean script for Verbex.Server
REM Removes logs directory, verbex.json, data directory, and verbex.db

echo Cleaning Verbex.Server...

if exist "logs" (
    echo Deleting logs directory...
    rmdir /s /q "logs"
)

if exist "verbex.json" (
    echo Deleting verbex.json...
    del /f /q "verbex.json"
)

if exist "data" (
    echo Deleting data directory...
    rmdir /s /q "data"
)

if exist "verbex.db" (
    echo Deleting verbex.db...
    del /f /q "verbex.db"
    del /f /q "verbex.db-shm"
    del /f /q "verbex.db-wal"
)

echo Clean complete.
