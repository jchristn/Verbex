namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// REST settings.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// Port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Enable SSL.
        /// </summary>
        public bool Ssl
        {
            get
            {
                return _Ssl;
            }
            set
            {
                _Ssl = value;
            }
        }

        /// <summary>
        /// SSL certificate filename.
        /// </summary>
        public string? SslCertificateFile
        {
            get
            {
                return _SslCertificateFile;
            }
            set
            {
                _SslCertificateFile = value;
            }
        }

        /// <summary>
        /// SSL certificate password.
        /// </summary>
        public string? SslCertificatePassword
        {
            get
            {
                return _SslCertificatePassword;
            }
            set
            {
                _SslCertificatePassword = value;
            }
        }

        /// <summary>
        /// Enable OpenAPI documentation endpoint.
        /// When enabled, the OpenAPI specification document will be available at the configured path (typically /openapi.json).
        /// Default value is true.
        /// </summary>
        public bool EnableOpenApi
        {
            get
            {
                return _EnableOpenApi;
            }
            set
            {
                _EnableOpenApi = value;
            }
        }

        /// <summary>
        /// Enable Swagger UI endpoint.
        /// When enabled, the interactive Swagger UI will be available at the configured path (typically /swagger).
        /// Requires EnableOpenApi to be true for Swagger UI to function.
        /// Default value is true.
        /// </summary>
        public bool EnableSwaggerUi
        {
            get
            {
                return _EnableSwaggerUi;
            }
            set
            {
                _EnableSwaggerUi = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8080;
        private bool _Ssl = false;
        private string? _SslCertificateFile = null;
        private string? _SslCertificatePassword = null;
        private bool _EnableOpenApi = true;
        private bool _EnableSwaggerUi = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RestSettings()
        {

        }

        #endregion
    }
}