import { useState, useEffect } from 'react';
import './JsonEditor.css';

function JsonEditor({ value, onChange, placeholder = 'Enter JSON...', label = 'Custom Metadata' }) {
  const [text, setText] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (value !== undefined && value !== null) {
      setText(JSON.stringify(value, null, 2));
    } else {
      setText('');
    }
  }, [value]);

  const handleChange = (e) => {
    const newText = e.target.value;
    setText(newText);

    if (!newText.trim()) {
      setError('');
      onChange(null);
      return;
    }

    try {
      const parsed = JSON.parse(newText);
      setError('');
      onChange(parsed);
    } catch (err) {
      setError('Invalid JSON');
    }
  };

  return (
    <div className="json-editor">
      {label && <label className="json-editor-label">{label}</label>}
      <textarea
        value={text}
        onChange={handleChange}
        placeholder={placeholder}
        className={`json-editor-textarea ${error ? 'json-error' : ''}`}
        rows={4}
        title="Enter valid JSON"
      />
      {error && <span className="json-error-message">{error}</span>}
      <span className="json-hint">Any valid JSON value (object, array, string, number, boolean, null)</span>
    </div>
  );
}

export default JsonEditor;
