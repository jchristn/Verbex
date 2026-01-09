namespace Verbex.Server.Classes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to create a new tenant.
    /// </summary>
    public class CreateTenantRequest
    {
        /// <summary>
        /// Tenant name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description.
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

            if (String.IsNullOrEmpty(Name))
            {
                errorMessage = "Tenant name is required";
                return false;
            }

            if (Name.Length > 256)
            {
                errorMessage = "Tenant name must be 256 characters or less";
                return false;
            }

            return true;
        }
    }
}
