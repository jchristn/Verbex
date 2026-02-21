import { useState, useEffect, useCallback, useRef } from 'react';
import { useOnboarding } from '../context/OnboardingContext';
import { useAuth } from '../context/AuthContext';
import './Tour.css';

function getTourSteps(isAdmin) {
  const steps = [
    {
      target: 'topbar-logo',
      title: 'Verbex Dashboard',
      description: 'This is your central hub for managing full-text search indices, documents, and queries.',
      position: 'bottom'
    },
    {
      target: 'topbar-server',
      title: 'Server Connection',
      description: 'Shows the Verbex server you\'re connected to \u2014 all operations go through this endpoint.',
      position: 'bottom'
    },
    {
      target: 'topbar-theme',
      title: 'Theme Toggle',
      description: 'Switch between light and dark mode. Your preference is saved automatically.',
      position: 'bottom-left'
    },
    {
      target: 'topbar-logout',
      title: 'Logout',
      description: 'Securely end your session and return to the login screen.',
      position: 'bottom-left'
    },
    {
      target: 'nav-indices',
      title: 'Indices',
      description: 'Core of Verbex \u2014 each index is a self-contained search collection with its own tokenization, lemmatization, and stop word settings.',
      position: 'right'
    },
    {
      target: 'nav-documents',
      title: 'Documents',
      description: 'Add, view, and manage documents within indices. Each document has searchable text plus optional labels, tags, and metadata.',
      position: 'right'
    },
    {
      target: 'nav-search',
      title: 'Search',
      description: 'Full-text search with TF-IDF/BM25 scoring, AND/OR logic, and label/tag filtering.',
      position: 'right'
    }
  ];

  if (isAdmin) {
    steps.push({
      target: 'nav-tenants',
      title: 'Administration',
      description: 'Tenant, user, and credential management for multi-tenant isolation and API access control.',
      position: 'right'
    });
  }

  // Final step with no target (centered)
  steps.push({
    target: null,
    title: 'You\'re All Set!',
    description: 'You now know your way around the dashboard. Try the Setup Wizard next to create your first index, add a document, and run a search.',
    position: 'center'
  });

  return steps;
}

function Tour() {
  const { tourActive, tourStep, nextTourStep, prevTourStep, endTour } = useOnboarding();
  const { userInfo } = useAuth();
  const [spotlightRect, setSpotlightRect] = useState(null);
  const observerRef = useRef(null);
  const isAdmin = userInfo?.isAdmin || userInfo?.isGlobalAdmin || false;
  const steps = getTourSteps(isAdmin);
  const totalSteps = steps.length;
  const currentStep = steps[tourStep];

  const updateSpotlight = useCallback(() => {
    if (!currentStep || !currentStep.target) {
      setSpotlightRect(null);
      return;
    }

    const element = document.querySelector(`[data-tour-id="${currentStep.target}"]`);
    if (element) {
      const rect = element.getBoundingClientRect();
      const padding = 6;
      setSpotlightRect({
        top: rect.top - padding,
        left: rect.left - padding,
        width: rect.width + padding * 2,
        height: rect.height + padding * 2
      });
    } else {
      setSpotlightRect(null);
    }
  }, [currentStep]);

  // Update spotlight position on step change and layout changes
  useEffect(() => {
    if (!tourActive) return;

    updateSpotlight();

    observerRef.current = new ResizeObserver(() => {
      updateSpotlight();
    });
    observerRef.current.observe(document.body);

    window.addEventListener('resize', updateSpotlight);

    return () => {
      if (observerRef.current) {
        observerRef.current.disconnect();
      }
      window.removeEventListener('resize', updateSpotlight);
    };
  }, [tourActive, tourStep, updateSpotlight]);

  // Escape key handler
  useEffect(() => {
    if (!tourActive) return;

    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        endTour(true);
      } else if (e.key === 'ArrowRight' || e.key === 'Enter') {
        if (tourStep < totalSteps - 1) {
          nextTourStep(totalSteps);
        } else {
          endTour(true);
        }
      } else if (e.key === 'ArrowLeft') {
        prevTourStep();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [tourActive, tourStep, totalSteps, nextTourStep, prevTourStep, endTour]);

  if (!tourActive || !currentStep) return null;

  const getTooltipStyle = () => {
    if (currentStep.position === 'center' || !spotlightRect) {
      return null; // Use centered class instead
    }

    const style = {};
    const gap = 16;

    if (currentStep.position === 'bottom' || currentStep.position === 'bottom-left') {
      style.top = spotlightRect.top + spotlightRect.height + gap;
      if (currentStep.position === 'bottom-left') {
        style.left = Math.max(8, spotlightRect.left + spotlightRect.width - 320);
      } else {
        style.left = Math.max(8, spotlightRect.left);
      }
    } else if (currentStep.position === 'right') {
      style.top = spotlightRect.top;
      style.left = spotlightRect.left + spotlightRect.width + gap;
    }

    // Keep tooltip on screen
    if (style.left + 320 > window.innerWidth) {
      style.left = window.innerWidth - 328;
    }
    if (style.top + 200 > window.innerHeight) {
      style.top = window.innerHeight - 208;
    }

    return style;
  };

  const isCentered = currentStep.position === 'center' || !spotlightRect;
  const isLastStep = tourStep === totalSteps - 1;

  return (
    <div className="tour-overlay">
      <div className="tour-backdrop" onClick={() => endTour(true)} />

      {spotlightRect && (
        <div
          className="tour-spotlight"
          style={{
            top: spotlightRect.top,
            left: spotlightRect.left,
            width: spotlightRect.width,
            height: spotlightRect.height
          }}
        />
      )}

      {!spotlightRect && currentStep.position !== 'center' ? null : (
        <div
          className={isCentered ? 'tour-tooltip-center' : 'tour-tooltip'}
          style={isCentered ? undefined : getTooltipStyle()}
        >
          {!isCentered && (
            <div className={`tour-tooltip-arrow ${
              currentStep.position === 'right' ? 'tour-tooltip-arrow-left' : 'tour-tooltip-arrow-top'
            }`} />
          )}

          {isCentered && isLastStep && (
            <div className="tour-finish-icon">&#9989;</div>
          )}

          <h4 className="tour-tooltip-title">{currentStep.title}</h4>
          <p className="tour-tooltip-desc">{currentStep.description}</p>

          <div className="tour-tooltip-footer">
            <div className="tour-step-indicators">
              {steps.map((_, index) => (
                <div
                  key={index}
                  className={`tour-step-dot ${index === tourStep ? 'active' : ''}`}
                />
              ))}
            </div>

            <div className="tour-tooltip-actions">
              {tourStep > 0 && (
                <button className="btn btn-secondary" onClick={prevTourStep}>
                  Back
                </button>
              )}
              {isLastStep ? (
                <button className="btn btn-primary" onClick={() => endTour(true)}>
                  Done
                </button>
              ) : (
                <button className="btn btn-primary" onClick={() => nextTourStep(totalSteps)}>
                  Next
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default Tour;
