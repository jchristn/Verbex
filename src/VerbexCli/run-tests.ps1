# VerbexCli Test Runner
# Simple script to run the comprehensive test suite

param(
    [string]$TestType = "batch"  # "batch" or "powershell"
)

Write-Host "VerbexCli Test Runner" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host ""

# Build the project first
Write-Host "Building VerbexCli..." -ForegroundColor Yellow
try {
    dotnet build VerbexCli.csproj -c Debug > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Build error: $_" -ForegroundColor Red
    exit 1
}

# Change to the executable directory
$exeDir = "bin\Debug\net8.0\win-x64"
if (-not (Test-Path $exeDir)) {
    Write-Host "✗ Executable directory not found: $exeDir" -ForegroundColor Red
    exit 1
}

Set-Location $exeDir

# Copy the test script
$testScript = "test.bat"
$sourcePath = "..\..\..\..\test.bat"
if (Test-Path $sourcePath) {
    Copy-Item $sourcePath . -Force
    Write-Host "✓ Test script copied" -ForegroundColor Green
} else {
    Write-Host "✗ Test script not found: $sourcePath" -ForegroundColor Red
    exit 1
}

# Run the tests
Write-Host ""
Write-Host "Running comprehensive test suite..." -ForegroundColor Yellow
Write-Host "This will take several minutes to complete all 20 test sections." -ForegroundColor Yellow
Write-Host ""

try {
    if ($TestType -eq "powershell") {
        # Run via PowerShell for better compatibility
        & ".\test.bat"
    } else {
        # Run directly
        & ".\test.bat"
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ All tests completed successfully!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "✗ Some tests failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host ""
    Write-Host "✗ Test execution error: $_" -ForegroundColor Red
    exit 1
} finally {
    # Return to original directory
    Set-Location ..\..\..\..
}