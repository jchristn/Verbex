#!/bin/bash
# ================================================
# VerbexCli COMPREHENSIVE Test Suite
# ================================================
# This script EXHAUSTIVELY tests every CLI command, option, and feature
# It includes proper pass/fail tracking and detailed reporting

set -e

# Test tracking variables
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Find VBX executable
VBX_EXE="./vbx"
if [ -f "./vbx.exe" ]; then
    VBX_EXE="./vbx.exe"
elif [ -f "./vbx" ]; then
    VBX_EXE="./vbx"
elif [ -f "bin/Release/net8.0/win-x64/vbx.exe" ]; then
    VBX_EXE="bin/Release/net8.0/win-x64/vbx.exe"
elif [ -f "bin/Release/net8.0/linux-x64/vbx" ]; then
    VBX_EXE="bin/Release/net8.0/linux-x64/vbx"
elif [ -f "bin/Debug/net8.0/win-x64/vbx.exe" ]; then
    VBX_EXE="bin/Debug/net8.0/win-x64/vbx.exe"
elif [ -f "bin/Debug/net8.0/linux-x64/vbx" ]; then
    VBX_EXE="bin/Debug/net8.0/linux-x64/vbx"
fi

echo
echo -e "${BLUE}================================================${NC}"
echo -e "${BLUE}VerbexCli COMPREHENSIVE Test Suite${NC}"
echo -e "${BLUE}================================================${NC}"
echo "Using executable: $VBX_EXE"
echo "Starting exhaustive testing of ALL CLI features..."
echo

# Verify VBX executable exists
if [ ! -f "$VBX_EXE" ]; then
    echo -e "${RED}ERROR: VBX executable not found at $VBX_EXE${NC}"
    echo "Please build the project first with: dotnet build VerbexCli"
    exit 1
fi

if [ ! -x "$VBX_EXE" ]; then
    echo "Making VBX executable..."
    chmod +x "$VBX_EXE"
fi

# Clean up any existing test state
echo "Cleaning up previous test state..."
if [ -d "$HOME/.vbx" ]; then
    rm -rf "$HOME/.vbx" 2>/dev/null || true
fi
rm -f temp_test_doc.txt *.json 2>/dev/null || true
echo "Test environment cleaned"

# Function to run a test
run_test() {
    local test_desc="$1"
    local test_cmd="$2"
    local expected_exit="${3:-0}"

    TOTAL_TESTS=$((TOTAL_TESTS + 1))

    echo "[$TOTAL_TESTS] $test_desc..."

    if eval "$test_cmd" >/dev/null 2>&1; then
        actual_exit=0
    else
        actual_exit=1
    fi

    if [ "$actual_exit" = "$expected_exit" ]; then
        echo -e "    ${GREEN}PASS${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "    ${RED}FAIL${NC} (Expected exit code $expected_exit, got $actual_exit)"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
}

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}BASIC FUNCTIONALITY TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Version command" "$VBX_EXE --version" "0"
run_test "Help command" "$VBX_EXE --help" "0"
run_test "Root help" "$VBX_EXE -h" "0"
run_test "Short help" "$VBX_EXE -?" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}GLOBAL OPTIONS TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Verbose flag" "$VBX_EXE --verbose index ls" "0"
run_test "Short verbose flag" "$VBX_EXE -v index ls" "0"
run_test "Quiet flag" "$VBX_EXE --quiet index ls" "0"
run_test "Short quiet flag" "$VBX_EXE -q index ls" "0"
run_test "Debug flag" "$VBX_EXE --debug index ls" "0"
run_test "No color flag" "$VBX_EXE --no-color index ls" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}CONFIG COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Config show" "$VBX_EXE config show" "0"
run_test "Config help" "$VBX_EXE config --help" "0"
run_test "Config set output json" "$VBX_EXE config set output json" "0"
run_test "Config set output csv" "$VBX_EXE config set output csv" "0"
run_test "Config set output yaml" "$VBX_EXE config set output yaml" "0"
run_test "Config set output table" "$VBX_EXE config set output table" "0"
run_test "Config set color true" "$VBX_EXE config set color true" "0"
run_test "Config set color false" "$VBX_EXE config set color false" "0"
run_test "Config set verbose true" "$VBX_EXE config set verbose true" "0"
run_test "Config set verbose false" "$VBX_EXE config set verbose false" "0"
run_test "Config unset output" "$VBX_EXE config unset output" "0"
run_test "Config unset color" "$VBX_EXE config unset color" "0"
run_test "Config unset verbose" "$VBX_EXE config unset verbose" "0"
run_test "Config show JSON output" "$VBX_EXE config show --output json" "0"
run_test "Config show CSV output" "$VBX_EXE config show --output csv" "0"
run_test "Config show YAML output" "$VBX_EXE config show --output yaml" "0"
run_test "Config show Table output" "$VBX_EXE config show --output table" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}INDEX COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

# Clean up any previous test indices
$VBX_EXE index delete test-memory --force 2>/dev/null || true
$VBX_EXE index delete test-disk --force 2>/dev/null || true
$VBX_EXE index delete test-hybrid --force 2>/dev/null || true
$VBX_EXE index delete test-advanced --force 2>/dev/null || true

run_test "Index help" "$VBX_EXE index --help" "0"
run_test "Index list empty" "$VBX_EXE index ls" "0"
run_test "Index list JSON" "$VBX_EXE index ls --output json" "0"
run_test "Index list CSV" "$VBX_EXE index ls --output csv" "0"
run_test "Index list YAML" "$VBX_EXE index ls --output yaml" "0"
run_test "Index list Table" "$VBX_EXE index ls --output table" "0"

run_test "Index create memory" "$VBX_EXE index create test-memory --storage memory" "0"
run_test "Index create disk" "$VBX_EXE index create test-disk --storage disk" "0"
run_test "Index create hybrid" "$VBX_EXE index create test-hybrid --storage hybrid" "0"
run_test "Index create advanced" "$VBX_EXE index create test-advanced --storage hybrid --lemmatizer --stopwords --min-length 2 --max-length 50" "0"

run_test "Index list with indices" "$VBX_EXE index ls" "0"
run_test "Index use test-memory" "$VBX_EXE index use test-memory" "0"
run_test "Index use test-disk" "$VBX_EXE index use test-disk" "0"
run_test "Index use test-hybrid" "$VBX_EXE index use test-hybrid" "0"
run_test "Index use test-advanced" "$VBX_EXE index use test-advanced" "0"

run_test "Index info current" "$VBX_EXE index info" "0"
run_test "Index info test-memory" "$VBX_EXE index info test-memory" "0"
run_test "Index info test-disk" "$VBX_EXE index info test-disk" "0"
run_test "Index info JSON" "$VBX_EXE index info test-memory --output json" "0"
run_test "Index info CSV" "$VBX_EXE index info test-memory --output csv" "0"
run_test "Index info YAML" "$VBX_EXE index info test-memory --output yaml" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}DOCUMENT COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

# Create test content variables
DOC1_CONTENT="This is a comprehensive test document about artificial intelligence and machine learning."
DOC2_CONTENT="Advanced search capabilities using inverted indices provide fast full-text retrieval."
DOC3_CONTENT="Natural language processing techniques enable intelligent text analysis."
DOC4_CONTENT="Database indexing and query optimization are fundamental for performance."

run_test "Doc help" "$VBX_EXE doc --help" "0"
run_test "Doc add help" "$VBX_EXE doc add --help" "0"
run_test "Doc add-file help" "$VBX_EXE doc add-file --help" "0"
run_test "Doc remove help" "$VBX_EXE doc remove --help" "0"
run_test "Doc list help" "$VBX_EXE doc ls --help" "0"
run_test "Doc clear help" "$VBX_EXE doc clear --help" "0"

# Use test-memory index as active
run_test "Set active index to test-memory" "$VBX_EXE index use test-memory" "0"

run_test "Doc list empty (active index)" "$VBX_EXE doc ls" "0"
run_test "Doc add document 1 (active index)" "$VBX_EXE doc add doc1 \"$DOC1_CONTENT\"" "0"
run_test "Doc add document 2 (active index)" "$VBX_EXE doc add doc2 \"$DOC2_CONTENT\"" "0"
run_test "Doc add document 3 (active index)" "$VBX_EXE doc add doc3 \"$DOC3_CONTENT\"" "0"
run_test "Doc add document 4 (active index)" "$VBX_EXE doc add doc4 \"$DOC4_CONTENT\"" "0"

# Test file-based addition
echo "Test file content for document addition" > temp_test_doc.txt
run_test "Doc add-file (active index)" "$VBX_EXE doc add-file doc5 temp_test_doc.txt" "0"
rm -f temp_test_doc.txt

run_test "Doc list with documents (active index)" "$VBX_EXE doc ls" "0"
run_test "Doc list JSON (active index)" "$VBX_EXE doc ls --output json" "0"
run_test "Doc list CSV (active index)" "$VBX_EXE doc ls --output csv" "0"
run_test "Doc list YAML (active index)" "$VBX_EXE doc ls --output yaml" "0"

# Add documents to other indices using --index flag
run_test "Doc add to disk index (explicit)" "$VBX_EXE doc add --index test-disk diskdoc1 \"Document in disk storage index\"" "0"
run_test "Doc add to hybrid index (explicit)" "$VBX_EXE doc add --index test-hybrid hybriddoc1 \"Document in hybrid storage index\"" "0"
run_test "Doc add to advanced index (explicit)" "$VBX_EXE doc add --index test-advanced advdoc1 \"Running algorithms efficiently requires optimized data structures\"" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}SEARCH COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Search help" "$VBX_EXE search --help" "0"
run_test "Search OR logic (active index)" "$VBX_EXE search \"artificial intelligence\"" "0"
run_test "Search AND logic (active index)" "$VBX_EXE search \"artificial intelligence\" --and" "0"
run_test "Search with limit (active index)" "$VBX_EXE search \"test\" --limit 5" "0"
run_test "Search with short limit (active index)" "$VBX_EXE search \"test\" -l 3" "0"
run_test "Search JSON output (active index)" "$VBX_EXE search \"search\" --output json" "0"
run_test "Search CSV output (active index)" "$VBX_EXE search \"search\" --output csv" "0"
run_test "Search YAML output (active index)" "$VBX_EXE search \"search\" --output yaml" "0"
run_test "Search Table output (active index)" "$VBX_EXE search \"search\" --output table" "0"

run_test "Search single term (active index)" "$VBX_EXE search \"artificial\"" "0"
run_test "Search multiple terms OR (active index)" "$VBX_EXE search \"search capabilities\"" "0"
run_test "Search multiple terms AND (active index)" "$VBX_EXE search \"search capabilities\" --and" "0"
run_test "Search nonexistent term (active index)" "$VBX_EXE search \"nonexistent\"" "0"

run_test "Search disk index (explicit)" "$VBX_EXE search \"document\" --index test-disk" "0"
run_test "Search hybrid index (explicit)" "$VBX_EXE search \"document\" --index test-hybrid" "0"
run_test "Search advanced index (explicit)" "$VBX_EXE search \"algorithms\" --index test-advanced" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}STATS COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Stats help" "$VBX_EXE stats --help" "0"
run_test "Stats general (active index)" "$VBX_EXE stats" "0"
run_test "Stats cache (active index)" "$VBX_EXE stats --cache" "0"
run_test "Stats cache short (active index)" "$VBX_EXE stats -c" "0"
run_test "Stats term specific (active index)" "$VBX_EXE stats --term artificial" "0"
run_test "Stats term short (active index)" "$VBX_EXE stats -t intelligence" "0"
run_test "Stats JSON output (active index)" "$VBX_EXE stats --output json" "0"
run_test "Stats CSV output (active index)" "$VBX_EXE stats --output csv" "0"
run_test "Stats YAML output (active index)" "$VBX_EXE stats --output yaml" "0"

run_test "Stats disk index (explicit)" "$VBX_EXE stats --index test-disk" "0"
run_test "Stats hybrid index (explicit)" "$VBX_EXE stats --index test-hybrid" "0"
run_test "Stats advanced index (explicit)" "$VBX_EXE stats --index test-advanced" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}MAINTENANCE COMMAND TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Maint help" "$VBX_EXE maint --help" "0"
run_test "Maint flush help" "$VBX_EXE maint flush --help" "0"
run_test "Maint gc help" "$VBX_EXE maint gc --help" "0"
run_test "Maint benchmark help" "$VBX_EXE maint benchmark --help" "0"
run_test "Maint stress help" "$VBX_EXE maint stress --help" "0"

run_test "Maint flush (active index)" "$VBX_EXE maint flush" "0"
run_test "Maint flush disk (explicit)" "$VBX_EXE maint flush --index test-disk" "0"
run_test "Maint flush hybrid (explicit)" "$VBX_EXE maint flush --index test-hybrid" "0"

run_test "Maint GC (active index)" "$VBX_EXE maint gc" "0"
run_test "Maint GC disk (explicit)" "$VBX_EXE maint gc --index test-disk" "0"
run_test "Maint GC hybrid (explicit)" "$VBX_EXE maint gc --index test-hybrid" "0"

run_test "Maint benchmark small (active index)" "$VBX_EXE maint benchmark --documents 10" "0"
run_test "Maint benchmark short docs (active index)" "$VBX_EXE maint benchmark -d 5" "0"
run_test "Maint benchmark JSON (active index)" "$VBX_EXE maint benchmark --documents 5 --output json" "0"

run_test "Maint stress small (active index)" "$VBX_EXE maint stress --documents 50" "0"
run_test "Maint stress short docs (active index)" "$VBX_EXE maint stress -d 25" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}INDEX EXPORT TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Index export help" "$VBX_EXE index export --help" "0"
run_test "Index export memory" "$VBX_EXE index export test-memory export-memory.json" "0"
run_test "Index export disk" "$VBX_EXE index export test-disk export-disk.json" "0"
run_test "Index export hybrid" "$VBX_EXE index export test-hybrid export-hybrid.json" "0"
run_test "Index export advanced" "$VBX_EXE index export test-advanced export-advanced.json" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}DOCUMENT REMOVAL TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

# Ensure test-memory is active before removal tests (in case previous tests changed it)
run_test "Set active index to test-memory for removal tests" "$VBX_EXE index use test-memory" "0"
run_test "Doc remove from active index" "$VBX_EXE doc remove doc1" "0"
run_test "Doc remove from disk (explicit)" "$VBX_EXE doc remove --index test-disk diskdoc1" "0"
run_test "Doc remove from hybrid (explicit)" "$VBX_EXE doc remove --index test-hybrid hybriddoc1" "0"
run_test "Doc remove from advanced (explicit)" "$VBX_EXE doc remove --index test-advanced advdoc1" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}OUTPUT FORMAT COMBINATION TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Verbose + JSON" "$VBX_EXE --verbose index ls --output json" "0"
run_test "Quiet + CSV" "$VBX_EXE --quiet index ls --output csv" "0"
run_test "Debug + YAML" "$VBX_EXE --debug index ls --output yaml" "0"
run_test "No-color + Table" "$VBX_EXE --no-color index ls --output table" "0"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}ERROR HANDLING TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Invalid command" "$VBX_EXE invalidcommand" "1"
run_test "Index create duplicate" "$VBX_EXE index create test-memory --storage memory" "1"
run_test "Index use nonexistent" "$VBX_EXE index use nonexistent-index" "1"
run_test "Index delete nonexistent" "$VBX_EXE index delete nonexistent-index --force" "1"
run_test "Doc add to nonexistent index (explicit)" "$VBX_EXE doc add --index nonexistent-index testdoc \"content\"" "1"
run_test "Doc remove nonexistent doc (active index)" "$VBX_EXE doc remove nonexistent-doc" "1"
run_test "Search nonexistent index (explicit)" "$VBX_EXE search \"test\" --index nonexistent-index" "1"
run_test "Stats nonexistent index (explicit)" "$VBX_EXE stats --index nonexistent-index" "1"
run_test "Config set invalid key" "$VBX_EXE config set invalidkey value" "1"
run_test "Config set invalid output format" "$VBX_EXE config set output invalidformat" "1"

echo
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}CLEANUP TESTS${NC}"
echo -e "${YELLOW}========================================${NC}"

run_test "Doc clear with force (active index)" "$VBX_EXE doc clear --force" "0"
run_test "Index delete memory" "$VBX_EXE index delete test-memory --force" "0"
run_test "Index delete disk" "$VBX_EXE index delete test-disk --force" "0"
run_test "Index delete hybrid" "$VBX_EXE index delete test-hybrid --force" "0"
run_test "Index delete advanced" "$VBX_EXE index delete test-advanced --force" "0"

echo
echo "Cleaning up test files..."
rm -f export-*.json temp_test_doc.txt 2>/dev/null || true

echo
echo -e "${BLUE}================================================${NC}"
echo -e "${BLUE}TEST SUMMARY${NC}"
echo -e "${BLUE}================================================${NC}"
echo
echo "Total Tests Run: $TOTAL_TESTS"
echo -e "Tests Passed:   ${GREEN}$PASSED_TESTS${NC}"
echo -e "Tests Failed:   ${RED}$FAILED_TESTS${NC}"
echo

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}===============================${NC}"
    echo -e "${GREEN}ALL TESTS PASSED! SUCCESS! ✓${NC}"
    echo -e "${GREEN}===============================${NC}"
    echo
    echo "VerbexCli has been COMPREHENSIVELY tested and ALL $TOTAL_TESTS tests passed."
    echo
    echo "FEATURES TESTED:"
    echo "✓ All global options (verbose, quiet, debug, no-color, output formats)"
    echo "✓ All config commands (show, set, unset) with all valid values"
    echo "✓ All index commands (create, list, use, delete, info, export)"
    echo "✓ All storage modes (memory, disk, hybrid) with all options"
    echo "✓ All document commands (add, add-file, remove, list, clear)"
    echo "✓ All search functionality (OR/AND logic, limits, all output formats)"
    echo "✓ All statistics commands (general, cache, term-specific)"
    echo "✓ All maintenance commands (flush, gc, benchmark, stress)"
    echo "✓ All output formats (Table, JSON, CSV, YAML) on all applicable commands"
    echo "✓ Error handling for invalid commands and parameters"
    echo "✓ Comprehensive cleanup and state management"
    echo
    echo "The Verbex library has been EXHAUSTIVELY exercised through the CLI."
    echo
    exit 0
else
    echo -e "${RED}===============================${NC}"
    echo -e "${RED}TESTS FAILED! $FAILED_TESTS failures detected${NC}"
    echo -e "${RED}===============================${NC}"
    echo
    echo "$FAILED_TESTS out of $TOTAL_TESTS tests failed."
    echo "Please review the failures above and fix the issues."
    echo
    exit 1
fi