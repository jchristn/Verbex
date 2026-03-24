import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
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
import './DocumentsView.css';

function DocumentsView({ selectedIndex, indices, onRefresh, onIndexSelect }) {
  const { apiClient } = useAuth();
  const [documents, setDocuments] = useState([]);
  const [totalDocumentCount, setTotalDocumentCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  // Add document modal
  const [showAddModal, setShowAddModal] = useState(false);
  const [newDocId, setNewDocId] = useState('');
  const [newDocContent, setNewDocContent] = useState('');
  const [newDocLabels, setNewDocLabels] = useState([]);
  const [newDocTags, setNewDocTags] = useState({});
  const [newDocCustomMetadata, setNewDocCustomMetadata] = useState(null);
  const [isAdding, setIsAdding] = useState(false);
  const [docIdError, setDocIdError] = useState('');

  // View document modal
  const [showViewModal, setShowViewModal] = useState(false);
  const [viewDocument, setViewDocument] = useState(null);
  const [isLoadingDoc, setIsLoadingDoc] = useState(false);

  // Edit mode states for document labels/tags/customMetadata
  const [editingDocLabels, setEditingDocLabels] = useState(false);
  const [editingDocTags, setEditingDocTags] = useState(false);
  const [editingDocCustomMetadata, setEditingDocCustomMetadata] = useState(false);
  const [editDocLabels, setEditDocLabels] = useState([]);
  const [editDocTags, setEditDocTags] = useState({});
  const [editDocCustomMetadata, setEditDocCustomMetadata] = useState(null);
  const [isSavingDocLabels, setIsSavingDocLabels] = useState(false);
  const [isSavingDocTags, setIsSavingDocTags] = useState(false);
  const [isSavingDocCustomMetadata, setIsSavingDocCustomMetadata] = useState(false);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataDoc, setMetadataDoc] = useState(null);
  const [isLoadingMetadata, setIsLoadingMetadata] = useState(false);

  // Alert modal state
  const [alertModal, setAlertModal] = useState({ isOpen: false, title: '', message: '', variant: 'error' });

  // Delete confirmation modal state
  const [deleteConfirm, setDeleteConfirm] = useState({ isOpen: false, docId: null, isDeleting: false });

  // Bulk selection state
  const [selectedDocIds, setSelectedDocIds] = useState(new Set());
  const [bulkDeleteConfirm, setBulkDeleteConfirm] = useState({ isOpen: false, isDeleting: false });

  // Sorting state
  const [sortColumn, setSortColumn] = useState('indexedDate');
  const [sortDirection, setSortDirection] = useState('desc');

  // Filtering state
  const [filterDocId, setFilterDocId] = useState('');
  const [filterLabels, setFilterLabels] = useState('');
  const [filterTags, setFilterTags] = useState('');
  const [showFilters, setShowFilters] = useState(false);

  // Applied filters — only updated when user clicks Apply
  const [appliedLabels, setAppliedLabels] = useState(null);
  const [appliedTags, setAppliedTags] = useState(null);

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const showAlert = (title, message, variant = 'error') => {
    setAlertModal({ isOpen: true, title, message, variant });
  };

  const closeAlert = () => {
    setAlertModal({ isOpen: false, title: '', message: '', variant: 'error' });
  };

  const selectedIndexInfo = indices.find((i) => i.identifier === selectedIndex);

  const handleIndexChange = (e) => {
    const newIndex = e.target.value;
    onIndexSelect(newIndex || null);
  };

  // Sorting handler - only indexedDate is supported server-side
  const handleSort = (column, direction) => {
    setSortColumn(column);
    setSortDirection(direction);
    setCurrentPage(1);
    // Server-side sorting is triggered by the useEffect dependency on sortDirection
  };

  // Filter handler
  const handleFilterChange = (column, value) => {
    if (column === 'documentId') {
      setFilterDocId(value);
    }
  };

  // Selection handlers
  const handleToggleSelect = (docId) => {
    setSelectedDocIds(prev => {
      const newSet = new Set(prev);
      if (newSet.has(docId)) {
        newSet.delete(docId);
      } else {
        newSet.add(docId);
      }
      return newSet;
    });
  };

  const handleSelectAll = () => {
    const currentPageIds = paginatedDocuments.map(doc => doc.documentId);
    const allSelected = currentPageIds.every(id => selectedDocIds.has(id));

    if (allSelected) {
      // Deselect all on current page
      setSelectedDocIds(prev => {
        const newSet = new Set(prev);
        currentPageIds.forEach(id => newSet.delete(id));
        return newSet;
      });
    } else {
      // Select all on current page
      setSelectedDocIds(prev => {
        const newSet = new Set(prev);
        currentPageIds.forEach(id => newSet.add(id));
        return newSet;
      });
    }
  };

  const handleClearSelection = () => {
    setSelectedDocIds(new Set());
  };

  const handleBulkDelete = () => {
    setBulkDeleteConfirm({ isOpen: true, isDeleting: false });
  };

  const confirmBulkDelete = async () => {
    setBulkDeleteConfirm(prev => ({ ...prev, isDeleting: true }));

    try {
      const idsToDelete = Array.from(selectedDocIds);
      const response = await apiClient.deleteDocumentsBatch(selectedIndex, idsToDelete);
      const deletedCount = response.data?.deletedCount || 0;
      const notFoundCount = response.data?.notFoundCount || 0;

      setBulkDeleteConfirm({ isOpen: false, isDeleting: false });
      setSelectedDocIds(new Set());
      loadDocuments();

      if (notFoundCount > 0) {
        showAlert('Bulk Delete', `Deleted ${deletedCount} document(s). ${notFoundCount} document(s) were not found.`, 'warning');
      }
    } catch (err) {
      setBulkDeleteConfirm({ isOpen: false, isDeleting: false });
      showAlert('Error', `Failed to delete documents: ${err.message}`);
    }
  };

  // Filter documents (client-side filter on current page only)
  const filteredDocuments = useMemo(() => {
    if (!filterDocId) return documents;

    const lowerFilter = filterDocId.toLowerCase();
    return documents.filter((doc) =>
      doc.documentId?.toLowerCase().includes(lowerFilter)
    );
  }, [documents, filterDocId]);

  // Pagination calculations - server handles pagination, we display what we get
  const isFiltering = filterDocId.trim() !== '';
  const totalItems = isFiltering ? filteredDocuments.length : totalDocumentCount;
  const totalPages = Math.ceil(totalDocumentCount / pageSize);

  // When filtering client-side, show filtered results; otherwise show server results directly
  const paginatedDocuments = isFiltering ? filteredDocuments : documents;

  // Reset page when filter changes
  useEffect(() => {
    setCurrentPage(1);
  }, [filterDocId]);

  // Auto-select if only one index available
  useEffect(() => {
    if (indices.length === 1 && !selectedIndex) {
      onIndexSelect(indices[0].identifier);
    }
  }, [indices, selectedIndex, onIndexSelect]);

  // Parse raw filter strings into arrays/objects
  const parseLabels = (str) => {
    if (!str || !str.trim()) return null;
    const parsed = str.split(',').map(l => l.trim()).filter(l => l.length > 0);
    return parsed.length > 0 ? parsed : null;
  };

  const parseTags = (str) => {
    if (!str || !str.trim()) return null;
    const tags = {};
    str.split(',').forEach(pair => {
      const eqIdx = pair.indexOf('=');
      if (eqIdx > 0) {
        const key = pair.substring(0, eqIdx).trim();
        const value = pair.substring(eqIdx + 1).trim();
        if (key) tags[key] = value;
      }
    });
    return Object.keys(tags).length > 0 ? tags : null;
  };

  const handleApplyFilters = () => {
    setAppliedLabels(parseLabels(filterLabels));
    setAppliedTags(parseTags(filterTags));
    setCurrentPage(1);
  };

  const handleClearFilters = () => {
    setFilterLabels('');
    setFilterTags('');
    setAppliedLabels(null);
    setAppliedTags(null);
    setCurrentPage(1);
  };

  // Check if the input fields differ from what's currently applied
  const filtersAreDirty = useMemo(() => {
    const pendingLabels = parseLabels(filterLabels);
    const pendingTags = parseTags(filterTags);
    return JSON.stringify(pendingLabels) !== JSON.stringify(appliedLabels) ||
           JSON.stringify(pendingTags) !== JSON.stringify(appliedTags);
  }, [filterLabels, filterTags, appliedLabels, appliedTags]);

  const hasAppliedFilters = appliedLabels !== null || appliedTags !== null;

  const loadDocuments = useCallback(async (signalOrEvent, page = currentPage, size = pageSize) => {
    if (!selectedIndex || !apiClient) return;

    // Handle both AbortSignal (from useEffect) and no signal (from button click)
    const signal = signalOrEvent instanceof AbortSignal ? signalOrEvent : undefined;

    setIsLoading(true);
    setError('');

    try {
      // Use server-side pagination with skip
      const skip = (page - 1) * size;
      const ordering = sortDirection === 'asc' ? 'CreatedAscending' : 'CreatedDescending';

      const response = await apiClient.getDocuments(selectedIndex, {
        maxResults: size,
        skip,
        ordering,
        labels: appliedLabels,
        tags: appliedTags,
        ...(signal ? { signal } : {})
      });

      const data = response.data || response;
      setDocuments(data.objects || []);
      setTotalDocumentCount(data.totalRecords || 0);
    } catch (err) {
      if (err.name === 'AbortError') return;
      setError(err.message || 'Failed to load documents');
    } finally {
      setIsLoading(false);
    }
  }, [apiClient, selectedIndex, currentPage, pageSize, sortDirection, appliedLabels, appliedTags]);

  // Clear filters on index change
  useEffect(() => {
    setFilterLabels('');
    setFilterTags('');
    setAppliedLabels(null);
    setAppliedTags(null);
    setShowFilters(false);
  }, [selectedIndex]);

  // Load documents when index, page, pageSize, sort, or filters change
  useEffect(() => {
    if (selectedIndex) {
      const abortController = new AbortController();
      setSelectedDocIds(new Set()); // Clear selection when data changes
      loadDocuments(abortController.signal, currentPage, pageSize);
      return () => abortController.abort();
    } else {
      setDocuments([]);
      setSelectedDocIds(new Set());
      setTotalDocumentCount(0);
    }
  }, [selectedIndex, currentPage, pageSize, sortDirection, appliedLabels, appliedTags, apiClient]);

  const handleAddDocument = async (e) => {
    e.preventDefault();

    if (!newDocContent.trim()) {
      return;
    }

    setIsAdding(true);

    try {
      const document = {
        content: newDocContent.trim()
      };

      if (newDocId.trim()) {
        document.id = newDocId.trim();
      }

      if (newDocLabels.length > 0) {
        document.labels = newDocLabels;
      }

      if (Object.keys(newDocTags).length > 0) {
        document.tags = newDocTags;
      }

      if (newDocCustomMetadata !== null) {
        document.customMetadata = newDocCustomMetadata;
      }

      await apiClient.addDocument(selectedIndex, document);
      setShowAddModal(false);
      setNewDocId('');
      setNewDocContent('');
      setNewDocLabels([]);
      setNewDocTags({});
      setNewDocCustomMetadata(null);
      setDocIdError('');
      loadDocuments();
    } catch (err) {
      showAlert('Error', `Failed to add document: ${err.message}`);
    } finally {
      setIsAdding(false);
    }
  };

  const handleViewDocument = async (docId) => {
    setShowViewModal(true);
    setIsLoadingDoc(true);
    setViewDocument(null);

    try {
      const response = await apiClient.getDocument(selectedIndex, docId);
      // Handle potential nested document structure
      const docData = response.data?.document || response.data;
      setViewDocument(docData);
    } catch (err) {
      showAlert('Error', `Failed to load document: ${err.message}`);
      setShowViewModal(false);
    } finally {
      setIsLoadingDoc(false);
    }
  };

  const handleViewMetadata = async (docId) => {
    setShowMetadataModal(true);
    setIsLoadingMetadata(true);
    setMetadataDoc(null);

    try {
      const response = await apiClient.getDocument(selectedIndex, docId);
      const docData = response.data?.document || response.data;
      setMetadataDoc(docData);
    } catch (err) {
      showAlert('Error', `Failed to load document metadata: ${err.message}`);
      setShowMetadataModal(false);
    } finally {
      setIsLoadingMetadata(false);
    }
  };

  const handleDeleteDocument = (docId) => {
    setDeleteConfirm({ isOpen: true, docId, isDeleting: false });
  };

  const confirmDeleteDocument = async () => {
    const docId = deleteConfirm.docId;
    setDeleteConfirm(prev => ({ ...prev, isDeleting: true }));

    try {
      await apiClient.deleteDocument(selectedIndex, docId);
      setDeleteConfirm({ isOpen: false, docId: null, isDeleting: false });
      setShowViewModal(false);
      setViewDocument(null);
      loadDocuments();
    } catch (err) {
      setDeleteConfirm({ isOpen: false, docId: null, isDeleting: false });
      showAlert('Error', `Failed to delete document: ${err.message}`);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  const handleDocIdChange = (value) => {
    setNewDocId(value);
    setDocIdError('');
  };

  // Document labels edit handlers
  const handleStartEditDocLabels = () => {
    setEditDocLabels(viewDocument.labels || []);
    setEditingDocLabels(true);
  };

  const handleCancelEditDocLabels = () => {
    setEditingDocLabels(false);
    setEditDocLabels([]);
  };

  const handleSaveDocLabels = async () => {
    setIsSavingDocLabels(true);
    try {
      await apiClient.updateDocumentLabels(selectedIndex, viewDocument.documentId, editDocLabels);
      // Refresh document details
      const response = await apiClient.getDocument(selectedIndex, viewDocument.documentId);
      const docData = response.data?.document || response.data;
      setViewDocument(docData);
      setEditingDocLabels(false);
      loadDocuments();
    } catch (err) {
      showAlert('Error', `Failed to update labels: ${err.message}`);
    } finally {
      setIsSavingDocLabels(false);
    }
  };

  // Document tags edit handlers
  const handleStartEditDocTags = () => {
    // Convert tags to string values for the editor
    const stringTags = {};
    if (viewDocument.tags) {
      Object.entries(viewDocument.tags).forEach(([key, value]) => {
        stringTags[key] = String(value);
      });
    }
    setEditDocTags(stringTags);
    setEditingDocTags(true);
  };

  const handleCancelEditDocTags = () => {
    setEditingDocTags(false);
    setEditDocTags({});
  };

  const handleSaveDocTags = async () => {
    setIsSavingDocTags(true);
    try {
      await apiClient.updateDocumentTags(selectedIndex, viewDocument.documentId, editDocTags);
      // Refresh document details
      const response = await apiClient.getDocument(selectedIndex, viewDocument.documentId);
      const docData = response.data?.document || response.data;
      setViewDocument(docData);
      setEditingDocTags(false);
      loadDocuments();
    } catch (err) {
      showAlert('Error', `Failed to update tags: ${err.message}`);
    } finally {
      setIsSavingDocTags(false);
    }
  };

  // Document custom metadata edit handlers
  const handleStartEditDocCustomMetadata = () => {
    setEditDocCustomMetadata(viewDocument.customMetadata !== undefined ? viewDocument.customMetadata : null);
    setEditingDocCustomMetadata(true);
  };

  const handleCancelEditDocCustomMetadata = () => {
    setEditingDocCustomMetadata(false);
    setEditDocCustomMetadata(null);
  };

  const handleSaveDocCustomMetadata = async () => {
    setIsSavingDocCustomMetadata(true);
    try {
      await apiClient.updateDocumentCustomMetadata(selectedIndex, viewDocument.documentId, editDocCustomMetadata);
      // Refresh document details
      const response = await apiClient.getDocument(selectedIndex, viewDocument.documentId);
      const docData = response.data?.document || response.data;
      setViewDocument(docData);
      setEditingDocCustomMetadata(false);
      loadDocuments();
    } catch (err) {
      showAlert('Error', `Failed to update custom metadata: ${err.message}`);
    } finally {
      setIsSavingDocCustomMetadata(false);
    }
  };

  return (
    <div className="documents-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Documents</h2>
          <p className="workspace-subtitle">Add, view, and manage documents within an index</p>
        </div>
        <div className="workspace-actions">
          <div className="index-selector-inline">
            <label htmlFor="index-select">Index:</label>
            <select
              id="index-select"
              value={selectedIndex || ''}
              onChange={handleIndexChange}
              title="Select an index to manage documents"
            >
              <option value="">Select an index...</option>
              {indices.map((index) => (
                <option key={index.identifier} value={index.identifier}>
                  {index.name || index.identifier}
                </option>
              ))}
            </select>
          </div>
          {selectedIndex && (
            <button className="btn btn-primary" onClick={() => setShowAddModal(true)} title="Add a new document to this index">
              Add Document
            </button>
          )}
        </div>
      </div>

      {!selectedIndex ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">📄</div>
            <h3 className="empty-state-title">Select an Index</h3>
            <p className="empty-state-description">
              Choose an index from the dropdown above to manage its documents.
            </p>
          </div>
        </div>
      ) : isLoading ? (
        <div className="workspace-card">
          <div className="loading-spinner">Loading documents...</div>
        </div>
      ) : error ? (
        <div className="workspace-card">
          <div className="error-state">
            <p>{error}</p>
            <button className="btn btn-secondary" onClick={loadDocuments} title="Retry loading documents">
              Retry
            </button>
          </div>
        </div>
      ) : documents.length === 0 && !hasAppliedFilters ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">📄</div>
            <h3 className="empty-state-title">No Documents</h3>
            <p className="empty-state-description">
              This index has no documents yet. Add your first document to start indexing.
            </p>
            <button className="btn btn-primary" onClick={() => setShowAddModal(true)} title="Add a new document to this index">
              Add Document
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          {selectedDocIds.size > 0 && (
            <div className="bulk-action-bar">
              <span className="bulk-selection-count">{selectedDocIds.size} document(s) selected</span>
              <div className="bulk-actions">
                <button className="btn btn-secondary btn-sm" onClick={handleClearSelection} title="Deselect all documents">
                  Clear Selection
                </button>
                <button className="btn btn-danger btn-sm" onClick={handleBulkDelete} title="Delete all selected documents">
                  Delete Selected
                </button>
              </div>
            </div>
          )}
          <div className="doc-filter-bar">
            <button
              className="doc-filter-toggle"
              onClick={() => setShowFilters(!showFilters)}
              title="Show or hide label and tag filters"
            >
              {showFilters ? '\u25BC Hide Filters' : '\u25B6 Filter by Labels/Tags'}
            </button>
            {hasAppliedFilters && !showFilters && (
              <span className="doc-filter-active-badge">Filters active</span>
            )}
            {showFilters && (
              <div className="doc-filter-panel">
                <div className="doc-filter-row">
                  <div className="doc-filter-field">
                    <label htmlFor="docFilterLabels">Labels</label>
                    <input
                      type="text"
                      id="docFilterLabels"
                      placeholder="important, reviewed"
                      value={filterLabels}
                      onChange={(e) => setFilterLabels(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') handleApplyFilters(); }}
                      title="Filter documents by labels (comma-separated)"
                    />
                    <span className="doc-filter-hint">Comma-separated, AND logic</span>
                  </div>
                  <div className="doc-filter-field">
                    <label htmlFor="docFilterTags">Tags</label>
                    <input
                      type="text"
                      id="docFilterTags"
                      placeholder="category=tech, status=published"
                      value={filterTags}
                      onChange={(e) => setFilterTags(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') handleApplyFilters(); }}
                      title="Filter documents by tags (key=value, comma-separated)"
                    />
                    <span className="doc-filter-hint">key=value pairs, comma-separated, AND logic</span>
                  </div>
                </div>
                <div className="doc-filter-actions">
                  <button
                    className="btn btn-sm btn-primary"
                    onClick={handleApplyFilters}
                    disabled={!filtersAreDirty}
                    title="Apply the current label and tag filters"
                  >
                    Apply Filters
                  </button>
                  {hasAppliedFilters && (
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleClearFilters}
                      title="Remove all active filters"
                    >
                      Clear Filters
                    </button>
                  )}
                </div>
              </div>
            )}
          </div>
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={pageSize}
            totalItems={totalItems}
            onPageChange={setCurrentPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setCurrentPage(1);
            }}
            onRefresh={loadDocuments}
          />
          <table className="data-table documents-table">
            <thead>
              <tr>
                <th className="checkbox-column">
                  <input
                    type="checkbox"
                    checked={paginatedDocuments.length > 0 && paginatedDocuments.every(doc => selectedDocIds.has(doc.documentId))}
                    onChange={handleSelectAll}
                    title="Select all on this page"
                  />
                </th>
                <SortableHeader
                  label="Document ID"
                  sortKey="documentId"
                  currentSort={sortColumn}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters={true}
                  filterable={true}
                  filterValue={filterDocId}
                  onFilterChange={handleFilterChange}
                  filterPlaceholder="Filter by ID..."
                />
                <SortableHeader
                  label="Length"
                  sortKey="documentLength"
                  currentSort={sortColumn}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters={true}
                />
                <SortableHeader
                  label="Indexed"
                  sortKey="indexedDate"
                  currentSort={sortColumn}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters={true}
                />
                <SortableHeader
                  label="Indexing Time"
                  sortKey="indexingRuntimeMs"
                  currentSort={sortColumn}
                  currentDirection={sortDirection}
                  onSort={handleSort}
                  hasFilters={true}
                />
                <th className="sortable-header has-filters actions-column">
                  <button className="sortable-header-btn" disabled>
                    <span className="sortable-header-label">Actions</span>
                  </button>
                  <div className="sortable-header-filter-container">
                    <div className="sortable-header-filter-spacer" />
                  </div>
                </th>
              </tr>
            </thead>
            <tbody>
              {paginatedDocuments.map((doc) => (
                <tr key={doc.documentId} className={`clickable-row ${selectedDocIds.has(doc.documentId) ? 'selected' : ''}`} onClick={() => handleViewDocument(doc.documentId)}>
                  <td className="checkbox-column" onClick={(e) => e.stopPropagation()}>
                    <input
                      type="checkbox"
                      checked={selectedDocIds.has(doc.documentId)}
                      onChange={() => handleToggleSelect(doc.documentId)}
                      title="Select this document"
                    />
                  </td>
                  <td><CopyableId value={doc.documentId} /></td>
                  <td>{doc.documentLength?.toLocaleString() || 'N/A'}</td>
                  <td>{formatDate(doc.indexedDate)}</td>
                  <td>{doc.indexingRuntimeMs != null ? `${doc.indexingRuntimeMs.toFixed(2)} ms` : 'N/A'}</td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <ActionMenu
                      actions={[
                        {
                          label: 'View',
                          onClick: () => handleViewDocument(doc.documentId)
                        },
                        {
                          label: 'View JSON',
                          onClick: () => handleViewMetadata(doc.documentId)
                        },
                        {
                          label: 'Delete',
                          variant: 'danger',
                          onClick: () => handleDeleteDocument(doc.documentId)
                        }
                      ]}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {documents.length === 0 && hasAppliedFilters && (
            <div className="empty-filter-state">
              <p>No documents match the current filters.</p>
              <button className="btn btn-secondary btn-sm" onClick={handleClearFilters} title="Remove all active filters">
                Clear Filters
              </button>
            </div>
          )}
        </div>
      )}

      {/* Add Document Modal */}
      <Modal
        isOpen={showAddModal}
        onClose={() => {
          setShowAddModal(false);
          setNewDocId('');
          setNewDocContent('');
          setNewDocLabels([]);
          setNewDocTags({});
          setNewDocCustomMetadata(null);
          setDocIdError('');
        }}
        title="Add Document"
        size="large"
      >
        <form className="add-document-form" onSubmit={handleAddDocument}>
          <div className="form-group">
            <label htmlFor="docId">Document ID (optional)</label>
            <div className="input-with-action">
              <input
                type="text"
                id="docId"
                value={newDocId}
                onChange={(e) => handleDocIdChange(e.target.value)}
                placeholder="Leave empty to auto-generate"
                className={docIdError ? 'input-error' : ''}
                title="Custom document ID (leave empty to auto-generate)"
              />
            </div>
            {docIdError && <span className="form-error">{docIdError}</span>}
            <span className="form-hint">
              Leave empty to auto-generate a unique ID.
            </span>
          </div>

          <div className="form-group">
            <label htmlFor="docContent">Content *</label>
            <textarea
              id="docContent"
              value={newDocContent}
              onChange={(e) => setNewDocContent(e.target.value)}
              placeholder="Enter the document content to be indexed..."
              rows={10}
              required
              title="Document text content to index"
            />
          </div>

          <div className="form-group">
            <label>Labels</label>
            <TagInput
              value={newDocLabels}
              onChange={setNewDocLabels}
              placeholder="Add labels..."
            />
          </div>

          <div className="form-group">
            <label>Tags</label>
            <KeyValueEditor
              value={newDocTags}
              onChange={setNewDocTags}
              keyPlaceholder="Tag name"
              valuePlaceholder="Tag value"
            />
          </div>

          <div className="form-group">
            <JsonEditor
              value={newDocCustomMetadata}
              onChange={setNewDocCustomMetadata}
              placeholder='{"key": "value"}'
              label="Custom Metadata"
            />
          </div>

          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                setShowAddModal(false);
                setNewDocId('');
                setNewDocContent('');
                setNewDocLabels([]);
                setNewDocTags({});
                setNewDocCustomMetadata(null);
                setDocIdError('');
              }}
              disabled={isAdding}
              title="Cancel adding document"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isAdding || !newDocContent.trim() || !!docIdError}
              title="Add the document to the index"
            >
              {isAdding ? 'Adding...' : 'Add Document'}
            </button>
          </div>
        </form>
      </Modal>

      {/* View Document Modal */}
      <Modal
        isOpen={showViewModal}
        onClose={() => {
          setShowViewModal(false);
          setViewDocument(null);
        }}
        title="Document Details"
        size="fullscreen"
      >
        {isLoadingDoc ? (
          <div className="loading-spinner">Loading document...</div>
        ) : viewDocument ? (
          <div className="document-details">
            <div className="details-section">
              <h4>Metadata</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">Index ID</span>
                  <span className="detail-value">
                    <CopyableId value={selectedIndex} />
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Document ID</span>
                  <span className="detail-value">
                    <CopyableId value={viewDocument.documentId} />
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Document Path</span>
                  <span className="detail-value">
                    {viewDocument.documentPath ? <CopyableId value={viewDocument.documentPath} /> : 'N/A'}
                  </span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Length</span>
                  <span className="detail-value">{viewDocument.documentLength?.toLocaleString() || 'N/A'} chars</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Indexed</span>
                  <span className="detail-value">{formatDate(viewDocument.indexedDate)}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Last Modified</span>
                  <span className="detail-value">{formatDate(viewDocument.lastModified)}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Indexing Time</span>
                  <span className="detail-value">{viewDocument.indexingRuntimeMs != null ? `${viewDocument.indexingRuntimeMs.toFixed(2)} ms` : 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Content Hash (SHA256)</span>
                  <span className="detail-value">
                    {viewDocument.contentSha256 ? <CopyableId value={viewDocument.contentSha256} /> : 'N/A'}
                  </span>
                </div>
              </div>
            </div>

            {(viewDocument.content || viewDocument.Content) && (
              <div className="details-section">
                <h4>Content</h4>
                <div className="document-content">
                  {viewDocument.content || viewDocument.Content}
                </div>
              </div>
            )}

            <div className="details-section">
              <div className="section-header">
                <h4>Labels</h4>
                {!editingDocLabels && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDocLabels} title="Edit labels">
                    Edit
                  </button>
                )}
              </div>
              {editingDocLabels ? (
                <div className="edit-section">
                  <TagInput
                    value={editDocLabels}
                    onChange={setEditDocLabels}
                    placeholder="Add labels..."
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveDocLabels}
                      disabled={isSavingDocLabels}
                      title="Save changes"
                    >
                      {isSavingDocLabels ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDocLabels}
                      disabled={isSavingDocLabels}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : viewDocument.labels && viewDocument.labels.length > 0 ? (
                <div className="document-labels">
                  {viewDocument.labels.map((label, i) => (
                    <span key={i} className="label-badge">{label}</span>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No labels assigned to this document.</p>
              )}
            </div>

            <div className="details-section">
              <div className="section-header">
                <h4>Tags</h4>
                {!editingDocTags && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDocTags} title="Edit tags">
                    Edit
                  </button>
                )}
              </div>
              {editingDocTags ? (
                <div className="edit-section">
                  <KeyValueEditor
                    value={editDocTags}
                    onChange={setEditDocTags}
                    keyPlaceholder="Tag name"
                    valuePlaceholder="Tag value"
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveDocTags}
                      disabled={isSavingDocTags}
                      title="Save changes"
                    >
                      {isSavingDocTags ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDocTags}
                      disabled={isSavingDocTags}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : viewDocument.tags && Object.keys(viewDocument.tags).length > 0 ? (
                <div className="document-tags">
                  {Object.entries(viewDocument.tags).map(([key, value], i) => (
                    <div key={i} className="tag-item">
                      <span className="tag-key">{key}</span>
                      <span className="tag-separator">=</span>
                      <span className="tag-value"><CopyableId value={String(value)} /></span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No tags assigned to this document.</p>
              )}
            </div>

            <div className="details-section">
              <div className="section-header">
                <h4>Custom Metadata</h4>
                {!editingDocCustomMetadata && (
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDocCustomMetadata} title="Edit custom metadata">
                    Edit
                  </button>
                )}
              </div>
              {editingDocCustomMetadata ? (
                <div className="edit-section">
                  <JsonEditor
                    value={editDocCustomMetadata}
                    onChange={setEditDocCustomMetadata}
                    placeholder='{"key": "value"}'
                    label={null}
                  />
                  <div className="edit-actions">
                    <button
                      className="btn btn-sm btn-primary"
                      onClick={handleSaveDocCustomMetadata}
                      disabled={isSavingDocCustomMetadata}
                      title="Save changes"
                    >
                      {isSavingDocCustomMetadata ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDocCustomMetadata}
                      disabled={isSavingDocCustomMetadata}
                      title="Cancel editing"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : viewDocument.customMetadata !== undefined && viewDocument.customMetadata !== null ? (
                <pre className="custom-metadata-display">{JSON.stringify(viewDocument.customMetadata, null, 2)}</pre>
              ) : (
                <p className="no-content-notice">No custom metadata assigned to this document.</p>
              )}
            </div>

            <div className="details-section">
              <h4>Indexed Terms ({viewDocument.terms?.length || 0})</h4>
              {viewDocument.terms && viewDocument.terms.length > 0 ? (
                <div className="document-terms">
                  {viewDocument.terms.map((term, i) => (
                    <span key={i} className="term-badge">{term}</span>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No terms indexed for this document.</p>
              )}
            </div>

            <div className="details-actions">
              <button
                className="btn btn-danger"
                onClick={() => handleDeleteDocument(viewDocument.documentId)}
                title="Permanently delete this document"
              >
                Delete Document
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowViewModal(false);
                  setViewDocument(null);
                }}
                title="Close this dialog"
              >
                Close
              </button>
            </div>
          </div>
        ) : null}
      </Modal>

      {/* Metadata Modal */}
      <MetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false);
          setMetadataDoc(null);
        }}
        title="Document JSON"
        data={metadataDoc}
        isLoading={isLoadingMetadata}
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
        onClose={() => setDeleteConfirm({ isOpen: false, docId: null, isDeleting: false })}
        onConfirm={confirmDeleteDocument}
        title="Delete Document"
        message="Are you sure you want to delete this document?"
        entityName={deleteConfirm.docId}
        confirmLabel="Delete"
        warningMessage="This action cannot be undone."
        variant="danger"
        isLoading={deleteConfirm.isDeleting}
      />

      {/* Bulk Delete Confirmation Modal */}
      <ConfirmModal
        isOpen={bulkDeleteConfirm.isOpen}
        onClose={() => setBulkDeleteConfirm({ isOpen: false, isDeleting: false })}
        onConfirm={confirmBulkDelete}
        title="Delete Multiple Documents"
        message={`Are you sure you want to delete ${selectedDocIds.size} document(s)?`}
        confirmLabel="Delete All"
        warningMessage="This action cannot be undone."
        variant="danger"
        isLoading={bulkDeleteConfirm.isDeleting}
      />
    </div>
  );
}

export default DocumentsView;
