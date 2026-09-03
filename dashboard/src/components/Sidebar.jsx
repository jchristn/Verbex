import { useOnboarding } from '../context/OnboardingContext';
import './Sidebar.css';

function Sidebar({ activeView, onViewChange, indices, isAdmin }) {
  const { startTour, startWizard } = useOnboarding();

  const navItems = [
    { id: 'indices', label: 'Indices', icon: '\uD83D\uDCDA', tourId: 'nav-indices' },
    { id: 'documents', label: 'Documents', icon: '\uD83D\uDCC4', tourId: 'nav-documents' },
    { id: 'search', label: 'Search', icon: '\uD83D\uDD0D', tourId: 'nav-search' },
    { id: 'apiExplorer', label: 'API Explorer', icon: '\uD83E\uDDEA', tourId: 'nav-api-explorer' },
    { id: 'requestHistory', label: 'Request History', icon: '\uD83D\uDD52', tourId: 'nav-request-history' },
    { id: 'observability', label: 'Observability', icon: '\uD83D\uDCC8', tourId: 'nav-observability' }
  ];

  const adminItems = [
    { id: 'tenants', label: 'Tenants', icon: '\uD83C\uDFE2', tourId: 'nav-tenants' },
    { id: 'users', label: 'Users', icon: '\uD83D\uDC64', tourId: 'nav-users' },
    { id: 'credentials', label: 'Credentials', icon: '\uD83D\uDD11', tourId: 'nav-credentials' }
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
