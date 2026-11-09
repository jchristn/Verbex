#!/usr/bin/env sh
# Connect Claude Code to the Verbex MCP server at user scope (every project sees it), via the claude CLI.
# The Verbex MCP server requires no authentication.
# Usage: sh install-claude.sh
# Override the endpoint with VERBEX_MCP_URL.
set -e

URL="${VERBEX_MCP_URL:-http://127.0.0.1:8200/mcp}"

command -v claude >/dev/null 2>&1 || {
  echo "Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/en/docs/claude-code" >&2
  exit 1
}

# Re-running replaces the existing entry instead of failing on a duplicate. Older versions of this script
# registered at local scope (only the directory it ran in), so drop a local entry here too.
claude mcp remove --scope local verbex >/dev/null 2>&1 || true
claude mcp remove --scope user verbex >/dev/null 2>&1 || true
claude mcp add --scope user --transport http verbex "$URL"
echo "Added 'verbex' MCP server to Claude Code for every project ($URL). Restart Claude Code to pick it up."
