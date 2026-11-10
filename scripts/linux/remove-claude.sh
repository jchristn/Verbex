#!/usr/bin/env bash
# Disconnect Claude Code from the Verbex MCP server (user scope), via the claude CLI.
# Succeeds with "nothing to remove" when there is no 'verbex' entry; every other server is kept.
# Usage: scripts/linux/remove-claude.sh
# Requires the claude CLI.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL (Docker Desktop containers have one too); WSL also has interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and the WSL 2 network."
fi

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments; see --help)"
command -v claude >/dev/null 2>&1 || die "'claude' was not found. Install Claude Code: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."

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
