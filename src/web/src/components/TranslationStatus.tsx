import React from 'react';
import type { TranslationStatusResponse } from '../types/translation';

interface TranslationStatusProps {
  status: TranslationStatusResponse;
}

const statusColors: Record<string, string> = {
  Uploading: '#2196f3',
  Processing: '#ff9800',
  Completed: '#4caf50',
  Failed: '#f44336',
  Error: '#f44336',
};

const TranslationStatus: React.FC<TranslationStatusProps> = ({ status }) => {
  const color = statusColors[status.status] || '#666';
  const isTerminal = ['Completed', 'Failed', 'Error'].includes(status.status);

  return (
    <div className="translation-status" style={{ marginTop: '16px' }}>
      <div
        style={{
          backgroundColor: '#f5f5f5',
          borderRadius: '8px',
          padding: '20px',
          borderLeft: `4px solid ${color}`,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <h3 style={{ margin: 0, color }}>
            {status.status === 'Uploading' && '⬆️ Uploading...'}
            {status.status === 'Processing' && '⏳ Processing...'}
            {status.status === 'Completed' && '✅ Translation Complete'}
            {status.status === 'Failed' && '❌ Translation Failed'}
            {status.status === 'Error' && '⚠️ Error'}
          </h3>
          {!isTerminal && (
            <span style={{ color: '#999', fontSize: '0.85em' }}>Auto-refreshing every 5s</span>
          )}
        </div>

        <p style={{ margin: '4px 0', color: '#555' }}>
          <strong>Session:</strong> {status.sessionId}
        </p>
        <p style={{ margin: '4px 0', color: '#555' }}>
          <strong>Target Language:</strong> {status.targetLanguage}
        </p>
        <p style={{ margin: '4px 0', color: '#555' }}>
          <strong>Total Files:</strong> {status.totalFiles}
        </p>

        {status.error && (
          <div style={{ backgroundColor: '#fdecea', padding: '12px', borderRadius: '4px', marginTop: '12px' }}>
            <p style={{ color: '#721c24', margin: 0 }}>
              <strong>Error:</strong> {status.error}
            </p>
          </div>
        )}

        {status.batches.length > 0 && (
          <div style={{ marginTop: '16px' }}>
            <h4 style={{ margin: '0 0 8px 0', color: '#333' }}>Batch Progress</h4>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9em' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid #ddd' }}>
                  <th style={{ textAlign: 'left', padding: '8px' }}>Batch</th>
                  <th style={{ textAlign: 'left', padding: '8px' }}>Status</th>
                  <th style={{ textAlign: 'right', padding: '8px' }}>Files</th>
                  <th style={{ textAlign: 'right', padding: '8px' }}>Translated</th>
                </tr>
              </thead>
              <tbody>
                {status.batches.map((batch, index) => (
                  <tr key={batch.batchId} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '8px' }}>Batch {index + 1}</td>
                    <td style={{ padding: '8px', color: statusColors[batch.status] || '#666' }}>
                      {batch.status}
                    </td>
                    <td style={{ textAlign: 'right', padding: '8px' }}>{batch.fileCount}</td>
                    <td style={{ textAlign: 'right', padding: '8px' }}>
                      {batch.translatedFileCount ?? '-'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

export default TranslationStatus;
