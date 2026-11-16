import React, { useCallback, useState } from 'react';
import { router, useFocusEffect } from 'expo-router';
import { View } from 'react-native';
import { Button, Chip, Header, Notice, Page, Row, Section, T, layout } from '../ui';
import { push } from '../push/runtime';
import { useOperation } from './operation';
export function PhoneNotificationsScreen() {
  const [state, setState] = useState(push.snapshot());
  const [override, setUpvotes] = useState<boolean | null>(null);
  const upvotes = override ?? state.upvotes;
  const operation = useOperation();
  useFocusEffect(
    useCallback(() => {
      const unsubscribe = push.subscribe(setState);
      void push.sync().catch(() => {});
      return unsubscribe;
    }, []),
  );
  return (
    <Page>
      <Header title="Phone notifications" />
      <T>
        Get a gentle heads-up for replies, finished study sessions and your chosen revision
        reminder.
      </T>
      {!!state.reason && <Notice>{state.reason}</Notice>}
      {!!state.error && <Notice tone="error">{state.error}</Notice>}
      {state.pendingRevoke && (
        <Notice>
          Unlinking this device is waiting for a connection. Generic alerts may still arrive until
          it reconnects.
        </Notice>
      )}
      <Section title="Private previews" />
      <T>
        Phone previews say only that an activity update is available. Open the app to view it with
        your signed-in account.
      </T>
      <Section title="Grouped upvotes" />
      <T>Upvote alerts are optional. They also need to be enabled in Activity preferences.</T>
      <View style={layout.inline}>
        <Chip
          label="Off"
          selected={!upvotes}
          onPress={() => {
            if (!state.busy) setUpvotes(false);
          }}
        />
        <Chip
          label="On"
          selected={upvotes}
          onPress={() => {
            if (!state.busy) setUpvotes(true);
          }}
        />
      </View>
      <Button
        label={state.enabled ? 'Update phone preferences' : 'Enable phone notifications'}
        disabled={!state.available}
        busy={state.busy || operation.busy}
        onPress={() => {
          void operation.run(async () => {
            await push.sync(true, upvotes);
            setUpvotes(null);
          });
        }}
      />
      {state.enabled && (
        <Button
          label="Turn off on this device"
          kind="quiet"
          busy={state.busy || operation.busy}
          onPress={() => {
            void operation.run(async () => {
              await push.disable();
            });
          }}
        />
      )}
      {!!operation.error && <Notice tone="error">{operation.error}</Notice>}
      <Row
        title="Activity preferences"
        detail="Replies, followed threads, study sessions and upvotes"
        icon="Bell"
        onPress={() => router.push('/activity-settings')}
      />
      <Row
        title="Time and quiet hours"
        detail="Choose your timezone, quiet window and daily reminder"
        icon="Moon"
        onPress={() => router.push('/reminders')}
      />
      <Row
        title="Open Activity"
        detail="Your inbox works with phone notification permission off"
        icon="History"
        onPress={() => router.push('/activity')}
      />
      <T variant="small">
        The study service must be running to send new updates. Turning phone alerts off keeps your
        Activity history.
      </T>
    </Page>
  );
}
