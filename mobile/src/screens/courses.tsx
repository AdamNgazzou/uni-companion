import React, { useState } from 'react';
import { Pressable, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import {
  Badge,
  Button,
  Chip,
  CourseIcon,
  CourseRow,
  Empty,
  Field,
  Header,
  Hero,
  Icon,
  Logo,
  Notice,
  Page,
  Row,
  SearchField,
  Section,
  T,
  layout,
  useNav,
} from '../ui';
import { colors as c, fonts } from '../theme';
import { uid, useStore } from '../store';
import { RootStack } from '../types';
export function HomeScreen() {
  const nav = useNav();
  const { data, storageError } = useStore();
  const [search, setSearch] = useState('');
  const [all, setAll] = useState(false);
  const courses = data.courses.filter(
    (x) =>
      (all || data.joined.includes(x.id)) &&
      `${x.title} ${x.code}`.toLowerCase().includes(search.toLowerCase()),
  );
  return (
    <Page>
      <View style={[layout.inline, { justifyContent: 'space-between', paddingTop: 8 }]}>
        <Logo />
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Open your profile"
          onPress={() => nav.navigate('Tabs', { screen: 'You' })}
          style={{
            backgroundColor: c.orangeSoft,
            width: 43,
            height: 43,
            borderRadius: 16,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <T variant="title" style={{ color: '#975313' }}>
            {data.profile.name[0].toUpperCase()}
          </T>
        </Pressable>
      </View>
      {storageError && (
        <Notice tone="error">
          Your changes couldn’t be saved. This device may be out of storage.
        </Notice>
      )}
      <Hero
        title={`A little progress, ${data.profile.name.split(' ')[0]}?`}
        subtitle="Your next ‘oh, I get it’ starts here."
      />
      {data.joined.includes('db') && (
        <View
          style={{
            backgroundColor: c.blue,
            borderRadius: 23,
            padding: 21,
            gap: 14,
            overflow: 'hidden',
          }}
        >
          <View style={layout.inline}>
            <View style={{ flex: 1, gap: 6 }}>
              <T variant="small" style={{ color: '#DDE8FF', fontFamily: fonts.bold }}>
                Pick up a small win
              </T>
              <T variant="heading" style={{ color: c.white }}>
                Database Systems
              </T>
              <T style={{ color: '#F1F5FF' }}>Normalization · Lecture 06</T>
            </View>
            <View style={{ backgroundColor: '#2E73FF', padding: 12, borderRadius: 20 }}>
              <Icon name="Database" size={40} color={c.white} />
            </View>
          </View>
          <Button
            label={data.attempts.length ? 'Keep practising' : 'Let’s understand it'}
            kind="secondary"
            icon="ArrowRight"
            onPress={() => nav.navigate('Summary', { id: 'norm' })}
          />
        </View>
      )}
      <Section
        title="Your courses"
        action="Manage"
        onPress={() => nav.navigate('SelectCourses', { editing: true })}
      />
      <SearchField value={search} onChangeText={setSearch} placeholder="Find a course…" />
      <View style={layout.inline}>
        <Chip label="My courses" selected={!all} onPress={() => setAll(false)} />
        <Chip label="Discover" selected={all} onPress={() => setAll(true)} />
      </View>
      {courses.map((course) => (
        <CourseRow
          key={course.id}
          course={course}
          onPress={() => nav.navigate('Course', { id: course.id })}
        />
      ))}
      {!courses.length && (
        <Empty
          title={search ? 'No matches yet.' : 'Your semester starts here.'}
          body={
            search
              ? 'Try a title or course code.'
              : 'Choose courses curated by the admin or created by your classmates.'
          }
          action="Explore courses"
          onPress={() => {
            setSearch('');
            setAll(true);
          }}
        />
      )}
      <Button
        label="Create a course space"
        kind="secondary"
        icon="Plus"
        onPress={() => nav.navigate('CreateCourse')}
      />
      <T variant="small" style={{ textAlign: 'center', marginTop: 3 }}>
        A little effort. A brighter you.
      </T>
    </Page>
  );
}
export function CourseScreen({ route }: NativeStackScreenProps<RootStack, 'Course'>) {
  const nav = useNav();
  const { data, update } = useStore();
  const [tab, setTab] = useState<'Study' | 'Discuss'>('Study');
  const course = data.courses.find((x) => x.id === route.params.id);
  if (!course)
    return (
      <Page>
        <Header title="Course" />
        <Empty title="Course not found" body="Go back and choose another course." />
      </Page>
    );
  const joined = data.joined.includes(course.id);
  const materials = data.materials.filter((x) => x.courseId === course.id);
  const posts = data.posts.filter((x) => x.courseId === course.id);
  return (
    <Page>
      <Header title={course.code} />
      <View style={layout.inline}>
        <CourseIcon course={course} size={72} />
        <View style={{ flex: 1, gap: 5 }}>
          <T variant="heading">{course.title}</T>
          <Badge course={course} />
        </View>
      </View>
      <T style={{ color: c.muted }}>{course.description}</T>
      <T variant="small">
        Created by {course.creator}
        {course.origin === 'student' && course.recognised ? ' · reviewed by the course admin' : ''}
      </T>
      {!joined && (
        <Button
          label="Join this course"
          icon="Plus"
          onPress={() => update((s) => ({ ...s, joined: [...s.joined, course.id] }))}
        />
      )}
      <View
        style={{ flexDirection: 'row', backgroundColor: c.blueSoft, padding: 5, borderRadius: 16 }}
      >
        {(['Study', 'Discuss'] as const).map((value) => (
          <Pressable
            key={value}
            accessibilityRole="tab"
            accessibilityState={{ selected: value === tab }}
            onPress={() => setTab(value)}
            style={{
              flex: 1,
              backgroundColor: value === tab ? c.blue : 'transparent',
              borderRadius: 12,
              padding: 12,
              alignItems: 'center',
            }}
          >
            <T variant="label" style={{ color: value === tab ? c.white : c.muted }}>
              {value}
            </T>
          </Pressable>
        ))}
      </View>
      {tab === 'Study' ? (
        <>
          <Section
            title="Your lectures"
            action="Add PDF"
            onPress={() => nav.navigate('Upload', { courseId: course.id })}
          />
          {materials.map((material, index) => (
            <Pressable
              key={material.id}
              accessibilityRole="button"
              onPress={() => nav.navigate('Document', { id: material.id })}
              style={{
                flexDirection: 'row',
                gap: 14,
                borderBottomWidth: 1,
                borderBottomColor: c.line,
                paddingVertical: 16,
                alignItems: 'center',
              }}
            >
              <View
                style={{
                  width: 43,
                  height: 43,
                  borderRadius: 14,
                  backgroundColor: index === 0 ? c.blueSoft : c.canvas,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <T variant="label" style={{ color: c.blue }}>
                  {String(index + 1).padStart(2, '0')}
                </T>
              </View>
              <View style={{ flex: 1, gap: 5 }}>
                <T variant="label">{material.title}</T>
                <T variant="small">
                  {material.sample ? 'Sample lecture' : 'Your PDF'}
                  {material.pages ? ` · ${material.pages} pages` : ''}
                </T>
              </View>
              <Icon name="ChevronRight" size={21} color={c.blue} />
            </Pressable>
          ))}
          {materials.some((x) => x.id === 'norm') && (
            <View style={layout.card}>
              <T variant="title">Make sense of your notes.</T>
              <T style={{ color: c.muted }}>
                A clear summary. A few questions. One less confusing topic.
              </T>
              <View style={layout.inline}>
                <View style={{ flex: 1 }}>
                  <Button
                    label="Summary"
                    kind="secondary"
                    icon="FileText"
                    onPress={() => nav.navigate('Summary', { id: 'norm' })}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Button
                    label="Quiz"
                    kind="secondary"
                    icon="HelpCircle"
                    onPress={() => nav.navigate('QuizSetup', { id: 'norm' })}
                  />
                </View>
              </View>
            </View>
          )}
          {!materials.length && (
            <Empty
              title="The first page is yours."
              body="Add lecture notes to start building this course library."
              action="Add a PDF"
              onPress={() => nav.navigate('Upload', { courseId: course.id })}
            />
          )}
        </>
      ) : (
        <>
          <Section
            title="The course crowd"
            action="Ask a question"
            onPress={() => nav.navigate('Compose', { courseId: course.id })}
          />
          {posts.map((post) => (
            <Row
              key={post.id}
              icon="MessageCircle"
              title={post.title}
              detail={`${post.anonymous ? 'Anonymous' : post.author} · ${post.comments.length} replies`}
              onPress={() => nav.navigate('Thread', { id: post.id })}
            />
          ))}
          {!posts.length && (
            <Empty
              title="Break the ice."
              body="Ask a question or share something that helped you."
              action="Start a discussion"
              onPress={() => nav.navigate('Compose', { courseId: course.id })}
            />
          )}
        </>
      )}
      {data.profile.admin && course.origin === 'student' && (
        <Button
          label={course.recognised ? 'Remove recognition' : 'Recognise this course'}
          kind="secondary"
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
      )}
    </Page>
  );
}
export function CreateCourseScreen() {
  const nav = useNav();
  const { data, update } = useStore();
  const [title, setTitle] = useState('');
  const [code, setCode] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');
  return (
    <Page>
      <Header title="Create a course" />
      <Hero title="Make room for an idea." subtitle="A useful space starts with a clear name." />
      <Notice>
        {data.profile.admin
          ? 'Admin preview: this course will be labelled Admin curated.'
          : 'Your course will show your name and a Student-created label. The admin can recognise it after review.'}
      </Notice>
      <Field
        label="Course title"
        value={title}
        onChangeText={setTitle}
        placeholder="e.g. Data Structures"
        maxLength={80}
      />
      <Field
        label="Course code"
        value={code}
        onChangeText={setCode}
        placeholder="e.g. CSC 302"
        autoCapitalize="characters"
        maxLength={20}
      />
      <Field
        label="About this course"
        value={description}
        onChangeText={setDescription}
        multiline
        placeholder="What will people learn here?"
        style={{ minHeight: 100, textAlignVertical: 'top' }}
        maxLength={500}
      />
      {error ? <Notice tone="error">{error}</Notice> : null}
      <Button
        label="Create course space"
        icon="Plus"
        onPress={() => {
          if (title.trim().length < 3 || !code.trim()) {
            setError('Add a course title and code.');
            return;
          }
          if (
            data.courses.some(
              (x) =>
                x.code.toLowerCase() === code.trim().toLowerCase() &&
                x.title.toLowerCase() === title.trim().toLowerCase(),
            )
          ) {
            setError('That course already exists. Choose it from Discover.');
            return;
          }
          const id = uid();
          update((s) => ({
            ...s,
            courses: [
              ...s.courses,
              {
                id,
                title: title.trim(),
                code: code.trim().toUpperCase(),
                description: description.trim() || 'A new space to learn together.',
                creator: s.profile.name,
                origin: s.profile.admin ? 'admin' : 'student',
                recognised: s.profile.admin,
                color: c.blueSoft,
                icon: 'database',
              },
            ],
            joined: [...s.joined, id],
          }));
          nav.replace('Course', { id });
        }}
      />
    </Page>
  );
}
