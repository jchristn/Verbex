@echo off
if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)
docker buildx build --platform linux/amd64,linux/arm64/v8 -t jchristn/verbex-dashboard:%~1 --push .
