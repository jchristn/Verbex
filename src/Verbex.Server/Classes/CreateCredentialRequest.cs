namespace Verbex.Server.Classes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to create a new credential (API key).
    /// </summary>
    public class CreateCredentialRequest
    {
        /// <summary>
        /// Optional description for this credential.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if invalid.</param>
        /// <returns>True if valid.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }
    }
}
