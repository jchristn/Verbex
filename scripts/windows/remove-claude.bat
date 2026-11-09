@echo off
REM Disconnect Claude Code from the Verbex MCP server (via the claude CLI).
setlocal
where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH.
  exit /b 1
)

REM claude is itself a batch file (claude.cmd), so it must be run with "call" or this script would end when it does.
call claude mcp remove verbex
echo Removed 'verbex' MCP server from Claude Code.
endlocal
