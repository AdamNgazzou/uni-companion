import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ApiClient, ApiError, type TokenStorage } from '../src/api/client';
import { base64, physicalPage } from '../src/pdf/bytes';
function storage(initial: string | null = null) {
  let value = initial;
  return {
    get: async () => value,
    set: async (token: string) => {
      value = token;
    },
    clear: async () => {
      value = null;
    },
  } satisfies TokenStorage;
}
const response = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status });
test('concurrent 401s share one refresh and preserve idempotent POSTs', async () => {
  let refreshes = 0;
  const keys: string[] = [];
  const client = new ApiClient('http://example.test', storage(), async (url, init) => {
    if (String(url).endsWith('/refresh')) {
      refreshes++;
      await new Promise((resolve) => setTimeout(resolve, 10));
      return response({ accessToken: 'fresh', refreshToken: 'refresh', expiresIn: 300 });
    }
    keys.push(new Headers(init?.headers).get('Idempotency-Key')!);
    return new Headers(init?.headers).get('Authorization') === 'Bearer fresh'
      ? response({ id: 'one' })
      : response({}, 401);
  });
  await client.adopt({ accessToken: 'expired', refreshToken: 'refresh', expiresIn: 300 });
  const values = await Promise.all(
    [1, 2, 3].map(() =>
      client.request('/jobs', {
        method: 'POST',
        body: '{}',
        headers: { 'Idempotency-Key': 'stable' },
      }),
    ),
  );
  assert.equal(refreshes, 1);
  assert.equal(values.length, 3);
  assert.ok(keys.every((key) => key === 'stable'));
});
test('transport failures preserve a saved refresh token; invalid refresh removes it', async () => {
  const store = storage('saved');
  const client = new ApiClient('http://example.test', store, async () => {
    throw new TypeError('offline');
  });
  await assert.rejects(client.restore());
  assert.equal(await store.get(), 'saved');
  const invalid = new ApiClient('http://example.test', store, async () => response({}, 401));
  await assert.rejects(invalid.restore(), ApiError);
  assert.equal(await store.get(), null);
});
test('logout fences late account responses and clears secure writes in order', async () => {
  const store = storage();
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  const client = new ApiClient('http://example.test', store, async () => {
    await waiting;
    return response({ private: true });
  });
  await client.adopt({ accessToken: 'old', refreshToken: 'old-refresh', expiresIn: 300 });
  const pending = client.request('/private');
  await client.clear();
  release();
  await assert.rejects(pending, ApiError);
  assert.equal(await store.get(), null);
  let finish!: () => void;
  const blocked = new Promise<void>((resolve) => {
    finish = resolve;
  });
  const slowStore = {
    ...store,
    set: async (value: string) => {
      await blocked;
      await store.set(value);
    },
  };
  const other = new ApiClient('http://example.test', slowStore);
  const adopting = other.adopt({
    accessToken: 'old',
    refreshToken: 'late-refresh',
    expiresIn: 300,
  });
  await Promise.resolve();
  const clearing = other.clear();
  finish();
  await assert.rejects(adopting, ApiError);
  await clearing;
  assert.equal(await store.get(), null);
});
test('private PDF bytes and physical page parameters are lossless and bounded', () => {
  for (const count of [0, 1, 2, 3, 8197]) {
    const bytes = Uint8Array.from({ length: count }, (_, i) => i % 256);
    assert.equal(base64(bytes), Buffer.from(bytes).toString('base64'));
  }
  assert.equal(physicalPage('-10'), 1);
  assert.equal(physicalPage('1.5'), 1);
  assert.equal(physicalPage('14'), 14);
});

test('job polling stops at terminal states and ignores responses after background/logout', async () => {
  const { jobPoller } = await import('../src/api/polling');
  let run: (() => void) | undefined;
  let calls = 0;
  const seen: string[] = [];
  const poller = jobPoller(
    async () => ({ state: ++calls === 1 ? 'running' : 'succeeded' }),
    (value) => seen.push(value.state),
    () => assert.fail(),
    (callback) => {
      run = callback;
      return () => {
        run = undefined;
      };
    },
  );
  poller.start();
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(seen, ['running']);
  const next = run!;
  run = undefined;
  next();
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(seen, ['running', 'succeeded']);
  assert.equal(run, undefined);
  let release!: (job: { state: string }) => void;
  const late = jobPoller(
    () =>
      new Promise<{ state: string }>((resolve) => {
        release = resolve;
      }),
    () => assert.fail('stale result leaked'),
    () => assert.fail(),
  );
  late.start();
  late.stop();
  release({ state: 'succeeded' });
  await new Promise((resolve) => setImmediate(resolve));
});

test('the default browser fetch keeps its global receiver', async () => {
  const original = globalThis.fetch;
  globalThis.fetch = async function () {
    assert.equal(this, globalThis);
    return response({ ok: true });
  };
  try {
    const client = new ApiClient('http://example.test', storage());
    assert.deepEqual(await client.request('/public', {}, false), { ok: true });
  } finally {
    globalThis.fetch = original;
  }
});

test('browser sessions use HTTP-only cookie endpoints without bearer tokens or browser token persistence', async () => {
  const store = storage('cookie-session');
  const calls: { path: string; credentials?: string; authorization: string | null }[] = [];
  const client = new ApiClient(
    'http://example.test',
    store,
    async (url, init) => {
      calls.push({
        path: String(url),
        credentials: init?.credentials,
        authorization: new Headers(init?.headers).get('Authorization'),
      });
      return String(url).endsWith('/me')
        ? response({ id: 'account' })
        : new Response(null, { status: 204 });
    },
    { browserSession: true },
  );
  await client.signIn('student@example.test', 'password');
  await client.request('/accounts/me');
  await client.signOut();
  assert.ok(calls[0].path.endsWith('/accounts/web/login'));
  assert.ok(calls.at(-1)!.path.endsWith('/accounts/web/logout'));
  assert.ok(
    calls.every((call) => call.credentials === 'same-origin' && call.authorization === null),
  );
  assert.equal(await store.get(), null);
  assert.equal(
    calls.some((call) => call.path.endsWith('/refresh')),
    false,
  );
});
test('browser logout completes before a following login can replace the cookie', async () => {
  const order: string[] = [];
  let finish!: () => void;
  const waiting = new Promise<void>((resolve) => {
    finish = resolve;
  });
  const client = new ApiClient(
    'http://example.test',
    storage('cookie-session'),
    async (url) => {
      if (String(url).endsWith('/logout')) {
        order.push('logout-start');
        await waiting;
        order.push('logout-end');
      } else order.push('login');
      return new Response(null, { status: 204 });
    },
    { browserSession: true },
  );
  const logout = client.signOut();
  const login = client.signIn('next@example.test', 'password');
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(order, ['logout-start']);
  finish();
  await Promise.all([logout, login]);
  assert.deepEqual(order, ['logout-start', 'logout-end', 'login']);
});

test('browser logout still clears the server cookie when local storage fails', async () => {
  const store = storage('cookie-session');
  store.clear = async () => {
    throw new Error('Storage unavailable');
  };
  const paths: string[] = [];
  const client = new ApiClient(
    'http://example.test',
    store,
    async (url) => {
      paths.push(String(url));
      return new Response(null, { status: 204 });
    },
    { browserSession: true },
  );
  await assert.rejects(client.signOut(), /Storage unavailable/);
  assert.ok(paths[0].endsWith('/accounts/web/logout'));
});
