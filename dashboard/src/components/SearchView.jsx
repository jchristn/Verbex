import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
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
      onIndexSelect(indices[0].id);
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

      setResults(filteredResults);
      setSearchTime(response.processingTimeMs);
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

  const selectedIndexInfo = indices.find((i) => i.id === selectedIndex);

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
                <option key={index.id} value={index.id}>
                  {index.name || index.id}
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
              <div className="results-list">
                {results.results?.map((result, index) => (
                  <div key={result.documentId || index} className="result-item">
                    <div className="result-header">
                      <span className="result-rank">#{index + 1}</span>
                      <div className="result-score-container">
                        <div
                          className="result-score-bar"
                          style={{ width: `${(result.score || 0) * 100}%` }}
                        />
                        <span className="result-score">
                          {((result.score || 0) * 100).toFixed(1)}%
                        </span>
                      </div>
                    </div>
                    <div className="result-doc-id">
                      <code>{result.documentId}</code>
                    </div>
                    {result.matchedTerms && result.matchedTerms.length > 0 && (
                      <div className="result-matches">
                        <span className="matches-label">Matched:</span>
                        {result.matchedTerms.map((term, i) => (
                          <span key={i} className="match-term">{term}</span>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default SearchView;
