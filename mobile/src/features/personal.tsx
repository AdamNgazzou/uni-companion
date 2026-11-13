import React, { useState } from 'react';
import { router } from 'expo-router';
import { Button, Field, Header, Hero, Notice, Page, Row, Section, T } from '../ui';
import { useSession } from '../auth/session';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import { ActivityBell } from './activity';
export function ProfileScreen() {
  const { profile, logout } = useSession();
  const [error, setError] = useState('');
  return (
    <Page>
      <Header title="Your corner" back={false} right={<ActivityBell />} />
      <Hero title={profile?.displayName || 'Your profile'} subtitle={profile?.university} />
      <T>{profile?.email}</T>
      <Section title="Make progress your way" />
      <Row
        title="Your learning"
        detail="Your activity, revision queue and study reminders"
        icon="GraduationCap"
        onPress={() => router.push('/progress')}
      />
      <Row
        title="Activity"
        detail="Replies, followed discussions and finished study sessions"
        icon="Bell"
        onPress={() => router.push('/activity')}
      />
      <Row
        title="My discussions"
        detail="Your threads and replies, including anonymous posts"
        icon="MessageCircle"
        onPress={() => router.push('/my-discussions')}
      />
      <Row
        title="Saved items"
        detail="Bookmarked discussions and resources"
        icon="Bookmark"
        onPress={() => router.push('/saved-items')}
      />
      {profile?.roles.includes('Admin') && (
        <Row
          title="Community reports"
          detail="Review reported contributions"
          icon="Flag"
          onPress={() => router.push('/reports')}
        />
      )}

      <Row
        title="Course conversations"
        detail="Saved tutor chats and practice feedback"
        icon="Sparkle"
        onPress={() => router.push('/tutor')}
      />
      <Row
        title="Study history"
        detail="Saved explanations, summaries, quizzes and practice"
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
      <Row
        title="Hidden participants"
        detail="Manage hidden profiles and anonymous contributions"
        icon="EyeOff"
        onPress={() => router.push('/blocked')}
      />
      <Row
        title="Activity preferences"
        detail="Choose the updates you receive"
        icon="Bell"
        onPress={() => router.push('/activity-settings')}
      />
      <Row
        title="Phone notifications"
        detail="Device permission and private push previews"
        icon="Bell"
        onPress={() => router.push('/phone-notifications')}
      />
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
