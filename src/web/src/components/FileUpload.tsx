import React, { useCallback } from 'react';
import { useDropzone } from 'react-dropzone';

interface FileUploadProps {
  onFilesSelected: (files: File[]) => void;
  disabled?: boolean;
}

const ACCEPTED_EXTENSIONS = {
  'application/pdf': ['.pdf'],
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': ['.docx'],
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'],
  'application/vnd.openxmlformats-officedocument.presentationml.presentation': ['.pptx'],
  'text/html': ['.html', '.htm'],
  'text/plain': ['.txt'],
  'application/xliff+xml': ['.xlf', '.xliff'],
  'text/tab-separated-values': ['.tsv'],
};

const FileUpload: React.FC<FileUploadProps> = ({ onFilesSelected, disabled = false }) => {
  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      if (acceptedFiles.length > 0) {
        onFilesSelected(acceptedFiles);
      }
    },
    [onFilesSelected]
  );

  const { getRootProps, getInputProps, isDragActive, acceptedFiles } = useDropzone({
    onDrop,
    accept: ACCEPTED_EXTENSIONS,
    disabled,
    multiple: true,
  });

  return (
    <div className="file-upload">
      <div
        {...getRootProps()}
        className={`dropzone ${isDragActive ? 'dropzone--active' : ''} ${disabled ? 'dropzone--disabled' : ''}`}
        style={{
          border: '2px dashed #ccc',
          borderRadius: '8px',
          padding: '40px 20px',
          textAlign: 'center',
          cursor: disabled ? 'not-allowed' : 'pointer',
          backgroundColor: isDragActive ? '#e3f2fd' : disabled ? '#f5f5f5' : '#fafafa',
          transition: 'all 0.2s ease',
        }}
      >
        <input {...getInputProps()} />
        {isDragActive ? (
          <p>Drop your documents here...</p>
        ) : (
          <div>
            <p style={{ fontSize: '1.1em', marginBottom: '8px' }}>
              Drag and drop documents here, or click to select files
            </p>
            <p style={{ color: '#666', fontSize: '0.9em' }}>
              Supported formats: PDF, DOCX, XLSX, PPTX, HTML, TXT
            </p>
            <p style={{ color: '#666', fontSize: '0.9em' }}>
              Max file size: 30 MB
            </p>
          </div>
        )}
      </div>
      {acceptedFiles.length > 0 && (
        <div style={{ marginTop: '16px' }}>
          <strong>{acceptedFiles.length} file(s) selected:</strong>
          <ul style={{ listStyle: 'none', padding: 0, marginTop: '8px' }}>
            {acceptedFiles.map((file) => (
              <li key={file.name} style={{ padding: '4px 0', color: '#333' }}>
                📄 {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export default FileUpload;
