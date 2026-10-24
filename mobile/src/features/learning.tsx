import React, { useRef, useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { api, newKey } from '../api/runtime';
import type { LearningCounts, RevisionCard, RevisionPage, ReminderSettings } from '../api/types';
import {
  Button,
  Chip,
  Empty,
  Field,
  Header,
  Hero,
  Notice,
  Page,
  Row,
  SearchField,
  Section,
  T,
  layout,
} from '../ui';
import { colors as c } from '../theme';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';
import { useOperation } from './operation';
const amount = (count: number, singular: string, plural = singular + 's') =>
  `${count} ${count === 1 ? singular : plural}`;

export function ProgressScreen() {
  const resource = useResource<LearningCounts>('/learning/progress');
  const revision = useResource<RevisionPage>('/learning/revision');
  const counts = resource.data;
  return (
    <Page
      onRefresh={() => {
        resource.reload();
        revision.reload();
      }}
      refreshing={resource.loading || revision.loading}
    >
      <Header title="Your learning" />
      <Hero
        title="Small steps, saved."
        subtitle="Your actual study activity, ready to return to."
        mascot={false}
      />
      <ResourceState {...resource} retry={resource.reload} />
      {counts && (
        <>
          <View style={[layout.card, { backgroundColor: c.blueSoft, gap: 10 }]}>
            <T variant="title">{amount(counts.quizzesAttempted, 'quiz', 'quizzes')} attempted</T>
            <T>
              {amount(counts.quizAttempts, 'submitted quiz attempt')} · scores are in Quiz results
            </T>
            <T>{amount(counts.writtenTasksAttempted, 'written task')} attempted</T>
            <T>{amount(counts.codingLabsAttempted, 'coding lab')} attempted</T>
            <T variant="small">
              {amount(counts.practiceAttempts, 'saved written/code submission')} ·{' '}
              {amount(counts.assistedTasks, 'task')} used hints or a previously viewed solution
            </T>
          </View>
          <Row
            title={`${amount(counts.selfCompletedTasks, 'task')} marked complete by you`}
            detail="Your own completion choice, separate from quiz scores"
            icon="CheckCircle2"
            onPress={() => router.push('/history')}
          />
          <Row
            title={amount(counts.feedbackConversations, 'AI feedback conversation')}
            detail="Formative feedback on saved work"
            icon="Sparkle"
            onPress={() => router.push('/tutor')}
          />
          <Row
            title={amount(counts.savedSessions, 'saved study session')}
            detail="Summaries, explanations, quizzes and practice sets"
            icon="History"
            onPress={() => router.push('/history')}
          />
        </>
      )}
      <ResourceState {...revision} retry={revision.reload} />
      <Row
        title="Your revision queue"
        detail={
          revision.data
            ? `${revision.data.dueCount} items to revisit · missed questions and concepts you saved`
            : 'Missed questions and concepts you saved'
        }
        icon="BookOpen"
        onPress={() => router.push('/revision')}
      />
      <Row
        title="Study reminders"
        detail="Choose a time, timezone and quiet hours"
        icon="Bell"
        onPress={() => router.push('/reminders')}
      />
      <Notice>
        Quiz scores use the saved answer key. Written work, code and AI feedback do not receive an
        automatic grade.
      </Notice>
    </Page>
  );
}

export function RevisionScreen() {
  const operation = useOperation();
  const [state, setState] = useState<'active' | 'snoozed' | 'dismissed'>('active');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [adding, setAdding] = useState(false);
  const resource = useResource<RevisionPage>(
    `/learning/revision?state=${state}&page=${page}&q=${encodeURIComponent(search.trim())}`,
  );
  return (
    <Page>
      <Header title="Your revision queue" />
      <T>
        Missed questions follow your latest attempt at each quiz. Add concepts you want to return
        to, and name topics in your own words.
      </T>
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {(['active', 'snoozed', 'dismissed'] as const).map((value) => (
          <Chip
            key={value}
            label={
              value === 'active' ? 'To revisit' : value === 'snoozed' ? 'Snoozed' : 'Dismissed'
            }
            selected={state === value}
            onPress={() => {
              setState(value);
              setPage(1);
            }}
          />
        ))}
      </View>
      <SearchField
        value={search}
        onChangeText={(value) => {
          setSearch(value);
          setPage(1);
        }}
        placeholder="Find a concept or topic…"
      />
      <Button
        label={adding ? 'Close new concept' : 'Save a concept'}
        kind="secondary"
        onPress={() => setAdding((value) => !value)}
      />
      {adding && (
        <ConceptForm
          onSaved={() => {
            setAdding(false);
            setPage(1);
            setState('active');
            resource.refresh();
          }}
        />
      )}
      <ResourceState {...resource} retry={resource.reload} />
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
      {resource.data?.data.map((card) => (
        <RevisionItem key={card.key} card={card} refresh={resource.refresh} operation={operation} />
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty
          title={state === 'active' ? 'A little space to learn' : 'Nothing here yet'}
          body={
            search
              ? 'No matching items. Try a different topic.'
              : state === 'active'
                ? 'Save a concept, or return after a quiz to revisit missed questions.'
                : 'Items you snooze or dismiss will appear here.'
          }
        />
      )}
      {resource.data && resource.data.total > 20 && (
        <View style={layout.inline}>
          <Button
            label="Previous"
            kind="quiet"
            disabled={page === 1}
            onPress={() => setPage(page - 1)}
          />
          <T>Page {page}</T>
          <Button
            label="Next"
            kind="quiet"
            disabled={page * 20 >= resource.data.total}
            onPress={() => setPage(page + 1)}
          />
        </View>
      )}
      <Row
        title="Study reminders"
        detail="Set a daily time that suits you"
        icon="Bell"
        onPress={() => router.push('/reminders')}
      />
    </Page>
  );
}
function ConceptForm({ onSaved }: { onSaved: () => void }) {
  const [title, setTitle] = useState('');
  const [topic, setTopic] = useState('');
  const [note, setNote] = useState('');
  const operation = useOperation();
  const key = useRef(newKey());
  const payload = useRef('');
  function save() {
    const body = JSON.stringify({ title: title.trim(), topic: topic.trim(), note: note.trim() });
    if (payload.current && payload.current !== body) key.current = newKey();
    payload.current = body;
    void operation.run(async (signal) => {
      await api.request('/learning/revision', {
        method: 'POST',
        signal,
        headers: { 'Idempotency-Key': key.current },
        body,
      });
      if (!signal.aborted) onSaved();
    });
  }
  return (
    <View style={[layout.card, { gap: 12 }]}>
      <Section title="A concept to revisit" />
      <Field label="Concept" value={title} onChangeText={setTitle} maxLength={160} />
      <Field label="Topic (optional)" value={topic} onChangeText={setTopic} maxLength={80} />
      <Field
        label="Your note (optional)"
        value={note}
        onChangeText={setNote}
        multiline
        maxLength={2000}
      />
      <Button label="Save concept" busy={operation.busy} disabled={!title.trim()} onPress={save} />
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
    </View>
  );
}
function RevisionItem({
  card,
  refresh,
  operation,
}: {
  card: RevisionCard;
  refresh: () => void;
  operation: ReturnType<typeof useOperation>;
}) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(card.title);
  const [topic, setTopic] = useState(card.topic);
  const [note, setNote] = useState(card.note);
  function update(state: RevisionCard['state'], snoozeDays?: number, edited = false) {
    void operation.run(async (signal) => {
      await api.request('/learning/revision/' + encodeURIComponent(card.key), {
        method: 'PUT',
        signal,
        body: JSON.stringify({
          title: edited ? title : card.title,
          topic: edited ? topic : card.topic,
          note: edited ? note : card.note,
          state,
          snoozeDays,
        }),
      });
      if (!signal.aborted) {
        setEditing(false);
        refresh();
      }
    });
  }
  return (
    <View style={[layout.card, { gap: 12 }]}>
      <T variant="small">
        {card.kind === 'quiz' ? 'Missed quiz question' : 'Concept saved by you'}
        {card.courseTitle ? ' · ' + card.courseTitle : ''}
      </T>
      <T variant="title">{card.title}</T>
      {!!card.topic && <T variant="label">{card.topic}</T>}
      {!!card.note && <T selectable>{card.note}</T>}
      {card.state === 'snoozed' && card.snoozedUntil && (
        <T variant="small">Back in your queue {new Date(card.snoozedUntil).toLocaleString()}</T>
      )}
      {card.attemptId && (
        <Button
          label="Review question and correction"
          kind="secondary"
          onPress={() =>
            router.push({
              pathname: '/attempt',
              params: { id: card.attemptId!, questionId: card.questionId! },
            })
          }
        />
      )}
      <Button
        label={editing ? 'Keep saved labels' : 'Edit topic and notes'}
        kind="quiet"
        onPress={() => {
          setTitle(card.title);
          setTopic(card.topic);
          setNote(card.note);
          setEditing((value) => !value);
        }}
      />
      {editing && (
        <>
          <Field label="Your label" value={title} onChangeText={setTitle} maxLength={160} />
          <Field label="Topic" value={topic} onChangeText={setTopic} maxLength={80} />
          <Field label="Your note" value={note} onChangeText={setNote} maxLength={2000} multiline />
          {card.originalQuestion && <Notice>Original question: {card.originalQuestion}</Notice>}
          <Button
            label="Save labels and note"
            busy={operation.busy}
            disabled={!title.trim()}
            onPress={() => update(card.state, undefined, true)}
          />
        </>
      )}
      <View style={[layout.inline, { flexWrap: 'wrap' }]}>
        {card.state === 'active' ? (
          <>
            <Button
              label="Snooze 1 day"
              kind="quiet"
              busy={operation.busy}
              onPress={() => update('snoozed', 1)}
            />
            <Button
              label="Dismiss"
              kind="quiet"
              busy={operation.busy}
              onPress={() => update('dismissed')}
            />
          </>
        ) : (
          <Button
            label="Return to queue"
            kind="quiet"
            busy={operation.busy}
            onPress={() => update('active')}
          />
        )}
      </View>
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
    </View>
  );
}
const timeText = (minute: number) =>
  `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
const readTime = (value: string) =>
  /^([01]\d|2[0-3]):[0-5]\d$/.test(value)
    ? Number(value.slice(0, 2)) * 60 + Number(value.slice(3))
    : null;
export function RemindersScreen() {
  const resource = useResource<ReminderSettings>('/learning/reminder');
  return (
    <Page>
      <Header title="Study reminders" />
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data && <ReminderForm initial={resource.data} />}
    </Page>
  );
}
function ReminderForm({ initial }: { initial: ReminderSettings }) {
  const [enabled, setEnabled] = useState(initial.enabled);
  const [time, setTime] = useState(timeText(initial.minuteOfDay));
  const [zone, setZone] = useState(initial.timeZoneId);
  const [start, setStart] = useState(timeText(initial.quietStart));
  const [end, setEnd] = useState(timeText(initial.quietEnd));
  const [saved, setSaved] = useState<ReminderSettings | null>(initial);
  const operation = useOperation();
  const valid =
    readTime(time) !== null && readTime(start) !== null && readTime(end) !== null && !!zone.trim();
  function save() {
    void operation.run(async (signal) => {
      const result = await api.request<ReminderSettings>('/learning/reminder', {
        method: 'PUT',
        signal,
        body: JSON.stringify({
          enabled,
          minuteOfDay: readTime(time),
          timeZoneId: zone.trim(),
          quietStart: readTime(start),
          quietEnd: readTime(end),
        }),
      });
      if (!signal.aborted) setSaved(result);
    });
  }
  return (
    <>
      <T>
        Choose a gentle daily reminder. These reminders appear in Activity while the study service
        is running. Phone push delivery still needs setup.
      </T>
      <View style={layout.inline}>
        <Chip
          label="Off"
          selected={!enabled}
          onPress={() => {
            if (operation.busy) return;
            setEnabled(false);
            setSaved(null);
          }}
        />
        <Chip
          label="Daily"
          selected={enabled}
          onPress={() => {
            if (operation.busy) return;
            setEnabled(true);
            setSaved(null);
          }}
        />
      </View>
      <Field
        label="Reminder time (24-hour HH:MM)"
        editable={!operation.busy}
        value={time}
        onChangeText={(v) => {
          setTime(v);
          setSaved(null);
        }}
        maxLength={5}
      />
      <Field
        label="Timezone"
        editable={!operation.busy}
        value={zone}
        onChangeText={(v) => {
          setZone(v);
          setSaved(null);
        }}
        autoCapitalize="none"
        autoCorrect={false}
        maxLength={100}
      />
      <T variant="small">For example, Africa/Lagos or Europe/Paris.</T>
      <Button
        label="Use phone timezone"
        kind="quiet"
        disabled={operation.busy}
        onPress={() => {
          setZone(Intl.DateTimeFormat().resolvedOptions().timeZone);
          setSaved(null);
        }}
      />
      <Section title="Quiet hours" />
      <Field
        label="Quiet from (HH:MM)"
        editable={!operation.busy}
        value={start}
        onChangeText={(v) => {
          setStart(v);
          setSaved(null);
        }}
        maxLength={5}
      />
      <Field
        label="Quiet until (HH:MM)"
        editable={!operation.busy}
        value={end}
        onChangeText={(v) => {
          setEnd(v);
          setSaved(null);
        }}
        maxLength={5}
      />
      <T variant="small">
        Choose a reminder outside quiet hours. Equal start and end times turn quiet hours off. Times
        follow your selected timezone; a spring clock gap moves to the next valid minute and a
        repeated autumn time sends once.
      </T>
      <Button
        label="Save reminder settings"
        busy={operation.busy}
        disabled={!valid}
        onPress={save}
      />
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
      {saved && (
        <Notice tone="success">
          {saved.enabled && saved.nextDueAt
            ? `Saved. Next reminder: ${new Date(saved.nextDueAt).toLocaleString(undefined, { timeZone: saved.timeZoneId })} (${saved.timeZoneId}).`
            : 'Saved. Daily reminders are off.'}
        </Notice>
      )}
    </>
  );
}
