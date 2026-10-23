import React, { useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import {
  Button,
  Chip,
  Empty,
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
} from '../ui';
import { colors as c, fonts } from '../theme';
import { useSession } from '../auth/session';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import type { Course, CourseDetail } from '../api/types';
import { useResource } from './hooks';
export function ResourceState({
  loading,
  error,
  retry,
}: {
  loading: boolean;
  error: string;
  retry: () => void;
}) {
  return (
    <>
      {loading && (
        <View style={{ padding: 22, alignItems: 'center', gap: 10 }}>
          <ActivityIndicator color={c.blue} />
          <T variant="small">Bringing your study space together…</T>
        </View>
      )}
      {!!error && (
        <>
          <Notice tone="error">{error}</Notice>
          <Button label="Try again" kind="secondary" onPress={retry} />
        </>
      )}
    </>
  );
}
function CourseCard({ course, onPress }: { course: Course; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={course.title}
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        gap: 14,
        padding: 16,
        borderRadius: 20,
        borderWidth: 1,
        borderColor: c.line,
        alignItems: 'center',
        opacity: pressed ? 0.7 : 1,
      })}
    >
      <View
        style={{
          width: 54,
          height: 54,
          borderRadius: 17,
          backgroundColor: c.blueSoft,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icon
          name={
            /data/i.test(course.title)
              ? 'Database'
              : /machine|cloud/i.test(course.title)
                ? 'Cpu'
                : 'BookOpen'
          }
          color={c.blue}
          size={28}
        />
      </View>
      <View style={{ flex: 1, gap: 5 }}>
        <T variant="label">{course.title}</T>
        <T variant="small" style={{ color: course.isCurated ? c.blue : c.muted }}>
          {course.isCurated
            ? 'Admin curated'
            : course.isRecognized
              ? 'Recognised · student-created'
              : 'Student-created'}{' '}
          · {course.materialCount} resources
        </T>
      </View>
      <Icon name="ChevronRight" color={c.muted} size={20} />
    </Pressable>
  );
}
export function CoursesScreen() {
  const { profile } = useSession();
  const resource = useResource<{ data: Course[] }>('/courses?limit=100');
  const [all, setAll] = useState(false);
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [joining, setJoining] = useState<string | null>(null);
  const courses =
    resource.data?.data.filter(
      (x) => (all || x.joined) && x.title.toLowerCase().includes(search.toLowerCase()),
    ) ?? [];
  async function open(course: Course) {
    if (joining) return;
    setJoining(course.id);
    setError('');
    try {
      if (!course.joined) await api.request(`/courses/${course.id}/join`, { method: 'POST' });
      router.push({ pathname: '/course', params: { id: course.id } });
    } catch (e) {
      setError(errorText(e));
    } finally {
      setJoining(null);
    }
  }
  return (
    <Page>
      <View style={[layout.inline, { justifyContent: 'space-between' }]}>
        <Logo />
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Your profile"
          onPress={() => router.push('/profile')}
          style={{ padding: 12, borderRadius: 15, backgroundColor: c.orangeSoft }}
        >
          <Icon name="UserRound" color={c.ink} />
        </Pressable>
      </View>
      <Hero
        title={`A little progress, ${profile?.displayName.split(' ')[0] || 'friend'}?`}
        subtitle="Your next ‘oh, I get it’ starts here."
      />
      {resource.data?.data.some((x) => x.joined) && (
        <View style={[layout.card, { backgroundColor: c.blue, gap: 16 }]}>
          <T variant="small" style={{ color: '#E3EDFF', fontFamily: fonts.bold }}>
            ONE SMALL WIN AT A TIME
          </T>
          <T variant="heading" style={{ color: c.white }}>
            Make it click.
          </T>
          <T style={{ color: c.white }}>
            Pick a resource. Ask a question. Leave with a clearer idea.
          </T>
          <Button
            label="Your saved study sessions"
            kind="secondary"
            onPress={() => router.push('/history')}
            icon="ArrowRight"
          />
        </View>
      )}
      <Section title="Your courses" />
      <SearchField value={search} onChangeText={setSearch} placeholder="Find a course…" />
      <View style={layout.inline}>
        <Chip label="My courses" selected={!all} onPress={() => setAll(false)} />
        <Chip label="Discover" selected={all} onPress={() => setAll(true)} />
      </View>
      {all && <T variant="small">Tap a course to join its study space.</T>}
      <ResourceState {...resource} retry={resource.reload} />
      {!!error && <Notice tone="error">{error}</Notice>}
      {joining && <ActivityIndicator color={c.blue} />}
      {courses.map((course) => (
        <CourseCard key={course.id} course={course} onPress={() => void open(course)} />
      ))}
      {!resource.loading && !resource.error && !courses.length && (
        <Empty
          title={search ? 'No matches yet.' : 'Your semester starts here.'}
          body={
            search
              ? 'Try another course title.'
              : 'Discover courses and join the ones you’re studying.'
          }
          action="Explore courses"
          onPress={() => {
            setAll(true);
            setSearch('');
          }}
        />
      )}
    </Page>
  );
}
export function CourseScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const resource = useResource<CourseDetail>(`/courses/${encodeURIComponent(id || '')}`);
  const course = resource.data;
  return (
    <Page>
      <Header title="Course space" />
      <ResourceState {...resource} retry={resource.reload} />
      {course && (
        <>
          <Hero
            title={course.title}
            subtitle={
              course.isCurated ? 'Admin curated · Horizon University' : 'Student-created course'
            }
            mascot={false}
          />
          <View style={[layout.card, { backgroundColor: c.orangeSoft }]}>
            <T variant="title">Stuck on something?</T>
            <T>Ask about your course and follow the explanation back to its source.</T>
            <Button
              label="Ask your course companion"
              onPress={() =>
                router.push({ pathname: '/study', params: { courseId: id, kind: 'answer' } })
              }
              icon="MessageCircle"
            />
          </View>
          <Section title="Course materials" />
          {!course.materials.length && (
            <Empty
              title="A little quiet here."
              body="Materials will appear here once they’re added to this course."
            />
          )}
          {course.materials.map((material) => (
            <Row
              key={material.id}
              title={material.title}
              detail={
                material.contentType === 'application/pdf'
                  ? 'PDF · Read, summarise or practise'
                  : 'Dataset · study tools support PDFs'
              }
              icon={material.contentType === 'application/pdf' ? 'FileText' : 'Database'}
              onPress={
                material.contentType === 'application/pdf' && material.currentVersionId
                  ? () =>
                      router.push({
                        pathname: '/material',
                        params: {
                          courseId: id,
                          id: material.id,
                          versionId: material.currentVersionId,
                          title: material.title,
                          chapterId: material.lessonId || '',
                        },
                      })
                  : undefined
              }
            />
          ))}
          <Button
            label="Saved sessions for this course"
            kind="secondary"
            onPress={() => router.push({ pathname: '/history', params: { courseId: id } })}
          />
          <Notice>Student uploads and course discussions are coming in a later update.</Notice>
        </>
      )}
    </Page>
  );
}
export function MaterialScreen() {
  const params = useLocalSearchParams<{
    id: string;
    courseId: string;
    versionId: string;
    title: string;
    chapterId: string;
  }>();
  return (
    <Page>
      <Header title="Your study resource" />
      <Hero
        title={params.title || 'Course material'}
        subtitle="One resource. A few ways to understand it."
        mascot={false}
      />
      <Row
        icon="FileText"
        title="Read the original"
        detail="Open the PDF, one page at a time"
        onPress={() =>
          router.push({
            pathname: '/reader',
            params: { materialId: params.id, versionId: params.versionId, page: '1' },
          })
        }
      />
      {(['summary', 'answer', 'quiz'] as const).map((kind) => (
        <Row
          key={kind}
          icon={kind === 'summary' ? 'BookOpen' : kind === 'quiz' ? 'HelpCircle' : 'MessageCircle'}
          title={
            kind === 'summary'
              ? 'The key ideas'
              : kind === 'quiz'
                ? 'Test what clicked'
                : 'Ask a question'
          }
          detail={
            kind === 'summary'
              ? 'A saved summary with links to the source'
              : kind === 'quiz'
                ? 'Practice questions, then helpful corrections'
                : 'An explanation grounded in this document'
          }
          onPress={() =>
            router.push({
              pathname: '/study',
              params: {
                courseId: params.courseId,
                materialId: params.id,
                chapterId: params.chapterId,
                kind,
                title: params.title,
              },
            })
          }
        />
      ))}
      <Notice>
        AI study aids can miss nuance. Use the linked original pages to check your understanding.
      </Notice>
    </Page>
  );
}
