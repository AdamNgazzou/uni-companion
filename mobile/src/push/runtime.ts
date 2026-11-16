import Constants, { ExecutionEnvironment } from 'expo-constants';
import * as Device from 'expo-device';
import * as Crypto from 'expo-crypto';
import * as SecureStore from 'expo-secure-store';
import { AppState, Platform } from 'react-native';
import { api } from '../api/runtime';
import { ApiError, errorText } from '../api/client';
import { PushController, type Installation } from './controller';
const storageKey = 'uni-companion.push-installation.v1';
let notifications: typeof import('expo-notifications') | undefined;
const sdk = async () => (notifications ??= await import('expo-notifications'));
function capability() {
  const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;
  const reason =
    Platform.OS === 'web'
      ? 'Phone notifications use the mobile development build.'
      : Constants.executionEnvironment === ExecutionEnvironment.StoreClient
        ? 'Phone push needs the development build. Keep using Activity in Expo Go.'
        : !Device.isDevice
          ? 'Use a physical phone to enable notifications.'
          : typeof projectId !== 'string' || !/^[a-f0-9-]{36}$/i.test(projectId)
            ? 'Phone push setup needs this app’s Expo project identity.'
            : '';
  return {
    available: !reason,
    projectId: typeof projectId === 'string' ? projectId : undefined,
    reason,
  };
}
const controller = new PushController({
  read: async () => {
    try {
      const text = await SecureStore.getItemAsync(storageKey);
      if (!text) return null;
      const value = JSON.parse(text) as Installation;
      if (
        !/^[a-f0-9-]{36}$/i.test(value.installationId) ||
        !/^[a-f0-9]{64}$/i.test(value.installationSecret) ||
        !Number.isSafeInteger(value.revision) ||
        value.revision < 0 ||
        typeof value.consented !== 'boolean' ||
        typeof value.pendingRevoke !== 'boolean' ||
        typeof value.upvotes !== 'boolean' ||
        !(value.ownerId === null || typeof value.ownerId === 'string')
      )
        throw new Error('Invalid installation');
      return value;
    } catch {
      throw new ApiError(0, 'secure_storage_unavailable');
    }
  },
  write: async (value) => {
    try {
      const text = JSON.stringify(value);
      await SecureStore.setItemAsync(storageKey, text, {
        keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
      });
      if ((await SecureStore.getItemAsync(storageKey)) !== text)
        throw new Error('Installation storage failed');
    } catch {
      throw new ApiError(0, 'secure_storage_unavailable');
    }
  },
  identity: async () => ({
    installationId: Crypto.randomUUID(),
    installationSecret: Array.from(await Crypto.getRandomBytesAsync(32), (byte) =>
      byte.toString(16).padStart(2, '0'),
    ).join(''),
  }),
  capability,
  server: () => api.request('/devices/push/status'),
  permission: async (ask) => {
    const n = await sdk();
    if (Platform.OS === 'android')
      await n.setNotificationChannelAsync('activity', {
        name: 'Activity',
        importance: n.AndroidImportance.DEFAULT,
      });
    const current = await n.getPermissionsAsync();
    if (current.granted || current.ios?.status === n.IosAuthorizationStatus.PROVISIONAL)
      return true;
    if (!ask || !current.canAskAgain) return false;
    const result = await n.requestPermissionsAsync();
    return result.granted || result.ios?.status === n.IosAuthorizationStatus.PROVISIONAL;
  },
  token: async (projectId) => (await (await sdk()).getExpoPushTokenAsync({ projectId })).data,
  initialize: (value) =>
    api.request('/devices/push/initialize', {
      method: 'POST',
      body: JSON.stringify({
        installationId: value.installationId,
        installationSecret: value.installationSecret,
        revision: Math.max(1, value.revision),
      }),
    }),
  register: (value, token, projectId) =>
    api.request('/devices/push/register', {
      method: 'POST',
      body: JSON.stringify({
        installationId: value.installationId,
        installationSecret: value.installationSecret,
        revision: value.revision,
        token,
        projectId,
        platform: Platform.OS,
        timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
        upvotes: value.upvotes,
      }),
    }),
  revoke: async (value) => {
    const abort = new AbortController();
    const timer = setTimeout(() => abort.abort(), 2000);
    try {
      await api.request(
        '/devices/push/revoke',
        {
          method: 'POST',
          signal: abort.signal,
          body: JSON.stringify({
            installationId: value.installationId,
            installationSecret: value.installationSecret,
            revision: value.revision,
          }),
        },
        false,
      );
    } finally {
      clearTimeout(timer);
    }
  },
  error: errorText,
});
let owner: string | null = null;
let epoch = 0;
let pending: string | null = null;
let open: ((id: string) => Promise<void>) | null = null;
let lastResponse = '';
async function response(value: import('expo-notifications').NotificationResponse) {
  const activityId = value.notification.request.content.data?.activityId;
  const identifier = value.notification.request.identifier;
  if (
    typeof activityId !== 'string' ||
    !/^[a-f0-9-]{36}$/i.test(activityId) ||
    identifier === lastResponse
  )
    return;
  lastResponse = identifier;
  pending = activityId;
  (await sdk()).clearLastNotificationResponse();
  await consume();
}
async function consume() {
  if (!owner || !open || !pending) return;
  const id = pending;
  pending = null;
  try {
    await open(id);
  } catch {
    /* A revoked/foreign destination cannot be opened. Activity remains available. */
  }
}
export const push = {
  snapshot: () => controller.snapshot(),
  subscribe: controller.subscribe.bind(controller),
  sync: controller.sync.bind(controller),
  disable: controller.disable.bind(controller),
  detach: controller.detach.bind(controller),
  watch: (account: string | null, onOpen: (id: string) => Promise<void>) => {
    const generation = ++epoch;
    owner = account;
    controller.setOwner(account);
    open = async (id) => {
      if (epoch === generation && owner === account) await onOpen(id);
    };
    let cleanup = () => {};
    const refresh = () => {
      void controller.sync().catch(() => {});
    };
    refresh();
    if (capability().available)
      void sdk()
        .then((n) => {
          if (epoch !== generation) return;
          // The in-app bell handles foreground activity. OS previews remain generic.
          n.setNotificationHandler({
            handleNotification: async () => ({
              shouldShowBanner: false,
              shouldShowList: false,
              shouldPlaySound: false,
              shouldSetBadge: false,
            }),
          });
          const token = n.addPushTokenListener(refresh);
          const tap = n.addNotificationResponseReceivedListener((value) => {
            void response(value).catch(() => {});
          });
          cleanup = () => {
            token.remove();
            tap.remove();
          };
          const initial = n.getLastNotificationResponse();
          if (initial) void response(initial).catch(() => {});
          void consume();
        })
        .catch(() => {});
    const foreground = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        refresh();
        void consume();
      }
    });
    return () => {
      if (epoch === generation) {
        epoch++;
        owner = null;
        open = null;
        controller.setOwner(null);
      }
      cleanup();
      foreground.remove();
    };
  },
};
