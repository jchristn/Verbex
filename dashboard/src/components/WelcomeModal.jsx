import { useEffect } from 'react';
import { useOnboarding } from '../context/OnboardingContext';
import './WelcomeModal.css';

function WelcomeModal() {
  const { showWelcome, startTour, skipWelcome } = useOnboarding();

  useEffect(() => {
    const handleEscape = (e) => {
      if (e.key === 'Escape' && showWelcome) {
        skipWelcome();
      }
    };

    if (showWelcome) {
      document.addEventListener('keydown', handleEscape);
      document.body.style.overflow = 'hidden';
    }

    return () => {
      document.removeEventListener('keydown', handleEscape);
      if (showWelcome) {
        document.body.style.overflow = '';
      }
    };
  }, [showWelcome, skipWelcome]);

  if (!showWelcome) return null;

  return (
    <div className="welcome-overlay" onClick={skipWelcome}>
      <div className="welcome-modal" onClick={(e) => e.stopPropagation()}>
        <div className="welcome-header">
          <img src="/logo.png" alt="Verbex" className="welcome-logo" />
          <h2 className="welcome-title">Welcome to Verbex</h2>
          <p className="welcome-subtitle">
            Your full-text search management dashboard
          </p>
        </div>

        <div className="welcome-body">
          <ul className="welcome-features">
            <li className="welcome-feature">
              <span className="welcome-feature-icon">&#128218;</span>
              <div className="welcome-feature-text">
                <div className="welcome-feature-title">Create Indices</div>
                <div className="welcome-feature-desc">
                  Build searchable collections with configurable tokenization, lemmatization, and stop word settings.
                </div>
              </div>
            </li>
            <li className="welcome-feature">
              <span className="welcome-feature-icon">&#128196;</span>
              <div className="welcome-feature-text">
                <div className="welcome-feature-title">Manage Documents</div>
                <div className="welcome-feature-desc">
                  Add, view, and organize documents with labels, tags, and metadata.
                </div>
              </div>
            </li>
            <li className="welcome-feature">
              <span className="welcome-feature-icon">&#128269;</span>
              <div className="welcome-feature-text">
                <div className="welcome-feature-title">Search &amp; Analyze</div>
                <div className="welcome-feature-desc">
                  Full-text search with TF-IDF/BM25 scoring, AND/OR logic, and metadata filtering.
                </div>
              </div>
            </li>
          </ul>
        </div>

        <div className="welcome-footer">
          <button className="btn btn-secondary" onClick={skipWelcome}>
            Skip
          </button>
          <button className="btn btn-primary" onClick={startTour}>
            Start Tour
          </button>
        </div>
      </div>
    </div>
  );
}

export default WelcomeModal;
