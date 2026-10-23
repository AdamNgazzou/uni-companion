import React, { useState } from 'react';
import { Platform, Pressable, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import * as DocumentPicker from 'expo-document-picker';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import * as Haptics from 'expo-haptics';
import {
  Button,
  Chip,
  Empty,
  Field,
  Header,
  Hero,
  Icon,
  IconButton,
  Mascot,
  Notice,
  Page,
  Row,
  Section,
  T,
  layout,
  useNav,
} from '../ui';
import { colors as c, fonts } from '../theme';
import { questions, summarySections } from '../data';
import { quizResult } from '../logic';
import { uid, useSaved, useStore } from '../store';
import { RootStack } from '../types';
function getSections(id: string) {
  if (id === 'joins')
    return [
      {
        title: 'INNER JOIN',
        short: 'Only matches',
        body: 'An inner join returns rows where the join condition matches in both tables.',
        page: 3,
      },
      {
        title: 'LEFT JOIN',
        short: 'Keep the left side',
        body: 'A left join keeps every row from the left table. Missing values from the right table become NULL.',
        page: 6,
      },
      {
        title: 'Joining with keys',
        short: 'Connect related rows',
        body: 'Join Student.DepartmentID to Department.ID to find a student’s department name.',
        page: 10,
      },
    ];
  if (id === 'rel')
    return [
      {
        title: 'Relations',
        short: 'Rows and columns',
        body: 'A relation represents a collection of records with named attributes.',
        page: 2,
      },
      {
        title: 'Candidate keys',
        short: 'Identify a row',
        body: 'A candidate key is a minimal set of attributes that uniquely identifies a row.',
        page: 5,
      },
      {
        title: 'Foreign keys',
        short: 'Connect tables',
        body: 'A foreign key references a key in a related table and helps preserve referential integrity.',
        page: 8,
      },
    ];
  return summarySections;
}
export function UploadScreen({ route }: NativeStackScreenProps<RootStack, 'Upload'>) {
  const nav = useNav();
  const { data, update } = useStore();
  const course = data.courses.find((x) => x.id === route.params.courseId);
  const [file, setFile] = useState<DocumentPicker.DocumentPickerAsset | null>(null);
  const [title, setTitle] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function choose() {
    try {
      const result = await DocumentPicker.getDocumentAsync({
        type: 'application/pdf',
        copyToCacheDirectory: true,
      });
      if (!result.canceled) {
        const asset = result.assets[0];
        if (!asset.name.toLowerCase().endsWith('.pdf')) {
          setError('Choose a PDF file.');
          return;
        }
        if ((asset.size ?? 0) > 25 * 1024 * 1024) {
          setError('Choose a PDF smaller than 25 MB.');
          return;
        }
        setFile(asset);
        setTitle(asset.name.replace(/\.pdf$/i, ''));
        setError('');
      }
    } catch {
      setError('Couldn’t open the file picker. Please try again.');
    }
  }
  async function save() {
    if (!file || !title.trim()) return;
    setBusy(true);
    try {
      const id = uid();
      let uri = file.uri;
      if (Platform.OS !== 'web' && FileSystem.documentDirectory) {
        uri = `${FileSystem.documentDirectory}${id}.pdf`;
        await FileSystem.copyAsync({ from: file.uri, to: uri });
      }
      update((s) => ({
        ...s,
        materials: [
          ...s.materials,
          {
            id,
            title: title.trim(),
            courseId: route.params.courseId,
            pages: 0,
            fileName: file.name,
            uri: Platform.OS === 'web' ? undefined : uri,
            sample: false,
            status: 'ready',
          },
        ],
      }));
      nav.replace('Document', { id });
    } catch {
      setError('Couldn’t save that PDF on this device. Try again.');
    } finally {
      setBusy(false);
    }
  }
  return (
    <Page>
      <Header title="Add course material" />
      <T variant="heading">A fresh set of notes.</T>
      <T style={{ color: c.muted }}>
        {course?.code} · {course?.title}
      </T>
      {file ? (
        <Row
          title={file.name}
          detail={`${((file.size ?? 0) / 1024 / 1024).toFixed(1)} MB · PDF`}
          icon="FileText"
          right={<IconButton label="Remove selected file" icon="X" onPress={() => setFile(null)} />}
        />
      ) : (
        <View style={[layout.card, { alignItems: 'center', paddingVertical: 30 }]}>
          <Icon name="Upload" size={40} color={c.blue} />
          <T variant="title">Bring your notes along.</T>
          <Button label="Choose a PDF" kind="secondary" onPress={choose} />
        </View>
      )}
      <Field
        label="Lecture title"
        value={title}
        onChangeText={setTitle}
        placeholder="e.g. Lecture 06 — Normalization"
        maxLength={120}
      />
      <Notice>
        Local prototype: this PDF stays on this device. Sharing and AI processing will be connected
        later.
      </Notice>
      {Platform.OS === 'web' && (
        <T variant="small">
          The browser preview saves file details. Use Android to keep and share the PDF itself.
        </T>
      )}
      {error && <Notice tone="error">{error}</Notice>}
      <Button
        label="Add PDF to course"
        disabled={!file || !title.trim()}
        busy={busy}
        icon="Plus"
        onPress={save}
      />
      {file && <Button label="Choose another file" kind="quiet" onPress={choose} />}
    </Page>
  );
}
export function DocumentScreen({ route }: NativeStackScreenProps<RootStack, 'Document'>) {
  const nav = useNav();
  const { data } = useStore();
  const material = data.materials.find((x) => x.id === route.params.id);
  const [page, setPage] = useState(route.params.page ?? 1);
  const [error, setError] = useState('');
  const bookmark = useSaved(route.params.id);
  if (!material)
    return (
      <Page>
        <Header title="Material" />
        <Empty
          title="Material not found"
          body="Return to your course and choose another document."
        />
      </Page>
    );
  const sections = getSections(material.id);
  const active = [...sections].reverse().find((x) => x.page <= page) ?? sections[0];
  async function openFile() {
    try {
      if (material?.uri && (await Sharing.isAvailableAsync()))
        await Sharing.shareAsync(material.uri, {
          mimeType: 'application/pdf',
          dialogTitle: 'Open or share PDF',
        });
      else setError('This PDF is available only on the device where it was imported.');
    } catch {
      setError('Couldn’t open the PDF. Try importing it again.');
    }
  }
  return (
    <Page
      footer={
        material.sample ? (
          <View style={layout.inline}>
            {(['Summary', 'Ask', 'QuizSetup'] as const).map((target, index) => (
              <View key={target} style={{ flex: 1 }}>
                <Button
                  label={['Summary', 'Ask', 'Practise'][index]}
                  kind="secondary"
                  onPress={() => nav.navigate(target, { id: material.id })}
                />
              </View>
            ))}
          </View>
        ) : undefined
      }
    >
      <Header
        title={material.title}
        right={
          <IconButton
            icon="Bookmark"
            label={bookmark.saved ? 'Unsave material' : 'Save material'}
            selected={bookmark.saved}
            onPress={bookmark.toggle}
            color={c.blue}
          />
        }
      />
      {material.sample ? (
        <>
          <View
            style={[
              layout.inline,
              { justifyContent: 'space-between', backgroundColor: c.blueSoft, borderRadius: 14 },
            ]}
          >
            <IconButton
              icon="ArrowLeft"
              label="Previous page"
              onPress={() => setPage((p) => Math.max(1, p - 1))}
            />
            <T variant="label">
              Page {page} of {material.pages}
            </T>
            <IconButton
              icon="ArrowRight"
              label="Next page"
              onPress={() => setPage((p) => Math.min(material.pages, p + 1))}
            />
          </View>
          <T variant="small">Sample lecture reader · illustrative content</T>
          <View
            style={{ borderWidth: 1, borderColor: c.line, borderRadius: 16, padding: 22, gap: 22 }}
          >
            <T variant="heading">{material.title}</T>
            <T>This lecture explores how we organise and connect data in a relational database.</T>
            <T variant="title">{active.title}</T>
            <T>{active.body}</T>
            <T variant="label">Example: Student table</T>
            <View
              style={{ borderWidth: 1, borderColor: c.line, borderRadius: 8, overflow: 'hidden' }}
            >
              {[
                ['ID', 'Name', 'Dept.'],
                ['S001', 'Ada', 'D01'],
                ['S002', 'Malik', 'D02'],
                ['S003', 'Sam', 'D01'],
              ].map((row, i) => (
                <View
                  key={i}
                  style={{
                    flexDirection: 'row',
                    paddingVertical: 11,
                    paddingHorizontal: 10,
                    backgroundColor: i === 0 ? c.blueSoft : c.white,
                    borderBottomWidth: i === 3 ? 0 : 1,
                    borderBottomColor: c.line,
                  }}
                >
                  {row.map((cell, j) => (
                    <T
                      key={j}
                      variant={i === 0 ? 'label' : 'body'}
                      style={{ flex: 1, fontSize: 13 }}
                    >
                      {cell}
                    </T>
                  ))}
                </View>
              ))}
            </View>
            <T variant="title">Think about it</T>
            <T>
              Which columns identify a record, and which describe it? Look for repeated information
              and dependencies between attributes.
            </T>
            <View style={layout.card}>
              <T variant="label">A useful question</T>
              <T>What changes if a department is renamed?</T>
            </View>
          </View>
        </>
      ) : (
        <>
          <Hero title="Your PDF is here." subtitle={material.fileName} />
          <Notice tone="success">
            File details are saved locally. AI study tools aren’t connected for imported PDFs yet.
          </Notice>
          <Button label="Open or share PDF" icon="ExternalLink" onPress={openFile} />
          <Button
            label="Try the sample study tools"
            kind="secondary"
            onPress={() => nav.navigate('Summary', { id: 'norm' })}
          />
          {error && <Notice tone="error">{error}</Notice>}
        </>
      )}
    </Page>
  );
}
export function SummaryScreen({ route }: NativeStackScreenProps<RootStack, 'Summary'>) {
  const nav = useNav();
  const { data } = useStore();
  const material = data.materials.find((x) => x.id === route.params.id);
  const bookmark = useSaved(route.params.id);
  if (!material?.sample)
    return (
      <Page>
        <Header title="Summary" />
        <Empty
          title="Your notes, explained soon."
          body="AI generation isn’t connected in this local prototype. Try the sample lecture."
          action="Open sample summary"
          onPress={() => nav.replace('Summary', { id: 'norm' })}
        />
      </Page>
    );
  return (
    <Page
      footer={
        <>
          <Button
            label="Practise this lecture"
            icon="ArrowRight"
            onPress={() => nav.navigate('QuizSetup', { id: material.id })}
          />
          <Button
            label="Ask your notes"
            kind="quiet"
            onPress={() => nav.navigate('Ask', { id: material.id })}
          />
        </>
      }
    >
      <Header
        title="Lecture summary"
        right={
          <IconButton
            icon="Bookmark"
            label={bookmark.saved ? 'Unsave material' : 'Save material'}
            selected={bookmark.saved}
            color={c.blue}
            onPress={bookmark.toggle}
          />
        }
      />
      <T variant="hero">
        {material.title},{'\n'}in plain words.
      </T>
      <T variant="small">{material.fileName} · sample summary</T>
      {getSections(material.id).map((section, index) => (
        <View
          key={section.title}
          style={{ gap: 12, paddingVertical: 13, borderBottomColor: c.line, borderBottomWidth: 1 }}
        >
          <View style={layout.inline}>
            <View
              style={{
                width: 34,
                height: 34,
                backgroundColor: c.blue,
                borderRadius: 12,
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <T variant="label" style={{ color: c.white }}>
                {index + 1}
              </T>
            </View>
            <T variant="title" style={{ flex: 1 }}>
              {section.title}
            </T>
          </View>
          <T style={{ marginLeft: 44 }}>{section.body}</T>
          <Pressable
            accessibilityRole="button"
            onPress={() => nav.navigate('Document', { id: material.id, page: section.page })}
            style={[layout.inline, { marginLeft: 44, minHeight: 48 }]}
          >
            <Icon name="BookOpen" size={18} color={c.blue} />
            <T style={layout.link}>See page {section.page}</T>
            <Icon name="ArrowRight" size={17} color={c.blue} />
          </Pressable>
        </View>
      ))}
      <View style={layout.card}>
        <T variant="label">Take it one idea at a time.</T>
        <T>
          Try explaining the topic in your own words, then check your understanding with a few
          questions.
        </T>
      </View>
    </Page>
  );
}
export function AskScreen({ route }: NativeStackScreenProps<RootStack, 'Ask'>) {
  const nav = useNav();
  const { data } = useStore();
  const material = data.materials.find((x) => x.id === route.params.id);
  const [draft, setDraft] = useState('');
  const [messages, setMessages] = useState<
    { role: 'you' | 'notes'; body: string; page?: number }[]
  >([]);
  function send(value: string) {
    const q = value.trim();
    if (!q) return;
    let answer =
      'This offline preview has sample explanations for 1NF, 2NF, and 3NF in the Normalization lecture. Live AI is not connected.';
    let page: number | undefined;
    if (material?.id === 'norm') {
      const lower = q.toLowerCase();
      if (/2nf|second|partial/.test(lower) && /3nf|third|difference|different/.test(lower)) {
        answer =
          '2NF removes partial dependencies: non-key attributes depend on the whole candidate key. 3NF then removes transitive dependencies: a non-key attribute should not depend on the key through another non-key attribute.';
        page = 14;
      } else {
        const section = /3nf|third|transitive/.test(lower)
          ? summarySections[2]
          : /2nf|second|partial/.test(lower)
            ? summarySections[1]
            : /1nf|first|atomic/.test(lower)
              ? summarySections[0]
              : null;
        if (section) {
          answer = section.body;
          page = section.page;
        }
      }
    }
    setMessages((old) => [...old, { role: 'you', body: q }, { role: 'notes', body: answer, page }]);
    setDraft('');
  }
  return (
    <Page
      footer={
        <View style={layout.inline}>
          <View style={{ flex: 1 }}>
            <Field
              label="Ask a follow-up"
              value={draft}
              onChangeText={setDraft}
              placeholder="What’s confusing you?"
              onSubmitEditing={() => send(draft)}
              maxLength={500}
            />
          </View>
          <IconButton
            icon="Send"
            label="Send question"
            color={c.blue}
            onPress={() => send(draft)}
          />
        </View>
      }
    >
      <Header title="Ask your notes" />
      <View style={layout.card}>
        <T variant="label">{material?.fileName ?? 'Selected lecture'}</T>
        <T variant="small">Sample answers · no live AI connection</T>
      </View>
      {!messages.length && (
        <>
          <Hero title="Let’s untangle it." subtitle="Start with one small question." />
          <Chip
            label="How is 2NF different from 3NF?"
            onPress={() => send('How is 2NF different from 3NF?')}
          />
          <Chip
            label="Explain transitive dependencies"
            onPress={() => send('Explain transitive dependencies')}
          />
        </>
      )}
      {messages.map((message, i) => (
        <View
          key={i}
          style={{
            padding: 17,
            gap: 10,
            borderRadius: 18,
            backgroundColor: message.role === 'you' ? c.blueSoft : c.canvas,
            marginLeft: message.role === 'you' ? 25 : 0,
            marginRight: message.role === 'notes' ? 10 : 0,
          }}
        >
          <T variant="small" style={{ color: c.blue, fontFamily: fonts.bold }}>
            {message.role === 'you' ? 'You' : 'Your notes · sample'}
          </T>
          <T>{message.body}</T>
          {message.page && (
            <Button
              label={`Read source · p. ${message.page}`}
              kind="quiet"
              icon="BookOpen"
              onPress={() => nav.navigate('Document', { id: route.params.id, page: message.page })}
            />
          )}
        </View>
      ))}
    </Page>
  );
}
export function QuizSetupScreen({ route }: NativeStackScreenProps<RootStack, 'QuizSetup'>) {
  const nav = useNav();
  const { data } = useStore();
  const [count, setCount] = useState(5);
  const [mistakes, setMistakes] = useState(false);
  const material = data.materials.find((x) => x.id === route.params.id);
  const last = [...data.attempts].reverse().find((x) => x.materialId === route.params.id);
  const missed = last?.missed ?? [];
  if (route.params.id !== 'norm')
    return (
      <Page>
        <Header title="Quick practice" />
        <Empty
          title="Try a little practice."
          body="The local quiz preview is available for the Normalization lecture."
          action="Open sample quiz"
          onPress={() => nav.replace('QuizSetup', { id: 'norm' })}
        />
      </Page>
    );
  return (
    <Page
      footer={
        <Button
          label={
            mistakes
              ? `Practise ${missed.length} missed question${missed.length === 1 ? '' : 's'}`
              : `Start ${count} questions`
          }
          disabled={mistakes && !missed.length}
          onPress={() =>
            nav.replace('Quiz', {
              id: route.params.id,
              count,
              missed: mistakes ? missed : undefined,
            })
          }
          icon="ArrowRight"
        />
      }
    >
      <Header title="Quick practice" />
      <Hero title="Make it a small win." subtitle="Quick questions. Clearer thinking." />
      <Row
        icon="Database"
        title={material?.title ?? 'Normalization'}
        detail="CSC 301 · sample quiz"
      />
      <T variant="label">Number of questions</T>
      <View style={layout.inline}>
        {[3, 5].map((value) => (
          <View key={value} style={{ flex: 1 }}>
            <Chip
              label={`${value} questions`}
              selected={count === value}
              onPress={() => setCount(value)}
            />
          </View>
        ))}
      </View>
      <T variant="label">Focus on</T>
      <Chip label="Whole lecture" selected={!mistakes} onPress={() => setMistakes(false)} />
      <Chip
        label={`Previous mistakes${missed.length ? ` (${missed.length})` : ''}`}
        selected={mistakes}
        onPress={() => setMistakes(true)}
      />
      {mistakes && !missed.length && (
        <Notice>No missed questions yet. Try a whole-lecture quiz first.</Notice>
      )}
      <Notice>
        Answers include explanations and source pages. These are prewritten sample questions.
      </Notice>
    </Page>
  );
}
export function QuizScreen({ route }: NativeStackScreenProps<RootStack, 'Quiz'>) {
  const nav = useNav();
  const { update } = useStore();
  const ids = route.params.missed?.length
    ? route.params.missed
    : questions.map((_, i) => i).slice(0, route.params.count);
  const [index, setIndex] = useState(0);
  const [selected, setSelected] = useState<number | null>(null);
  const [checked, setChecked] = useState(false);
  const [answers, setAnswers] = useState<{ id: number; correct: boolean }[]>([]);
  const current = questions[ids[index]];
  const correct = selected === current.answer;
  function next() {
    const complete = [...answers, { id: ids[index], correct }];
    if (index < ids.length - 1) {
      setAnswers(complete);
      setIndex(index + 1);
      setSelected(null);
      setChecked(false);
    } else {
      const id = uid();
      update((s) => ({
        ...s,
        attempts: [
          ...s.attempts,
          {
            id,
            courseId: 'db',
            materialId: route.params.id,
            ...quizResult(complete),
            createdAt: Date.now(),
          },
        ],
      }));
      nav.replace('Results', { attemptId: id });
    }
  }
  return (
    <Page
      footer={
        <>
          <Button
            label={
              checked
                ? index === ids.length - 1
                  ? 'See my results'
                  : 'Next question'
                : 'Check answer'
            }
            disabled={selected === null}
            onPress={() => {
              if (checked) next();
              else {
                setChecked(true);
                if (Platform.OS !== 'web')
                  void Haptics.notificationAsync(
                    correct
                      ? Haptics.NotificationFeedbackType.Success
                      : Haptics.NotificationFeedbackType.Warning,
                  ).catch(() => {});
              }
            }}
            icon={checked ? 'ArrowRight' : undefined}
          />
          {checked && (
            <Button
              label="Discuss this question"
              kind="quiet"
              icon="MessageCircle"
              onPress={() =>
                nav.navigate('Compose', { courseId: 'db', question: current.question })
              }
            />
          )}
        </>
      }
    >
      <Header title="Quick practice" />
      <View style={{ height: 10, borderRadius: 8, backgroundColor: c.line, overflow: 'hidden' }}>
        <View
          style={{
            backgroundColor: c.blue,
            width: `${((index + 1) / ids.length) * 100}%`,
            height: 10,
            borderRadius: 8,
          }}
        />
      </View>
      <T variant="small">
        Question {index + 1} of {ids.length}
      </T>
      <View style={[layout.inline, { marginVertical: 8 }]}>
        <T variant="hero" style={{ flex: 1 }}>
          {current.question}
        </T>
        <Mascot size={96} />
      </View>
      {current.options.map((option, i) => {
        const chosen = selected === i;
        const good = checked && i === current.answer;
        const bad = checked && chosen && !correct;
        return (
          <Pressable
            key={`${index}-${i}`}
            accessibilityRole="radio"
            accessibilityState={{ checked: chosen, disabled: checked }}
            disabled={checked}
            onPress={() => setSelected(i)}
            style={{
              borderWidth: 2,
              borderColor: good ? c.green : bad ? c.red : chosen ? c.blue : c.line,
              backgroundColor: good ? c.greenSoft : bad ? c.redSoft : chosen ? c.blueSoft : c.white,
              borderRadius: 17,
              padding: 15,
              flexDirection: 'row',
              alignItems: 'center',
              gap: 13,
              minHeight: 65,
            }}
          >
            <View
              style={{
                width: 30,
                height: 30,
                borderRadius: 11,
                backgroundColor: good ? c.green : chosen ? c.blue : c.blueSoft,
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <T variant="label" style={{ color: chosen || good ? c.white : c.ink }}>
                {String.fromCharCode(65 + i)}
              </T>
            </View>
            <T variant="label" style={{ flex: 1 }}>
              {option}
            </T>
            {good && <Icon name="CheckCircle2" color={c.green} size={23} />}
          </Pressable>
        );
      })}
      {checked && (
        <View style={[layout.card, { backgroundColor: correct ? c.greenSoft : c.orangeSoft }]}>
          <T variant="title" style={{ color: correct ? c.green : '#8B5616' }}>
            {correct ? 'You’ve got it!' : 'One to learn from.'}
          </T>
          <T>{current.explanation}</T>
          <Button
            label={`Review source · p. ${current.page}`}
            kind="quiet"
            onPress={() => nav.navigate('Document', { id: route.params.id, page: current.page })}
          />
        </View>
      )}
    </Page>
  );
}
export function ResultsScreen({ route }: NativeStackScreenProps<RootStack, 'Results'>) {
  const nav = useNav();
  const { data } = useStore();
  const attempt = data.attempts.find((x) => x.id === route.params.attemptId);
  if (!attempt)
    return (
      <Page>
        <Header title="Results" />
        <Empty title="No results yet" body="Take a quiz to see how you did." />
      </Page>
    );
  return (
    <Page>
      <Header title="Quiz results" />
      <Hero
        title={attempt.correct === attempt.total ? 'Look at you go.' : 'That’s progress.'}
        subtitle="A little clearer than before."
      />
      <View style={[layout.card, { alignItems: 'center', paddingVertical: 25 }]}>
        <T variant="small">Normalization · CSC 301</T>
        <T variant="hero" style={{ color: c.blue, fontSize: 43, lineHeight: 50 }}>
          {attempt.correct} of {attempt.total}
        </T>
        <T variant="label">questions correct</T>
      </View>
      <Section
        title={attempt.missed.length ? 'A few things to revisit' : 'You’ve earned a breather.'}
      />
      {attempt.missed.map((id) => (
        <View
          key={id}
          style={{ gap: 10, borderBottomWidth: 1, borderBottomColor: c.line, paddingBottom: 16 }}
        >
          <T variant="label">{questions[id].question}</T>
          <T>{questions[id].explanation}</T>
          <Button
            label={`Read source · p. ${questions[id].page}`}
            kind="quiet"
            onPress={() =>
              nav.navigate('Document', { id: attempt.materialId, page: questions[id].page })
            }
          />
        </View>
      ))}
      {!!attempt.missed.length && (
        <Button
          label="Practise missed questions"
          onPress={() =>
            nav.replace('Quiz', {
              id: attempt.materialId,
              count: attempt.missed.length,
              missed: attempt.missed,
            })
          }
        />
      )}
      <Button
        label="Back to course"
        kind="secondary"
        onPress={() => nav.navigate('Course', { id: attempt.courseId })}
      />
      <Button label="Go home" kind="quiet" onPress={() => nav.popToTop()} />
    </Page>
  );
}
