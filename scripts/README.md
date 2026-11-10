# Agent connection scripts

One-shot scripts that connect an AI agent to the Verbex MCP server, or disconnect it. Every OS folder has the
same scripts, with the same names, steps, messages and exit codes:

```
scripts/
  windows/   install-<agent>.bat   remove-<agent>.bat   reinstall-<agent>.bat
  macos/     install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh
  linux/     install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh
  wsl/       install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   (WSL 1)
  wsl2/      install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   (WSL 2)
  tests/     test-scripts.py
```

Agents: **claude** (Claude Code), **codex**, **cursor**, **gemini** (Gemini CLI), **mux**.

These scripts do the same thing as the built-in `verbex-mcp --install` command (see
[`../MCP_API.md`](../MCP_API.md)); use whichever is convenient. All of them target the Verbex MCP
**Streamable HTTP** endpoint, so start the server in HTTP mode first:

```bash
dotnet run --project src/Verbex.Mcp -- --transport http --host 127.0.0.1 --port 8200
```

## Usage

Windows (Command Prompt or PowerShell):

```bat
scripts\windows\install-claude.bat
scripts\windows\reinstall-claude.bat
scripts\windows\remove-claude.bat
```

macOS, Linux and WSL (run the WSL scripts inside the distro: `wsl2/` on WSL 2, `wsl/` on WSL 1):

```sh
scripts/macos/install-claude.sh
scripts/linux/reinstall-codex.sh
bash scripts/wsl2/remove-cursor.sh
```

Every script prints its usage with `-h` or `--help` (`/?` too on Windows) and rejects unknown options and extra
arguments with the same messages on every OS. Each script
checks that it runs on its own OS and names the right folder otherwise (the Linux scripts refuse to run under
WSL; the WSL scripts refuse to run outside WSL or on the other WSL version). `sh script.sh` also works: the
scripts re-run themselves with bash.

## Semantics

- **install** is idempotent: it adds the `verbex` entry, or replaces an existing one, and keeps every other server and
  setting in the config.
- **remove** deletes the `verbex` entry and succeeds with "nothing to remove" when there is none.
- **reinstall** removes the entry (continuing when there is none), then adds it again. It does the same as install;
  it exists so install, uninstall and reinstall are available for every agent on every OS.
- The server URL must be `http://` or `https://` with a host and no spaces; anything else is refused before any
  change is made.
- A config file that is not a JSON object, or a `config.toml` form the scripts cannot edit safely (an inline
  `mcp_servers` table, or `mcp_servers.verbex` written with dotted keys or as an inline table), is reported and left
  unchanged; nothing is overwritten with a guess. With Python 3.11+ the new `config.toml` is also parsed before it is
  written. Names are matched case-sensitively, like the clients do, and a key that differs from ours only in case
  is refused. Writes are atomic (a uniquely named temporary file in the same folder), keep the file's permissions
  (a new file is readable only by you), follow a symlinked config and keep CRLF line endings.
- Restart the agent afterwards to pick up the change.

## What each script does

| Agent | How it connects | Config it edits (macOS, Linux, WSL / Windows) |
| --- | --- | --- |

| **claude** | runs `claude mcp remove --scope user` and `claude mcp add --scope user --transport http` | Claude Code's user-scope config (`~/.claude.json`) |

| **codex** | writes an `[mcp_servers.verbex]` table (`url`) | `${CODEX_HOME:-~/.codex}/config.toml` / `%CODEX_HOME%\config.toml` (default `%USERPROFILE%\.codex\config.toml`) |

| **cursor** | writes an `mcpServers.verbex` entry (`url`) | `~/.cursor/mcp.json` / `%USERPROFILE%\.cursor\mcp.json` |

| **gemini** | writes an `mcpServers.verbex` entry (`httpUrl`) | `~/.gemini/settings.json` / `%USERPROFILE%\.gemini\settings.json` |

| **mux** | writes a `verbex` object in the `servers` array (`transport: http`, origin `url`, `mcpPath`, `auth` type `none`) | `${MUX_CONFIG_DIR:-~/.mux}/mcp-servers.json` / `%MUX_CONFIG_DIR%\mcp-servers.json` (default `%USERPROFILE%\.mux\mcp-servers.json`) |

The formats follow each client's documentation: `claude mcp add --help`, Codex's `config.toml` (`codex mcp get verbex`
shows the result), Cursor's `mcp.json` (`url`, `headers`), the Gemini CLI's `settings.json` (`httpUrl` for Streamable
HTTP, `headers`) and Mux's `mcp-servers.json` (read from `MUX_CONFIG_DIR` when set, otherwise `~/.mux`).

The Verbex MCP server exposes **no authentication**, so no credential is written.

On macOS, Linux and WSL the config is edited with `python3` (3.6 or later). On Windows the `.bat` file carries its
PowerShell code after a `## POWERSHELL` line and runs it with `powershell -Command` (not subject to the execution
policy); nothing else is needed. The
`claude` scripts need the `claude` CLI on `PATH`.

## Overriding the defaults

| Variable | Default | Applies to |
| --- | --- | --- |

| `VERBEX_MCP_URL` | `http://127.0.0.1:8200/mcp` | every agent (Mux stores its origin and path) |

| `VERBEX_MCP_BASE_URL` | derived from `VERBEX_MCP_URL` | mux only: the server origin, path `/mcp` |

| `VERBEX_CODEX_CONFIG`, `VERBEX_CURSOR_CONFIG`, `VERBEX_GEMINI_CONFIG`, `VERBEX_MUX_CONFIG` | the paths above | the config file to edit |

| `CODEX_HOME`, `MUX_CONFIG_DIR` | the clients' own defaults | honored the same way the clients honor them |

Example, connecting to a remote server:

```sh
VERBEX_MCP_URL=https://server.example/mcp scripts/linux/install-claude.sh
VERBEX_MCP_URL=https://server.example/mcp scripts/linux/install-mux.sh
```

```bat
set VERBEX_MCP_URL=https://server.example/mcp
scripts\windows\install-claude.bat
```

## WSL

The `wsl/` and `wsl2/` scripts configure the agent that runs *inside* the distro and write its config under the
distro's home directory. For an agent running on Windows, run the `scripts\windows` script from Windows. They
source `_wsl-common.sh`, which:

- drops Windows (`/mnt/<drive>/...`) entries from `PATH`, so a Windows `claude`, `codex` or `gemini` shim is never
  used; a tool that exists only on the Windows side is reported as such (`WSL_KEEP_WINDOWS_PATH=1` keeps them);
- fails clearly on scripts checked out with CRLF line endings and warns about a checkout on `/mnt/<drive>`;
- refuses to run on the other WSL version.

On **WSL 2** with NAT networking, `127.0.0.1` inside the distro is not Windows. When `VERBEX_MCP_URL` (and, for mux,
`VERBEX_MCP_BASE_URL`) is not set and nothing answers at the default address inside WSL but the Windows host does,
the scripts use the Windows host address instead. Set the URL yourself to choose, `WSL_NO_HOST_REWRITE=1` to keep
`127.0.0.1`, or enable `networkingMode=mirrored` in `.wslconfig`. The host address can change after a Windows
restart, so re-run the install script if the agent stops connecting.

On **WSL 1** the distro shares the network with Windows, so `127.0.0.1` already reaches a server running on Windows
and the URL is never rewritten.

## Tests

`python3 scripts/tests/test-scripts.py` runs every Unix script against a temporary home directory with fake
`claude`, `uname`, `curl` and `ip` tools and checks the files each one writes, the install/remove/reinstall rules,
argument handling and the OS guards, again under `/usr/bin/python3` when that is another version (macOS: 3.9).
When `pwsh` is installed it runs the same scenarios through the PowerShell part of every Windows script and checks
that Windows writes the same entries and prints the same messages as the Unix scripts.
