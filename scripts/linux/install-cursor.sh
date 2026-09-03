#!/usr/bin/env sh
# Connect Cursor to the Verbex MCP server by adding a 'verbex' entry to ~/.cursor/mcp.json.
# The Verbex MCP server requires no authentication.
# Usage: sh install-cursor.sh
# Override with VERBEX_MCP_URL / VERBEX_CURSOR_CONFIG.
set -e

URL="${VERBEX_MCP_URL:-http://127.0.0.1:8200/mcp}"
CONFIG="${VERBEX_CURSOR_CONFIG:-$HOME/.cursor/mcp.json}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" <<'PY'
import json, os, sys
path, url = sys.argv[1], sys.argv[2]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    if not isinstance(cfg, dict):
        cfg = {}
except Exception:
    cfg = {}
servers = cfg.get("mcpServers")
if not isinstance(servers, dict):
    servers = {}
    cfg["mcpServers"] = servers
servers["verbex"] = {"url": url}
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'verbex' to " + path)
PY
echo "Restart Cursor to pick up the change (Settings -> MCP)."
