import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import ActionMenu from './ActionMenu';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import Pagination from './Pagination';
import SortableHeader from './SortableHeader';
import './UsersView.css';

function UsersView({ selectedTenant, tenants, onTenantSelect }) {
  const { apiClient } = useAuth();
  const [users, setUsers] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState(null);

  // Create form state
  const [createEmail, setCreateEmail] = useState('');
  const [createPassword, setCreatePassword] = useState('');
  const [createFirstName, setCreateFirstName] = useState('');
  const [createLastName, setCreateLastName] = useState('');
  const [createIsAdmin, setCreateIsAdmin] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState(null);

  // Edit modal state
  const [showEditModal, setShowEditModal] = useState(false);
  const [editUser, setEditUser] = useState(null);
  const [editEmail, setEditEmail] = useState('');
  const [editPassword, setEditPassword] = useState('');
  const [editFirstName, setEditFirstName] = useState('');
  const [editLastName, setEditLastName] = useState('');
  const [editIsAdmin, setEditIsAdmin] = useState(false);
  const [editActive, setEditActive] = useState(true);
  const [isEditingUser, setIsEditingUser] = useState(false);
  const [editError, setEditError] = useState(null);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataUser, setMetadataUser] = useState(null);

  // Sorting
  const [sortKey, setSortKey] = useState('email');
  const [sortDirection, setSortDirection] = useState('asc');

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

  const loadUsers = useCallback(async (signal) => {
    if (!apiClient || !selectedTenant) return;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getUsers(selectedTenant, { signal });
      setUsers(response.data?.users || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      console.error('Failed to load users:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  }, [apiClient, selectedTenant]);

  useEffect(() => {
    if (selectedTenant) {
      const abortController = new AbortController();
      loadUsers(abortController.signal);
      return () => abortController.abort();
    } else {
      setUsers([]);
    }
  }, [loadUsers]);

  // Filter and sort users
  const filteredAndSortedUsers = useMemo(() => {
    let result = [...users];

    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      if (value) {
        result = result.filter(user => {
          const fieldValue = user[key];
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
  }, [users, filters, sortKey, sortDirection]);

  // Paginate
  const totalPages = Math.ceil(filteredAndSortedUsers.length / pageSize);
  const paginatedUsers = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredAndSortedUsers.slice(start, start + pageSize);
  }, [filteredAndSortedUsers, currentPage, pageSize]);

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

  const handleViewDetails = (user) => {
    setSelectedUser(user);
    setShowDetailModal(true);
  };

  const handleDelete = async (userId) => {
    if (!confirm(`Are you sure you want to delete this user? This action cannot be undone.`)) {
      return;
    }

    setIsDeleting(true);
    try {
      await apiClient.deleteUser(selectedTenant, userId);
      setShowDetailModal(false);
      setSelectedUser(null);
      loadUsers();
    } catch (err) {
      alert(`Failed to delete user: ${err.message}`);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    setCreateError(null);

    if (!createEmail.trim()) {
      setCreateError('Email is required');
      return;
    }

    if (!createPassword.trim()) {
      setCreateError('Password is required');
      return;
    }

    if (createPassword.length < 6) {
      setCreateError('Password must be at least 6 characters');
      return;
    }

    setIsCreating(true);
    try {
      await apiClient.createUser(selectedTenant, {
        email: createEmail.trim(),
        password: createPassword,
        firstName: createFirstName.trim() || undefined,
        lastName: createLastName.trim() || undefined,
        isAdmin: createIsAdmin
      });
      setShowCreateModal(false);
      resetCreateForm();
      loadUsers();
    } catch (err) {
      setCreateError(err.message);
    } finally {
      setIsCreating(false);
    }
  };

  const resetCreateForm = () => {
    setCreateEmail('');
    setCreatePassword('');
    setCreateFirstName('');
    setCreateLastName('');
    setCreateIsAdmin(false);
    setCreateError(null);
  };

  const handleCloseCreateModal = () => {
    setShowCreateModal(false);
    resetCreateForm();
  };

  const handleOpenEditModal = (user) => {
    setEditUser(user);
    setEditEmail(user.email || '');
    setEditPassword('');
    setEditFirstName(user.firstName || '');
    setEditLastName(user.lastName || '');
    setEditIsAdmin(user.isAdmin || false);
    setEditActive(user.active);
    setEditError(null);
    setShowEditModal(true);
  };

  const handleCloseEditModal = () => {
    setShowEditModal(false);
    setEditUser(null);
    setEditEmail('');
    setEditPassword('');
    setEditFirstName('');
    setEditLastName('');
    setEditIsAdmin(false);
    setEditActive(true);
    setEditError(null);
  };

  const handleEditUser = async (e) => {
    e.preventDefault();
    setEditError(null);

    if (!editEmail.trim()) {
      setEditError('Email is required');
      return;
    }

    if (editPassword && editPassword.length < 6) {
      setEditError('Password must be at least 6 characters');
      return;
    }

    setIsEditingUser(true);
    try {
      const updates = {
        email: editEmail.trim(),
        firstName: editFirstName.trim() || null,
        lastName: editLastName.trim() || null,
        isAdmin: editIsAdmin,
        active: editActive
      };
      if (editPassword) {
        updates.password = editPassword;
      }
      await apiClient.updateUser(selectedTenant, editUser.identifier, updates);
      handleCloseEditModal();
      loadUsers();
    } catch (err) {
      setEditError(err.message);
    } finally {
      setIsEditingUser(false);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  const selectedTenantData = tenants?.find(t => t.identifier === selectedTenant);

  return (
    <div className="users-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Users</h2>
          {selectedTenant && <span className="count-badge">{filteredAndSortedUsers.length}</span>}
        </div>
        <div className="workspace-actions">
          {selectedTenant && (
            <>
              <button className="btn btn-secondary" onClick={loadUsers}>
                Refresh
              </button>
              <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
                Create User
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
            <div className="empty-state-icon">👤</div>
            <h3 className="empty-state-title">Select a Tenant</h3>
            <p className="empty-state-description">
              Select a tenant above to view and manage its users.
            </p>
          </div>
        </div>
      ) : isLoading ? (
        <div className="workspace-card">
          <div className="loading-spinner">Loading users...</div>
        </div>
      ) : error ? (
        <div className="workspace-card error-card">
          <p className="error-message">Error: {error}</p>
        </div>
      ) : users.length === 0 ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">👤</div>
            <h3 className="empty-state-title">No Users Found</h3>
            <p className="empty-state-description">
              Create your first user for tenant "{selectedTenantData?.name || selectedTenant}".
            </p>
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              Create User
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
                  label="Email"
                  sortKey="email"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  filterable
                  filterValue={filters.email || ''}
                  onFilterChange={handleFilterChange}
                  hasFilters
                />
                <SortableHeader
                  label="Name"
                  sortKey="firstName"
                  currentSort={sortKey}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  filterable
                  filterValue={filters.firstName || ''}
                  onFilterChange={handleFilterChange}
                  hasFilters
                />
                <SortableHeader
                  label="Role"
                  sortKey="isAdmin"
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
              {paginatedUsers.map((user) => (
                <tr key={user.identifier}>
                  <td>
                    <span className={`status-badge ${user.active ? 'enabled' : 'disabled'}`}>
                      {user.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td><CopyableId value={user.identifier} /></td>
                  <td>{user.email}</td>
                  <td>{[user.firstName, user.lastName].filter(Boolean).join(' ') || '-'}</td>
                  <td>
                    <span className={`role-badge ${user.isAdmin ? 'admin' : 'user'}`}>
                      {user.isAdmin ? 'Admin' : 'User'}
                    </span>
                  </td>
                  <td>{formatDate(user.createdUtc)}</td>
                  <td>
                    <ActionMenu
                      actions={[
                        {
                          label: 'Details',
                          onClick: () => handleViewDetails(user)
                        },
                        {
                          label: 'Edit',
                          onClick: () => handleOpenEditModal(user)
                        },
                        {
                          label: 'View Metadata',
                          onClick: () => {
                            setMetadataUser(user);
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
            totalItems={filteredAndSortedUsers.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={setPageSize}
          />
        </div>
      )}

      {/* Create User Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={handleCloseCreateModal}
        title="Create New User"
      >
        <form onSubmit={handleCreate} className="user-form">
          {createError && (
            <div className="form-error">{createError}</div>
          )}
          <div className="form-group">
            <label htmlFor="userEmail">Email *</label>
            <input
              type="email"
              id="userEmail"
              value={createEmail}
              onChange={(e) => setCreateEmail(e.target.value)}
              placeholder="user@example.com"
              disabled={isCreating}
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="userPassword">Password *</label>
            <input
              type="password"
              id="userPassword"
              value={createPassword}
              onChange={(e) => setCreatePassword(e.target.value)}
              placeholder="At least 6 characters"
              disabled={isCreating}
            />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="userFirstName">First Name</label>
              <input
                type="text"
                id="userFirstName"
                value={createFirstName}
                onChange={(e) => setCreateFirstName(e.target.value)}
                placeholder="Optional"
                disabled={isCreating}
              />
            </div>
            <div className="form-group">
              <label htmlFor="userLastName">Last Name</label>
              <input
                type="text"
                id="userLastName"
                value={createLastName}
                onChange={(e) => setCreateLastName(e.target.value)}
                placeholder="Optional"
                disabled={isCreating}
              />
            </div>
          </div>
          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={createIsAdmin}
                onChange={(e) => setCreateIsAdmin(e.target.checked)}
                disabled={isCreating}
              />
              <span>Tenant Administrator</span>
            </label>
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
              {isCreating ? 'Creating...' : 'Create User'}
            </button>
          </div>
        </form>
      </Modal>

      {/* User Details Modal */}
      <Modal
        isOpen={showDetailModal}
        onClose={() => {
          setShowDetailModal(false);
          setSelectedUser(null);
        }}
        title={`User: ${selectedUser?.email || ''}`}
      >
        {selectedUser && (
          <div className="user-details">
            <div className="details-section">
              <h4>General Information</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">ID</span>
                  <span className="detail-value"><CopyableId value={selectedUser.identifier} /></span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Tenant ID</span>
                  <span className="detail-value"><CopyableId value={selectedUser.tenantId} /></span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Email</span>
                  <span className="detail-value">{selectedUser.email}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">First Name</span>
                  <span className="detail-value">{selectedUser.firstName || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Last Name</span>
                  <span className="detail-value">{selectedUser.lastName || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Status</span>
                  <span className={`status-badge ${selectedUser.active ? 'enabled' : 'disabled'}`}>
                    {selectedUser.active ? 'Active' : 'Disabled'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Role</span>
                  <span className={`role-badge ${selectedUser.isAdmin ? 'admin' : 'user'}`}>
                    {selectedUser.isAdmin ? 'Admin' : 'User'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Created</span>
                  <span className="detail-value">{formatDate(selectedUser.createdUtc)}</span>
                </div>
              </div>
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDelete(selectedUser.identifier)}
                disabled={isDeleting}
              >
                {isDeleting ? 'Deleting...' : 'Delete User'}
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedUser(null);
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
          setMetadataUser(null);
        }}
        title="User Metadata"
        data={metadataUser}
      />

      {/* Edit User Modal */}
      <Modal
        isOpen={showEditModal}
        onClose={handleCloseEditModal}
        title={`Edit User: ${editUser?.email || ''}`}
      >
        <form onSubmit={handleEditUser} className="user-form">
          {editError && (
            <div className="form-error">{editError}</div>
          )}
          <div className="form-group">
            <label>ID</label>
            <div className="form-static-value">
              <CopyableId value={editUser?.identifier} />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="editUserEmail">Email *</label>
            <input
              type="email"
              id="editUserEmail"
              value={editEmail}
              onChange={(e) => setEditEmail(e.target.value)}
              placeholder="user@example.com"
              disabled={isEditingUser}
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="editUserPassword">Password</label>
            <input
              type="password"
              id="editUserPassword"
              value={editPassword}
              onChange={(e) => setEditPassword(e.target.value)}
              placeholder="Leave blank to keep current password"
              disabled={isEditingUser}
            />
            <span className="form-hint">Leave blank to keep current password</span>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="editUserFirstName">First Name</label>
              <input
                type="text"
                id="editUserFirstName"
                value={editFirstName}
                onChange={(e) => setEditFirstName(e.target.value)}
                placeholder="Optional"
                disabled={isEditingUser}
              />
            </div>
            <div className="form-group">
              <label htmlFor="editUserLastName">Last Name</label>
              <input
                type="text"
                id="editUserLastName"
                value={editLastName}
                onChange={(e) => setEditLastName(e.target.value)}
                placeholder="Optional"
                disabled={isEditingUser}
              />
            </div>
          </div>
          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={editIsAdmin}
                onChange={(e) => setEditIsAdmin(e.target.checked)}
                disabled={isEditingUser}
              />
              <span>Tenant Administrator</span>
            </label>
          </div>
          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={editActive}
                onChange={(e) => setEditActive(e.target.checked)}
                disabled={isEditingUser}
              />
              <span>Active</span>
            </label>
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCloseEditModal}
              disabled={isEditingUser}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isEditingUser}
            >
              {isEditingUser ? 'Saving...' : 'Save Changes'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

export default UsersView;
