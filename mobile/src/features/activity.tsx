import React, { useCallback, useRef, useState } from 'react';
import { Pressable, View } from 'react-native';
import { router, useFocusEffect } from 'expo-router';
import { Button, Chip, Empty, Header, Icon, IconButton, Notice, Page, T } from '../ui';
import { colors as c } from '../theme';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import type { ActivityPage, ActivityPreferences, ActivityDestination } from '../api/types';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';

export function ActivityBell() {
  const resource = useResource<{ count: number }>('/activity/count', 15000);
  const count = resource.data?.count;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={typeof count === 'number' ? `Activity, ${count} unread` : 'Activity'}
      onPress={() => router.push('/activity')}
      style={{
        minWidth: 48,
        minHeight: 48,
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
      }}
    >
      <Icon name="Bell" color={c.blue} />
      {typeof count === 'number' && count > 0 && (
        <View
          style={{
            position: 'absolute',
            right: 0,
            top: 0,
            backgroundColor: c.blue,
            borderRadius: 12,
            minWidth: 22,
            paddingHorizontal: 4,
            alignItems: 'center',
          }}
        >
          <T variant="small" style={{ color: c.white }}>
            {count > 99 ? '99+' : count}
          </T>
        </View>
      )}
    </Pressable>
  );
}
export function ActivityScreen() {
  const [unread, setUnread] = useState(false);
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const resource = useResource<ActivityPage>(`/activity?unread=${unread}&page=${page}`, 15000);
  const focus = useRef<AbortController | null>(null);
  useFocusEffect(
    useCallback(() => {
      const controller = new AbortController();
      focus.current = controller;
      setBusy(false);
      return () => controller.abort();
    }, []),
  );
  async function readAll() {
    const controller = focus.current;
    if (busy || !controller || controller.signal.aborted) return;
    setBusy(true);
    setError('');
    try {
      await api.request('/activity/read-all', { method: 'PUT', signal: controller.signal });
      if (!controller.signal.aborted) resource.reload();
    } catch (e) {
      if (!controller.signal.aborted) setError(errorText(e));
    } finally {
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function open(id: string) {
    const controller = focus.current;
    if (busy || !controller || controller.signal.aborted) return;
    setBusy(true);
    setError('');
    try {
      const destination = await api.request<ActivityDestination>(`/activity/${id}/destination`, {
        signal: controller.signal,
      });
      await api.request(`/activity/${id}/read`, { method: 'PUT', signal: controller.signal });
      if (controller.signal.aborted) return;
      if (destination.kind === 'study')
        router.push({ pathname: '/job', params: { id: destination.id } });
      else
        router.push({
          pathname: '/thread',
          params: {
            id: destination.id,
            replyId: destination.replyId || '',
            replyPage: String(destination.replyPage || 1),
          },
        });
    } catch (e) {
      if (!controller.signal.aborted) setError(errorText(e));
    } finally {
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  return (
    <Page onRefresh={resource.reload} refreshing={resource.loading}>
      <Header
        title="Activity"
        right={
          <IconButton
            icon="Settings"
            label="Activity preferences"
            onPress={() => router.push('/activity-settings')}
          />
        }
      />
      <T>A reply, a useful conversation, or your next study session—pick up what matters.</T>
      <View style={{ flexDirection: 'row', gap: 8 }}>
        <Chip
          label="All"
          selected={!unread}
          onPress={() => {
            setUnread(false);
            setPage(1);
          }}
        />
        <Chip
          label="Unread"
          selected={unread}
          onPress={() => {
            setUnread(true);
            setPage(1);
          }}
        />
      </View>
      <ResourceState {...resource} retry={resource.reload} />
      {!!error && <Notice tone="error">{error}</Notice>}
      {!!resource.data?.unreadCount && (
        <Button label="Mark all as read" kind="quiet" busy={busy} onPress={() => void readAll()} />
      )}
      {resource.data?.data.map((item) => (
        <Pressable
          key={item.id}
          disabled={busy}
          accessibilityRole="button"
          accessibilityLabel={`${item.readAt ? 'Read' : 'Unread'}. ${item.title}. ${item.detail}`}
          onPress={() => void open(item.id)}
          style={({ pressed }) => ({
            flexDirection: 'row',
            gap: 12,
            paddingVertical: 20,
            borderBottomWidth: 1,
            borderBottomColor: c.line,
            opacity: pressed ? 0.65 : 1,
          })}
        >
          <View
            style={{
              width: 40,
              height: 40,
              borderRadius: 13,
              backgroundColor: c.blueSoft,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icon
              name={
                item.kind === 'reply'
                  ? 'MessageCircle'
                  : item.kind === 'upvote'
                    ? 'ArrowUp'
                    : item.kind === 'study_failed'
                      ? 'AlertCircle'
                      : 'CheckCircle2'
              }
              color={c.blue}
              size={22}
            />
          </View>
          <View style={{ flex: 1, gap: 6 }}>
            <T variant="label">{item.title}</T>
            <T variant="small">{item.detail}</T>
            <T variant="small" style={{ color: !item.readAt ? c.blue : c.muted }}>
              {!item.readAt ? 'Unread · ' : ''}
              {new Date(item.updatedAt).toLocaleDateString()}
            </T>
          </View>
        </Pressable>
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty
          title={unread ? 'You’re all caught up' : 'Your activity starts here'}
          body="Replies in followed discussions and completed study sessions appear here. Choose what you receive in preferences."
        />
      )}
      <Button label="Refresh activity" kind="quiet" icon="RefreshCw" onPress={resource.reload} />
      {resource.data && resource.data.total > 20 && (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          <Button
            label="Previous"
            kind="quiet"
            disabled={page === 1}
            onPress={() => setPage(page - 1)}
          />
          <T variant="small">Page {page}</T>
          <Button
            label="Next"
            kind="quiet"
            disabled={page * 20 >= resource.data.total}
            onPress={() => setPage(page + 1)}
          />
        </View>
      )}
    </Page>
  );
}
export function ActivitySettingsScreen() {
  const resource = useResource<ActivityPreferences>('/activity/preferences');
  return (
    <Page>
      <Header title="Activity preferences" />
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data && <PreferenceForm initial={resource.data} />}
    </Page>
  );
}
function PreferenceForm({ initial }: { initial: ActivityPreferences }) {
  const [values, setValues] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function save() {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api.request('/activity/preferences', { method: 'PUT', body: JSON.stringify(values) });
      setNotice('Your activity preferences are saved.');
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  const labels: Record<keyof ActivityPreferences, string> = {
    replies: 'Replies to my discussions',
    followedThreads: 'Replies in threads I follow',
    studyJobs: 'Finished or failed study sessions',
    upvotes: 'Grouped upvotes on my contributions',
  };
  return (
    <>
      <T>
        Choose new activity for your inbox. Existing items stay in your history. You can also mute
        an individual thread.
      </T>
      {Object.entries(labels).map(([key, label]) => (
        <Pressable
          key={key}
          accessibilityRole="switch"
          accessibilityLabel={label}
          accessibilityState={{ checked: values[key as keyof ActivityPreferences] }}
          aria-checked={values[key as keyof ActivityPreferences]}
          onPress={() =>
            setValues((current) => ({
              ...current,
              [key]: !current[key as keyof ActivityPreferences],
            }))
          }
          style={{
            flexDirection: 'row',
            gap: 12,
            alignItems: 'center',
            minHeight: 64,
            paddingVertical: 16,
            borderBottomWidth: 1,
            borderBottomColor: c.line,
          }}
        >
          <T style={{ flex: 1 }}>{label}</T>
          <Icon
            name={values[key as keyof ActivityPreferences] ? 'CheckCircle2' : 'Plus'}
            color={values[key as keyof ActivityPreferences] ? c.blue : c.muted}
          />
        </Pressable>
      ))}
      <Button label="Save activity preferences" busy={busy} onPress={() => void save()} />
      {!!notice && <Notice tone="success">{notice}</Notice>}
      {!!error && <Notice tone="error">{error}</Notice>}
      <T variant="small">
        Phone alerts aren’t available yet. These preferences control your in-app inbox.
      </T>
    </>
  );
}
