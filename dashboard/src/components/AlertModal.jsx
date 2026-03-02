import Modal from './Modal';
import './AlertModal.css';

const AlertModal = ({
  isOpen,
  onClose,
  title = 'Alert',
  message,
  variant = 'error', // 'error', 'success', 'warning', 'info'
  buttonLabel = 'OK'
}) => {
  const getIcon = () => {
    if (variant === 'success') {
      return (
        <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="10"></circle>
          <path d="M9 12l2 2 4-4"></path>
        </svg>
      );
    }
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
    // error (default)
    return (
      <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="10"></circle>
        <line x1="15" y1="9" x2="9" y2="15"></line>
        <line x1="9" y1="9" x2="15" y2="15"></line>
      </svg>
    );
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title}>
      <div className="alert-modal">
        <div className={`alert-icon alert-icon-${variant}`}>
          {getIcon()}
        </div>
        <p className="alert-message">{message}</p>
        <div className="alert-actions">
          <button
            className={`btn btn-alert btn-alert-${variant}`}
            onClick={onClose}
            title="Dismiss this alert"
          >
            {buttonLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default AlertModal;
