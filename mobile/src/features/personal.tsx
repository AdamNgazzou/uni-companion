import React, { useState } from 'react';
import { router } from 'expo-router';
import { Button, Field, Header, Hero, Notice, Page, Row, Section, T } from '../ui';
import { useSession } from '../auth/session';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
export function ProfileScreen() {
  const { profile, logout } = useSession();
  const [error, setError] = useState('');
  return (
    <Page>
      <Header title="Your corner" back={false} />
      <Hero title={profile?.displayName || 'Your profile'} subtitle={profile?.university} />
      <T>{profile?.email}</T>
      <Section title="Make progress your way" />
      <Row
        title="Study history"
        detail="Saved explanations, summaries and quizzes"
        icon="History"
        onPress={() => router.push('/history')}
      />
      <Row
        title="Account settings"
        detail="Your name and password"
        icon="Settings"
        onPress={() => router.push('/settings')}
      />
      <Notice>
        Your account belongs to Horizon University. Course recognition means admin reviewed, rather
        than university accreditation.
      </Notice>
      {!!error && <Notice tone="error">{error}</Notice>}
      <Button
        label="Sign out on this device"
        kind="quiet"
        onPress={() => {
          void logout().catch((e) => setError(errorText(e)));
        }}
        icon="LogOut"
      />
    </Page>
  );
}
export function SettingsScreen() {
  const { profile, update, logout } = useSession();
  const [name, setName] = useState(profile?.displayName || '');
  const [currentPassword, setCurrent] = useState('');
  const [newPassword, setNew] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function save(password = false) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      if (password) {
        await api.request('/accounts/change-password', {
          method: 'POST',
          body: JSON.stringify({ currentPassword, newPassword }),
        });
        await logout();
      } else {
        await update(name.trim());
        setNotice('Your name has been saved.');
      }
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Page>
      <Header title="Account settings" />
      <Section title="Your profile" />
      <Field label="Your name" value={name} onChangeText={setName} maxLength={80} />
      <Button label="Save name" onPress={() => void save()} disabled={!name.trim()} busy={busy} />
      {!!notice && <Notice tone="success">{notice}</Notice>}
      <Section title="Change password" />
      <Field
        label="Current password"
        value={currentPassword}
        onChangeText={setCurrent}
        password
        autoComplete="current-password"
        maxLength={128}
      />
      <Field
        label="New password"
        value={newPassword}
        onChangeText={setNew}
        password
        autoComplete="new-password"
        maxLength={128}
      />
      <T variant="small">
        Use at least 12 characters with uppercase, lowercase, a number and a symbol. Updating your
        password signs out your existing sessions.
      </T>
      <Button
        label="Update password"
        kind="secondary"
        onPress={() => void save(true)}
        busy={busy}
        disabled={!currentPassword || newPassword.length < 12}
      />
      {!!error && <Notice tone="error">{error}</Notice>}
    </Page>
  );
}
