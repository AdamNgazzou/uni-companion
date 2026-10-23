import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
export function useResource<T>(path: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  useFocusEffect(
    useCallback(() => {
      if (!path) {
        setData(null);
        setError('');
        setLoading(false);
        return;
      }
      void revision;
      setData(null);
      const controller = new AbortController();
      setLoading(true);
      setError('');
      api
        .request<T>(path, { signal: controller.signal })
        .then((value) => {
          if (!controller.signal.aborted) setData(value);
        })
        .catch((e) => {
          if (!controller.signal.aborted) setError(errorText(e));
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
      return () => controller.abort();
    }, [path, revision]),
  );
  return { data, error, loading, reload: () => setRevision((x) => x + 1) };
}
