import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import './MetadataModal.css';
import './ApiExplorerView.css';

const HISTORY_KEY = 'verbex_api_explorer_history';
const RESPONSE_TABS = ['preview', 'body', 'headers', 'status', 'code'];
const CODE_TABS = ['curl', 'fetch', 'csharp'];

function loadExplorerHistory() {
  try {
    const saved = localStorage.getItem(HISTORY_KEY);
    return saved ? JSON.parse(saved) : [];
  } catch {
    return [];
  }
}

function saveExplorerHistory(history) {
  localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, 12)));
}

function resolveSchema(schema, spec, seen = new Set()) {
  if (!schema) return null;

  if (schema.$ref) {
    const refName = schema.$ref.split('/').pop();
    if (!refName || seen.has(refName)) return null;
    seen.add(refName);
    return resolveSchema(spec?.components?.schemas?.[refName], spec, seen);
  }

  if (schema.allOf?.length) {
    return schema.allOf.reduce((merged, item) => {
      const resolved = resolveSchema(item, spec, new Set(seen));
      return {
        ...merged,
        ...resolved,
        properties: { ...(merged?.properties || {}), ...(resolved?.properties || {}) }
      };
    }, {});
  }

  if (schema.oneOf?.length) return resolveSchema(schema.oneOf[0], spec, seen);
  if (schema.anyOf?.length) return resolveSchema(schema.anyOf[0], spec, seen);
  return schema;
}

function schemaExample(schema, spec) {
  const resolved = resolveSchema(schema, spec);
  if (!resolved) return {};
  if (resolved.example !== undefined) return resolved.example;
  if (resolved.default !== undefined) return resolved.default;

  switch (resolved.type) {
    case 'object': {
      const value = {};
      Object.entries(resolved.properties || {}).forEach(([key, childSchema]) => {
        value[key] = schemaExample(childSchema, spec);
      });
      return value;
    }
    case 'array':
      return resolved.items ? [schemaExample(resolved.items, spec)] : [];
    case 'integer':
    case 'number':
      return 0;
    case 'boolean':
      return false;
    case 'string':
      if (resolved.enum?.length) return resolved.enum[0];
      if (resolved.format === 'date-time') return new Date().toISOString();
      return '';
    default:
      return {};
  }
}

function parameterInitialValue(parameter, spec) {
  if (parameter.example !== undefined) return String(parameter.example);
  const resolved = resolveSchema(parameter.schema, spec);
  if (resolved?.default !== undefined) return String(resolved.default);
  if (resolved?.enum?.length) return String(resolved.enum[0]);
  return '';
}

function formatBytes(bytes) {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(value >= 10 || unit === 0 ? 0 : 1)} ${units[unit]}`;
}

function prettifyContent(body, contentType) {
  if (!body) return '(empty)';
  if (contentType?.includes('application/json')) {
    try {
      return JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      return body;
    }
  }
  return body;
}

function generateCodeSnippets(request) {
  const headerLines = Object.entries(request.headers || {});
  const curlHeaders = headerLines.map(([key, value]) => `-H "${key}: ${value}"`).join(' \\\n  ');
  const fetchHeaders = headerLines.length > 0
    ? `,\n  headers: ${JSON.stringify(request.headers, null, 2).replace(/\n/g, '\n  ')}`
    : '';
  const csharpHeaders = headerLines.map(([key, value]) => `request.Headers.TryAddWithoutValidation("${key}", "${value}");`).join('\n');
  const body = request.body ? prettifyContent(request.body, request.contentType || 'application/json') : '';
  const curlBody = request.body ? ` \\\n  --data '${body.replace(/'/g, "'\\''")}'` : '';
  const fetchBody = request.body ? `,\n  body: ${JSON.stringify(body)}` : '';
  const csharpBody = request.body
    ? `request.Content = new StringContent(${JSON.stringify(body)}, Encoding.UTF8, "${request.contentType || 'application/json'}");`
    : '';

  return {
    curl: `curl -X ${request.method.toUpperCase()} "${request.url}"${curlHeaders ? ` \\\n  ${curlHeaders}` : ''}${curlBody}`,
    fetch: `const response = await fetch(${JSON.stringify(request.url)}, {\n  method: ${JSON.stringify(request.method.toUpperCase())}${fetchHeaders}${fetchBody}\n});\n\nconst data = await response.text();`,
    csharp: `using System.Net.Http;\nusing System.Text;\n\nusing var client = new HttpClient();\nusing var request = new HttpRequestMessage(HttpMethod.${request.method.charAt(0).toUpperCase() + request.method.slice(1).toLowerCase()}, ${JSON.stringify(request.url)});\n${csharpHeaders}${csharpBody ? `\n${csharpBody}` : ''}\nusing var response = await client.SendAsync(request);\nvar body = await response.Content.ReadAsStringAsync();`
  };
}

function methodClass(method) {
  return `api-method-badge method-${method.toLowerCase()}`;
}

function getOperationSubtext(operation) {
  const summary = operation.summary?.trim();
  const description = operation.description?.trim();
  const methodPath = `${operation.method.toUpperCase()} ${operation.path}`;

  if (description && description !== summary && description !== methodPath && description !== operation.path) {
    return description;
  }

  if (summary && summary !== methodPath && summary !== operation.path) {
    return summary;
  }

  return '';
}

function getResponseText(response, responseTab, codeTab) {
  if (!response) return '';

  if (responseTab === 'headers') {
    return JSON.stringify(response.headers, null, 2);
  }

  if (responseTab === 'status') {
    return JSON.stringify({
      status: response.status,
      statusText: response.statusText,
      contentType: response.contentType,
      durationMs: response.durationMs,
      sizeBytes: response.sizeBytes
    }, null, 2);
  }

  if (responseTab === 'code') {
    return response.code[codeTab];
  }

  if (responseTab === 'preview') {
    return typeof response.preview === 'string'
      ? response.preview
      : JSON.stringify(response.preview, null, 2);
  }

  return prettifyContent(response.body, response.contentType);
}

async function copyText(text) {
  let success = false;

  if (navigator.clipboard && navigator.clipboard.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      success = true;
    } catch {
      success = false;
    }
  }

  if (!success) {
    try {
      const textArea = document.createElement('textarea');
      textArea.value = text;
      textArea.style.position = 'fixed';
      textArea.style.left = '-9999px';
      textArea.style.top = '-9999px';
      document.body.appendChild(textArea);
      textArea.focus();
      textArea.select();
      success = document.execCommand('copy');
      document.body.removeChild(textArea);
    } catch {
      success = false;
    }
  }

  return success;
}

function ApiExplorerView() {
  const { serverUrl, token } = useAuth();
  const [spec, setSpec] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [selectedOperationId, setSelectedOperationId] = useState('');
  const [operationFilter, setOperationFilter] = useState('');
  const [selectedTag, setSelectedTag] = useState('All');
  const [pathValues, setPathValues] = useState({});
  const [queryValues, setQueryValues] = useState({});
  const [headerValues, setHeaderValues] = useState({});
  const [bodyValue, setBodyValue] = useState('');
  const [response, setResponse] = useState(null);
  const [responseTab, setResponseTab] = useState('preview');
  const [codeTab, setCodeTab] = useState('curl');
  const [history, setHistory] = useState(() => loadExplorerHistory());
  const [abortController, setAbortController] = useState(null);
  const [pendingReplay, setPendingReplay] = useState(null);
  const [responseCopied, setResponseCopied] = useState(false);

  const operations = useMemo(() => {
    if (!spec?.paths) return [];

    return Object.entries(spec.paths).flatMap(([path, pathItem]) =>
      Object.entries(pathItem)
        .filter(([method]) => ['get', 'post', 'put', 'delete', 'patch', 'head'].includes(method))
        .map(([method, operation]) => {
          const mergedParameters = [...(pathItem.parameters || []), ...(operation.parameters || [])];
          const parameters = mergedParameters.filter((parameter, index) => {
            const current = `${parameter.in}:${parameter.name}`;
            return mergedParameters.findIndex((item) => `${item.in}:${item.name}` === current) === index;
          });
          const requestBodyContent = operation.requestBody?.content || {};
          const preferredContentType = requestBodyContent['application/json']
            ? 'application/json'
            : Object.keys(requestBodyContent)[0] || '';

          return {
            id: operation.operationId || `${method}:${path}`,
            method,
            path,
            summary: operation.summary || `${method.toUpperCase()} ${path}`,
            description: operation.description || '',
            tag: operation.tags?.[0] || 'General',
            parameters,
            requestBody: requestBodyContent[preferredContentType] || null,
            requestBodyContentType: preferredContentType,
            responses: operation.responses || {}
          };
        })
    );
  }, [spec]);

  const tags = useMemo(() => ['All', ...new Set(operations.map((operation) => operation.tag))], [operations]);
  const filteredOperations = useMemo(() => {
    return operations.filter((operation) => {
      const matchesTag = selectedTag === 'All' || operation.tag === selectedTag;
      const haystack = `${operation.summary} ${operation.path} ${operation.tag}`.toLowerCase();
      const matchesFilter = !operationFilter || haystack.includes(operationFilter.toLowerCase());
      return matchesTag && matchesFilter;
    });
  }, [operations, operationFilter, selectedTag]);

  const selectedOperation = useMemo(
    () => operations.find((operation) => operation.id === selectedOperationId) || filteredOperations[0] || null,
    [operations, filteredOperations, selectedOperationId]
  );

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();

    async function loadSpec() {
      if (!serverUrl) return;

      setLoading(true);
      setError('');

      try {
        const response = await fetch(`${serverUrl.replace(/\/$/, '')}/openapi.json`, {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
          signal: controller.signal
        });

        if (!response.ok) throw new Error(`Failed to load OpenAPI document (${response.status})`);
        const data = await response.json();
        if (!cancelled) setSpec(data);
      } catch (err) {
        if (!cancelled && err.name !== 'AbortError') {
          setError(err.message || 'Failed to load OpenAPI document');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadSpec();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [serverUrl, token]);

  useEffect(() => {
    if (!selectedOperation && filteredOperations.length > 0) {
      setSelectedOperationId(filteredOperations[0].id);
    }
  }, [filteredOperations, selectedOperation]);

  useEffect(() => {
    if (!selectedOperation) return;

    if (pendingReplay?.operationId === selectedOperation.id) {
      setPathValues(pendingReplay.request.pathValues || {});
      setQueryValues(pendingReplay.request.queryValues || {});
      setHeaderValues(pendingReplay.request.headerValues || {});
      setBodyValue(pendingReplay.request.bodyValue || '');
      setPendingReplay(null);
    } else {
      const nextPath = {};
      const nextQuery = {};
      const nextHeaders = {};

      selectedOperation.parameters.forEach((parameter) => {
        if (parameter.in === 'path') nextPath[parameter.name] = parameterInitialValue(parameter, spec);
        if (parameter.in === 'query') nextQuery[parameter.name] = parameterInitialValue(parameter, spec);
        if (parameter.in === 'header' && !['authorization', 'content-type'].includes(parameter.name.toLowerCase())) {
          nextHeaders[parameter.name] = parameterInitialValue(parameter, spec);
        }
      });

      setPathValues(nextPath);
      setQueryValues(nextQuery);
      setHeaderValues(nextHeaders);
      setBodyValue(selectedOperation.requestBody
        ? JSON.stringify(schemaExample(selectedOperation.requestBody.schema, spec), null, 2)
        : '');
    }

    setResponse(null);
    setResponseTab('preview');
    setCodeTab('curl');
  }, [selectedOperation, spec, pendingReplay]);

  const requestPreview = useMemo(() => {
    if (!selectedOperation || !serverUrl) return null;

    const path = Object.entries(pathValues).reduce(
      (currentPath, [key, value]) => currentPath.replace(`{${key}}`, encodeURIComponent(value || `{${key}}`)),
      selectedOperation.path
    );

    const url = new URL(path, `${serverUrl.replace(/\/$/, '')}/`);
    Object.entries(queryValues).forEach(([key, value]) => {
      if (value !== '') url.searchParams.set(key, value);
    });

    const headers = {};
    if (token) headers.Authorization = `Bearer ${token}`;
    Object.entries(headerValues).forEach(([key, value]) => {
      if (value !== '') headers[key] = value;
    });
    if (selectedOperation.requestBody && bodyValue.trim()) {
      headers['Content-Type'] = selectedOperation.requestBodyContentType || 'application/json';
    }

    return {
      method: selectedOperation.method,
      url: url.toString(),
      headers,
      body: bodyValue.trim() || '',
      contentType: selectedOperation.requestBodyContentType || 'application/json'
    };
  }, [selectedOperation, serverUrl, token, pathValues, queryValues, headerValues, bodyValue]);

  const responseCopyText = useMemo(
    () => getResponseText(response, responseTab, codeTab),
    [response, responseTab, codeTab]
  );

  const handleSend = async () => {
    if (!requestPreview) return;

    const controller = new AbortController();
    setAbortController(controller);
    setError('');
    const startedAt = performance.now();

    try {
      const fetchOptions = {
        method: requestPreview.method.toUpperCase(),
        headers: requestPreview.headers,
        signal: controller.signal
      };

      if (requestPreview.body) {
        fetchOptions.body = requestPreview.body;
      }

      const result = await fetch(requestPreview.url, fetchOptions);
      const durationMs = performance.now() - startedAt;
      const responseHeaders = Object.fromEntries(result.headers.entries());
      const contentType = result.headers.get('content-type') || '';

      let rawBody = '';
      let sizeBytes = 0;
      let preview = null;

      if (contentType.includes('application/json') || contentType.startsWith('text/')) {
        rawBody = await result.text();
        sizeBytes = new TextEncoder().encode(rawBody).length;
        if (contentType.includes('application/json')) {
          try {
            preview = JSON.parse(rawBody);
          } catch {
            preview = rawBody;
          }
        } else {
          preview = rawBody;
        }
      } else {
        const blob = await result.blob();
        sizeBytes = blob.size;
        rawBody = `Binary response (${blob.type || 'application/octet-stream'}, ${blob.size} bytes)`;
        preview = rawBody;
      }

      const nextResponse = {
        ok: result.ok,
        status: result.status,
        statusText: result.statusText,
        durationMs,
        headers: responseHeaders,
        contentType,
        body: rawBody,
        preview,
        sizeBytes,
        code: generateCodeSnippets(requestPreview)
      };

      setResponse(nextResponse);
      setResponseCopied(false);
      setHistory((currentHistory) => {
        const nextHistory = [
          {
            id: crypto.randomUUID(),
            timestamp: new Date().toISOString(),
            operationId: selectedOperation.id,
            summary: selectedOperation.summary,
            method: selectedOperation.method,
            path: selectedOperation.path,
            status: result.status,
            request: {
              pathValues,
              queryValues,
              headerValues,
              bodyValue
            }
          },
          ...currentHistory
        ].slice(0, 12);

        saveExplorerHistory(nextHistory);
        return nextHistory;
      });
    } catch (err) {
      if (err.name !== 'AbortError') {
        setError(err.message || 'Request failed');
      }
    } finally {
      setAbortController(null);
    }
  };

  const handleReplay = (entry) => {
    setPendingReplay(entry);
    setSelectedOperationId(entry.operationId);
  };

  const clearHistory = () => {
    setHistory([]);
    saveExplorerHistory([]);
  };

  const handleCopyResponse = async () => {
    if (!responseCopyText) return;

    const success = await copyText(responseCopyText);
    if (success) {
      setResponseCopied(true);
      window.setTimeout(() => setResponseCopied(false), 2000);
    }
  };

  return (
    <div className="api-explorer-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>API Explorer</h2>
          <p className="workspace-subtitle">Browse the live OpenAPI document, compose requests, and replay recent calls from the dashboard.</p>
        </div>
        <div className="workspace-actions">
          <button className="btn btn-secondary" onClick={() => window.open(`${serverUrl.replace(/\/$/, '')}/swagger`, '_blank', 'noopener,noreferrer')}>
            Swagger UI
          </button>
          <button className="btn btn-primary" onClick={handleSend} disabled={!selectedOperation || !!abortController || loading}>
            {abortController ? 'Running...' : 'Send Request'}
          </button>
          {abortController && (
            <button className="btn btn-secondary" onClick={() => abortController.abort()}>
              Stop
            </button>
          )}
        </div>
      </div>

      {error && <div className="api-explorer-error">{error}</div>}

      <div className="api-explorer-layout">
        <div className="api-explorer-sidebar">
          <div className="workspace-card">
            <div className="workspace-card-header">
              <h3>Operations</h3>
            </div>
            <div className="workspace-card-body api-explorer-card-body">
              <div className="api-explorer-filters">
                <input
                  type="text"
                  value={operationFilter}
                  onChange={(event) => setOperationFilter(event.target.value)}
                  placeholder="Filter operations"
                />
                <select value={selectedTag} onChange={(event) => setSelectedTag(event.target.value)}>
                  {tags.map((tag) => (
                    <option key={tag} value={tag}>{tag}</option>
                  ))}
                </select>
              </div>
              <div className="api-explorer-operations">
                {loading && <div className="loading-spinner">Loading OpenAPI document...</div>}
                {!loading && filteredOperations.map((operation) => (
                  <button
                    key={operation.id}
                    type="button"
                    className={`api-operation-item ${selectedOperation?.id === operation.id ? 'active' : ''}`}
                    onClick={() => setSelectedOperationId(operation.id)}
                  >
                    <span className={methodClass(operation.method)}>{operation.method.toUpperCase()}</span>
                    <span className="api-operation-label">{operation.path}</span>
                    {getOperationSubtext(operation) && (
                      <span className="api-operation-subtext">{getOperationSubtext(operation)}</span>
                    )}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="workspace-card">
            <div className="workspace-card-header">
              <h3>Recent Requests</h3>
              <button className="btn btn-secondary btn-sm" onClick={clearHistory} disabled={history.length === 0}>Clear</button>
            </div>
            <div className="workspace-card-body api-explorer-card-body">
              {history.length === 0 ? (
                <div className="empty-state compact-empty-state">
                  <p className="empty-state-description">Local replay history is empty.</p>
                </div>
              ) : (
                <div className="api-history-list">
                  {history.map((entry) => (
                    <button key={entry.id} type="button" className="api-history-item" onClick={() => handleReplay(entry)}>
                      <span className={methodClass(entry.method)}>{entry.method.toUpperCase()}</span>
                      <span className="api-history-summary">{entry.summary}</span>
                      <span className="api-history-meta">{new Date(entry.timestamp).toLocaleString()} - {entry.status || 'pending'}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="api-explorer-main">
          <div className="workspace-card">
            <div className="workspace-card-header">
              <h3>{selectedOperation?.summary || 'Request Builder'}</h3>
              {selectedOperation && <span className="api-tag-badge">{selectedOperation.tag}</span>}
            </div>
            <div className="workspace-card-body api-explorer-card-body">
              {selectedOperation ? (
                <>
                  <div className="api-request-overview">
                    <span className={methodClass(selectedOperation.method)}>{selectedOperation.method.toUpperCase()}</span>
                    <code>{selectedOperation.path}</code>
                  </div>
                  {selectedOperation.description && <p className="workspace-subtitle">{selectedOperation.description}</p>}

                  <div className="api-parameter-grid">
                    <ParameterSection
                      title="Path Parameters"
                      parameters={selectedOperation.parameters.filter((parameter) => parameter.in === 'path')}
                      values={pathValues}
                      onChange={setPathValues}
                    />
                    <ParameterSection
                      title="Query Parameters"
                      parameters={selectedOperation.parameters.filter((parameter) => parameter.in === 'query')}
                      values={queryValues}
                      onChange={setQueryValues}
                    />
                    <ParameterSection
                      title="Headers"
                      parameters={selectedOperation.parameters.filter((parameter) => parameter.in === 'header' && !['authorization', 'content-type'].includes(parameter.name.toLowerCase()))}
                      values={headerValues}
                      onChange={setHeaderValues}
                    />
                  </div>

                  {selectedOperation.requestBody && (
                    <div className="api-body-section">
                      <div className="api-section-heading">
                        <h4>Request Body</h4>
                        <span>{selectedOperation.requestBodyContentType || 'application/json'}</span>
                      </div>
                      <textarea
                        value={bodyValue}
                        onChange={(event) => setBodyValue(event.target.value)}
                        spellCheck="false"
                        rows={14}
                      />
                    </div>
                  )}

                  {requestPreview && (
                    <div className="api-request-preview">
                      <div className="api-section-heading">
                        <h4>Request Preview</h4>
                        <span>{requestPreview.method.toUpperCase()}</span>
                      </div>
                      <pre>{requestPreview.url}</pre>
                    </div>
                  )}
                </>
              ) : (
                <div className="empty-state compact-empty-state">
                  <p className="empty-state-description">No operations are available in the current OpenAPI document.</p>
                </div>
              )}
            </div>
          </div>

          <div className="workspace-card">
            <div className="workspace-card-header">
              <h3>Response</h3>
              {response && (
                <div className="api-response-meta">
                  <span className={`status-pill ${response.ok ? 'success' : 'error'}`}>{response.status} {response.statusText}</span>
                  <span>{response.durationMs.toFixed(2)} ms</span>
                  <span>{formatBytes(response.sizeBytes)}</span>
                  <button
                    className={`metadata-copy-btn ${responseCopied ? 'copied' : ''}`}
                    onClick={handleCopyResponse}
                    title={responseCopied ? 'Copied!' : 'Copy current response view'}
                    type="button"
                  >
                    {responseCopied ? (
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    ) : (
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
                      </svg>
                    )}
                  </button>
                </div>
              )}
            </div>
            <div className="workspace-card-body api-explorer-card-body">
              <div className="api-tab-row">
                {RESPONSE_TABS.map((tab) => (
                  <button
                    key={tab}
                    type="button"
                    className={`api-tab ${responseTab === tab ? 'active' : ''}`}
                    onClick={() => setResponseTab(tab)}
                    disabled={!response}
                  >
                    {tab}
                  </button>
                ))}
              </div>

              {!response ? (
                <div className="empty-state compact-empty-state">
                  <p className="empty-state-description">Send a request to inspect the live response here.</p>
                </div>
              ) : (
                <ResponsePanel response={response} responseTab={responseTab} codeTab={codeTab} onCodeTabChange={setCodeTab} />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function ParameterSection({ title, parameters, values, onChange }) {
  return (
    <div className="api-parameter-section">
      <div className="api-section-heading">
        <h4>{title}</h4>
        <span>{parameters.length}</span>
      </div>
      {parameters.length === 0 ? (
        <div className="api-empty-copy">No parameters</div>
      ) : (
        <div className="api-input-stack">
          {parameters.map((parameter) => (
            <label key={`${parameter.in}-${parameter.name}`} className="api-input-field">
              <span>{parameter.name}</span>
              <input
                type="text"
                value={values[parameter.name] || ''}
                onChange={(event) => onChange((current) => ({ ...current, [parameter.name]: event.target.value }))}
                placeholder={parameter.description || parameter.name}
              />
            </label>
          ))}
        </div>
      )}
    </div>
  );
}

function ResponsePanel({ response, responseTab, codeTab, onCodeTabChange }) {
  if (responseTab === 'headers') {
    return (
      <div className="api-response-table">
        {Object.entries(response.headers).map(([key, value]) => (
          <div key={key} className="api-response-row">
            <span>{key}</span>
            <code>{value}</code>
          </div>
        ))}
      </div>
    );
  }

  if (responseTab === 'status') {
    return (
      <div className="api-status-grid">
        <div className="api-stat-card">
          <span>Status</span>
          <strong>{response.status}</strong>
        </div>
        <div className="api-stat-card">
          <span>Content Type</span>
          <strong>{response.contentType || 'n/a'}</strong>
        </div>
        <div className="api-stat-card">
          <span>Duration</span>
          <strong>{response.durationMs.toFixed(2)} ms</strong>
        </div>
        <div className="api-stat-card">
          <span>Size</span>
          <strong>{formatBytes(response.sizeBytes)}</strong>
        </div>
      </div>
    );
  }

  if (responseTab === 'code') {
    return (
      <>
        <div className="api-tab-row nested">
          {CODE_TABS.map((tab) => (
            <button key={tab} type="button" className={`api-tab ${codeTab === tab ? 'active' : ''}`} onClick={() => onCodeTabChange(tab)}>
              {tab}
            </button>
          ))}
        </div>
        <pre className="api-code-block">{response.code[codeTab]}</pre>
      </>
    );
  }

  const value = getResponseText(response, responseTab, codeTab);

  return <pre className="api-code-block">{value || '(empty)'}</pre>;
}

export default ApiExplorerView;
