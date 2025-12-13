namespace VerbexCli.Infrastructure
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Manages global VerbexCli configuration that persists across commands
    /// </summary>
    public static class GlobalConfig
    {
        private static readonly string _GlobalConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vbxconfig");

        /// <summary>
        /// Configuration structure for global settings
        /// </summary>
        private class Config
        {
            public string? ConfigDirectory { get; set; }
        }

        /// <summary>
        /// Gets the persisted config directory, or null if none is set
        /// </summary>
        /// <returns>Custom config directory path, or null for default</returns>
        public static string? GetConfigDirectory()
        {
            try
            {
                if (!File.Exists(_GlobalConfigPath))
                {
                    return null;
                }

                string json = File.ReadAllText(_GlobalConfigPath);
                Config? config = JsonSerializer.Deserialize<Config>(json);
                return config?.ConfigDirectory;
            }
            catch
            {
                // If there's any error reading the global config, fall back to default
                return null;
            }
        }

        /// <summary>
        /// Sets the config directory preference to persist across commands
        /// </summary>
        /// <param name="configDirectory">Directory path to persist, or null to clear</param>
        public static void SetConfigDirectory(string? configDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(configDirectory))
                {
                    // Clear the global config
                    if (File.Exists(_GlobalConfigPath))
                    {
                        File.Delete(_GlobalConfigPath);
                    }
                    return;
                }

                Config config = new Config
                {
                    ConfigDirectory = configDirectory
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_GlobalConfigPath, json);
            }
            catch
            {
                // Silently ignore errors saving global config - don't break the command
            }
        }

        /// <summary>
        /// Gets the effective config directory (custom if set, otherwise default)
        /// </summary>
        /// <returns>Directory path to use for configuration</returns>
        public static string GetEffectiveConfigDirectory()
        {
            string? customDir = GetConfigDirectory();
            if (!string.IsNullOrEmpty(customDir))
            {
                return customDir;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vbx");
        }
    }
}