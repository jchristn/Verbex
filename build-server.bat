@echo off
if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --provenance=false --sbom=false -t jchristn77/verbex-server:%~1 -t jchristn77/verbex-server:latest --push -f src/Verbex.Server/Dockerfile src
