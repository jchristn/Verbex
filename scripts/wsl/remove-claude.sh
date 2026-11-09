#!/usr/bin/env sh
# Disconnect Claude Code (installed inside WSL) from the Verbex MCP server.
# WSL 1 counterpart of scripts/linux/remove-claude.sh. Uses the Linux claude CLI, never the Windows shim.
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
wsl_run "$SCRIPT_DIR/../linux/remove-claude.sh" "$@"
