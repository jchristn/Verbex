namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;
    using Verbex;
    using Verbex.Models;

    /// <summary>
    /// Request to create a new index.
    /// </summary>
    public class CreateIndexRequest
    {
        #region Public-Members

        /// <summary>
        /// Optional custom identifier for the index.
        /// If not provided, a unique identifier will be auto-generated.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? "";
        }

        /// <summary>
        /// Tenant ID for the index (required for global admins, ignored for tenant users).
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? "";
        }

        /// <summary>
        /// Display name for the index.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? "";
        }

        /// <summary>
        /// Description of the index.
        /// </summary>
        public string Description
        {
            get => _Description;
            set => _Description = value ?? "";
        }

        /// <summary>
        /// Whether this index should be in-memory only (SQLite).
        /// </summary>
        /// <remarks>When true, index data is not persisted and will be lost on restart.</remarks>
        public bool InMemory
        {
            get => _InMemory;
            set => _InMemory = value;
        }

        /// <summary>
        /// Whether to enable lemmatization.
        /// </summary>
        public bool EnableLemmatizer { get; set; } = false;

        /// <summary>
        /// Whether to enable stop word removal.
        /// </summary>
        public bool EnableStopWordRemover { get; set; } = false;

        /// <summary>
        /// Minimum token length (0 = disabled).
        /// </summary>
        public int MinTokenLength
        {
            get => _MinTokenLength;
            set => _MinTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Maximum token length (0 = disabled).
        /// </summary>
        public int MaxTokenLength
        {
            get => _MaxTokenLength;
            set => _MaxTokenLength = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Labels for categorizing the index.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Custom tags (key-value pairs) for the index.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Custom metadata for the index.
        /// Can be any JSON-serializable value (object, array, string, number, boolean, null).
        /// </summary>
        public object? CustomMetadata
        {
            get => _CustomMetadata;
            set => _CustomMetadata = value;
        }

        /// <summary>
        /// Cache configuration for the index.
        /// When null, caching is disabled.
        /// </summary>
        public CacheConfiguration? CacheConfiguration
        {
            get => _CacheConfiguration;
            set => _CacheConfiguration = value;
        }

        #endregion

        #region Private-Members

        private string _Identifier = "";
        private string _TenantId = "";
        private string _Name = "";
        private string _Description = "";
        private bool _InMemory = false;
        private int _MinTokenLength = 0;
        private int _MaxTokenLength = 0;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private object? _CustomMetadata = null;
        private CacheConfiguration? _CacheConfiguration = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CreateIndexRequest()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            if (String.IsNullOrEmpty(_Name))
            {
                errorMessage = "Name is required";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// Convert to IndexMetadata.
        /// </summary>
        /// <param name="tenantId">The tenant ID this index belongs to.</param>
        /// <returns>IndexMetadata instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId is null or empty.</exception>
        public IndexMetadata ToIndexMetadata(string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID is required");
            }

            IndexMetadata metadata = new IndexMetadata(tenantId, _Name, _Description)
            {
                InMemory = _InMemory,
                EnableLemmatizer = EnableLemmatizer,
                EnableStopWordRemover = EnableStopWordRemover,
                MinTokenLength = _MinTokenLength,
                MaxTokenLength = _MaxTokenLength,
                Labels = _Labels,
                Tags = _Tags,
                CustomMetadata = _CustomMetadata,
                CacheConfiguration = _CacheConfiguration
            };

            // Use provided identifier if specified
            if (!String.IsNullOrEmpty(_Identifier))
            {
                metadata.Identifier = _Identifier;
            }

            return metadata;
        }

        #endregion
    }
}
