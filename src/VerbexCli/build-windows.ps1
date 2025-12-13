#!/usr/bin/env pwsh
# Build script for Windows platforms (x64 and ARM64)

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Building VerbexCli for Windows" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = $PSScriptRoot
$rootOutputDir = Join-Path $projectDir $OutputDir
$platforms = @("win-x64", "win-arm64")

# Create output directory
if (!(Test-Path $rootOutputDir)) {
    New-Item -ItemType Directory -Path $rootOutputDir | Out-Null
}

foreach ($platform in $platforms) {
    Write-Host "Building for $platform..." -ForegroundColor Yellow

    $outputPath = Join-Path $rootOutputDir $platform

    # Clean previous build
    if (Test-Path $outputPath) {
        Remove-Item -Path $outputPath -Recurse -Force
    }

    # Build command
    $buildArgs = @(
        "publish",
        "-c", $Configuration,
        "-r", $platform,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:PublishReadyToRun=true",
        "-p:EnableCompressionInSingleFile=true",
        "-o", $outputPath
    )

    & dotnet @buildArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Successfully built $platform" -ForegroundColor Green

        # Show output file info
        $exePath = Join-Path $outputPath "vbx.exe"
        if (Test-Path $exePath) {
            $fileInfo = Get-Item $exePath
            $sizeInMB = [math]::Round($fileInfo.Length / 1MB, 2)
            Write-Host "  Output: $exePath ($sizeInMB MB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "✗ Failed to build $platform" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
}

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Windows build complete!" -ForegroundColor Green
Write-Host "Output directory: $rootOutputDir" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan