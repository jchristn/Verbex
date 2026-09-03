@echo off
REM
REM update.bat - Update the Verbex Docker stack to the latest published images.
REM
REM Non-destructive: named volumes are preserved. For a destructive factory reset,
REM use factory\reset.bat instead.
REM
REM This script runs, in order:
REM   1. docker compose pull   - pull the latest published images
REM   2. docker compose down   - stop and remove the current containers
REM   3. docker compose up -d  - recreate the stack (detached)
REM   4. docker ps -a          - report final container status
REM

setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%"

echo ========================================
echo   Verbex Docker Update
echo ========================================
echo.

echo [1/4] Pulling latest images...
docker compose pull

echo.
echo [2/4] Stopping current stack...
docker compose down

echo.
echo [3/4] Starting stack (detached)...
docker compose up -d

echo.
echo [4/4] Container status:
docker ps -a

popd
endlocal
