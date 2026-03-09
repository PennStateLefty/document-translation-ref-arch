import { useState, useCallback } from 'react';
import { startTranslation, ApiError } from '../services/apiClient';
import type { TranslationSessionResponse } from '../types/translation';

interface UseTranslationReturn {
  files: File[];
  targetLanguage: string;
  isUploading: boolean;
  error: string | null;
  errorDetails: string[] | undefined;
  session: TranslationSessionResponse | null;
  setFiles: (files: File[]) => void;
  setTargetLanguage: (language: string) => void;
  submitTranslation: () => Promise<void>;
  reset: () => void;
  clearError: () => void;
}

export function useTranslation(): UseTranslationReturn {
  const [files, setFiles] = useState<File[]>([]);
  const [targetLanguage, setTargetLanguage] = useState('');
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorDetails, setErrorDetails] = useState<string[] | undefined>();
  const [session, setSession] = useState<TranslationSessionResponse | null>(null);

  const submitTranslation = useCallback(async () => {
    if (files.length === 0) {
      setError('No files selected. Please select at least one file to translate.');
      return;
    }

    if (!targetLanguage) {
      setError('Please select a target language.');
      return;
    }

    setIsUploading(true);
    setError(null);
    setErrorDetails(undefined);

    try {
      const result = await startTranslation(files, targetLanguage);
      setSession(result);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        setErrorDetails(err.details);
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsUploading(false);
    }
  }, [files, targetLanguage]);

  const reset = useCallback(() => {
    setFiles([]);
    setTargetLanguage('');
    setIsUploading(false);
    setError(null);
    setErrorDetails(undefined);
    setSession(null);
  }, []);

  const clearError = useCallback(() => {
    setError(null);
    setErrorDetails(undefined);
  }, []);

  return {
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
  };
}
