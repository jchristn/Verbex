import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import './CredentialsView.css';

function CredentialsView({ selectedTenant, tenants, onTenantSelect }) {
  const { apiClient } = useAuth();
  const [credentials, setCredentials] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [selectedCredential, setSelectedCredential] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState(null);

  // Create form state
  const [createDescription, setCreateDescription] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState(null);

  // New credential token display
  const [newCredentialToken, setNewCredentialToken] = useState(null);
  const [tokenCopied, setTokenCopied] = useState(false);

  const loadCredentials = async () => {
    if (!apiClient || !selectedTenant) return;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getCredentials(selectedTenant);
      setCredentials(response.data?.credentials || []);
    } catch (err) {
      console.error('Failed to load credentials:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (selectedTenant) {
      loadCredentials();
    } else {
      setCredentials([]);
    }
  }, [apiClient, selectedTenant]);

  const handleViewDetails = (credential) => {
    setSelectedCredential(credential);
    setShowDetailModal(true);
  };

  const handleDelete = async (credentialId) => {
    if (!confirm(`Are you sure you want to delete this credential? Any applications using this API key will no longer be able to authenticate. This action cannot be undone.`)) {
      return;
    }

    setIsDeleting(true);
    try {
      await apiClient.deleteCredential(selectedTenant, credentialId);
      setShowDetailModal(false);
      setSelectedCredential(null);
      loadCredentials();
    } catch (err) {
      alert(`Failed to delete credential: ${err.message}`);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    setCreateError(null);

    setIsCreating(true);
    try {
      const response = await apiClient.createCredential(selectedTenant, {
        description: createDescription.trim() || undefined
      });
      // Display the new token to the user
      if (response.data?.credential?.bearerToken) {
        setNewCredentialToken(response.data.credential.bearerToken);
      }
      setShowCreateModal(false);
      setCreateDescription('');
      loadCredentials();
    } catch (err) {
      setCreateError(err.message);
    } finally {
      setIsCreating(false);
    }
  };

  const handleCloseCreateModal = () => {
    setShowCreateModal(false);
    setCreateDescription('');
    setCreateError(null);
  };

  const handleCopyToken = async () => {
    if (newCredentialToken) {
      try {
        await navigator.clipboard.writeText(newCredentialToken);
        setTokenCopied(true);
        setTimeout(() => setTokenCopied(false), 2000);
      } catch (err) {
        console.error('Failed to copy token:', err);
      }
    }
  };

  const handleCloseTokenModal = () => {
    setNewCredentialToken(null);
    setTokenCopied(false);
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  const maskToken = (token) => {
    if (!token) return 'N/A';
    if (token.length <= 8) return '********';
    return token.substring(0, 4) + '...' + token.substring(token.length - 4);
  };

  const selectedTenantData = tenants?.find(t => t.identifier === selectedTenant);

  return (
    <div className="credentials-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Credentials</h2>
          {selectedTenant && <span className="count-badge">{credentials.length}</span>}
        </div>
        <div className="workspace-actions">
          {selectedTenant && (
            <>
              <button className="btn btn-secondary" onClick={loadCredentials}>
                Refresh
              </button>
              <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
                Create Credential
              </button>
            </>
          )}
        </div>
      </div>

      {/* Tenant selector */}
      <div className="workspace-card tenant-selector-card">
        <label htmlFor="tenantSelect">Select Tenant:</label>
        <select
          id="tenantSelect"
          value={selectedTenant || ''}
          onChange={(e) => onTenantSelect && onTenantSelect(e.target.value || null)}
          className="tenant-select"
        >
          <option value="">-- Select a tenant --</option>
          {tenants?.map((tenant) => (
            <option key={tenant.identifier} value={tenant.identifier}>
              {tenant.name || tenant.identifier}
            </option>
          ))}
        </select>
      </div>

      {!selectedTenant ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">🔑</div>
            <h3 className="empty-state-title">Select a Tenant</h3>
            <p className="empty-state-description">
              Select a tenant above to view and manage its API credentials.
            </p>
          </div>
        </div>
      ) : isLoading ? (
        <div className="workspace-card">
          <div className="loading-spinner">Loading credentials...</div>
        </div>
      ) : error ? (
        <div className="workspace-card error-card">
          <p className="error-message">Error: {error}</p>
        </div>
      ) : credentials.length === 0 ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">🔑</div>
            <h3 className="empty-state-title">No Credentials Found</h3>
            <p className="empty-state-description">
              Create your first API credential for tenant "{selectedTenantData?.name || selectedTenant}".
            </p>
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              Create Credential
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <table className="credentials-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>ID</th>
                <th>Description</th>
                <th>Token</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {credentials.map((credential) => (
                <tr key={credential.identifier}>
                  <td>
                    <span className={`status-badge ${credential.active ? 'enabled' : 'disabled'}`}>
                      {credential.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td className="credential-id">{credential.identifier}</td>
                  <td>{credential.name || '-'}</td>
                  <td className="credential-token">{maskToken(credential.bearerToken)}</td>
                  <td>{formatDate(credential.createdUtc)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => handleViewDetails(credential)}
                      >
                        Details
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Create Credential Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={handleCloseCreateModal}
        title="Create New Credential"
      >
        <form onSubmit={handleCreate} className="credential-form">
          {createError && (
            <div className="form-error">{createError}</div>
          )}
          <div className="form-info">
            <p>A new API key (bearer token) will be generated automatically. Make sure to copy it when displayed - you won't be able to see it again.</p>
          </div>
          <div className="form-group">
            <label htmlFor="credentialDescription">Description</label>
            <input
              type="text"
              id="credentialDescription"
              value={createDescription}
              onChange={(e) => setCreateDescription(e.target.value)}
              placeholder="Optional description (e.g., 'Production API Key')"
              disabled={isCreating}
              autoFocus
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
              {isCreating ? 'Creating...' : 'Create Credential'}
            </button>
          </div>
        </form>
      </Modal>

      {/* New Token Display Modal */}
      <Modal
        isOpen={!!newCredentialToken}
        onClose={handleCloseTokenModal}
        title="Credential Created"
      >
        <div className="token-display">
          <div className="token-warning">
            <strong>Important:</strong> Copy this API key now. You won't be able to see it again!
          </div>
          <div className="token-container">
            <code className="token-value">{newCredentialToken}</code>
            <button
              className="btn btn-secondary copy-btn"
              onClick={handleCopyToken}
            >
              {tokenCopied ? 'Copied!' : 'Copy'}
            </button>
          </div>
          <div className="form-actions">
            <button
              className="btn btn-primary"
              onClick={handleCloseTokenModal}
            >
              Done
            </button>
          </div>
        </div>
      </Modal>

      {/* Credential Details Modal */}
      <Modal
        isOpen={showDetailModal}
        onClose={() => {
          setShowDetailModal(false);
          setSelectedCredential(null);
        }}
        title={`Credential: ${selectedCredential?.identifier || ''}`}
      >
        {selectedCredential && (
          <div className="credential-details">
            <div className="details-section">
              <h4>General Information</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">ID</span>
                  <span className="detail-value">{selectedCredential.identifier}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Description</span>
                  <span className="detail-value">{selectedCredential.name || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Status</span>
                  <span className={`status-badge ${selectedCredential.active ? 'enabled' : 'disabled'}`}>
                    {selectedCredential.active ? 'Active' : 'Disabled'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Token</span>
                  <span className="detail-value credential-token">{maskToken(selectedCredential.bearerToken)}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Created</span>
                  <span className="detail-value">{formatDate(selectedCredential.createdUtc)}</span>
                </div>
              </div>
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDelete(selectedCredential.identifier)}
                disabled={isDeleting}
              >
                {isDeleting ? 'Deleting...' : 'Delete Credential'}
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedCredential(null);
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

export default CredentialsView;
