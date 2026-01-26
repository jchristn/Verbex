namespace Verbex.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies the result of an authorization check.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthorizationResultEnum
    {
        /// <summary>
        /// Authorization was successful; the user has access to the requested resource.
        /// </summary>
        Authorized,

        /// <summary>
        /// Authorization failed; the user is not authorized.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The user is authenticated but lacks sufficient privileges for this operation.
        /// </summary>
        InsufficientPrivileges,

        /// <summary>
        /// The requested resource was not found.
        /// </summary>
        ResourceNotFound,

        /// <summary>
        /// Access to the requested resource is denied.
        /// </summary>
        AccessDenied
    }
}
