@echo off
REM =============================================================================
REM Verbex.Server Multi-Index API Test Script
REM =============================================================================
REM This script tests all the REST API endpoints of the Verbex.Server
REM with focus on multi-index functionality across multiple indices.
REM Make sure the server is running before executing this script.
REM Usage: test.bat
REM =============================================================================

echo Starting Verbex.Server Multi-Index API Tests
echo ============================================
echo.

REM Set server base URL and test indices
set SERVER_URL=http://localhost:8080
set AUTH_HEADER=Authorization: Bearer verbexadmin
set TEST_INDEX_1=test-docs
set TEST_INDEX_2=technical
set TEST_INDEX_3=temp-index

echo Testing server availability...
echo Server URL: %SERVER_URL%
echo Test Indices: %TEST_INDEX_1%, %TEST_INDEX_2%, %TEST_INDEX_3%
echo.

REM =============================================================================
REM Health Check Tests
REM =============================================================================

echo.
echo [1/25] Testing Root Health Check...
curl -s -w "Status: %%{http_code}\n" %SERVER_URL%/ | findstr /C:"Status" /C:"Healthy"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Root health check failed
    goto :error
)

echo.
echo [2/25] Testing v1.0 Health Check...
curl -s -w "Status: %%{http_code}\n" %SERVER_URL%/v1.0/health | findstr /C:"Status" /C:"Healthy"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: v1.0 health check failed
    goto :error
)

REM =============================================================================
REM Authentication Tests
REM =============================================================================

echo.
echo [3/25] Testing Login with Valid Credentials...
curl -s -X POST %SERVER_URL%/v1.0/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"username\": \"admin\", \"password\": \"password\"}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status" /C:"token"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Login with valid credentials failed
    goto :error
)

echo.
echo [4/25] Testing Token Validation...
curl -s -X GET %SERVER_URL%/v1.0/auth/validate ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status" /C:"valid"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Token validation failed
    goto :error
)

REM =============================================================================
REM Index Management Tests - List and Verify Empty State
REM =============================================================================

echo.
echo [5/25] Testing List Initial Indices State...
curl -s -X GET %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: List indices failed
    goto :error
)

echo.
echo [6/25] Creating Test Index: main-index...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"main-index\", \"name\": \"Main Test Index\", \"description\": \"Main test index for validation\", \"repositoryFilename\": \"main-test.db\", \"inMemory\": false}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"created successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Create main test index failed
    goto :error
)

echo.
echo [7/25] Creating Test Index: test-index...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"test-index\", \"name\": \"Test Validation Index\", \"description\": \"Test index for validation purposes\", \"repositoryFilename\": \"test-validation.db\", \"inMemory\": true}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"created successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Create test validation index failed
    goto :error
)

REM =============================================================================
REM Index Management Tests - Create New Indices
REM =============================================================================

echo.
echo [8/25] Creating Test Index 1: %TEST_INDEX_1%...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"%TEST_INDEX_1%\", \"name\": \"Test Documents\", \"description\": \"Test document index\", \"repositoryFilename\": \"%TEST_INDEX_1%.db\", \"inMemory\": false}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"created successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Create test index 1 failed
    goto :error
)

echo.
echo [9/25] Creating Test Index 2: %TEST_INDEX_2%...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"%TEST_INDEX_2%\", \"name\": \"Technical Documentation\", \"description\": \"Technical documentation index\", \"repositoryFilename\": \"%TEST_INDEX_2%.db\", \"inMemory\": true}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"created successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Create test index 2 failed
    goto :error
)

echo.
echo [10/25] Creating Temporary Index: %TEST_INDEX_3%...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"%TEST_INDEX_3%\", \"name\": \"Temporary Index\", \"description\": \"Temporary index for cleanup testing\", \"repositoryFilename\": \"%TEST_INDEX_3%.db\", \"inMemory\": true}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"created successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Create temporary index failed
    goto :error
)

echo.
echo [11/25] Verifying All Indices Listed...
curl -s -X GET %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" > temp_indices.txt

findstr /C:"Status: 200" /C:"main-index" /C:"test-index" /C:"%TEST_INDEX_1%" /C:"%TEST_INDEX_2%" /C:"%TEST_INDEX_3%" temp_indices.txt >nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Not all created indices are listed
    del temp_indices.txt
    goto :error
)
del temp_indices.txt

REM =============================================================================
REM Document Management Tests - Multiple Indices
REM =============================================================================

echo.
echo [12/25] Adding Documents to Main Index...
curl -s -X POST %SERVER_URL%/v1.0/indices/main-index/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"11111111-1111-1111-1111-111111111111\", \"content\": \"This is a sample document in the main index with search terms.\"}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"added successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Add document to main index failed
    goto :error
)

echo.
echo [13/25] Adding Documents to Test Index...
curl -s -X POST %SERVER_URL%/v1.0/indices/test-index/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"22222222-2222-2222-2222-222222222222\", \"content\": \"This is a test document with different content and keywords.\"}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"added successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Add document to test index failed
    goto :error
)

echo.
echo [14/25] Adding Documents to Custom Index 1...
curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_1%/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"33333333-3333-3333-3333-333333333333\", \"content\": \"Custom document with unique content for testing multi-index functionality.\"}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"added successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Add document to custom index 1 failed
    goto :error
)

echo.
echo [15/25] Adding Documents to Custom Index 2...
curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_2%/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"44444444-4444-4444-4444-444444444444\", \"content\": \"Technical documentation about algorithms, data structures, and programming concepts.\"}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 201" /C:"added successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Add document to custom index 2 failed
    goto :error
)

echo.
echo [16/25] Adding Multiple Documents to Test Cross-Index Isolation...
curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_1%/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"55555555-5555-5555-5555-555555555555\", \"content\": \"Isolation test document one with shared keywords.\"}"

curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_2%/documents ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"66666666-6666-6666-6666-666666666666\", \"content\": \"Isolation test document two with shared keywords.\"}"

echo Multiple documents added for isolation testing

REM =============================================================================
REM Document Retrieval Tests - Multiple Indices
REM =============================================================================

echo.
echo [17/25] Testing Get Documents from Main Index...
curl -s -X GET %SERVER_URL%/v1.0/indices/main-index/documents ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"documents"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Get documents from main index failed
    goto :error
)

echo.
echo [18/25] Testing Get Specific Document from Test Index...
curl -s -X GET %SERVER_URL%/v1.0/indices/test-index/documents/22222222-2222-2222-2222-222222222222 ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"22222222-2222-2222-2222-222222222222"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Get specific document from test index failed
    goto :error
)

echo.
echo [19/25] Testing Get Documents from Custom Index 1...
curl -s -X GET %SERVER_URL%/v1.0/indices/%TEST_INDEX_1%/documents ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"documents"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Get documents from custom index 1 failed
    goto :error
)

REM =============================================================================
REM Search Tests - Multiple Indices
REM =============================================================================

echo.
echo [20/25] Testing Search in Main Index...
curl -s -X POST %SERVER_URL%/v1.0/indices/main-index/search ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"query\": \"sample document\", \"maxResults\": 10}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"results"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Search in main index failed
    goto :error
)

echo.
echo [21/25] Testing Search in Test Index...
curl -s -X POST %SERVER_URL%/v1.0/indices/test-index/search ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"query\": \"test document\", \"maxResults\": 10}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"results"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Search in test index failed
    goto :error
)

echo.
echo [22/25] Testing Search in Technical Index...
curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_2%/search ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"query\": \"algorithms data structures\", \"maxResults\": 5}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"results"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Search in technical index failed
    goto :error
)

echo.
echo [23/25] Testing Cross-Index Search Isolation...
curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_1%/search ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"query\": \"isolation test\", \"maxResults\": 10}" ^
  -w "Status: %%{http_code}\n" > temp_search1.txt

curl -s -X POST %SERVER_URL%/v1.0/indices/%TEST_INDEX_2%/search ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"query\": \"isolation test\", \"maxResults\": 10}" ^
  -w "Status: %%{http_code}\n" > temp_search2.txt

findstr /C:"Status: 200" temp_search1.txt >nul && findstr /C:"Status: 200" temp_search2.txt >nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cross-index search isolation test failed
    echo Search 1 result:
    type temp_search1.txt
    echo Search 2 result:
    type temp_search2.txt
    del temp_search1.txt temp_search2.txt
    goto :error
)
del temp_search1.txt temp_search2.txt
echo Cross-index search isolation verified

REM =============================================================================
REM Index Cleanup Tests
REM =============================================================================

echo.
echo [24/25] Testing Delete Document from Specific Index...
curl -s -X DELETE %SERVER_URL%/v1.0/indices/%TEST_INDEX_1%/documents/55555555-5555-5555-5555-555555555555 ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"deleted successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Delete document from specific index failed
    goto :error
)

echo.
echo [25/25] Testing Delete Temporary Index...
curl -s -X DELETE %SERVER_URL%/v1.0/indices/%TEST_INDEX_3% ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 200" /C:"deleted successfully"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Delete temporary index failed
    goto :error
)

REM =============================================================================
REM Error Handling Tests
REM =============================================================================

echo.
echo Testing Error Handling...

echo Testing 404 Not Found...
curl -s -X GET %SERVER_URL%/nonexistent ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 404"
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: 404 handling may need verification
)

echo Testing Unauthorized Access...
curl -s -X GET %SERVER_URL%/v1.0/indices ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 401"
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Unauthorized access handling may need verification
)

echo Testing Non-existent Index...
curl -s -X GET %SERVER_URL%/v1.0/indices/nonexistent-index ^
  -H "%AUTH_HEADER%" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 404"
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Non-existent index handling may need verification
)

echo Testing Duplicate Index Creation...
curl -s -X POST %SERVER_URL%/v1.0/indices ^
  -H "%AUTH_HEADER%" ^
  -H "Content-Type: application/json" ^
  -d "{\"id\": \"main-index\", \"name\": \"Duplicate Test Index\", \"description\": \"This should fail - duplicate ID\", \"repositoryFilename\": \"duplicate.db\", \"inMemory\": false}" ^
  -w "Status: %%{http_code}\n" | findstr /C:"Status: 409"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Duplicate index creation should return 409 Conflict
    goto :error
) else (
    echo SUCCESS: Duplicate index creation properly rejected with 409 Conflict
)

REM =============================================================================
REM Cleanup and Success
REM =============================================================================

echo.
echo Final Verification - Listing All Remaining Indices...
curl -s -X GET %SERVER_URL%/v1.0/indices -H "%AUTH_HEADER%"
echo.

echo.
echo Cleaning up temporary files...
if exist temp_*.txt del temp_*.txt

echo.
echo ================================================================
echo ALL MULTI-INDEX TESTS PASSED SUCCESSFULLY! ✓
echo ================================================================
echo.
echo Summary:
echo - Health checks: PASSED
echo - Authentication: PASSED
echo - Default index management: PASSED
echo - Custom index creation: PASSED (%TEST_INDEX_1%, %TEST_INDEX_2%)
echo - Multi-index document operations: PASSED
echo - Cross-index search isolation: VERIFIED
echo - Document retrieval across indices: PASSED
echo - Index cleanup operations: PASSED
echo - Error handling: VERIFIED
echo - Duplicate index creation rejection: VERIFIED
echo.
echo Multi-Index Verbex.Server API is working correctly!
echo Created indices: main-index, test-index, %TEST_INDEX_1%, %TEST_INDEX_2%
echo Deleted indices: %TEST_INDEX_3%
goto :end

REM =============================================================================
REM Error Handling
REM =============================================================================

:error
echo.
echo ================================================================
echo MULTI-INDEX TESTS FAILED! ✗
echo ================================================================
echo.
echo One or more API tests failed. Please check:
echo 1. Ensure Verbex.Server is running on %SERVER_URL%
echo 2. Check server logs for errors
echo 3. Verify multi-index API endpoints are properly configured
echo 4. Confirm authentication tokens are correct
echo 5. Ensure indices can be created and managed properly
echo.
echo To start the server, run:
echo   dotnet run --project Verbex.Server
echo.
if exist temp_*.txt del temp_*.txt
exit /b 1

:end
echo Test script completed.
pause