import { useOnboarding } from '../context/OnboardingContext';
import './Sidebar.css';

function Sidebar({ activeView, onViewChange, indices, isAdmin }) {
  const { startTour, startWizard } = useOnboarding();

  const navItems = [
    { id: 'indices', label: 'Indices', icon: '📚', tourId: 'nav-indices' },
    { id: 'documents', label: 'Documents', icon: '📄', tourId: 'nav-documents' },
    { id: 'search', label: 'Search', icon: '🔍', tourId: 'nav-search' }
  ];

  const adminItems = [
    { id: 'tenants', label: 'Tenants', icon: '🏢', tourId: 'nav-tenants' },
    { id: 'users', label: 'Users', icon: '👤', tourId: 'nav-users' },
    { id: 'credentials', label: 'Credentials', icon: '🔑', tourId: 'nav-credentials' }
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
              data-tour-id={item.tourId}
              title={item.label}
            >
              <span className="nav-icon">{item.icon}</span>
              <span className="nav-label">{item.label}</span>
            </button>
          ))}
        </div>
        {isAdmin && (
          <div className="nav-section">
            <div className="nav-section-title">Administration</div>
            {adminItems.map((item) => (
              <button
                key={item.id}
                className={`nav-item ${activeView === item.id ? 'active' : ''}`}
                onClick={() => onViewChange(item.id)}
                data-tour-id={item.tourId}
                title={item.label}
              >
                <span className="nav-icon">{item.icon}</span>
                <span className="nav-label">{item.label}</span>
              </button>
            ))}
          </div>
        )}
      </nav>

      <div className="sidebar-footer">
        <div className="sidebar-info">
          <span className="info-label">Total Indices</span>
          <span className="info-value">{indices.length}</span>
        </div>
        <div className="sidebar-onboarding-links">
          <button className="sidebar-link" onClick={startTour} title="Start the guided dashboard tour">
            Take Tour
          </button>
          <span className="sidebar-link-separator">|</span>
          <button className="sidebar-link" onClick={startWizard} title="Open the setup wizard">
            Setup Wizard
          </button>
        </div>
      </div>
    </aside>
  );
}

export default Sidebar;
