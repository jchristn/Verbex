import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import Topbar from './Topbar';
import Sidebar from './Sidebar';
import Workspace from './Workspace';
import WelcomeModal from './WelcomeModal';
import Tour from './Tour';
import SetupWizard from './SetupWizard';
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
    if (savedView) setActiveView(savedView);
    // Note: savedIndex is validated after indices load
  }, []);

  // Validate and restore saved index after indices are loaded
  useEffect(() => {
    if (indices.length > 0 && !selectedIndex) {
      const savedIndex = localStorage.getItem('verbex_selected_index');
      if (savedIndex) {
        // Only restore if it matches a valid index identifier
        const validIndex = indices.find(i => i.identifier === savedIndex);
        if (validIndex) {
          setSelectedIndex(savedIndex);
        } else {
          // Clear invalid saved index
          localStorage.removeItem('verbex_selected_index');
        }
      }
    }
  }, [indices, selectedIndex]);

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
  const loadIndices = useCallback(async (signal) => {
    if (!apiClient) return;

    setIsLoading(true);
    try {
      const response = await apiClient.getIndices({ maxResults: 1000, skip: 0, signal });
      setIndices(response.data?.objects || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.error('Failed to load indices:', err);
    } finally {
      setIsLoading(false);
    }
  }, [apiClient]);

  // Load tenants
  const loadTenants = useCallback(async (signal) => {
    if (!apiClient) return;

    try {
      const response = await apiClient.getTenants({ maxResults: 1000, skip: 0, signal });
      setTenants(response.data?.objects || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.error('Failed to load tenants:', err);
    }
  }, [apiClient]);

  useEffect(() => {
    const abortController = new AbortController();
    loadIndices(abortController.signal);
    loadTenants(abortController.signal);
    return () => abortController.abort();
  }, [loadIndices, loadTenants]);

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
      <WelcomeModal />
      <Tour />
      <SetupWizard onNavigate={handleViewChange} />
    </div>
  );
}

export default Dashboard;
