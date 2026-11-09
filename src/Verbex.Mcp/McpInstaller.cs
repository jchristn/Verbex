namespace Verbex.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Registers (or removes) the Verbex MCP server in the configuration files of supported AI clients:
    /// Claude Code, Cursor, Codex, the Gemini CLI, and Mux. Each client's config is edited in place with a
    /// DOM-preserving JSON merge (or, for Codex, a line-preserving edit of its TOML file), so every other MCP
    /// server entry and unrelated setting is retained.
    /// <para>
    /// The Verbex MCP server exposes no authentication, so no credential headers are written. Entries point
    /// at the Streamable HTTP endpoint, which requires the server to be running with
    /// <c>--transport http</c>.
    /// </para>
    /// </summary>
    public static class McpInstaller
    {
        #region Public-Members

        /// <summary>
        /// Default host advertised to clients when none is supplied.
        /// </summary>
        public const string DefaultHost = "127.0.0.1";

        /// <summary>
        /// Default port advertised to clients when none is supplied.
        /// </summary>
        public const int DefaultPort = 8200;

        #endregion

        #region Private-Members

        private const string _ServerKey = "verbex";
        private const string _McpPath = "/mcp";

        private static readonly Regex _TomlTableHeader = new Regex(
            @"^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly JsonSerializerOptions _WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Detect supported AI client config files and add or update the Verbex MCP server entry in each.
        /// Missing config files are created at the client's primary path so a fresh machine still connects.
        /// </summary>
        /// <param name="host">Host the MCP server listens on. When null or empty, <see cref="DefaultHost"/> is used.</param>
        /// <param name="port">Port the MCP server listens on. Values less than 1 fall back to <see cref="DefaultPort"/>.</param>
        public static void Install(string? host, int port)
        {
            string resolvedHost = string.IsNullOrWhiteSpace(host) ? DefaultHost : host!;
            int resolvedPort = port < 1 ? DefaultPort : port;
            string baseUrl = "http://" + resolvedHost + ":" + resolvedPort.ToString();
            string fullUrl = baseUrl + _McpPath;

            Console.WriteLine("Installing Verbex MCP (" + fullUrl + ") into supported AI clients:");

            foreach (McpClientTarget client in BuildTargets())
            {
                try
                {
                    InstallClient(client, baseUrl, fullUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Warning: failed to update " + client.Name + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Detect supported AI client config files and remove the Verbex MCP server entry from each.
        /// Missing config files are left untouched.
        /// </summary>
        public static void Uninstall()
        {
            Console.WriteLine("Removing Verbex MCP from supported AI clients:");

            foreach (McpClientTarget client in BuildTargets())
            {
                try
                {
                    UninstallClient(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Warning: failed to update " + client.Name + ": " + ex.Message);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static List<McpClientTarget> BuildTargets()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return new List<McpClientTarget>
            {
                new McpClientTarget("Claude Code", McpConfigFormat.McpServersTypeUrl, new string[]
                {
                    Path.Combine(home, ".claude.json")
                }),
                new McpClientTarget("Cursor", McpConfigFormat.McpServersUrl, new string[]
                {
                    Path.Combine(home, ".cursor", "mcp.json")
                }),
                new McpClientTarget("Codex", McpConfigFormat.CodexToml, new string[]
                {
                    Path.Combine(home, ".codex", "config.toml")
                }),
                new McpClientTarget("Gemini", McpConfigFormat.McpServersHttpUrl, new string[]
                {
                    Path.Combine(home, ".gemini", "settings.json")
                }),
                new McpClientTarget("Mux", McpConfigFormat.MuxServersArray, new string[]
                {
                    Path.Combine(home, ".mux", "mcp-servers.json")
                })
            };
        }

        private static void InstallClient(McpClientTarget client, string baseUrl, string fullUrl)
        {
            string targetPath = ResolveExistingPath(client) ?? client.ConfigPaths[0];

            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (client.Format == McpConfigFormat.CodexToml)
            {
                ApplyCodexTomlEntry(targetPath, fullUrl);
                RemoveLegacyCodexJsonEntry(targetPath);
                Console.WriteLine("  Installed Verbex MCP for " + client.Name + " at " + targetPath);
                return;
            }

            JsonObject root = LoadRoot(targetPath);

            if (client.Format == McpConfigFormat.MuxServersArray)
                ApplyMuxEntry(root, baseUrl);
            else
                ApplyMcpServersEntry(root, client.Format, fullUrl);

            File.WriteAllText(targetPath, root.ToJsonString(_WriteOptions));
            Console.WriteLine("  Installed Verbex MCP for " + client.Name + " at " + targetPath);
        }

        private static void UninstallClient(McpClientTarget client)
        {
            if (client.Format == McpConfigFormat.CodexToml)
            {
                UninstallCodex(client);
                return;
            }

            string? targetPath = ResolveExistingPath(client);
            if (targetPath == null)
            {
                Console.WriteLine("  Skipped " + client.Name + ": no config at " + client.ConfigPaths[0]);
                return;
            }

            JsonObject root = LoadRoot(targetPath);
            bool changed;

            if (client.Format == McpConfigFormat.MuxServersArray)
                changed = RemoveMuxEntry(root);
            else
                changed = RemoveMcpServersEntry(root);

            if (!changed)
            {
                Console.WriteLine("  Skipped " + client.Name + ": no Verbex entry in " + targetPath);
                return;
            }

            File.WriteAllText(targetPath, root.ToJsonString(_WriteOptions));
            Console.WriteLine("  Removed Verbex MCP for " + client.Name + " at " + targetPath);
        }

        private static void UninstallCodex(McpClientTarget client)
        {
            string targetPath = client.ConfigPaths[0];
            bool removedLegacy = RemoveLegacyCodexJsonEntry(targetPath);
            bool removed = File.Exists(targetPath) && RemoveCodexTomlEntry(targetPath);

            if (removed)
                Console.WriteLine("  Removed Verbex MCP for " + client.Name + " at " + targetPath);
            else if (!removedLegacy)
                Console.WriteLine("  Skipped " + client.Name + ": no Verbex entry in " + targetPath);
        }

        private static void ApplyCodexTomlEntry(string path, string fullUrl)
        {
            List<string> lines = ReadTomlWithoutVerbexTable(path, out bool _);

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);
            if (lines.Count > 0)
                lines.Add(string.Empty);

            lines.Add("[mcp_servers." + _ServerKey + "]");
            lines.Add("url = " + JsonSerializer.Serialize(fullUrl));
            WriteTomlLines(path, lines);
        }

        private static bool RemoveCodexTomlEntry(string path)
        {
            List<string> lines = ReadTomlWithoutVerbexTable(path, out bool found);
            if (!found)
                return false;

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);
            WriteTomlLines(path, lines);
            return true;
        }

        private static List<string> ReadTomlWithoutVerbexTable(string path, out bool found)
        {
            found = false;
            List<string> kept = new List<string>();
            if (!File.Exists(path))
                return kept;

            string tableName = "mcp_servers." + _ServerKey;
            bool skipping = false;

            foreach (string line in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                Match header = _TomlTableHeader.Match(line);
                if (header.Success)
                {
                    string name = header.Groups[1].Value.Replace("\"", string.Empty);
                    skipping = string.Equals(name, tableName, StringComparison.Ordinal)
                        || name.StartsWith(tableName + ".", StringComparison.Ordinal);
                    found = found || skipping;
                }

                if (!skipping)
                    kept.Add(line);
            }

            return kept;
        }

        private static void WriteTomlLines(string path, List<string> lines)
        {
            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        }

        private static bool RemoveLegacyCodexJsonEntry(string tomlPath)
        {
            // Earlier releases wrote the entry to config.json, which Codex never reads; clean it up.
            string? directory = Path.GetDirectoryName(tomlPath);
            if (string.IsNullOrEmpty(directory))
                return false;

            string legacyPath = Path.Combine(directory, "config.json");
            if (!File.Exists(legacyPath))
                return false;

            JsonObject root = LoadRoot(legacyPath);
            if (!RemoveMcpServersEntry(root))
                return false;

            File.WriteAllText(legacyPath, root.ToJsonString(_WriteOptions));
            Console.WriteLine("  Removed stale Verbex entry from " + legacyPath + " (Codex reads config.toml)");
            return true;
        }

        private static string? ResolveExistingPath(McpClientTarget client)
        {
            foreach (string candidate in client.ConfigPaths)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        private static JsonObject LoadRoot(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        JsonNode? parsed = JsonNode.Parse(json);
                        if (parsed is JsonObject existing)
                            return existing;
                    }
                    catch (JsonException)
                    {
                        // Corrupt or non-object config; fall through to a fresh document.
                    }
                }
            }

            return new JsonObject();
        }

        private static void ApplyMcpServersEntry(JsonObject root, McpConfigFormat format, string fullUrl)
        {
            JsonObject servers = GetOrCreateObject(root, "mcpServers");
            JsonObject entry = new JsonObject();

            switch (format)
            {
                case McpConfigFormat.McpServersTypeUrl:
                    entry["type"] = "http";
                    entry["url"] = fullUrl;
                    break;
                case McpConfigFormat.McpServersUrl:
                    entry["url"] = fullUrl;
                    break;
                case McpConfigFormat.McpServersHttpUrl:
                    entry["httpUrl"] = fullUrl;
                    break;
            }

            servers[_ServerKey] = entry;
        }

        private static bool RemoveMcpServersEntry(JsonObject root)
        {
            if (root["mcpServers"] is JsonObject servers && servers.ContainsKey(_ServerKey))
            {
                servers.Remove(_ServerKey);
                return true;
            }
            return false;
        }

        private static void ApplyMuxEntry(JsonObject root, string baseUrl)
        {
            JsonArray servers = GetOrCreateArray(root, "servers");
            RemoveNamedFromArray(servers, _ServerKey);

            JsonObject entry = new JsonObject
            {
                ["name"] = _ServerKey,
                ["transport"] = "http",
                ["url"] = baseUrl,
                ["mcpPath"] = _McpPath
            };

            servers.Add(entry);
        }

        private static bool RemoveMuxEntry(JsonObject root)
        {
            if (root["servers"] is JsonArray servers)
                return RemoveNamedFromArray(servers, _ServerKey);
            return false;
        }

        private static bool RemoveNamedFromArray(JsonArray servers, string name)
        {
            bool removed = false;
            for (int i = servers.Count - 1; i >= 0; i--)
            {
                if (servers[i] is JsonObject item
                    && item["name"] is JsonValue value
                    && value.TryGetValue(out string? itemName)
                    && string.Equals(itemName, name, StringComparison.Ordinal))
                {
                    servers.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }

        private static JsonObject GetOrCreateObject(JsonObject root, string property)
        {
            if (root[property] is JsonObject existing)
                return existing;

            JsonObject created = new JsonObject();
            root[property] = created;
            return created;
        }

        private static JsonArray GetOrCreateArray(JsonObject root, string property)
        {
            if (root[property] is JsonArray existing)
                return existing;

            JsonArray created = new JsonArray();
            root[property] = created;
            return created;
        }

        #endregion
    }
}
