import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { Button, Field, Notice, T } from '../ui';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import type { CommunityPost } from '../api/types';
import { useResource } from './hooks';

export function BookmarkControl({
  kind,
  id,
  compact = false,
}: {
  kind: 'thread' | 'material';
  id: string;
  compact?: boolean;
}) {
  const resource = useResource<{ saved: boolean }>(`/bookmarks/${kind}/${id}`);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function toggle() {
    setBusy(true);
    setError('');
    try {
      await api.request(`/bookmarks/${kind}/${id}`, {
        method: resource.data?.saved ? 'DELETE' : 'PUT',
      });
      resource.reload();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <View style={{ gap: 6 }}>
      <Button
        label={
          compact
            ? resource.data?.saved
              ? 'Saved'
              : 'Save'
            : resource.data?.saved
              ? 'Remove from saved items'
              : 'Save for later'
        }
        icon="Bookmark"
        kind="quiet"
        busy={busy}
        disabled={resource.loading || !!resource.error}
        onPress={() => void toggle()}
      />
      {!!resource.error && (
        <Button label="Retry saved status" kind="quiet" onPress={resource.reload} />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
    </View>
  );
}
export function FollowControl({ id }: { id: string }) {
  const resource = useResource<{ following: boolean }>(`/community/posts/${id}/subscription`);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function toggle() {
    setBusy(true);
    setError('');
    try {
      await api.request(`/community/posts/${id}/subscription`, {
        method: 'PUT',
        body: JSON.stringify({ following: !resource.data?.following }),
      });
      resource.reload();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <View style={{ gap: 6 }}>
      <Button
        label={resource.data?.following ? 'Mute' : 'Follow'}
        icon="Bell"
        kind="quiet"
        busy={busy}
        disabled={resource.loading || !!resource.error}
        onPress={() => void toggle()}
      />
      {!!resource.error && (
        <Button label="Retry follow status" kind="quiet" onPress={resource.reload} />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
    </View>
  );
}
export function PostControls({ post, onChanged }: { post: CommunityPost; onChanged: () => void }) {
  const [expanded, setExpanded] = useState(false);
  const [confirm, setConfirm] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function act(action: string) {
    setBusy(true);
    setError('');
    try {
      await api.request(`/community/posts/${post.id}${action === 'delete' ? '' : '/' + action}`, {
        method: action === 'delete' ? 'DELETE' : 'POST',
        ...(action === 'report' ? { body: JSON.stringify({ reason }) } : {}),
      });
      setConfirm('');
      setExpanded(false);
      setNotice(
        action === 'report'
          ? 'Report sent for review.'
          : action === 'block'
            ? 'Contributions from this participant are hidden in this identity mode.'
            : 'Your contribution was deleted.',
      );
      if (action === 'block') router.replace('/(tabs)/discuss');
      else if (action !== 'report') onChanged();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  if (post.isDeleted) return <T variant="small">Deleted contribution</T>;
  return (
    <View style={{ gap: 8 }}>
      {!!post.editedAt && <T variant="small">Edited</T>}
      <Button
        label={expanded ? 'Close options' : post.isMine ? 'Manage your post' : 'Post options'}
        kind="quiet"
        icon="MoreHorizontal"
        onPress={() => setExpanded(!expanded)}
      />
      {expanded && (
        <>
          {post.isMine ? (
            <>
              <Button
                label="Edit"
                kind="quiet"
                icon="Pencil"
                onPress={() => router.push({ pathname: '/compose', params: { editId: post.id } })}
              />
              <Button
                label="Delete"
                kind="quiet"
                icon="Trash2"
                onPress={() => setConfirm('delete')}
              />
            </>
          ) : (
            <>
              <Button
                label="Report this contribution"
                kind="quiet"
                icon="Flag"
                onPress={() => setConfirm('report')}
              />
              <Button
                label={post.isAnonymous ? 'Hide this anonymous participant' : 'Hide this profile'}
                kind="quiet"
                onPress={() => setConfirm('block')}
              />
            </>
          )}
        </>
      )}
      {confirm === 'delete' && (
        <T>Your text will be removed. Other students’ replies stay in the conversation.</T>
      )}
      {confirm === 'block' && (
        <T>
          {post.isAnonymous
            ? 'Hide this participant’s anonymous contributions. Named posts stay separate to protect anonymity.'
            : 'Hide this profile’s named contributions. Anonymous posts stay separate.'}{' '}
          You can undo this in settings.
        </T>
      )}
      {confirm === 'report' && (
        <Field
          label="What should the moderator review?"
          value={reason}
          onChangeText={setReason}
          maxLength={1000}
          multiline
        />
      )}
      {!!confirm && (
        <>
          <Button
            label={
              confirm === 'delete'
                ? 'Confirm deletion'
                : confirm === 'report'
                  ? 'Send report'
                  : 'Confirm hide'
            }
            busy={busy}
            disabled={confirm === 'report' && !reason.trim()}
            onPress={() => void act(confirm)}
          />
          <Button label="Keep things as they are" kind="quiet" onPress={() => setConfirm('')} />
        </>
      )}
      {!!notice && <Notice tone="success">{notice}</Notice>}
      {!!error && <Notice tone="error">{error}</Notice>}
    </View>
  );
}
