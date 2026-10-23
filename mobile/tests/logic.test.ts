import assert from 'node:assert/strict';
import { test } from 'node:test';
import { nextVote, quizResult, visibleAuthor } from '../src/logic';

test('a vote changes direction or removes itself, never accumulates', () => {
  assert.equal(nextVote(0, 1), 1);
  assert.equal(nextVote(1, 1), 0);
  assert.equal(nextVote(1, -1), -1);
  assert.equal(nextVote(-1, 1), 1);
  assert.equal(nextVote(-1, -1), 0);
  let vote: -1 | 0 | 1 = 0;
  const baseScore = 24;
  for (let i = 0; i < 100; i++) vote = nextVote(vote, 1);
  assert.equal(baseScore + vote, baseScore);
});

test('anonymous names stay hidden, including original-author replies', () => {
  const person = { author: 'Private Name', ownerId: 'student-42', anonymous: true };
  assert.equal(visibleAuthor(person), 'Anonymous');
  assert.equal(visibleAuthor(person, { ownerId: 'student-42' }), 'Anonymous author');
  assert.equal(visibleAuthor(person, { ownerId: 'someone-else' }), 'Anonymous');
  assert.equal(visibleAuthor({ ...person, anonymous: false }), 'Private Name');
});

test('quiz results preserve original question ids for a missed-question retry', () => {
  assert.deepEqual(
    quizResult([
      { id: 3, correct: false },
      { id: 0, correct: true },
      { id: 4, correct: false },
    ]),
    { correct: 1, total: 3, missed: [3, 4] },
  );
  assert.deepEqual(quizResult([{ id: 4, correct: true }]), { correct: 1, total: 1, missed: [] });
});
