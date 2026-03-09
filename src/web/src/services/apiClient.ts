import type { TranslationSessionResponse, TranslationStatusResponse, LanguagesResponse } from '../types/translation';

const API_BASE = '/api';

export async function startTranslation(files: File[], targetLanguage: string): Promise<TranslationSessionResponse> {
  const formData = new FormData();
  formData.append('targetLanguage', targetLanguage);
  files.forEach((file) => formData.append('files', file));

  const response = await fetch(`${API_BASE}/translate`, {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    const errorData = await response.json();
    throw new ApiError(errorData.error || 'Upload failed', errorData.details);
  }

  return response.json();
}

export async function getTranslationStatus(sessionId: string): Promise<TranslationStatusResponse> {
  const response = await fetch(`${API_BASE}/translate/${sessionId}`);

  if (!response.ok) {
    const errorData = await response.json();
    throw new ApiError(errorData.error || 'Failed to get status', errorData.details);
  }

  return response.json();
}

export function getDownloadUrl(sessionId: string): string {
  return `${API_BASE}/translate/${sessionId}/download`;
}

export async function getLanguages(): Promise<LanguagesResponse> {
  const response = await fetch(`${API_BASE}/languages`);

  if (!response.ok) {
    throw new ApiError('Failed to load supported languages');
  }

  return response.json();
}

export class ApiError extends Error {
  public details?: string[];

  constructor(message: string, details?: string[]) {
    super(message);
    this.name = 'ApiError';
    this.details = details;
  }
}
