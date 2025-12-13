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

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8080;
        private bool _Ssl = false;
        private string? _SslCertificateFile = null;
        private string? _SslCertificatePassword = null;

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