#!/usr/bin/env sh
# Connect the Gemini CLI to Verbex by adding a 'verbex' entry to ~/.gemini/settings.json.
# Gemini addresses streamable-HTTP servers with the "httpUrl" field.
# The Verbex MCP server requires no authentication.
# Usage: sh install-gemini.sh
# Override with VERBEX_MCP_URL / VERBEX_GEMINI_CONFIG.
set -e

URL="${VERBEX_MCP_URL:-http://127.0.0.1:8200/mcp}"
CONFIG="${VERBEX_GEMINI_CONFIG:-$HOME/.gemini/settings.json}"

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
servers["verbex"] = {"httpUrl": url}
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'verbex' to " + path)
PY
echo "Restart the Gemini CLI to pick up the change (run /mcp to verify)."
