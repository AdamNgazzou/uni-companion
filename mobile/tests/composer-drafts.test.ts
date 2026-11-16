import test from 'node:test';
import assert from 'node:assert/strict';
import { ComposerDrafts, type ComposerDraft } from '../src/community/drafts';
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
const value: ComposerDraft = {
  title: 'A question',
  body: 'Private draft',
  kind: 'Question',
  courseId: 'course',
  anonymous: true,
  requestKey: 'request-one',
  lastPayload: 'original-body',
  updatedAt: Date.now(),
};
test('composer restores identity and submission key only for its account and destination', async () => {
  const storage = new Memory();
  const drafts = new ComposerDrafts(storage);
  const generation = drafts.generation();
  await drafts.save('alice', 'new', value, generation);
  assert.deepEqual(await new ComposerDrafts(storage).read('alice', 'new', 0), value);
  assert.equal(await drafts.read('bob', 'new', generation), null);
  assert.equal(await drafts.read('alice', 'reply:other', generation), null);
  await drafts.remove('alice', 'new');
  assert.equal(await drafts.read('alice', 'new', generation), null);
});
test('logout fences saves scheduled by an old screen and preserves other app storage', async () => {
  const storage = new Memory();
  storage.values.set('other-app', 'keep');
  const drafts = new ComposerDrafts(storage);
  const generation = drafts.generation();
  const pending = drafts.save('alice', 'new', value, generation);
  const clear = drafts.clear();
  await Promise.all([pending, clear]);
  await drafts.save('alice', 'new', value, generation);
  assert.equal(await drafts.read('alice', 'new', drafts.generation()), null);
  assert.equal(storage.values.get('other-app'), 'keep');
});
test('corrupt and expired composer drafts are not restored', async () => {
  const storage = new Memory();
  const drafts = new ComposerDrafts(storage);
  await drafts.save('alice', 'new', { ...value, updatedAt: Date.now() - 8 * 86400000 }, 0);
  assert.equal(await drafts.read('alice', 'new', 0), null);
  storage.values.set([...storage.values.keys()][0], '{broken');
  assert.equal(await drafts.read('alice', 'new', 0), null);
});
