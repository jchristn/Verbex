import { useState, useEffect, useMemo } from 'react';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import MetadataModal from './MetadataModal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import SortableHeader from './SortableHeader';
import './SearchView.css';

function SearchView({ selectedIndex, indices, onIndexSelect }) {
  const { apiClient } = useAuth();
  const [query, setQuery] = useState('');
  const [maxResults, setMaxResults] = useState(25);
  const [results, setResults] = useState(null);
  const [isSearching, setIsSearching] = useState(false);
  const [error, setError] = useState('');
  const [searchTime, setSearchTime] = useState(null);

  // Search options
  const [searchMode, setSearchMode] = useState('any'); // 'any' (OR), 'all' (AND)

  // Min score (inline search option, not part of filter apply)
  const [minScore, setMinScore] = useState('');

  // Filter bar state (input fields)
  const [showFilters, setShowFilters] = useState(false);
  const [filterLabels, setFilterLabels] = useState('');
  const [filterTags, setFilterTags] = useState('');

  // Applied filter state (only updated on Apply)
  const [appliedLabels, setAppliedLabels] = useState(null);
  const [appliedTags, setAppliedTags] = useState(null);

  // Sorting state
  const [sortColumn, setSortColumn] = useState('rank');
  const [sortDirection, setSortDirection] = useState('asc');

  // Document detail/metadata modals
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [showMetadataModal, setShowMetadataModal] = useState(false);
  const [selectedResult, setSelectedResult] = useState(null);

  const handleIndexChange = (e) => {
    const newIndex = e.target.value;
    onIndexSelect(newIndex || null);
    // Clear results and filters when index changes
    setResults(null);
    setError('');
    setFilterLabels('');
    setFilterTags('');
    setMinScore('');
    setAppliedLabels(null);
    setAppliedTags(null);
    setShowFilters(false);
  };

  // Auto-select if only one index available
  useEffect(() => {
    if (indices.length === 1 && !selectedIndex) {
      onIndexSelect(indices[0].identifier);
    }
  }, [indices, selectedIndex, onIndexSelect]);

  // Filter parsing helpers
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

  const parseMinScore = (str) => {
    if (!str || !str.trim()) return 0;
    const val = parseFloat(str);
    return isNaN(val) ? 0 : val;
  };

  const handleApplyFilters = () => {
    setAppliedLabels(parseLabels(filterLabels));
    setAppliedTags(parseTags(filterTags));
  };

  const handleClearFilters = () => {
    setFilterLabels('');
    setFilterTags('');
    setAppliedLabels(null);
    setAppliedTags(null);
  };

  const hasAppliedFilters = appliedLabels !== null || appliedTags !== null;

  const filtersAreDirty = useMemo(() => {
    const pendingLabels = parseLabels(filterLabels);
    const pendingTags = parseTags(filterTags);
    return JSON.stringify(pendingLabels) !== JSON.stringify(appliedLabels) ||
           JSON.stringify(pendingTags) !== JSON.stringify(appliedTags);
  }, [filterLabels, filterTags, appliedLabels, appliedTags]);

  const handleSearch = async (e) => {
    e.preventDefault();

    if (!selectedIndex) {
      setError('Please select an index from the dropdown');
      return;
    }

    if (!query.trim()) {
      setError('Please enter a search query');
      return;
    }

    setError('');
    setIsSearching(true);
    setResults(null);

    try {
      let searchQuery = query.trim();

      const useAndLogic = searchMode === 'all';
      const response = await apiClient.search(selectedIndex, searchQuery, maxResults, appliedLabels, appliedTags, useAndLogic);

      // Filter results by minimum score if specified
      let filteredResults = response.data;
      const effectiveMinScore = parseMinScore(minScore);
      if (effectiveMinScore > 0 && filteredResults?.results) {
        filteredResults = {
          ...filteredResults,
          results: filteredResults.results.filter(r => (r.score || 0) >= effectiveMinScore),
          totalCount: filteredResults.results.filter(r => (r.score || 0) >= effectiveMinScore).length
        };
      }

      // Add rank and derive matchedTerms from termScores keys
      if (filteredResults?.results) {
        filteredResults.results = filteredResults.results.map((r, i) => ({
          ...r,
          rank: i + 1,
          matchedTerms: r.matchedTerms || (r.termScores ? Object.keys(r.termScores) : [])
        }));
      }

      setResults(filteredResults);
      setSearchTime(response.processingTimeMs);
      // Reset sort to rank ascending when new results come in
      setSortColumn('rank');
      setSortDirection('asc');
    } catch (err) {
      setError(err.message || 'Search failed');
    } finally {
      setIsSearching(false);
    }
  };

  const handleClear = () => {
    setQuery('');
    setResults(null);
    setError('');
    setSearchTime(null);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSearch(e);
    }
  };

  // Sorting handler
  const handleSort = (column, direction) => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  // Sort results
  const sortedResults = useMemo(() => {
    if (!results?.results) return [];

    const sorted = [...results.results];
    sorted.sort((a, b) => {
      let aVal, bVal;

      switch (sortColumn) {
        case 'rank':
          aVal = a.rank;
          bVal = b.rank;
          break;
        case 'score':
          aVal = a.score || 0;
          bVal = b.score || 0;
          break;
        case 'documentId':
          aVal = a.documentId || '';
          bVal = b.documentId || '';
          break;
        case 'matchedTerms':
          aVal = a.matchedTerms?.length || 0;
          bVal = b.matchedTerms?.length || 0;
          break;
        default:
          aVal = a.rank;
          bVal = b.rank;
      }

      if (typeof aVal === 'string') {
        const comparison = aVal.localeCompare(bVal);
        return sortDirection === 'asc' ? comparison : -comparison;
      } else {
        return sortDirection === 'asc' ? aVal - bVal : bVal - aVal;
      }
    });

    return sorted;
  }, [results, sortColumn, sortDirection]);

  const selectedIndexInfo = indices.find((i) => i.identifier === selectedIndex);

  const handleViewDetails = (result) => {
    setSelectedResult(result);
    setShowDetailModal(true);
  };

  const handleViewMetadata = (result) => {
    setSelectedResult(result);
    setShowMetadataModal(true);
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="search-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Search</h2>
          <p className="workspace-subtitle">Query your indices and explore search results</p>
        </div>
        <div className="workspace-actions">
          <div className="index-selector-inline">
            <label htmlFor="search-index-select">Index:</label>
            <select
              id="search-index-select"
              value={selectedIndex || ''}
              onChange={handleIndexChange}
              title="Select an index to search"
            >
              <option value="">Select an index...</option>
              {indices.map((index) => (
                <option key={index.identifier} value={index.identifier}>
                  {index.name || index.identifier}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Search Form */}
      <div className="workspace-card">
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
                  <label htmlFor="searchFilterLabels">Labels</label>
                  <input
                    type="text"
                    id="searchFilterLabels"
                    placeholder="important, reviewed"
                    value={filterLabels}
                    onChange={(e) => setFilterLabels(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleApplyFilters(); } }}
                    title="Filter results by labels (comma-separated)"
                  />
                  <span className="doc-filter-hint">Comma-separated, AND logic</span>
                </div>
                <div className="doc-filter-field">
                  <label htmlFor="searchFilterTags">Tags</label>
                  <input
                    type="text"
                    id="searchFilterTags"
                    placeholder="category=tech, status=published"
                    value={filterTags}
                    onChange={(e) => setFilterTags(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleApplyFilters(); } }}
                    title="Filter results by tags (key=value, comma-separated)"
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
        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-input-wrapper">
            <input
              type="text"
              className="search-input"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Enter search terms or * for all documents..."
              autoFocus
              title="Enter search terms or * for all documents"
            />
            {query && (
              <button
                type="button"
                className="search-clear"
                onClick={handleClear}
                title="Clear search"
              >
                ×
              </button>
            )}
          </div>

          <div className="search-hint">
            Use <strong>*</strong> as a catch-all to return all documents. Combine with label/tag filters to browse matching documents.
          </div>
          <div className="search-controls">
            <div className="search-options">
              <div className="search-option">
                <label htmlFor="searchMode">Match:</label>
                <select
                  id="searchMode"
                  value={searchMode}
                  onChange={(e) => setSearchMode(e.target.value)}
                  title="Choose how search terms are matched"
                >
                  <option value="any">Any term (OR)</option>
                  <option value="all">All terms (AND)</option>
                </select>
              </div>

              <div className="search-option">
                <label htmlFor="maxResults">Max Results:</label>
                <select
                  id="maxResults"
                  value={maxResults}
                  onChange={(e) => setMaxResults(parseInt(e.target.value, 10))}
                  title="Maximum number of results to return"
                >
                  <option value={10}>10</option>
                  <option value={25}>25</option>
                  <option value={50}>50</option>
                  <option value={100}>100</option>
                  <option value={250}>250</option>
                </select>
              </div>

              <div className="search-option">
                <label htmlFor="minScore">Min Score:</label>
                <input
                  type="number"
                  id="minScore"
                  value={minScore}
                  onChange={(e) => setMinScore(e.target.value)}
                  min="0"
                  max="1"
                  step="0.1"
                  className="score-input"
                  placeholder="0"
                  title="Minimum relevance score (0-1, 0 shows all results)"
                />
              </div>
            </div>

            <div className="search-actions">
              <button
                type="submit"
                className="btn btn-primary"
                disabled={isSearching || !query.trim() || !selectedIndex}
                title="Execute the search"
              >
                {isSearching ? 'Searching...' : 'Search'}
              </button>
            </div>
          </div>
        </form>
      </div>

      {error && (
        <div className="search-error">
          {error}
        </div>
      )}

      {/* Search Results */}
      {results && (
        <div className="workspace-card search-results-card">
          <div className="workspace-card-header">
            <h3>
              Results
              <span className="results-count">
                {results.totalCount} found
                {searchTime !== null && (
                  <span className="results-time"> in {searchTime.toFixed(2)}ms</span>
                )}
              </span>
            </h3>
          </div>
          <div className="workspace-card-body">
            {results.results?.length === 0 ? (
              <div className="no-results">
                <p>No documents match your search query.</p>
                <p className="no-results-hint">
                  Try different keywords, change the match mode, or lower the minimum score.
                </p>
              </div>
            ) : (
              <table className="data-table search-results-table">
                <thead>
                  <tr>
                    <SortableHeader
                      label="#"
                      sortKey="rank"
                      currentSort={sortColumn}
                      currentDirection={sortDirection}
                      onSort={handleSort}
                    />
                    <SortableHeader
                      label="Score"
                      sortKey="score"
                      currentSort={sortColumn}
                      currentDirection={sortDirection}
                      onSort={handleSort}
                    />
                    <SortableHeader
                      label="Document ID"
                      sortKey="documentId"
                      currentSort={sortColumn}
                      currentDirection={sortDirection}
                      onSort={handleSort}
                    />
                    <SortableHeader
                      label="Matched Terms"
                      sortKey="matchedTerms"
                      currentSort={sortColumn}
                      currentDirection={sortDirection}
                      onSort={handleSort}
                    />
                    <th className="actions-column">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {sortedResults.map((result) => (
                    <tr key={result.documentId || result.rank} className="clickable-row" onClick={() => handleViewDetails(result)}>
                      <td className="rank-column">{result.rank}</td>
                      <td className="score-column">
                        <div className="score-cell">
                          <div className="score-bar-container">
                            <div
                              className="score-bar"
                              style={{ width: `${(result.score || 0) * 100}%` }}
                            />
                          </div>
                          <span className="score-text">
                            {((result.score || 0) * 100).toFixed(1)}%
                          </span>
                        </div>
                      </td>
                      <td><CopyableId value={result.documentId} /></td>
                      <td className="terms-column">
                        {result.matchedTerms && result.matchedTerms.length > 0 ? (
                          <div className="matched-terms-cell">
                            {result.matchedTerms.map((term, i) => (
                              <span key={i} className="match-term">{term}</span>
                            ))}
                          </div>
                        ) : (
                          <span className="no-terms">-</span>
                        )}
                      </td>
                      <td className="actions-column" onClick={(e) => e.stopPropagation()}>
                        <ActionMenu
                          actions={[
                            {
                              label: 'View Details',
                              onClick: () => handleViewDetails(result)
                            },
                            {
                              label: 'View JSON',
                              onClick: () => handleViewMetadata(result)
                            }
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Document Detail Modal */}
      <Modal
        isOpen={showDetailModal}
        onClose={() => {
          setShowDetailModal(false);
          setSelectedResult(null);
        }}
        title="Search Result Details"
        size="fullscreen"
      >
        {selectedResult && (
          <div className="search-result-details">
            {/* Search Score Section */}
            <div className="details-section">
              <h4>Search Score</h4>
              <div className="score-display">
                <div className="score-visual">
                  <div
                    className="score-fill"
                    style={{ width: `${(selectedResult.score || 0) * 100}%` }}
                  />
                </div>
                <span className="score-value">
                  {((selectedResult.score || 0) * 100).toFixed(2)}%
                </span>
              </div>
              <div className="score-stats">
                <span className="stat-item">
                  <span className="stat-label">Matched Terms:</span>
                  <span className="stat-value">{selectedResult.matchedTermCount || selectedResult.matchedTerms?.length || 0}</span>
                </span>
                <span className="stat-item">
                  <span className="stat-label">Total Matches:</span>
                  <span className="stat-value">{selectedResult.totalTermMatches || 0}</span>
                </span>
              </div>
            </div>

            {/* Matched Terms Section */}
            {selectedResult.matchedTerms && selectedResult.matchedTerms.length > 0 && (
              <div className="details-section">
                <h4>Matched Terms</h4>
                <div className="matched-terms-detail">
                  {selectedResult.matchedTerms.map((term, i) => (
                    <span key={i} className="match-term-detail">
                      {term}
                      {selectedResult.termFrequencies && selectedResult.termFrequencies[term] && (
                        <span className="term-freq">x{selectedResult.termFrequencies[term]}</span>
                      )}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Indexed Terms Section */}
            {selectedResult.document?.terms && selectedResult.document.terms.length > 0 && (
              <div className="details-section">
                <h4>Indexed Terms ({selectedResult.document.terms.length})</h4>
                <div className="document-terms">
                  {selectedResult.document.terms.map((term, i) => (
                    <span key={i} className="term-badge">{term}</span>
                  ))}
                </div>
              </div>
            )}

            {/* Document Metadata Section */}
            <div className="details-section">
              <h4>Document Metadata</h4>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">Document ID</span>
                  <span className="detail-value">
                    <CopyableId value={selectedResult.documentId} />
                  </span>
                </div>
                {selectedResult.document && (
                  <>
                    <div className="detail-item">
                      <span className="detail-label">Document Path</span>
                      <span className="detail-value">
                        {selectedResult.document.documentPath ? <CopyableId value={selectedResult.document.documentPath} /> : 'N/A'}
                      </span>
                    </div>
                    <div className="detail-item">
                      <span className="detail-label">Length</span>
                      <span className="detail-value">
                        {selectedResult.document.documentLength?.toLocaleString() || 'N/A'} chars
                      </span>
                    </div>
                    <div className="detail-item">
                      <span className="detail-label">Indexed</span>
                      <span className="detail-value">{formatDate(selectedResult.document.indexedDate)}</span>
                    </div>
                    <div className="detail-item">
                      <span className="detail-label">Last Modified</span>
                      <span className="detail-value">{formatDate(selectedResult.document.lastModified)}</span>
                    </div>
                    {selectedResult.document.contentSha256 && (
                      <div className="detail-item">
                        <span className="detail-label">Content Hash</span>
                        <span className="detail-value">
                          <CopyableId value={selectedResult.document.contentSha256} />
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>

            {/* Labels Section */}
            {selectedResult.document?.labels && selectedResult.document.labels.length > 0 && (
              <div className="details-section">
                <h4>Labels</h4>
                <div className="document-labels">
                  {selectedResult.document.labels.map((label, i) => (
                    <span key={i} className="label-badge">{label}</span>
                  ))}
                </div>
              </div>
            )}

            {/* Tags Section */}
            {selectedResult.document?.tags && Object.keys(selectedResult.document.tags).length > 0 && (
              <div className="details-section">
                <h4>Tags</h4>
                <div className="document-tags">
                  {Object.entries(selectedResult.document.tags).map(([key, value], i) => (
                    <div key={i} className="tag-item">
                      <span className="tag-key">{key}</span>
                      <span className="tag-separator">=</span>
                      <span className="tag-value"><CopyableId value={String(value)} /></span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Custom Metadata Section */}
            {selectedResult.document?.customMetadata !== undefined && selectedResult.document?.customMetadata !== null && (
              <div className="details-section">
                <h4>Custom Metadata</h4>
                <pre className="custom-metadata-display">
                  {JSON.stringify(selectedResult.document.customMetadata, null, 2)}
                </pre>
              </div>
            )}

            {/* Document Content Section */}
            {(selectedResult.document?.content || selectedResult.document?.Content) && (
              <div className="details-section">
                <h4>Content</h4>
                <div className="document-content">
                  {selectedResult.document.content || selectedResult.document.Content}
                </div>
              </div>
            )}

            <div className="details-actions">
              <button
                className="btn btn-secondary"
                onClick={() => {
                  setShowDetailModal(false);
                  setSelectedResult(null);
                }}
                title="Close this dialog"
              >
                Close
              </button>
            </div>
          </div>
        )}
      </Modal>

      {/* Metadata JSON Modal */}
      <MetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false);
          setSelectedResult(null);
        }}
        title="Search Result JSON"
        data={selectedResult}
      />
    </div>
  );
}

export default SearchView;
