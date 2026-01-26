namespace Verbex.Sdk
{
    /// <summary>
    /// Token validation response data.
    /// </summary>
    public class ValidationData
    {
        /// <summary>
        /// Indicates whether the token is valid.
        /// </summary>
        public bool Valid { get; set; }

        /// <summary>
        /// The tenant identifier associated with the token.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The user identifier associated with the token.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// The email address associated with the token.
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
