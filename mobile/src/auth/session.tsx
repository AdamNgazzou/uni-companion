import React, { createContext, useContext, useEffect, useState } from 'react';
import { api } from '../api/runtime';
import { ApiError, errorText } from '../api/client';
import { drafts } from '../study/draft-runtime';
import { practiceDrafts } from '../practice/draft-runtime';
import { composerDrafts } from '../community/draft-runtime';
import { push } from '../push/runtime';
import type { Profile } from '../api/types';
type Session = {
  profile: Profile | null;
  ready: boolean;
  error: string;
  restore: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  update: (name: string) => Promise<void>;
};
const Context = createContext<Session | null>(null);
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState('');
  async function restore() {
    try {
      await api.restore();
      setProfile(await api.request<Profile>('/accounts/me'));
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) setError(errorText(e));
    } finally {
      setReady(true);
    }
  }
  // Session bootstrap synchronizes the external encrypted token store on mount.
  useEffect(() => {
    api.onInvalidSession = () => {
      setProfile(null);
      void Promise.allSettled([
        drafts.clear(),
        composerDrafts.clear(),
        practiceDrafts.clear(),
        push.detach(),
      ]);
    };
    // eslint-disable-next-line react-hooks/set-state-in-effect -- synchronize the external secure store on mount
    void restore();
    return () => {
      api.onInvalidSession = () => {};
    };
  }, []);
  async function login(email: string, password: string) {
    setProfile(null);
    await Promise.all([
      drafts.clear(),
      composerDrafts.clear(),
      practiceDrafts.clear(),
      push.detach(),
    ]);
    await api.signIn(email, password);
    setProfile(await api.request<Profile>('/accounts/me'));
    setError('');
  }
  async function logout() {
    setProfile(null);
    const results = await Promise.allSettled([
      push.detach(),
      drafts.clear(),
      composerDrafts.clear(),
      practiceDrafts.clear(),
      api.signOut(),
    ]);
    const failure = results.find((result) => result.status === 'rejected');
    if (failure?.status === 'rejected') {
      setError(errorText(failure.reason));
      throw failure.reason;
    }
  }
  async function update(displayName: string) {
    await api.request('/accounts/me', { method: 'PATCH', body: JSON.stringify({ displayName }) });
    setProfile(await api.request<Profile>('/accounts/me'));
  }
  return (
    <Context.Provider value={{ profile, ready, error, restore, login, logout, update }}>
      {children}
    </Context.Provider>
  );
}
export function useSession() {
  const value = useContext(Context);
  if (!value) throw new Error('Missing session provider');
  return value;
}
