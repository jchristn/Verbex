import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import IndexForm from './IndexForm';
import Modal from './Modal';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './IndicesView.css';

function IndicesView({ indices, isLoading, onRefresh, onIndexSelectAndNavigate }) {
  const { apiClient } = useAuth();
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(null);
  const [indexDetails, setIndexDetails] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // Edit mode states
  const [editingLabels, setEditingLabels] = useState(false);
  const [editingTags, setEditingTags] = useState(false);
  const [editLabels, setEditLabels] = useState([]);
  const [editTags, setEditTags] = useState({});
  const [isSavingLabels, setIsSavingLabels] = useState(false);
  const [isSavingTags, setIsSavingTags] = useState(false);

  const handleViewDetails = async (index) => {
    setSelectedIndex(index);
    setShowDetailModal(true);

    try {
      const response = await apiClient.getIndex(index.id);
      setIndexDetails(response.data);
    } catch (err) {
      console.error('Failed to load index details:', err);
      setIndexDetails(null);
    }
  };

  const handleDelete = async (indexId) => {
    if (!confirm(`Are you sure you want to delete index "${indexId}"? This action cannot be undone.`)) {
      return;
    }

    setIsDeleting(true);
    try {
      await apiClient.deleteIndex(indexId);
      setShowDetailModal(false);
      onRefresh();
    } catch (err) {
      alert(`Failed to delete index: ${err.message}`);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleCreateSuccess = () => {
    setShowCreateModal(false);
    onRefresh();
  };

  const handleStartEditLabels = () => {
    setEditLabels(indexDetails.labels || []);
    setEditingLabels(true);
  };

  const handleCancelEditLabels = () => {
    setEditingLabels(false);
    setEditLabels([]);
  };

  const handleSaveLabels = async () => {
    setIsSavingLabels(true);
    try {
      await apiClient.updateIndexLabels(indexDetails.id, editLabels);
      // Refresh index details
      const response = await apiClient.getIndex(indexDetails.id);
      setIndexDetails(response.data);
      setEditingLabels(false);
      onRefresh();
    } catch (err) {
      alert(`Failed to update labels: ${err.message}`);
    } finally {
      setIsSavingLabels(false);
    }
  };

  const handleStartEditTags = () => {
    setEditTags(indexDetails.tags || {});
    setEditingTags(true);
  };

  const handleCancelEditTags = () => {
    setEditingTags(false);
    setEditTags({});
  };

  const handleSaveTags = async () => {
    setIsSavingTags(true);
    try {
      await apiClient.updateIndexTags(indexDetails.id, editTags);
      // Refresh index details
      const response = await apiClient.getIndex(indexDetails.id);
      setIndexDetails(response.data);
      setEditingTags(false);
      onRefresh();
    } catch (err) {
      alert(`Failed to update tags: ${err.message}`);
    } finally {
      setIsSavingTags(false);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  const formatSize = (bytes) => {
    if (!bytes) return 'N/A';
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(2)} ${sizes[i]}`;
  };

  if (isLoading) {
    return (
      <div className="indices-view">
        <div className="loading-spinner">Loading indices...</div>
      </div>
    );
  }

  return (
    <div className="indices-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Indices</h2>
          <span className="count-badge">{indices.length}</span>
        </div>
        <div className="workspace-actions">
          <button className="btn btn-secondary" onClick={onRefresh}>
            Refresh
          </button>
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            Create Index
          </button>
        </div>
      </div>

      {indices.length === 0 ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">📚</div>
            <h3 className="empty-state-title">No Indices Found</h3>
            <p className="empty-state-description">
              Create your first index to start indexing and searching documents.
            </p>
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              Create Index
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <table className="indices-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>ID</th>
                <th>Name</th>
                <th>Storage</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {indices.map((index) => (
                <tr key={index.id}>
                  <td>
                    <span className={`status-badge ${index.enabled ? 'enabled' : 'disabled'}`}>
                      {index.enabled ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td className="index-id">{index.id}</td>
                  <td>{index.name || '-'}</td>
                  <td>{index.inMemory ? 'Memory' : 'Disk'}</td>
                  <td>{formatDate(index.createdUtc)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => handleViewDetails(index)}
                      >
                        Details
                      </button>
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={() => {
                          onIndexSelectAndNavigate(index.id);
                        }}
                      >
                        Documents
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Create Index Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create New Index"
      >
        <IndexForm
          onSuccess={handleCreateSuccess}
          onCancel={() => setShowCreateModal(false)}
        />
      </Modal>

      {/* Index Details Modal */}
      <Modal
        isOpen={showDetailModal}
        onClose={() => {
          setShowDetailModal(false);
          setSelectedIndex(null);
          setIndexDetails(null);
        }}
        title={`Index: ${selectedIndex?.id || ''}`}
      >
        {indexDetails ? (
          <div className="index-details">
            <div className="details-section">
              <h4>General Information</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">ID</span>
                  <span className="detail-value">{indexDetails.id}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Name</span>
                  <span className="detail-value">{indexDetails.name || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Description</span>
                  <span className="detail-value">{indexDetails.description || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Status</span>
                  <span className={`status-badge ${indexDetails.enabled ? 'enabled' : 'disabled'}`}>
                    {indexDetails.enabled ? 'Active' : 'Disabled'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Storage Mode</span>
                  <span className="detail-value">{indexDetails.inMemory ? 'In-Memory' : 'Persistent'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Created</span>
                  <span className="detail-value">{formatDate(indexDetails.createdUtc)}</span>
                </div>
              </div>
            </div>

            {indexDetails.statistics && (
              <div className="details-section">
                <h4>Statistics</h4>
                <div className="details-grid">
                  <div className="detail-item">
                    <span className="detail-label">Documents</span>
                    <span className="detail-value">{indexDetails.statistics.documentCount?.toLocaleString() || 0}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Terms</span>
                    <span className="detail-value">{indexDetails.statistics.termCount?.toLocaleString() || 0}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Index Size</span>
                    <span className="detail-value">{formatSize(indexDetails.statistics.indexSize)}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Avg Document Length</span>
                    <span className="detail-value">{indexDetails.statistics.averageDocumentLength?.toFixed(2) || 'N/A'}</span>
                  </div>
                </div>
              </div>
            )}

            <div className="details-section">
              <div className="section-header">
                <h4>Labels</h4>
                {!editingLabels && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditLabels}>
                    Edit
                  </button>
                )}
              </div>
              {editingLabels ? (
                <div className="edit-section">
                  <TagInput
                    value={editLabels}
                    onChange={setEditLabels}
                    placeholder="Add labels..."
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveLabels}
                      disabled={isSavingLabels}
                    >
                      {isSavingLabels ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditLabels}
                      disabled={isSavingLabels}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : indexDetails.labels && indexDetails.labels.length > 0 ? (
                <div className="index-labels">
                  {indexDetails.labels.map((label, i) => (
                    <span key={i} className="label-badge">{label}</span>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No labels assigned to this index.</p>
              )}
            </div>

            <div className="details-section">
              <div className="section-header">
                <h4>Tags</h4>
                {!editingTags && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditTags}>
                    Edit
                  </button>
                )}
              </div>
              {editingTags ? (
                <div className="edit-section">
                  <KeyValueEditor
                    value={editTags}
                    onChange={setEditTags}
                    keyPlaceholder="Tag name"
                    valuePlaceholder="Tag value"
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveTags}
                      disabled={isSavingTags}
                    >
                      {isSavingTags ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditTags}
                      disabled={isSavingTags}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : indexDetails.tags && Object.keys(indexDetails.tags).length > 0 ? (
                <div className="index-tags">
                  {Object.entries(indexDetails.tags).map(([key, value], i) => (
                    <div key={i} className="tag-item">
                      <span className="tag-key">{key}</span>
                      <span className="tag-separator">=</span>
                      <span className="tag-value">{value}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No tags assigned to this index.</p>
              )}
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDelete(indexDetails.id)}
                disabled={isDeleting}
              >
                {isDeleting ? 'Deleting...' : 'Delete Index'}
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedIndex(null);
                  setIndexDetails(null);
                }}
              >
                Close
              </button>
            </div>
          </div>
        ) : (
          <div className="loading-spinner">Loading index details...</div>
        )}
      </Modal>
    </div>
  );
}

export default IndicesView;
