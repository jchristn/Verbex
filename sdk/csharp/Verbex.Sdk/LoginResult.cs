namespace Verbex.Sdk
{
    using System;

    /// <summary>
    /// Result of a login attempt, containing authentication and authorization status along with session context.
    /// </summary>
    public class LoginResult
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether the login was successful.
        /// </summary>
        public bool Success
        {
            get
            {
                return _Success;
            }
            set
            {
                _Success = value;
            }
        }

        /// <summary>
        /// The result of the authentication attempt.
        /// </summary>
        public AuthenticationResultEnum AuthenticationResult
        {
            get
            {
                return _AuthenticationResult;
            }
            set
            {
                _AuthenticationResult = value;
            }
        }

        /// <summary>
        /// The result of the authorization check.
        /// </summary>
        public AuthorizationResultEnum AuthorizationResult
        {
            get
            {
                return _AuthorizationResult;
            }
            set
            {
                _AuthorizationResult = value;
            }
        }

        /// <summary>
        /// Error message if the login failed.
        /// </summary>
        public string? ErrorMessage
        {
            get
            {
                return _ErrorMessage;
            }
            set
            {
                _ErrorMessage = value;
            }
        }

        /// <summary>
        /// Bearer token for authenticated requests. Only populated on successful login.
        /// </summary>
        public string? Token
        {
            get
            {
                return _Token;
            }
            set
            {
                _Token = value;
            }
        }

        /// <summary>
        /// The tenant identifier for the authenticated session.
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

        /// <summary>
        /// The user identifier for the authenticated session.
        /// </summary>
        public string? UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                _UserId = value;
            }
        }

        /// <summary>
        /// The email address of the authenticated user.
        /// </summary>
        public string? Email
        {
            get
            {
                return _Email;
            }
            set
            {
                _Email = value;
            }
        }

        /// <summary>
        /// Indicates whether the authenticated user is a tenant administrator.
        /// </summary>
        public bool IsAdmin
        {
            get
            {
                return _IsAdmin;
            }
            set
            {
                _IsAdmin = value;
            }
        }

        /// <summary>
        /// Indicates whether the authenticated user is a global administrator.
        /// </summary>
        public bool IsGlobalAdmin
        {
            get
            {
                return _IsGlobalAdmin;
            }
            set
            {
                _IsGlobalAdmin = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Success = false;
        private AuthenticationResultEnum _AuthenticationResult = AuthenticationResultEnum.NotAuthenticated;
        private AuthorizationResultEnum _AuthorizationResult = AuthorizationResultEnum.Unauthorized;
        private string? _ErrorMessage = null;
        private string? _Token = null;
        private string? _TenantId = null;
        private string? _UserId = null;
        private string? _Email = null;
        private bool _IsAdmin = false;
        private bool _IsGlobalAdmin = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty LoginResult.
        /// </summary>
        public LoginResult()
        {
        }

        /// <summary>
        /// Create a successful login result.
        /// </summary>
        /// <param name="token">The bearer token.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="isAdmin">Whether the user is a tenant admin.</param>
        /// <param name="isGlobalAdmin">Whether the user is a global admin.</param>
        /// <returns>A successful LoginResult.</returns>
        public static LoginResult Successful(
            string token,
            string? tenantId = null,
            string? userId = null,
            string? email = null,
            bool isAdmin = false,
            bool isGlobalAdmin = false)
        {
            return new LoginResult
            {
                Success = true,
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Authorized,
                Token = token,
                TenantId = tenantId,
                UserId = userId,
                Email = email,
                IsAdmin = isAdmin,
                IsGlobalAdmin = isGlobalAdmin
            };
        }

        /// <summary>
        /// Create a failed login result.
        /// </summary>
        /// <param name="authenticationResult">The authentication result.</param>
        /// <param name="authorizationResult">The authorization result.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed LoginResult.</returns>
        public static LoginResult Failed(
            AuthenticationResultEnum authenticationResult,
            AuthorizationResultEnum authorizationResult,
            string? errorMessage = null)
        {
            return new LoginResult
            {
                Success = false,
                AuthenticationResult = authenticationResult,
                AuthorizationResult = authorizationResult,
                ErrorMessage = errorMessage
            };
        }

        #endregion
    }
}
