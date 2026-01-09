import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
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

  const loadUsers = async () => {
    if (!apiClient || !selectedTenant) return;

    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.getUsers(selectedTenant);
      setUsers(response.data?.users || []);
    } catch (err) {
      console.error('Failed to load users:', err);
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (selectedTenant) {
      loadUsers();
    } else {
      setUsers([]);
    }
  }, [apiClient, selectedTenant]);

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
          {selectedTenant && <span className="count-badge">{users.length}</span>}
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
          <table className="users-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Email</th>
                <th>Name</th>
                <th>Role</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.identifier}>
                  <td>
                    <span className={`status-badge ${user.active ? 'enabled' : 'disabled'}`}>
                      {user.active ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td className="user-email">{user.email}</td>
                  <td>{[user.firstName, user.lastName].filter(Boolean).join(' ') || '-'}</td>
                  <td>
                    <span className={`role-badge ${user.isAdmin ? 'admin' : 'user'}`}>
                      {user.isAdmin ? 'Admin' : 'User'}
                    </span>
                  </td>
                  <td>{formatDate(user.createdUtc)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => handleViewDetails(user)}
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
                  <span className="detail-value">{selectedUser.identifier}</span>
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
    </div>
  );
}

export default UsersView;
