namespace Verbex.Sdk
{
    /// <summary>
    /// Login response data containing authentication token.
    /// </summary>
    public class LoginData
    {
        /// <summary>
        /// Bearer token for authenticated requests.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Authenticated username or email.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// The email address of the authenticated user.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Indicates whether the user is a tenant administrator.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Indicates whether the user is a global administrator.
        /// </summary>
        public bool IsGlobalAdmin { get; set; }
    }
}
