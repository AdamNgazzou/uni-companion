import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import {
  Button,
  Chip,
  Empty,
  Field,
  Header,
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
import {
  pending,
  type CourseDetail,
  type TutorConversation,
  type TutorPage,
  type TutorTurn,
} from '../api/types';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';
import { useOperation } from './operation';
import { useJob } from '../study/use-job';
import { StudyText } from '../study/presentation';
import { Citations } from '../study/citations';

export function TutorScreen() {
  const params = useLocalSearchParams<{
    id?: string;
    courseId?: string;
    materialId?: string;
    chapterId?: string;
  }>();
  return params.id ? (
    <Conversation key={params.id} id={params.id} />
  ) : (
    <TutorHome key={`${params.courseId || ''}:${params.materialId || ''}`} {...params} />
  );
}
function TutorHome({
  courseId,
  materialId,
  chapterId,
}: {
  courseId?: string;
  materialId?: string;
  chapterId?: string;
}) {
  const [page, setPage] = useState(1);
  const [language, setLanguage] = useState<'en' | 'fr'>('en');
  const [confirm, setConfirm] = useState(false);
  const list = useResource<TutorPage>(
    `/tutor/conversations?page=${page}${courseId ? '&courseId=' + courseId : ''}`,
  );
  const scope = useResource<CourseDetail>(courseId ? `/courses/${courseId}` : null);
  const operation = useOperation();
  const key = useRef(newKey());
  const payload = useRef('');
  function start() {
    if (!courseId || !scope.data) return;
    const body = JSON.stringify({
      language,
      materialId: materialId || undefined,
      chapterId: chapterId || undefined,
    });
    if (payload.current && payload.current !== body) key.current = newKey();
    payload.current = body;
    void operation.run(async (signal) => {
      const result = await api.request<{ id: string }>(`/courses/${courseId}/tutor-conversations`, {
        method: 'POST',
        signal,
        headers: { 'Idempotency-Key': key.current },
        body,
      });
      if (!signal.aborted) router.push({ pathname: '/tutor', params: { id: result.id } });
    });
  }
  function clear() {
    void operation.run(async (signal) => {
      await api.request('/tutor/conversations', { method: 'DELETE', signal });
      if (!signal.aborted) {
        setConfirm(false);
        list.reload();
      }
    });
  }
  return (
    <Page>
      <Header title="Course conversations" />
      {courseId ? (
        <>
          <ResourceState {...scope} retry={scope.reload} />
          {scope.data && (
            <>
              <T variant="title">{scope.data.title}</T>
              <T>
                {materialId
                  ? scope.data.materials.find((m) => m.id === materialId)?.title || 'Selected PDF'
                  : chapterId
                    ? 'Selected chapter PDFs'
                    : 'Course PDFs with indexed text'}
              </T>
              <View style={[layout.inline, { flexWrap: 'wrap' }]}>
                <Chip
                  label="English"
                  selected={language === 'en'}
                  onPress={() => setLanguage('en')}
                />
                <Chip
                  label="Français"
                  selected={language === 'fr'}
                  onPress={() => setLanguage('fr')}
                />
              </View>
              <Button label="Start a conversation" busy={operation.busy} onPress={start} />
            </>
          )}
        </>
      ) : (
        <Row
          title="Start with a course"
          detail="Choose a joined course or PDF, then open its tutor"
          icon="BookOpen"
          onPress={() => router.push('/(tabs)')}
        />
      )}
      <T variant="small">
        Follow-up questions use your course sources and recent messages. Restate older details when
        needed. Changing course starts a new conversation.
      </T>
      <Section title={courseId ? 'Saved in this course' : 'Your saved conversations'} />
      <ResourceState {...list} retry={list.reload} />
      {list.data?.data.map((item) => (
        <Row
          key={item.id}
          icon={item.isFeedback ? 'CheckCircle2' : 'MessageCircle'}
          title={item.title}
          detail={`${item.isFeedback ? 'Practice feedback · ' : ''}${item.courseTitle} · ${new Date(item.updatedAt).toLocaleDateString()}`}
          onPress={() => router.push({ pathname: '/tutor', params: { id: item.id } })}
        />
      ))}
      {!list.loading && !list.error && !list.data?.data.length && (
        <Empty
          title="Your next question starts here"
          body="Sent messages and source-linked replies stay in your account so you can return later."
        />
      )}
      {list.data && list.data.total > 20 && (
        <View style={[layout.inline, { flexWrap: 'wrap' }]}>
          <Button
            label="Previous"
            kind="quiet"
            disabled={page === 1}
            onPress={() => setPage(page - 1)}
          />
          <T variant="small">Page {page}</T>
          <Button
            label="Next"
            kind="quiet"
            disabled={page * 20 >= list.data.total}
            onPress={() => setPage(page + 1)}
          />
        </View>
      )}
      {!courseId && !!list.data?.total && (
        <Button
          label="Clear all conversation history"
          kind="quiet"
          onPress={() => setConfirm(true)}
        />
      )}
      {confirm && (
        <View style={{ padding: 18, gap: 12, borderRadius: 16, backgroundColor: c.orangeSoft }}>
          <T>
            Delete all your tutor conversations across every course? This also stops their pending
            replies and can’t be undone. Practice attempts stay saved.
          </T>
          <Button
            label="Delete all conversations"
            kind="danger"
            busy={operation.busy}
            onPress={clear}
          />
          <Button label="Keep history" kind="quiet" onPress={() => setConfirm(false)} />
        </View>
      )}
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
    </Page>
  );
}
function LiveTurn({
  id,
  onSettled,
  onCancel,
}: {
  id: string;
  onSettled: () => void;
  onCancel: () => void;
}) {
  const { job, error, reload } = useJob(id);
  useEffect(() => {
    if (job && !pending(job.state)) onSettled();
  }, [job, onSettled]);
  return (
    <View style={{ gap: 12 }}>
      <View style={layout.inline}>
        <ActivityIndicator color={c.blue} />
        <T style={{ flex: 1 }}>{job?.stage || 'Preparing a source-linked reply…'}</T>
      </View>
      {!!error && (
        <>
          <Notice tone="error">{error}</Notice>
          <Button label="Reconnect reply" kind="quiet" onPress={reload} />
        </>
      )}
      <Button label="Stop this reply" kind="quiet" onPress={onCancel} />
    </View>
  );
}
function ReplyContent({
  turn,
  feedback,
  onSettled,
  onCancel,
  onRetry,
}: {
  turn: TutorTurn;
  feedback: boolean;
  onSettled: () => void;
  onCancel: () => void;
  onRetry: () => void;
}) {
  return (
    <View style={{ gap: 12, paddingVertical: 12 }}>
      <T variant="small" style={{ color: c.blue }}>
        {feedback ? 'AI formative feedback' : 'Course tutor · AI study aid'}
      </T>
      {pending(turn.state) ? (
        <LiveTurn id={turn.jobId} onSettled={onSettled} onCancel={onCancel} />
      ) : turn.state === 'succeeded' ? (
        <>
          {turn.result?.insufficientEvidence && !turn.result.paragraphs?.length && (
            <Notice>
              There isn’t enough evidence in these sources. Try a more specific question or another
              PDF.
            </Notice>
          )}
          {turn.result?.paragraphs?.map((p, i) => (
            <View key={i} style={{ gap: 10 }}>
              {p.basis && (
                <T variant="small">
                  {p.basis === 'saved_work'
                    ? 'Based on your saved work and task rubric'
                    : 'Based on course sources'}
                </T>
              )}
              <StudyText text={p.text} />
              <Citations citations={p.citations} />
            </View>
          ))}
        </>
      ) : (
        <>
          <Notice tone="error">
            {turn.state === 'cancelled'
              ? 'This reply was stopped.'
              : `Couldn’t finish this reply (${(turn.errorCode || 'service unavailable').replaceAll('_', ' ')}). Your sent message is saved.`}
          </Notice>
          <Button label="Try this question again" kind="quiet" onPress={onRetry} />
        </>
      )}
    </View>
  );
}
function Conversation({ id }: { id: string }) {
  const resource = useResource<TutorConversation>(`/tutor/conversations/${id}`);
  const conversation = resource.data;
  const [question, setQuestion] = useState('');
  const [action, setAction] = useState<'explain' | 'hint' | 'check'>('explain');
  const [confirm, setConfirm] = useState(false);
  const [visible, setVisible] = useState(10);
  const [showAttempt, setShowAttempt] = useState(false);
  const operation = useOperation();
  const key = useRef(newKey());
  const payload = useRef('');
  const scroll = useRef<React.ComponentRef<typeof ScrollView>>(null);
  const shouldScroll = useRef(true);
  const nearBottom = useRef(true);
  const busy = conversation?.turns.some((t) => pending(t.state)) || false;
  const full = (conversation?.turns.length || 0) >= (conversation?.turnLimit || 40);
  const refresh = resource.refresh;
  const settled = useCallback(() => {
    shouldScroll.current = nearBottom.current;
    refresh();
  }, [refresh]);
  function send() {
    const text = question.trim();
    if (!text || busy || full || !conversation) return;
    const body = JSON.stringify({ question: text, action });
    if (payload.current && payload.current !== body) key.current = newKey();
    payload.current = body;
    void operation.run(async (signal) => {
      await api.request(`/tutor/conversations/${id}/turns`, {
        method: 'POST',
        signal,
        headers: { 'Idempotency-Key': key.current },
        body,
      });
      if (!signal.aborted) {
        setQuestion('');
        key.current = newKey();
        payload.current = '';
        shouldScroll.current = true;
        resource.refresh();
      }
    });
  }
  function stop(jobId: string) {
    void operation.run(async (signal) => {
      await api.request(`/study-jobs/${jobId}/cancel`, { method: 'POST', signal });
      if (!signal.aborted) settled();
    });
  }
  function remove() {
    void operation.run(async (signal) => {
      await api.request(`/tutor/conversations/${id}`, { method: 'DELETE', signal });
      if (!signal.aborted)
        router.replace({
          pathname: '/tutor',
          params: conversation ? { courseId: conversation.courseId } : {},
        });
    });
  }
  const footer = confirm ? (
    <View style={{ gap: 10 }}>
      <T>Delete this conversation and its messages? Pending replies stop. This can’t be undone.</T>
      <Button
        label="Delete this conversation"
        kind="danger"
        busy={operation.busy}
        onPress={remove}
      />
      <Button label="Keep conversation" kind="quiet" onPress={() => setConfirm(false)} />
    </View>
  ) : (
    <View style={{ gap: 10 }}>
      {full ? (
        <Notice>
          This conversation has reached 40 messages. Start a new one for the next topic.
        </Notice>
      ) : (
        <>
          <View style={[layout.inline, { flexWrap: 'wrap' }]}>
            {(['explain', 'hint', 'check'] as const).map((value) => (
              <Chip
                key={value}
                label={
                  value === 'explain' ? 'Explain' : value === 'hint' ? 'Hint' : 'Check reasoning'
                }
                selected={action === value}
                onPress={() => setAction(value)}
              />
            ))}
          </View>
          <Field
            label="Your message"
            value={question}
            onChangeText={setQuestion}
            multiline
            maxLength={2000}
            autoCapitalize="none"
            autoCorrect={false}
            editable={!operation.busy}
            style={{ minHeight: 70, maxHeight: 120, textAlignVertical: 'top' }}
            placeholder={
              action === 'hint'
                ? 'What would you like a hint about?'
                : action === 'check'
                  ? 'Share your reasoning or a short code excerpt'
                  : 'Ask a question or follow up with “why?”'
            }
          />
          <Button
            label={busy ? 'Reply in progress…' : 'Send message'}
            icon="Send"
            busy={operation.busy}
            disabled={busy || !conversation || !question.trim()}
            onPress={send}
          />
        </>
      )}
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
    </View>
  );
  return (
    <Page scroll={false} padded={false} footer={footer}>
      <View style={{ paddingHorizontal: 22 }}>
        <Header
          title="Course tutor"
          right={
            <IconButton
              icon="Trash2"
              label="Delete conversation"
              disabled={!conversation}
              onPress={() => setConfirm(true)}
            />
          }
        />
        {conversation && (
          <T variant="small">
            {conversation.courseTitle} · {conversation.scopeTitle} ·{' '}
            {conversation.language === 'fr' ? 'Français' : 'English'}
          </T>
        )}
      </View>
      <ScrollView
        ref={scroll}
        style={{ flex: 1 }}
        contentContainerStyle={{ paddingHorizontal: 22, paddingBottom: 20, gap: 16 }}
        keyboardShouldPersistTaps="handled"
        onContentSizeChange={() => {
          if (conversation && shouldScroll.current) {
            scroll.current?.scrollToEnd({ animated: true });
            shouldScroll.current = false;
          }
        }}
        onScroll={(event) => {
          const { layoutMeasurement, contentOffset, contentSize } = event.nativeEvent;
          nearBottom.current =
            contentSize.height - layoutMeasurement.height - contentOffset.y < 120;
        }}
        scrollEventThrottle={32}
      >
        <ResourceState {...resource} retry={resource.reload} />
        {conversation?.isFeedback && (
          <Notice>
            AI formative feedback on your saved attempt. Different well-supported reasoning can be
            valid. This is not an official mark or a claim that code was executed.
          </Notice>
        )}
        {conversation?.practice && (
          <View style={{ gap: 12 }}>
            {conversation.practice.usesEarlierSources && (
              <Notice>
                This feedback uses the original sources saved with your practice, which have since
                changed.
              </Notice>
            )}
            <Button
              label="View task and saved attempt"
              kind="secondary"
              onPress={() =>
                router.push({
                  pathname: '/job',
                  params: {
                    id: conversation.practice!.jobId,
                    practiceTaskId: conversation.practice!.taskId,
                  },
                })
              }
            />
            <Button
              label={showAttempt ? 'Hide your saved attempt' : 'Show your saved attempt'}
              kind="quiet"
              onPress={() => setShowAttempt((value) => !value)}
            />
            {showAttempt && (
              <View style={{ padding: 16, borderRadius: 16, backgroundColor: c.orangeSoft }}>
                <T selectable>{conversation.practice.answer}</T>
              </View>
            )}
          </View>
        )}
        {conversation && !conversation.turns.length && (
          <Empty
            title="What’s on your mind?"
            body="Ask about a course idea. You can then ask for an example, a hint, or an explanation of your reasoning."
          />
        )}
        {conversation && conversation.turns.length > visible && (
          <Button
            label="Show earlier messages"
            kind="quiet"
            onPress={() => {
              shouldScroll.current = false;
              setVisible((value) => value + 10);
              scroll.current?.scrollTo({ y: 0, animated: true });
            }}
          />
        )}
        {conversation?.turns.slice(-visible).map((turn) => (
          <View key={turn.id} style={{ gap: 12 }}>
            <View style={{ padding: 16, borderRadius: 16, backgroundColor: c.blueSoft, gap: 6 }}>
              <T variant="small" style={{ color: c.blue }}>
                You ·{' '}
                {turn.action === 'check'
                  ? 'Check reasoning'
                  : turn.action === 'hint'
                    ? 'Hint'
                    : 'Explain'}
              </T>
              <T selectable>{turn.question}</T>
            </View>
            <ReplyContent
              turn={turn}
              feedback={conversation.isFeedback}
              onSettled={settled}
              onCancel={() => stop(turn.jobId)}
              onRetry={() => {
                setQuestion(turn.question);
                setAction(turn.action);
              }}
            />
          </View>
        ))}
        <Button label="Refresh messages" kind="quiet" icon="RefreshCw" onPress={refresh} />
        <Button
          label="Your saved conversations"
          kind="quiet"
          onPress={() =>
            router.push({
              pathname: '/tutor',
              params: conversation ? { courseId: conversation.courseId } : {},
            })
          }
        />
      </ScrollView>
    </Page>
  );
}
