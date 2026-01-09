namespace Verbex.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the context of an authentication attempt and its result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to pass authentication information through the request pipeline
    /// and to determine what actions the authenticated principal can perform.
    /// </para>
    /// <para>
    /// Authentication can be performed via:
    /// <list type="bullet">
    /// <item>Bearer token (via <see cref="BearerToken"/>)</item>
    /// <item>Email and password (via <see cref="Email"/> and <see cref="Password"/>)</item>
    /// <item>Admin email and password for global administrator access</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class AuthenticationContext
    {
        private string _TenantId = string.Empty;
        private string _Email = string.Empty;
        private string _Password = string.Empty;
        private string _PasswordSha256 = string.Empty;
        private string _BearerToken = string.Empty;
        private Administrator? _Administrator = null;
        private TenantMetadata? _Tenant = null;
        private UserMaster? _User = null;
        private Credential? _Credential = null;
        private AuthenticationResultEnum _Result = AuthenticationResultEnum.NotAuthenticated;
        private string _ErrorMessage = string.Empty;

        /// <summary>
        /// Gets or sets the tenant ID for tenant-scoped authentication.
        /// </summary>
        /// <value>
        /// The tenant identifier. Required for user authentication.
        /// Not required for global administrator authentication.
        /// </value>
        [JsonPropertyName("tenantId")]
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the email for authentication.
        /// </summary>
        /// <value>The email address of the user or administrator.</value>
        [JsonPropertyName("email")]
        public string Email
        {
            get => _Email;
            set => _Email = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the plain text password for authentication.
        /// </summary>
        /// <value>
        /// The plain text password. This should be cleared after authentication
        /// using <see cref="ClearSensitiveData"/>.
        /// </value>
        [JsonPropertyName("password")]
        public string Password
        {
            get => _Password;
            set => _Password = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the password.
        /// </summary>
        /// <value>
        /// The pre-computed password hash. Used when the client provides a hash
        /// instead of plain text password.
        /// </value>
        [JsonPropertyName("passwordSha256")]
        public string PasswordSha256
        {
            get => _PasswordSha256;
            set => _PasswordSha256 = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the bearer token for API authentication.
        /// </summary>
        /// <value>The bearer token from the Authorization header.</value>
        [JsonPropertyName("bearerToken")]
        public string BearerToken
        {
            get => _BearerToken;
            set => _BearerToken = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the authenticated administrator.
        /// </summary>
        /// <value>
        /// The administrator if authentication was successful with administrator credentials;
        /// null otherwise.
        /// </value>
        [JsonIgnore]
        public Administrator? Administrator
        {
            get => _Administrator;
            set => _Administrator = value;
        }

        /// <summary>
        /// Gets or sets the authenticated tenant.
        /// </summary>
        /// <value>
        /// The tenant if authentication was successful with tenant-scoped credentials;
        /// null otherwise.
        /// </value>
        [JsonIgnore]
        public TenantMetadata? Tenant
        {
            get => _Tenant;
            set => _Tenant = value;
        }

        /// <summary>
        /// Gets or sets the authenticated user.
        /// </summary>
        /// <value>
        /// The user if authentication was successful with user credentials;
        /// null otherwise.
        /// </value>
        [JsonIgnore]
        public UserMaster? User
        {
            get => _User;
            set => _User = value;
        }

        /// <summary>
        /// Gets or sets the credential used for authentication.
        /// </summary>
        /// <value>
        /// The credential if authentication was via bearer token;
        /// null otherwise.
        /// </value>
        [JsonIgnore]
        public Credential? Credential
        {
            get => _Credential;
            set => _Credential = value;
        }

        /// <summary>
        /// Gets or sets the result of the authentication attempt.
        /// </summary>
        /// <value>The authentication result. Default is <see cref="AuthenticationResultEnum.NotAuthenticated"/>.</value>
        [JsonPropertyName("result")]
        public AuthenticationResultEnum Result
        {
            get => _Result;
            set => _Result = value;
        }

        /// <summary>
        /// Gets or sets an error message describing authentication failure.
        /// </summary>
        /// <value>
        /// A human-readable error message when authentication fails;
        /// empty string on success.
        /// </value>
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage
        {
            get => _ErrorMessage;
            set => _ErrorMessage = value ?? string.Empty;
        }

        /// <summary>
        /// Gets whether authentication was successful.
        /// </summary>
        /// <value>True if <see cref="Result"/> is <see cref="AuthenticationResultEnum.Success"/>.</value>
        [JsonPropertyName("isAuthenticated")]
        public bool IsAuthenticated => _Result == AuthenticationResultEnum.Success;

        /// <summary>
        /// Gets whether the authenticated principal is a global administrator.
        /// </summary>
        /// <value>True if authentication was successful with administrator credentials.</value>
        [JsonPropertyName("isGlobalAdmin")]
        public bool IsGlobalAdmin => IsAuthenticated && _Administrator != null;

        /// <summary>
        /// Gets whether the authenticated principal is a tenant administrator.
        /// </summary>
        /// <value>True if authentication was successful and the user has IsAdmin flag set.</value>
        [JsonPropertyName("isTenantAdmin")]
        public bool IsTenantAdmin => IsAuthenticated && _User != null && _User.IsAdmin;

        /// <summary>
        /// Gets whether the authenticated principal has any administrative privileges.
        /// </summary>
        /// <value>True if the principal is either a global admin or a tenant admin.</value>
        [JsonPropertyName("hasAdminPrivileges")]
        public bool HasAdminPrivileges => IsGlobalAdmin || IsTenantAdmin;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationContext"/> class.
        /// </summary>
        public AuthenticationContext()
        {
        }

        /// <summary>
        /// Determines whether the authenticated principal can access the specified tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID to check access for.</param>
        /// <returns>
        /// True if the principal is a global admin or belongs to the specified tenant.
        /// </returns>
        public bool CanAccessTenant(string tenantId)
        {
            if (!IsAuthenticated)
            {
                return false;
            }

            if (IsGlobalAdmin)
            {
                return true;
            }

            if (_Tenant != null && string.Equals(_Tenant.Identifier, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_User != null && string.Equals(_User.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the authenticated principal can manage the specified tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID to check management access for.</param>
        /// <returns>
        /// True if the principal is a global admin or a tenant admin for the specified tenant.
        /// </returns>
        public bool CanManageTenant(string tenantId)
        {
            if (!IsAuthenticated)
            {
                return false;
            }

            if (IsGlobalAdmin)
            {
                return true;
            }

            if (IsTenantAdmin)
            {
                if (_User != null && string.Equals(_User.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clears sensitive data (passwords) from the context.
        /// </summary>
        /// <remarks>
        /// Call this method after authentication is complete to prevent
        /// sensitive data from being inadvertently exposed.
        /// </remarks>
        public void ClearSensitiveData()
        {
            _Password = string.Empty;
            _PasswordSha256 = string.Empty;
            _BearerToken = string.Empty;
        }

        /// <summary>
        /// Creates a success result for administrator authentication.
        /// </summary>
        /// <param name="administrator">The authenticated administrator.</param>
        /// <returns>An authentication context indicating success.</returns>
        public static AuthenticationContext CreateAdminSuccess(Administrator administrator)
        {
            return new AuthenticationContext
            {
                Administrator = administrator,
                Result = AuthenticationResultEnum.Success
            };
        }

        /// <summary>
        /// Creates a success result for user authentication.
        /// </summary>
        /// <param name="tenant">The tenant the user belongs to.</param>
        /// <param name="user">The authenticated user.</param>
        /// <param name="credential">The credential used (if bearer token auth).</param>
        /// <returns>An authentication context indicating success.</returns>
        public static AuthenticationContext CreateUserSuccess(TenantMetadata tenant, UserMaster user, Credential? credential = null)
        {
            return new AuthenticationContext
            {
                Tenant = tenant,
                User = user,
                Credential = credential,
                TenantId = tenant.Identifier,
                Result = AuthenticationResultEnum.Success
            };
        }

        /// <summary>
        /// Creates a failure result with the specified error.
        /// </summary>
        /// <param name="result">The failure result type.</param>
        /// <param name="errorMessage">A description of the failure.</param>
        /// <returns>An authentication context indicating failure.</returns>
        public static AuthenticationContext CreateFailure(AuthenticationResultEnum result, string errorMessage)
        {
            return new AuthenticationContext
            {
                Result = result,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }
}
