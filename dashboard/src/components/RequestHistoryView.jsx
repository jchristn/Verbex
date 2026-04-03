import { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import ConfirmModal from './ConfirmModal';
import AlertModal from './AlertModal';
import Pagination from './Pagination';
import CopyableId from './CopyableId';
import './ApiExplorerView.css';
import './RequestHistoryView.css';

const ACTIVITY_RANGE_OPTIONS = [
  { id: 'lastHour', label: 'Last Hour', bucketMinutes: 1, sliceCount: 60 },
  { id: 'lastDay', label: 'Last Day', bucketMinutes: 15, sliceCount: 96 },
  { id: 'lastWeek', label: 'Last Week', bucketMinutes: 120, sliceCount: 84 },
  { id: 'lastMonth', label: 'Last Month', bucketMinutes: 720, sliceCount: 60 }
];
const MAX_X_AXIS_LABELS = 8;

function toLocalInputValue(date) {
  const offsetMs = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function buildApiDate(value) {
  return value ? new Date(value).toISOString() : '';
}

function formatBytes(bytes) {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index += 1;
  }
  return `${value.toFixed(value >= 10 || index === 0 ? 0 : 1)} ${units[index]}`;
}

function formatDate(value) {
  return value ? new Date(value).toLocaleString() : 'n/a';
}

function getActivityRangeConfig(rangeId) {
  return ACTIVITY_RANGE_OPTIONS.find((option) => option.id === rangeId) || ACTIVITY_RANGE_OPTIONS[1];
}

function getActivityRangeWindow(rangeId, now = new Date()) {
  const config = getActivityRangeConfig(rangeId);
  const bucketMs = config.bucketMinutes * 60 * 1000;
  const endExclusiveMs = Math.floor(now.getTime() / bucketMs) * bucketMs + bucketMs;
  const startMs = endExclusiveMs - config.sliceCount * bucketMs;

  return {
    ...config,
    bucketMs,
    startMs,
    endExclusiveMs,
    startUtc: new Date(startMs),
    endUtc: new Date(endExclusiveMs - 1)
  };
}

function buildActivityRangeParams(rangeId, now = new Date()) {
  const range = getActivityRangeWindow(rangeId, now);
  return {
    bucketMinutes: range.bucketMinutes,
    fromUtc: range.startUtc.toISOString(),
    toUtc: range.endUtc.toISOString()
  };
}

function floorToBucketTimestamp(value, bucketMs) {
  return Math.floor(new Date(value).getTime() / bucketMs) * bucketMs;
}

function normalizeSummaryBuckets(summary, range) {
  const apiBuckets = new Map(
    (summary?.buckets || []).map((bucket) => [
      floorToBucketTimestamp(bucket.bucketStartUtc, range.bucketMs),
      bucket
    ])
  );

  return Array.from({ length: range.sliceCount }, (_, index) => {
    const bucketStartMs = range.startMs + (index * range.bucketMs);
    const apiBucket = apiBuckets.get(bucketStartMs);

    return {
      bucketStartUtc: new Date(bucketStartMs).toISOString(),
      bucketEndUtc: new Date(bucketStartMs + range.bucketMs).toISOString(),
      totalCount: apiBucket?.totalCount || 0,
      successCount: apiBucket?.successCount || 0,
      failureCount: apiBucket?.failureCount || 0,
      averageDurationMs: apiBucket?.averageDurationMs || 0
    };
  });
}

function formatChartLabel(value, rangeId) {
  const date = new Date(value);

  if (rangeId === 'lastHour') {
    return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }

  if (rangeId === 'lastDay') {
    return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  }

  if (rangeId === 'lastWeek') {
    return date.toLocaleString([], { weekday: 'short', hour: 'numeric' });
  }

  if (rangeId === 'lastMonth') {
    return date.toLocaleString([], { month: 'short', day: 'numeric' });
  }

  return date.toLocaleString();
}

function buildChartLabels(buckets, rangeId) {
  if (!buckets?.length) return [];

  const labelCount = Math.min(MAX_X_AXIS_LABELS, buckets.length);
  const labelIndices = new Set();

  if (labelCount === 1) {
    labelIndices.add(0);
  } else {
    for (let index = 0; index < labelCount; index += 1) {
      labelIndices.add(Math.round((index * (buckets.length - 1)) / (labelCount - 1)));
    }
  }

  return Array.from(labelIndices)
    .sort((left, right) => left - right)
    .map((index, position, values) => ({
      bucketStartUtc: buckets[index].bucketStartUtc,
      label: formatChartLabel(buckets[index].bucketStartUtc, rangeId),
      left: values.length === 1 ? 0 : (position / (values.length - 1)) * 100,
      align: position === 0 ? 'start' : position === values.length - 1 ? 'end' : 'center'
    }));
}

function getTooltipPosition(clientX, clientY) {
  const tooltipWidth = 280;
  const tooltipHeight = 140;
  const offset = 14;
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;

  let left = clientX + offset;
  let top = clientY + offset;

  if (left + tooltipWidth > viewportWidth - 12) {
    left = clientX - tooltipWidth - offset;
  }

  if (top + tooltipHeight > viewportHeight - 12) {
    top = clientY - tooltipHeight - offset;
  }

  return {
    left: Math.max(12, left),
    top: Math.max(12, top)
  };
}

function HistoryChart({ summary, rangeId }) {
  const [tooltipState, setTooltipState] = useState(null);
  const range = getActivityRangeWindow(rangeId);
  const buckets = normalizeSummaryBuckets(summary, range);

  if (!summary) {
    return <div className="empty-state compact-empty-state"><p className="empty-state-description">No request history for the selected range.</p></div>;
  }

  const maxCount = Math.max(...buckets.map((bucket) => bucket.totalCount), 1);
  const axisTicks = [maxCount, Math.round(maxCount / 2), 0].filter((value, index, values) => values.indexOf(value) === index);
  const tooltipPosition = tooltipState ? getTooltipPosition(tooltipState.clientX, tooltipState.clientY) : null;
  const xAxisLabels = buildChartLabels(buckets, rangeId);
  const isDenseChart = buckets.length > 72;

  return (
    <>
      <div
        className="history-chart-shell"
        style={{
          '--history-chart-column-gap': isDenseChart ? '1px' : '2px',
          '--history-chart-bar-max-width': isDenseChart ? '6px' : '8px'
        }}
      >
        <div className="history-chart-axis-label">Requests</div>
        <div className="history-chart-main">
          <div className="history-chart-axis-values">
            {axisTicks.map((tick) => (
              <span key={tick} className="history-chart-axis-value">{tick}</span>
            ))}
          </div>
          <div className="history-chart-plot">
            {buckets.map((bucket) => {
              const totalHeight = `${Math.max((bucket.totalCount / maxCount) * 100, bucket.totalCount > 0 ? 6 : 2)}%`;
              const successHeight = bucket.totalCount > 0 ? `${(bucket.successCount / bucket.totalCount) * 100}%` : '0%';
              const failureHeight = bucket.totalCount > 0 ? `${(bucket.failureCount / bucket.totalCount) * 100}%` : '0%';
              const bucketId = bucket.bucketStartUtc;
              const isHovered = tooltipState?.bucketId === bucketId;

              return (
                <div
                  key={bucket.bucketStartUtc}
                  className={`history-chart-column ${isHovered ? 'is-hovered' : ''}`}
                  onMouseEnter={(event) => {
                    setTooltipState({
                      bucketId,
                      bucket,
                      clientX: event.clientX,
                      clientY: event.clientY
                    });
                  }}
                  onMouseMove={(event) => {
                    setTooltipState((current) => {
                      if (!current || current.bucketId !== bucketId) {
                        return {
                          bucketId,
                          bucket,
                          clientX: event.clientX,
                          clientY: event.clientY
                        };
                      }

                      return {
                        ...current,
                        bucket,
                        clientX: event.clientX,
                        clientY: event.clientY
                      };
                    });
                  }}
                  onMouseLeave={() => setTooltipState(null)}
                >
                  <div className="history-chart-track">
                    <div className="history-chart-bar" style={{ height: totalHeight }}>
                      <span className="history-chart-bar-success" style={{ height: successHeight }} />
                      <span className="history-chart-bar-failure" style={{ height: failureHeight }} />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
          <div className="history-chart-spacer" />
          <div className="history-chart-label-row">
            {xAxisLabels.map((label) => (
              <span
                key={label.bucketStartUtc}
                className={`history-chart-label history-chart-label-${label.align}`}
                style={{ left: `${label.left}%` }}
              >
                {label.label}
              </span>
            ))}
          </div>
        </div>
      </div>
      {tooltipState && tooltipPosition && createPortal(
        <div
          className="history-chart-tooltip"
          role="status"
          aria-live="polite"
          style={{ left: `${tooltipPosition.left}px`, top: `${tooltipPosition.top}px` }}
        >
          <strong>{formatDate(tooltipState.bucket.bucketStartUtc)} to {formatDate(tooltipState.bucket.bucketEndUtc)}</strong>
          <span>{tooltipState.bucket.totalCount} total</span>
          <span>{tooltipState.bucket.successCount} success</span>
          <span>{tooltipState.bucket.failureCount} failed</span>
          <span>{tooltipState.bucket.averageDurationMs.toFixed(2)} ms avg</span>
        </div>,
        document.body
      )}
    </>
  );
}

function DetailBlock({ title, value, note }) {
  const text = typeof value === 'string' ? value : JSON.stringify(value || {}, null, 2);

  return (
    <div className="history-detail-block">
      <div className="api-section-heading">
        <h4>{title}</h4>
        {note && <span>{note}</span>}
      </div>
      <pre className="api-code-block">{text || '(empty)'}</pre>
    </div>
  );
}

function RequestHistoryView() {
  const { apiClient, userInfo } = useAuth();
  const [entries, setEntries] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedEntry, setSelectedEntry] = useState(null);
  const [selectedDetail, setSelectedDetail] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [deleteEntry, setDeleteEntry] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showBulkDelete, setShowBulkDelete] = useState(false);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [alertState, setAlertState] = useState({ isOpen: false, title: '', message: '', variant: 'error' });
  const [activityRange, setActivityRange] = useState('lastDay');
  const [filters, setFilters] = useState(() => ({
    method: '',
    statusCode: '',
    route: '',
    indexId: '',
    principal: '',
    tenantId: '',
    userId: '',
    fromUtc: toLocalInputValue(new Date(Date.now() - 24 * 60 * 60 * 1000)),
    toUtc: toLocalInputValue(new Date())
  }));

  const scopedFilters = useMemo(() => ({
    method: filters.method,
    statusCode: filters.statusCode,
    route: filters.route,
    indexId: filters.indexId,
    principal: filters.principal,
    tenantId: userInfo?.isGlobalAdmin ? filters.tenantId : undefined,
    userId: userInfo?.isAdmin || userInfo?.isGlobalAdmin ? filters.userId : undefined
  }), [filters.indexId, filters.method, filters.principal, filters.route, filters.statusCode, filters.tenantId, filters.userId, userInfo]);

  const queryParams = useMemo(() => ({
    page,
    pageSize,
    ...scopedFilters,
    fromUtc: buildApiDate(filters.fromUtc),
    toUtc: buildApiDate(filters.toUtc)
  }), [filters.fromUtc, filters.toUtc, page, pageSize, scopedFilters]);

  const summaryQueryParams = useMemo(() => ({
    ...scopedFilters,
    ...buildActivityRangeParams(activityRange)
  }), [activityRange, scopedFilters]);

  useEffect(() => {
    let cancelled = false;

    async function loadEntries() {
      if (!apiClient) return;

      setLoading(true);
      try {
        const response = await apiClient.getRequestHistory(queryParams);
        if (!cancelled) {
          const data = response.data || {};
          setEntries(data.objects || []);
          setTotalCount(data.totalCount || 0);
        }
      } catch (err) {
        if (!cancelled) {
          setAlertState({ isOpen: true, title: 'Request History', message: err.message, variant: 'error' });
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadEntries();
    return () => {
      cancelled = true;
    };
  }, [apiClient, queryParams, refreshKey]);

  useEffect(() => {
    let cancelled = false;

    async function loadSummary() {
      if (!apiClient) return;

      setSummaryLoading(true);
      try {
        const response = await apiClient.getRequestHistorySummary(summaryQueryParams);
        if (!cancelled) {
          setSummary(response.data || null);
        }
      } catch (err) {
        if (!cancelled) {
          setAlertState({ isOpen: true, title: 'Request History', message: err.message, variant: 'error' });
        }
      } finally {
        if (!cancelled) setSummaryLoading(false);
      }
    }

    loadSummary();
    return () => {
      cancelled = true;
    };
  }, [apiClient, refreshKey, summaryQueryParams]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const successRate = summary?.totalCount ? ((summary.successCount / summary.totalCount) * 100).toFixed(1) : '0.0';
  const activityRangeConfig = getActivityRangeConfig(activityRange);

  const handleFilterChange = (key, value) => {
    setFilters((current) => ({ ...current, [key]: value }));
    setPage(1);
  };

  const resetFilters = () => {
    setFilters({
      method: '',
      statusCode: '',
      route: '',
      indexId: '',
      principal: '',
      tenantId: '',
      userId: '',
      fromUtc: toLocalInputValue(new Date(Date.now() - 24 * 60 * 60 * 1000)),
      toUtc: toLocalInputValue(new Date())
    });
    setActivityRange('lastDay');
    setPage(1);
  };

  const handleOpenDetails = async (entry) => {
    setSelectedEntry(entry);
    setSelectedDetail(null);
    setDetailLoading(true);

    try {
      const response = await apiClient.getRequestHistoryDetail(entry.id);
      setSelectedDetail(response.data || null);
    } catch (err) {
      setAlertState({ isOpen: true, title: 'Request History', message: err.message, variant: 'error' });
    } finally {
      setDetailLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteEntry) return;

    setIsDeleting(true);
    try {
      await apiClient.deleteRequestHistoryEntry(deleteEntry.id);
      setDeleteEntry(null);
      setSelectedEntry(null);
      setSelectedDetail(null);
      setRefreshKey((current) => current + 1);
    } catch (err) {
      setAlertState({ isOpen: true, title: 'Request History', message: err.message, variant: 'error' });
    } finally {
      setIsDeleting(false);
    }
  };

  const handleBulkDelete = async () => {
    setIsBulkDeleting(true);
    try {
      await apiClient.bulkDeleteRequestHistory(queryParams);
      setShowBulkDelete(false);
      setPage(1);
      setRefreshKey((current) => current + 1);
    } catch (err) {
      setAlertState({ isOpen: true, title: 'Request History', message: err.message, variant: 'error' });
    } finally {
      setIsBulkDeleting(false);
    }
  };

  return (
    <div className="request-history-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Request History</h2>
          <p className="workspace-subtitle">Inspect captured API traffic, filter it by route or principal, and drill into stored request and response payloads.</p>
        </div>
        <div className="workspace-actions">
          <button className="btn btn-secondary" onClick={resetFilters}>Reset Filters</button>
          <button className="btn btn-danger" onClick={() => setShowBulkDelete(true)} disabled={entries.length === 0}>Delete Filtered</button>
        </div>
      </div>

      <div className="request-history-top-grid">
        <div className="workspace-card">
          <div className="workspace-card-header">
            <h3>Summary</h3>
            <span className="request-history-range-caption">{activityRangeConfig.label}</span>
          </div>
          <div className="workspace-card-body request-history-stats">
            <div className="history-stat-card">
              <span>Total</span>
              <strong>{summaryLoading ? '...' : summary?.totalCount || 0}</strong>
            </div>
            <div className="history-stat-card">
              <span>Success Rate</span>
              <strong>{summaryLoading ? '...' : `${successRate}%`}</strong>
            </div>
            <div className="history-stat-card">
              <span>Failed</span>
              <strong>{summaryLoading ? '...' : summary?.failureCount || 0}</strong>
            </div>
            <div className="history-stat-card">
              <span>Avg Duration</span>
              <strong>{summaryLoading ? '...' : `${(summary?.averageDurationMs || 0).toFixed(2)} ms`}</strong>
            </div>
          </div>
        </div>

        <div className="workspace-card">
          <div className="workspace-card-header request-history-activity-header">
            <h3>Activity</h3>
            <div className="request-history-range-group" role="tablist" aria-label="Activity range">
              {ACTIVITY_RANGE_OPTIONS.map((option) => (
                <button
                  key={option.id}
                  type="button"
                  className={`request-history-range-btn ${activityRange === option.id ? 'active' : ''}`}
                  onClick={() => setActivityRange(option.id)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>
          <div className="workspace-card-body">
            {summaryLoading ? <div className="loading-spinner">Loading summary...</div> : <HistoryChart summary={summary} rangeId={activityRange} />}
          </div>
        </div>
      </div>

      <div className="workspace-card">
        <div className="workspace-card-header">
          <h3>Filters</h3>
        </div>
        <div className="workspace-card-body">
          <div className="request-history-filter-grid">
            <label className="api-input-field">
              <span>Method</span>
              <select value={filters.method} onChange={(event) => handleFilterChange('method', event.target.value)}>
                <option value="">All methods</option>
                <option value="GET">GET</option>
                <option value="POST">POST</option>
                <option value="PUT">PUT</option>
                <option value="DELETE">DELETE</option>
                <option value="PATCH">PATCH</option>
                <option value="HEAD">HEAD</option>
              </select>
            </label>
            <label className="api-input-field">
              <span>Status Code</span>
              <input type="text" value={filters.statusCode} onChange={(event) => handleFilterChange('statusCode', event.target.value)} placeholder="200" />
            </label>
            <label className="api-input-field">
              <span>Route</span>
              <input type="text" value={filters.route} onChange={(event) => handleFilterChange('route', event.target.value)} placeholder="/v1.0/indices" />
            </label>
            <label className="api-input-field">
              <span>Index ID</span>
              <input type="text" value={filters.indexId} onChange={(event) => handleFilterChange('indexId', event.target.value)} placeholder="default" />
            </label>
            <label className="api-input-field">
              <span>Principal</span>
              <input type="text" value={filters.principal} onChange={(event) => handleFilterChange('principal', event.target.value)} placeholder="default@user.com" />
            </label>
            {userInfo?.isGlobalAdmin && (
              <label className="api-input-field">
                <span>Tenant ID</span>
                <input type="text" value={filters.tenantId} onChange={(event) => handleFilterChange('tenantId', event.target.value)} placeholder="default" />
              </label>
            )}
            {(userInfo?.isAdmin || userInfo?.isGlobalAdmin) && (
              <label className="api-input-field">
                <span>User ID</span>
                <input type="text" value={filters.userId} onChange={(event) => handleFilterChange('userId', event.target.value)} placeholder="default" />
              </label>
            )}
            <label className="api-input-field">
              <span>From</span>
              <input type="datetime-local" value={filters.fromUtc} onChange={(event) => handleFilterChange('fromUtc', event.target.value)} />
            </label>
            <label className="api-input-field">
              <span>To</span>
              <input type="datetime-local" value={filters.toUtc} onChange={(event) => handleFilterChange('toUtc', event.target.value)} />
            </label>
          </div>
        </div>
      </div>

      <div className="workspace-card">
        <div className="workspace-card-header">
          <h3>Entries</h3>
          <span className="workspace-subtitle">{totalCount} visible requests</span>
        </div>
        <div className="workspace-card-body request-history-table-wrap">
          {loading ? (
            <div className="loading-spinner">Loading request history...</div>
          ) : entries.length === 0 ? (
            <div className="empty-state compact-empty-state"><p className="empty-state-description">No entries match the current filters.</p></div>
          ) : (
            <table className="data-table request-history-table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Method</th>
                  <th>Route</th>
                  <th>Principal</th>
                  <th>Status</th>
                  <th>Duration</th>
                  <th>Payloads</th>
                  <th className="actions-column">Actions</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry) => (
                  <tr key={entry.id}>
                    <td>{formatDate(entry.createdUtc)}</td>
                    <td><span className={`api-method-badge method-${entry.httpMethod.toLowerCase()}`}>{entry.httpMethod}</span></td>
                    <td><code>{entry.routeTemplate}</code></td>
                    <td>{entry.principalName || 'Anonymous'}</td>
                    <td><span className={`status-pill ${entry.success ? 'success' : 'error'}`}>{entry.statusCode}</span></td>
                    <td>{entry.durationMs?.toFixed(2)} ms</td>
                    <td>{formatBytes(entry.requestSizeBytes)} / {formatBytes(entry.responseSizeBytes)}</td>
                    <td className="actions-column">
                      <div className="history-actions">
                        <button className="btn btn-secondary btn-sm" onClick={() => handleOpenDetails(entry)}>View</button>
                        <button className="btn btn-danger btn-sm" onClick={() => setDeleteEntry(entry)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <Pagination
          currentPage={page}
          totalPages={totalPages}
          pageSize={pageSize}
          totalItems={totalCount}
          onPageChange={setPage}
          onPageSizeChange={(nextPageSize) => {
            setPageSize(nextPageSize);
            setPage(1);
          }}
        />
      </div>

      <Modal isOpen={!!selectedEntry} onClose={() => { setSelectedEntry(null); setSelectedDetail(null); }} title="Request Detail" size="fullscreen">
        {!selectedEntry ? null : detailLoading ? (
          <div className="loading-spinner">Loading request detail...</div>
        ) : (
          <div className="request-history-detail">
            <div className="history-detail-summary">
              <div className="history-detail-summary-item">
                <span>Entry ID</span>
                <CopyableId value={selectedEntry.id} />
              </div>
              <div className="history-detail-summary-item">
                <span>Route</span>
                <strong>{selectedEntry.routeTemplate}</strong>
              </div>
              <div className="history-detail-summary-item">
                <span>Principal</span>
                <strong>{selectedEntry.principalName || 'Anonymous'}</strong>
              </div>
              <div className="history-detail-summary-item">
                <span>Status</span>
                <strong>{selectedEntry.statusCode}</strong>
              </div>
            </div>

            <div className="request-history-detail-grid">
              <DetailBlock title="Route Parameters" value={selectedDetail?.routeParameters || {}} />
              <DetailBlock title="Query Parameters" value={selectedDetail?.queryParameters || {}} />
              <DetailBlock title="Request Headers" value={selectedDetail?.requestHeaders || {}} />
              <DetailBlock title="Response Headers" value={selectedDetail?.responseHeaders || {}} />
            </div>

            <DetailBlock
              title="Request Body"
              value={selectedDetail?.requestBody || '(empty)'}
              note={selectedDetail?.requestBodyTruncated ? `Stored ${formatBytes(selectedDetail.requestBodyBytes)} (truncated)` : formatBytes(selectedDetail?.requestBodyBytes || 0)}
            />
            <DetailBlock
              title="Response Body"
              value={selectedDetail?.responseBody || '(empty)'}
              note={selectedDetail?.responseBodyTruncated ? `Stored ${formatBytes(selectedDetail.responseBodyBytes)} (truncated)` : formatBytes(selectedDetail?.responseBodyBytes || 0)}
            />
          </div>
        )}
      </Modal>

      <ConfirmModal
        isOpen={!!deleteEntry}
        onClose={() => setDeleteEntry(null)}
        onConfirm={handleDelete}
        title="Delete Request Entry"
        message="This removes the stored request history entry and its JSON detail file."
        entityName={deleteEntry?.routeTemplate}
        confirmLabel="Delete Entry"
        isLoading={isDeleting}
      />

      <ConfirmModal
        isOpen={showBulkDelete}
        onClose={() => setShowBulkDelete(false)}
        onConfirm={handleBulkDelete}
        title="Delete Filtered Entries"
        message="This deletes every request history record that matches the current filter set."
        warningMessage="This cannot be undone."
        confirmLabel="Delete Filtered"
        isLoading={isBulkDeleting}
      />

      <AlertModal
        isOpen={alertState.isOpen}
        onClose={() => setAlertState({ isOpen: false, title: '', message: '', variant: 'error' })}
        title={alertState.title}
        message={alertState.message}
        variant={alertState.variant}
      />
    </div>
  );
}

export default RequestHistoryView;
