import test from 'node:test';
import assert from 'node:assert/strict';
import { PushController, type Installation, type PushPorts } from '../src/push/controller';
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
async function until(check: () => boolean) {
  for (let i = 0; i < 100 && !check(); i++) await new Promise((done) => setImmediate(done));
  assert.ok(check());
}
function setup() {
  let stored: Installation | null = null;
  const registered: Installation[] = [];
  const revoked: Installation[] = [];
  let permissionCalls = 0;
  const ports: PushPorts = {
    read: async () => stored,
    write: async (value) => {
      stored = { ...value };
    },
    identity: async () => ({
      installationId: 'd871a33f-d3d4-456d-8509-76dbb631a762',
      installationSecret: 'a'.repeat(64),
    }),
    capability: () => ({ available: true, projectId: 'project', reason: '' }),
    server: async () => ({ available: true, projectId: 'project' }),
    permission: async () => {
      permissionCalls++;
      return true;
    },
    token: async () => 'private-token',
    initialize: async () => {},
    register: async (value) => {
      registered.push({ ...value });
    },
    revoke: async (value) => {
      revoked.push({ ...value });
    },
    error: (error) => (error instanceof Error ? error.message : 'Unavailable'),
  };
  return {
    ports,
    registered,
    revoked,
    stored: () => stored,
    permissions: () => permissionCalls,
    controller: new PushController(ports),
  };
}
test('push permission is requested only after consent; unavailable setup keeps Activity independent', async () => {
  const f = setup();
  f.controller.setOwner('A');
  await f.controller.sync();
  assert.equal(f.permissions(), 0);
  assert.equal(f.registered.length, 0);
  f.ports.server = async () => ({ available: false });
  await f.controller.sync(true);
  assert.equal(f.permissions(), 0);
  assert.equal(f.registered.length, 0);
  assert.equal(f.controller.snapshot().enabled, false);
});
test('logout fences a late permission decision and a late token without registering the old account', async () => {
  const permission = deferred<boolean>();
  const f = setup();
  let asked = false;
  f.ports.permission = () => {
    asked = true;
    return permission.promise;
  };
  f.controller.setOwner('A');
  const old = f.controller.sync(true);
  await until(() => asked);
  await f.controller.detach();
  permission.resolve(true);
  await old;
  assert.equal(f.registered.length, 0);
  const token = deferred<string>();
  let tokenRequested = false;
  f.ports.permission = async () => true;
  f.ports.token = () => {
    tokenRequested = true;
    return token.promise;
  };
  f.controller.setOwner('A');
  const next = f.controller.sync(true);
  await until(() => tokenRequested);
  await f.controller.detach();
  token.resolve('old-token');
  await next;
  assert.equal(f.registered.length, 0);
  assert.equal(f.stored()?.consented, false);
});
test('persisted unlink revision supersedes an in-flight registration and late success cannot enable another account', async () => {
  const f = setup();
  const response = deferred<void>();
  let calls = 0;
  f.ports.register = async (value) => {
    f.registered.push({ ...value });
    if (++calls === 1) await response.promise;
  };
  f.controller.setOwner('A');
  const old = f.controller.sync(true);
  await until(() => f.registered.length === 1);
  await f.controller.detach();
  assert.ok(f.revoked[0].revision > f.registered[0].revision);
  f.controller.setOwner('B');
  await f.controller.sync();
  assert.equal(f.registered.length, 1);
  await f.controller.sync(true);
  assert.equal(f.registered[1].ownerId, 'B');
  assert.ok(f.registered[1].revision > f.revoked[0].revision);
  response.resolve();
  await old;
  assert.equal(f.controller.snapshot().enabled, true);
  assert.equal(f.stored()?.ownerId, 'B');
});
test('offline unlink survives restart and is flushed before a different account can register', async () => {
  const f = setup();
  f.controller.setOwner('A');
  await f.controller.sync(true);
  f.ports.revoke = async () => {
    throw new Error('offline');
  };
  await f.controller.detach();
  assert.equal(f.stored()?.pendingRevoke, true);
  assert.equal(f.stored()?.ownerId, null);
  const restarted = new PushController(f.ports);
  restarted.setOwner('B');
  await restarted.sync(true);
  assert.equal(f.registered.length, 1);
  assert.equal(restarted.snapshot().pendingRevoke, true);
  f.ports.revoke = async (value) => {
    f.revoked.push({ ...value });
  };
  await restarted.sync(true);
  assert.equal(f.registered.length, 2);
  assert.equal(f.registered[1].ownerId, 'B');
  assert.equal(f.stored()?.pendingRevoke, false);
});
test('secure installation persistence failure prevents token registration without a fallback', async () => {
  const f = setup();
  f.ports.write = async () => {
    throw new Error('secure storage unavailable');
  };
  f.controller.setOwner('A');
  await assert.rejects(f.controller.sync(true), /secure storage/);
  assert.equal(f.registered.length, 0);
  assert.equal(f.controller.snapshot().enabled, false);
});
test('overlapping foreground refreshes share one permission/token registration', async () => {
  const f = setup();
  f.controller.setOwner('A');
  await Promise.all([f.controller.sync(true), f.controller.sync(true), f.controller.sync(true)]);
  assert.equal(f.registered.length, 1);
  assert.equal(f.permissions(), 1);
});
