@echo off
REM Disconnect Claude Code from the Verbex MCP server (via the claude CLI). Removes the user-scope entry, plus a
REM local-scope entry for the current directory left by older versions of install-claude.bat.
setlocal
where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH.
  exit /b 1
)

REM claude is itself a batch file (claude.cmd), so it must be run with "call" or this script would end when it does.
set REMOVED=0
call claude mcp remove --scope user verbex >nul 2>nul && set REMOVED=1
call claude mcp remove --scope local verbex >nul 2>nul && set REMOVED=1
if "%REMOVED%"=="1" (
  echo Removed 'verbex' MCP server from Claude Code.
) else (
  echo No 'verbex' MCP server was registered with Claude Code; nothing to remove.
)
endlocal
