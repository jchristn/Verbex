namespace Verbex.Server.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to update a user.
    /// </summary>
    public class UpdateUserRequest
    {
        /// <summary>
        /// Optional email update.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>
        /// Optional password update (plain text, will be hashed).
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; }

        /// <summary>
        /// Optional first name update.
        /// </summary>
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        /// <summary>
        /// Optional last name update.
        /// </summary>
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        /// <summary>
        /// Optional admin status update.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool? IsAdmin { get; set; }

        /// <summary>
        /// Optional active status update.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; }
    }
}
