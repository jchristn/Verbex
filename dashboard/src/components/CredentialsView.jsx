import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import ConfirmModal from './ConfirmModal';
import ActionMenu from './ActionMenu';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import Pagination from './Pagination';
import SortableHeader from './SortableHeader';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './CredentialsView.css';

function CredentialsView({ selectedTenant, tenants, onTenantSelect }) {
  const { apiClient } = useAuth();
  const [credentials, setCredentials] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [selectedCredential, setSelectedCredential] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isToggling, setIsToggling] = useState(false);
  const [error, setError] = useState(null);

  // Create form state
  const [createDescription, setCreateDescription] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState(null);

  // Edit modal state
  const [showEditModal, setShowEditModal] = useState(false);
  const [editCredential, setEditCredential] = useState(null);
  const [editName, setEditName] = useState('');
  const [editActive, setEditActive] = useState(true);
  const [editLabels, setEditLabels] = useState([]);
  const [editTags, setEditTags] = useState({});
  const [isEditingCredential, setIsEditingCredential] = useState(false);
  const [editError, setEditError] = useState(null);

  // New credential token display
  const [newCredentialToken, setNewCredentialToken] = useState(null);
  const [tokenCopied, setTokenCopied] = useState(false);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataCredential, setMetadataCredential] = useState(null);

  // Confirm modals
  const [showToggleConfirm, setShowToggleConfirm] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [confirmCredential, setConfirmCredential] = useState(null);
  const [actionError, setActionError] = useState(null);

  // Sorting
  const [sortKey, setSortKey] = useState('createdUtc');
  const [sortDirection, setSortDirection] = useState('desc');

  // Filtering
  const [filters, setFilters] = useState({});

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Auto-select tenant if only one exists
  useEffect(() => {
    if (!selectedTenant && tenants?.length === 1) {
      onTenantSelect && onTenantSelect(tenants[0].identifier);
    }
  }, [tenants, selectedTenant, onTenantSelect]);

  const loadCredentials = useCallback(async (signalOrEvent) => {
    if (!apiClient || !selectedTenant) return;

    // Handle both AbortSignal (from useEffect) and no signal (from button click)
    const signal = signalOrEvent instanceof AbortSignal ? signalOrEvent : undefined;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getCredentials(selectedTenant, { maxResults: 1000, skip: 0, ...(signal ? { signal } : {}) });
      setCredentials(response.data?.objects || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.error('Failed to load credentials:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  }, [apiClient, selectedTenant]);

  useEffect(() => {
    if (selectedTenant) {
      const abortController = new AbortController();
      loadCredentials(abortController.signal);
      return () => abortController.abort();
    } else {
      setCredentials([]);
    }
  }, [loadCredentials]);

  // Filter and sort credentials
  const filteredAndSortedCredentials = useMemo(() => {
    let result = [...credentials];

    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      if (value) {
        result = result.filter(cred => {
          const fieldValue = cred[key];
          if (fieldValue === null || fieldValue === undefined) return false;
          return String(fieldValue).toLowerCase().includes(value.toLowerCase());
        });
      }
    });

    // Apply sorting
    result.sort((a, b) => {
      let aVal = a[sortKey];
      let bVal = b[sortKey];

      if (aVal === null || aVal === undefined) aVal = '';
      if (bVal === null || bVal === undefined) bVal = '';

      if (typeof aVal === 'string') aVal = aVal.toLowerCase();
      if (typeof bVal === 'string') bVal = bVal.toLowerCase();

      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    });

    return result;
  }, [credentials, filters, sortKey, sortDirection]);

  // Paginate
  const totalPages = Math.ceil(filteredAndSortedCredentials.length / pageSize);
  const paginatedCredentials = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredAndSortedCredentials.slice(start, start + pageSize);
  }, [filteredAndSortedCredentials, currentPage, pageSize]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [filters, pageSize]);

  const handleSort = (key, direction) => {
    setSortKey(key);
    setSortDirection(direction);
  };

  const handleFilterChange = (key, value) => {
    setFilters(prev => ({ ...prev, [key]: value }));
  };

  const handleViewDetails = (credential) => {
    setSelectedCredential(credential);
    setShowDetailModal(true);
  };

  const handleToggleActive = (credential) => {
    setConfirmCredential(credential);
    setActionError(null);
    setShowToggleConfirm(true);
  };

  const confirmToggleActive = async () => {
    if (!confirmCredential) return;

    const newStatus = !confirmCredential.active;
    setIsToggling(true);
    setActionError(null);

    try {
      await apiClient.updateCredential(selectedTenant, confirmCredential.identifier, {
        active: newStatus
      });
      setShowToggleConfirm(false);
      setConfirmCredential(null);
      loadCredentials();
    } catch (err) {
      setActionError(err.message);
    } finally {
      setIsToggling(false);
    }
  };

  const handleDelete = (credential) => {
    setConfirmCredential(credential);
    setActionError(null);
    setShowDeleteConfirm(true);
  };

  const confirmDelete = async () => {
    if (!confirmCredential) return;

    setIsDeleting(true);
    setActionError(null);

    try {
      await apiClient.deleteCredential(selectedTenant, confirmCredential.identifier);
      setShowDeleteConfirm(false);
      setShowDetailModal(false);
      setConfirmCredential(null);
      setSelectedCredential(null);
      loadCredentials();
    } catch (err) {
      setActionError(err.message);
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

  const handleOpenEditModal = (credential) => {
    setEditCredential(credential);
    setEditName(credential.name || '');
    setEditActive(credential.active);
    setEditLabels(credential.labels || []);
    setEditTags(credential.tags || {});
    setEditError(null);
    setShowEditModal(true);
  };

  const handleCloseEditModal = () => {
    setShowEditModal(false);
    setEditCredential(null);
    setEditName('');
    setEditActive(true);
    setEditLabels([]);
    setEditTags({});
    setEditError(null);
  };

  const handleEditCredential = async (e) => {
    e.preventDefault();
    setEditError(null);

    setIsEditingCredential(true);
    try {
      await apiClient.updateCredential(selectedTenant, editCredential.identifier, {
        name: editName.trim() || null,
        active: editActive
      });
      // Update labels and tags
      await apiClient.updateCredentialLabels(selectedTenant, editCredential.identifier, editLabels);
      await apiClient.updateCredentialTags(selectedTenant, editCredential.identifier, editTags);
      handleCloseEditModal();
      loadCredentials();
    } catch (err) {
      setEditError(err.message);
    } finally {
      setIsEditingCredential(false);
    }
  };

  const handleCopyToken = async () => {
    if (!newCredentialToken) return;

    let success = false;

    // Try modern clipboard API first
    if (navigator.clipboard && navigator.clipboard.writeText) {
      try {
        await navigator.clipboard.writeText(newCredentialToken);
        success = true;
      } catch (err) {
        console.warn('Clipboard API failed, trying fallback:', err);
      }
    }

    // Fallback for non-HTTPS or older browsers
    if (!success) {
      try {
        const textArea = document.createElement('textarea');
        textArea.value = newCredentialToken;
        textArea.style.position = 'fixed';
        textArea.style.left = '-9999px';
        textArea.style.top = '-9999px';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        success = document.execCommand('copy');
        document.body.removeChild(textArea);
      } catch (err) {
        console.error('Fallback copy failed:', err);
      }
    }

    if (success) {
      setTokenCopied(true);
      setTimeout(() => setTokenCopied(false), 2000);
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

  const formatTokenPreview = (tokenPreview) => {
    if (!tokenPreview) return 'N/A';
    return tokenPreview;
  };

  const selectedTenantData = tenants?.find(t => t.identifier === selectedTenant);

  return (
    <div className="credentials-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <div className="workspace-title-row">
            <h2>Credentials</h2>
            {selectedTenant && <span className="count-badge">{filteredAndSortedCredentials.length}</span>}
          </div>
          <p className="workspace-subtitle">Generate and manage API access tokens for a tenant</p>
        </div>
        <div className="workspace-actions">
          {selectedTenant && (
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)} title="Create a new API credential">
              Create Credential
            </button>
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
          title="Select a tenant to manage credentials"
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
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)} title="Create a new API credential">
              Create Credential
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={pageSize}
            totalItems={filteredAndSortedCredentials.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={setPageSize}
            onRefresh={loadCredentials}
          />
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader
                  label="Status"
                  sortKey="active"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters
                />
                <SortableHeader
                  label="ID"
                  sortKey="identifier"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  filterable
                  filterValue={filters.identifier || ''}
                  onFilterChange={handleFilterChange}
                  hasFilters
                />
                <SortableHeader
                  label="Description"
                  sortKey="name"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  filterable
                  filterValue={filters.name || ''}
                  onFilterChange={handleFilterChange}
                  hasFilters
                />
                <SortableHeader
                  label="Token"
                  sortKey="tokenPreview"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters
                />
                <SortableHeader
                  label="Created"
                  sortKey="createdUtc"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters
                />
                <th className="actions-column">Actions</th>
              </tr>
            </thead>
            <tbody>
              {paginatedCredentials.map((credential) => (
                <tr key={credential.identifier} className="clickable-row" onClick={() => handleOpenEditModal(credential)}>
                  <td>
                    <span className={`status-badge ${credential.active ? 'enabled' : 'disabled'}`}>
                      {credential.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td><CopyableId value={credential.identifier} /></td>
                  <td>{credential.name || '-'}</td>
                  <td className="credential-token">{formatTokenPreview(credential.tokenPreview)}</td>
                  <td>{formatDate(credential.createdUtc)}</td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <ActionMenu
                      actions={[
                        {
                          label: 'Details',
                          onClick: () => handleViewDetails(credential)
                        },
                        {
                          label: 'Edit',
                          onClick: () => handleOpenEditModal(credential)
                        },
                        {
                          label: credential.active ? 'Deactivate' : 'Activate',
                          onClick: () => handleToggleActive(credential),
                          disabled: isToggling
                        },
                        {
                          label: 'View JSON',
                          onClick: () => {
                            setMetadataCredential(credential);
                            setShowMetadataModal(true);
                          }
                        },
                        {
                          label: 'Delete',
                          variant: 'danger',
                          onClick: () => handleDelete(credential)
                        }
                      ]}
                    />
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
              title="Optional description for this credential"
            />
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCloseCreateModal}
              disabled={isCreating}
              title="Cancel"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isCreating}
              title="Create the credential"
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
              type="button"
              className="btn btn-secondary copy-btn"
              onClick={handleCopyToken}
              title="Copy API key to clipboard"
            >
              {tokenCopied ? 'Copied!' : 'Copy'}
            </button>
          </div>
          <div className="form-actions">
            <button
              className="btn btn-primary"
              onClick={handleCloseTokenModal}
              title="Close this dialog"
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
                  <span className="detail-value"><CopyableId value={selectedCredential.identifier} /></span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Tenant ID</span>
                  <span className="detail-value"><CopyableId value={selectedCredential.tenantId} /></span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">User ID</span>
                  <span className="detail-value"><CopyableId value={selectedCredential.userId} /></span>
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
                  <span className="detail-value credential-token">{formatTokenPreview(selectedCredential.tokenPreview)}</span>
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
                onClick={() => handleDelete(selectedCredential)}
                disabled={isDeleting}
                title="Permanently delete this credential"
              >
                Delete Credential
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedCredential(null);
                }}
                title="Close this dialog"
              >
                Close
              </button>
            </div>
          </div>
        )}
      </Modal>

      {/* Metadata Modal */}
      <MetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false);
          setMetadataCredential(null);
        }}
        title="Credential Metadata"
        data={metadataCredential}
      />

      {/* Toggle Active Confirm Modal */}
      <ConfirmModal
        isOpen={showToggleConfirm}
        onClose={() => {
          setShowToggleConfirm(false);
          setConfirmCredential(null);
          setActionError(null);
        }}
        onConfirm={confirmToggleActive}
        title={confirmCredential?.active ? 'Deactivate Credential' : 'Activate Credential'}
        message={confirmCredential?.active
          ? 'Are you sure you want to deactivate this credential? Applications using this API key will no longer be able to authenticate.'
          : 'Are you sure you want to activate this credential?'
        }
        entityName={confirmCredential?.identifier}
        confirmLabel={confirmCredential?.active ? 'Deactivate' : 'Activate'}
        variant={confirmCredential?.active ? 'warning' : 'info'}
        warningMessage={actionError || (confirmCredential?.active ? 'The credential can be reactivated later.' : null)}
        isLoading={isToggling}
      />

      {/* Delete Confirm Modal */}
      <ConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => {
          setShowDeleteConfirm(false);
          setConfirmCredential(null);
          setActionError(null);
        }}
        onConfirm={confirmDelete}
        title="Delete Credential"
        message="Are you sure you want to delete this credential? Any applications using this API key will no longer be able to authenticate."
        entityName={confirmCredential?.identifier}
        confirmLabel="Delete"
        variant="danger"
        warningMessage={actionError || 'This action cannot be undone.'}
        isLoading={isDeleting}
      />

      {/* Edit Credential Modal */}
      <Modal
        isOpen={showEditModal}
        onClose={handleCloseEditModal}
        title={`Edit Credential: ${editCredential?.identifier || ''}`}
      >
        <form onSubmit={handleEditCredential} className="credential-form">
          {editError && (
            <div className="form-error">{editError}</div>
          )}
          <div className="form-group">
            <label>ID</label>
            <div className="form-static-value">
              <CopyableId value={editCredential?.identifier} />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="editCredentialName">Name/Description</label>
            <input
              type="text"
              id="editCredentialName"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              placeholder="Optional description (e.g., 'Production API Key')"
              disabled={isEditingCredential}
              autoFocus
              title="Name or description for this credential"
            />
          </div>
          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={editActive}
                onChange={(e) => setEditActive(e.target.checked)}
                disabled={isEditingCredential}
                title="Enable or disable this credential"
              />
              <span>Active</span>
            </label>
          </div>
          <div className="form-group">
            <label>Labels</label>
            <TagInput
              value={editLabels}
              onChange={setEditLabels}
              placeholder="Add a label..."
            />
          </div>
          <div className="form-group">
            <label>Tags</label>
            <KeyValueEditor
              value={editTags}
              onChange={setEditTags}
            />
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCloseEditModal}
              disabled={isEditingCredential}
              title="Cancel"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isEditingCredential}
              title="Save changes"
            >
              {isEditingCredential ? 'Saving...' : 'Save Changes'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

export default CredentialsView;
