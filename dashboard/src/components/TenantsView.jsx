import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import './TenantsView.css';

function TenantsView({ onTenantSelect }) {
  const { apiClient } = useAuth();
  const [tenants, setTenants] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState(null);

  // Create form state
  const [createName, setCreateName] = useState('');
  const [createDescription, setCreateDescription] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState(null);

  const loadTenants = async () => {
    if (!apiClient) return;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getTenants();
      setTenants(response.data?.tenants || []);
    } catch (err) {
      console.error('Failed to load tenants:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadTenants();
  }, [apiClient]);

  const handleViewDetails = (tenant) => {
    setSelectedTenant(tenant);
    setShowDetailModal(true);
  };

  const handleDelete = async (tenantId) => {
    if (!confirm(`Are you sure you want to delete tenant "${tenantId}"? This will delete all users, credentials, and data associated with this tenant. This action cannot be undone.`)) {
      return;
    }

    setIsDeleting(true);
    try {
      await apiClient.deleteTenant(tenantId);
      setShowDetailModal(false);
      setSelectedTenant(null);
      loadTenants();
    } catch (err) {
      alert(`Failed to delete tenant: ${err.message}`);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    setCreateError(null);

    if (!createName.trim()) {
      setCreateError('Tenant name is required');
      return;
    }

    setIsCreating(true);
    try {
      await apiClient.createTenant({
        name: createName.trim(),
        description: createDescription.trim() || undefined
      });
      setShowCreateModal(false);
      setCreateName('');
      setCreateDescription('');
      loadTenants();
    } catch (err) {
      setCreateError(err.message);
    } finally {
      setIsCreating(false);
    }
  };

  const handleCloseCreateModal = () => {
    setShowCreateModal(false);
    setCreateName('');
    setCreateDescription('');
    setCreateError(null);
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  if (isLoading) {
    return (
      <div className="tenants-view">
        <div className="loading-spinner">Loading tenants...</div>
      </div>
    );
  }

  return (
    <div className="tenants-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Tenants</h2>
          <span className="count-badge">{tenants.length}</span>
        </div>
        <div className="workspace-actions">
          <button className="btn btn-secondary" onClick={loadTenants}>
            Refresh
          </button>
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            Create Tenant
          </button>
        </div>
      </div>

      {error && (
        <div className="workspace-card error-card">
          <p className="error-message">Error: {error}</p>
        </div>
      )}

      {tenants.length === 0 ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">🏢</div>
            <h3 className="empty-state-title">No Tenants Found</h3>
            <p className="empty-state-description">
              Create your first tenant to start organizing users and credentials.
            </p>
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              Create Tenant
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <table className="tenants-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>ID</th>
                <th>Name</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {tenants.map((tenant) => (
                <tr key={tenant.identifier}>
                  <td>
                    <span className={`status-badge ${tenant.active ? 'enabled' : 'disabled'}`}>
                      {tenant.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td className="tenant-id">{tenant.identifier}</td>
                  <td>{tenant.name || '-'}</td>
                  <td>{formatDate(tenant.createdUtc)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => handleViewDetails(tenant)}
                      >
                        Details
                      </button>
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => onTenantSelect && onTenantSelect(tenant.identifier, 'users')}
                      >
                        Users
                      </button>
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => onTenantSelect && onTenantSelect(tenant.identifier, 'credentials')}
                      >
                        Credentials
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Create Tenant Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={handleCloseCreateModal}
        title="Create New Tenant"
      >
        <form onSubmit={handleCreate} className="tenant-form">
          {createError && (
            <div className="form-error">{createError}</div>
          )}
          <div className="form-group">
            <label htmlFor="tenantName">Name *</label>
            <input
              type="text"
              id="tenantName"
              value={createName}
              onChange={(e) => setCreateName(e.target.value)}
              placeholder="Enter tenant name"
              disabled={isCreating}
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="tenantDescription">Description</label>
            <textarea
              id="tenantDescription"
              value={createDescription}
              onChange={(e) => setCreateDescription(e.target.value)}
              placeholder="Optional description"
              disabled={isCreating}
              rows={3}
            />
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCloseCreateModal}
              disabled={isCreating}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isCreating}
            >
              {isCreating ? 'Creating...' : 'Create Tenant'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Tenant Details Modal */}
      <Modal
        isOpen={showDetailModal}
        onClose={() => {
          setShowDetailModal(false);
          setSelectedTenant(null);
        }}
        title={`Tenant: ${selectedTenant?.name || selectedTenant?.identifier || ''}`}
      >
        {selectedTenant && (
          <div className="tenant-details">
            <div className="details-section">
              <h4>General Information</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">ID</span>
                  <span className="detail-value">{selectedTenant.identifier}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Name</span>
                  <span className="detail-value">{selectedTenant.name || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Status</span>
                  <span className={`status-badge ${selectedTenant.active ? 'enabled' : 'disabled'}`}>
                    {selectedTenant.active ? 'Active' : 'Disabled'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Created</span>
                  <span className="detail-value">{formatDate(selectedTenant.createdUtc)}</span>
                </div>
              </div>
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDelete(selectedTenant.identifier)}
                disabled={isDeleting}
              >
                {isDeleting ? 'Deleting...' : 'Delete Tenant'}
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedTenant(null);
                }}
              >
                Close
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}

export default TenantsView;
