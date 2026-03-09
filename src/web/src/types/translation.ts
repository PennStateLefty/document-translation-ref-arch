export interface TranslationSessionResponse {
  sessionId: string;
  status: string;
  statusUrl: string;
  createdAt: string;
}

export interface BatchInfo {
  batchId: string;
  status: string;
  fileCount: number;
  translatedFileCount?: number;
  error?: string;
}

export interface TranslationStatusResponse {
  sessionId: string;
  status: 'Uploading' | 'Processing' | 'Completed' | 'Failed' | 'Error';
  targetLanguage: string;
  totalFiles: number;
  createdAt: string;
  batches: BatchInfo[];
  downloadUrl?: string;
  error?: string;
}

export interface LanguageOption {
  code: string;
  name: string;
}

export interface LanguagesResponse {
  languages: LanguageOption[];
}

export interface ErrorResponse {
  error: string;
  details?: string[];
}
