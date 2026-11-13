import type { PushState } from './controller';
const state: PushState = {
  available: false,
  enabled: false,
  busy: false,
  pendingRevoke: false,
  error: '',
  reason:
    'Phone notifications use the mobile development build. Your Activity inbox is available here.',
  upvotes: false,
};
export const push = {
  snapshot: () => state,
  subscribe: (listener: (state: PushState) => void) => {
    listener(state);
    return () => {};
  },
  watch: (_owner: string | null, _open: (id: string) => Promise<void>) => () => {},
  sync: async (_ask = false, _upvotes?: boolean) => {},
  disable: async () => {},
  detach: async () => {},
};
