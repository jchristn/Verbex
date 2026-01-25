namespace Verbex.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant information.
    /// </summary>
    public class TenantInfo
    {
        /// <summary>
        /// Unique identifier for the tenant.
        /// </summary>
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the tenant.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        /// <summary>
        /// UTC timestamp when the tenant was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Labels associated with the tenant.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags (key-value pairs) associated with the tenant.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Tenants list response data.
    /// </summary>
    public class TenantsListData
    {
        /// <summary>
        /// List of tenants.
        /// </summary>
        [JsonPropertyName("tenants")]
        public List<TenantInfo> Tenants { get; set; } = new List<TenantInfo>();

        /// <summary>
        /// Total count of tenants.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// User information.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Unique identifier for the user.
        /// </summary>
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier the user belongs to.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Email address of the user.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// First name of the user.
        /// </summary>
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name of the user.
        /// </summary>
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        /// <summary>
        /// Whether the user has administrator privileges.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        /// <summary>
        /// UTC timestamp when the user was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Labels associated with the user.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags (key-value pairs) associated with the user.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Users list response data.
    /// </summary>
    public class UsersListData
    {
        /// <summary>
        /// List of users.
        /// </summary>
        [JsonPropertyName("users")]
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();

        /// <summary>
        /// Total count of users.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Credential information.
    /// </summary>
    public class CredentialInfo
    {
        /// <summary>
        /// Unique identifier for the credential.
        /// </summary>
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier the credential belongs to.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the credential.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Bearer token for authentication.
        /// </summary>
        [JsonPropertyName("bearerToken")]
        public string? BearerToken { get; set; }

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        /// <summary>
        /// UTC timestamp when the credential was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Labels associated with the credential.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags (key-value pairs) associated with the credential.
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Credentials list response data.
    /// </summary>
    public class CredentialsListData
    {
        /// <summary>
        /// List of credentials.
        /// </summary>
        [JsonPropertyName("credentials")]
        public List<CredentialInfo> Credentials { get; set; } = new List<CredentialInfo>();

        /// <summary>
        /// Total count of credentials.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Create tenant request.
    /// </summary>
    public class CreateTenantRequest
    {
        /// <summary>
        /// Display name for the tenant.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the tenant.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateTenantRequest"/> class.
        /// </summary>
        /// <param name="name">Display name for the tenant.</param>
        /// <param name="description">Description of the tenant.</param>
        public CreateTenantRequest(string name, string? description = null)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Create user request.
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// Email address of the user.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Password for the user.
        /// </summary>
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// First name of the user.
        /// </summary>
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name of the user.
        /// </summary>
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        /// <summary>
        /// Whether the user has administrator privileges.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUserRequest"/> class.
        /// </summary>
        /// <param name="email">Email address of the user.</param>
        /// <param name="password">Password for the user.</param>
        /// <param name="firstName">First name of the user.</param>
        /// <param name="lastName">Last name of the user.</param>
        /// <param name="isAdmin">Whether the user has administrator privileges.</param>
        public CreateUserRequest(string email, string password, string? firstName = null, string? lastName = null, bool isAdmin = false)
        {
            Email = email;
            Password = password;
            FirstName = firstName;
            LastName = lastName;
            IsAdmin = isAdmin;
        }
    }

    /// <summary>
    /// Create credential request.
    /// </summary>
    public class CreateCredentialRequest
    {
        /// <summary>
        /// Description of the credential.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateCredentialRequest"/> class.
        /// </summary>
        /// <param name="description">Description of the credential.</param>
        public CreateCredentialRequest(string? description = null)
        {
            Description = description;
        }
    }

    /// <summary>
    /// Create tenant response data.
    /// </summary>
    public class CreateTenantData
    {
        /// <summary>
        /// Response message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Created tenant information.
        /// </summary>
        [JsonPropertyName("tenant")]
        public TenantInfo? Tenant { get; set; }
    }

    /// <summary>
    /// Create user response data.
    /// </summary>
    public class CreateUserData
    {
        /// <summary>
        /// Response message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Created user information.
        /// </summary>
        [JsonPropertyName("user")]
        public UserInfo? User { get; set; }
    }

    /// <summary>
    /// Create credential response data.
    /// </summary>
    public class CreateCredentialData
    {
        /// <summary>
        /// Response message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Created credential information.
        /// </summary>
        [JsonPropertyName("credential")]
        public CredentialInfo? Credential { get; set; }
    }

    /// <summary>
    /// Delete response data.
    /// </summary>
    public class DeleteData
    {
        /// <summary>
        /// Response message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
