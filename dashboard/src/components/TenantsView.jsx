import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import ActionMenu from './ActionMenu';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import Pagination from './Pagination';
import SortableHeader from './SortableHeader';
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

  // Edit modal state
  const [showEditModal, setShowEditModal] = useState(false);
  const [editTenant, setEditTenant] = useState(null);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editActive, setEditActive] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [editError, setEditError] = useState(null);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataTenant, setMetadataTenant] = useState(null);

  // Sorting
  const [sortKey, setSortKey] = useState('name');
  const [sortDirection, setSortDirection] = useState('asc');

  // Filtering
  const [filters, setFilters] = useState({});

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const loadTenants = useCallback(async (signal) => {
    if (!apiClient) return;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getTenants({ signal });
      setTenants(response.data?.tenants || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.error('Failed to load tenants:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  }, [apiClient]);

  useEffect(() => {
    const abortController = new AbortController();
    loadTenants(abortController.signal);
    return () => abortController.abort();
  }, [loadTenants]);

  // Filter and sort tenants
  const filteredAndSortedTenants = useMemo(() => {
    let result = [...tenants];

    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      if (value) {
        result = result.filter(tenant => {
          const fieldValue = tenant[key];
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
  }, [tenants, filters, sortKey, sortDirection]);

  // Paginate
  const totalPages = Math.ceil(filteredAndSortedTenants.length / pageSize);
  const paginatedTenants = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredAndSortedTenants.slice(start, start + pageSize);
  }, [filteredAndSortedTenants, currentPage, pageSize]);

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

  const handleOpenEditModal = (tenant) => {
    setEditTenant(tenant);
    setEditName(tenant.name || '');
    setEditDescription(tenant.description || '');
    setEditActive(tenant.active);
    setEditError(null);
    setShowEditModal(true);
  };

  const handleCloseEditModal = () => {
    setShowEditModal(false);
    setEditTenant(null);
    setEditName('');
    setEditDescription('');
    setEditActive(true);
    setEditError(null);
  };

  const handleEdit = async (e) => {
    e.preventDefault();
    setEditError(null);

    if (!editName.trim()) {
      setEditError('Tenant name is required');
      return;
    }

    setIsEditing(true);
    try {
      await apiClient.updateTenant(editTenant.identifier, {
        name: editName.trim(),
        description: editDescription.trim(),
        active: editActive
      });
      handleCloseEditModal();
      loadTenants();
    } catch (err) {
      setEditError(err.message);
    } finally {
      setIsEditing(false);
    }
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
          <span className="count-badge">{filteredAndSortedTenants.length}</span>
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
                  label="Name"
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
              {paginatedTenants.map((tenant) => (
                <tr key={tenant.identifier}>
                  <td>
                    <span className={`status-badge ${tenant.active ? 'enabled' : 'disabled'}`}>
                      {tenant.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td><CopyableId value={tenant.identifier} /></td>
                  <td>{tenant.name || '-'}</td>
                  <td>{formatDate(tenant.createdUtc)}</td>
                  <td>
                    <ActionMenu
                      actions={[
                        {
                          label: 'Details',
                          onClick: () => handleViewDetails(tenant)
                        },
                        {
                          label: 'Edit',
                          onClick: () => handleOpenEditModal(tenant)
                        },
                        {
                          label: 'Users',
                          onClick: () => onTenantSelect && onTenantSelect(tenant.identifier, 'users')
                        },
                        {
                          label: 'Credentials',
                          onClick: () => onTenantSelect && onTenantSelect(tenant.identifier, 'credentials')
                        },
                        {
                          label: 'View Metadata',
                          onClick: () => {
                            setMetadataTenant(tenant);
                            setShowMetadataModal(true);
                          }
                        }
                      ]}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={pageSize}
            totalItems={filteredAndSortedTenants.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={setPageSize}
          />
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
                  <span className="detail-value"><CopyableId value={selectedTenant.identifier} /></span>
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

      {/* Metadata Modal */}
      <MetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false);
          setMetadataTenant(null);
        }}
        title="Tenant Metadata"
        data={metadataTenant}
      />

      {/* Edit Tenant Modal */}
      <Modal
        isOpen={showEditModal}
        onClose={handleCloseEditModal}
        title={`Edit Tenant: ${editTenant?.name || editTenant?.identifier || ''}`}
      >
        <form onSubmit={handleEdit} className="tenant-form">
          {editError && (
            <div className="form-error">{editError}</div>
          )}
          <div className="form-group">
            <label>ID</label>
            <div className="form-static-value">
              <CopyableId value={editTenant?.identifier} />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="editTenantName">Name *</label>
            <input
              type="text"
              id="editTenantName"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              placeholder="Enter tenant name"
              disabled={isEditing}
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="editTenantDescription">Description</label>
            <textarea
              id="editTenantDescription"
              value={editDescription}
              onChange={(e) => setEditDescription(e.target.value)}
              placeholder="Optional description"
              disabled={isEditing}
              rows={3}
            />
          </div>
          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={editActive}
                onChange={(e) => setEditActive(e.target.checked)}
                disabled={isEditing}
              />
              <span>Active</span>
            </label>
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCloseEditModal}
              disabled={isEditing}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isEditing}
            >
              {isEditing ? 'Saving...' : 'Save Changes'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

export default TenantsView;
