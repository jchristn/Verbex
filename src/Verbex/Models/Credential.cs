namespace Verbex.Models
{
    using System;
    using System.Security.Cryptography;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents an API credential (bearer token) for a user.
    /// </summary>
    /// <remarks>
    /// Credentials provide API access via bearer tokens. Each user can have multiple credentials
    /// for different purposes (e.g., production, testing, CI/CD).
    /// Bearer tokens are globally unique and used for authentication.
    /// </remarks>
    public class Credential
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "cred_";
        private const int BearerTokenLength = 64;

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _UserId = string.Empty;
        private string _BearerToken = string.Empty;
        private string _Name = string.Empty;
        private bool _Active = true;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the credential.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "cred_" prefix.
        /// Example: "cred_01ar5xxlajk1sxr6hzf29ksz4o".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this credential belongs to.
        /// </summary>
        /// <value>The identifier of the tenant. Must reference a valid tenant.</value>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the user ID this credential belongs to.
        /// </summary>
        /// <value>The identifier of the user. Must reference a valid user within the tenant.</value>
        [JsonPropertyName("userId")]
        public string UserId
        {
            get => _UserId;
            set => _UserId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the bearer token for API authentication.
        /// </summary>
        /// <value>
        /// A 64-character cryptographically secure random string.
        /// This token is globally unique across all tenants and users.
        /// </value>
        [JsonPropertyName("bearerToken")]
        public string BearerToken
        {
            get => _BearerToken;
            set => _BearerToken = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the display name for this credential.
        /// </summary>
        /// <value>
        /// An optional name to identify the purpose of this credential.
        /// Example: "Production API Key", "CI/CD Token".
        /// </value>
        [JsonPropertyName("name")]
        public string Name
        {
            get => _Name;
            set => _Name = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether the credential is active.
        /// </summary>
        /// <value>
        /// True if the credential can be used for authentication; false if disabled.
        /// Default is true.
        /// </value>
        [JsonPropertyName("active")]
        public bool Active
        {
            get => _Active;
            set => _Active = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the credential was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the credential was last updated.
        /// </summary>
        /// <value>The last update timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Credential"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "cred_" prefix.
        /// A bearer token is automatically generated.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public Credential()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _BearerToken = GenerateBearerToken();
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Credential"/> class with tenant and user.
        /// </summary>
        /// <param name="tenantId">The tenant ID this credential belongs to.</param>
        /// <param name="userId">The user ID this credential belongs to.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or userId is null or whitespace.</exception>
        public Credential(string tenantId, string userId) : this()
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException(nameof(userId), "User ID cannot be null or whitespace.");
            }

            _TenantId = tenantId;
            _UserId = userId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Credential"/> class with tenant, user, and name.
        /// </summary>
        /// <param name="tenantId">The tenant ID this credential belongs to.</param>
        /// <param name="userId">The user ID this credential belongs to.</param>
        /// <param name="name">The display name for this credential.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or userId is null or whitespace.</exception>
        public Credential(string tenantId, string userId, string name) : this(tenantId, userId)
        {
            _Name = name ?? string.Empty;
        }

        /// <summary>
        /// Regenerates the bearer token with a new cryptographically secure value.
        /// </summary>
        /// <remarks>
        /// Use this method to rotate credentials. The old bearer token will no longer be valid.
        /// </remarks>
        public void RegenerateBearerToken()
        {
            _BearerToken = GenerateBearerToken();
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Generates a cryptographically secure bearer token.
        /// </summary>
        /// <returns>A 64-character alphanumeric string.</returns>
        private static string GenerateBearerToken()
        {
            byte[] randomBytes = RandomNumberGenerator.GetBytes(48);
            string base64 = Convert.ToBase64String(randomBytes);

            string token = base64
                .Replace("+", "A")
                .Replace("/", "B")
                .Replace("=", "");

            if (token.Length > BearerTokenLength)
            {
                token = token.Substring(0, BearerTokenLength);
            }

            while (token.Length < BearerTokenLength)
            {
                byte[] extraBytes = RandomNumberGenerator.GetBytes(8);
                string extraBase64 = Convert.ToBase64String(extraBytes)
                    .Replace("+", "C")
                    .Replace("/", "D")
                    .Replace("=", "");
                token += extraBase64;
            }

            return token.Substring(0, BearerTokenLength);
        }
    }
}
