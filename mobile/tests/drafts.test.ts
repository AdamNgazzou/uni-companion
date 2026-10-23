import { test } from 'node:test';
import assert from 'node:assert/strict';
import { QuizDrafts, validDraft, type QuizDraft, type DraftStorage } from '../src/study/drafts';
import type { Question } from '../src/api/types';
const questions: Question[] = [
  {
    id: 'question-a',
    question: 'One?',
    options: [
      { id: 'option-a', text: 'A' },
      { id: 'option-b', text: 'B' },
    ],
    citations: [],
  },
];
const draft: QuizDraft = {
  version: 1,
  answers: { 'question-a': 'option-b' },
  questionId: 'question-a',
  attemptKey: 'attempt-stable',
  signature: 'submitted-choices',
  updatedAt: Date.now(),
};
function memory() {
  const values = new Map<string, string>();
  return {
    values,
    getItem: async (key: string) => values.get(key) || null,
    setItem: async (key: string, value: string) => {
      values.set(key, value);
    },
    removeItem: async (key: string) => {
      values.delete(key);
    },
    getAllKeys: async () => [...values.keys()],
  } satisfies DraftStorage & { values: Map<string, string> };
}
test('quiz choices, position and submission key survive a new repository instance per account', async () => {
  const storage = memory();
  const first = new QuizDrafts(storage);
  await first.save('owner-a', 'quiz-a', draft);
  const second = new QuizDrafts(storage);
  assert.deepEqual(await second.read('owner-a', 'quiz-a', questions), draft);
  assert.equal(await second.read('owner-b', 'quiz-a', questions), null);
  assert.equal(await second.read('owner-a', 'quiz-a', questions, 'retry-attempt'), null);
});
test('draft restoration excludes obsolete or foreign questions/options and expires old data', () => {
  const raw = JSON.stringify({
    ...draft,
    answers: { 'question-a': 'invented-option', 'foreign-question': 'option-b' },
    questionId: 'foreign-question',
  });
  const restored = validDraft(raw, questions)!;
  assert.deepEqual(restored.answers, {});
  assert.equal(restored.questionId, undefined);
  assert.equal(
    validDraft(JSON.stringify({ ...draft, updatedAt: Date.now() - 8 * 86400000 }), questions),
    null,
  );
  assert.equal(validDraft('not json', questions), null);
});
test('logout fences pending draft writes without deleting unrelated app storage', async () => {
  const storage = memory();
  storage.values.set('another-feature', 'keep');
  const repo = new QuizDrafts(storage);
  const pending = repo.save('owner', 'quiz', draft);
  await repo.clear();
  await pending;
  assert.equal(await repo.read('owner', 'quiz', questions), null);
  assert.equal(storage.values.get('another-feature'), 'keep');
});
test('completed quiz clears only its own full or retry draft', async () => {
  const repo = new QuizDrafts(memory());
  await repo.save('owner', 'quiz', draft);
  await repo.save('owner', 'quiz', draft, 'retry');
  await repo.remove('owner', 'quiz');
  assert.equal(await repo.read('owner', 'quiz', questions), null);
  assert.ok(await repo.read('owner', 'quiz', questions, 'retry'));
});

test('pruning a corrupt old draft preserves the newly saved choices', async () => {
  const storage = memory();
  storage.values.set('uni.quiz-draft.v1:corrupt', '{bad json');
  const repo = new QuizDrafts(storage);
  for (let i = 0; i < 30; i++) await repo.save('owner', 'quiz-' + i, draft);
  assert.equal(storage.values.size, 30);
  assert.equal(storage.values.has('uni.quiz-draft.v1:corrupt'), false);
  assert.deepEqual(await repo.read('owner', 'quiz-29', questions), draft);
});
