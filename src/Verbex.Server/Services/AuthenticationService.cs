namespace Verbex.Server.Services
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Verbex.Server.Classes;

    /// <summary>
    /// Authentication service.
    /// </summary>
    public class AuthenticationService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string _AdminBearerToken = "";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="adminBearerToken">Admin bearer token.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameter is null.</exception>
        public AuthenticationService(string adminBearerToken)
        {
            _AdminBearerToken = adminBearerToken ?? throw new ArgumentNullException(nameof(adminBearerToken));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Authenticate using bearer token.
        /// </summary>
        /// <param name="token">Token.</param>
        /// <returns>Boolean indicating if authentication succeeded.</returns>
        public bool AuthenticateBearer(string token)
        {
            if (String.IsNullOrEmpty(token)) return false;

            if (String.Equals(token, _AdminBearerToken, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
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
}