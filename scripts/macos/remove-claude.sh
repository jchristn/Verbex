#!/usr/bin/env sh
# Disconnect Claude Code from the Verbex MCP server (via the claude CLI). Removes the user-scope entry, plus a
# local-scope entry for the current directory left by older versions of install-claude.sh.
set -e

command -v claude >/dev/null 2>&1 || { echo "Claude CLI not found on PATH." >&2; exit 1; }

removed=0
claude mcp remove --scope user verbex >/dev/null 2>&1 && removed=1
claude mcp remove --scope local verbex >/dev/null 2>&1 && removed=1
if [ "$removed" -eq 1 ]; then
  echo "Removed 'verbex' MCP server from Claude Code."
else
  echo "No 'verbex' MCP server was registered with Claude Code; nothing to remove."
fi
