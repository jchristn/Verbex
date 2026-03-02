import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './Login.css';

function Login() {
  const navigate = useNavigate();
  const { login, theme, toggleTheme } = useAuth();

  const [serverUrl, setServerUrl] = useState(
    window.__ENV__?.VERBEX_SERVER_URL || __DEFAULT_SERVER_URL__
  );
  const [authMode, setAuthMode] = useState('token');
  const [token, setToken] = useState(
    window.__ENV__?.VERBEX_API_KEY || __DEFAULT_API_KEY__
  );
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showToken, setShowToken] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      if (authMode === 'token') {
        await login(serverUrl, token);
      } else {
        await login(serverUrl, { username, password });
      }
      navigate('/dashboard');
    } catch (err) {
      setError(err.message || 'Failed to connect to server');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <button
          className="theme-toggle"
          onClick={toggleTheme}
          title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>
        <div className="login-header">
          <img src="/logo.png" alt="Verbex" className="login-logo" />
          <p>Inverted Index Dashboard</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="serverUrl">Server URL</label>
            <input
              type="url"
              id="serverUrl"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              placeholder="http://localhost:8080"
              required
              title="URL of the Verbex server to connect to"
            />
          </div>

          <div className="form-group">
            <label>Authentication Method</label>
            <div className="auth-mode-toggle">
              <button
                type="button"
                className={`auth-mode-btn ${authMode === 'token' ? 'active' : ''}`}
                onClick={() => setAuthMode('token')}
                title="Authenticate using an API token"
              >
                API Token
              </button>
              <button
                type="button"
                className={`auth-mode-btn ${authMode === 'credentials' ? 'active' : ''}`}
                onClick={() => setAuthMode('credentials')}
                title="Authenticate using username and password"
              >
                Credentials
              </button>
            </div>
          </div>

          {authMode === 'token' ? (
            <div className="form-group">
              <label htmlFor="token">API Token</label>
              <div className="input-with-toggle">
                <input
                  type={showToken ? 'text' : 'password'}
                  id="token"
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  placeholder="Enter your API token"
                  required
                  title="Enter your API bearer token"
                />
                <button
                  type="button"
                  className="visibility-toggle"
                  onClick={() => setShowToken(!showToken)}
                  title={showToken ? 'Hide token' : 'Show token'}
                >
                  {showToken ? (
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/>
                      <line x1="1" y1="1" x2="23" y2="23"/>
                    </svg>
                  ) : (
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                      <circle cx="12" cy="12" r="3"/>
                    </svg>
                  )}
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="form-group">
                <label htmlFor="username">Username</label>
                <input
                  type="text"
                  id="username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="Enter username"
                  required
                  title="Enter your username"
                />
              </div>
              <div className="form-group">
                <label htmlFor="password">Password</label>
                <div className="input-with-toggle">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    id="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Enter password"
                    required
                    title="Enter your password"
                  />
                  <button
                    type="button"
                    className="visibility-toggle"
                    onClick={() => setShowPassword(!showPassword)}
                    title={showPassword ? 'Hide password' : 'Show password'}
                  >
                    {showPassword ? (
                      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/>
                        <line x1="1" y1="1" x2="23" y2="23"/>
                      </svg>
                    ) : (
                      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                        <circle cx="12" cy="12" r="3"/>
                      </svg>
                    )}
                  </button>
                </div>
              </div>
            </>
          )}

          {error && <div className="error-message">{error}</div>}

          <button
            type="submit"
            className="btn btn-primary login-btn"
            disabled={isLoading}
            title="Connect to the Verbex server"
          >
            {isLoading ? 'Connecting...' : 'Connect'}
          </button>
        </form>
      </div>
    </div>
  );
}

export default Login;
