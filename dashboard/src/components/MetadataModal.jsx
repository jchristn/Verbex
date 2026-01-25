import { useState } from 'react';
import Modal from './Modal';
import './MetadataModal.css';

function MetadataModal({ isOpen, onClose, title, data, isLoading = false }) {
  const [copied, setCopied] = useState(false);

  const jsonString = JSON.stringify(data, null, 2);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(jsonString);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy to clipboard:', err);
    }
  };

  const handleClose = () => {
    setCopied(false);
    onClose();
  };

  const copyButton = data && !isLoading ? (
    <button
      className={`metadata-copy-btn ${copied ? 'copied' : ''}`}
      onClick={handleCopy}
      title={copied ? 'Copied!' : 'Copy JSON to clipboard'}
      type="button"
    >
      {copied ? (
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
  ) : null;

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="large" headerAction={copyButton}>
      <div className="metadata-modal">
        {isLoading ? (
          <div className="metadata-loading">Loading metadata...</div>
        ) : (
          <div className="metadata-content">
            <pre className="metadata-json">{jsonString}</pre>
          </div>
        )}
      </div>
    </Modal>
  );
}

export default MetadataModal;
