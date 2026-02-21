import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './Topbar.css';

function Topbar() {
  const navigate = useNavigate();
  const { logout, theme, toggleTheme, serverUrl } = useAuth();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <header className="topbar">
      <div className="topbar-brand" data-tour-id="topbar-logo">
        <img src="/logo.png" alt="Verbex" className="topbar-logo" />
      </div>

      <div className="topbar-server" data-tour-id="topbar-server">
        <span className="server-label">Server:</span>
        <span className="server-url">{serverUrl}</span>
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
