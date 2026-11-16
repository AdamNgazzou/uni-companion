import type { DraftStorage } from '../study/drafts';
export type PracticeDraft = {
  answer: string;
  requestKey: string;
  lastAnswer?: string;
  updatedAt: number;
};
const prefix = 'uni.practice-draft.v1:';
function parse(raw: string | null): PracticeDraft | null {
  if (!raw || raw.length > 100000) return null;
  try {
    const value = JSON.parse(raw) as PracticeDraft;
    if (
      typeof value.answer !== 'string' ||
      value.answer.length > 12000 ||
      typeof value.requestKey !== 'string' ||
      !/^[\w:-]{1,100}$/.test(value.requestKey) ||
      (value.lastAnswer !== undefined &&
        (typeof value.lastAnswer !== 'string' || value.lastAnswer.length > 12000)) ||
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
export class PracticeDrafts {
  private queue: Promise<unknown> = Promise.resolve();
  private epoch = 0;
  constructor(private storage: DraftStorage) {}
  generation() {
    return this.epoch;
  }
  private key(owner: string, job: string, task: string) {
    return prefix + [owner, job, task].map(encodeURIComponent).join(':');
  }
  private ordered<T>(work: () => Promise<T>) {
    const next = this.queue.catch(() => {}).then(work);
    this.queue = next;
    return next;
  }
  async read(owner: string, job: string, task: string, generation: number) {
    await this.queue.catch(() => {});
    const value = parse(await this.storage.getItem(this.key(owner, job, task)));
    return generation === this.epoch ? value : null;
  }
  save(owner: string, job: string, task: string, draft: PracticeDraft, generation: number) {
    return this.ordered(async () => {
      if (generation !== this.epoch) return;
      await this.storage.setItem(this.key(owner, job, task), JSON.stringify(draft));
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
  remove(owner: string, job: string, task: string) {
    return this.ordered(() => this.storage.removeItem(this.key(owner, job, task)));
  }
  clear() {
    this.epoch++;
    return this.ordered(async () => {
      for (const key of await this.storage.getAllKeys())
        if (key.startsWith(prefix)) await this.storage.removeItem(key);
    });
  }
}
