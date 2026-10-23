import React from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { router } from 'expo-router';
import type { Citation, Job } from '../api/types';
import { Button, Hero, Icon, Notice, T, layout } from '../ui';
import { colors as c, fonts } from '../theme';

export const sourceName = (value: string) =>
  value
    .replace(/^[a-f0-9]{8}-\d+-/i, '')
    .replace(/\.pdf$/i, '')
    .replaceAll('_', ' ')
    .trim();
export const stateLabel = (value: string) =>
  ({
    queued: 'Queued',
    running: 'In progress',
    retry_wait: 'Trying again shortly',
    succeeded: 'Ready',
    failed: 'Needs another try',
    cancelled: 'Cancelled',
  })[value] || 'Saved session';
function Meta({
  label,
  icon = 'BookOpen',
}: {
  label: string;
  icon?: 'BookOpen' | 'CheckCircle2' | 'FileText';
}) {
  return (
    <View
      style={{
        flexDirection: 'row',
        gap: 6,
        alignItems: 'center',
        paddingVertical: 7,
        paddingHorizontal: 10,
        backgroundColor: c.canvas,
        borderRadius: 10,
      }}
    >
      <Icon name={icon} size={15} color={icon === 'CheckCircle2' ? c.green : c.muted} />
      <T variant="small">{label}</T>
    </View>
  );
}
function StudyText({ text }: { text: string }) {
  return (
    <T selectable style={{ fontSize: 17, lineHeight: 28 }}>
      {text.split(/(\*\*[^*\n]+\*\*)/g).map((part, index) =>
        part.startsWith('**') && part.endsWith('**') ? (
          <T key={index} style={{ fontFamily: fonts.bold, fontSize: 17, lineHeight: 28 }}>
            {part.slice(2, -2)}
          </T>
        ) : (
          part
        ),
      )}
    </T>
  );
}
export function StudyArticle({
  job,
  renderCitations,
}: {
  job: Job;
  renderCitations: (citations: Citation[]) => React.ReactNode;
}) {
  const summary = job.kind === 'summary';
  const paragraphs = job.result?.paragraphs || [];
  const minutes = Math.max(
    1,
    Math.ceil(paragraphs.reduce((count, p) => count + p.text.split(/\s+/).length, 0) / 200),
  );
  const sources = new Set(
    paragraphs.flatMap((p) => p.citations.map((source) => source.versionId + ':' + source.page)),
  ).size;
  function study(kind: 'answer' | 'quiz') {
    router.push({
      pathname: '/study',
      params: {
        courseId: job.courseId,
        kind,
        materialId: job.request.materialId || '',
        chapterId: job.request.chapterId || '',
        title: job.scopeTitle || '',
        language: job.request.language,
      },
    });
  }
  return (
    <View style={{ gap: 25 }}>
      <View style={{ gap: 12 }}>
        <T variant="hero" accessibilityRole="header">
          {summary ? 'The key ideas.' : 'Here’s the idea.'}
        </T>
        <T variant="label" style={{ color: c.muted }}>
          {job.scopeTitle || job.courseTitle || 'Your course materials'}
        </T>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 7 }}>
          <Meta
            label={
              summary
                ? `~${minutes} min read`
                : `${sources} source ${sources === 1 ? 'page' : 'pages'}`
            }
          />
          {summary && <Meta label={`${job.result?.totalPages || 0} pages`} icon="FileText" />}
          <Meta label="Saved" icon="CheckCircle2" />
        </View>
      </View>
      {!!job.request.question && (
        <View style={{ padding: 18, borderRadius: 18, backgroundColor: c.orangeSoft, gap: 7 }}>
          <T variant="small" style={{ color: '#795219' }}>
            Your question
          </T>
          <T style={{ fontFamily: fonts.bold }}>{job.request.question}</T>
        </View>
      )}
      {job.result?.insufficientEvidence ? (
        <Notice>
          These materials don’t give us enough evidence for a reliable answer. Try narrowing the
          question or choosing another resource.
        </Notice>
      ) : (
        paragraphs.map((paragraph, index) => (
          <View
            key={index}
            style={{
              gap: 10,
              paddingTop: index ? 7 : 0,
              paddingBottom: 17,
              borderBottomWidth: index < paragraphs.length - 1 ? 1 : 0,
              borderBottomColor: c.line,
            }}
          >
            {!!paragraph.heading && (
              <T variant="title" accessibilityRole="header">
                {paragraph.heading}
              </T>
            )}
            <StudyText text={paragraph.text} />
            {renderCitations(paragraph.citations)}
          </View>
        ))
      )}
      {summary && job.result && (
        <View style={{ gap: 10 }}>
          <View style={layout.inline}>
            <Icon name="CheckCircle2" size={18} color={c.green} />
            <T variant="small" style={{ flex: 1 }}>
              {job.result.coveredPages} of {job.result.totalPages} PDF pages reviewed
            </T>
          </View>
          {job.result.coveredPages < job.result.totalPages && (
            <Notice>
              Some original pages have no extractable text and may need OCR. Check them in the
              original PDF.
            </Notice>
          )}
        </View>
      )}
      {!job.result?.insufficientEvidence && !!(job.request.materialId || job.request.chapterId) && (
        <View style={{ backgroundColor: c.blueSoft, padding: 20, gap: 12, borderRadius: 20 }}>
          <T variant="title">Make it stick.</T>
          <T>Try a few questions while the ideas are fresh.</T>
          <Button label="Practise these ideas" icon="ArrowRight" onPress={() => study('quiz')} />
          <Button label="Ask about this material" kind="quiet" onPress={() => study('answer')} />
        </View>
      )}
      <T variant="small">A study companion, with the original pages always one tap away.</T>
    </View>
  );
}
export function StudyProgress({ job }: { job: Job }) {
  return (
    <View style={{ gap: 20 }}>
      <Hero
        title={
          job.kind === 'summary'
            ? 'Pulling the ideas together.'
            : job.kind === 'quiz'
              ? 'A little practice is on its way.'
              : 'Let’s work through it.'
        }
        subtitle={job.scopeTitle || 'Grounded in your course materials.'}
      />
      <View style={[layout.inline, { padding: 18, borderRadius: 16, backgroundColor: c.blueSoft }]}>
        <ActivityIndicator color={c.blue} />
        <View style={{ flex: 1, gap: 4 }}>
          <T variant="label">{stateLabel(job.state)}</T>
          <T variant="small">{job.stage}</T>
        </View>
      </View>
      <Notice>
        You can leave this screen. Your session keeps going and will be waiting in Study history.
      </Notice>
    </View>
  );
}
export function HistoryEntry({ job, onPress }: { job: Job; onPress: () => void }) {
  const kind =
    job.kind === 'quiz'
      ? `Practice · ${job.request.count || 5} questions`
      : job.kind === 'summary'
        ? 'Summary'
        : 'Explanation';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${kind}: ${job.title || job.request.question || job.scopeTitle || 'Course material'}, ${stateLabel(job.state)}`}
      onPress={onPress}
      style={({ pressed }) => ({
        paddingVertical: 19,
        borderBottomWidth: 1,
        borderBottomColor: c.line,
        gap: 10,
        opacity: pressed ? 0.65 : 1,
      })}
    >
      <View style={[layout.inline, { justifyContent: 'space-between' }]}>
        <T variant="small" style={{ color: c.blue, fontFamily: fonts.bold }}>
          {kind}
        </T>
        <T variant="small" style={{ color: job.state === 'succeeded' ? c.green : c.muted }}>
          {stateLabel(job.state)}
        </T>
      </View>
      <View style={layout.inline}>
        <View style={{ flex: 1, gap: 6 }}>
          <T variant="label" numberOfLines={3}>
            {job.title || job.request.question || job.scopeTitle || 'Course material'}
          </T>
          <T variant="small">
            {job.courseTitle || 'Your course'} ·{' '}
            {new Date(job.createdAt).toLocaleDateString(undefined, {
              day: 'numeric',
              month: 'short',
            })}
          </T>
        </View>
        <Icon name="ChevronRight" size={19} color={c.muted} />
      </View>
    </Pressable>
  );
}
