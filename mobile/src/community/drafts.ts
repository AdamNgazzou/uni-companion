import type { DraftStorage } from '../study/drafts';
export type ComposerDraft = {
  title: string;
  body: string;
  kind: 'Question' | 'Study tip' | 'Discussion';
  courseId: string;
  anonymous: boolean;
  requestKey: string;
  lastPayload?: string;
  updatedAt: number;
};
const prefix = 'uni.composer-draft.v1:';
function parse(raw: string | null): ComposerDraft | null {
  if (!raw || raw.length > 200000) return null;
  try {
    const value = JSON.parse(raw) as ComposerDraft;
    if (
      typeof value.title !== 'string' ||
      value.title.length > 180 ||
      typeof value.body !== 'string' ||
      value.body.length > 8000 ||
      !['Question', 'Study tip', 'Discussion'].includes(value.kind) ||
      typeof value.courseId !== 'string' ||
      value.courseId.length > 100 ||
      typeof value.anonymous !== 'boolean' ||
      typeof value.requestKey !== 'string' ||
      !/^[\w:-]{1,100}$/.test(value.requestKey) ||
      (value.lastPayload !== undefined &&
        (typeof value.lastPayload !== 'string' || value.lastPayload.length > 60000)) ||
      !Number.isFinite(value.updatedAt) ||
      value.updatedAt > Date.now() + 60000 ||
      Date.now() - value.updatedAt > 7 * 86400000
    )
      return null;
    return value;
  } catch {
    return null;
  }
}
export class ComposerDrafts {
  private queue: Promise<unknown> = Promise.resolve();
  private epoch = 0;
  constructor(private storage: DraftStorage) {}
  generation() {
    return this.epoch;
  }
  private key(owner: string, scope: string) {
    return prefix + encodeURIComponent(owner) + ':' + encodeURIComponent(scope);
  }
  private ordered<T>(work: () => Promise<T>) {
    const next = this.queue.catch(() => {}).then(work);
    this.queue = next;
    return next;
  }
  async read(owner: string, scope: string, generation: number) {
    await this.queue.catch(() => {});
    const value = parse(await this.storage.getItem(this.key(owner, scope)));
    return generation === this.epoch ? value : null;
  }
  save(owner: string, scope: string, value: ComposerDraft, generation: number) {
    return this.ordered(async () => {
      if (generation !== this.epoch) return;
      await this.storage.setItem(this.key(owner, scope), JSON.stringify(value));
      const keys = (await this.storage.getAllKeys()).filter((key) => key.startsWith(prefix));
      if (keys.length > 30) {
        const rows = await Promise.all(
          keys.map(async (key) => ({
            key,
            time: parse(await this.storage.getItem(key))?.updatedAt || 0,
          })),
        );
        for (const row of rows.sort((a, b) => a.time - b.time).slice(0, keys.length - 30))
          await this.storage.removeItem(row.key);
      }
    });
  }
  remove(owner: string, scope: string) {
    return this.ordered(() => this.storage.removeItem(this.key(owner, scope)));
  }
  clear() {
    this.epoch++;
    return this.ordered(async () => {
      for (const key of await this.storage.getAllKeys())
        if (key.startsWith(prefix)) await this.storage.removeItem(key);
    });
  }
}
