import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './Topbar.css';

function Topbar() {
  const navigate = useNavigate();
  const { logout, theme, toggleTheme, serverUrl } = useAuth();
  const [copied, setCopied] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const handleCopyUrl = () => {
    navigator.clipboard.writeText(serverUrl).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  };

  return (
    <header className="topbar">
      <div className="topbar-left">
        <div className="topbar-brand" data-tour-id="topbar-logo">
          <img src="/logo.png" alt="Verbex" className="topbar-logo" />
        </div>

        <div className="topbar-server-badge" data-tour-id="topbar-server" onClick={handleCopyUrl} title={copied ? 'Copied!' : 'Click to copy server URL'}>
          <span className="server-url">{serverUrl}</span>
          <svg className="copy-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            {copied ? (
              <polyline points="20 6 9 17 4 12" />
            ) : (
              <>
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </>
            )}
          </svg>
        </div>
      </div>

      <div className="topbar-actions">
        <button
          className="topbar-btn"
          onClick={toggleTheme}
          title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
          data-tour-id="topbar-theme"
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>
        <button
          className="topbar-btn logout-btn"
          onClick={handleLogout}
          title="Logout"
          data-tour-id="topbar-logout"
        >
          Logout
        </button>
      </div>
    </header>
  );
}

export default Topbar;
