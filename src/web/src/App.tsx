import React from 'react';
import FileUpload from './components/FileUpload';
import LanguageSelector from './components/LanguageSelector';
import ErrorMessage from './components/ErrorMessage';
import TranslationStatus from './components/TranslationStatus';
import DownloadButton from './components/DownloadButton';
import { useTranslation } from './hooks/useTranslation';
import { usePolling } from './hooks/usePolling';

const App: React.FC = () => {
  const {
    files,
    targetLanguage,
    isUploading,
    error,
    errorDetails,
    session,
    setFiles,
    setTargetLanguage,
    submitTranslation,
    reset,
    clearError,
  } = useTranslation();

  const { status: pollingStatus, error: pollingError } = usePolling(session?.sessionId ?? null);

  return (
    <div style={{ maxWidth: '800px', margin: '0 auto', padding: '40px 20px', fontFamily: 'system-ui, sans-serif' }}>
      <header style={{ marginBottom: '32px' }}>
        <h1 style={{ color: '#1a1a1a', marginBottom: '8px' }}>📄 Document Translation</h1>
        <p style={{ color: '#666' }}>
          Upload documents and translate them to your target language using Azure AI Translator.
        </p>
      </header>

      {!session ? (
        <div>
          <FileUpload onFilesSelected={setFiles} disabled={isUploading} />
          <LanguageSelector
            selectedLanguage={targetLanguage}
            onLanguageChange={setTargetLanguage}
            disabled={isUploading}
          />

          {error && <ErrorMessage message={error} details={errorDetails} onDismiss={clearError} />}

          <div style={{ marginTop: '24px' }}>
            <button
              onClick={submitTranslation}
              disabled={isUploading || files.length === 0 || !targetLanguage}
              style={{
                padding: '12px 32px',
                fontSize: '1.1em',
                backgroundColor: isUploading || files.length === 0 || !targetLanguage ? '#ccc' : '#0078d4',
                color: 'white',
                border: 'none',
                borderRadius: '6px',
                cursor: isUploading || files.length === 0 || !targetLanguage ? 'not-allowed' : 'pointer',
                transition: 'background-color 0.2s',
              }}
            >
              {isUploading ? '⏳ Uploading...' : '🚀 Start Translation'}
            </button>
          </div>
        </div>
      ) : (
        <div>
          {pollingStatus ? (
            <TranslationStatus status={pollingStatus} />
          ) : (
            <div
              style={{
                backgroundColor: '#e8f5e9',
                border: '1px solid #c8e6c9',
                borderRadius: '8px',
                padding: '20px',
                marginBottom: '16px',
              }}
            >
              <h2 style={{ margin: '0 0 8px 0', color: '#2e7d32' }}>✅ Translation Started</h2>
              <p>
                <strong>Session ID:</strong> {session.sessionId}
              </p>
              <p>
                <strong>Status:</strong> {session.status}
              </p>
            </div>
          )}

          {pollingError && <ErrorMessage message={pollingError} onDismiss={() => {}} />}

          {pollingStatus && ['Completed', 'Failed'].includes(pollingStatus.status) && pollingStatus.downloadUrl && (
            <DownloadButton sessionId={session.sessionId} />
          )}

          <button
            onClick={reset}
            style={{
              padding: '10px 24px',
              fontSize: '1em',
              backgroundColor: '#f5f5f5',
              border: '1px solid #ddd',
              borderRadius: '6px',
              cursor: 'pointer',
              marginTop: '16px',
            }}
          >
            ← Start New Translation
          </button>
        </div>
      )}
    </div>
  );
};

export default App;
