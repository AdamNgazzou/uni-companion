import Constants from 'expo-constants';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import { ApiClient, ApiError, type TokenStorage } from './client';
// Browser auth lives only in an HTTP-only server cookie; this marker contains no credentials.
const signedOut = 'uni.session.signed-out';
const key = 'uni-companion.refresh.v1';
const storage: TokenStorage = {
  get: () =>
    Platform.OS === 'web'
      ? Promise.resolve(
          typeof window !== 'undefined' && window.localStorage.getItem(signedOut) === '1'
            ? null
            : 'cookie-session',
        )
      : SecureStore.getItemAsync(key),
  set: async (value) => {
    if (Platform.OS === 'web') {
      window.localStorage.removeItem(signedOut);
      return;
    }
    try {
      await SecureStore.setItemAsync(key, value, {
        keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
      });
      if ((await SecureStore.getItemAsync(key)) !== value)
        throw new Error('Secure session storage could not be verified.');
    } catch {
      throw new ApiError(0, 'secure_storage_unavailable');
    }
  },
  clear: async () => {
    if (Platform.OS === 'web' && typeof window !== 'undefined')
      window.localStorage.setItem(signedOut, '1');
    if (Platform.OS !== 'web') {
      try {
        await SecureStore.deleteItemAsync(key);
      } catch {
        throw new ApiError(0, 'secure_storage_unavailable');
      }
    }
  },
};
function origin() {
  const configured = process.env.EXPO_PUBLIC_API_URL;
  if (configured) {
    const url = new URL(configured);
    if (
      !['http:', 'https:'].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.pathname !== '/'
    )
      throw new Error('EXPO_PUBLIC_API_URL must be an HTTP(S) origin.');
    return url.origin;
  }
  if (Platform.OS === 'web')
    return typeof window === 'undefined' ? 'http://localhost:5080' : window.location.origin;
  const host = Constants.expoConfig?.hostUri;
  if (!host) throw new Error('Set EXPO_PUBLIC_API_URL to the backend origin.');
  return `${host.includes('exp.direct') ? 'https' : 'http'}://${host}`;
}
export const api = new ApiClient(origin(), storage, undefined, {
  browserSession: Platform.OS === 'web',
});
export const newKey = () =>
  `mobile-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;
