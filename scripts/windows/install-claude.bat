@echo off
REM Connect Claude Code to the Verbex MCP server at user scope (every project sees it), via the claude CLI.
REM The Verbex MCP server requires no authentication.
REM Usage: install-claude.bat
REM Override the endpoint with VERBEX_MCP_URL.
setlocal
if "%VERBEX_MCP_URL%"=="" set "VERBEX_MCP_URL=http://127.0.0.1:8200/mcp"

where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/en/docs/claude-code
  exit /b 1
)

REM Re-running replaces the existing entry instead of failing on a duplicate. claude is itself a batch file
REM (claude.cmd), so it must be run with "call" or this script would end when it does.
REM Older versions registered at local scope (only the directory it ran in), so drop a local entry here too.
call claude mcp remove --scope local verbex >nul 2>nul
call claude mcp remove --scope user verbex >nul 2>nul
call claude mcp add --scope user --transport http verbex "%VERBEX_MCP_URL%"
if errorlevel 1 exit /b 1
echo Added 'verbex' MCP server to Claude Code for every project. Restart Claude Code to pick it up.
endlocal
