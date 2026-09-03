#!/usr/bin/env sh
# Connect Claude Code to the Verbex MCP server (via the claude CLI).
# The Verbex MCP server requires no authentication.
# Usage: sh install-claude.sh
# Override the endpoint with VERBEX_MCP_URL.
set -e

URL="${VERBEX_MCP_URL:-http://127.0.0.1:8200/mcp}"

command -v claude >/dev/null 2>&1 || {
  echo "Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/en/docs/claude-code" >&2
  exit 1
}

claude mcp add --transport http verbex "$URL"
echo "Added 'verbex' MCP server to Claude Code. Restart Claude Code to pick it up."
