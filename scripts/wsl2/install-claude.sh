#!/usr/bin/env sh
# Connect Claude Code (installed inside WSL) to the Verbex MCP server. Usage: install-claude.sh
# WSL 2 counterpart of scripts/linux/install-claude.sh. Uses the Linux claude CLI, never the Windows shim on the appended Windows PATH.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd claude "Install Claude Code inside WSL: npm i -g @anthropic-ai/claude-code"

# Under WSL 2 NAT networking, 127.0.0.1 is the distro, not Windows; if the server only answers on the
# Windows host, use that address. An explicit VERBEX_MCP_URL is used as given.
if [ -z "${VERBEX_MCP_URL:-}" ]; then
  VERBEX_MCP_URL="$(wsl_resolve_local_url "http://127.0.0.1:8200/mcp")"
  export VERBEX_MCP_URL
fi

wsl_log "This registers verbex with Claude Code inside WSL. For Claude Code on Windows, run scripts\\windows\\install-claude.bat from Windows."
wsl_run "$SCRIPT_DIR/../linux/install-claude.sh" "$@"
