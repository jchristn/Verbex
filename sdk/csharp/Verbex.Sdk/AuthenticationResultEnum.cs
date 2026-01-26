namespace Verbex.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies the result of an authentication attempt.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthenticationResultEnum
    {
        /// <summary>
        /// Authentication was successful.
        /// </summary>
        Success,

        /// <summary>
        /// No authentication was attempted (no credentials provided).
        /// </summary>
        NotAuthenticated,

        /// <summary>
        /// Required credentials were missing from the request.
        /// </summary>
        MissingCredentials,

        /// <summary>
        /// The specified user, tenant, or administrator was not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The account exists but is inactive (disabled).
        /// </summary>
        Inactive,

        /// <summary>
        /// The provided credentials (password or bearer token) were invalid.
        /// </summary>
        InvalidCredentials,

        /// <summary>
        /// The specified tenant was not found.
        /// </summary>
        TenantNotFound,

        /// <summary>
        /// The tenant exists but is inactive (disabled).
        /// </summary>
        TenantInactive,

        /// <summary>
        /// The user does not have permission to access the requested tenant.
        /// </summary>
        TenantAccessDenied
    }
}
