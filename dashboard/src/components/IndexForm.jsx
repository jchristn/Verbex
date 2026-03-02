import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import JsonEditor from './JsonEditor';
import CopyableId from './CopyableId';
import './IndexForm.css';

function IndexForm({ onSuccess, onCancel, tenants = [] }) {
  const { apiClient, userInfo } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [labels, setLabels] = useState([]);
  const [tags, setTags] = useState({});
  const [customMetadata, setCustomMetadata] = useState(null);
  const [selectedTenantId, setSelectedTenantId] = useState('');

  const isGlobalAdmin = userInfo?.isGlobalAdmin;

  // Auto-select tenant if only one exists
  useEffect(() => {
    if (isGlobalAdmin && tenants.length === 1 && !selectedTenantId) {
      setSelectedTenantId(tenants[0].identifier);
    }
  }, [isGlobalAdmin, tenants, selectedTenantId]);

  const [formData, setFormData] = useState({
    identifier: '',
    name: '',
    description: '',
    inMemory: false,
    enableLemmatizer: true,
    enableStopWordRemover: true,
    minTokenLength: 2,
    maxTokenLength: 50
  });

  // Cache configuration state
  const [showCacheSettings, setShowCacheSettings] = useState(false);
  const [cacheConfig, setCacheConfig] = useState({
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
  });

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleNumberChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: parseInt(value, 10) || 0
    }));
  };

  const handleCacheChange = (e) => {
    const { name, value, type, checked } = e.target;
    setCacheConfig((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleCacheNumberChange = (e) => {
    const { name, value } = e.target;
    setCacheConfig((prev) => ({
      ...prev,
      [name]: parseInt(value, 10) || 0
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setIsSubmitting(true);

    try {
      const indexConfig = {
        name: formData.name,
        description: formData.description || undefined,
        inMemory: formData.inMemory,
        enableLemmatizer: formData.enableLemmatizer,
        enableStopWordRemover: formData.enableStopWordRemover,
        minTokenLength: formData.minTokenLength,
        maxTokenLength: formData.maxTokenLength
      };

      // Include custom identifier if provided
      if (formData.identifier && formData.identifier.trim()) {
        indexConfig.identifier = formData.identifier.trim();
      }

      // For global admins, include the selected tenant ID
      if (isGlobalAdmin) {
        if (!selectedTenantId) {
          setError('Please select a tenant');
          setIsSubmitting(false);
          return;
        }
        indexConfig.tenantId = selectedTenantId;
      }

      if (labels.length > 0) {
        indexConfig.labels = labels;
      }
      if (Object.keys(tags).length > 0) {
        indexConfig.tags = tags;
      }
      if (customMetadata !== null) {
        indexConfig.customMetadata = customMetadata;
      }

      // Include cache configuration if caching is enabled
      if (cacheConfig.enabled) {
        indexConfig.cacheConfiguration = cacheConfig;
      }

      await apiClient.createIndex(indexConfig);
      onSuccess();
    } catch (err) {
      setError(err.message || 'Failed to create index');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form className="index-form" onSubmit={handleSubmit}>
      <div className="form-section">
        <h4>Basic Information</h4>

        {isGlobalAdmin ? (
          <div className="form-group">
            <label htmlFor="tenantSelect">Tenant *</label>
            <select
              id="tenantSelect"
              value={selectedTenantId}
              onChange={(e) => setSelectedTenantId(e.target.value)}
              required
              title="Select the tenant that will own this index"
            >
              <option value="">-- Select a tenant --</option>
              {tenants.map((tenant) => (
                <option key={tenant.identifier} value={tenant.identifier}>
                  {tenant.name || tenant.identifier}
                </option>
              ))}
            </select>
          </div>
        ) : userInfo?.tenantId ? (
          <div className="form-group">
            <label>Tenant</label>
            <div className="form-static-value">
              <CopyableId value={userInfo.tenantId} />
            </div>
          </div>
        ) : null}

        <div className="form-group">
          <label htmlFor="identifier">Identifier</label>
          <input
            type="text"
            id="identifier"
            name="identifier"
            value={formData.identifier}
            onChange={handleChange}
            placeholder="my-custom-index-id"
            title="Custom identifier for this index (optional)"
          />
          <span className="form-hint">Optional. Leave blank to auto-generate a unique identifier</span>
        </div>

        <div className="form-group">
          <label htmlFor="name">Display Name *</label>
          <input
            type="text"
            id="name"
            name="name"
            value={formData.name}
            onChange={handleChange}
            placeholder="My Index"
            required
            title="Human-readable name for the index"
          />
        </div>

        <div className="form-group">
          <label htmlFor="description">Description</label>
          <textarea
            id="description"
            name="description"
            value={formData.description}
            onChange={handleChange}
            placeholder="Optional description of this index"
            rows={2}
            title="Optional description of this index"
          />
        </div>

        <div className="form-group">
          <label>Labels</label>
          <TagInput
            value={labels}
            onChange={setLabels}
            placeholder="Add labels..."
          />
        </div>

        <div className="form-group">
          <label>Tags</label>
          <KeyValueEditor
            value={tags}
            onChange={setTags}
            keyPlaceholder="Tag name"
            valuePlaceholder="Tag value"
          />
        </div>

        <div className="form-group">
          <JsonEditor
            value={customMetadata}
            onChange={setCustomMetadata}
            placeholder='{"key": "value"}'
            label="Custom Metadata"
          />
        </div>
      </div>

      <div className="form-section">
        <div className="form-group form-group-checkbox">
          <label>
            <input
              type="checkbox"
              name="inMemory"
              checked={formData.inMemory}
              onChange={handleChange}
              title="Store index in memory only (not persisted)"
            />
            In-Memory Storage
          </label>
          <span className="form-hint">Fastest performance, but data is not persisted on restart</span>
        </div>

        <div className="form-row">
          <div className="form-group form-group-checkbox">
            <label>
              <input
                type="checkbox"
                name="enableLemmatizer"
                checked={formData.enableLemmatizer}
                onChange={handleChange}
                title="Reduce words to their base forms during indexing"
              />
              Enable Lemmatization
            </label>
            <span className="form-hint">Reduces words to base forms (e.g., "running" to "run")</span>
          </div>

          <div className="form-group form-group-checkbox">
            <label>
              <input
                type="checkbox"
                name="enableStopWordRemover"
                checked={formData.enableStopWordRemover}
                onChange={handleChange}
                title="Filter out common words like 'the' and 'and'"
              />
              Remove Stop Words
            </label>
            <span className="form-hint">Filters common words like "the", "and"</span>
          </div>
        </div>

        <div className="form-row">
          <div className="form-group">
            <label htmlFor="minTokenLength">Min Token Length</label>
            <input
              type="number"
              id="minTokenLength"
              name="minTokenLength"
              value={formData.minTokenLength}
              onChange={handleNumberChange}
              min={0}
              max={100}
              title="Minimum character length for indexed tokens"
            />
          </div>

          <div className="form-group">
            <label htmlFor="maxTokenLength">Max Token Length</label>
            <input
              type="number"
              id="maxTokenLength"
              name="maxTokenLength"
              value={formData.maxTokenLength}
              onChange={handleNumberChange}
              min={0}
              max={1000}
              title="Maximum character length for indexed tokens"
            />
          </div>
        </div>
      </div>

      {/* Caching Configuration Section */}
      <div className="form-section">
        <button
          type="button"
          className="advanced-toggle"
          onClick={() => setShowCacheSettings(!showCacheSettings)}
          title="Configure caching settings for this index"
        >
          {showCacheSettings ? '▼' : '▶'} Caching Configuration
        </button>

        {showCacheSettings && (
          <div className="advanced-options">
            <div className="form-group form-group-checkbox">
              <label>
                <input
                  type="checkbox"
                  name="enabled"
                  checked={cacheConfig.enabled}
                  onChange={handleCacheChange}
                  title="Enable or disable all caching for this index"
                />
                Enable Caching
              </label>
              <span className="form-hint">Master switch to enable or disable all caching for this index</span>
            </div>

            {cacheConfig.enabled && (
              <>
                {/* Term Cache Settings */}
                <div className="cache-subsection">
                  <h5>Term Cache</h5>
                  <div className="form-group form-group-checkbox">
                    <label>
                      <input
                        type="checkbox"
                        name="enableTermCache"
                        checked={cacheConfig.enableTermCache}
                        onChange={handleCacheChange}
                        title="Cache term records for faster search"
                      />
                      Enable Term Cache
                    </label>
                    <span className="form-hint">Cache term records for faster search operations</span>
                  </div>
                  {cacheConfig.enableTermCache && (
                    <div className="form-row">
                      <div className="form-group">
                        <label htmlFor="termCacheCapacity">Capacity</label>
                        <input
                          type="number"
                          id="termCacheCapacity"
                          name="termCacheCapacity"
                          value={cacheConfig.termCacheCapacity}
                          onChange={handleCacheNumberChange}
                          min={1}
                          max={100000}
                          title="Maximum number of cached term entries"
                        />
                        <span className="form-hint">Max entries (default: 10,000)</span>
                      </div>
                      <div className="form-group">
                        <label htmlFor="termCacheTtlSeconds">TTL (seconds)</label>
                        <input
                          type="number"
                          id="termCacheTtlSeconds"
                          name="termCacheTtlSeconds"
                          value={cacheConfig.termCacheTtlSeconds}
                          onChange={handleCacheNumberChange}
                          min={1}
                          max={86400}
                          title="Time-to-live for cached terms in seconds"
                        />
                        <span className="form-hint">Time-to-live (default: 300)</span>
                      </div>
                    </div>
                  )}
                </div>

                {/* Document Cache Settings */}
                <div className="cache-subsection">
                  <h5>Document Cache</h5>
                  <div className="form-group form-group-checkbox">
                    <label>
                      <input
                        type="checkbox"
                        name="enableDocumentCache"
                        checked={cacheConfig.enableDocumentCache}
                        onChange={handleCacheChange}
                        title="Cache document metadata for faster retrieval"
                      />
                      Enable Document Cache
                    </label>
                    <span className="form-hint">Cache document metadata for faster retrieval</span>
                  </div>
                  {cacheConfig.enableDocumentCache && (
                    <div className="form-row">
                      <div className="form-group">
                        <label htmlFor="documentCacheCapacity">Capacity</label>
                        <input
                          type="number"
                          id="documentCacheCapacity"
                          name="documentCacheCapacity"
                          value={cacheConfig.documentCacheCapacity}
                          onChange={handleCacheNumberChange}
                          min={1}
                          max={100000}
                          title="Maximum number of cached document entries"
                        />
                        <span className="form-hint">Max entries (default: 5,000)</span>
                      </div>
                      <div className="form-group">
                        <label htmlFor="documentCacheTtlSeconds">TTL (seconds)</label>
                        <input
                          type="number"
                          id="documentCacheTtlSeconds"
                          name="documentCacheTtlSeconds"
                          value={cacheConfig.documentCacheTtlSeconds}
                          onChange={handleCacheNumberChange}
                          min={1}
                          max={86400}
                          title="Time-to-live for cached documents in seconds"
                        />
                        <span className="form-hint">Time-to-live (default: 600)</span>
                      </div>
                    </div>
                  )}
                </div>

                {/* Statistics Cache Settings */}
                <div className="cache-subsection">
                  <h5>Statistics Cache</h5>
                  <div className="form-group form-group-checkbox">
                    <label>
                      <input
                        type="checkbox"
                        name="enableStatisticsCache"
                        checked={cacheConfig.enableStatisticsCache}
                        onChange={handleCacheChange}
                        title="Cache index statistics"
                      />
                      Enable Statistics Cache
                    </label>
                    <span className="form-hint">Cache index statistics to reduce compute overhead</span>
                  </div>
                  {cacheConfig.enableStatisticsCache && (
                    <div className="form-row">
                      <div className="form-group">
                        <label htmlFor="statisticsCacheTtlSeconds">TTL (seconds)</label>
                        <input
                          type="number"
                          id="statisticsCacheTtlSeconds"
                          name="statisticsCacheTtlSeconds"
                          value={cacheConfig.statisticsCacheTtlSeconds}
                          onChange={handleCacheNumberChange}
                          min={1}
                          max={3600}
                          title="Time-to-live for cached statistics in seconds"
                        />
                        <span className="form-hint">Time-to-live (default: 60)</span>
                      </div>
                    </div>
                  )}
                </div>
              </>
            )}
          </div>
        )}
      </div>

      {error && <div className="form-error">{error}</div>}

      <div className="form-actions">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onCancel}
          disabled={isSubmitting}
          title="Cancel index creation"
        >
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isSubmitting}
          title="Create the new index"
        >
          {isSubmitting ? 'Creating...' : 'Create Index'}
        </button>
      </div>
    </form>
  );
}

export default IndexForm;
