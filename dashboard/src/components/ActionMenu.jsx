import { useState, useRef, useEffect, useLayoutEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import './ActionMenu.css';

function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false);
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0 });
  const menuRef = useRef(null);
  const triggerRef = useRef(null);

  const updateDropdownPosition = useCallback(() => {
    if (!triggerRef.current) return;

    const rect = triggerRef.current.getBoundingClientRect();
    const menuWidth = menuRef.current?.offsetWidth || 160;
    const menuHeight = menuRef.current?.offsetHeight || Math.max(actions.length * 40, 40);
    const viewportPadding = 8;
    const triggerGap = 4;
    const maxLeft = Math.max(viewportPadding, window.innerWidth - menuWidth - viewportPadding);
    const maxTop = Math.max(viewportPadding, window.innerHeight - menuHeight - viewportPadding);
    let left = rect.right - menuWidth;
    let top = rect.bottom + triggerGap;

    if (top + menuHeight > window.innerHeight - viewportPadding) {
      top = rect.top - menuHeight - triggerGap;
    }

    setDropdownPosition({
      top: Math.min(Math.max(viewportPadding, top), maxTop),
      left: Math.min(Math.max(viewportPadding, left), maxLeft)
    });
  }, [actions.length]);

  useLayoutEffect(() => {
    if (isOpen) {
      updateDropdownPosition();
    }
  }, [isOpen, updateDropdownPosition]);

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
      window.addEventListener('resize', updateDropdownPosition);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('scroll', handleScroll, true);
      window.removeEventListener('resize', updateDropdownPosition);
    };
  }, [isOpen, updateDropdownPosition]);

  const handleToggle = () => {
    setIsOpen((current) => !current);
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
