import Modal from './Modal';
import './ConfirmModal.css';

const ConfirmModal = ({
  isOpen,
  onClose,
  onConfirm,
  title = 'Confirm Action',
  message,
  entityName,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  warningMessage,
  variant = 'danger', // 'danger', 'warning', 'info'
  isLoading = false
}) => {
  const getIcon = () => {
    if (variant === 'warning') {
      return (
        <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
          <line x1="12" y1="9" x2="12" y2="13"></line>
          <line x1="12" y1="17" x2="12.01" y2="17"></line>
        </svg>
      );
    }
    if (variant === 'info') {
      return (
        <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="12" y1="16" x2="12" y2="12"></line>
          <line x1="12" y1="8" x2="12.01" y2="8"></line>
        </svg>
      );
    }
    // danger (default)
    return (
      <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="10"></circle>
        <line x1="12" y1="8" x2="12" y2="12"></line>
        <line x1="12" y1="16" x2="12.01" y2="16"></line>
      </svg>
    );
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title}>
      <div className="confirm-modal">
        <div className={`confirm-icon confirm-icon-${variant}`}>
          {getIcon()}
        </div>
        <p className="confirm-message">{message}</p>
        {entityName && (
          <p className="confirm-entity">{entityName}</p>
        )}
        {warningMessage && (
          <p className="confirm-warning">{warningMessage}</p>
        )}
        <div className="confirm-actions">
          <button
            className="btn btn-secondary"
            onClick={onClose}
            disabled={isLoading}
            title="Cancel this action"
          >
            {cancelLabel}
          </button>
          <button
            className={`btn btn-confirm btn-confirm-${variant}`}
            onClick={onConfirm}
            disabled={isLoading}
            title={confirmLabel}
          >
            {isLoading ? 'Processing...' : confirmLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default ConfirmModal;
