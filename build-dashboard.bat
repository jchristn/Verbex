@echo off
if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --provenance=false --sbom=false -t jchristn77/verbex-dashboard:%~1 -t jchristn77/verbex-dashboard:latest --push dashboard
if errorlevel 1 exit /b %ERRORLEVEL%

REM Pull the just-pushed images into the local registry (registry pull, not a second cloud build)
docker pull jchristn77/verbex-dashboard:%~1
if errorlevel 1 exit /b %ERRORLEVEL%
docker pull jchristn77/verbex-dashboard:latest
if errorlevel 1 exit /b %ERRORLEVEL%
