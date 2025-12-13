namespace Verbex
{
    using System;
    using System.IO;

    /// <summary>
    /// Configuration settings for the Verbex inverted index.
    /// </summary>
    public class VerbexConfiguration
    {
        private StorageMode _StorageMode = StorageMode.InMemory;
        private string? _StorageDirectory;
        private string _DatabaseFilename = "index.db";
        private int _MinTokenLength;
        private int _MaxTokenLength;
        private int _DefaultMaxSearchResults = 100;
        private double _PhraseSearchBonus = 2.0;
        private double _SigmoidNormalizationDivisor = 10.0;
        private char[] _TokenizationDelimiters = { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?' };

        /// <summary>
        /// Gets or sets the storage mode for the index.
        /// Default: InMemory
        /// </summary>
        public StorageMode StorageMode
        {
            get { return _StorageMode; }
            set { _StorageMode = value; }
        }

        /// <summary>
        /// Gets or sets the storage directory for disk-based storage mode.
        /// Only used when StorageMode is OnDisk.
        /// If null, the database will be created in the default location.
        /// </summary>
        public string? StorageDirectory
        {
            get { return _StorageDirectory; }
            set { _StorageDirectory = value; }
        }

        /// <summary>
        /// Gets or sets the SQLite database filename.
        /// Default: "index.db"
        /// </summary>
        public string DatabaseFilename
        {
            get { return _DatabaseFilename; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Database filename cannot be null or empty.", nameof(value));
                }
                _DatabaseFilename = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum token length. Tokens shorter than this will be excluded.
        /// Valid range: 0 to Int32.MaxValue. Set to 0 to disable minimum length filtering.
        /// Default: 0 (disabled)
        /// </summary>
        public int MinTokenLength
        {
            get { return _MinTokenLength; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Minimum token length cannot be negative.");
                }
                _MinTokenLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum token length. Tokens longer than this will be excluded.
        /// Valid range: 0 to Int32.MaxValue. Set to 0 to disable maximum length filtering.
        /// Default: 0 (disabled)
        /// </summary>
        public int MaxTokenLength
        {
            get { return _MaxTokenLength; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Maximum token length cannot be negative.");
                }
                _MaxTokenLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the lemmatizer instance for reducing words to their base forms.
        /// If null, no lemmatization will be performed.
        /// </summary>
        public ILemmatizer? Lemmatizer { get; set; }

        /// <summary>
        /// Gets or sets the stop word remover instance for filtering common words.
        /// If null, no stop word filtering will be performed.
        /// </summary>
        public IStopWordRemover? StopWordRemover { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer instance for splitting text into tokens.
        /// If null, the default tokenizer will be used.
        /// </summary>
        public ITokenizer? Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets the default maximum search results.
        /// Default: 100
        /// </summary>
        public int DefaultMaxSearchResults
        {
            get { return _DefaultMaxSearchResults; }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Default max search results must be at least 1.");
                }
                _DefaultMaxSearchResults = value;
            }
        }

        /// <summary>
        /// Gets or sets the phrase search scoring bonus multiplier.
        /// Default: 2.0
        /// </summary>
        public double PhraseSearchBonus
        {
            get { return _PhraseSearchBonus; }
            set
            {
                if (value <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Phrase search bonus must be positive.");
                }
                _PhraseSearchBonus = value;
            }
        }

        /// <summary>
        /// Gets or sets the sigmoid normalization divisor for score calculation.
        /// Default: 10.0
        /// </summary>
        public double SigmoidNormalizationDivisor
        {
            get { return _SigmoidNormalizationDivisor; }
            set
            {
                if (value <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Sigmoid normalization divisor must be positive.");
                }
                _SigmoidNormalizationDivisor = value;
            }
        }

        /// <summary>
        /// Gets or sets the tokenization delimiters.
        /// Default: space, tab, newline, carriage return, period, comma, semicolon, colon, exclamation, question mark.
        /// </summary>
        public char[] TokenizationDelimiters
        {
            get { return _TokenizationDelimiters; }
            set
            {
                if (value == null || value.Length == 0)
                {
                    throw new ArgumentException("Tokenization delimiters cannot be null or empty.", nameof(value));
                }
                _TokenizationDelimiters = value;
            }
        }

        /// <summary>
        /// Gets the full database path for disk-based storage.
        /// </summary>
        /// <returns>Full path to the database file.</returns>
        public string GetDatabasePath()
        {
            if (string.IsNullOrEmpty(_StorageDirectory))
            {
                return _DatabaseFilename;
            }
            return Path.Combine(_StorageDirectory, _DatabaseFilename);
        }

        /// <summary>
        /// Gets the default storage directory for the Verbex index.
        /// Returns the path to .vbx/indices/{indexName}/ in the user's profile directory.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <returns>Full path to the default storage directory.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexName is null.</exception>
        /// <exception cref="ArgumentException">Thrown when indexName is empty or whitespace.</exception>
        public static string GetDefaultStorageDirectory(string indexName)
        {
            ArgumentNullException.ThrowIfNull(indexName);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty or whitespace.", nameof(indexName));
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".vbx", "indices", indexName);
        }

        /// <summary>
        /// Creates a configuration with default settings for in-memory storage.
        /// </summary>
        /// <returns>Configuration for in-memory index.</returns>
        public static VerbexConfiguration CreateInMemory()
        {
            return new VerbexConfiguration
            {
                StorageMode = StorageMode.InMemory
            };
        }

        /// <summary>
        /// Creates a configuration with default settings for disk-based storage.
        /// </summary>
        /// <param name="indexName">Name of the index (used to create storage directory).</param>
        /// <returns>Configuration for disk-based index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when indexName is null.</exception>
        /// <exception cref="ArgumentException">Thrown when indexName is empty or whitespace.</exception>
        public static VerbexConfiguration CreateOnDisk(string indexName)
        {
            return new VerbexConfiguration
            {
                StorageMode = StorageMode.OnDisk,
                StorageDirectory = GetDefaultStorageDirectory(indexName)
            };
        }

        /// <summary>
        /// Creates a configuration with default settings for disk-based storage at a custom path.
        /// </summary>
        /// <param name="storageDirectory">Directory to store the database.</param>
        /// <param name="databaseFilename">Database filename (default: "index.db").</param>
        /// <returns>Configuration for disk-based index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when storageDirectory is null.</exception>
        /// <exception cref="ArgumentException">Thrown when storageDirectory is empty or whitespace.</exception>
        public static VerbexConfiguration CreateOnDisk(string storageDirectory, string databaseFilename = "index.db")
        {
            ArgumentNullException.ThrowIfNull(storageDirectory);

            if (string.IsNullOrWhiteSpace(storageDirectory))
            {
                throw new ArgumentException("Storage directory cannot be empty or whitespace.", nameof(storageDirectory));
            }

            return new VerbexConfiguration
            {
                StorageMode = StorageMode.OnDisk,
                StorageDirectory = storageDirectory,
                DatabaseFilename = databaseFilename
            };
        }

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when configuration values are invalid.</exception>
        public void Validate()
        {
            if (_DefaultMaxSearchResults < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(DefaultMaxSearchResults), "Default max search results must be at least 1.");
            }

            if (_PhraseSearchBonus <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(PhraseSearchBonus), "Phrase search bonus must be positive.");
            }

            if (_SigmoidNormalizationDivisor <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(SigmoidNormalizationDivisor), "Sigmoid normalization divisor must be positive.");
            }

            if (_TokenizationDelimiters == null || _TokenizationDelimiters.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TokenizationDelimiters), "Tokenization delimiters cannot be null or empty.");
            }

            if (_MinTokenLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MinTokenLength), "Minimum token length cannot be negative.");
            }

            if (_MaxTokenLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxTokenLength), "Maximum token length cannot be negative.");
            }

            if (_MaxTokenLength > 0 && _MinTokenLength > _MaxTokenLength)
            {
                throw new ArgumentOutOfRangeException(nameof(MinTokenLength), "Minimum token length cannot be greater than maximum token length.");
            }

            if (_StorageMode == StorageMode.OnDisk && string.IsNullOrWhiteSpace(_StorageDirectory))
            {
                throw new ArgumentException("Storage directory must be specified for OnDisk storage mode.", nameof(StorageDirectory));
            }
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new configuration instance with the same values.</returns>
        public VerbexConfiguration Clone()
        {
            return new VerbexConfiguration
            {
                StorageMode = _StorageMode,
                StorageDirectory = _StorageDirectory,
                DatabaseFilename = _DatabaseFilename,
                MinTokenLength = _MinTokenLength,
                MaxTokenLength = _MaxTokenLength,
                Lemmatizer = Lemmatizer,
                StopWordRemover = StopWordRemover,
                Tokenizer = Tokenizer,
                DefaultMaxSearchResults = _DefaultMaxSearchResults,
                PhraseSearchBonus = _PhraseSearchBonus,
                SigmoidNormalizationDivisor = _SigmoidNormalizationDivisor,
                TokenizationDelimiters = (char[])_TokenizationDelimiters.Clone()
            };
        }
    }
}
