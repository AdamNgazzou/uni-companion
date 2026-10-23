import React from 'react';
import { ActivityIndicator, Platform, View } from 'react-native';
import { Stack } from 'expo-router';
import { useFonts } from 'expo-font';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { SessionProvider, useSession } from '../auth/session';
import { colors as c } from '../theme';
import { T } from '../ui';
function Routes() {
  const { profile, ready } = useSession();
  if (!ready)
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
        <ActivityIndicator size="large" color={c.blue} />
      </View>
    );
  return (
    <Stack
      key={profile?.id || 'guest'}
      screenOptions={{ headerShown: false, contentStyle: { backgroundColor: c.white } }}
    >
      <Stack.Screen name="index" />
      <Stack.Protected guard={!profile}>
        <Stack.Screen name="auth" />
      </Stack.Protected>
      <Stack.Protected guard={!!profile}>
        <Stack.Screen name="(tabs)" />
        <Stack.Screen name="course" />
        <Stack.Screen name="material" />
        <Stack.Screen name="study" />
        <Stack.Screen name="job" />
        <Stack.Screen name="reader" />
        <Stack.Screen name="history" />
        <Stack.Screen name="attempt" />
        <Stack.Screen name="settings" />
        <Stack.Screen name="thread" />
        <Stack.Screen name="compose" />
        <Stack.Screen name="my-discussions" />
        <Stack.Screen name="saved-items" />
        <Stack.Screen name="blocked" />
        <Stack.Screen name="reports" />
        <Stack.Screen name="activity" />
        <Stack.Screen name="activity-settings" />
      </Stack.Protected>
    </Stack>
  );
}
export default function RootLayout() {
  const [loaded, error] = useFonts({
    Nunito_400Regular: require('@expo-google-fonts/nunito/400Regular/Nunito_400Regular.ttf'),
    Nunito_600SemiBold: require('@expo-google-fonts/nunito/600SemiBold/Nunito_600SemiBold.ttf'),
    Nunito_800ExtraBold: require('@expo-google-fonts/nunito/800ExtraBold/Nunito_800ExtraBold.ttf'),
    Nunito_900Black: require('@expo-google-fonts/nunito/900Black/Nunito_900Black.ttf'),
  });
  return (
    <SafeAreaProvider>
      <StatusBar style="dark" />
      <View style={{ flex: 1, backgroundColor: '#EDF2FC' }}>
        <View
          style={{
            flex: 1,
            width: '100%',
            maxWidth: Platform.OS === 'web' ? 480 : undefined,
            alignSelf: 'center',
            backgroundColor: c.white,
          }}
        >
          {error ? (
            <T style={{ padding: 30 }}>Couldn’t load the app fonts. Please restart the app.</T>
          ) : loaded ? (
            <SessionProvider>
              <Routes />
            </SessionProvider>
          ) : (
            <ActivityIndicator size="large" color={c.blue} />
          )}
        </View>
      </View>
    </SafeAreaProvider>
  );
}
