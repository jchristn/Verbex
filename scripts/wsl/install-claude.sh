#!/usr/bin/env sh
# Connect Claude Code (installed inside WSL) to the Verbex MCP server. Usage: install-claude.sh
# WSL 1 counterpart of scripts/linux/install-claude.sh. Uses the Linux claude CLI, never the Windows shim on the appended Windows PATH.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd claude "Install Claude Code inside WSL: npm i -g @anthropic-ai/claude-code"

# WSL 1 shares localhost with Windows, so the default 127.0.0.1 endpoint reaches a server on either side.

wsl_log "This registers verbex with Claude Code inside WSL. For Claude Code on Windows, run scripts\\windows\\install-claude.bat from Windows."
wsl_run "$SCRIPT_DIR/../linux/install-claude.sh" "$@"
