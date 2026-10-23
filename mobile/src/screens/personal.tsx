import React, { useState } from 'react';
import { Platform, Pressable, Switch, View } from 'react-native';
import * as FileSystem from 'expo-file-system/legacy';
import { Avatar, PostCard } from './social';
import {
  Badge,
  Button,
  Chip,
  CourseIcon,
  Empty,
  Field,
  Header,
  Icon,
  IconButton,
  Notice,
  Page,
  Row,
  Section,
  T,
  layout,
  useNav,
} from '../ui';
import { colors as c } from '../theme';
import { useStore } from '../store';
import { initialData } from '../data';
export function ProfileScreen() {
  const nav = useNav();
  const { data } = useStore();
  const [tab, setTab] = useState<'Tools' | 'My posts' | 'Anonymous'>('Tools');
  const mine = data.posts.filter(
    (x) => x.ownerId === 'me' && (tab === 'Anonymous' ? x.anonymous : !x.anonymous),
  );
  return (
    <Page>
      <Header
        title="You"
        back={false}
        right={
          <IconButton
            icon="Settings"
            label="Open settings"
            color={c.blue}
            onPress={() => nav.navigate('Settings')}
          />
        }
      />
      <View style={[layout.inline, { gap: 18, paddingVertical: 12 }]}>
        <Avatar name={data.profile.name} size={85} />
        <View style={{ flex: 1, gap: 4 }}>
          <T variant="heading">{data.profile.name}</T>
          <T style={{ color: c.muted }}>{data.profile.department}</T>
          <T variant="small">Year {data.profile.year} · local profile</T>
        </View>
      </View>
      <Button
        label="Edit profile"
        kind="secondary"
        icon="Pencil"
        onPress={() => nav.navigate('EditProfile')}
      />
      <View style={[layout.inline, { flexWrap: 'wrap', gap: 7 }]}>
        <Chip label="My tools" selected={tab === 'Tools'} onPress={() => setTab('Tools')} />
        <Chip label="My posts" selected={tab === 'My posts'} onPress={() => setTab('My posts')} />
        <Chip
          label="Anonymous"
          selected={tab === 'Anonymous'}
          onPress={() => setTab('Anonymous')}
        />
      </View>
      {tab === 'Tools' ? (
        <>
          <Row
            icon="GraduationCap"
            title="My courses"
            detail={`${data.joined.length} spaces to learn together`}
            onPress={() => nav.navigate('SelectCourses', { editing: true })}
          />
          <Row
            icon="Bookmark"
            title="Saved for later"
            detail="Notes and discussions worth coming back to"
            onPress={() => nav.navigate('Saved')}
          />
          <Row
            icon="History"
            title="Quiz history"
            detail={`${data.attempts.length} completed practice sessions`}
            onPress={() => nav.navigate('QuizHistory')}
          />
          <Row
            icon="LockKeyhole"
            title="Your anonymous posts"
            detail="Private to this local profile"
            onPress={() => setTab('Anonymous')}
          />
          {data.profile.admin && (
            <Row
              icon="ShieldCheck"
              title="Review student courses"
              detail="Local admin preview"
              onPress={() => nav.navigate('Admin')}
            />
          )}
        </>
      ) : (
        <>
          {tab === 'Anonymous' && (
            <Notice>
              This list is private to your profile. Anonymous posts hide your profile from
              classmates.
            </Notice>
          )}
          {mine.map((post) => (
            <PostCard key={post.id} post={post} />
          ))}
          {!mine.length && (
            <Empty
              title="Your first conversation awaits."
              body="Ask a question or share a study tip. You choose how to appear."
              action="Start a discussion"
              onPress={() => nav.navigate('Compose')}
            />
          )}
        </>
      )}
    </Page>
  );
}
export function SavedScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [tab, setTab] = useState('Materials');
  const materials = data.materials.filter((x) => data.saved.includes(x.id));
  const posts = data.posts.filter(
    (x) => data.saved.includes(x.id) && !data.blocked.includes(x.ownerId),
  );
  return (
    <Page>
      <Header title="Saved for later" />
      <View style={layout.inline}>
        {['Materials', 'Discussions'].map((value) => (
          <View key={value} style={{ flex: 1 }}>
            <Chip label={value} selected={tab === value} onPress={() => setTab(value)} />
          </View>
        ))}
      </View>
      {tab === 'Materials' ? (
        <>
          {materials.map((material) => (
            <Row
              key={material.id}
              title={material.title}
              detail={`${data.courses.find((x) => x.id === material.courseId)?.code ?? ''} · ${material.sample ? 'Sample lecture' : 'Imported PDF'}`}
              icon="FileText"
              onPress={() => nav.navigate('Document', { id: material.id })}
              right={
                <IconButton
                  icon="Bookmark"
                  label={`Unsave ${material.title}`}
                  selected
                  color={c.blue}
                  onPress={() =>
                    update((s) => ({ ...s, saved: s.saved.filter((x) => x !== material.id) }))
                  }
                />
              }
            />
          ))}
          {!materials.length && (
            <Empty
              title="Keep the good stuff close."
              body="Tap a bookmark on a lecture to find it here."
            />
          )}
        </>
      ) : (
        <>
          {posts.map((post) => (
            <PostCard key={post.id} post={post} />
          ))}
          {!posts.length && (
            <Empty
              title="Worth coming back to."
              body="Save helpful discussions and they’ll appear here."
            />
          )}
        </>
      )}
    </Page>
  );
}
export function EditProfileScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [name, setName] = useState(data.profile.name);
  const [department, setDepartment] = useState(data.profile.department);
  const [year, setYear] = useState(data.profile.year);
  return (
    <Page>
      <Header title="Edit profile" />
      <View style={{ alignItems: 'center', paddingVertical: 15 }}>
        <Avatar name={name || 'You'} size={90} />
      </View>
      <Field
        label="Display name"
        value={name}
        onChangeText={setName}
        maxLength={40}
        placeholder="Your name"
      />
      <T variant="label">Department</T>
      {['Computer Science', 'Mathematics', 'Engineering'].map((value) => (
        <Chip
          key={value}
          label={value}
          selected={department === value}
          onPress={() => setDepartment(value)}
        />
      ))}
      <T variant="label">Year</T>
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
      <Button
        label="Save profile"
        disabled={name.trim().length < 2}
        onPress={() => {
          update((s) => ({
            ...s,
            profile: { ...s.profile, name: name.trim(), department, year },
            posts: s.posts.map((p) => ({
              ...p,
              author: p.ownerId === 'me' ? name.trim() : p.author,
              comments: p.comments.map((comment) =>
                comment.ownerId === 'me' ? { ...comment, author: name.trim() } : comment,
              ),
            })),
          }));
          nav.goBack();
        }}
      />
    </Page>
  );
}
export function QuizHistoryScreen() {
  const nav = useNav();
  const { data } = useStore();
  return (
    <Page>
      <Header title="Quiz history" />
      <T variant="heading">Every small win counts.</T>
      {[...data.attempts].reverse().map((attempt) => (
        <Row
          key={attempt.id}
          icon="CheckCheck"
          title={`${attempt.correct} of ${attempt.total} correct`}
          detail={`${data.materials.find((x) => x.id === attempt.materialId)?.title ?? 'Practice'} · ${new Date(attempt.createdAt).toLocaleDateString()}`}
          onPress={() => nav.navigate('Results', { attemptId: attempt.id })}
        />
      ))}
      {!data.attempts.length && (
        <Empty
          title="Let’s get your first win."
          body="A few questions can make a topic much clearer."
          action="Try a sample quiz"
          onPress={() => nav.navigate('QuizSetup', { id: 'norm' })}
        />
      )}
    </Page>
  );
}
export function AdminScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [onlyPending, setOnlyPending] = useState(true);
  const courses = data.courses.filter(
    (x) => x.origin === 'student' && (!onlyPending || !x.recognised),
  );
  if (!data.profile.admin)
    return (
      <Page>
        <Header title="Course review" />
        <Notice>
          Enable the admin preview in Settings to explore course recognition. This is a local demo
          role, not real access control.
        </Notice>
      </Page>
    );
  return (
    <Page>
      <Header title="Course review" />
      <T variant="hero">Good work deserves recognition.</T>
      <T style={{ color: c.muted }}>
        Review student spaces. Recognition keeps their creator visible.
      </T>
      <Notice>Local admin preview. These actions change only this device’s sample data.</Notice>
      <View style={layout.inline}>
        <Chip label="Needs review" selected={onlyPending} onPress={() => setOnlyPending(true)} />
        <Chip
          label="All student courses"
          selected={!onlyPending}
          onPress={() => setOnlyPending(false)}
        />
      </View>
      {courses.map((course) => (
        <View
          key={course.id}
          style={{ gap: 13, paddingVertical: 18, borderBottomWidth: 1, borderBottomColor: c.line }}
        >
          <Pressable
            accessibilityRole="button"
            onPress={() => nav.navigate('Course', { id: course.id })}
            style={layout.inline}
          >
            <CourseIcon course={course} />
            <View style={{ flex: 1, gap: 4 }}>
              <T variant="label">{course.title}</T>
              <T variant="small">
                {course.code} · by {course.creator}
              </T>
              <Badge course={course} />
            </View>
            <Icon name="ChevronRight" color={c.blue} />
          </Pressable>
          <T>{course.description}</T>
          <Button
            label={course.recognised ? 'Remove recognition' : 'Recognise course'}
            kind={course.recognised ? 'secondary' : 'primary'}
            icon="ShieldCheck"
            onPress={() =>
              update((s) => ({
                ...s,
                courses: s.courses.map((x) =>
                  x.id === course.id ? { ...x, recognised: !x.recognised } : x,
                ),
              }))
            }
          />
        </View>
      ))}
      {!courses.length && (
        <Empty title="All caught up." body="Student-created courses will appear here for review." />
      )}
      <Button
        label="Create an admin-curated course"
        kind="secondary"
        icon="Plus"
        onPress={() => nav.navigate('CreateCourse')}
      />
    </Page>
  );
}
export function SettingsScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [panel, setPanel] = useState('');
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function clearDemo() {
    setBusy(true);
    try {
      if (Platform.OS !== 'web' && FileSystem.documentDirectory) {
        const files = data.materials.filter(
          (x) => !x.sample && x.uri?.startsWith(FileSystem.documentDirectory!),
        );
        for (const file of files) await FileSystem.deleteAsync(file.uri!, { idempotent: true });
      }
      update(() => ({ ...initialData, profile: { ...initialData.profile } }));
      nav.reset({ index: 0, routes: [{ name: 'Welcome' }] });
    } catch {
      setError('Couldn’t clear imported files. Please try again.');
    } finally {
      setBusy(false);
    }
  }
  return (
    <Page>
      <Header title="Settings" />
      <Section title="Account" />
      <Row
        icon="UserRound"
        title="Edit profile"
        detail={data.profile.email}
        onPress={() => nav.navigate('EditProfile')}
      />
      <Row
        icon="LockKeyhole"
        title="Password reset preview"
        detail="No real password is stored"
        onPress={() => nav.navigate('ResetPassword')}
      />
      <Section title="Your experience" />
      <Row icon="Sun" title="Appearance" detail="Light · the current design preview" />
      <Row
        icon="Bell"
        title="Study reminders"
        detail="Notification setup comes in a later version"
      />
      <Row
        icon="Users"
        title="Blocked accounts"
        detail={`${data.blocked.length} blocked on this device`}
        onPress={() => setPanel(panel === 'blocked' ? '' : 'blocked')}
      />
      {panel === 'blocked' && (
        <View style={layout.card}>
          {data.blocked.length ? (
            data.blocked.map((id, index) => (
              <Row
                key={id}
                icon="UserRound"
                title={`Blocked author ${index + 1}`}
                right={
                  <Button
                    label="Unblock"
                    kind="quiet"
                    onPress={() =>
                      update((s) => ({ ...s, blocked: s.blocked.filter((x) => x !== id) }))
                    }
                  />
                }
              />
            ))
          ) : (
            <T>No blocked accounts.</T>
          )}
        </View>
      )}
      <Row
        icon="ShieldCheck"
        title="About anonymous posting"
        onPress={() => setPanel(panel === 'privacy' ? '' : 'privacy')}
      />
      {panel === 'privacy' && (
        <Notice>
          Anonymous posts hide your profile from classmates. Your own anonymous history stays
          private. In a future connected service, authorised moderators will be able to identify
          authors. This local prototype has no server-side privacy boundary.
        </Notice>
      )}
      <Section title="Preview tools" />
      <View style={layout.inline}>
        <View style={{ flex: 1, gap: 4 }}>
          <T variant="label">Admin preview</T>
          <T variant="small">Try curating and recognising courses locally.</T>
        </View>
        <Switch
          accessibilityLabel="Enable local admin preview"
          value={data.profile.admin}
          onValueChange={(admin) => update((s) => ({ ...s, profile: { ...s.profile, admin } }))}
          trackColor={{ false: c.line, true: c.blue }}
          thumbColor={c.white}
        />
      </View>
      {data.profile.admin && (
        <Button
          label="Review student courses"
          kind="secondary"
          icon="ShieldCheck"
          onPress={() => nav.navigate('Admin')}
        />
      )}
      <Notice>
        Everything in this preview stays on this device. AI answers are prewritten samples, and
        account forms don’t authenticate with a server.
      </Notice>
      <Button
        label="Log out of preview"
        kind="secondary"
        icon="LogOut"
        onPress={() => {
          update((s) => ({ ...s, session: false }));
          nav.reset({ index: 0, routes: [{ name: 'Welcome' }] });
        }}
      />
      <Button
        label="Reset local demo data"
        kind="danger"
        icon="Trash2"
        onPress={() => setConfirm(true)}
      />
      {confirm && (
        <View style={[layout.card, { backgroundColor: c.redSoft }]}>
          <T variant="label">Clear your local changes?</T>
          <T>
            This removes imported PDFs, posts, votes, profile changes, and quiz results from this
            prototype, then restores the sample data.
          </T>
          <Button label="Yes, reset local data" kind="danger" busy={busy} onPress={clearDemo} />
          <Button label="Keep my data" kind="quiet" onPress={() => setConfirm(false)} />
        </View>
      )}
      {error && <Notice tone="error">{error}</Notice>}
      <T variant="small" style={{ textAlign: 'center', paddingVertical: 12 }}>
        Uni Companion · made for your next small win
      </T>
    </Page>
  );
}
