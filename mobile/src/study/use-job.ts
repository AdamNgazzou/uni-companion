import { useCallback, useState } from 'react';
import { AppState } from 'react-native';
import { useFocusEffect } from 'expo-router';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import { jobPoller } from '../api/polling';
import type { Job } from '../api/types';

export function useJob(id: string) {
  const [job, setJob] = useState<Job | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useFocusEffect(
    useCallback(() => {
      // Revision explicitly restarts the external polling subscription after a manual retry.
      void revision;
      const poller = jobPoller(
        (signal) => api.request<Job>(`/study-jobs/${id}`, { signal }),
        (value) => {
          setJob(value);
          setError('');
        },
        (e) => setError(errorText(e)),
      );
      if (AppState.currentState !== 'background') poller.start();
      const subscription = AppState.addEventListener('change', (state) => {
        if (state === 'active') poller.start();
        else poller.stop();
      });
      return () => {
        poller.stop();
        subscription.remove();
      };
    }, [id, revision]),
  );
  return { job: job?.id === id ? job : null, error, reload: () => setRevision((x) => x + 1) };
}
