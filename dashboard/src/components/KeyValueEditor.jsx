import { useState } from 'react';
import './KeyValueEditor.css';

function KeyValueEditor({ value = {}, onChange, keyPlaceholder = 'Key', valuePlaceholder = 'Value' }) {
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');

  const entries = Object.entries(value);

  const handleAdd = () => {
    const key = newKey.trim();
    const val = newValue.trim();
    if (key && val) {
      onChange({ ...value, [key]: val });
      setNewKey('');
      setNewValue('');
    }
  };

  const handleRemove = (keyToRemove) => {
    const newObj = { ...value };
    delete newObj[keyToRemove];
    onChange(newObj);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAdd();
    }
  };

  return (
    <div className="kv-editor">
      {entries.length > 0 && (
        <div className="kv-list">
          {entries.map(([k, v]) => (
            <div key={k} className="kv-item">
              <span className="kv-key">{k}</span>
              <span className="kv-separator">=</span>
              <span className="kv-value">{v}</span>
              <button
                type="button"
                className="kv-remove"
                onClick={() => handleRemove(k)}
                aria-label={`Remove ${k}`}
              >
                &times;
              </button>
            </div>
          ))}
        </div>
      )}
      <div className="kv-add-row">
        <input
          type="text"
          value={newKey}
          onChange={(e) => setNewKey(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={keyPlaceholder}
          className="kv-input kv-key-input"
        />
        <input
          type="text"
          value={newValue}
          onChange={(e) => setNewValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={valuePlaceholder}
          className="kv-input kv-value-input"
        />
        <button
          type="button"
          className="kv-add-btn"
          onClick={handleAdd}
          disabled={!newKey.trim() || !newValue.trim()}
        >
          Add
        </button>
      </div>
      <span className="kv-hint">Add key-value pairs for custom metadata</span>
    </div>
  );
}

export default KeyValueEditor;
