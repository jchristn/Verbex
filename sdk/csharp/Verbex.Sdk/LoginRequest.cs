namespace Verbex.Sdk
{
    using System;

    /// <summary>
    /// Request body for user authentication.
    /// </summary>
    public class LoginRequest
    {
        #region Public-Members

        /// <summary>
        /// Username for authentication.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                _Username = value ?? "";
            }
        }

        /// <summary>
        /// Password for authentication.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                _Password = value ?? "";
            }
        }

        /// <summary>
        /// Tenant ID for tenant-scoped authentication.
        /// </summary>
        public string? TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                _TenantId = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Username = "";
        private string _Password = "";
        private string? _TenantId = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty LoginRequest.
        /// </summary>
        public LoginRequest()
        {
        }

        /// <summary>
        /// Instantiate a LoginRequest with credentials.
        /// </summary>
        /// <param name="username">Username for authentication.</param>
        /// <param name="password">Password for authentication.</param>
        public LoginRequest(string username, string password)
        {
            Username = username;
            Password = password;
        }

        /// <summary>
        /// Instantiate a LoginRequest with credentials and tenant ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID for tenant-scoped authentication.</param>
        /// <param name="username">Username for authentication.</param>
        /// <param name="password">Password for authentication.</param>
        public LoginRequest(string tenantId, string username, string password)
        {
            TenantId = tenantId;
            Username = username;
            Password = password;
        }

        #endregion
    }
}
