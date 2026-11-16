import { useCallback, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { useFocusEffect } from 'expo-router';
import { api } from '../api/runtime';
import { ApiError, errorText } from '../api/client';
export function useResource<T>(path: string | null, interval = 0) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const refreshHandler = useRef<(() => void) | null>(null);
  const refresh = useCallback(() => refreshHandler.current?.(), []);
  useFocusEffect(
    useCallback(() => {
      void revision;
      if (!path) {
        refreshHandler.current = null;
        setData(null);
        setError('');
        setLoading(false);
        return;
      }
      let active = true;
      let foreground =
        AppState.currentState !== 'background' && AppState.currentState !== 'inactive';
      let generation = 0;
      let timer: ReturnType<typeof setTimeout> | undefined;
      let controller: AbortController | undefined;
      function stop() {
        generation++;
        clearTimeout(timer);
        controller?.abort();
      }
      async function load(initial: boolean) {
        if (!active) return;
        const current = ++generation;
        const request = new AbortController();
        controller = request;
        if (initial) {
          setData(null);
          setLoading(true);
          setError('');
        }
        try {
          const value = await api.request<T>(path!, { signal: request.signal });
          if (active && current === generation) {
            setData(value);
            setError('');
          }
        } catch (e) {
          if (active && current === generation && !request.signal.aborted) {
            setError(errorText(e));
            if (e instanceof ApiError && [401, 403, 404].includes(e.status)) setData(null);
          }
        } finally {
          if (active && current === generation) {
            setLoading(false);
            if (interval > 0 && foreground && !request.signal.aborted)
              timer = setTimeout(() => void load(false), interval);
          }
        }
      }
      refreshHandler.current = () => {
        stop();
        void load(false);
      };
      const subscription =
        interval > 0
          ? AppState.addEventListener('change', (state) => {
              foreground = state === 'active';
              stop();
              if (foreground) void load(false);
            })
          : undefined;
      if (!interval || foreground) void load(true);
      return () => {
        active = false;
        refreshHandler.current = null;
        stop();
        subscription?.remove();
      };
    }, [path, revision, interval]),
  );
  return { data, error, loading, refresh, reload: () => setRevision((value) => value + 1) };
}
