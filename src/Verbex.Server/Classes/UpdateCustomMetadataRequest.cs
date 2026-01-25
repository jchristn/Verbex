namespace Verbex.Server.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to update custom metadata for an index or document.
    /// </summary>
    public class UpdateCustomMetadataRequest
    {
        #region Public-Members

        /// <summary>
        /// Gets or sets the custom metadata.
        /// Can be any JSON-serializable value (object, array, string, number, boolean, null).
        /// </summary>
        [JsonPropertyName("customMetadata")]
        public object? CustomMetadata
        {
            get => _CustomMetadata;
            set => _CustomMetadata = value;
        }

        #endregion

        #region Private-Members

        private object? _CustomMetadata;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UpdateCustomMetadataRequest()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validates the request.
        /// </summary>
        /// <returns>True if valid.</returns>
        public bool IsValid()
        {
            return true; // CustomMetadata can be any value including null
        }

        #endregion
    }
}
