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
        /// Authenticated username.
        /// </summary>
        public string? Username { get; set; }
    }
}
