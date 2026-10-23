import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { initialData } from './data';
import { LocalData } from './types';
const KEY = 'uni-companion:prototype:v1';
type Store = {
  data: LocalData;
  update: (fn: (value: LocalData) => LocalData) => void;
  ready: boolean;
  storageError: boolean;
};
const Context = createContext<Store | null>(null);
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [data, setData] = useState<LocalData>(initialData);
  const [ready, setReady] = useState(false);
  const [storageError, setStorageError] = useState(false);
  const writes = useRef(Promise.resolve());
  useEffect(() => {
    let active = true;
    AsyncStorage.getItem(KEY)
      .then((raw) => {
        if (raw && active) {
          const value = JSON.parse(raw);
          if (
            value.version === 1 &&
            Array.isArray(value.courses) &&
            Array.isArray(value.posts) &&
            value.profile
          )
            setData({ ...initialData, ...value });
        }
      })
      .catch(() => {
        if (active) setStorageError(true);
      })
      .finally(() => {
        if (active) setReady(true);
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    if (!ready) return;
    writes.current = writes.current
      .then(() => AsyncStorage.setItem(KEY, JSON.stringify(data)))
      .catch(() => setStorageError(true));
  }, [data, ready]);
  return (
    <Context.Provider value={{ data, update: setData, ready, storageError }}>
      {children}
    </Context.Provider>
  );
}
export function useStore() {
  const value = useContext(Context);
  if (!value) throw new Error('StoreProvider is required');
  return value;
}
export const uid = () => `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
export function useSaved(id: string) {
  const { data, update } = useStore();
  return {
    saved: data.saved.includes(id),
    toggle: () =>
      update((s) => ({
        ...s,
        saved: s.saved.includes(id) ? s.saved.filter((x) => x !== id) : [...s.saved, id],
      })),
  };
}
