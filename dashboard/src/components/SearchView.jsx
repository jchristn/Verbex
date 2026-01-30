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

  // Advanced search options
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [searchMode, setSearchMode] = useState('any'); // 'any' (OR), 'all' (AND)
  const [minScore, setMinScore] = useState(0);
  const [filterLabels, setFilterLabels] = useState('');
  const [filterTags, setFilterTags] = useState('');

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
    // Clear results when index changes
    setResults(null);
    setError('');
  };

  // Auto-select if only one index available
  useEffect(() => {
    if (indices.length === 1 && !selectedIndex) {
      onIndexSelect(indices[0].identifier);
    }
  }, [indices, selectedIndex, onIndexSelect]);

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
      // Build search query based on mode
      let searchQuery = query.trim();

      // Parse labels filter (comma-separated)
      const labels = filterLabels.trim()
        ? filterLabels.split(',').map(l => l.trim()).filter(l => l)
        : null;

      // Parse tags filter (key=value pairs, comma-separated)
      let tags = null;
      if (filterTags.trim()) {
        tags = {};
        filterTags.split(',').forEach(pair => {
          const [key, value] = pair.split('=').map(s => s.trim());
          if (key && value !== undefined) {
            tags[key] = value;
          }
        });
        if (Object.keys(tags).length === 0) tags = null;
      }

      const useAndLogic = searchMode === 'all';
      const response = await apiClient.search(selectedIndex, searchQuery, maxResults, labels, tags, useAndLogic);

      // Filter results by minimum score if specified
      let filteredResults = response.data;
      if (minScore > 0 && filteredResults?.results) {
        filteredResults = {
          ...filteredResults,
          results: filteredResults.results.filter(r => (r.score || 0) >= minScore),
          totalCount: filteredResults.results.filter(r => (r.score || 0) >= minScore).length
        };
      }

      // Add rank to each result
      if (filteredResults?.results) {
        filteredResults.results = filteredResults.results.map((r, i) => ({
          ...r,
          rank: i + 1
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
        </div>
        <div className="workspace-actions">
          <div className="index-selector-inline">
            <label htmlFor="search-index-select">Index:</label>
            <select
              id="search-index-select"
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
        </div>
      </div>

      {/* Search Form */}
      <div className="workspace-card search-form-card">
        <form className="search-form" onSubmit={handleSearch}>
          <div className="search-input-wrapper">
            <input
              type="text"
              className="search-input"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Enter search terms..."
              autoFocus
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

          <div className="search-controls">
            <div className="search-options">
              <div className="search-option">
                <label htmlFor="searchMode">Match:</label>
                <select
                  id="searchMode"
                  value={searchMode}
                  onChange={(e) => setSearchMode(e.target.value)}
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
                >
                  <option value={10}>10</option>
                  <option value={25}>25</option>
                  <option value={50}>50</option>
                  <option value={100}>100</option>
                  <option value={250}>250</option>
                </select>
              </div>

              <button
                type="button"
                className="advanced-toggle-btn"
                onClick={() => setShowAdvanced(!showAdvanced)}
              >
                {showAdvanced ? '▼ Less options' : '▶ More options'}
              </button>
            </div>

            {showAdvanced && (
              <div className="advanced-options">
                <div className="search-option">
                  <label htmlFor="minScore">Min Score:</label>
                  <input
                    type="number"
                    id="minScore"
                    value={minScore}
                    onChange={(e) => setMinScore(parseFloat(e.target.value) || 0)}
                    min="0"
                    max="1"
                    step="0.1"
                    className="score-input"
                  />
                  <span className="option-hint">0-1 (0 = all results)</span>
                </div>
                <div className="search-option filter-option">
                  <label htmlFor="filterLabels">Filter by Labels:</label>
                  <input
                    type="text"
                    id="filterLabels"
                    value={filterLabels}
                    onChange={(e) => setFilterLabels(e.target.value)}
                    placeholder="important, reviewed"
                    className="filter-input"
                  />
                  <span className="option-hint">Comma-separated (AND logic)</span>
                </div>
                <div className="search-option filter-option">
                  <label htmlFor="filterTags">Filter by Tags:</label>
                  <input
                    type="text"
                    id="filterTags"
                    value={filterTags}
                    onChange={(e) => setFilterTags(e.target.value)}
                    placeholder="category=tech, status=published"
                    className="filter-input"
                  />
                  <span className="option-hint">key=value pairs, comma-separated (AND logic)</span>
                </div>
              </div>
            )}

            <div className="search-actions">
              <button
                type="submit"
                className="btn btn-primary"
                disabled={isSearching || !query.trim() || !selectedIndex}
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
                    <tr key={result.documentId || result.rank}>
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
                      <td className="actions-column">
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
        size="large"
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
                      <span className="detail-value">{selectedResult.document.documentPath || 'N/A'}</span>
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
                        <span className="detail-value hash-value">{selectedResult.document.contentSha256}</span>
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
                      <span className="tag-value">{value}</span>
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
