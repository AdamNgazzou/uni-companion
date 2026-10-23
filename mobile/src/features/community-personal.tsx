import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { Button, Chip, Empty, Header, Notice, Page, Row, T } from '../ui';
import { api } from '../api/runtime';
import { errorText } from '../api/client';
import type { MyDiscussionPage, SavedItem, Report } from '../api/types';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';

export function MyDiscussionsScreen() {
  const [kind, setKind] = useState('threads');
  const [page, setPage] = useState(1);
  const resource = useResource<MyDiscussionPage>(
    `/accounts/me/discussions?kind=${kind}&page=${page}`,
  );
  return (
    <Page>
      <Header title="My discussions" />
      <T>Your contributions, including posts you shared anonymously.</T>
      <View style={{ flexDirection: 'row', gap: 8 }}>
        {['threads', 'replies'].map((value) => (
          <Chip
            key={value}
            label={value === 'threads' ? 'My threads' : 'My replies'}
            selected={kind === value}
            onPress={() => {
              setKind(value);
              setPage(1);
            }}
          />
        ))}
      </View>
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data?.data.map(({ post, threadId, threadTitle, replyPage }) => (
        <Row
          key={post.id}
          title={post.parentId ? threadTitle : post.title}
          detail={`${post.parentId ? post.body.slice(0, 140) + ' · ' : ''}${post.isAnonymous ? 'Anonymous · only you see this in your history' : 'Your profile'} · ${post.courseTitle || 'Campus life'}`}
          icon={post.isAnonymous ? 'EyeOff' : 'MessageCircle'}
          onPress={() =>
            router.push({
              pathname: '/thread',
              params: {
                id: threadId,
                replyPage: String(replyPage),
                replyId: post.parentId ? post.id : '',
              },
            })
          }
        />
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty
          title={kind === 'threads' ? 'Your first conversation awaits' : 'No replies yet'}
          body="Your own contributions will appear here."
        />
      )}
      {resource.data && (
        <Pager page={page} more={page * 20 < resource.data.total} change={setPage} />
      )}
    </Page>
  );
}
function Pager({
  page,
  more,
  change,
}: {
  page: number;
  more: boolean;
  change: (page: number) => void;
}) {
  return (
    <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
      <Button
        label="Previous"
        kind="quiet"
        disabled={page === 1}
        onPress={() => change(page - 1)}
      />
      <T variant="small">Page {page}</T>
      <Button label="Next" kind="quiet" disabled={!more} onPress={() => change(page + 1)} />
    </View>
  );
}
export function SavedItemsScreen() {
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');
  const resource = useResource<{ data: SavedItem[]; hasMore: boolean }>(`/bookmarks?page=${page}`);
  async function remove(item: SavedItem) {
    try {
      await api.request(`/bookmarks/${item.kind}/${item.targetId}`, { method: 'DELETE' });
      resource.reload();
    } catch (e) {
      setError(errorText(e));
    }
  }
  return (
    <Page>
      <Header title="Saved items" />
      <T>Threads and resources you bookmarked. Generated work remains in Study history.</T>
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data?.data.map((item) => (
        <View key={item.id} style={{ gap: 4 }}>
          <Row
            title={item.title}
            detail={item.kind === 'thread' ? 'Saved discussion' : 'Saved course resource'}
            icon={item.kind === 'thread' ? 'MessageCircle' : 'FileText'}
            onPress={() => {
              if (item.kind === 'thread')
                router.push({ pathname: '/thread', params: { id: item.targetId } });
              else if (item.contentType === 'application/pdf' && item.currentVersionId)
                router.push({
                  pathname: '/material',
                  params: {
                    id: item.targetId,
                    courseId: item.courseId,
                    versionId: item.currentVersionId,
                    title: item.title,
                  },
                });
              else router.push({ pathname: '/course', params: { id: item.courseId } });
            }}
          />
          <Button label="Remove bookmark" kind="quiet" onPress={() => void remove(item)} />
        </View>
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty
          title="Keep something useful close"
          body="Save a discussion or resource to find it here. Deleted or inaccessible items are hidden."
        />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
      {resource.data && <Pager page={page} more={resource.data.hasMore} change={setPage} />}
    </Page>
  );
}
export function BlockedScreen() {
  const [page, setPage] = useState(1);
  const resource = useResource<{ data: { id: string; label: string }[]; hasMore: boolean }>(
    `/community/blocks?page=${page}`,
  );
  const [error, setError] = useState('');
  async function unblock(id: string) {
    try {
      await api.request(`/community/blocks/${id}`, { method: 'DELETE' });
      resource.reload();
    } catch (e) {
      setError(errorText(e));
    }
  }
  return (
    <Page>
      <Header title="Hidden participants" />
      <T>
        Named profiles and anonymous participants stay separate. No hidden identity is revealed
        here.
      </T>
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data?.data.map((item) => (
        <Row
          key={item.id}
          title={item.label}
          detail="Tap to show contributions again"
          icon="EyeOff"
          onPress={() => void unblock(item.id)}
        />
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty title="Nothing hidden" body="You can hide contributions from a post’s options." />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
      {resource.data && <Pager page={page} more={resource.data.hasMore} change={setPage} />}
    </Page>
  );
}
export function ReportsScreen() {
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const resource = useResource<{ data: Report[] }>(`/community/reports?page=${page}`);
  async function review(id: string, action: string) {
    setBusy(true);
    setError('');
    try {
      await api.request(`/community/reports/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ action }),
      });
      resource.reload();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Page>
      <Header title="Community reports" />
      <T>Review reported content. Removing it hides its text and keeps other replies.</T>
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data?.data.map((item) => (
        <View key={item.id} style={{ gap: 10, paddingVertical: 16 }}>
          <T variant="title">{item.title || 'Reported reply'}</T>
          <T>{item.body}</T>
          <Notice>Report: {item.reason}</Notice>
          <Button
            label="Open conversation"
            kind="quiet"
            onPress={() => router.push({ pathname: '/thread', params: { id: item.threadId } })}
          />
          <Button
            label="Remove reported content"
            disabled={busy}
            onPress={() => void review(item.id, 'remove')}
          />
          <Button
            label="Dismiss report"
            kind="quiet"
            disabled={busy}
            onPress={() => void review(item.id, 'dismiss')}
          />
        </View>
      ))}
      {!resource.loading && !resource.error && !resource.data?.data.length && (
        <Empty
          title="No open reports"
          body="New reports will appear here for an administrator to review."
        />
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
      {resource.data && (
        <Pager page={page} more={resource.data.data.length === 20} change={setPage} />
      )}
    </Page>
  );
}
