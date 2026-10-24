import type { Tokens } from './types';
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    public retryAfter?: number,
  ) {
    super(code);
  }
}
export interface TokenStorage {
  get(): Promise<string | null>;
  set(value: string): Promise<void>;
  clear(): Promise<void>;
}
// No React/Expo dependencies here: exercise refresh, retries and account races directly.
export class ApiClient {
  private access: string | null = null;
  private refresh: Promise<void> | null = null;
  private epoch = 0;
  private writes: Promise<void> = Promise.resolve();
  private storeWrite(action: () => Promise<void>) {
    const operation = this.writes.catch(() => {}).then(action);
    this.writes = operation;
    return operation;
  }
  private sessionOperations: Promise<unknown> = Promise.resolve();
  private sessionOperation<T>(run: () => Promise<T>) {
    const operation = this.sessionOperations.catch(() => {}).then(run);
    this.sessionOperations = operation;
    return operation;
  }
  private controllers = new Set<AbortController>();
  onInvalidSession: () => void = () => {};
  constructor(
    private origin: string,
    private storage: TokenStorage,
    private send: typeof fetch = (...args) => globalThis.fetch(...args),
    private options: { browserSession?: boolean } = {},
  ) {}
  async adopt(tokens: Tokens) {
    const epoch = this.epoch;
    await this.storeWrite(async () => {
      if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
      await this.storage.set(tokens.refreshToken);
    });
    if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
    this.access = tokens.accessToken;
  }
  async clear() {
    this.epoch++;
    this.access = null;
    this.refresh = null;
    for (const controller of this.controllers) controller.abort();
    this.controllers.clear();
    await this.storeWrite(() => this.storage.clear());
  }
  async restore() {
    if (this.options.browserSession) return;
    if (await this.storage.get()) await this.renew();
  }
  signIn(email: string, password: string) {
    return this.sessionOperation(async () => {
      await this.clear();
      if (this.options.browserSession) {
        await this.request(
          '/accounts/web/login',
          { method: 'POST', body: JSON.stringify({ email: email.trim(), password }) },
          false,
        );
        await this.storeWrite(() => this.storage.set('cookie-session'));
      } else {
        const tokens = await this.request<Tokens>(
          '/accounts/login',
          { method: 'POST', body: JSON.stringify({ email: email.trim(), password }) },
          false,
        );
        await this.adopt(tokens);
      }
    });
  }
  signOut() {
    const clearing = this.clear();
    return this.sessionOperation(async () => {
      try {
        await clearing;
      } finally {
        if (this.options.browserSession)
          await this.request('/accounts/web/logout', { method: 'POST' }, false);
      }
    });
  }
  private async renew() {
    if (this.refresh) return this.refresh;
    const epoch = this.epoch;
    const operation = (async () => {
      const token = await this.storage.get();
      if (!token) throw new ApiError(401, 'sign_in_required');
      try {
        const tokens = await this.request<Tokens>(
          '/accounts/refresh',
          { method: 'POST', body: JSON.stringify({ refreshToken: token }) },
          false,
        );
        if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
        await this.adopt(tokens);
      } catch (error) {
        if (epoch === this.epoch && error instanceof ApiError && error.status === 401) {
          await this.clear();
          this.onInvalidSession();
        }
        throw error;
      }
    })();
    this.refresh = operation;
    try {
      await operation;
    } finally {
      if (this.refresh === operation) this.refresh = null;
    }
  }
  async request<T>(
    path: string,
    init: RequestInit = {},
    authenticated = true,
    binary = false,
  ): Promise<T> {
    if (!path.startsWith('/') || path.startsWith('//') || path.includes('..'))
      throw new Error('Invalid API path');
    const epoch = this.epoch;
    if (authenticated && this.options.browserSession && !(await this.storage.get()))
      throw new ApiError(401, 'sign_in_required');
    if (authenticated && !this.options.browserSession && !this.access) await this.renew();
    const originalToken = this.access;
    const execute = async () => {
      if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
      const controller = new AbortController();
      this.controllers.add(controller);
      const abort = () => controller.abort();
      init.signal?.addEventListener('abort', abort, { once: true });
      if (init.signal?.aborted) controller.abort();
      const timeout = setTimeout(abort, binary ? 60000 : 25000);
      try {
        const headers = new Headers(init.headers);
        if (init.body) headers.set('Content-Type', 'application/json');
        if (authenticated && !this.options.browserSession && this.access)
          headers.set('Authorization', `Bearer ${this.access}`);
        const response = await this.send(this.origin + '/api/v1' + path, {
          ...init,
          headers,
          signal: controller.signal,
          credentials: this.options.browserSession ? 'same-origin' : 'omit',
        });
        if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
        if (!response.ok) {
          const problem = (await response.json().catch(() => ({}))) as {
            title?: string;
            message?: string;
            errors?: Record<string, string[]>;
          };
          const detail = problem.errors
            ? Object.values(problem.errors).flat().join(' ')
            : undefined;
          throw new ApiError(
            response.status,
            detail || problem.title || problem.message || 'request_failed',
            Number(response.headers.get('Retry-After')) || undefined,
          );
        }
        if (binary && Number(response.headers.get('Content-Length') || 0) > 32 * 1024 * 1024) {
          controller.abort();
          throw new ApiError(413, 'This document is larger than the 32 MB mobile limit.');
        }
        const value =
          response.status === 204 || response.headers.get('content-length') === '0'
            ? undefined
            : binary
              ? await response.arrayBuffer()
              : await response.json();
        if (epoch !== this.epoch) throw new ApiError(401, 'session_changed');
        return value as T;
      } finally {
        clearTimeout(timeout);
        init.signal?.removeEventListener('abort', abort);
        this.controllers.delete(controller);
      }
    };
    try {
      return await execute();
    } catch (error) {
      if (
        authenticated &&
        !this.options.browserSession &&
        error instanceof ApiError &&
        error.status === 401 &&
        epoch === this.epoch
      ) {
        if (this.access === originalToken) await this.renew();
        return execute(); // Preserve method, body and Idempotency-Key, and retry only once.
      }
      if (
        authenticated &&
        this.options.browserSession &&
        error instanceof ApiError &&
        error.status === 401 &&
        epoch === this.epoch
      ) {
        await this.clear();
        this.onInvalidSession();
      }
      throw error;
    }
  }
}
export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code === 'secure_storage_unavailable')
      return 'Your device could not save or clear its encrypted session. Check device storage and restart the app before signing in again.';
    if (error.status === 401)
      return 'Please sign in again. Check that your email is confirmed and your password is correct.';
    if (error.status === 404) return 'This resource is no longer available to your account.';
    if (error.status === 429)
      return error.code === 'study_limit'
        ? 'You’ve reached the study limit. Give your current sessions a moment, then try again.'
        : 'You’re doing that a little quickly. Wait a moment and try again.';
    if (error.code === 'conversation_busy')
      return 'A reply is still in progress. Refresh the messages to wait for it or stop it.';
    if (error.code === 'reminder_in_quiet_hours')
      return 'Choose a reminder time outside your quiet hours.';
    if (error.code === 'invalid_timezone')
      return 'Choose a valid timezone, such as Africa/Lagos, or use your phone timezone.';
    if (error.code === 'invalid_reminder')
      return 'Check the reminder time, timezone and quiet hours, then try again.';
    if (error.code === 'revision_limit')
      return 'You have 500 saved concepts. Reuse or edit an existing concept.';
    if (error.code === 'submit_before_completion')
      return 'Save an attempt before marking this task complete.';
    if (error.code === 'conversation_turn_limit')
      return 'This conversation has reached 40 messages. Start a new conversation for the next topic.';
    if (error.code === 'conversation_limit')
      return 'You have 100 saved conversations. Delete an old conversation before starting another.';
    if (error.code === 'feedback_scope_too_large')
      return 'This task and attempt are too long for one feedback reply. Try a shorter attempt or compare with the worked solution.';
    if (error.code === 'question_too_long') return 'Try a shorter question focused on one idea.';
    if (error.code === 'material_processing')
      return 'This document is still being prepared. Please try again later.';
    if (error.code === 'scope_too_large')
      return 'This chapter is too large for one session. Choose a single document.';
    if (error.status >= 500)
      return 'The service is unavailable right now. Your saved work is safe. Try again shortly.';
    return error.code.replaceAll('_', ' ');
  }
  return 'Couldn’t connect. Check your internet connection and that the backend is running, then retry.';
}
