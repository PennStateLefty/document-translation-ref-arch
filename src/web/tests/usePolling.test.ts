import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { usePolling } from '../src/hooks/usePolling';
import * as apiClient from '../src/services/apiClient';
import type { TranslationStatusResponse } from '../src/types/translation';

vi.mock('../src/services/apiClient');

const mockStatus: TranslationStatusResponse = {
  sessionId: 'test-session-123',
  status: 'Processing',
  targetLanguage: 'es',
  totalFiles: 5,
  createdAt: '2025-01-01T00:00:00Z',
  batches: [
    { batchId: 'batch-1', status: 'Running', fileCount: 5 },
  ],
};

describe('usePolling', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.mocked(apiClient.getTranslationStatus).mockResolvedValue(mockStatus);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('does not poll when sessionId is null', () => {
    const { result } = renderHook(() => usePolling(null));

    expect(result.current.status).toBeNull();
    expect(result.current.isPolling).toBe(false);
    expect(apiClient.getTranslationStatus).not.toHaveBeenCalled();
  });

  it('starts polling when sessionId is provided', async () => {
    const { result } = renderHook(() => usePolling('test-session-123'));

    // Flush the initial poll promise
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(apiClient.getTranslationStatus).toHaveBeenCalledWith('test-session-123');
    expect(result.current.isPolling).toBe(true);
  });

  it('stops polling on terminal status (Completed)', async () => {
    const completedStatus: TranslationStatusResponse = {
      ...mockStatus,
      status: 'Completed',
      downloadUrl: '/api/translate/test-session-123/download',
    };
    vi.mocked(apiClient.getTranslationStatus).mockResolvedValue(completedStatus);

    const { result } = renderHook(() => usePolling('test-session-123'));

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(result.current.status?.status).toBe('Completed');
    expect(result.current.isPolling).toBe(false);
  });

  it('stops polling on terminal status (Failed)', async () => {
    const failedStatus: TranslationStatusResponse = {
      ...mockStatus,
      status: 'Failed',
      error: 'Translation failed',
    };
    vi.mocked(apiClient.getTranslationStatus).mockResolvedValue(failedStatus);

    const { result } = renderHook(() => usePolling('test-session-123'));

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(result.current.status?.status).toBe('Failed');
    expect(result.current.isPolling).toBe(false);
  });

  it('handles API errors gracefully', async () => {
    vi.mocked(apiClient.getTranslationStatus).mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => usePolling('test-session-123'));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(result.current.error).toBe('Network error');
  });

  it('cleans up interval on unmount', () => {
    const clearIntervalSpy = vi.spyOn(global, 'clearInterval');
    const { unmount } = renderHook(() => usePolling('test-session-123'));

    unmount();

    expect(clearIntervalSpy).toHaveBeenCalled();
  });
});
