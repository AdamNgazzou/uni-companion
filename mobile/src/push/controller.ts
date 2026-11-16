export type Installation = {
  installationId: string;
  installationSecret: string;
  revision: number;
  ownerId: string | null;
  consented: boolean;
  upvotes: boolean;
  pendingRevoke: boolean;
};
export type PushState = {
  available: boolean;
  enabled: boolean;
  busy: boolean;
  pendingRevoke: boolean;
  error: string;
  reason: string;
  upvotes: boolean;
};
export type PushPorts = {
  read: () => Promise<Installation | null>;
  write: (value: Installation) => Promise<void>;
  identity: () => Promise<Pick<Installation, 'installationId' | 'installationSecret'>>;
  capability: () => { available: boolean; projectId?: string; reason: string };
  server: () => Promise<{ available: boolean; projectId?: string | null }>;
  permission: (ask: boolean) => Promise<boolean>;
  token: (projectId: string) => Promise<string>;
  initialize: (value: Installation) => Promise<void>;
  register: (value: Installation, token: string, projectId: string) => Promise<void>;
  revoke: (value: Installation) => Promise<void>;
  error: (error: unknown) => string;
};
// OS/network requests never hold the storage queue. Logout can persist a newer
// revocation while an earlier permission/token/registration request is pending.
export class PushController {
  private owner: string | null = null;
  private epoch = 0;
  private value: Installation | null = null;
  private loading?: Promise<void>;
  private creating?: Promise<void>;
  private writes: Promise<void> = Promise.resolve();
  private listeners = new Set<(state: PushState) => void>();
  private state: PushState = {
    available: false,
    enabled: false,
    busy: false,
    pendingRevoke: false,
    error: '',
    reason: '',
    upvotes: false,
  };
  private running?: Promise<void>;
  constructor(private ports: PushPorts) {}
  snapshot() {
    return this.state;
  }
  subscribe(listener: (state: PushState) => void) {
    this.listeners.add(listener);
    listener(this.state);
    return () => {
      this.listeners.delete(listener);
    };
  }
  private publish(patch: Partial<PushState>) {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) listener(this.state);
  }
  setOwner(owner: string | null) {
    if (owner === this.owner) return;
    this.owner = owner;
    this.epoch++;
    this.running = undefined;
    this.publish({ enabled: false, busy: false, error: '', reason: '', upvotes: false });
  }
  private async load(create = false) {
    this.loading ??= this.ports.read().then((value) => {
      this.value = value;
    });
    await this.loading;
    if (!this.value && create) {
      this.creating ??= this.ports.identity().then((identity) =>
        this.save({
          ...identity,
          revision: 0,
          ownerId: null,
          consented: false,
          upvotes: false,
          pendingRevoke: false,
        }),
      );
      await this.creating;
    }
    return this.value;
  }
  private save(value: Installation) {
    this.value = value;
    const task = this.writes.then(() => this.ports.write(value));
    this.writes = task.catch(() => {});
    return task;
  }
  private async flush() {
    const value = await this.load();
    if (!value?.pendingRevoke) return true;
    try {
      await this.ports.revoke(value);
      if (this.value?.revision === value.revision)
        await this.save({ ...this.value, pendingRevoke: false });
      this.publish({ pendingRevoke: this.value?.pendingRevoke || false });
      return true;
    } catch {
      this.publish({ enabled: false, pendingRevoke: true });
      return false;
    }
  }
  private async unlink() {
    const value = await this.load();
    if (!value) return;
    const next = {
      ...value,
      revision: value.revision + 1,
      ownerId: null,
      consented: false,
      pendingRevoke: true,
    };
    await this.save(next);
    this.publish({ enabled: false, pendingRevoke: true, upvotes: false });
    const unlinked = await this.flush();
    this.publish({
      reason: unlinked
        ? 'Phone notifications are off on this device.'
        : 'Device unlink is pending. It will retry when you reconnect.',
    });
  }
  async detach() {
    this.setOwner(null);
    try {
      await this.unlink();
    } catch (error) {
      this.publish({ enabled: false, error: this.ports.error(error) });
      throw error;
    }
  }
  async disable() {
    this.epoch++;
    try {
      await this.unlink();
      this.publish({ busy: false });
    } catch (error) {
      this.publish({ enabled: false, busy: false, error: this.ports.error(error) });
      throw error;
    }
  }
  sync(ask = false, upvotes?: boolean): Promise<void> {
    if (this.running) return this.running;
    const task = this.synchronize(ask, upvotes);
    this.running = task;
    void task
      .finally(() => {
        if (this.running === task) this.running = undefined;
      })
      .catch(() => {});
    return task;
  }
  private async synchronize(ask: boolean, upvotes?: boolean) {
    const owner = this.owner;
    const epoch = this.epoch;
    const current = () => this.epoch === epoch && this.owner === owner;
    this.publish({ busy: true, error: '' });
    try {
      let value = await this.load();
      if (!current()) return;
      if (value?.ownerId && value.ownerId !== owner) {
        await this.unlink();
        value = this.value;
      }
      if (!current()) return;
      if (!(await this.flush())) {
        this.publish({ reason: 'Device unlink is pending. It will retry when you reconnect.' });
        return;
      }
      if (!current() || !owner) return;
      const capability = this.ports.capability();
      this.publish({ available: capability.available, reason: capability.reason });
      if (!capability.available || !capability.projectId) return;
      value = this.value;
      if (!ask && (!value?.consented || value.ownerId !== owner)) return;
      const server = await this.ports.server();
      if (!current()) return;
      if (!server.available || server.projectId !== capability.projectId) {
        this.publish({
          available: false,
          enabled: false,
          reason: 'Phone push setup is not complete for this app yet.',
        });
        return;
      }
      const granted = await this.ports.permission(ask);
      if (!current()) return;
      if (!granted) {
        await this.unlink();
        if (current())
          this.publish({
            reason:
              'Notification permission is off. Activity still works; you can change permission in phone settings.',
          });
        return;
      }
      value = await this.load(true);
      if (!current() || !value) return;
      await this.save({
        ...value,
        consented: true,
        ownerId: owner,
        upvotes: upvotes ?? value.upvotes,
      });
      await this.ports.initialize(this.value!);
      if (!current()) return;
      const token = await this.ports.token(capability.projectId);
      if (!current() || !this.value) return;
      // Persist a command revision before sending. A later unlink tombstone rejects
      // this register even if its network request reaches the server after logout.
      const registration = { ...this.value, revision: this.value.revision + 1 };
      await this.save(registration);
      if (!current()) return;
      await this.ports.register(registration, token, capability.projectId);
      if (!current()) return;
      this.publish({
        enabled: true,
        pendingRevoke: false,
        reason: 'Phone notifications are enabled for this account on this device.',
        upvotes: registration.upvotes,
      });
    } catch (error) {
      if (current()) this.publish({ enabled: false, error: this.ports.error(error) });
      throw error;
    } finally {
      if (current()) this.publish({ busy: false });
    }
  }
}
