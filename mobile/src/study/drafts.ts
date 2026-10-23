import type { Question } from '../api/types';
export type QuizDraft = {
  version: 1;
  answers: Record<string, string>;
  questionId?: string;
  attemptKey: string;
  signature?: string;
  updatedAt: number;
};
export interface DraftStorage {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  removeItem(key: string): Promise<void>;
  getAllKeys(): Promise<readonly string[]>;
}
const prefix = 'uni.quiz-draft.v1:';
export function validDraft(raw: string | null, questions: Question[]): QuizDraft | null {
  if (!raw || raw.length > 50000) return null;
  try {
    const value = JSON.parse(raw) as QuizDraft;
    if (
      value.version !== 1 ||
      typeof value.updatedAt !== 'number' ||
      Date.now() - value.updatedAt > 7 * 86400000 ||
      typeof value.attemptKey !== 'string' ||
      !/^[\w:-]{1,100}$/.test(value.attemptKey) ||
      !value.answers ||
      typeof value.answers !== 'object'
    )
      return null;
    const answers = Object.fromEntries(
      questions.flatMap((q) =>
        q.options.some((o) => o.id === value.answers[q.id]) ? [[q.id, value.answers[q.id]]] : [],
      ),
    );
    return {
      version: 1,
      answers,
      questionId: questions.some((q) => q.id === value.questionId) ? value.questionId : undefined,
      attemptKey: value.attemptKey,
      signature:
        typeof value.signature === 'string' && value.signature.length < 10000
          ? value.signature
          : undefined,
      updatedAt: value.updatedAt,
    };
  } catch {
    return null;
  }
}
export class QuizDrafts {
  private writes: Promise<unknown> = Promise.resolve();
  private epoch = 0;
  constructor(private storage: DraftStorage) {}
  private key(owner: string, job: string, retry?: string) {
    return prefix + [owner, job, retry || 'full'].map(encodeURIComponent).join(':');
  }
  private write<T>(run: () => Promise<T>) {
    const next = this.writes.catch(() => {}).then(run);
    this.writes = next;
    return next;
  }
  async read(owner: string, job: string, questions: Question[], retry?: string) {
    await this.writes.catch(() => {});
    return validDraft(await this.storage.getItem(this.key(owner, job, retry)), questions);
  }
  save(owner: string, job: string, value: QuizDraft, retry?: string) {
    const epoch = this.epoch;
    return this.write(async () => {
      if (epoch !== this.epoch) return;
      await this.storage.setItem(this.key(owner, job, retry), JSON.stringify(value));
      const keys = (await this.storage.getAllKeys()).filter((key) => key.startsWith(prefix));
      if (keys.length > 30) {
        const ages = await Promise.all(
          keys.map(async (key) => {
            const raw = await this.storage.getItem(key);
            let time = 0;
            try {
              const updatedAt = JSON.parse(raw || '{}')?.updatedAt;
              if (typeof updatedAt === 'number' && Number.isFinite(updatedAt)) time = updatedAt;
            } catch {
              // Discard corrupt old entries before pruning healthy drafts.
            }
            return { key, time };
          }),
        );
        for (const old of ages.sort((a, b) => a.time - b.time).slice(0, keys.length - 30))
          await this.storage.removeItem(old.key);
      }
    });
  }
  remove(owner: string, job: string, retry?: string) {
    return this.write(() => this.storage.removeItem(this.key(owner, job, retry)));
  }
  clear() {
    this.epoch++;
    return this.write(async () => {
      for (const key of await this.storage.getAllKeys())
        if (key.startsWith(prefix)) await this.storage.removeItem(key);
    });
  }
}
