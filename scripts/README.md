# Agent connection scripts

One-shot scripts to connect (or disconnect) an AI agent to the Verbex MCP server. There is an
`install-<agent>` and a `remove-<agent>` script per supported agent, in a folder per OS.

```
scripts/
  windows/   install-<agent>.bat   remove-<agent>.bat
  macos/     install-<agent>.sh    remove-<agent>.sh
  linux/     install-<agent>.sh    remove-<agent>.sh
  wsl/       install-<agent>.sh    remove-<agent>.sh   (WSL 1)
  wsl2/      install-<agent>.sh    remove-<agent>.sh   (WSL 2)
```

Supported agents: **claude**, **cursor**, **codex**, **gemini**, **mux**.

These scripts do the same thing as the built-in `verbex-mcp --install` command (see
[`../MCP_API.md`](../MCP_API.md)); use whichever is convenient. All of them target the Verbex MCP
**Streamable HTTP** endpoint, so start the server in HTTP mode first:

```bash
dotnet run --project src/Verbex.Mcp -- --transport http --host 127.0.0.1 --port 8200
```

## Usage

Windows (Command Prompt / PowerShell):

```bat
scripts\windows\install-cursor.bat
scripts\windows\remove-cursor.bat
```

macOS / Linux:

```sh
sh scripts/macos/install-cursor.sh      # or scripts/linux/... ; the two are identical
sh scripts/linux/remove-cursor.sh
```

WSL (run inside the distro; use `wsl2/` on WSL 2 and `wsl/` on WSL 1):

```sh
sh scripts/wsl2/install-claude.sh
sh scripts/wsl2/remove-claude.sh
```

The WSL scripts register the agent that runs *inside* WSL and write the WSL-side config (`~` in the distro). They
reuse the Linux scripts after fixing the WSL environment: Windows entries on `PATH` are ignored, so a Windows
`claude`/`codex`/`gemini` shim is never used (install the agent inside WSL, or run `scripts\windows\...` from Windows
for a Windows agent). On WSL 2 with NAT networking, `127.0.0.1` inside the distro is not Windows; when the default
endpoint does not answer inside WSL but the Windows host does, the scripts use the Windows host address instead
(set `VERBEX_MCP_URL`/`VERBEX_MCP_BASE_URL` to choose the address yourself, `WSL_NO_HOST_REWRITE=1` to keep `127.0.0.1`,
or enable `networkingMode=mirrored` in `.wslconfig`). That host address can change after a Windows restart, so
re-run the install script if the agent stops connecting.

Each `install` script is idempotent — it updates the existing `verbex` entry in place and preserves every
other MCP server in the config. Restart the agent afterward to pick up the change.

## What each script does

| Agent | How it connects | Config it edits |
| --- | --- | --- |
| **claude** | runs the `claude` CLI (`claude mcp add` / `claude mcp remove`) | Claude Code's own store |
| **cursor** | writes an `mcpServers.verbex` entry (`url`) | `~/.cursor/mcp.json` |
| **codex** | writes an `[mcp_servers.verbex]` table (`url`) | `~/.codex/config.toml` |
| **gemini** | writes an `mcpServers.verbex` entry (`httpUrl`) | `~/.gemini/settings.json` |
| **mux** | appends a `verbex` object to the `servers` array (`transport: http`, base `url`, `mcpPath`) | `~/.mux/mcp-servers.json` |

The scripts connect to `http://127.0.0.1:8200/mcp`. The Verbex MCP server exposes **no authentication**, so
no credential headers are written. On Windows the config is edited with PowerShell; on macOS/Linux with
`python3` (required). The `claude` scripts require the `claude` CLI on `PATH`.

## Overriding the defaults

Set environment variables before running:

| Variable | Default | Applies to |
| --- | --- | --- |
| `VERBEX_MCP_URL` | `http://127.0.0.1:8200/mcp` | claude, cursor, codex, gemini |
| `VERBEX_MCP_BASE_URL` | `http://127.0.0.1:8200` | mux (path is `/mcp`) |
| `VERBEX_CURSOR_CONFIG` / `VERBEX_CODEX_CONFIG` / `VERBEX_GEMINI_CONFIG` / `VERBEX_MUX_CONFIG` | the paths above | override a config file location |

Example (macOS/Linux), connecting to a server on another port:

```sh
VERBEX_MCP_URL=http://127.0.0.1:9000/mcp sh scripts/linux/install-cursor.sh
```
