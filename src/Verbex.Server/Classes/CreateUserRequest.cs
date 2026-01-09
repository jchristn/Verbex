namespace Verbex.Server.Classes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to create a new user.
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// User email.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User password.
        /// </summary>
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name.
        /// </summary>
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        /// <summary>
        /// Whether user is a tenant admin.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if invalid.</param>
        /// <returns>True if valid.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (String.IsNullOrEmpty(Email))
            {
                errorMessage = "Email is required";
                return false;
            }

            if (!Email.Contains("@"))
            {
                errorMessage = "Invalid email format";
                return false;
            }

            if (String.IsNullOrEmpty(Password))
            {
                errorMessage = "Password is required";
                return false;
            }

            if (Password.Length < 6)
            {
                errorMessage = "Password must be at least 6 characters";
                return false;
            }

            return true;
        }
    }
}
