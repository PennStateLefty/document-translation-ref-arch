import React from 'react';
import { getDownloadUrl } from '../services/apiClient';

interface DownloadButtonProps {
  sessionId: string;
}

const DownloadButton: React.FC<DownloadButtonProps> = ({ sessionId }) => {
  const handleDownload = () => {
    window.open(getDownloadUrl(sessionId), '_blank');
  };

  return (
    <button
      onClick={handleDownload}
      style={{
        padding: '12px 32px',
        fontSize: '1.1em',
        backgroundColor: '#4caf50',
        color: 'white',
        border: 'none',
        borderRadius: '6px',
        cursor: 'pointer',
        marginTop: '16px',
        transition: 'background-color 0.2s',
      }}
    >
      📥 Download Translated Files
    </button>
  );
};

export default DownloadButton;
