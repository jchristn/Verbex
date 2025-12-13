@echo off
REM Clean script for Verbex.Server
REM Removes logs directory, verbex.json, and data directory

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

echo Clean complete.
