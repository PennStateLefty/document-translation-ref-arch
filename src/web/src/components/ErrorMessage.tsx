import React from 'react';

interface ErrorMessageProps {
  message: string;
  details?: string[];
  onDismiss?: () => void;
}

const ErrorMessage: React.FC<ErrorMessageProps> = ({ message, details, onDismiss }) => {
  return (
    <div
      className="error-message"
      style={{
        backgroundColor: '#fdecea',
        border: '1px solid #f5c6cb',
        borderRadius: '8px',
        padding: '16px',
        marginTop: '16px',
        color: '#721c24',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <p style={{ margin: 0, fontWeight: 'bold' }}>❌ {message}</p>
        {onDismiss && (
          <button
            onClick={onDismiss}
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              fontSize: '1.2em',
              color: '#721c24',
            }}
            aria-label="Dismiss error"
          >
            ✕
          </button>
        )}
      </div>
      {details && details.length > 0 && (
        <ul style={{ marginTop: '8px', paddingLeft: '20px' }}>
          {details.map((detail, index) => (
            <li key={index} style={{ marginBottom: '4px' }}>
              {detail}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

export default ErrorMessage;
