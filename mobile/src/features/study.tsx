import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, AppState, Pressable, ScrollView, View } from 'react-native';
import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import {
  Button,
  Chip,
  Empty,
  Field,
  Header,
  Hero,
  Icon,
  IconButton,
  Notice,
  Page,
  Row,
  Section,
  T,
  layout,
} from '../ui';
import { colors as c } from '../theme';
import { api, newKey } from '../api/runtime';
import { errorText } from '../api/client';
import { jobPoller } from '../api/polling';
import {
  pending,
  type Attempt,
  type Job,
  type StudyRequest,
  type CourseDetail,
} from '../api/types';
import { sourceScope } from './course-resources';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';
import PdfCanvas from '../pdf/PdfCanvas';
import { base64, physicalPage } from '../pdf/bytes';
import { useSession } from '../auth/session';
import { drafts } from '../study/draft-runtime';
import type { QuizDraft } from '../study/drafts';
import { PracticePlayer } from './practice';
import { Citations } from '../study/citations';
import { StudyArticle, StudyProgress, HistoryEntry } from '../study/presentation';
export function StudySetupScreen() {
  const params = useLocalSearchParams<{
    courseId: string;
    materialId?: string;
    chapterId?: string;
    kind: string;
    title?: string;
    language?: string;
  }>();
  const kind = ['answer', 'summary', 'quiz'].includes(params.kind)
    ? (params.kind as StudyRequest['kind'])
    : 'answer';
  const [question, setQuestion] = useState('');
  const [language, setLanguage] = useState<'en' | 'fr'>(params.language === 'fr' ? 'fr' : 'en');
  const [count, setCount] = useState<5 | 10>(5);
  const [chapter, setChapter] = useState(false);
  const scope = useResource<CourseDetail>(params.courseId ? `/courses/${params.courseId}` : null);
  const included = scope.data
    ? sourceScope(scope.data, params.materialId, chapter ? params.chapterId : undefined)
    : [];
  const sectionTitle = scope.data?.structure.chapters?.find(
    (item) => item.id === params.chapterId,
  )?.title;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const key = useRef(newKey());
  const last = useRef('');
  async function start() {
    setBusy(true);
    setError('');
    const request: StudyRequest = {
      kind,
      language,
      ...(kind === 'answer' ? { question: question.trim() } : {}),
      ...(chapter && params.chapterId
        ? { chapterId: params.chapterId }
        : params.materialId
          ? { materialId: params.materialId }
          : {}),
      ...(kind === 'quiz' ? { count } : {}),
    };
    const json = JSON.stringify(request);
    if (last.current && last.current !== json) key.current = newKey();
    last.current = json;
    try {
      const job = await api.request<Job>(`/courses/${params.courseId}/study-jobs`, {
        method: 'POST',
        headers: { 'Idempotency-Key': key.current },
        body: json,
      });
      router.replace({ pathname: '/job', params: { id: job.id } });
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Page
      footer={
        <Button
          label={
            kind === 'answer'
              ? 'Help me understand'
              : kind === 'summary'
                ? 'Build my summary'
                : 'Let’s practise'
          }
          busy={busy}
          disabled={
            scope.loading ||
            !!scope.error ||
            (kind === 'answer'
              ? !question.trim()
              : !included.some(
                  (material) => material.contentType === 'application/pdf' && material.indexed,
                ))
          }
          onPress={() => void start()}
          icon="ArrowRight"
        />
      }
    >
      <Header
        title={
          kind === 'answer'
            ? 'Ask your companion'
            : kind === 'summary'
              ? 'Summary'
              : 'A little practice'
        }
      />
      <Hero
        title={
          kind === 'answer'
            ? 'What hasn’t clicked yet?'
            : kind === 'summary'
              ? 'Make the big picture clearer.'
              : 'See what stuck.'
        }
        subtitle={params.title || 'Grounded in the materials in this course.'}
      />
      {kind === 'answer' && (
        <Field
          label="Your question"
          placeholder="e.g. How is supervised learning different?"
          value={question}
          onChangeText={setQuestion}
          multiline
          maxLength={2000}
          style={{ minHeight: 130, textAlignVertical: 'top' }}
        />
      )}
      <Section title="Study language" />
      <View style={layout.inline}>
        <Chip label="English" selected={language === 'en'} onPress={() => setLanguage('en')} />
        <Chip label="Français" selected={language === 'fr'} onPress={() => setLanguage('fr')} />
      </View>
      {kind !== 'answer' && !!params.chapterId && (
        <>
          <Section title="Cover what?" />
          <Chip label="This document" selected={!chapter} onPress={() => setChapter(false)} />
          <Chip
            label={sectionTitle || 'This section'}
            selected={chapter}
            onPress={() => setChapter(true)}
          />
        </>
      )}
      <Section title="Sources for this session" />
      <ResourceState {...scope} retry={scope.reload} />
      {included.map((material) => (
        <T key={material.id} variant="small">
          {material.title} ·{' '}
          {material.contentType !== 'application/pdf'
            ? 'Not used: only PDFs are supported'
            : material.indexed
              ? 'Indexed PDF'
              : 'PDF indexing pending'}
        </T>
      ))}
      {kind === 'quiz' && (
        <>
          <Section title="How many questions?" />
          <View style={layout.inline}>
            {([5, 10] as const).map((value) => (
              <Chip
                key={value}
                label={`${value} questions`}
                selected={count === value}
                onPress={() => setCount(value)}
              />
            ))}
          </View>
        </>
      )}
      <Notice>
        Your session is saved on the server. You can leave the app and return through Study history.
      </Notice>
      {!!error && <Notice tone="error">{error}</Notice>}
    </Page>
  );
}
function useJob(id: string) {
  const [job, setJob] = useState<Job | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useFocusEffect(
    useCallback(() => {
      // Revision explicitly restarts the external polling subscription after a manual retry.
      void revision;
      const poller = jobPoller(
        (signal) => api.request<Job>(`/study-jobs/${id}`, { signal }),
        (value) => {
          setJob(value);
          setError('');
        },
        (e) => setError(errorText(e)),
      );
      if (AppState.currentState !== 'background') poller.start();
      const subscription = AppState.addEventListener('change', (state) => {
        if (state === 'active') poller.start();
        else poller.stop();
      });
      return () => {
        poller.stop();
        subscription.remove();
      };
    }, [id, revision]),
  );
  return { job: job?.id === id ? job : null, error, reload: () => setRevision((x) => x + 1) };
}
function QuizSession({
  id,
  questions,
  prior,
  initialDraft,
  owner,
  retryId,
}: {
  initialDraft: QuizDraft | null;
  owner: string;
  retryId?: string;
  id: string;
  questions: import('../api/types').Question[];
  prior?: Attempt;
}) {
  const [answers, setAnswers] = useState<Record<string, string>>(() => ({
    ...initialDraft?.answers,
    ...Object.fromEntries(
      prior?.corrections
        .filter((q) => q.selectedOptionId === q.correctOptionId)
        .map((q) => [q.questionId, q.selectedOptionId]) || [],
    ),
  }));
  const [index, setIndex] = useState(() => {
    const eligible = prior
      ? questions.filter((q) =>
          prior.corrections.some(
            (a) => a.questionId === q.id && a.selectedOptionId !== a.correctOptionId,
          ),
        )
      : questions;
    return Math.max(
      0,
      eligible.findIndex((q) => q.id === initialDraft?.questionId),
    );
  });
  const [busy, setBusy] = useState(false);
  const [submitError, setSubmitError] = useState('');
  const attemptKey = useRef(initialDraft?.attemptKey || newKey());
  const previous = useRef(initialDraft?.signature || '');
  const submitted = useRef(false);
  const [saved, setSaved] = useState(false);
  const [saveError, setSaveError] = useState('');
  const visible = prior
    ? questions.filter((q) =>
        prior.corrections.some(
          (correction) =>
            correction.questionId === q.id &&
            correction.selectedOptionId !== correction.correctOptionId,
        ),
      )
    : questions;
  const question = visible[index];
  const questionId = question?.id;
  useEffect(() => {
    if (submitted.current) return;
    let active = true;
    drafts
      .save(
        owner,
        id,
        {
          version: 1,
          answers,
          questionId,
          attemptKey: attemptKey.current,
          signature: previous.current,
          updatedAt: Date.now(),
        },
        retryId,
      )
      .then(() => {
        if (active) {
          setSaved(true);
          setSaveError('');
        }
      })
      .catch(() => {
        if (active)
          setSaveError(
            'Progress could not be saved on this device. Keep this screen open to finish your quiz.',
          );
      });
    return () => {
      active = false;
    };
  }, [answers, questionId, owner, id, retryId]);
  async function submit() {
    setBusy(true);
    setSubmitError('');
    const body = JSON.stringify({
      answers: questions.map((q) => ({ questionId: q.id, optionId: answers[q.id] })),
    });
    if (previous.current && previous.current !== body) attemptKey.current = newKey();
    previous.current = body;
    try {
      await drafts.save(
        owner,
        id,
        {
          version: 1,
          answers,
          questionId,
          attemptKey: attemptKey.current,
          signature: body,
          updatedAt: Date.now(),
        },
        retryId,
      );
    } catch {
      setSaveError(
        'Progress could not be saved on this device. Keep this screen open until your result appears.',
      );
    }
    try {
      const attempt = await api.request<Attempt>(`/quizzes/${id}/attempts`, {
        method: 'POST',
        body,
        headers: { 'Idempotency-Key': attemptKey.current },
      });
      submitted.current = true;
      await drafts.remove(owner, id, retryId).catch(() => {});
      router.replace({ pathname: '/attempt', params: { id: attempt.id } });
    } catch (e) {
      setSubmitError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      {prior && (
        <Notice>
          Your previously correct answers carry forward. The final score covers the full quiz.
        </Notice>
      )}
      {initialDraft && Object.keys(initialDraft.answers).length > 0 && (
        <Notice>Welcome back. Your saved choices are right here.</Notice>
      )}
      {!!saveError && <Notice tone="error">{saveError}</Notice>}
      {question && (
        <>
          <View style={[layout.inline, { justifyContent: 'space-between' }]}>
            <T variant="small">{saved ? 'Saved on this device' : 'Saving progress…'}</T>
            {saved ? (
              <Icon name="CheckCircle2" color={c.green} size={16} />
            ) : (
              <ActivityIndicator color={c.muted} />
            )}
          </View>
          <T variant="small">
            Question {index + 1} of {visible.length} ·{' '}
            {visible.filter((q) => !!answers[q.id]).length} answered
          </T>
          <View style={{ height: 8, backgroundColor: c.blueSoft, borderRadius: 8 }}>
            <View
              style={{
                width: `${((index + 1) / visible.length) * 100}%`,
                height: 8,
                borderRadius: 8,
                backgroundColor: c.blue,
              }}
            />
          </View>
          <T variant="heading">{question.question}</T>
          {question.options.map((option, i) => (
            <Pressable
              key={option.id}
              accessibilityRole="radio"
              accessibilityState={{ checked: answers[question.id] === option.id }}
              aria-checked={answers[question.id] === option.id}
              onPress={() => {
                setSaved(false);
                setAnswers((value) => ({ ...value, [question.id]: option.id }));
              }}
              style={({ pressed }) => ({
                minHeight: 64,
                borderRadius: 16,
                padding: 16,
                gap: 14,
                flexDirection: 'row',
                alignItems: 'center',
                backgroundColor: answers[question.id] === option.id ? c.blueSoft : c.white,
                borderWidth: 2,
                borderColor: answers[question.id] === option.id ? c.blue : c.line,
                opacity: pressed ? 0.75 : 1,
              })}
            >
              <T variant="label" style={{ color: c.blue }}>
                {String.fromCharCode(65 + i)}
              </T>
              <T style={{ flex: 1 }}>{option.text}</T>
              {answers[question.id] === option.id && <Icon name="Check" color={c.blue} />}
            </Pressable>
          ))}
          <View style={layout.inline}>
            <View style={{ flex: 1 }}>
              <Button
                label="Previous"
                kind="secondary"
                disabled={index === 0}
                onPress={() => {
                  setSaved(false);
                  setIndex((i) => i - 1);
                }}
              />
            </View>
            <View style={{ flex: 1 }}>
              <Button
                label={index < visible.length - 1 ? 'Next' : 'See my result'}
                busy={busy}
                disabled={
                  index < visible.length - 1
                    ? !answers[question.id]
                    : questions.some((q) => !answers[q.id])
                }
                onPress={() => {
                  if (index < visible.length - 1) {
                    setSaved(false);
                    setIndex((i) => i + 1);
                  } else void submit();
                }}
              />
            </View>
          </View>
        </>
      )}
      {!!submitError && <Notice tone="error">{submitError}</Notice>}
    </>
  );
}

function QuizPlayer(props: {
  id: string;
  questions: import('../api/types').Question[];
  prior?: Attempt;
  retryId?: string;
}) {
  const { profile } = useSession();
  const [loaded, setLoaded] = useState<{
    draft: QuizDraft | null;
    scope: string;
    warning?: string;
  } | null>(null);
  const scope = `${profile?.id}:${props.id}:${props.retryId || 'full'}`;
  useEffect(() => {
    let active = true;
    if (profile)
      drafts
        .read(profile.id, props.id, props.questions, props.retryId)
        .then((draft) => {
          if (active) setLoaded({ draft, scope });
        })
        .catch(() => {
          if (active)
            setLoaded({
              draft: null,
              scope,
              warning: 'Saved progress could not be read. You can still complete this quiz.',
            });
        });
    return () => {
      active = false;
    };
  }, [profile, props.id, props.questions, props.retryId, scope]);
  if (!profile || loaded?.scope !== scope) return <ActivityIndicator color={c.blue} />;
  return (
    <>
      {loaded.warning && <Notice tone="error">{loaded.warning}</Notice>}
      <QuizSession
        {...props}
        owner={profile.id}
        initialDraft={loaded.draft}
        retryId={props.retryId}
      />
    </>
  );
}

export function JobScreen() {
  const { id, retryAttemptId } = useLocalSearchParams<{ id: string; retryAttemptId?: string }>();
  const { job, error, reload } = useJob(id);
  const [submitError, setSubmitError] = useState('');
  const retry = useResource<Attempt>(retryAttemptId ? `/quiz-attempts/${retryAttemptId}` : null);
  return (
    <Page>
      <Header
        title={
          job?.kind === 'practice'
            ? 'Written & coding practice'
            : job?.kind === 'quiz'
              ? 'Practice time'
              : job?.kind === 'summary'
                ? 'Your summary'
                : 'Let’s understand it'
        }
      />
      {!!error && (
        <>
          <Notice tone="error">{error}</Notice>
          <Button label="Reconnect" onPress={reload} kind="secondary" />
        </>
      )}
      {!job && !error && <ActivityIndicator color={c.blue} />}
      {job && (
        <>
          {job.stale && (
            <Notice>
              This session uses an earlier version of the source materials. Its links open the
              original pages used.
            </Notice>
          )}
          {pending(job.state) ? (
            <>
              <StudyProgress job={job} />
              <Button
                label="Cancel this session"
                kind="quiet"
                onPress={() => {
                  void api
                    .request(`/study-jobs/${id}/cancel`, { method: 'POST' })
                    .then(reload)
                    .catch((e) => setSubmitError(errorText(e)));
                }}
              />
            </>
          ) : job.state !== 'succeeded' ? (
            <Empty
              title={job.state === 'cancelled' ? 'Session cancelled.' : 'That didn’t quite click.'}
              body={
                job.state === 'cancelled'
                  ? 'Your other study sessions are still saved.'
                  : `We couldn’t finish this session (${(job.errorCode || 'service unavailable').replaceAll('_', ' ')}). You can start a new one.`
              }
              action="Back to study history"
              onPress={() => router.replace('/history')}
            />
          ) : (
            <>
              {job.kind !== 'quiz' && job.kind !== 'practice' && (
                <StudyArticle
                  job={job}
                  renderCitations={(citations) => <Citations citations={citations} />}
                />
              )}
              {job.kind === 'practice' && job.result?.practice && (
                <PracticePlayer key={id} id={id} artifact={job.result.practice} />
              )}
              {job.kind === 'quiz' && (
                <>
                  <ResourceState {...retry} retry={retry.reload} />
                  {(!retryAttemptId || retry.data?.jobId === id) && (
                    <QuizPlayer
                      key={retryAttemptId || id}
                      id={id}
                      questions={job.result?.questions || []}
                      prior={retry.data || undefined}
                      retryId={retryAttemptId}
                    />
                  )}
                </>
              )}
            </>
          )}
          {!!submitError && <Notice tone="error">{submitError}</Notice>}
        </>
      )}
    </Page>
  );
}
export function HistoryScreen() {
  const { courseId } = useLocalSearchParams<{ courseId?: string }>();
  const [tab, setTab] = useState<'sessions' | 'attempts'>('sessions');
  const [page, setPage] = useState(1);
  const sessions = useResource<{ data: Job[] }>(
    `/study-jobs${courseId ? `?courseId=${courseId}` : ''}`,
  );
  const attempts = useResource<{ data: Attempt[] }>(
    `/quiz-attempts?page=${page}${courseId ? `&courseId=${courseId}` : ''}`,
  );
  const resource = tab === 'sessions' ? sessions : attempts;
  return (
    <Page>
      <Header title="Your study history" />
      <Hero
        title="Your progress lives here."
        subtitle="Pick up a session or revisit a breakthrough."
        mascot={false}
      />
      <View style={layout.inline}>
        <Chip
          label="Study sessions"
          selected={tab === 'sessions'}
          onPress={() => setTab('sessions')}
        />
        <Chip
          label="Quiz results"
          selected={tab === 'attempts'}
          onPress={() => setTab('attempts')}
        />
      </View>
      <ResourceState {...resource} retry={resource.reload} />
      {tab === 'sessions'
        ? sessions.data?.data.map((job) => (
            <HistoryEntry
              key={job.id}
              job={job}
              onPress={() => router.push({ pathname: '/job', params: { id: job.id } })}
            />
          ))
        : attempts.data?.data.map((attempt) => (
            <Row
              key={attempt.id}
              icon="CheckCircle2"
              title={attempt.title || 'Practice result'}
              detail={`${attempt.correct} / ${attempt.total} correct · ${attempt.courseTitle || new Date(attempt.createdAt).toLocaleDateString()}`}
              onPress={() => router.push({ pathname: '/attempt', params: { id: attempt.id } })}
            />
          ))}
      {!resource.loading && !resource.error && resource.data?.data.length === 0 && (
        <Empty
          title="Your first breakthrough awaits."
          body="Saved study sessions and completed quizzes will appear here."
        />
      )}
      {tab === 'attempts' && (
        <View style={layout.inline}>
          <Button
            label="Previous"
            kind="quiet"
            disabled={page === 1}
            onPress={() => setPage((p) => p - 1)}
          />
          <T variant="small">Page {page}</T>
          <Button
            label="Next"
            kind="quiet"
            disabled={(attempts.data?.data.length || 0) < 20}
            onPress={() => setPage((p) => p + 1)}
          />
        </View>
      )}
    </Page>
  );
}
export function AttemptScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const resource = useResource<Attempt>(`/quiz-attempts/${id}`);
  const attempt = resource.data;
  const [missedOnly, setMissedOnly] = useState(false);
  return (
    <Page>
      <Header title="Your little wins" />
      <ResourceState {...resource} retry={resource.reload} />
      {attempt && (
        <>
          <Hero
            title={attempt.correct === attempt.total ? 'Look at you go!' : 'Every try teaches you.'}
            subtitle={`${attempt.correct} out of ${attempt.total} correct. Let’s make the next try even clearer.`}
          />
          <View style={{ padding: 20, backgroundColor: c.blueSoft, borderRadius: 20, gap: 12 }}>
            <View style={[layout.inline, { justifyContent: 'space-between' }]}>
              <View>
                <T variant="hero" style={{ color: c.blue }}>
                  {attempt.correct} / {attempt.total}
                </T>
                <T variant="small">Correct answers</T>
              </View>
              <Icon name="GraduationCap" color={c.blue} size={36} />
            </View>
            <View style={{ height: 8, borderRadius: 8, backgroundColor: '#D4E1FF' }}>
              <View
                style={{
                  width: `${attempt.total ? (attempt.correct / attempt.total) * 100 : 0}%`,
                  height: 8,
                  borderRadius: 8,
                  backgroundColor: c.blue,
                }}
              />
            </View>
            <T variant="small">
              {attempt.title || 'Practice complete'}
              {attempt.courseTitle ? ` · ${attempt.courseTitle}` : ''}
            </T>
          </View>
          <View style={layout.inline}>
            <Chip
              label="All questions"
              selected={!missedOnly}
              onPress={() => setMissedOnly(false)}
            />
            <Chip
              label="What to revisit"
              selected={missedOnly}
              onPress={() => setMissedOnly(true)}
            />
          </View>
          {attempt.corrections
            .filter((q) => !missedOnly || q.selectedOptionId !== q.correctOptionId)
            .map((q, i) => (
              <View
                key={q.questionId}
                style={{
                  gap: 12,
                  paddingVertical: 14,
                  borderBottomWidth: 1,
                  borderBottomColor: c.line,
                }}
              >
                <T variant="title">
                  {i + 1}. {q.question}
                </T>
                <Notice tone={q.selectedOptionId === q.correctOptionId ? 'success' : 'error'}>
                  Your answer: {q.options.find((o) => o.id === q.selectedOptionId)?.text}
                </Notice>
                {q.selectedOptionId !== q.correctOptionId && (
                  <T variant="label" style={{ color: c.green }}>
                    Correct answer: {q.options.find((o) => o.id === q.correctOptionId)?.text}
                  </T>
                )}
                <T>{q.explanation}</T>
                <Citations citations={q.citations} />
              </View>
            ))}
          {missedOnly && attempt.correct === attempt.total && (
            <Notice tone="success">Nothing to revisit this time. Nice work!</Notice>
          )}
          {attempt.correct < attempt.total && (
            <Button
              label="Retry what I missed"
              onPress={() =>
                router.push({
                  pathname: '/job',
                  params: { id: attempt.jobId, retryAttemptId: attempt.id },
                })
              }
            />
          )}
          <Button
            label="Practise this quiz again"
            onPress={() => router.push({ pathname: '/job', params: { id: attempt.jobId } })}
          />
          <Button label="Back to history" kind="quiet" onPress={() => router.replace('/history')} />
        </>
      )}
    </Page>
  );
}
export function ReaderScreen() {
  const {
    materialId,
    versionId,
    page: requested,
  } = useLocalSearchParams<{ materialId: string; versionId: string; page?: string }>();
  const [page, setPage] = useState(physicalPage(requested));
  const [total, setTotal] = useState(0);
  const [zoom, setZoom] = useState(1);
  const [data, setData] = useState('');
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [showText, setShowText] = useState(false);
  const path = `/materials/${materialId}/versions/${versionId}`;
  const text = useResource<{ text: string }>(`${path}/pages/${page}`);
  useEffect(() => {
    const controller = new AbortController();
    api
      .request<ArrayBuffer>(`${path}/content`, { signal: controller.signal }, true, true)
      .then((bytes) => {
        if (controller.signal.aborted) return;
        if (bytes.byteLength > 32 * 1024 * 1024)
          throw new Error('Document exceeds the 32 MB mobile limit.');
        const array = new Uint8Array(bytes);
        if (String.fromCharCode(...array.slice(0, 5)) !== '%PDF-') throw new Error('Not a PDF');
        setData(base64(array));
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(errorText(e));
      });
    return () => controller.abort();
  }, [path, revision]);
  const changed = useCallback((current: number, count: number) => {
    setPage(current);
    setTotal(count);
  }, []);
  return (
    <Page
      scroll={false}
      footer={
        <>
          <View style={[layout.inline, { justifyContent: 'space-between' }]}>
            <Button
              label="Previous"
              kind="quiet"
              disabled={page <= 1 || !total}
              onPress={() => setPage((p) => p - 1)}
            />
            <T variant="small">
              PDF {page}
              {total ? ` / ${total}` : ''}
            </T>
            <Button
              label="Next"
              kind="quiet"
              disabled={!total || page >= total}
              onPress={() => setPage((p) => p + 1)}
            />
          </View>
          <Button
            label={showText ? 'Show original PDF' : 'Read extracted page text'}
            kind="quiet"
            onPress={() => setShowText((value) => !value)}
          />
        </>
      }
    >
      <Header title="The original source" />
      {!!error && (
        <>
          <Notice tone="error">{error}</Notice>
          <Button
            label="Download again"
            onPress={() => {
              setData('');
              setError('');
              setRevision((v) => v + 1);
            }}
          />
        </>
      )}
      {!data && !error && <ActivityIndicator color={c.blue} />}
      {!!data && !showText && (
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 4,
          }}
        >
          <IconButton
            icon="ZoomOut"
            label="Zoom out"
            disabled={!total || zoom <= 1}
            onPress={() => setZoom((value) => Math.max(1, value - 0.25))}
          />
          <T variant="small" accessibilityLabel="PDF zoom">
            {Math.round(zoom * 100)}%
          </T>
          <IconButton
            icon="ZoomIn"
            label="Zoom in"
            disabled={!total || zoom >= 3}
            onPress={() => setZoom((value) => Math.min(3, value + 0.25))}
          />
          <Chip label="Fit width" selected={zoom === 1} onPress={() => setZoom(1)} />
        </View>
      )}
      {!!data && !showText && (
        <View style={{ flex: 1, minHeight: 0, marginHorizontal: -22 }}>
          <PdfCanvas
            key={`${materialId}:${versionId}:${revision}`}
            data={data}
            page={page}
            zoom={zoom}
            onPage={changed}
          />
        </View>
      )}
      {showText && (
        <ScrollView style={{ flex: 1 }}>
          <ResourceState {...text} retry={text.reload} />
          <T selectable>{text.data?.text || 'No extractable text on this page.'}</T>
        </ScrollView>
      )}
    </Page>
  );
}
