import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { Button, Field, Header, Hero, Logo, Mascot, Notice, Page, T } from '../ui';
import { useSession } from '../auth/session';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import { colors as c } from '../theme';
type Mode = 'welcome' | 'login' | 'register' | 'verify' | 'forgot' | 'reset';
export default function AuthScreen() {
  const { login, error: sessionError, restore } = useSession();
  const [mode, setMode] = useState<Mode>('welcome');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  function change(next: Mode) {
    setMode(next);
    setError('');
    setNotice('');
    setPassword('');
    setCode('');
  }
  async function submit() {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      if (mode === 'login') {
        await login(email, password);
        router.replace('/');
      } else {
        const path =
          mode === 'register'
            ? 'register'
            : mode === 'verify'
              ? 'resend-confirmation'
              : mode === 'forgot'
                ? 'forgot-password'
                : 'reset-password';
        const body =
          mode === 'register'
            ? { email: email.trim(), password, displayName: name.trim() }
            : mode === 'reset'
              ? { email: email.trim(), code: code.trim(), newPassword: password }
              : { email: email.trim() };
        await api.request(
          `/accounts/${path}`,
          { method: 'POST', body: JSON.stringify(body) },
          false,
        );
        if (mode === 'reset') {
          change('login');
          setNotice('Password updated. Sign in with your new password.');
        } else if (mode === 'register') {
          change('verify');
          setNotice('Check your email and open the confirmation link before signing in.');
        } else
          setNotice(
            mode === 'verify'
              ? 'If your address is eligible, a confirmation link is on its way.'
              : 'If your account is eligible, a reset code is on its way.',
          );
      }
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  if (mode === 'welcome')
    return (
      <Page
        footer={
          <>
            <Button
              label="Let’s get started"
              onPress={() => change('register')}
              icon="ArrowRight"
            />
            <Button
              label="I already have an account"
              kind="quiet"
              onPress={() => change('login')}
            />
          </>
        }
      >
        <Logo />
        <View style={{ alignItems: 'center', paddingVertical: 30, gap: 24 }}>
          <Mascot size={230} />
          <T variant="hero" style={{ textAlign: 'center' }}>
            Big ideas.{'\n'}Little breakthroughs.
          </T>
          <T style={{ textAlign: 'center', color: c.muted }}>
            Your university courses, clearer explanations and practice that helps things click.
          </T>
        </View>
        {!!sessionError && (
          <>
            <Notice tone="error">{sessionError}</Notice>
            <Button
              label="Reconnect to my account"
              kind="secondary"
              onPress={() => void restore()}
            />
          </>
        )}
      </Page>
    );
  const titles = {
    login: 'Good to see you.',
    register: 'Your next chapter.',
    verify: 'Check your inbox.',
    forgot: 'Let’s get you back in.',
    reset: 'A fresh start.',
  };
  return (
    <Page
      footer={
        <Button
          label={
            mode === 'login'
              ? 'Sign in'
              : mode === 'register'
                ? 'Create account'
                : mode === 'verify'
                  ? 'Resend confirmation'
                  : mode === 'forgot'
                    ? 'Send reset code'
                    : 'Update password'
          }
          onPress={() => void submit()}
          busy={busy}
          disabled={
            !email.trim() ||
            ((mode === 'login' || mode === 'register' || mode === 'reset') && !password) ||
            (mode === 'register' && !name.trim()) ||
            (mode === 'reset' && !code.trim())
          }
        />
      }
    >
      <Header
        title="Uni Companion"
        right={<Button label="Back" kind="quiet" onPress={() => change('welcome')} />}
        back={false}
      />
      <Hero
        title={titles[mode]}
        subtitle={
          mode === 'verify'
            ? 'Open the confirmation link in your email, then come back to sign in.'
            : mode === 'register'
              ? 'For students at Horizon University.'
              : 'A little progress starts here.'
        }
      />
      <Field
        label="Email address"
        value={email}
        onChangeText={setEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
        maxLength={254}
      />
      {mode === 'register' && (
        <Field
          label="Your name"
          value={name}
          onChangeText={setName}
          autoComplete="name"
          maxLength={80}
        />
      )}
      {(mode === 'login' || mode === 'register' || mode === 'reset') && (
        <>
          <Field
            label={mode === 'reset' ? 'New password' : 'Password'}
            value={password}
            onChangeText={setPassword}
            password
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            maxLength={128}
          />
          {mode !== 'login' && (
            <T variant="small">
              Use 12–128 characters, with an uppercase letter, a lowercase letter, a number and a
              symbol.
            </T>
          )}
        </>
      )}
      {mode === 'reset' && (
        <Field
          label="Reset code from your email"
          value={code}
          onChangeText={setCode}
          autoCapitalize="none"
          multiline
          maxLength={4096}
        />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
      {!!notice && <Notice tone="success">{notice}</Notice>}
      {mode === 'login' && (
        <>
          <Button label="Forgot your password?" kind="quiet" onPress={() => change('forgot')} />
          <Button label="Resend email confirmation" kind="quiet" onPress={() => change('verify')} />
        </>
      )}
      {mode === 'forgot' && (
        <Button label="I have a reset code" kind="secondary" onPress={() => change('reset')} />
      )}
      {mode !== 'login' && (
        <Button label="Back to sign in" kind="quiet" onPress={() => change('login')} />
      )}
    </Page>
  );
}
