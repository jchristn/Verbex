namespace Verbex.Server.Services
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Database.Interfaces;
    using Verbex.Models;
    using Verbex.Server.Classes;

    /// <summary>
    /// Authentication service that validates credentials against the database.
    /// </summary>
    public class AuthenticationService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly string _AdminBearerToken;
        private readonly DatabaseDriverBase _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="adminBearerToken">Fallback admin bearer token.</param>
        /// <param name="database">Database driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameter is null.</exception>
        public AuthenticationService(string adminBearerToken, DatabaseDriverBase database)
        {
            _AdminBearerToken = adminBearerToken ?? throw new ArgumentNullException(nameof(adminBearerToken));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Authenticate using bearer token.
        /// Returns the authentication context if valid.
        /// </summary>
        /// <param name="token">Token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Authentication context or null if invalid.</returns>
        public async Task<AuthContext?> AuthenticateBearerAsync(string token, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(token)) return null;

            // Check fallback admin token
            if (String.Equals(token, _AdminBearerToken, StringComparison.InvariantCultureIgnoreCase))
            {
                return new AuthContext
                {
                    IsAuthenticated = true,
                    IsGlobalAdmin = true,
                    Token = token
                };
            }

            // Check credentials table for API keys
            Credential? credential = await _Database.Credentials.ReadByBearerTokenAsync(token, cancellationToken).ConfigureAwait(false);
            if (credential != null && credential.Active)
            {
                // Look up the user to check if they're a tenant admin
                bool isTenantAdmin = false;
                string? email = null;
                if (!String.IsNullOrEmpty(credential.UserId))
                {
                    UserMaster? user = await _Database.Users.ReadByIdentifierAsync(credential.TenantId, credential.UserId, cancellationToken).ConfigureAwait(false);
                    if (user != null)
                    {
                        isTenantAdmin = user.IsAdmin;
                        email = user.Email;
                    }
                }

                return new AuthContext
                {
                    IsAuthenticated = true,
                    IsGlobalAdmin = false,
                    IsTenantAdmin = isTenantAdmin,
                    TenantId = credential.TenantId,
                    CredentialId = credential.Identifier,
                    UserId = credential.UserId,
                    Email = email,
                    Token = token
                };
            }

            return null;
        }

        /// <summary>
        /// Authenticate using bearer token (sync wrapper).
        /// </summary>
        /// <param name="token">Token.</param>
        /// <returns>Boolean indicating if authentication succeeded.</returns>
        public bool AuthenticateBearer(string token)
        {
            AuthContext? ctx = AuthenticateBearerAsync(token).GetAwaiter().GetResult();
            return ctx != null && ctx.IsAuthenticated;
        }

        /// <summary>
        /// Authenticate administrator with email and password.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Administrator record if valid, null otherwise.</returns>
        public async Task<Administrator?> AuthenticateAdminAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(email) || String.IsNullOrEmpty(password)) return null;

            Administrator? admin = await _Database.Administrators.ReadByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (admin == null || !admin.Active) return null;

            string passwordHash = HashPassword(password);
            if (String.Equals(admin.PasswordSha256, passwordHash, StringComparison.OrdinalIgnoreCase))
            {
                return admin;
            }

            return null;
        }

        /// <summary>
        /// Authenticate user with email and password within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>User record if valid, null otherwise.</returns>
        public async Task<UserMaster?> AuthenticateUserAsync(string tenantId, string email, string password, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(email) || String.IsNullOrEmpty(password)) return null;

            UserMaster? user = await _Database.Users.ReadByEmailAsync(tenantId, email, cancellationToken).ConfigureAwait(false);
            if (user == null || !user.Active) return null;

            string passwordHash = HashPassword(password);
            if (String.Equals(user.PasswordSha256, passwordHash, StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }

            return null;
        }

        /// <summary>
        /// Generate a simple authentication token.
        /// </summary>
        /// <param name="identifier">Identifier.</param>
        /// <returns>Token.</returns>
        public string GenerateToken(string identifier)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string data = identifier + ":" + timestamp;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Hash a password using SHA-256.
        /// </summary>
        /// <param name="password">Password to hash.</param>
        /// <returns>Hexadecimal hash string.</returns>
        public static string HashPassword(string password)
        {
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = SHA256.HashData(passwordBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Validate a token format.
        /// </summary>
        /// <param name="token">Token.</param>
        /// <returns>Boolean indicating if token format is valid.</returns>
        public bool ValidateTokenFormat(string token)
        {
            if (String.IsNullOrEmpty(token)) return false;

            try
            {
                Convert.FromBase64String(token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }

    /// <summary>
    /// Authentication context containing identity information.
    /// </summary>
    public class AuthContext
    {
        /// <summary>
        /// Whether the authentication was successful.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>
        /// Whether this is a global administrator (not tenant-scoped).
        /// </summary>
        public bool IsGlobalAdmin { get; set; } = false;

        /// <summary>
        /// Tenant ID if tenant-scoped.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User ID if user-authenticated.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Credential ID if API key authenticated.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// The token used for authentication.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Whether this user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// The user's email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Whether this context has tenant access (either global admin or has tenant ID).
        /// </summary>
        public bool HasTenantAccess => IsGlobalAdmin || !String.IsNullOrEmpty(TenantId);

        /// <summary>
        /// Whether this context has admin privileges (either global admin or tenant admin).
        /// </summary>
        public bool HasAdminAccess => IsGlobalAdmin || IsTenantAdmin;
    }
}
