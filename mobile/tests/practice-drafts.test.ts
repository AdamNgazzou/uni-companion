import test from 'node:test';
import assert from 'node:assert/strict';
import { PracticeDrafts, type PracticeDraft } from '../src/practice/drafts';
import type { DraftStorage } from '../src/study/drafts';
class Memory implements DraftStorage {
  values = new Map<string, string>();
  async getItem(key: string) {
    return this.values.get(key) || null;
  }
  async setItem(key: string, value: string) {
    this.values.set(key, value);
  }
  async removeItem(key: string) {
    this.values.delete(key);
  }
  async getAllKeys() {
    return [...this.values.keys()];
  }
}
const value: PracticeDraft = {
  answer: 'Private code or reasoning',
  requestKey: 'attempt-one',
  lastAnswer: 'Private code or reasoning',
  updatedAt: Date.now(),
};
test('practice text and retry key survive reopening only for their account, job and task', async () => {
  const storage = new Memory();
  const drafts = new PracticeDrafts(storage);
  await drafts.save('alice', 'job', 'task', value, 0);
  assert.deepEqual(await new PracticeDrafts(storage).read('alice', 'job', 'task', 0), value);
  assert.equal(await drafts.read('bob', 'job', 'task', 0), null);
  assert.equal(await drafts.read('alice', 'other', 'task', 0), null);
  assert.equal(await drafts.read('alice', 'job', 'other', 0), null);
  await drafts.remove('alice', 'job', 'task');
  assert.equal(await drafts.read('alice', 'job', 'task', 0), null);
});
test('logout fences old practice writes and clears only practice drafts', async () => {
  const storage = new Memory();
  storage.values.set('other-app', 'keep');
  const drafts = new PracticeDrafts(storage);
  const old = drafts.generation();
  const pending = drafts.save('alice', 'job', 'task', value, old);
  await Promise.all([pending, drafts.clear()]);
  await drafts.save('alice', 'job', 'task', value, old);
  assert.equal(await drafts.read('alice', 'job', 'task', drafts.generation()), null);
  assert.equal(storage.values.get('other-app'), 'keep');
});
test('expired and invalid practice drafts cannot restore text or request identities', async () => {
  const storage = new Memory();
  const drafts = new PracticeDrafts(storage);
  await drafts.save('alice', 'job', 'task', { ...value, updatedAt: Date.now() - 8 * 86400000 }, 0);
  assert.equal(await drafts.read('alice', 'job', 'task', 0), null);
  storage.values.set([...storage.values.keys()][0], '{broken');
  assert.equal(await drafts.read('alice', 'job', 'task', 0), null);
});
