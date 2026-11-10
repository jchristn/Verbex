#!/usr/bin/env bash
# Disconnect Claude Code from the Verbex MCP server (user scope), via the claude CLI.
# Succeeds with "nothing to remove" when there is no 'verbex' entry; every other server is kept.
# WSL 2 version: configures Claude Code inside this distro (not on Windows). For Claude Code on Windows, run
# scripts\windows\remove-claude.bat from Windows.
# Usage: scripts/wsl2/remove-claude.sh
# Requires the claude CLI.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments; see --help)"
wsl_require_cmd claude "Install Claude Code inside WSL: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."
wsl_log "This configures Claude Code inside this WSL distro. For Claude Code on Windows, run scripts\\windows\\remove-claude.bat from Windows."

# Prints "removed" or "absent"; fails on any other error from the claude CLI.
claude_remove() {
    local out
    if out="$(claude mcp remove --scope user verbex 2>&1)"; then echo removed; return 0; fi
    case "$out" in
        *"No MCP server named"*) echo absent ;;
        *) die "claude mcp remove failed: $out" ;;
    esac
}
claude_add() {
    claude mcp add --scope user --transport http verbex "$URL" >/dev/null || die "claude mcp add failed."
}

# Assigned first: a failure inside $(...) in an if test would not stop the script.
STATUS="$(claude_remove)"
if [ "$STATUS" = removed ]; then
    echo "Removed 'verbex' from Claude Code at user scope."
else
    echo "No 'verbex' entry in Claude Code at user scope; nothing to remove."
fi
echo "Restart Claude Code to pick up the change."
