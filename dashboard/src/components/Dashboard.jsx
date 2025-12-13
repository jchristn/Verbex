import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import Topbar from './Topbar';
import Sidebar from './Sidebar';
import Workspace from './Workspace';
import './Dashboard.css';

function Dashboard() {
  const { apiClient } = useAuth();
  const [activeView, setActiveView] = useState('indices');
  const [selectedIndex, setSelectedIndex] = useState(null);
  const [indices, setIndices] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

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

  useEffect(() => {
    loadIndices();
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
  };

  return (
    <div className="dashboard">
      <Topbar />
      <div className="dashboard-content">
        <Sidebar
          activeView={activeView}
          onViewChange={handleViewChange}
          indices={indices}
        />
        <Workspace
          activeView={activeView}
          selectedIndex={selectedIndex}
          indices={indices}
          isLoading={isLoading}
          onRefresh={handleRefresh}
          onIndexSelect={handleIndexSelect}
          onIndexSelectAndNavigate={handleIndexSelectAndNavigate}
        />
      </div>
    </div>
  );
}

export default Dashboard;
