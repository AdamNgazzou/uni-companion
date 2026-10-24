import { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { errorText } from '../api/client';

// Ignore a late response after Back/account switch, including before navigation.
export function useOperation() {
  const focus = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const locked = useRef(false);
  const [error, setError] = useState('');
  useFocusEffect(
    useCallback(() => {
      const controller = new AbortController();
      focus.current = controller;
      locked.current = false;
      setBusy(false);
      return () => controller.abort();
    }, []),
  );
  async function run(action: (signal: AbortSignal) => Promise<void>) {
    const controller = focus.current;
    if (locked.current || !controller || controller.signal.aborted) return;
    locked.current = true;
    setBusy(true);
    setError('');
    try {
      await action(controller.signal);
    } catch (e) {
      if (!controller.signal.aborted) setError(errorText(e));
    } finally {
      if (!controller.signal.aborted) {
        locked.current = false;
        setBusy(false);
      }
    }
  }
  return { run, busy, error };
}
