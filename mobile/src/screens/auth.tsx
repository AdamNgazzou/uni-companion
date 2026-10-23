import React, { useState } from 'react';
import { Pressable, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import {
  Button,
  Chip,
  CourseRow,
  Field,
  Header,
  Hero,
  Logo,
  Mascot,
  Notice,
  Page,
  SearchField,
  T,
  layout,
  useNav,
} from '../ui';
import { colors as c } from '../theme';
import { useStore } from '../store';
import { RootStack } from '../types';
const validEmail = (email: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
export function WelcomeScreen() {
  const nav = useNav();
  const { update } = useStore();
  return (
    <Page
      footer={
        <>
          <Button label="Get started" icon="ArrowRight" onPress={() => nav.navigate('Register')} />
          <Button
            label="I already have an account"
            kind="secondary"
            onPress={() => nav.navigate('Login')}
          />
          <Button
            label="Explore the local demo"
            kind="quiet"
            onPress={() => {
              update((s) => ({ ...s, session: true, onboarded: true }));
              nav.reset({ index: 0, routes: [{ name: 'Tabs' }] });
            }}
          />
        </>
      }
    >
      <View style={{ paddingTop: 18 }}>
        <Logo />
      </View>
      <View style={{ alignItems: 'center', marginTop: 20 }}>
        <Mascot size={260} />
      </View>
      <T variant="hero" style={{ fontSize: 42, lineHeight: 46 }}>
        Your courses.{'\n'}Your people.
      </T>
      <T style={{ color: c.muted, fontSize: 18, lineHeight: 27 }}>
        Understand your notes, practise, and learn together.
      </T>
      <View style={[layout.inline, { flexWrap: 'wrap', marginTop: 4 }]}>
        <Chip label="Learn a little" icon="BookOpen" />
        <Chip label="Ask your crowd" icon="MessageCircle" />
      </View>
      <T variant="small">
        A local preview with sample courses. No account or backend connection is needed.
      </T>
    </Page>
  );
}
export function LoginScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const submit = () => {
    setSubmitted(true);
    if (!validEmail(email) || !password) return;
    update((s) => ({ ...s, session: true, profile: { ...s.profile, email: email.trim() } }));
    nav.reset({ index: 0, routes: [{ name: data.onboarded ? 'Tabs' : 'Setup' }] });
  };
  return (
    <Page>
      <Header title="Welcome back" />
      <Hero title="Good to see you." subtitle="A little learning goes a long way." />
      <Notice>
        Local preview: use any valid email and a non-empty password. No credentials are sent or
        saved.
      </Notice>
      <Field
        label="Email"
        value={email}
        onChangeText={setEmail}
        placeholder="Your university email"
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
        error={submitted && !validEmail(email) ? 'Enter a valid email address.' : undefined}
      />
      <Field
        label="Password"
        value={password}
        onChangeText={setPassword}
        placeholder="Your password"
        password
        autoComplete="current-password"
        error={submitted && !password ? 'Enter a password to continue.' : undefined}
      />
      <Pressable
        onPress={() => nav.navigate('ForgotPassword')}
        accessibilityRole="button"
        style={{ alignSelf: 'flex-end', minHeight: 48, justifyContent: 'center' }}
      >
        <T style={layout.link}>Forgot password?</T>
      </Pressable>
      <Button label="Log in" onPress={submit} icon="ArrowRight" />
      <Button
        label="New here? Create an account"
        kind="quiet"
        onPress={() => nav.navigate('Register')}
      />
    </Page>
  );
}
export function RegisterScreen() {
  const nav = useNav();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitted, setSubmitted] = useState(false);
  return (
    <Page>
      <Header title="Create account" />
      <Hero title="Make yourself at home." subtitle="Your next study session starts here." />
      <Field
        label="Display name"
        value={name}
        onChangeText={setName}
        placeholder="What should we call you?"
        autoComplete="nickname"
        error={submitted && name.trim().length < 2 ? 'Use at least 2 characters.' : undefined}
      />
      <Field
        label="Email"
        value={email}
        onChangeText={setEmail}
        placeholder="you@university.edu"
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
        error={submitted && !validEmail(email) ? 'Enter a valid email address.' : undefined}
      />
      <Field
        label="Password"
        value={password}
        onChangeText={setPassword}
        password
        placeholder="At least 12 characters"
        autoComplete="new-password"
        error={
          submitted && password.length < 12
            ? 'Use at least 12 characters for this preview.'
            : undefined
        }
      />
      <Notice>
        This preview creates a profile on this device. It doesn’t create a real account or store
        your password.
      </Notice>
      <Button
        label="Create account"
        icon="ArrowRight"
        onPress={() => {
          setSubmitted(true);
          if (name.trim().length >= 2 && validEmail(email) && password.length >= 12)
            nav.navigate('Verify', { name: name.trim(), email: email.trim() });
        }}
      />
      <Button label="Already joined? Log in" kind="quiet" onPress={() => nav.navigate('Login')} />
    </Page>
  );
}
export function VerifyScreen({ route }: NativeStackScreenProps<RootStack, 'Verify'>) {
  const nav = useNav();
  const { update } = useStore();
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [resent, setResent] = useState(false);
  return (
    <Page>
      <Header title="Verify email" />
      <View style={{ alignItems: 'center' }}>
        <Mascot size={190} />
      </View>
      <T variant="hero">Check your inbox.</T>
      <T style={{ color: c.muted }}>A verification code would be sent to {route.params.email}.</T>
      <Notice>Offline preview code: 123456. No email is sent.</Notice>
      <Field
        label="6-digit code"
        value={code}
        onChangeText={(value) => {
          setCode(value.replace(/\D/g, '').slice(0, 6));
          setError('');
        }}
        keyboardType="number-pad"
        maxLength={6}
        placeholder="123456"
        autoComplete="one-time-code"
        style={{ letterSpacing: 8, fontSize: 25, textAlign: 'center' }}
        error={error || undefined}
      />
      <Button
        label="Verify email"
        disabled={code.length !== 6}
        onPress={() => {
          if (code !== '123456') {
            setError('Use the preview code 123456.');
            return;
          }
          update((s) => ({
            ...s,
            session: true,
            profile: { ...s.profile, name: route.params.name, email: route.params.email },
          }));
          nav.replace('Setup');
        }}
      />
      <Button label="Resend preview code" kind="secondary" onPress={() => setResent(true)} />
      {resent && <Notice>For this preview, the code is still 123456.</Notice>}
      <Button label="Change email" kind="quiet" onPress={() => nav.goBack()} />
    </Page>
  );
}
export function ForgotPasswordScreen() {
  const nav = useNav();
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');
  return (
    <Page>
      <Header title="Log in" />
      <Hero title="Let’s get you back in." subtitle="We’ll help you find your way back." />
      <Field
        label="Email address"
        value={email}
        onChangeText={setEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        placeholder="you@university.edu"
        error={error || undefined}
      />
      <Button
        label="Send reset link"
        onPress={() => {
          if (!validEmail(email)) {
            setError('Enter a valid email address.');
            return;
          }
          setError('');
          setSent(true);
        }}
      />
      {sent && (
        <>
          <Notice tone="success">
            Preview only: no email is sent. You can try the reset form below.
          </Notice>
          <Button
            label="Preview reset form"
            kind="secondary"
            onPress={() => nav.navigate('ResetPassword')}
          />
        </>
      )}
      <Button label="Back to login" kind="quiet" onPress={() => nav.goBack()} />
    </Page>
  );
}
export function ResetPasswordScreen() {
  const nav = useNav();
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);
  return (
    <Page>
      <Header title="New password" />
      <Hero title="A fresh start." subtitle="Try the password reset experience." />
      {done ? (
        <>
          <Notice tone="success">
            Reset preview complete. No real password was changed or saved.
          </Notice>
          <Button label="Back to login" onPress={() => nav.navigate('Login')} />
        </>
      ) : (
        <>
          <Field
            label="New password"
            value={password}
            onChangeText={setPassword}
            password
            placeholder="At least 12 characters"
          />
          <Field
            label="Confirm password"
            value={confirm}
            onChangeText={setConfirm}
            password
            placeholder="Enter it again"
            error={error || undefined}
          />
          <Button
            label="Reset password"
            onPress={() => {
              if (password.length < 12) setError('Use at least 12 characters.');
              else if (confirm !== password) setError('The passwords don’t match.');
              else {
                setDone(true);
                setPassword('');
                setConfirm('');
              }
            }}
          />
        </>
      )}
    </Page>
  );
}
export function SetupScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [department, setDepartment] = useState(data.profile.department);
  const [year, setYear] = useState(data.profile.year);
  return (
    <Page
      footer={
        <Button
          label="Choose my courses"
          icon="ArrowRight"
          onPress={() => {
            update((s) => ({ ...s, profile: { ...s.profile, department, year } }));
            nav.navigate('SelectCourses');
          }}
        />
      }
    >
      <Header title="Your university" />
      <T variant="small" style={{ color: c.blue }}>
        Step 1 of 2
      </T>
      <Hero title="Find your corner of campus." subtitle="We’ll help you make it yours." />
      <T variant="label">Your department</T>
      <View style={{ gap: 9 }}>
        {['Computer Science', 'Mathematics', 'Engineering'].map((value) => (
          <Chip
            key={value}
            label={value}
            selected={department === value}
            onPress={() => setDepartment(value)}
          />
        ))}
      </View>
      <T variant="label">Your year</T>
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {[1, 2, 3, 4].map((value) => (
          <Chip
            key={value}
            label={`Year ${value}`}
            selected={year === value}
            onPress={() => setYear(value)}
          />
        ))}
      </View>
      <T variant="small">You can change this later.</T>
    </Page>
  );
}
export function SelectCoursesScreen({ route }: NativeStackScreenProps<RootStack, 'SelectCourses'>) {
  const nav = useNav();
  const { data, update } = useStore();
  const [selected, setSelected] = useState(data.joined);
  const [search, setSearch] = useState('');
  const filtered = data.courses.filter((x) =>
    `${x.title} ${x.code}`.toLowerCase().includes(search.toLowerCase()),
  );
  return (
    <Page
      footer={
        <>
          <T variant="label">{selected.length} courses selected</T>
          <Button
            label={route.params?.editing ? 'Save my courses' : 'Let’s go'}
            icon="ArrowRight"
            onPress={() => {
              update((s) => ({ ...s, joined: selected, onboarded: true }));
              if (route.params?.editing) nav.goBack();
              else nav.reset({ index: 0, routes: [{ name: 'Tabs' }] });
            }}
          />
        </>
      }
    >
      <Header title="Select courses" />
      <Hero title="What are you taking?" subtitle="Pick your courses. Find your people." />
      <SearchField value={search} onChangeText={setSearch} placeholder="Search courses…" />
      {filtered.map((course) => (
        <CourseRow
          key={course.id}
          course={course}
          selected={selected.includes(course.id)}
          onPress={() =>
            setSelected((old) =>
              old.includes(course.id) ? old.filter((x) => x !== course.id) : [...old, course.id],
            )
          }
        />
      ))}
      {!filtered.length && (
        <Notice>No courses match that search. Try a course code or title.</Notice>
      )}
      <T variant="small">
        Admin curated courses come from the course admin. Student-created courses keep their author
        label, even after recognition.
      </T>
    </Page>
  );
}
