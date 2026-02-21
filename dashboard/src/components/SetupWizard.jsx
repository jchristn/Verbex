import { useState, useCallback } from 'react';
import { useOnboarding } from '../context/OnboardingContext';
import { useAuth } from '../context/AuthContext';
import Modal from './Modal';
import './SetupWizard.css';

const SAMPLE_CONTENT = `Full-text search is a technique for searching through the entire content of documents rather than just metadata or titles. Modern search engines use inverted indices to map terms to the documents that contain them, enabling fast lookup even across millions of records. Key concepts include tokenization (breaking text into individual terms), lemmatization (reducing words to their root form), and stop word removal (filtering out common words like "the" and "is"). Scoring algorithms such as TF-IDF and BM25 rank results by relevance, considering how frequently a term appears in a document versus the entire corpus.`;

const PROGRESS_LABELS = ['Intro', 'Index', 'Document', 'Search', 'Done'];

function SetupWizard({ onNavigate }) {
  const { wizardActive, wizardStep, nextWizardStep, prevWizardStep, endWizard } = useOnboarding();
  const { apiClient, userInfo } = useAuth();

  // Form state
  const [indexName, setIndexName] = useState('My First Index');
  const [indexDesc, setIndexDesc] = useState('');
  const [enableLemmatizer, setEnableLemmatizer] = useState(false);
  const [removeStopWords, setRemoveStopWords] = useState(false);
  const [docContent, setDocContent] = useState(SAMPLE_CONTENT);
  const [searchQuery, setSearchQuery] = useState('search');

  // Result state
  const [createdIndexId, setCreatedIndexId] = useState(null);
  const [searchResults, setSearchResults] = useState(null);

  // UI state
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState('');

  const handleCreateIndex = useCallback(async () => {
    if (!indexName.trim()) {
      setError('Index name is required.');
      return;
    }
    setError('');
    setIsProcessing(true);
    try {
      const indexConfig = {
        name: indexName.trim(),
        description: indexDesc.trim() || undefined,
        enableLemmatizer: enableLemmatizer,
        enableStopWordRemover: removeStopWords
      };

      // Global admins must specify a tenant ID; regular users have one implicitly
      if (userInfo?.isGlobalAdmin) {
        indexConfig.tenantId = userInfo.tenantId || 'default';
      } else if (userInfo?.tenantId) {
        indexConfig.tenantId = userInfo.tenantId;
      }

      const response = await apiClient.createIndex(indexConfig);
      const indexId = response.data?.index?.identifier || response.data?.identifier || response.data?.id;
      setCreatedIndexId(indexId);
      nextWizardStep();
    } catch (err) {
      setError(err.message || 'Failed to create index.');
    } finally {
      setIsProcessing(false);
    }
  }, [apiClient, userInfo, indexName, indexDesc, enableLemmatizer, removeStopWords, nextWizardStep]);

  const handleAddDocument = useCallback(async () => {
    if (!docContent.trim()) {
      setError('Document content is required.');
      return;
    }
    if (!createdIndexId) {
      setError('No index found. Please go back and create an index first.');
      return;
    }
    setError('');
    setIsProcessing(true);
    try {
      await apiClient.addDocument(createdIndexId, {
        content: docContent.trim()
      });
      nextWizardStep();
    } catch (err) {
      setError(err.message || 'Failed to add document.');
    } finally {
      setIsProcessing(false);
    }
  }, [apiClient, createdIndexId, docContent, nextWizardStep]);

  const handleSearch = useCallback(async () => {
    if (!searchQuery.trim()) {
      setError('Search query is required.');
      return;
    }
    if (!createdIndexId) {
      setError('No index found. Please go back and create an index first.');
      return;
    }
    setError('');
    setIsProcessing(true);
    try {
      const response = await apiClient.search(createdIndexId, searchQuery.trim(), 10);
      setSearchResults(response.data?.results || []);
    } catch (err) {
      setError(err.message || 'Search failed.');
    } finally {
      setIsProcessing(false);
    }
  }, [apiClient, createdIndexId, searchQuery]);

  const handleClose = useCallback(() => {
    endWizard(wizardStep >= 4);
  }, [endWizard, wizardStep]);

  const handleGoToIndices = useCallback(() => {
    endWizard(true);
    if (onNavigate) {
      onNavigate('indices');
    }
  }, [endWizard, onNavigate]);

  if (!wizardActive) return null;

  const renderProgressBar = () => (
    <div className="wizard-progress">
      {PROGRESS_LABELS.map((label, index) => (
        <div key={label} style={{ display: 'flex', alignItems: 'flex-start' }}>
          <div className="wizard-progress-step">
            <div className={`wizard-progress-circle ${
              index < wizardStep ? 'completed' : index === wizardStep ? 'active' : ''
            }`}>
              {index < wizardStep ? '\u2713' : index + 1}
            </div>
            <span className={`wizard-progress-label ${index === wizardStep ? 'active' : ''}`}>
              {label}
            </span>
          </div>
          {index < PROGRESS_LABELS.length - 1 && (
            <div className={`wizard-progress-line ${index < wizardStep ? 'completed' : ''}`} />
          )}
        </div>
      ))}
    </div>
  );

  const renderStep = () => {
    switch (wizardStep) {
      case 0:
        return renderIntroStep();
      case 1:
        return renderCreateIndexStep();
      case 2:
        return renderAddDocumentStep();
      case 3:
        return renderSearchStep();
      case 4:
        return renderCompleteStep();
      default:
        return null;
    }
  };

  const renderIntroStep = () => (
    <div className="wizard-content">
      <p className="wizard-intro-text">
        This wizard walks you through the core Verbex workflow: creating a search index,
        adding a document, and running your first search query.
      </p>
      <ul className="wizard-step-list">
        <li className="wizard-step-item">
          <span className="wizard-step-number">1</span>
          <span className="wizard-step-text">Create a search index with custom settings</span>
        </li>
        <li className="wizard-step-item">
          <span className="wizard-step-number">2</span>
          <span className="wizard-step-text">Add a document with searchable content</span>
        </li>
        <li className="wizard-step-item">
          <span className="wizard-step-number">3</span>
          <span className="wizard-step-text">Search and see results ranked by relevance</span>
        </li>
      </ul>
      <div className="wizard-footer">
        <div className="wizard-footer-left">
          <button className="btn btn-secondary" onClick={handleClose}>Skip</button>
        </div>
        <div className="wizard-footer-right">
          <button className="btn btn-primary" onClick={nextWizardStep}>
            Let's Begin
          </button>
        </div>
      </div>
    </div>
  );

  const renderCreateIndexStep = () => (
    <div className="wizard-content">
      <h4 className="wizard-section-title">Create an Index</h4>
      <p className="wizard-section-desc">
        An index is a searchable collection optimized for full-text search. Verbex tokenizes
        content, applies lemmatization, and builds an inverted index for fast lookups.
      </p>

      {error && <div className="form-error">{error}</div>}

      <div className="form-group">
        <label>Index Name</label>
        <input
          type="text"
          className="form-input"
          value={indexName}
          onChange={(e) => setIndexName(e.target.value)}
          placeholder="My First Index"
        />
      </div>

      <div className="form-group">
        <label>Description (optional)</label>
        <input
          type="text"
          className="form-input"
          value={indexDesc}
          onChange={(e) => setIndexDesc(e.target.value)}
          placeholder="A test index for learning Verbex"
        />
      </div>

      <div className="wizard-checkbox">
        <input
          type="checkbox"
          id="wizard-lemmatizer"
          checked={enableLemmatizer}
          onChange={(e) => setEnableLemmatizer(e.target.checked)}
        />
        <label htmlFor="wizard-lemmatizer">Enable Lemmatizer</label>
      </div>

      <div className="wizard-checkbox">
        <input
          type="checkbox"
          id="wizard-stopwords"
          checked={removeStopWords}
          onChange={(e) => setRemoveStopWords(e.target.checked)}
        />
        <label htmlFor="wizard-stopwords">Remove Stop Words</label>
      </div>

      <div className="wizard-footer">
        <div className="wizard-footer-left">
          <button className="btn btn-secondary" onClick={prevWizardStep}>Back</button>
        </div>
        <div className="wizard-footer-right">
          <button
            className="btn btn-primary"
            onClick={handleCreateIndex}
            disabled={isProcessing}
          >
            {isProcessing ? 'Creating...' : 'Create Index'}
          </button>
        </div>
      </div>
    </div>
  );

  const renderAddDocumentStep = () => (
    <div className="wizard-content">
      <h4 className="wizard-section-title">Add a Document</h4>
      <p className="wizard-section-desc">
        Documents are the searchable content within an index. When you add a document, Verbex
        tokenizes the text, applies your index settings, and stores the processed terms.
      </p>

      {error && <div className="form-error">{error}</div>}

      <div className="form-group">
        <label>Content</label>
        <textarea
          className="form-input"
          rows={6}
          value={docContent}
          onChange={(e) => setDocContent(e.target.value)}
          placeholder="Enter document content..."
        />
        <div className="wizard-sample-text">
          Pre-filled with a sample paragraph about full-text search
        </div>
      </div>

      <div className="wizard-footer">
        <div className="wizard-footer-left">
          <button className="btn btn-secondary" onClick={prevWizardStep}>Back</button>
        </div>
        <div className="wizard-footer-right">
          <button
            className="btn btn-primary"
            onClick={handleAddDocument}
            disabled={isProcessing}
          >
            {isProcessing ? 'Adding...' : 'Add Document'}
          </button>
        </div>
      </div>
    </div>
  );

  const renderSearchStep = () => (
    <div className="wizard-content">
      <h4 className="wizard-section-title">Search</h4>
      <p className="wizard-section-desc">
        Verbex uses TF-IDF with BM25 ranking to score results by relevance. Try searching for
        words from the document you just added.
      </p>

      {error && <div className="form-error">{error}</div>}

      <div className="form-group">
        <label>Search Query</label>
        <div style={{ display: 'flex', gap: '8px' }}>
          <input
            type="text"
            className="form-input"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Enter search terms..."
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleSearch();
            }}
          />
          <button
            className="btn btn-primary"
            onClick={handleSearch}
            disabled={isProcessing}
            style={{ flexShrink: 0 }}
          >
            {isProcessing ? 'Searching...' : 'Search'}
          </button>
        </div>
      </div>

      {searchResults && searchResults.length > 0 && (
        <div className="wizard-results">
          <div className="wizard-results-header">
            {searchResults.length} result{searchResults.length !== 1 ? 's' : ''} found
          </div>
          {searchResults.map((result, index) => (
            <div key={index} className="wizard-result-row">
              <span className="wizard-result-rank">#{index + 1}</span>
              <span className="wizard-result-name">
                {result.documentId || result.name || `Document ${index + 1}`}
              </span>
              <span className="wizard-result-score">
                {typeof result.score === 'number' ? result.score.toFixed(4) : result.score}
              </span>
              {result.matchedTerms && (
                <span className="wizard-result-terms">
                  [{result.matchedTerms.join(', ')}]
                </span>
              )}
            </div>
          ))}
        </div>
      )}

      {searchResults && searchResults.length === 0 && (
        <div className="wizard-results">
          <div className="wizard-results-header">No results found. Try different search terms.</div>
        </div>
      )}

      <div className="wizard-footer">
        <div className="wizard-footer-left">
          <button className="btn btn-secondary" onClick={prevWizardStep}>Back</button>
        </div>
        <div className="wizard-footer-right">
          <button className="btn btn-primary" onClick={nextWizardStep}>
            Finish
          </button>
        </div>
      </div>
    </div>
  );

  const renderCompleteStep = () => (
    <div className="wizard-content" style={{ textAlign: 'center', padding: '20px 0' }}>
      <div className="wizard-complete-icon">&#127881;</div>
      <h3 className="wizard-complete-title">Congratulations!</h3>
      <p className="wizard-complete-text">
        You've completed the core Verbex workflow. Here's what you can explore next:
      </p>
      <ul className="wizard-next-steps">
        <li>Add more documents to your index with labels and tags</li>
        <li>Try advanced search with AND/OR logic and metadata filtering</li>
        <li>Create additional indices with different tokenization settings</li>
        <li>Explore the REST API for programmatic access</li>
      </ul>
      <div className="wizard-complete-actions">
        <button className="btn btn-secondary" onClick={() => endWizard(true)}>
          Close
        </button>
        <button className="btn btn-primary" onClick={handleGoToIndices}>
          Go to Indices
        </button>
      </div>
    </div>
  );

  return (
    <Modal
      isOpen={wizardActive}
      onClose={handleClose}
      title="Setup Wizard"
      size="large"
    >
      {renderProgressBar()}
      {renderStep()}
    </Modal>
  );
}

export default SetupWizard;
