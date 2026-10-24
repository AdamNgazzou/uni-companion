import React, { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { Button, Chip, Empty, Field, Header, Icon, Notice, Page, Section, T, layout } from '../ui';
import { colors as c } from '../theme';
import { api, newKey } from '../api/runtime';
import { useOperation } from './operation';
import type {
  CourseDetail,
  Job,
  PracticeArtifact,
  PracticeState,
  PracticeTask,
  StudyRequest,
} from '../api/types';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';
import { practiceDrafts } from '../practice/draft-runtime';
import type { PracticeDraft } from '../practice/drafts';
import { useSession } from '../auth/session';
import { Citations } from '../study/citations';

export function PracticeSetupScreen() {
  const params = useLocalSearchParams<{ courseId: string; materialId?: string }>();
  const course = useResource<CourseDetail>(params.courseId ? `/courses/${params.courseId}` : null);
  const [materialId, setMaterial] = useState(params.materialId || '');
  const [mode, setMode] = useState<'written' | 'coding' | 'mixed'>('mixed');
  const [difficulty, setDifficulty] = useState<'guided' | 'standard' | 'challenge'>('guided');
  const [minutes, setMinutes] = useState<15 | 30 | 45>(30);
  const [language, setLanguage] = useState<'en' | 'fr'>('en');
  const [topic, setTopic] = useState('');
  const operation = useOperation();
  const key = useRef(newKey());
  const payload = useRef('');
  const available =
    course.data?.materials.filter((m) => m.contentType === 'application/pdf' && m.indexed) || [];
  const selected = available.find((m) => m.id === materialId);
  function start() {
    if (!selected) return;
    const request: StudyRequest = {
      kind: 'practice',
      language,
      materialId: selected.id,
      question: topic.trim() || undefined,
      practice: { mode, difficulty, minutes },
    };
    const body = JSON.stringify(request);
    if (payload.current && payload.current !== body) key.current = newKey();
    payload.current = body;
    void operation.run(async (signal) => {
      const job = await api.request<Job>(`/courses/${params.courseId}/study-jobs`, {
        method: 'POST',
        body,
        headers: { 'Idempotency-Key': key.current },
        signal,
      });
      if (!signal.aborted) router.replace({ pathname: '/job', params: { id: job.id } });
    });
  }
  return (
    <Page>
      <Header title="Create practice" />
      <T variant="title">Put your course into practice.</T>
      <T>New written reasoning tasks and Python labs, grounded in one of your course PDFs.</T>
      <ResourceState {...course} retry={course.reload} />
      <Section title="Choose an indexed PDF" />
      <T variant="small">
        Lectures, exercise sheets and lab sheets can supply concepts. Figures, notebooks, datasets
        and ZIP contents aren’t read by this tool.
      </T>
      {available.map((m) => (
        <Pressable
          key={m.id}
          accessibilityRole="button"
          accessibilityState={{ selected: materialId === m.id }}
          onPress={() => setMaterial(m.id)}
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: 12,
            minHeight: 56,
            paddingVertical: 14,
            borderBottomWidth: 1,
            borderBottomColor: c.line,
          }}
        >
          <T style={{ flex: 1 }}>{m.title}</T>
          <Icon
            name={materialId === m.id ? 'CheckCircle2' : 'FileText'}
            color={materialId === m.id ? c.blue : c.muted}
          />
        </Pressable>
      ))}
      {course.data && !available.length && (
        <Empty
          title="No indexed PDF yet"
          body="Choose another course or wait for its PDFs to be indexed."
        />
      )}
      {selected && (
        <Notice>Source: {selected.title}. The original version is saved with your practice.</Notice>
      )}
      <Section title="Practice type" />
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {(['written', 'coding', 'mixed'] as const).map((value) => (
          <Chip
            key={value}
            label={value === 'mixed' ? 'Mixed' : value === 'written' ? 'Written' : 'Coding lab'}
            selected={mode === value}
            onPress={() => setMode(value)}
          />
        ))}
      </View>
      <T variant="small">
        {mode === 'written'
          ? '3 explanation or scenario tasks.'
          : mode === 'coding'
            ? '1 Python 3 lab with 3–5 steps.'
            : '2 written tasks and 1 Python 3 lab.'}{' '}
        Coding labs run on your own computer using the standard library.
      </T>
      <Field
        label="Topic (optional)"
        value={topic}
        onChangeText={setTopic}
        maxLength={1000}
        placeholder="A concept from the chosen PDF"
      />
      <Section title="How much guidance?" />
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {(['guided', 'standard', 'challenge'] as const).map((value) => (
          <Chip
            key={value}
            label={value === 'guided' ? 'Guided' : value === 'standard' ? 'Standard' : 'Challenge'}
            selected={difficulty === value}
            onPress={() => setDifficulty(value)}
          />
        ))}
      </View>
      <Section title="Estimated time" />
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {([15, 30, 45] as const).map((value) => (
          <Chip
            key={value}
            label={`${value} min`}
            selected={minutes === value}
            onPress={() => setMinutes(value)}
          />
        ))}
      </View>
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        <Chip label="English" selected={language === 'en'} onPress={() => setLanguage('en')} />
        <Chip label="Français" selected={language === 'fr'} onPress={() => setLanguage('fr')} />
      </View>
      <T variant="small">
        Examples and data are generated. Use source links to check the concepts. This is practice,
        with no official marks.
      </T>
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
      <Button
        label="Generate practice"
        busy={operation.busy}
        disabled={!selected}
        onPress={start}
      />
    </Page>
  );
}
function Code({ text }: { text: string }) {
  return (
    <ScrollView
      horizontal
      style={{ backgroundColor: c.canvas, borderRadius: 12 }}
      contentContainerStyle={{ padding: 16 }}
    >
      <T selectable style={{ fontFamily: 'monospace', fontSize: 13, lineHeight: 21 }}>
        {text}
      </T>
    </ScrollView>
  );
}
export function PracticePlayer({
  id,
  artifact,
  initialTaskId,
}: {
  id: string;
  artifact: PracticeArtifact;
  initialTaskId?: string;
}) {
  const progress = useResource<{ tasks: PracticeState[] }>(`/practice/${id}/progress`);
  const [index, setIndex] = useState(() =>
    Math.max(
      0,
      artifact.tasks.findIndex((task) => task.id === initialTaskId),
    ),
  );
  const task = artifact.tasks[index];
  const state = progress.data?.tasks.find((s) => s.id === task?.id);
  return (
    <View style={{ gap: 18 }}>
      <T variant="title">Your practice set</T>
      <Notice>
        AI-generated examples and synthetic data. Source pages explain the concepts; these are new
        exercises. Saved attempts are for study, with no official score.
      </Notice>
      <Section title="Objectives" />
      {artifact.objectives.map((value, i) => (
        <T key={i}>• {value}</T>
      ))}
      {!!artifact.prerequisites.length && (
        <>
          <Section title="Before you begin" />
          {artifact.prerequisites.map((value, i) => (
            <T key={i}>• {value}</T>
          ))}
        </>
      )}
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {artifact.tasks.map((t, i) => (
          <Chip
            key={t.id}
            label={`${i + 1}. ${t.kind === 'coding' ? 'Lab' : 'Written'}`}
            selected={index === i}
            onPress={() => setIndex(i)}
          />
        ))}
      </View>
      <ResourceState {...progress} retry={progress.reload} />
      {task && state && (
        <PracticeWork key={task.id} jobId={id} task={task} state={state} reload={progress.reload} />
      )}
    </View>
  );
}
function PracticeWork({
  jobId,
  task,
  state,
  reload,
}: {
  jobId: string;
  task: PracticeTask;
  state: PracticeState;
  reload: () => void;
}) {
  const { profile } = useSession();
  const [draft, setDraft] = useState<PracticeDraft | null>(null);
  const [draftReady, setDraftReady] = useState(false);
  const [warning, setWarning] = useState('');
  const generation = useRef(practiceDrafts.generation());
  const answer = draft?.answer || '';
  useEffect(() => {
    let active = true;
    const epoch = practiceDrafts.generation();
    generation.current = epoch;
    if (profile)
      void practiceDrafts
        .read(profile.id, jobId, task.id, epoch)
        .then((value) => {
          if (active) {
            setDraft(value || { answer: '', requestKey: newKey(), updatedAt: Date.now() });
            setDraftReady(true);
          }
        })
        .catch(() => {
          if (active) {
            setWarning(
              'Your local draft could not be restored. Saved attempts are still available.',
            );
            setDraft({ answer: '', requestKey: newKey(), updatedAt: Date.now() });
            setDraftReady(true);
          }
        });
    return () => {
      active = false;
    };
  }, [profile, jobId, task.id]);
  function setAnswer(text: string) {
    if (!profile || !draft) return;
    const next = { ...draft, answer: text, updatedAt: Date.now() };
    setDraft(next);
    void practiceDrafts
      .save(profile.id, jobId, task.id, next, generation.current)
      .catch(() =>
        setWarning(
          'This draft could not be saved locally. Keep this screen open and submit to save your attempt.',
        ),
      );
  }
  const [confirm, setConfirm] = useState(false);
  const [showStarter, setShowStarter] = useState(false);
  const [showChecks, setShowChecks] = useState(false);
  const operation = useOperation();
  const path = `/practice/${jobId}/tasks/${task.id}`;
  function mutate(suffix: string, body?: object) {
    void operation.run(async (signal) => {
      await api.request(path + suffix, {
        method: 'PUT',
        signal,
        body: body ? JSON.stringify(body) : undefined,
      });
      if (!signal.aborted) reload();
    });
  }
  function submit() {
    const text = answer.trim();
    if (!text || !profile || !draft) return;
    const next = {
      ...draft,
      answer: text,
      requestKey: draft.lastAnswer && draft.lastAnswer !== text ? newKey() : draft.requestKey,
      lastAnswer: text,
      updatedAt: Date.now(),
    };
    setDraft(next);
    void operation.run(async (signal) => {
      await practiceDrafts.save(profile.id, jobId, task.id, next, generation.current);
      if (signal.aborted) return;
      await api.request(path + '/attempts', {
        method: 'POST',
        signal,
        headers: { 'Idempotency-Key': next.requestKey },
        body: JSON.stringify({ answer: text }),
      });
      await practiceDrafts.remove(profile.id, jobId, task.id);
      if (!signal.aborted) {
        setDraft({ answer: '', requestKey: newKey(), updatedAt: Date.now() });
        reload();
      }
    });
  }
  if (!draftReady) return <ActivityIndicator color={c.blue} />;
  return (
    <View style={{ gap: 16 }}>
      <T variant="title">{task.title}</T>
      <T selectable>{task.prompt}</T>
      <Section title="Given information" />
      <T selectable>{task.given}</T>
      {!!task.steps.length && (
        <>
          <Section title="Lab steps" />
          {task.steps.map((value, i) => (
            <T key={i}>
              {i + 1}. {value}
            </T>
          ))}
        </>
      )}
      {task.kind === 'coding' && (
        <>
          <Notice>
            Run this lab locally in Python 3. AI-generated code hasn’t been run by the app; review
            it before running. Pasted code is saved as text.
          </Notice>
          <T>{task.runInstructions}</T>
          <Button
            label={showStarter ? 'Hide starter code' : 'Show starter code'}
            kind="secondary"
            onPress={() => setShowStarter(!showStarter)}
          />
          {showStarter && task.starterCode && <Code text={task.starterCode} />}
          <Button
            label={showChecks ? 'Hide expected results' : 'Show expected results'}
            kind="quiet"
            onPress={() => setShowChecks(!showChecks)}
          />
          {showChecks && (
            <>
              <T variant="label">Sample output</T>
              <Code text={task.expectedOutput || ''} />
              {task.checks.map((check, i) => (
                <View key={i} style={{ gap: 8 }}>
                  <T variant="label">Check {i + 1}</T>
                  <Code text={`Input: ${check.input}\nExpected: ${check.expected}`} />
                </View>
              ))}
            </>
          )}
        </>
      )}
      <Section title="Concepts from your course" />
      <Citations citations={task.citations} />
      {state.hints.map((hint, i) => (
        <Notice key={i}>
          Hint {i + 1}: {hint}
        </Notice>
      ))}
      {state.hintLevel < state.hintCount && (
        <Button
          label={`Open hint ${state.hintLevel + 1}`}
          kind="quiet"
          busy={operation.busy}
          onPress={() => mutate('/hint', { level: state.hintLevel + 1 })}
        />
      )}
      <Section title="Your attempt" />
      <Field
        label={task.kind === 'coding' ? 'Paste your code or output' : 'Write your reasoning'}
        value={answer}
        onChangeText={setAnswer}
        editable={!operation.busy}
        autoCapitalize={task.kind === 'coding' ? 'none' : 'sentences'}
        autoCorrect={task.kind !== 'coding'}
        multiline
        maxLength={12000}
      />
      <T variant="small">
        Your draft stays on this device for seven days. Submit to save your attempt to your account
        and compare with a worked solution.
      </T>
      <Button
        label="Save attempt & compare"
        busy={operation.busy}
        disabled={!answer.trim() || state.attempts.length >= 20}
        onPress={submit}
      />
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
      {!!warning && <Notice tone="error">{warning}</Notice>}
      {!state.solutionViewed && (
        <>
          <Button
            label="Reveal solution without submitting"
            kind="quiet"
            onPress={() => setConfirm(true)}
          />
          {confirm && (
            <View style={{ padding: 18, borderRadius: 16, backgroundColor: c.orangeSoft, gap: 12 }}>
              <T>
                Revealing saves “solution viewed” for this task. Future attempts will show that you
                used the solution.
              </T>
              <Button
                label="Reveal this solution"
                busy={operation.busy}
                onPress={() => mutate('/solution')}
              />
              <Button label="Keep working" kind="quiet" onPress={() => setConfirm(false)} />
            </View>
          )}
        </>
      )}
      {state.solutionViewed && (
        <View style={{ gap: 14 }}>
          <Section title="Worked solution" />
          <T variant="small">Solution viewed · compare your reasoning, with no automatic grade.</T>
          <T selectable>{state.solution}</T>
          {!!state.solutionCode && <Code text={state.solutionCode} />}
          <Section title="What to check" />
          {state.rubric?.map((value, i) => (
            <T key={i}>• {value}</T>
          ))}
        </View>
      )}
      {!!state.attempts.length && (
        <>
          <Section title="Saved attempts" />
          {state.attempts.map((attempt) => (
            <View
              key={attempt.id}
              style={{
                gap: 8,
                paddingVertical: 14,
                borderBottomWidth: 1,
                borderBottomColor: c.line,
              }}
            >
              <T variant="small">
                {new Date(attempt.createdAt).toLocaleString()} ·{' '}
                {attempt.solutionViewedBefore
                  ? 'Solution already viewed'
                  : attempt.hintsUsed
                    ? 'Hints used'
                    : 'Submitted before viewing hints or solution'}
              </T>
              <T selectable>{attempt.answer}</T>
              <Button
                label="Get AI feedback"
                kind="secondary"
                busy={operation.busy}
                onPress={() => {
                  void operation.run(async (signal) => {
                    const result = await api.request<{ id: string }>(
                      `/practice/${jobId}/attempts/${attempt.id}/feedback`,
                      {
                        method: 'POST',
                        signal,
                        headers: { 'Idempotency-Key': 'feedback:' + attempt.id },
                      },
                    );
                    if (!signal.aborted)
                      router.push({ pathname: '/tutor', params: { id: result.id } });
                  });
                }}
              />
            </View>
          ))}
        </>
      )}
    </View>
  );
}
