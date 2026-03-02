import './SortableHeader.css';

function SortableHeader({
  label,
  sortKey,
  currentSort,
  currentDirection,
  onSort,
  filterable = false,
  filterValue = '',
  onFilterChange,
  filterPlaceholder = 'Filter...',
  hasFilters = false // Pass true if ANY column has filters, to maintain alignment
}) {
  const isActive = currentSort === sortKey;

  const handleSort = () => {
    if (isActive) {
      // Toggle direction
      onSort(sortKey, currentDirection === 'asc' ? 'desc' : 'asc');
    } else {
      // Default to ascending
      onSort(sortKey, 'asc');
    }
  };

  return (
    <th className={`sortable-header ${hasFilters ? 'has-filters' : ''}`}>
      <button className="sortable-header-btn" onClick={handleSort} title={`Sort by ${label}`}>
        <span className="sortable-header-label">{label}</span>
        <span className={`sortable-header-arrows ${isActive ? 'active' : ''}`}>
          <svg
            className={`arrow-up ${isActive && currentDirection === 'asc' ? 'active' : ''}`}
            width="8"
            height="8"
            viewBox="0 0 8 8"
            fill="currentColor"
          >
            <path d="M4 0L7 4H1L4 0Z" />
          </svg>
          <svg
            className={`arrow-down ${isActive && currentDirection === 'desc' ? 'active' : ''}`}
            width="8"
            height="8"
            viewBox="0 0 8 8"
            fill="currentColor"
          >
            <path d="M4 8L1 4H7L4 8Z" />
          </svg>
        </span>
      </button>
      {hasFilters && (
        <div className="sortable-header-filter-container">
          {filterable ? (
            <input
              type="text"
              className="sortable-header-filter"
              value={filterValue}
              onChange={(e) => onFilterChange(sortKey, e.target.value)}
              placeholder={filterPlaceholder}
              title={filterPlaceholder || `Filter by ${label}`}
              onClick={(e) => e.stopPropagation()}
            />
          ) : (
            <div className="sortable-header-filter-spacer" />
          )}
        </div>
      )}
    </th>
  );
}

export default SortableHeader;
