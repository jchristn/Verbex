@echo off
if "%~1"=="" (
    echo Error: Image tag is required.
    echo Usage: %~nx0 ^<tag^>
    exit /b 1
)

pushd "%~dp0"
call "%~dp0build-dashboard.bat" "%~1"
if errorlevel 1 goto :failed

call "%~dp0build-server.bat" "%~1"
if errorlevel 1 goto :failed

popd
exit /b 0

:failed
set "exitCode=%ERRORLEVEL%"
popd
exit /b %exitCode%
