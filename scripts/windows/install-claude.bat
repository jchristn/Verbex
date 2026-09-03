@echo off
REM Connect Claude Code to the Verbex MCP server (via the claude CLI).
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

claude mcp add --transport http verbex "%VERBEX_MCP_URL%"
echo Added 'verbex' MCP server to Claude Code. Restart Claude Code to pick it up.
endlocal
