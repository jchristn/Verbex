import { useState, useMemo } from 'react';
import { useAuth } from '../context/AuthContext';
import IndexForm from './IndexForm';
import Modal from './Modal';
import AlertModal from './AlertModal';
import ConfirmModal from './ConfirmModal';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import JsonEditor from './JsonEditor';
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

  // Alert and delete confirmation modals
  const [alertModal, setAlertModal] = useState({ isOpen: false, title: '', message: '', variant: 'error' });
  const [deleteConfirm, setDeleteConfirm] = useState({ isOpen: false, index: null, isDeleting: false });

  // Edit mode states
  const [editingLabels, setEditingLabels] = useState(false);
  const [editingTags, setEditingTags] = useState(false);
  const [editingCustomMetadata, setEditingCustomMetadata] = useState(false);
  const [editLabels, setEditLabels] = useState([]);
  const [editTags, setEditTags] = useState({});
  const [editCustomMetadata, setEditCustomMetadata] = useState(null);
  const [isSavingLabels, setIsSavingLabels] = useState(false);
  const [isSavingTags, setIsSavingTags] = useState(false);
  const [isSavingCustomMetadata, setIsSavingCustomMetadata] = useState(false);

  // Edit details mode states
  const [editingDetails, setEditingDetails] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editEnabled, setEditEnabled] = useState(true);
  const [isSavingDetails, setIsSavingDetails] = useState(false);

  // Cache settings states
  const [editingCacheSettings, setEditingCacheSettings] = useState(false);
  const [editCacheConfig, setEditCacheConfig] = useState(null);
  const [isSavingCacheSettings, setIsSavingCacheSettings] = useState(false);
  const [isClearingCache, setIsClearingCache] = useState(false);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataIndex, setMetadataIndex] = useState(null);

  // Backup/Restore states
  const [isBackingUp, setIsBackingUp] = useState(null); // index ID being backed up
  const [backupIndexName, setBackupIndexName] = useState(''); // name of index being backed up
  const [showRestoreModal, setShowRestoreModal] = useState(false);
  const [restoreFile, setRestoreFile] = useState(null);
  const [restoreFileName, setRestoreFileName] = useState('');
  const [restoreName, setRestoreName] = useState('');
  const [isRestoring, setIsRestoring] = useState(false);
  const [restoreError, setRestoreError] = useState(null);
  const [showRestoreReplaceModal, setShowRestoreReplaceModal] = useState(false);
  const [restoreReplaceIndex, setRestoreReplaceIndex] = useState(null);
  const [restoreReplaceFile, setRestoreReplaceFile] = useState(null);
  const [restoreReplaceFileName, setRestoreReplaceFileName] = useState('');
  const [isRestoringReplace, setIsRestoringReplace] = useState(false);
  const [restoreReplaceError, setRestoreReplaceError] = useState(null);

  // Top Terms modal states
  const [showTopTermsModal, setShowTopTermsModal] = useState(false);
  const [topTermsIndex, setTopTermsIndex] = useState(null);
  const [topTermsData, setTopTermsData] = useState(null);
  const [isLoadingTopTerms, setIsLoadingTopTerms] = useState(false);
  const [topTermsError, setTopTermsError] = useState(null);

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

  const showAlert = (title, message, variant = 'error') => {
    setAlertModal({ isOpen: true, title, message, variant });
  };

  const closeAlert = () => {
    setAlertModal({ isOpen: false, title: '', message: '', variant: 'error' });
  };

  const handleDelete = (index) => {
    setDeleteConfirm({ isOpen: true, index, isDeleting: false });
  };

  const confirmDelete = async () => {
    const index = deleteConfirm.index;
    setDeleteConfirm(prev => ({ ...prev, isDeleting: true }));

    try {
      await apiClient.deleteIndex(index.identifier);
      setDeleteConfirm({ isOpen: false, index: null, isDeleting: false });
      setShowDetailModal(false);
      setSelectedIndex(null);
      setIndexDetails(null);
      onRefresh();
    } catch (err) {
      setDeleteConfirm({ isOpen: false, index: null, isDeleting: false });
      showAlert('Error', `Failed to delete index: ${err.message}`);
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

  const handleStartEditCustomMetadata = () => {
    setEditCustomMetadata(indexDetails.customMetadata !== undefined ? indexDetails.customMetadata : null);
    setEditingCustomMetadata(true);
  };

  const handleCancelEditCustomMetadata = () => {
    setEditingCustomMetadata(false);
    setEditCustomMetadata(null);
  };

  const handleSaveCustomMetadata = async () => {
    setIsSavingCustomMetadata(true);
    try {
      await apiClient.updateIndexCustomMetadata(indexDetails.identifier, editCustomMetadata);
      const response = await apiClient.getIndex(indexDetails.identifier);
      setIndexDetails(response.data);
      setEditingCustomMetadata(false);
      onRefresh();
    } catch (err) {
      alert(`Failed to update custom metadata: ${err.message}`);
    } finally {
      setIsSavingCustomMetadata(false);
    }
  };

  const handleStartEditDetails = () => {
    setEditName(indexDetails.name || '');
    setEditDescription(indexDetails.description || '');
    setEditEnabled(indexDetails.enabled);
    setEditingDetails(true);
  };

  const handleCancelEditDetails = () => {
    setEditingDetails(false);
  };

  const handleSaveDetails = async () => {
    setIsSavingDetails(true);
    try {
      await apiClient.updateIndex(indexDetails.identifier, {
        name: editName,
        description: editDescription,
        enabled: editEnabled
      });
      const response = await apiClient.getIndex(indexDetails.identifier);
      setIndexDetails(response.data);
      setEditingDetails(false);
      onRefresh();
    } catch (err) {
      showAlert('Error', `Failed to update index: ${err.message}`);
    } finally {
      setIsSavingDetails(false);
    }
  };

  // Cache settings handlers
  const handleStartEditCacheSettings = () => {
    const config = indexDetails.cacheConfiguration || {
      enabled: false,
      enableTermCache: true,
      termCacheCapacity: 10000,
      termCacheEvictCount: 1000,
      termCacheTtlSeconds: 300,
      termCacheSlidingExpiration: true,
      enableDocumentCache: true,
      documentCacheCapacity: 5000,
      documentCacheEvictCount: 500,
      documentCacheTtlSeconds: 600,
      documentCacheSlidingExpiration: true,
      enableStatisticsCache: true,
      statisticsCacheTtlSeconds: 60
    };
    setEditCacheConfig({ ...config });
    setEditingCacheSettings(true);
  };

  const handleCancelEditCacheSettings = () => {
    setEditingCacheSettings(false);
    setEditCacheConfig(null);
  };

  const handleCacheConfigChange = (e) => {
    const { name, value, type, checked } = e.target;
    setEditCacheConfig((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : (type === 'number' ? parseInt(value, 10) || 0 : value)
    }));
  };

  const handleSaveCacheSettings = async () => {
    setIsSavingCacheSettings(true);
    try {
      await apiClient.updateIndex(indexDetails.identifier, {
        cacheConfiguration: editCacheConfig
      });
      const response = await apiClient.getIndex(indexDetails.identifier);
      setIndexDetails(response.data);
      setEditingCacheSettings(false);
      setEditCacheConfig(null);
      onRefresh();
    } catch (err) {
      showAlert('Error', `Failed to update cache settings: ${err.message}`);
    } finally {
      setIsSavingCacheSettings(false);
    }
  };

  const handleClearCache = async () => {
    setIsClearingCache(true);
    try {
      await apiClient.clearIndexCache(indexDetails.identifier);
      const response = await apiClient.getIndex(indexDetails.identifier);
      setIndexDetails(response.data);
      showAlert('Success', 'Cache cleared successfully', 'success');
    } catch (err) {
      showAlert('Error', `Failed to clear cache: ${err.message}`);
    } finally {
      setIsClearingCache(false);
    }
  };

  const formatPercent = (value) => {
    if (value === null || value === undefined) return 'N/A';
    return `${(value * 100).toFixed(1)}%`;
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  // Format timestamp for backup filename: yyyyMMdd-HHmmss
  const formatBackupTimestamp = (date) => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');
    return `${year}${month}${day}-${hours}${minutes}${seconds}`;
  };

  // Backup handler
  const handleBackup = async (index) => {
    const indexName = index.name || index.identifier;
    setIsBackingUp(index.identifier);
    setBackupIndexName(indexName);
    try {
      const blob = await apiClient.backupIndex(index.identifier);

      // Create download link
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      const timestamp = formatBackupTimestamp(new Date());
      a.download = `Verbex_${indexName}_${timestamp}.vbx`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      showAlert('Success', `Backup of "${indexName}" is ready for download`, 'success');
    } catch (err) {
      showAlert('Backup Failed', err.message);
    } finally {
      setIsBackingUp(null);
      setBackupIndexName('');
    }
  };

  // Restore handler (new index)
  const handleRestore = async () => {
    if (!restoreFile) {
      setRestoreError('Please select a backup file');
      return;
    }

    setIsRestoring(true);
    setRestoreError(null);
    try {
      const result = await apiClient.restoreIndex(restoreFile, { name: restoreName || undefined });
      setShowRestoreModal(false);
      setRestoreFile(null);
      setRestoreFileName('');
      setRestoreName('');
      onRefresh();

      const message = result.data?.message || 'Index restored successfully';
      showAlert('Success', message, 'success');
    } catch (err) {
      setRestoreError(err.message);
    } finally {
      setIsRestoring(false);
    }
  };

  // Restore replace handler
  const handleRestoreReplace = async () => {
    if (!restoreReplaceFile || !restoreReplaceIndex) {
      setRestoreReplaceError('Please select a backup file');
      return;
    }

    setIsRestoringReplace(true);
    setRestoreReplaceError(null);
    try {
      const result = await apiClient.restoreReplaceIndex(restoreReplaceIndex.identifier, restoreReplaceFile);
      setShowRestoreReplaceModal(false);
      setRestoreReplaceFile(null);
      setRestoreReplaceFileName('');
      setRestoreReplaceIndex(null);
      onRefresh();

      const message = result.data?.message || 'Index replaced successfully';
      showAlert('Success', message, 'success');
    } catch (err) {
      setRestoreReplaceError(err.message);
    } finally {
      setIsRestoringReplace(false);
    }
  };

  // Top Terms handler
  const handleViewTopTerms = async (index) => {
    setTopTermsIndex(index);
    setShowTopTermsModal(true);
    setIsLoadingTopTerms(true);
    setTopTermsError(null);
    setTopTermsData(null);

    try {
      const response = await apiClient.getTopTerms(index.identifier, 25);
      setTopTermsData(response.data);
    } catch (err) {
      setTopTermsError(err.message);
    } finally {
      setIsLoadingTopTerms(false);
    }
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
          <div className="workspace-title-row">
            <h2>Indices</h2>
            <span className="count-badge">{filteredAndSortedIndices.length}</span>
          </div>
          <p className="workspace-subtitle">Create, configure, and manage your search indices</p>
        </div>
        <div className="workspace-actions">
          <button className="btn btn-secondary" onClick={() => setShowRestoreModal(true)} title="Restore an index from a backup file">
            Restore Backup
          </button>
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)} title="Create a new index">
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
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)} title="Create a new index">
              Create Index
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={pageSize}
            totalItems={filteredAndSortedIndices.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={(size) => { setPageSize(size); setCurrentPage(1); }}
            onRefresh={onRefresh}
          />
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
                <tr key={index.identifier} className="clickable-row" onClick={() => handleViewDetails(index)}>
                  <td>
                    <span className={`status-badge ${index.enabled ? 'enabled' : 'disabled'}`}>
                      {index.enabled ? 'Active' : 'Disabled'}
                    </span>
                  </td>
                  <td><CopyableId value={index.identifier} /></td>
                  <td>{index.name || '-'}</td>
                  <td>{index.inMemory ? 'Memory' : 'Disk'}</td>
                  <td>{formatDate(index.createdUtc)}</td>
                  <td onClick={(e) => e.stopPropagation()}>
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
                          label: 'View JSON',
                          onClick: () => {
                            setMetadataIndex(index);
                            setShowMetadataModal(true);
                          }
                        },
                        {
                          label: 'View Top Terms',
                          onClick: () => handleViewTopTerms(index)
                        },
                        ...(index.inMemory ? [] : [
                          {
                            label: isBackingUp === index.identifier ? 'Backing up...' : 'Backup',
                            onClick: () => handleBackup(index),
                            disabled: isBackingUp === index.identifier
                          },
                          {
                            label: 'Restore',
                            onClick: () => {
                              setRestoreReplaceIndex(index);
                              setShowRestoreReplaceModal(true);
                            }
                          }
                        ]),
                        {
                          label: 'Delete',
                          variant: 'danger',
                          onClick: () => handleDelete(index)
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
          setEditingDetails(false);
        }}
        title={`Index: ${indexDetails?.name || selectedIndex?.name || selectedIndex?.identifier || ''}`}
        size="large"
      >
        {indexDetails ? (
          <div className="index-details">
            <div className="details-section">
              <div className="section-header">
                <h4>General Information</h4>
                {!editingDetails && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDetails} title="Edit index details">
                    Edit
                  </button>
                )}
              </div>
              {editingDetails ? (
                <div className="edit-section">
                  <div className="form-group">
                    <label className="form-label">Name</label>
                    <input
                      type="text"
                      className="form-input"
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      placeholder="Index name"
                      title="Index name"
                    />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Description</label>
                    <textarea
                      className="form-input form-textarea"
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                      placeholder="Index description"
                      rows={3}
                      title="Index description"
                    />
                  </div>
                  <div className="form-group">
                    <label className="form-checkbox-label">
                      <input
                        type="checkbox"
                        checked={editEnabled}
                        onChange={(e) => setEditEnabled(e.target.checked)}
                        title="Enable or disable this index"
                      />
                      <span>Enabled</span>
                    </label>
                  </div>
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveDetails}
                      disabled={isSavingDetails}
                      title="Save changes"
                    >
                      {isSavingDetails ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDetails}
                      disabled={isSavingDetails}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
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
              )}
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

            {/* Cache Configuration and Statistics Section */}
            <div className="details-section">
              <div className="section-header">
                <h4>Cache Configuration</h4>
                {!editingCacheSettings && (
                  <div className="section-actions">
                    <button className="btn btn-sm btn-secondary" onClick={handleStartEditCacheSettings} title="Edit cache settings">
                      Edit
                    </button>
                    {indexDetails.cacheConfiguration?.enabled && (
                      <button
                        className="btn btn-sm btn-secondary"
                        onClick={handleClearCache}
                        disabled={isClearingCache}
                        title="Clear all cached data"
                      >
                        {isClearingCache ? 'Clearing...' : 'Clear Cache'}
                      </button>
                    )}
                  </div>
                )}
              </div>

              {editingCacheSettings ? (
                <div className="edit-section">
                  <div className="cache-edit-form">
                    <div className="form-group form-group-checkbox">
                      <label>
                        <input
                          type="checkbox"
                          name="enabled"
                          checked={editCacheConfig?.enabled || false}
                          onChange={handleCacheConfigChange}
                          title="Enable or disable caching"
                        />
                        Enable Caching
                      </label>
                    </div>

                    {editCacheConfig?.enabled && (
                      <>
                        <div className="cache-edit-subsection">
                          <h5>Term Cache</h5>
                          <div className="form-group form-group-checkbox">
                            <label>
                              <input
                                type="checkbox"
                                name="enableTermCache"
                                checked={editCacheConfig?.enableTermCache || false}
                                onChange={handleCacheConfigChange}
                                title="Enable or disable term cache"
                              />
                              Enable
                            </label>
                          </div>
                          {editCacheConfig?.enableTermCache && (
                            <div className="cache-edit-fields">
                              <div className="form-group">
                                <label>Capacity</label>
                                <input
                                  type="number"
                                  name="termCacheCapacity"
                                  value={editCacheConfig?.termCacheCapacity || 10000}
                                  onChange={handleCacheConfigChange}
                                  min={1}
                                  title="Term cache capacity"
                                />
                              </div>
                              <div className="form-group">
                                <label>TTL (sec)</label>
                                <input
                                  type="number"
                                  name="termCacheTtlSeconds"
                                  value={editCacheConfig?.termCacheTtlSeconds || 300}
                                  onChange={handleCacheConfigChange}
                                  min={1}
                                  title="Term cache TTL in seconds"
                                />
                              </div>
                            </div>
                          )}
                        </div>

                        <div className="cache-edit-subsection">
                          <h5>Document Cache</h5>
                          <div className="form-group form-group-checkbox">
                            <label>
                              <input
                                type="checkbox"
                                name="enableDocumentCache"
                                checked={editCacheConfig?.enableDocumentCache || false}
                                onChange={handleCacheConfigChange}
                                title="Enable or disable document cache"
                              />
                              Enable
                            </label>
                          </div>
                          {editCacheConfig?.enableDocumentCache && (
                            <div className="cache-edit-fields">
                              <div className="form-group">
                                <label>Capacity</label>
                                <input
                                  type="number"
                                  name="documentCacheCapacity"
                                  value={editCacheConfig?.documentCacheCapacity || 5000}
                                  onChange={handleCacheConfigChange}
                                  min={1}
                                  title="Document cache capacity"
                                />
                              </div>
                              <div className="form-group">
                                <label>TTL (sec)</label>
                                <input
                                  type="number"
                                  name="documentCacheTtlSeconds"
                                  value={editCacheConfig?.documentCacheTtlSeconds || 600}
                                  onChange={handleCacheConfigChange}
                                  min={1}
                                  title="Document cache TTL in seconds"
                                />
                              </div>
                            </div>
                          )}
                        </div>

                        <div className="cache-edit-subsection">
                          <h5>Statistics Cache</h5>
                          <div className="form-group form-group-checkbox">
                            <label>
                              <input
                                type="checkbox"
                                name="enableStatisticsCache"
                                checked={editCacheConfig?.enableStatisticsCache || false}
                                onChange={handleCacheConfigChange}
                                title="Enable or disable statistics cache"
                              />
                              Enable
                            </label>
                          </div>
                          {editCacheConfig?.enableStatisticsCache && (
                            <div className="cache-edit-fields">
                              <div className="form-group">
                                <label>TTL (sec)</label>
                                <input
                                  type="number"
                                  name="statisticsCacheTtlSeconds"
                                  value={editCacheConfig?.statisticsCacheTtlSeconds || 60}
                                  onChange={handleCacheConfigChange}
                                  min={1}
                                  title="Statistics cache TTL in seconds"
                                />
                              </div>
                            </div>
                          )}
                        </div>
                      </>
                    )}
                  </div>
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveCacheSettings}
                      disabled={isSavingCacheSettings}
                      title="Save cache settings"
                    >
                      {isSavingCacheSettings ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditCacheSettings}
                      disabled={isSavingCacheSettings}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <>
                  <div className="detail-item">
                    <span className="detail-label">Caching</span>
                    <span className={`status-badge ${indexDetails.cacheConfiguration?.enabled ? 'enabled' : 'disabled'}`}>
                      {indexDetails.cacheConfiguration?.enabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </div>

                  {indexDetails.cacheConfiguration?.enabled && (
                    <>
                      {/* Cache Statistics Display */}
                      {indexDetails.statistics?.cacheStatistics && (
                        <div className="cache-stats-grid">
                          {/* Term Cache Stats */}
                          {indexDetails.statistics.cacheStatistics.termCache && (
                            <div className="cache-stat-block">
                              <h5>Term Cache</h5>
                              <div className="cache-stat-items">
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hit Rate</span>
                                  <span className="stat-value highlight">{formatPercent(indexDetails.statistics.cacheStatistics.termCache.hitRate)}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hits / Misses</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.termCache.hitCount?.toLocaleString()} / {indexDetails.statistics.cacheStatistics.termCache.missCount?.toLocaleString()}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Entries</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.termCache.currentCount?.toLocaleString()} / {indexDetails.statistics.cacheStatistics.termCache.capacity?.toLocaleString()}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Evictions</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.termCache.evictionCount?.toLocaleString()}</span>
                                </div>
                              </div>
                            </div>
                          )}

                          {/* Document Cache Stats */}
                          {indexDetails.statistics.cacheStatistics.documentCache && (
                            <div className="cache-stat-block">
                              <h5>Document Cache</h5>
                              <div className="cache-stat-items">
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hit Rate</span>
                                  <span className="stat-value highlight">{formatPercent(indexDetails.statistics.cacheStatistics.documentCache.hitRate)}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hits / Misses</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.documentCache.hitCount?.toLocaleString()} / {indexDetails.statistics.cacheStatistics.documentCache.missCount?.toLocaleString()}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Entries</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.documentCache.currentCount?.toLocaleString()} / {indexDetails.statistics.cacheStatistics.documentCache.capacity?.toLocaleString()}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Evictions</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.documentCache.evictionCount?.toLocaleString()}</span>
                                </div>
                              </div>
                            </div>
                          )}

                          {/* Statistics Cache Stats */}
                          {indexDetails.statistics.cacheStatistics.statisticsCache && (
                            <div className="cache-stat-block">
                              <h5>Statistics Cache</h5>
                              <div className="cache-stat-items">
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hit Rate</span>
                                  <span className="stat-value highlight">{formatPercent(indexDetails.statistics.cacheStatistics.statisticsCache.hitRate)}</span>
                                </div>
                                <div className="cache-stat-item">
                                  <span className="stat-label">Hits / Misses</span>
                                  <span className="stat-value">{indexDetails.statistics.cacheStatistics.statisticsCache.hitCount?.toLocaleString()} / {indexDetails.statistics.cacheStatistics.statisticsCache.missCount?.toLocaleString()}</span>
                                </div>
                              </div>
                            </div>
                          )}
                        </div>
                      )}

                      {/* Cache Configuration Summary */}
                      <div className="cache-config-summary">
                        <div className="config-item">
                          <span>Term Cache:</span>
                          <span>{indexDetails.cacheConfiguration.enableTermCache ? `Enabled (${indexDetails.cacheConfiguration.termCacheCapacity?.toLocaleString()} entries, ${indexDetails.cacheConfiguration.termCacheTtlSeconds}s TTL)` : 'Disabled'}</span>
                        </div>
                        <div className="config-item">
                          <span>Document Cache:</span>
                          <span>{indexDetails.cacheConfiguration.enableDocumentCache ? `Enabled (${indexDetails.cacheConfiguration.documentCacheCapacity?.toLocaleString()} entries, ${indexDetails.cacheConfiguration.documentCacheTtlSeconds}s TTL)` : 'Disabled'}</span>
                        </div>
                        <div className="config-item">
                          <span>Statistics Cache:</span>
                          <span>{indexDetails.cacheConfiguration.enableStatisticsCache ? `Enabled (${indexDetails.cacheConfiguration.statisticsCacheTtlSeconds}s TTL)` : 'Disabled'}</span>
                        </div>
                      </div>
                    </>
                  )}
                </>
              )}
            </div>

            <div className="details-section">
              <div className="section-header">
                <h4>Labels</h4>
                {!editingLabels && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditLabels} title="Edit labels">
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
                      title="Save labels"
                    >
                      {isSavingLabels ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditLabels}
                      disabled={isSavingLabels}
                      title="Cancel editing"
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
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditTags} title="Edit tags">
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
                      title="Save tags"
                    >
                      {isSavingTags ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditTags}
                      disabled={isSavingTags}
                      title="Cancel editing"
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

            <div className="details-section">
              <div className="section-header">
                <h4>Custom Metadata</h4>
                {!editingCustomMetadata && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditCustomMetadata} title="Edit custom metadata">
                    Edit
                  </button>
                )}
              </div>
              {editingCustomMetadata ? (
                <div className="edit-section">
                  <JsonEditor
                    value={editCustomMetadata}
                    onChange={setEditCustomMetadata}
                    placeholder='{"key": "value"}'
                    label={null}
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveCustomMetadata}
                      disabled={isSavingCustomMetadata}
                      title="Save custom metadata"
                    >
                      {isSavingCustomMetadata ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditCustomMetadata}
                      disabled={isSavingCustomMetadata}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : indexDetails.customMetadata !== undefined && indexDetails.customMetadata !== null ? (
                <pre className="custom-metadata-display">{JSON.stringify(indexDetails.customMetadata, null, 2)}</pre>
              ) : (
                <p className="no-content-notice">No custom metadata assigned to this index.</p>
              )}
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDelete({ identifier: indexDetails.identifier, name: indexDetails.name })}
                title="Permanently delete this index"
              >
                Delete Index
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedIndex(null);
                  setIndexDetails(null);
                  setEditingDetails(false);
                }}
                title="Close this dialog"
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

      {/* Alert Modal */}
      <AlertModal
        isOpen={alertModal.isOpen}
        onClose={closeAlert}
        title={alertModal.title}
        message={alertModal.message}
        variant={alertModal.variant}
      />

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        isOpen={deleteConfirm.isOpen}
        onClose={() => setDeleteConfirm({ isOpen: false, index: null, isDeleting: false })}
        onConfirm={confirmDelete}
        title="Delete Index"
        message="Are you sure you want to delete this index? This will permanently delete all documents in the index."
        entityName={deleteConfirm.index?.name || deleteConfirm.index?.identifier}
        confirmLabel="Delete"
        warningMessage="This action cannot be undone."
        variant="danger"
        isLoading={deleteConfirm.isDeleting}
      />

      {/* Backup Preparing Modal */}
      <Modal
        isOpen={isBackingUp !== null}
        onClose={() => {}}
        title="Preparing Backup"
      >
        <div className="backup-preparing-content">
          <div className="backup-spinner"></div>
          <p>Your backup of "{backupIndexName}" is being prepared...</p>
        </div>
      </Modal>

      {/* Restore Backup Modal (New Index) */}
      <Modal
        isOpen={showRestoreModal}
        onClose={() => {
          setShowRestoreModal(false);
          setRestoreFile(null);
          setRestoreFileName('');
          setRestoreName('');
          setRestoreError(null);
        }}
        title="Restore Backup"
      >
        <div className="restore-modal-content">
          <p className="restore-description">
            Upload a backup file (.vbx) to restore as a new index.
          </p>
          {restoreError && (
            <div className="restore-error-message">
              {restoreError}
            </div>
          )}
          <div className="form-group">
            <label className="form-label">Backup File</label>
            <div className="file-input-wrapper">
              <input
                type="file"
                id="restore-file-input"
                className="file-input-hidden"
                accept=".vbx,.zip"
                title="Select a backup file to restore"
                onChange={(e) => {
                  const file = e.target.files[0];
                  setRestoreFile(file);
                  setRestoreFileName(file ? file.name : '');
                  setRestoreError(null);
                }}
              />
              <label htmlFor="restore-file-input" className="btn btn-secondary file-input-button">
                Choose File
              </label>
              <span className="file-input-filename">
                {restoreFileName || 'No file selected'}
              </span>
            </div>
          </div>
          <div className="form-group">
            <label className="form-label">Index Name (optional)</label>
            <input
              type="text"
              className="form-input"
              value={restoreName}
              onChange={(e) => setRestoreName(e.target.value)}
              placeholder="Leave empty to use original name"
              title="Name for the restored index (optional)"
            />
          </div>
          <div className="modal-actions">
            <button
              className="btn btn-primary"
              onClick={handleRestore}
              disabled={!restoreFile || isRestoring}
              title="Restore the backup as a new index"
            >
              {isRestoring ? 'Restoring...' : 'Restore'}
            </button>
            <button
              className="btn btn-secondary"
              onClick={() => {
                setShowRestoreModal(false);
                setRestoreFile(null);
                setRestoreFileName('');
                setRestoreName('');
                setRestoreError(null);
              }}
              disabled={isRestoring}
              title="Cancel"
            >
              Cancel
            </button>
          </div>
        </div>
      </Modal>

      {/* Restore Replace Modal */}
      <Modal
        isOpen={showRestoreReplaceModal}
        onClose={() => {
          setShowRestoreReplaceModal(false);
          setRestoreReplaceFile(null);
          setRestoreReplaceFileName('');
          setRestoreReplaceIndex(null);
          setRestoreReplaceError(null);
        }}
        title={`Restore to: ${restoreReplaceIndex?.name || restoreReplaceIndex?.identifier || ''}`}
      >
        <div className="restore-modal-content">
          <div className="warning-message">
            This will replace all data in the index with the backup contents. This action cannot be undone.
          </div>
          {restoreReplaceError && (
            <div className="restore-error-message">
              {restoreReplaceError}
            </div>
          )}
          <div className="form-group">
            <label className="form-label">Backup File</label>
            <div className="file-input-wrapper">
              <input
                type="file"
                id="restore-replace-file-input"
                className="file-input-hidden"
                accept=".vbx,.zip"
                title="Select a backup file to restore"
                onChange={(e) => {
                  const file = e.target.files[0];
                  setRestoreReplaceFile(file);
                  setRestoreReplaceFileName(file ? file.name : '');
                  setRestoreReplaceError(null);
                }}
              />
              <label htmlFor="restore-replace-file-input" className="btn btn-secondary file-input-button">
                Choose File
              </label>
              <span className="file-input-filename">
                {restoreReplaceFileName || 'No file selected'}
              </span>
            </div>
          </div>
          <div className="modal-actions">
            <button
              className="btn btn-danger"
              onClick={handleRestoreReplace}
              disabled={!restoreReplaceFile || isRestoringReplace}
              title="Replace index data with backup contents"
            >
              {isRestoringReplace ? 'Restoring...' : 'Replace Index'}
            </button>
            <button
              className="btn btn-secondary"
              onClick={() => {
                setShowRestoreReplaceModal(false);
                setRestoreReplaceFile(null);
                setRestoreReplaceFileName('');
                setRestoreReplaceIndex(null);
                setRestoreReplaceError(null);
              }}
              disabled={isRestoringReplace}
              title="Cancel"
            >
              Cancel
            </button>
          </div>
        </div>
      </Modal>

      {/* Top Terms Modal */}
      <Modal
        isOpen={showTopTermsModal}
        onClose={() => {
          setShowTopTermsModal(false);
          setTopTermsIndex(null);
          setTopTermsData(null);
          setTopTermsError(null);
        }}
        title={`Top Terms: ${topTermsIndex?.name || topTermsIndex?.identifier || ''}`}
      >
        <div className="top-terms-modal-content">
          {isLoadingTopTerms ? (
            <div className="loading-spinner">Loading top terms...</div>
          ) : topTermsError ? (
            <div className="error-message">{topTermsError}</div>
          ) : topTermsData && Object.keys(topTermsData).length > 0 ? (
            <div className="top-terms-list">
              <div className="top-terms-header">
                <span className="term-column">Term</span>
                <span className="count-column">Document Count</span>
              </div>
              {Object.entries(topTermsData)
                .sort((a, b) => b[1] - a[1])
                .map(([term, count], index) => (
                  <div key={term} className="top-terms-row">
                    <span className="term-rank">{index + 1}.</span>
                    <span className="term-value">{term}</span>
                    <span className="term-count">{count.toLocaleString()}</span>
                  </div>
                ))}
            </div>
          ) : (
            <p className="no-content-notice">No terms found in this index.</p>
          )}
          <div className="modal-actions">
            <button
              className="btn btn-secondary"
              onClick={() => {
                setShowTopTermsModal(false);
                setTopTermsIndex(null);
                setTopTermsData(null);
                setTopTermsError(null);
              }}
              title="Close this dialog"
            >
              Close
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

export default IndicesView;
