# ================================================
# VerbexCli COMPREHENSIVE Test Suite - PowerShell Version
# ================================================
# This script EXHAUSTIVELY tests every CLI command, option, and feature
# It includes proper pass/fail tracking and detailed reporting

param(
    [string]$VbxExe = "vbx.exe"
)

# Error handling - but don't stop on command failures since we're testing them
$ErrorActionPreference = "Continue"

# Test tracking variables
$Global:TotalTests = 0
$Global:PassedTests = 0
$Global:FailedTests = 0
$Global:TestResults = @()

# Find VBX executable
$VbxPath = $VbxExe
if (Test-Path "bin\Release\net8.0\win-x64\vbx.exe") { $VbxPath = "bin\Release\net8.0\win-x64\vbx.exe" }
elseif (Test-Path "bin\Release\net8.0\win-x64\publish\vbx.exe") { $VbxPath = "bin\Release\net8.0\win-x64\publish\vbx.exe" }
elseif (Test-Path "bin\Debug\net8.0\win-x64\vbx.exe") { $VbxPath = "bin\Debug\net8.0\win-x64\vbx.exe" }
elseif (Test-Path ".\vbx.exe") { $VbxPath = ".\vbx.exe" }

Write-Host ""
Write-Host "================================================"
Write-Host "VerbexCli COMPREHENSIVE Test Suite"
Write-Host "================================================"
Write-Host "Using executable: $VbxPath"
Write-Host "Starting exhaustive testing of ALL CLI features..."
Write-Host ""

# Verify VBX executable exists
if (-not (Test-Path $VbxPath)) {
    Write-Host "ERROR: VBX executable not found at $VbxPath" -ForegroundColor Red
    Write-Host "Please build the project first with: dotnet build VerbexCli"
    exit 1
}

# Clean up any existing test state
Write-Host "Cleaning up previous test state..."
$vbxDir = "$env:USERPROFILE\.vbx"
if (Test-Path $vbxDir) {
    Remove-Item $vbxDir -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item "temp_test_doc.txt" -ErrorAction SilentlyContinue
Remove-Item "*.json" -ErrorAction SilentlyContinue
Write-Host "Test environment cleaned"

# Function to run a test with proper output capture and result checking
function Test-VbxCommand {
    param(
        [string]$TestDescription,
        [string]$Command,
        [int]$ExpectedExitCode = 0
    )

    $Global:TotalTests++

    Write-Host ""
    Write-Host "[$Global:TotalTests] $TestDescription..."
    Write-Host "Command: $Command"
    Write-Host "----------------------------------------"

    try {
        # Execute command and capture output
        $output = cmd /c "$Command 2>&1"
        $actualExitCode = $LASTEXITCODE

        # Display the output
        if ($output) {
            $output | ForEach-Object { Write-Host $_ }
        }

        Write-Host "----------------------------------------"

        # Check result
        if ($actualExitCode -eq $ExpectedExitCode) {
            Write-Host "Result: PASS (exit code $actualExitCode)" -ForegroundColor Green
            $Global:PassedTests++
            $Global:TestResults += [PSCustomObject]@{
                Test = "$Global:TotalTests. $TestDescription"
                Result = "PASS"
                ExitCode = $actualExitCode
            }
        } else {
            Write-Host "Result: FAIL (Expected exit code $ExpectedExitCode, got $actualExitCode)" -ForegroundColor Red
            $Global:FailedTests++
            $Global:TestResults += [PSCustomObject]@{
                Test = "$Global:TotalTests. $TestDescription"
                Result = "FAIL"
                ExitCode = "$actualExitCode (expected $ExpectedExitCode)"
            }
        }
    }
    catch {
        Write-Host "----------------------------------------"
        Write-Host "Result: FAIL (Exception: $($_.Exception.Message))" -ForegroundColor Red
        $Global:FailedTests++
        $Global:TestResults += [PSCustomObject]@{
            Test = "$Global:TotalTests. $TestDescription"
            Result = "FAIL"
            ExitCode = "Exception: $($_.Exception.Message)"
        }
    }

    Write-Host ""
}

Write-Host ""
Write-Host "========================================"
Write-Host "BASIC FUNCTIONALITY TESTS"
Write-Host "========================================"

Test-VbxCommand "Version command" "$VbxPath --version" 0
Test-VbxCommand "Help command" "$VbxPath --help" 0
Test-VbxCommand "Root help" "$VbxPath -h" 0
Test-VbxCommand "Short help" "$VbxPath -?" 0

Write-Host ""
Write-Host "========================================"
Write-Host "GLOBAL OPTIONS TESTS"
Write-Host "========================================"

Test-VbxCommand "Verbose flag" "$VbxPath --verbose index ls" 0
Test-VbxCommand "Short verbose flag" "$VbxPath -v index ls" 0
Test-VbxCommand "Quiet flag" "$VbxPath --quiet index ls" 0
Test-VbxCommand "Short quiet flag" "$VbxPath -q index ls" 0
Test-VbxCommand "Debug flag" "$VbxPath --debug index ls" 0
Test-VbxCommand "No color flag" "$VbxPath --no-color index ls" 0

Write-Host ""
Write-Host "========================================"
Write-Host "CONFIG COMMAND TESTS"
Write-Host "========================================"

Test-VbxCommand "Config show" "$VbxPath config show" 0
Test-VbxCommand "Config help" "$VbxPath config --help" 0
Test-VbxCommand "Config set output json" "$VbxPath config set output json" 0
Test-VbxCommand "Config set output csv" "$VbxPath config set output csv" 0
Test-VbxCommand "Config set output yaml" "$VbxPath config set output yaml" 0
Test-VbxCommand "Config set output table" "$VbxPath config set output table" 0
Test-VbxCommand "Config set color true" "$VbxPath config set color true" 0
Test-VbxCommand "Config set color false" "$VbxPath config set color false" 0
Test-VbxCommand "Config set verbose true" "$VbxPath config set verbose true" 0
Test-VbxCommand "Config set verbose false" "$VbxPath config set verbose false" 0
Test-VbxCommand "Config unset output" "$VbxPath config unset output" 0
Test-VbxCommand "Config unset color" "$VbxPath config unset color" 0
Test-VbxCommand "Config unset verbose" "$VbxPath config unset verbose" 0
Test-VbxCommand "Config show JSON output" "$VbxPath config show --output json" 0
Test-VbxCommand "Config show CSV output" "$VbxPath config show --output csv" 0
Test-VbxCommand "Config show YAML output" "$VbxPath config show --output yaml" 0
Test-VbxCommand "Config show Table output" "$VbxPath config show --output table" 0

Write-Host ""
Write-Host "========================================"
Write-Host "INDEX COMMAND TESTS"
Write-Host "========================================"

# Clean up any previous test indices
Write-Host "Cleaning up previous test indices..."
cmd /c "$VbxPath index delete test-memory --force" 2>$null
Write-Host "test-memory index didn't exist"
cmd /c "$VbxPath index delete test-disk --force" 2>$null
Write-Host "test-disk index didn't exist"
cmd /c "$VbxPath index delete test-hybrid --force" 2>$null
Write-Host "test-hybrid index didn't exist"
cmd /c "$VbxPath index delete test-advanced --force" 2>$null
Write-Host "test-advanced index didn't exist"

Test-VbxCommand "Index help" "$VbxPath index --help" 0
Test-VbxCommand "Index list empty" "$VbxPath index ls" 0
Test-VbxCommand "Index list JSON" "$VbxPath index ls --output json" 0
Test-VbxCommand "Index list CSV" "$VbxPath index ls --output csv" 0
Test-VbxCommand "Index list YAML" "$VbxPath index ls --output yaml" 0
Test-VbxCommand "Index list Table" "$VbxPath index ls --output table" 0

Test-VbxCommand "Index create memory" "$VbxPath index create test-memory --storage memory" 0
Test-VbxCommand "Index create disk" "$VbxPath index create test-disk --storage disk" 0
Test-VbxCommand "Index create hybrid" "$VbxPath index create test-hybrid --storage hybrid" 0
Test-VbxCommand "Index create advanced" "$VbxPath index create test-advanced --storage hybrid --lemmatizer --stopwords --min-length 2 --max-length 50" 0

Test-VbxCommand "Index list with indices" "$VbxPath index ls" 0
Test-VbxCommand "Index use test-memory" "$VbxPath index use test-memory" 0
Test-VbxCommand "Index use test-disk" "$VbxPath index use test-disk" 0
Test-VbxCommand "Index use test-hybrid" "$VbxPath index use test-hybrid" 0
Test-VbxCommand "Index use test-advanced" "$VbxPath index use test-advanced" 0

Test-VbxCommand "Index info current" "$VbxPath index info" 0
Test-VbxCommand "Index info test-memory" "$VbxPath index info test-memory" 0
Test-VbxCommand "Index info test-disk" "$VbxPath index info test-disk" 0
Test-VbxCommand "Index info JSON" "$VbxPath index info test-memory --output json" 0
Test-VbxCommand "Index info CSV" "$VbxPath index info test-memory --output csv" 0
Test-VbxCommand "Index info YAML" "$VbxPath index info test-memory --output yaml" 0

Write-Host ""
Write-Host "========================================"
Write-Host "DOCUMENT COMMAND TESTS"
Write-Host "========================================"

# Create test content variables
$DOC1_CONTENT = "This is a comprehensive test document about artificial intelligence and machine learning."
$DOC2_CONTENT = "Advanced search capabilities using inverted indices provide fast full-text retrieval."
$DOC3_CONTENT = "Natural language processing techniques enable intelligent text analysis."
$DOC4_CONTENT = "Database indexing and query optimization are fundamental for performance."

Test-VbxCommand "Doc help" "$VbxPath doc --help" 0
Test-VbxCommand "Doc add help" "$VbxPath doc add --help" 0
Test-VbxCommand "Doc add-file help" "$VbxPath doc add-file --help" 0
Test-VbxCommand "Doc remove help" "$VbxPath doc remove --help" 0
Test-VbxCommand "Doc list help" "$VbxPath doc ls --help" 0
Test-VbxCommand "Doc clear help" "$VbxPath doc clear --help" 0

# Use test-memory index as active (ensure it's set after previous index switching tests)
Test-VbxCommand "Set active index to test-memory" "$VbxPath index use test-memory" 0

Test-VbxCommand "Doc list empty (active index)" "$VbxPath doc ls" 0
Test-VbxCommand "Doc add document 1 (active index)" "$VbxPath doc add doc1 `"$DOC1_CONTENT`"" 0
Test-VbxCommand "Doc add document 2 (active index)" "$VbxPath doc add doc2 `"$DOC2_CONTENT`"" 0
Test-VbxCommand "Doc add document 3 (active index)" "$VbxPath doc add doc3 `"$DOC3_CONTENT`"" 0
Test-VbxCommand "Doc add document 4 (active index)" "$VbxPath doc add doc4 `"$DOC4_CONTENT`"" 0

# Test file-based addition
Write-Host "Creating temporary test file..."
"Test file content for document addition" | Out-File -FilePath "temp_test_doc.txt" -Encoding UTF8
Test-VbxCommand "Doc add-file (active index)" "$VbxPath doc add-file doc5 temp_test_doc.txt" 0
Remove-Item "temp_test_doc.txt" -ErrorAction SilentlyContinue

Test-VbxCommand "Doc list with documents (active index)" "$VbxPath doc ls" 0
Test-VbxCommand "Doc list JSON (active index)" "$VbxPath doc ls --output json" 0
Test-VbxCommand "Doc list CSV (active index)" "$VbxPath doc ls --output csv" 0
Test-VbxCommand "Doc list YAML (active index)" "$VbxPath doc ls --output yaml" 0

# Add documents to other indices using --index flag
Test-VbxCommand "Doc add to disk index (explicit)" "$VbxPath doc add --index test-disk diskdoc1 `"Document in disk storage index`"" 0
Test-VbxCommand "Doc add to hybrid index (explicit)" "$VbxPath doc add --index test-hybrid hybriddoc1 `"Document in hybrid storage index`"" 0
Test-VbxCommand "Doc add to advanced index (explicit)" "$VbxPath doc add --index test-advanced advdoc1 `"Running algorithms efficiently requires optimized data structures`"" 0

Write-Host ""
Write-Host "========================================"
Write-Host "SEARCH COMMAND TESTS"
Write-Host "========================================"

Test-VbxCommand "Search help" "$VbxPath search --help" 0
Test-VbxCommand "Search OR logic (active index)" "$VbxPath search `"artificial intelligence`"" 0
Test-VbxCommand "Search AND logic (active index)" "$VbxPath search `"artificial intelligence`" --and" 0
Test-VbxCommand "Search with limit (active index)" "$VbxPath search `"test`" --limit 5" 0
Test-VbxCommand "Search with short limit (active index)" "$VbxPath search `"test`" -l 3" 0
Test-VbxCommand "Search JSON output (active index)" "$VbxPath search `"search`" --output json" 0
Test-VbxCommand "Search CSV output (active index)" "$VbxPath search `"search`" --output csv" 0
Test-VbxCommand "Search YAML output (active index)" "$VbxPath search `"search`" --output yaml" 0
Test-VbxCommand "Search Table output (active index)" "$VbxPath search `"search`" --output table" 0

Test-VbxCommand "Search single term (active index)" "$VbxPath search `"artificial`"" 0
Test-VbxCommand "Search multiple terms OR (active index)" "$VbxPath search `"search capabilities`"" 0
Test-VbxCommand "Search multiple terms AND (active index)" "$VbxPath search `"search capabilities`" --and" 0
Test-VbxCommand "Search nonexistent term (active index)" "$VbxPath search `"nonexistent`"" 0

Test-VbxCommand "Search disk index (explicit)" "$VbxPath search `"document`" --index test-disk" 0
Test-VbxCommand "Search hybrid index (explicit)" "$VbxPath search `"document`" --index test-hybrid" 0
Test-VbxCommand "Search advanced index (explicit)" "$VbxPath search `"algorithms`" --index test-advanced" 0

Write-Host ""
Write-Host "========================================"
Write-Host "STATS COMMAND TESTS"
Write-Host "========================================"

Test-VbxCommand "Stats help" "$VbxPath stats --help" 0
Test-VbxCommand "Stats general (active index)" "$VbxPath stats" 0
Test-VbxCommand "Stats cache (active index)" "$VbxPath stats --cache" 0
Test-VbxCommand "Stats cache short (active index)" "$VbxPath stats -c" 0
Test-VbxCommand "Stats term specific (active index)" "$VbxPath stats --term artificial" 0
Test-VbxCommand "Stats term short (active index)" "$VbxPath stats -t intelligence" 0
Test-VbxCommand "Stats JSON output (active index)" "$VbxPath stats --output json" 0
Test-VbxCommand "Stats CSV output (active index)" "$VbxPath stats --output csv" 0
Test-VbxCommand "Stats YAML output (active index)" "$VbxPath stats --output yaml" 0

Test-VbxCommand "Stats disk index (explicit)" "$VbxPath stats --index test-disk" 0
Test-VbxCommand "Stats hybrid index (explicit)" "$VbxPath stats --index test-hybrid" 0
Test-VbxCommand "Stats advanced index (explicit)" "$VbxPath stats --index test-advanced" 0

Write-Host ""
Write-Host "========================================"
Write-Host "MAINTENANCE COMMAND TESTS"
Write-Host "========================================"

Test-VbxCommand "Maint help" "$VbxPath maint --help" 0
Test-VbxCommand "Maint flush help" "$VbxPath maint flush --help" 0
Test-VbxCommand "Maint gc help" "$VbxPath maint gc --help" 0
Test-VbxCommand "Maint benchmark help" "$VbxPath maint benchmark --help" 0
Test-VbxCommand "Maint stress help" "$VbxPath maint stress --help" 0

Test-VbxCommand "Maint flush (active index)" "$VbxPath maint flush" 0
Test-VbxCommand "Maint flush disk (explicit)" "$VbxPath maint flush --index test-disk" 0
Test-VbxCommand "Maint flush hybrid (explicit)" "$VbxPath maint flush --index test-hybrid" 0

Test-VbxCommand "Maint GC (active index)" "$VbxPath maint gc" 0
Test-VbxCommand "Maint GC disk (explicit)" "$VbxPath maint gc --index test-disk" 0
Test-VbxCommand "Maint GC hybrid (explicit)" "$VbxPath maint gc --index test-hybrid" 0

Test-VbxCommand "Maint benchmark small (active index)" "$VbxPath maint benchmark --documents 10" 0
Test-VbxCommand "Maint benchmark short docs (active index)" "$VbxPath maint benchmark -d 5" 0
Test-VbxCommand "Maint benchmark JSON (active index)" "$VbxPath maint benchmark --documents 5 --output json" 0

Test-VbxCommand "Maint stress small (active index)" "$VbxPath maint stress --documents 50" 0
Test-VbxCommand "Maint stress short docs (active index)" "$VbxPath maint stress -d 25" 0

Write-Host ""
Write-Host "========================================"
Write-Host "INDEX EXPORT TESTS"
Write-Host "========================================"

Test-VbxCommand "Index export help" "$VbxPath index export --help" 0
Test-VbxCommand "Index export memory" "$VbxPath index export test-memory export-memory.json" 0
Test-VbxCommand "Index export disk" "$VbxPath index export test-disk export-disk.json" 0
Test-VbxCommand "Index export hybrid" "$VbxPath index export test-hybrid export-hybrid.json" 0
Test-VbxCommand "Index export advanced" "$VbxPath index export test-advanced export-advanced.json" 0

Write-Host ""
Write-Host "========================================"
Write-Host "DOCUMENT REMOVAL TESTS"
Write-Host "========================================"

# Ensure test-memory is active before removal tests (in case previous tests changed it)
Test-VbxCommand "Set active index to test-memory for removal tests" "$VbxPath index use test-memory" 0
Test-VbxCommand "Doc remove from active index" "$VbxPath doc remove doc1" 0
Test-VbxCommand "Doc remove from disk (explicit)" "$VbxPath doc remove --index test-disk diskdoc1" 0
Test-VbxCommand "Doc remove from hybrid (explicit)" "$VbxPath doc remove --index test-hybrid hybriddoc1" 0
Test-VbxCommand "Doc remove from advanced (explicit)" "$VbxPath doc remove --index test-advanced advdoc1" 0

Write-Host ""
Write-Host "========================================"
Write-Host "OUTPUT FORMAT COMBINATION TESTS"
Write-Host "========================================"

Test-VbxCommand "Verbose + JSON" "$VbxPath --verbose index ls --output json" 0
Test-VbxCommand "Quiet + CSV" "$VbxPath --quiet index ls --output csv" 0
Test-VbxCommand "Debug + YAML" "$VbxPath --debug index ls --output yaml" 0
Test-VbxCommand "No-color + Table" "$VbxPath --no-color index ls --output table" 0

Write-Host ""
Write-Host "========================================"
Write-Host "ERROR HANDLING TESTS"
Write-Host "========================================"

Test-VbxCommand "Invalid command" "$VbxPath invalidcommand" 1
Test-VbxCommand "Index create duplicate" "$VbxPath index create test-memory --storage memory" 1
Test-VbxCommand "Index use nonexistent" "$VbxPath index use nonexistent-index" 1
Test-VbxCommand "Index delete nonexistent" "$VbxPath index delete nonexistent-index --force" 1
Test-VbxCommand "Doc add to nonexistent index (explicit)" "$VbxPath doc add --index nonexistent-index testdoc `"content`"" 1
Test-VbxCommand "Doc remove nonexistent doc (active index)" "$VbxPath doc remove nonexistent-doc" 1
Test-VbxCommand "Search nonexistent index (explicit)" "$VbxPath search `"test`" --index nonexistent-index" 1
Test-VbxCommand "Stats nonexistent index (explicit)" "$VbxPath stats --index nonexistent-index" 1
Test-VbxCommand "Config set invalid key" "$VbxPath config set invalidkey value" 1
Test-VbxCommand "Config set invalid output format" "$VbxPath config set output invalidformat" 1

Write-Host ""
Write-Host "========================================"
Write-Host "CLEANUP TESTS"
Write-Host "========================================"

Test-VbxCommand "Doc clear with force (active index)" "$VbxPath doc clear --force" 0
Test-VbxCommand "Index delete memory" "$VbxPath index delete test-memory --force" 0
Test-VbxCommand "Index delete disk" "$VbxPath index delete test-disk --force" 0
Test-VbxCommand "Index delete hybrid" "$VbxPath index delete test-hybrid --force" 0
Test-VbxCommand "Index delete advanced" "$VbxPath index delete test-advanced --force" 0

Write-Host ""
Write-Host "Cleaning up test files..."
Remove-Item "export-*.json" -ErrorAction SilentlyContinue
Remove-Item "temp_test_doc.txt" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "================================================"
Write-Host "TEST SUMMARY"
Write-Host "================================================"
Write-Host ""
Write-Host "Total Tests Run: $Global:TotalTests"
Write-Host "Tests Passed:   $Global:PassedTests" -ForegroundColor Green
Write-Host "Tests Failed:   $Global:FailedTests" -ForegroundColor Red
Write-Host ""

# Display detailed results
Write-Host "DETAILED TEST RESULTS:"
Write-Host "======================"
foreach ($result in $Global:TestResults) {
    if ($result.Result -eq "PASS") {
        Write-Host "$($result.Test): PASS" -ForegroundColor Green
    } else {
        Write-Host "$($result.Test): FAIL ($($result.ExitCode))" -ForegroundColor Red
    }
}
Write-Host ""

if ($Global:FailedTests -eq 0) {
    Write-Host ""
    Write-Host "ALL TESTS PASSED" -ForegroundColor Green
    Write-Host ""
    exit 0
} else {
    Write-Host ""
    Write-Host "TESTS FAILED: $Global:FailedTests failures detected" -ForegroundColor Red
    Write-Host ""
    exit 1
}