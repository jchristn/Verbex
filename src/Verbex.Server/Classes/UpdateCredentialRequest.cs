namespace Verbex.Server.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to update a credential.
    /// </summary>
    public class UpdateCredentialRequest
    {
        /// <summary>
        /// Optional name update.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional active status update.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; }

        /// <summary>
        /// Optional description update.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
