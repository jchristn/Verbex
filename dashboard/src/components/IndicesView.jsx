import { useState, useMemo } from 'react';
import { useAuth } from '../context/AuthContext';
import IndexForm from './IndexForm';
import Modal from './Modal';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import ActionMenu from './ActionMenu';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import Pagination from './Pagination';
import SortableHeader from './SortableHeader';
import './IndicesView.css';

function IndicesView({ indices, isLoading, onRefresh, onIndexSelectAndNavigate, tenants = [] }) {
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

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataIndex, setMetadataIndex] = useState(null);

  // Sorting
  const [sortKey, setSortKey] = useState('name');
  const [sortDirection, setSortDirection] = useState('asc');

  // Filtering
  const [filters, setFilters] = useState({});

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Filter and sort indices
  const filteredAndSortedIndices = useMemo(() => {
    let result = [...indices];

    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      if (value) {
        result = result.filter(index => {
          const fieldValue = index[key];
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
  }, [indices, filters, sortKey, sortDirection]);

  // Paginate
  const totalPages = Math.ceil(filteredAndSortedIndices.length / pageSize);
  const paginatedIndices = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredAndSortedIndices.slice(start, start + pageSize);
  }, [filteredAndSortedIndices, currentPage, pageSize]);

  const handleSort = (key, direction) => {
    setSortKey(key);
    setSortDirection(direction);
  };

  const handleFilterChange = (key, value) => {
    setFilters(prev => ({ ...prev, [key]: value }));
    setCurrentPage(1);
  };

  const handleViewDetails = async (index) => {
    setSelectedIndex(index);
    setShowDetailModal(true);

    try {
      const response = await apiClient.getIndex(index.identifier);
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
      await apiClient.updateIndexLabels(indexDetails.identifier, editLabels);
      const response = await apiClient.getIndex(indexDetails.identifier);
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
      await apiClient.updateIndexTags(indexDetails.identifier, editTags);
      const response = await apiClient.getIndex(indexDetails.identifier);
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
          <span className="count-badge">{filteredAndSortedIndices.length}</span>
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
          <table className="data-table">
            <thead>
              <tr>
                <SortableHeader
                  label="Status"
                  sortKey="enabled"
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
                  label="Storage"
                  sortKey="inMemory"
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
              {paginatedIndices.map((index) => (
                <tr key={index.identifier}>
                  <td>
                    <span className={`status-badge ${index.enabled ? 'enabled' : 'disabled'}`}>
                      {index.enabled ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td><CopyableId value={index.identifier} /></td>
                  <td>{index.name || '-'}</td>
                  <td>{index.inMemory ? 'Memory' : 'Disk'}</td>
                  <td>{formatDate(index.createdUtc)}</td>
                  <td>
                    <ActionMenu
                      actions={[
                        {
                          label: 'Details',
                          onClick: () => handleViewDetails(index)
                        },
                        {
                          label: 'Documents',
                          onClick: () => onIndexSelectAndNavigate(index.identifier)
                        },
                        {
                          label: 'View Metadata',
                          onClick: () => {
                            setMetadataIndex(index);
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
            totalItems={filteredAndSortedIndices.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={(size) => { setPageSize(size); setCurrentPage(1); }}
          />
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
          tenants={tenants}
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
                  <span className="detail-label">Identifier</span>
                  <span className="detail-value"><CopyableId value={indexDetails.identifier} /></span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Tenant ID</span>
                  <span className="detail-value"><CopyableId value={indexDetails.tenantId} /></span>
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
                onClick={() => handleDelete(indexDetails.identifier)}
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

      {/* Metadata Modal */}
      <MetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false);
          setMetadataIndex(null);
        }}
        title="Index Metadata"
        data={metadataIndex}
      />
    </div>
  );
}

export default IndicesView;
