import { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import './ActionMenu.css';

function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false);
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0 });
  const menuRef = useRef(null);
  const triggerRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target) &&
          triggerRef.current && !triggerRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    };

    const handleScroll = () => {
      if (isOpen) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('scroll', handleScroll, true);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('scroll', handleScroll, true);
    };
  }, [isOpen]);

  const handleToggle = () => {
    if (!isOpen && triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect();
      setDropdownPosition({
        top: rect.bottom + 4,
        left: rect.right - 160 // 160 is min-width of dropdown
      });
    }
    setIsOpen(!isOpen);
  };

  const handleActionClick = (action) => {
    setIsOpen(false);
    if (action.onClick) {
      action.onClick();
    }
  };

  return (
    <div className="action-menu">
      <button
        ref={triggerRef}
        className="action-menu-trigger btn btn-sm btn-secondary"
        onClick={handleToggle}
        aria-haspopup="true"
        aria-expanded={isOpen}
        title="Actions menu"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
          <circle cx="12" cy="5" r="2" />
          <circle cx="12" cy="12" r="2" />
          <circle cx="12" cy="19" r="2" />
        </svg>
      </button>
      {isOpen && createPortal(
        <div
          ref={menuRef}
          className="action-menu-dropdown"
          style={{ top: dropdownPosition.top, left: dropdownPosition.left }}
        >
          {actions.map((action, index) => (
            <button
              key={index}
              className={`action-menu-item ${action.variant === 'danger' ? 'danger' : ''}`}
              onClick={() => handleActionClick(action)}
              disabled={action.disabled}
              title={action.label}
            >
              {action.icon && <span className="action-menu-icon">{action.icon}</span>}
              <span className="action-menu-label">{action.label}</span>
            </button>
          ))}
        </div>,
        document.body
      )}
    </div>
  );
}

export default ActionMenu;
