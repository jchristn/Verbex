#!/usr/bin/env pwsh
# Master build script for all platforms (Windows, macOS, Linux - x64 and ARM64)

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building VerbexCli for All Platforms" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = $PSScriptRoot
$rootOutputDir = Join-Path $projectDir $OutputDir
$platforms = @(
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "osx-x64",
    "osx-arm64"
)

# Create output directory
if (!(Test-Path $rootOutputDir)) {
    New-Item -ItemType Directory -Path $rootOutputDir | Out-Null
}

$successfulBuilds = @()
$failedBuilds = @()

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

    try {
        & dotnet @buildArgs 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Successfully built $platform" -ForegroundColor Green
            $successfulBuilds += $platform

            # Show output file info
            $exeName = if ($platform -like "win-*") { "vbx.exe" } else { "vbx" }
            $exePath = Join-Path $outputPath $exeName
            if (Test-Path $exePath) {
                $fileInfo = Get-Item $exePath
                $sizeInMB = [math]::Round($fileInfo.Length / 1MB, 2)
                Write-Host "  Output: $exePath ($sizeInMB MB)" -ForegroundColor Gray
            }
        } else {
            throw "Build failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Host "✗ Failed to build $platform" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        $failedBuilds += $platform
    }

    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Successful builds: $($successfulBuilds.Count)/$($platforms.Count)" -ForegroundColor Green
foreach ($platform in $successfulBuilds) {
    Write-Host "  ✓ $platform" -ForegroundColor Green
}

if ($failedBuilds.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed builds: $($failedBuilds.Count)/$($platforms.Count)" -ForegroundColor Red
    foreach ($platform in $failedBuilds) {
        Write-Host "  ✗ $platform" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Output directory: $rootOutputDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Exit with error if any builds failed
if ($failedBuilds.Count -gt 0) {
    exit 1
}