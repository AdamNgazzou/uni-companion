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
import { colors as c } from '../theme';
import { useSession } from '../auth/session';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import type { Course, CourseDetail, Job } from '../api/types';
import { useResource } from './hooks';
import { resourceGroups, resourceFormat } from './course-resources';
import { pending } from '../api/types';
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
  const sessions = useResource<{ data: Job[] }>('/study-jobs');
  const recent = sessions.data?.data.find((job) => pending(job.state)) || sessions.data?.data[0];
  const courses =
    resource.data?.data.filter(
      (x) => (all || x.joined) && x.title.toLowerCase().includes(search.toLowerCase()),
    ) ?? [];
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
      {recent && (
        <View style={[layout.card, { backgroundColor: c.blueSoft, gap: 12 }]}>
          <T variant="title">
            {pending(recent.state) ? 'Your study session is running' : 'Pick up where you left off'}
          </T>
          <T>{recent.title || 'Saved study session'}</T>
          <T variant="small">
            {recent.courseTitle} · {recent.scopeTitle}
          </T>
          <Button
            label={pending(recent.state) ? 'View progress' : 'Open last session'}
            onPress={() => router.push({ pathname: '/job', params: { id: recent.id } })}
          />
        </View>
      )}
      <Section title="Your courses" />
      <SearchField value={search} onChangeText={setSearch} placeholder="Find a course…" />
      <View style={layout.inline}>
        <Chip label="My courses" selected={!all} onPress={() => setAll(false)} />
        <Chip label="Discover" selected={all} onPress={() => setAll(true)} />
      </View>
      {all && <T variant="small">Explore a course, then choose whether to join.</T>}
      <ResourceState {...resource} retry={resource.reload} />
      {courses.map((course) => (
        <CourseCard
          key={course.id}
          course={course}
          onPress={() => router.push({ pathname: '/course', params: { id: course.id } })}
        />
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
  const preview = useResource<Course>(`/courses/${encodeURIComponent(id || '')}/preview`);
  const resource = useResource<CourseDetail>(
    preview.data?.canRead ? `/courses/${encodeURIComponent(id || '')}` : null,
  );
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState('');
  const course = resource.data;
  async function join() {
    setJoining(true);
    setError('');
    try {
      await api.request(`/courses/${id}/join`, { method: 'POST' });
      preview.reload();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setJoining(false);
    }
  }
  return (
    <Page>
      <Header title={preview.data?.canRead ? 'Course space' : 'Explore this course'} />
      <ResourceState {...preview} retry={preview.reload} />
      {preview.data && (
        <>
          <Hero
            title={preview.data.title}
            subtitle={
              preview.data.isCurated
                ? 'Admin curated · Horizon University'
                : preview.data.isRecognized
                  ? 'Recognised · student-created'
                  : 'Student-created course'
            }
            mascot={false}
          />
          {!!preview.data.description && <T>{preview.data.description}</T>}
          {!preview.data.canRead && (
            <>
              <T>
                {preview.data.materialCount} resources. Join to read the materials and use the study
                tools.
              </T>
              <Button label="Join this course" busy={joining} onPress={() => void join()} />
              {!!error && <Notice tone="error">{error}</Notice>}
            </>
          )}
          <Row
            title="Course discussions"
            detail="Ask classmates and share what helped"
            icon="MessageCircle"
            onPress={() => router.push({ pathname: '/(tabs)/discuss', params: { courseId: id } })}
          />
        </>
      )}
      {preview.data?.canRead && <ResourceState {...resource} retry={resource.reload} />}
      {course && (
        <>
          <Row
            title="Ask AI about this course"
            detail="An explanation grounded in your course PDFs"
            icon="Sparkle"
            onPress={() =>
              router.push({
                pathname: '/study',
                params: { courseId: id, kind: 'answer', title: course.title },
              })
            }
          />
          {!course.materials.length && (
            <Empty
              title="No materials yet"
              body="Resources will appear here once they are added to this course."
            />
          )}
          {resourceGroups(course).map((section) => (
            <View key={section.id} style={{ gap: 12 }}>
              <Section title={section.title} />
              {section.materials.map((material) => (
                <Row
                  key={material.id}
                  title={material.title}
                  detail={resourceFormat(material)}
                  icon={material.contentType === 'application/pdf' ? 'FileText' : 'Database'}
                  onPress={
                    material.contentType === 'application/pdf' && material.currentVersionId
                      ? () =>
                          router.push({
                            pathname: '/material',
                            params: {
                              courseId: id,
                              id: material.id,
                              versionId: material.currentVersionId!,
                              title: material.title,
                              chapterId: material.lessonId || '',
                            },
                          })
                      : undefined
                  }
                />
              ))}
            </View>
          ))}
          <Button
            label="Saved sessions for this course"
            kind="secondary"
            onPress={() => router.push({ pathname: '/history', params: { courseId: id } })}
          />
          <T variant="small">
            Student uploads are not available yet. PDF study tools use indexed text; other
            attachments are listed for context.
          </T>
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
          title={kind === 'summary' ? 'Summary' : kind === 'quiz' ? 'Quiz' : 'Ask AI'}
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
