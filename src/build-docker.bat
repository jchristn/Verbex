@echo off
if "%1"=="" (
    echo Usage: build-docker.bat ^<image-tag^>
    echo Example: build-docker.bat verbex:latest
    exit /b 1
)

echo Building Verbex Docker Image...
echo ================================

REM Check if Docker is running
docker version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Docker is not running or not installed.
    echo Please start Docker Desktop and try again.
    exit /b 1
)

REM Build the Docker image
echo Building %1...
docker build -t %1 -f Verbex.Server/Dockerfile .

if %errorlevel% equ 0 (
    echo.
    echo ✅ Docker image built successfully!
    echo.
    echo Image: %1
    echo.
    echo To run the container:
    echo   docker run -p 8080:8080 %1
    echo.
    echo To run with persistent storage:
    echo   docker run -p 8080:8080 -v /host/data:/app/data %1
) else (
    echo.
    echo ❌ Docker build failed!
    echo Please check the error messages above.
    exit /b 1
)

pause