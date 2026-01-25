namespace Verbex.Models
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json.Serialization;
    using PrettyId;

    /// <summary>
    /// Represents a user within a tenant.
    /// </summary>
    /// <remarks>
    /// Users belong to a specific tenant and can have multiple credentials for API access.
    /// Passwords are stored as SHA-256 hashes.
    /// </remarks>
    public class UserMaster
    {
        private static readonly IdGenerator _IdGenerator = new IdGenerator();
        private const int TotalIdLength = 48;
        private const string IdPrefix = "usr_";

        private string _Identifier = string.Empty;
        private string _TenantId = string.Empty;
        private string _Email = string.Empty;
        private string _PasswordSha256 = string.Empty;
        private string _FirstName = string.Empty;
        private string _LastName = string.Empty;
        private bool _IsAdmin = false;
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        /// <value>
        /// A k-sortable unique identifier with "usr_" prefix.
        /// Example: "usr_01ar5xxlajk1sxr6hzf29ksz4o".
        /// </value>
        [JsonPropertyName("identifier")]
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the tenant ID this user belongs to.
        /// </summary>
        /// <value>The identifier of the tenant. Must reference a valid tenant.</value>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        /// <value>The email address. Must be unique within the tenant.</value>
        [JsonPropertyName("email")]
        public string Email
        {
            get => _Email;
            set => _Email = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the user's password.
        /// </summary>
        /// <value>
        /// A 64-character hexadecimal string representing the SHA-256 hash.
        /// Use <see cref="SetPassword"/> to set from plain text.
        /// </value>
        [JsonPropertyName("passwordSha256")]
        public string PasswordSha256
        {
            get => _PasswordSha256;
            set => _PasswordSha256 = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the user's first name.
        /// </summary>
        /// <value>The first name. Optional.</value>
        [JsonPropertyName("firstName")]
        public string FirstName
        {
            get => _FirstName;
            set => _FirstName = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the user's last name.
        /// </summary>
        /// <value>The last name. Optional.</value>
        [JsonPropertyName("lastName")]
        public string LastName
        {
            get => _LastName;
            set => _LastName = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether the user is a tenant administrator.
        /// </summary>
        /// <value>
        /// True if the user has administrative privileges within the tenant; false otherwise.
        /// Tenant admins can manage users and credentials within their tenant.
        /// </value>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin
        {
            get => _IsAdmin;
            set => _IsAdmin = value;
        }

        /// <summary>
        /// Gets or sets whether the user is active.
        /// </summary>
        /// <value>
        /// True if the user account is active and can authenticate; false if disabled.
        /// Default is true.
        /// </value>
        [JsonPropertyName("active")]
        public bool Active
        {
            get => _Active;
            set => _Active = value;
        }

        /// <summary>
        /// Gets or sets the labels for this user.
        /// </summary>
        /// <value>A list of string labels for categorization and filtering.</value>
        [JsonPropertyName("labels")]
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Gets or sets the tags for this user.
        /// </summary>
        /// <value>A dictionary of key-value pairs for rich metadata.</value>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the user was created.
        /// </summary>
        /// <value>The creation timestamp in UTC.</value>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp when the user was last updated.
        /// </summary>
        /// <value>The last update timestamp in UTC.</value>
        [JsonPropertyName("lastUpdateUtc")]
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMaster"/> class.
        /// </summary>
        /// <remarks>
        /// The identifier is automatically generated using a k-sortable ID with "usr_" prefix.
        /// Timestamps are set to the current UTC time.
        /// </remarks>
        public UserMaster()
        {
            _Identifier = _IdGenerator.GenerateKSortable(IdPrefix, TotalIdLength - IdPrefix.Length);
            _CreatedUtc = DateTime.UtcNow;
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMaster"/> class with tenant and email.
        /// </summary>
        /// <param name="tenantId">The tenant ID this user belongs to.</param>
        /// <param name="email">The user's email address.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or email is null or whitespace.</exception>
        public UserMaster(string tenantId, string email) : this()
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentNullException(nameof(email), "Email cannot be null or whitespace.");
            }

            _TenantId = tenantId;
            _Email = email;
        }

        /// <summary>
        /// Sets the user's password by computing and storing its SHA-256 hash.
        /// </summary>
        /// <param name="plainTextPassword">The plain text password to hash.</param>
        /// <exception cref="ArgumentNullException">Thrown when password is null or whitespace.</exception>
        public void SetPassword(string plainTextPassword)
        {
            if (string.IsNullOrWhiteSpace(plainTextPassword))
            {
                throw new ArgumentNullException(nameof(plainTextPassword), "Password cannot be null or whitespace.");
            }

            _PasswordSha256 = ComputePasswordHash(plainTextPassword);
            _LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Verifies a plain text password against the stored hash.
        /// </summary>
        /// <param name="plainTextPassword">The plain text password to verify.</param>
        /// <returns>True if the password matches; false otherwise.</returns>
        public bool VerifyPassword(string plainTextPassword)
        {
            if (string.IsNullOrEmpty(plainTextPassword))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_PasswordSha256))
            {
                return false;
            }

            string hash = ComputePasswordHash(plainTextPassword);
            return string.Equals(_PasswordSha256, hash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Computes the SHA-256 hash of a password.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>A 64-character hexadecimal string representing the SHA-256 hash.</returns>
        public static string ComputePasswordHash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = SHA256.HashData(passwordBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
