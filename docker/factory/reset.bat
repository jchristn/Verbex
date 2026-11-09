@echo off
REM
REM reset.bat - Reset Verbex Docker deployment to factory defaults
REM
REM This script:
REM   1. Prompts the user to confirm by typing RESET
REM   2. Stops and removes all Verbex containers via docker compose down
REM   3. Restores the gold-copy config and database from the factory directory
REM   4. Clears all log files and transient data
REM

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%.."

echo ========================================
echo   Verbex Docker Factory Reset
echo ========================================
echo.
echo WARNING: This will destroy all current data and restore factory defaults.
echo   - All indices, documents, users, and credentials will be lost
echo   - All log files will be deleted
echo   - Configuration will be reset to defaults
echo.
set /p "confirmation=Type RESET to confirm: "

if not "%confirmation%"=="RESET" (
    echo.
    echo Reset cancelled.
    exit /b 1
)

echo.
echo [1/5] Stopping containers...

REM Try all compose files that might be running
if exist "%DOCKER_DIR%\compose.yaml" (
    docker compose -f "%DOCKER_DIR%\compose.yaml" down 2>NUL
)
if exist "%DOCKER_DIR%\compose-server.yaml" (
    docker compose -f "%DOCKER_DIR%\compose-server.yaml" down 2>NUL
)
if exist "%DOCKER_DIR%\compose-dashboard.yaml" (
    docker compose -f "%DOCKER_DIR%\compose-dashboard.yaml" down 2>NUL
)

echo [2/5] Restoring factory configuration...

copy /y "%SCRIPT_DIR%verbex.json" "%DOCKER_DIR%\server\verbex.json" >NUL
echo   Restored: server\verbex.json

echo [3/5] Restoring factory database...

REM Remove existing database and WAL files
del /f /q "%DOCKER_DIR%\server\db\verbex.db" 2>NUL
del /f /q "%DOCKER_DIR%\server\db\verbex.db-wal" 2>NUL
del /f /q "%DOCKER_DIR%\server\db\verbex.db-shm" 2>NUL

REM Copy gold-copy database
copy /y "%SCRIPT_DIR%verbex.db" "%DOCKER_DIR%\server\db\verbex.db" >NUL
echo   Restored: server\db\verbex.db

echo [4/5] Clearing data directory...

REM Remove all index data files but preserve directory and .gitkeep
for /d %%d in ("%DOCKER_DIR%\server\data\*") do (
    rmdir /s /q "%%d" 2>NUL
)
for %%f in ("%DOCKER_DIR%\server\data\*") do (
    if /i not "%%~nxf"==".gitkeep" (
        del /f /q "%%f" 2>NUL
    )
)
echo   Cleared:  server\data\

echo [5/5] Deleting log files...

REM Server logs - delete all files and subdirectories except .gitkeep
for /d %%d in ("%DOCKER_DIR%\server\logs\*") do (
    rmdir /s /q "%%d" 2>NUL
)
for %%f in ("%DOCKER_DIR%\server\logs\*") do (
    if /i not "%%~nxf"==".gitkeep" (
        del /f /q "%%f" 2>NUL
    )
)
echo   Cleared:  server\logs\

REM Dashboard logs - delete all files and subdirectories except .gitkeep
for /d %%d in ("%DOCKER_DIR%\dashboard\logs\*") do (
    rmdir /s /q "%%d" 2>NUL
)
for %%f in ("%DOCKER_DIR%\dashboard\logs\*") do (
    if /i not "%%~nxf"==".gitkeep" (
        del /f /q "%%f" 2>NUL
    )
)
echo   Cleared:  dashboard\logs\

echo.
echo ========================================
echo   Factory reset complete.
echo ========================================
echo.
echo Default credentials:
echo   Email:        default@user.com
echo   Password:     password
echo   Bearer Token: default
echo   Admin Token:  verbexadmin
echo.
echo Run 'docker compose up -d' from the docker directory to start.

endlocal
