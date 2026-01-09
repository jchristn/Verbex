import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import Topbar from './Topbar';
import Sidebar from './Sidebar';
import Workspace from './Workspace';
import './Dashboard.css';

function Dashboard() {
  const { apiClient, userInfo } = useAuth();
  const [activeView, setActiveView] = useState('indices');
  const [selectedIndex, setSelectedIndex] = useState(null);
  const [indices, setIndices] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  // Admin state
  const [tenants, setTenants] = useState([]);
  const [selectedTenant, setSelectedTenant] = useState(null);

  // Load saved state
  useEffect(() => {
    const savedView = localStorage.getItem('verbex_active_view');
    const savedIndex = localStorage.getItem('verbex_selected_index');

    if (savedView) setActiveView(savedView);
    if (savedIndex) setSelectedIndex(savedIndex);
  }, []);

  // Save state changes
  useEffect(() => {
    localStorage.setItem('verbex_active_view', activeView);
  }, [activeView]);

  useEffect(() => {
    if (selectedIndex) {
      localStorage.setItem('verbex_selected_index', selectedIndex);
    } else {
      localStorage.removeItem('verbex_selected_index');
    }
  }, [selectedIndex]);

  // Load indices
  const loadIndices = async () => {
    if (!apiClient) return;

    setIsLoading(true);
    try {
      const response = await apiClient.getIndices();
      setIndices(response.data?.indices || []);
    } catch (err) {
      console.error('Failed to load indices:', err);
    } finally {
      setIsLoading(false);
    }
  };

  // Load tenants
  const loadTenants = async () => {
    if (!apiClient) return;

    try {
      const response = await apiClient.getTenants();
      setTenants(response.data?.tenants || []);
    } catch (err) {
      console.error('Failed to load tenants:', err);
    }
  };

  useEffect(() => {
    loadIndices();
    loadTenants();
  }, [apiClient]);

  const handleViewChange = (view) => {
    setActiveView(view);
  };

  const handleIndexSelect = (indexId) => {
    setSelectedIndex(indexId);
  };

  const handleIndexSelectAndNavigate = (indexId) => {
    setSelectedIndex(indexId);
    setActiveView('documents');
  };

  const handleRefresh = () => {
    loadIndices();
    loadTenants();
  };

  const handleTenantSelect = (tenantId) => {
    setSelectedTenant(tenantId);
  };

  const handleTenantSelectAndNavigate = (tenantId, view) => {
    setSelectedTenant(tenantId);
    setActiveView(view || 'users');
  };

  return (
    <div className="dashboard">
      <Topbar />
      <div className="dashboard-content">
        <Sidebar
          activeView={activeView}
          onViewChange={handleViewChange}
          indices={indices}
          isAdmin={userInfo?.isAdmin || userInfo?.isGlobalAdmin || false}
        />
        <Workspace
          activeView={activeView}
          selectedIndex={selectedIndex}
          indices={indices}
          isLoading={isLoading}
          onRefresh={handleRefresh}
          onIndexSelect={handleIndexSelect}
          onIndexSelectAndNavigate={handleIndexSelectAndNavigate}
          tenants={tenants}
          selectedTenant={selectedTenant}
          onTenantSelect={handleTenantSelect}
          onTenantSelectAndNavigate={handleTenantSelectAndNavigate}
        />
      </div>
    </div>
  );
}

export default Dashboard;
