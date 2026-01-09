import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import ActionMenu from './ActionMenu';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import Pagination from './Pagination';
import SortableHeader from './SortableHeader';
import './DocumentsView.css';

function DocumentsView({ selectedIndex, indices, onRefresh, onIndexSelect }) {
  const { apiClient } = useAuth();
  const [documents, setDocuments] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  // Add document modal
  const [showAddModal, setShowAddModal] = useState(false);
  const [newDocId, setNewDocId] = useState('');
  const [newDocContent, setNewDocContent] = useState('');
  const [newDocLabels, setNewDocLabels] = useState([]);
  const [newDocTags, setNewDocTags] = useState({});
  const [isAdding, setIsAdding] = useState(false);
  const [docIdError, setDocIdError] = useState('');

  // View document modal
  const [showViewModal, setShowViewModal] = useState(false);
  const [viewDocument, setViewDocument] = useState(null);
  const [isLoadingDoc, setIsLoadingDoc] = useState(false);

  // Edit mode states for document labels/tags
  const [editingDocLabels, setEditingDocLabels] = useState(false);
  const [editingDocTags, setEditingDocTags] = useState(false);
  const [editDocLabels, setEditDocLabels] = useState([]);
  const [editDocTags, setEditDocTags] = useState({});
  const [isSavingDocLabels, setIsSavingDocLabels] = useState(false);
  const [isSavingDocTags, setIsSavingDocTags] = useState(false);

  // Metadata modal
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [metadataDoc, setMetadataDoc] = useState(null);

  // Sorting state
  const [sortColumn, setSortColumn] = useState('indexedDate');
  const [sortDirection, setSortDirection] = useState('desc');

  // Filtering state
  const [filterDocId, setFilterDocId] = useState('');

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const selectedIndexInfo = indices.find((i) => i.identifier === selectedIndex);

  const handleIndexChange = (e) => {
    const newIndex = e.target.value;
    onIndexSelect(newIndex || null);
  };

  // Sorting handler
  const handleSort = (column, direction) => {
    setSortColumn(column);
    setSortDirection(direction);
    setCurrentPage(1);
  };

  // Filter handler
  const handleFilterChange = (column, value) => {
    if (column === 'documentId') {
      setFilterDocId(value);
    }
  };

  // Filter and sort documents
  const filteredAndSortedDocuments = useMemo(() => {
    let result = [...documents];

    // Apply filter
    if (filterDocId) {
      const lowerFilter = filterDocId.toLowerCase();
      result = result.filter((doc) =>
        doc.documentId?.toLowerCase().includes(lowerFilter)
      );
    }

    // Apply sorting
    result.sort((a, b) => {
      let aVal, bVal;

      switch (sortColumn) {
        case 'documentId':
          aVal = a.documentId || '';
          bVal = b.documentId || '';
          break;
        case 'documentLength':
          aVal = a.documentLength || 0;
          bVal = b.documentLength || 0;
          break;
        case 'indexedDate':
          aVal = new Date(a.indexedDate || 0).getTime();
          bVal = new Date(b.indexedDate || 0).getTime();
          break;
        default:
          aVal = '';
          bVal = '';
      }

      if (typeof aVal === 'string') {
        const comparison = aVal.localeCompare(bVal);
        return sortDirection === 'asc' ? comparison : -comparison;
      } else {
        return sortDirection === 'asc' ? aVal - bVal : bVal - aVal;
      }
    });

    return result;
  }, [documents, filterDocId, sortColumn, sortDirection]);

  // Pagination calculations
  const totalItems = filteredAndSortedDocuments.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const paginatedDocuments = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize;
    return filteredAndSortedDocuments.slice(startIndex, startIndex + pageSize);
  }, [filteredAndSortedDocuments, currentPage, pageSize]);

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

  const loadDocuments = useCallback(async (signal) => {
    if (!selectedIndex || !apiClient) return;

    setIsLoading(true);
    setError('');

    try {
      const response = await apiClient.getDocuments(selectedIndex, { signal });
      setDocuments(response.data?.documents || []);
    } catch (err) {
      if (err.name === 'AbortError') return;
      setError(err.message || 'Failed to load documents');
    } finally {
      setIsLoading(false);
    }
  }, [apiClient, selectedIndex]);

  // Load documents when index changes
  useEffect(() => {
    if (selectedIndex) {
      const abortController = new AbortController();
      loadDocuments(abortController.signal);
      return () => abortController.abort();
    } else {
      setDocuments([]);
    }
  }, [loadDocuments]);

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

      await apiClient.addDocument(selectedIndex, document);
      setShowAddModal(false);
      setNewDocId('');
      setNewDocContent('');
      setNewDocLabels([]);
      setNewDocTags({});
      setDocIdError('');
      loadDocuments();
    } catch (err) {
      alert(`Failed to add document: ${err.message}`);
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
      alert(`Failed to load document: ${err.message}`);
      setShowViewModal(false);
    } finally {
      setIsLoadingDoc(false);
    }
  };

  const handleDeleteDocument = async (docId) => {
    if (!confirm(`Are you sure you want to delete this document? This action cannot be undone.`)) {
      return;
    }

    try {
      await apiClient.deleteDocument(selectedIndex, docId);
      setShowViewModal(false);
      setViewDocument(null);
      loadDocuments();
    } catch (err) {
      alert(`Failed to delete document: ${err.message}`);
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
      alert(`Failed to update labels: ${err.message}`);
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
      alert(`Failed to update tags: ${err.message}`);
    } finally {
      setIsSavingDocTags(false);
    }
  };

  return (
    <div className="documents-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Documents</h2>
        </div>
        <div className="workspace-actions">
          <div className="index-selector-inline">
            <label htmlFor="index-select">Index:</label>
            <select
              id="index-select"
              value={selectedIndex || ''}
              onChange={handleIndexChange}
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
            <>
              <button className="btn btn-secondary" onClick={loadDocuments}>
                Refresh
              </button>
              <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
                Add Document
              </button>
            </>
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
            <button className="btn btn-secondary" onClick={loadDocuments}>
              Retry
            </button>
          </div>
        </div>
      ) : documents.length === 0 ? (
        <div className="workspace-card">
          <div className="empty-state">
            <div className="empty-state-icon">📄</div>
            <h3 className="empty-state-title">No Documents</h3>
            <p className="empty-state-description">
              This index has no documents yet. Add your first document to start indexing.
            </p>
            <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
              Add Document
            </button>
          </div>
        </div>
      ) : (
        <div className="workspace-card">
          <table className="data-table documents-table">
            <thead>
              <tr>
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
                <tr key={doc.documentId}>
                  <td><CopyableId value={doc.documentId} /></td>
                  <td>{doc.documentLength?.toLocaleString() || 'N/A'}</td>
                  <td>{formatDate(doc.indexedDate)}</td>
                  <td>
                    <ActionMenu
                      actions={[
                        {
                          label: 'View',
                          onClick: () => handleViewDocument(doc.documentId)
                        },
                        {
                          label: 'View Metadata',
                          onClick: () => {
                            setMetadataDoc(doc);
                            setShowMetadataModal(true);
                          }
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
          />
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
                setDocIdError('');
              }}
              disabled={isAdding}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isAdding || !newDocContent.trim() || !!docIdError}
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
        size="large"
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
                  <span className="detail-value">{viewDocument.documentPath || 'N/A'}</span>
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
                  <span className="detail-label">Content Hash (SHA256)</span>
                  <span className="detail-value">{viewDocument.contentSha256 || 'N/A'}</span>
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
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDocLabels}>
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
                    >
                      {isSavingDocLabels ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDocLabels}
                      disabled={isSavingDocLabels}
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
                  <button className="btn btn-sm btn-secondary" onClick={handleStartEditDocTags}>
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
                    >
                      {isSavingDocTags ? 'Saving...' : 'Save'}
                    </button>
                    <button
                      className="btn btn-sm btn-secondary"
                      onClick={handleCancelEditDocTags}
                      disabled={isSavingDocTags}
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
                      <span className="tag-value">{value}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="no-content-notice">No tags assigned to this document.</p>
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
              >
                Delete Document
              </button>
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowViewModal(false);
                  setViewDocument(null);
                }}
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
        title="Document Metadata"
        data={metadataDoc}
      />
    </div>
  );
}

export default DocumentsView;
