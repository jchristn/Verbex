import './Sidebar.css';

function Sidebar({ activeView, onViewChange, indices }) {
  const navItems = [
    { id: 'indices', label: 'Indices', icon: '📚' },
    { id: 'documents', label: 'Documents', icon: '📄' },
    { id: 'search', label: 'Search', icon: '🔍' }
  ];

  return (
    <aside className="sidebar">
      <nav className="sidebar-nav">
        <div className="nav-section">
          <div className="nav-section-title">Navigation</div>
          {navItems.map((item) => (
            <button
              key={item.id}
              className={`nav-item ${activeView === item.id ? 'active' : ''}`}
              onClick={() => onViewChange(item.id)}
            >
              <span className="nav-icon">{item.icon}</span>
              <span className="nav-label">{item.label}</span>
            </button>
          ))}
        </div>
      </nav>

      <div className="sidebar-footer">
        <div className="sidebar-info">
          <span className="info-label">Total Indices</span>
          <span className="info-value">{indices.length}</span>
        </div>
      </div>
    </aside>
  );
}

export default Sidebar;
