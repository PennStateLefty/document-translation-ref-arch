import { useEffect, useRef, useState, useCallback } from 'react';
import { getTranslationStatus } from '../services/apiClient';
import type { TranslationStatusResponse } from '../types/translation';

const POLL_INTERVAL_MS = 5000;
const TERMINAL_STATUSES = ['Completed', 'Failed', 'Error'];

interface UsePollingReturn {
  status: TranslationStatusResponse | null;
  isPolling: boolean;
  error: string | null;
}

export function usePolling(sessionId: string | null): UsePollingReturn {
  const [status, setStatus] = useState<TranslationStatusResponse | null>(null);
  const [isPolling, setIsPolling] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const stopPolling = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
    setIsPolling(false);
  }, []);

  const poll = useCallback(async () => {
    if (!sessionId) return;

    try {
      const result = await getTranslationStatus(sessionId);
      setStatus(result);
      setError(null);

      if (TERMINAL_STATUSES.includes(result.status)) {
        stopPolling();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get status');
    }
  }, [sessionId, stopPolling]);

  useEffect(() => {
    if (!sessionId) {
      stopPolling();
      setStatus(null);
      return;
    }

    // Initial fetch
    setIsPolling(true);
    poll();

    // Start interval
    intervalRef.current = setInterval(poll, POLL_INTERVAL_MS);

    return () => {
      stopPolling();
    };
  }, [sessionId, poll, stopPolling]);

  return { status, isPolling, error };
}
