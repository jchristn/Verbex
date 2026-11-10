#!/usr/bin/env bash
# Re-register Claude Code with the Verbex MCP server at user scope: removes the entry, then adds it again.
# Continues when there is no 'verbex' entry to remove; every other server is kept.
# The Verbex MCP server requires no authentication, so no credential is written.
# WSL 2 version: configures Claude Code inside this distro (not on Windows). For Claude Code on Windows, run
# scripts\windows\reinstall-claude.bat from Windows.
# When VERBEX_MCP_URL is not set and nothing answers at 127.0.0.1 inside WSL 2 but the Windows host
# does, the Windows host address is used instead (WSL_NO_HOST_REWRITE=1 keeps 127.0.0.1).
# Usage: scripts/wsl2/reinstall-claude.sh
#   VERBEX_MCP_URL  server URL (default: http://127.0.0.1:8200/mcp)
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
# The same rule as the Windows scripts: http:// or https://, a host, and no whitespace.
URL_RE='^https?://[^/?#[:space:]]+[^[:space:]]*$'
check_url() { [[ "$1" =~ $URL_RE ]] || die "'$1' is not an http(s) URL."; }
if [ -n "${VERBEX_MCP_URL:-}" ]; then
    URL="$VERBEX_MCP_URL"
else
    # Under WSL 2 NAT networking 127.0.0.1 is the distro, not Windows; use the Windows host when only it answers.
    URL="$(wsl_resolve_local_url "http://127.0.0.1:8200/mcp")"
fi
check_url "$URL"
wsl_log "This configures Claude Code inside this WSL distro. For Claude Code on Windows, run scripts\\windows\\reinstall-claude.bat from Windows."

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
    echo "No 'verbex' entry in Claude Code at user scope; continuing."
fi
claude_add
echo "Added 'verbex' to Claude Code at user scope ($URL)."
echo "Restart Claude Code to pick up the change."
