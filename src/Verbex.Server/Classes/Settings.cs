namespace Verbex.Server.Classes
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Settings.
    /// </summary>
    public class Settings
    {
        #region Public-Members

        /// <summary>
        /// Settings creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                if (value == null) _Logging = new LoggingSettings();
                else _Logging = value;
            }
        }

        /// <summary>
        /// REST settings.
        /// </summary>
        public RestSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                if (value == null) _Rest = new RestSettings();
                else _Rest = value;
            }
        }

        /// <summary>
        /// Directory where index data is stored.
        /// Each subdirectory represents an index and contains an index.json metadata file.
        /// </summary>
        public string DataDirectory
        {
            get
            {
                return _DataDirectory;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _DataDirectory = "./data";
                else _DataDirectory = value;
            }
        }

        /// <summary>
        /// Admin bearer token for administrative operations.
        /// </summary>
        public string AdminBearerToken
        {
            get
            {
                return _AdminBearerToken;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(AdminBearerToken));
                _AdminBearerToken = value;
            }
        }

        /// <summary>
        /// Debug settings.
        /// </summary>
        public DebugSettings Debug
        {
            get
            {
                return _Debug;
            }
            set
            {
                if (value == null) _Debug = new DebugSettings();
                else _Debug = value;
            }
        }

        #endregion

        #region Private-Members

        private LoggingSettings _Logging = new LoggingSettings();
        private RestSettings _Rest = new RestSettings();
        private string _DataDirectory = "./data";
        private string _AdminBearerToken = "verbexadmin";
        private DebugSettings _Debug = new DebugSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Settings()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load settings from file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns>Settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public static Settings FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            if (!File.Exists(filename))
            {
                Settings defaultSettings = new Settings();
                defaultSettings.ToFile(filename);
                return defaultSettings;
            }

            string json = File.ReadAllText(filename);

            Settings? settings = JsonSerializer.Deserialize<Settings>(json, GetJsonSerializerOptions());

            return settings ?? new Settings();
        }

        /// <summary>
        /// Save settings to file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            string json = JsonSerializer.Serialize(this, GetJsonSerializerOptions());

            File.WriteAllText(filename, json);
        }

        /// <summary>
        /// Gets the JSON serializer options used for settings serialization.
        /// Uses PascalCase property naming (default .NET behavior).
        /// </summary>
        /// <returns>JSON serializer options.</returns>
        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };
        }

        #endregion
    }
}