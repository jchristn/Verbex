import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './IndexForm.css';

function IndexForm({ onSuccess, onCancel }) {
  const { apiClient } = useAuth();
  const [isAdvanced, setIsAdvanced] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [labels, setLabels] = useState([]);
  const [tags, setTags] = useState({});

  const [formData, setFormData] = useState({
    id: '',
    name: '',
    description: '',
    storageMode: 'OnDisk',
    enableLemmatizer: true,
    enableStopWordRemover: true,
    minTokenLength: 2,
    maxTokenLength: 50,
    hotCacheSize: 10000,
    documentCacheSize: 1000,
    expectedTerms: 1000000
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
      const isInMemory = formData.storageMode === 'InMemory';
      const indexConfig = {
        id: formData.id,
        name: formData.name,
        description: formData.description || undefined,
        storageMode: formData.storageMode,
        inMemory: isInMemory,
        enableLemmatizer: formData.enableLemmatizer,
        enableStopWordRemover: formData.enableStopWordRemover,
        minTokenLength: formData.minTokenLength,
        maxTokenLength: formData.maxTokenLength
      };

      if (isAdvanced) {
        indexConfig.hotCacheSize = formData.hotCacheSize;
        indexConfig.documentCacheSize = formData.documentCacheSize;
        indexConfig.expectedTerms = formData.expectedTerms;
      }

      if (labels.length > 0) {
        indexConfig.labels = labels;
      }
      if (Object.keys(tags).length > 0) {
        indexConfig.tags = tags;
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

        <div className="form-group">
          <label htmlFor="id">Index ID *</label>
          <input
            type="text"
            id="id"
            name="id"
            value={formData.id}
            onChange={handleChange}
            placeholder="my-index"
            required
            pattern="[a-zA-Z0-9_-]+"
            title="Only alphanumeric characters, hyphens, and underscores"
          />
          <span className="form-hint">Unique identifier for the index</span>
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
      </div>

      <div className="form-section">
        <div className="form-group">
          <label htmlFor="storageMode">Storage Mode</label>
          <select
            id="storageMode"
            name="storageMode"
            value={formData.storageMode}
            onChange={handleChange}
          >
            <option value="InMemory">In-Memory (Fastest, No Persistence)</option>
            <option value="OnDisk">On-Disk (Persistent)</option>
          </select>
          <span className="form-hint">Determines how the index data is stored</span>
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
            <span className="form-hint">Reduces words to base forms (e.g., "running" → "run")</span>
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

      <div className="form-section">
        <button
          type="button"
          className="advanced-toggle"
          onClick={() => setIsAdvanced(!isAdvanced)}
        >
          {isAdvanced ? '▼' : '▶'} Advanced Options
        </button>

        {isAdvanced && (
          <div className="advanced-options">
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="hotCacheSize">Hot Cache Size</label>
                <input
                  type="number"
                  id="hotCacheSize"
                  name="hotCacheSize"
                  value={formData.hotCacheSize}
                  onChange={handleNumberChange}
                  min={100}
                />
                <span className="form-hint">Frequently accessed terms cache</span>
              </div>

              <div className="form-group">
                <label htmlFor="documentCacheSize">Document Cache Size</label>
                <input
                  type="number"
                  id="documentCacheSize"
                  name="documentCacheSize"
                  value={formData.documentCacheSize}
                  onChange={handleNumberChange}
                  min={100}
                />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="expectedTerms">Expected Terms</label>
              <input
                type="number"
                id="expectedTerms"
                name="expectedTerms"
                value={formData.expectedTerms}
                onChange={handleNumberChange}
                min={1000}
              />
              <span className="form-hint">Expected unique terms for bloom filter sizing</span>
            </div>
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
