import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Button, Notice, T } from '../ui';
import { colors as c } from '../theme';

export function ResourceState({
  loading,
  error,
  retry,
}: {
  loading: boolean;
  error: string;
  retry: () => void;
}) {
  return (
    <>
      {loading && (
        <View style={{ padding: 22, alignItems: 'center', gap: 10 }}>
          <ActivityIndicator color={c.blue} />
          <T variant="small">Bringing your study space together…</T>
        </View>
      )}
      {!!error && (
        <>
          <Notice tone="error">{error}</Notice>
          <Button label="Try again" kind="secondary" onPress={retry} />
        </>
      )}
    </>
  );
}
