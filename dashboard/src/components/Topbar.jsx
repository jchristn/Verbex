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
      <div className="topbar-brand">
        <h1>Verbex</h1>
        <span className="topbar-subtitle">Inverted Index Dashboard</span>
      </div>

      <div className="topbar-server">
        <span className="server-label">Server:</span>
        <span className="server-url">{serverUrl}</span>
      </div>

      <div className="topbar-actions">
        <button
          className="topbar-btn"
          onClick={toggleTheme}
          title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>
        <button
          className="topbar-btn logout-btn"
          onClick={handleLogout}
          title="Logout"
        >
          Logout
        </button>
      </div>
    </header>
  );
}

export default Topbar;
