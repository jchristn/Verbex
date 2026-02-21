import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useAuth } from './AuthContext';

const OnboardingContext = createContext(null);

const TOUR_COMPLETED_KEY = 'verbex_tour_completed';
const WIZARD_COMPLETED_KEY = 'verbex_wizard_completed';

export function OnboardingProvider({ children }) {
  const { isAuthenticated } = useAuth();

  const [showWelcome, setShowWelcome] = useState(false);
  const [tourActive, setTourActive] = useState(false);
  const [tourStep, setTourStep] = useState(0);
  const [wizardActive, setWizardActive] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
  const [tourCompleted, setTourCompleted] = useState(
    () => localStorage.getItem(TOUR_COMPLETED_KEY) === 'true'
  );
  const [wizardCompleted, setWizardCompleted] = useState(
    () => localStorage.getItem(WIZARD_COMPLETED_KEY) === 'true'
  );

  // Show welcome modal on first authenticated visit
  useEffect(() => {
    if (isAuthenticated && !tourCompleted) {
      setShowWelcome(true);
    }
  }, [isAuthenticated, tourCompleted]);

  const dismissWelcome = useCallback(() => {
    setShowWelcome(false);
  }, []);

  const startTour = useCallback(() => {
    setShowWelcome(false);
    setTourStep(0);
    setTourActive(true);
  }, []);

  const nextTourStep = useCallback((maxSteps) => {
    setTourStep((prev) => {
      if (prev + 1 >= maxSteps) {
        return prev;
      }
      return prev + 1;
    });
  }, []);

  const prevTourStep = useCallback(() => {
    setTourStep((prev) => Math.max(0, prev - 1));
  }, []);

  const endTour = useCallback((completed = true) => {
    setTourActive(false);
    setTourStep(0);
    if (completed) {
      setTourCompleted(true);
      localStorage.setItem(TOUR_COMPLETED_KEY, 'true');
      if (!wizardCompleted) {
        setWizardActive(true);
        setWizardStep(0);
      }
    }
  }, [wizardCompleted]);

  const startWizard = useCallback(() => {
    setWizardStep(0);
    setWizardActive(true);
  }, []);

  const nextWizardStep = useCallback(() => {
    setWizardStep((prev) => prev + 1);
  }, []);

  const prevWizardStep = useCallback(() => {
    setWizardStep((prev) => Math.max(0, prev - 1));
  }, []);

  const endWizard = useCallback((completed = true) => {
    setWizardActive(false);
    setWizardStep(0);
    if (completed) {
      setWizardCompleted(true);
      localStorage.setItem(WIZARD_COMPLETED_KEY, 'true');
    }
  }, []);

  const skipWelcome = useCallback(() => {
    setShowWelcome(false);
    setTourCompleted(true);
    localStorage.setItem(TOUR_COMPLETED_KEY, 'true');
    if (!wizardCompleted) {
      setWizardActive(true);
      setWizardStep(0);
    }
  }, [wizardCompleted]);

  const value = {
    showWelcome,
    tourActive,
    tourStep,
    wizardActive,
    wizardStep,
    tourCompleted,
    wizardCompleted,
    dismissWelcome,
    startTour,
    nextTourStep,
    prevTourStep,
    endTour,
    startWizard,
    nextWizardStep,
    prevWizardStep,
    endWizard,
    skipWelcome
  };

  return (
    <OnboardingContext.Provider value={value}>
      {children}
    </OnboardingContext.Provider>
  );
}

export function useOnboarding() {
  const context = useContext(OnboardingContext);
  if (!context) {
    throw new Error('useOnboarding must be used within an OnboardingProvider');
  }
  return context;
}

export default OnboardingContext;
