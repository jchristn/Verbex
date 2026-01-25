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
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Tenants list response data.
    /// </summary>
    public class TenantsListData
    {
        [JsonPropertyName("tenants")]
        public List<TenantInfo> Tenants { get; set; } = new List<TenantInfo>();

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// User information.
    /// </summary>
    public class UserInfo
    {
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Users list response data.
    /// </summary>
    public class UsersListData
    {
        [JsonPropertyName("users")]
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Credential information.
    /// </summary>
    public class CredentialInfo
    {
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("bearerToken")]
        public string? BearerToken { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("createdUtc")]
        public DateTime? CreatedUtc { get; set; }

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Credentials list response data.
    /// </summary>
    public class CredentialsListData
    {
        [JsonPropertyName("credentials")]
        public List<CredentialInfo> Credentials { get; set; } = new List<CredentialInfo>();

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Create tenant request.
    /// </summary>
    public class CreateTenantRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

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
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

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
        [JsonPropertyName("description")]
        public string? Description { get; set; }

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
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tenant")]
        public TenantInfo? Tenant { get; set; }
    }

    /// <summary>
    /// Create user response data.
    /// </summary>
    public class CreateUserData
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("user")]
        public UserInfo? User { get; set; }
    }

    /// <summary>
    /// Create credential response data.
    /// </summary>
    public class CreateCredentialData
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("credential")]
        public CredentialInfo? Credential { get; set; }
    }

    /// <summary>
    /// Delete response data.
    /// </summary>
    public class DeleteData
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
