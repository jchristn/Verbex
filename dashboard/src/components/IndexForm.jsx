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
    name: '',
    description: '',
    inMemory: false,
    enableLemmatizer: true,
    enableStopWordRemover: true,
    minTokenLength: 2,
    maxTokenLength: 50
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
          <label htmlFor="name">Display Name *</label>
          <input
            type="text"
            id="name"
            name="name"
            value={formData.name}
            onChange={handleChange}
            placeholder="My Index"
            required
          />
          <span className="form-hint">A unique identifier will be generated automatically</span>
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
            />
          </div>
        </div>
      </div>

      {error && <div className="form-error">{error}</div>}

      <div className="form-actions">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onCancel}
          disabled={isSubmitting}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Creating...' : 'Create Index'}
        </button>
      </div>
    </form>
  );
}

export default IndexForm;
