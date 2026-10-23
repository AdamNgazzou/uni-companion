import React, { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { Button, Chip, Empty, Field, Header, Icon, Notice, Page, Section, T, layout } from '../ui';
import { colors as c, fonts } from '../theme';
import { api, newKey } from '../api/runtime';
import { errorText } from '../api/client';
import type { CommunityPost, CommunityPage, Course } from '../api/types';
import { useSession } from '../auth/session';
import { useResource } from './hooks';
import { ResourceState } from './resource-state';
import { BookmarkControl, FollowControl, PostControls } from './forum-controls';
import { composerDrafts } from '../community/draft-runtime';
import type { ComposerDraft } from '../community/drafts';

function age(value: string) {
  const minutes = Math.max(1, Math.floor((Date.now() - new Date(value).getTime()) / 60000));
  return minutes < 60
    ? `${minutes}m ago`
    : minutes < 1440
      ? `${Math.floor(minutes / 60)}h ago`
      : `${Math.floor(minutes / 1440)}d ago`;
}
function Avatar({ name, anonymous }: { name: string; anonymous?: boolean }) {
  const palette = [c.blueSoft, c.orangeSoft, '#E9F5EF'];
  const shade = name.charCodeAt(0) % palette.length;
  return (
    <View
      style={{
        width: 38,
        height: 38,
        borderRadius: 14,
        backgroundColor: anonymous ? c.blueSoft : palette[shade],
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {anonymous ? (
        <Icon name="UserRound" size={20} color={c.blue} />
      ) : (
        <T variant="small" style={{ fontFamily: fonts.bold, color: c.ink }}>
          {name
            .trim()
            .split(/\s+/)
            .slice(0, 2)
            .map((word) => word[0])
            .join('')}
        </T>
      )}
    </View>
  );
}
function Identity({ post }: { post: CommunityPost }) {
  return (
    <View style={layout.inline}>
      <Avatar name={post.authorName} anonymous={post.isAnonymous} />
      <View style={{ flex: 1, gap: 2 }}>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 7 }}>
          <T variant="small" style={{ color: c.ink, fontFamily: fonts.bold }}>
            {post.authorName}
            {post.isMine ? ' (you)' : ''}
          </T>
          {post.isOriginalPoster && (
            <T variant="small" style={{ color: c.blue }}>
              Author
            </T>
          )}
          {post.isSample && (
            <T
              variant="small"
              style={{
                color: c.muted,
                backgroundColor: c.canvas,
                paddingHorizontal: 6,
                borderRadius: 5,
              }}
            >
              Sample
            </T>
          )}
        </View>
        <T variant="small">{age(post.createdAt)}</T>
      </View>
    </View>
  );
}
function Votes({ post, replies = false }: { post: CommunityPost; replies?: boolean }) {
  const [value, setValue] = useState({ score: post.score, myVote: post.myVote });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function vote(direction: -1 | 1) {
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      const result = await api.request<CommunityPost>(`/community/posts/${post.id}/vote`, {
        method: 'PUT',
        body: JSON.stringify({ value: value.myVote === direction ? 0 : direction }),
      });
      setValue({ score: result.score, myVote: result.myVote });
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <View style={{ gap: 5 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 3, flexWrap: 'wrap' }}>
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            borderRadius: 14,
            backgroundColor: value.myVote ? c.blueSoft : c.canvas,
          }}
        >
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Upvote"
            aria-pressed={value.myVote === 1}
            accessibilityState={{ selected: value.myVote === 1, disabled: busy }}
            disabled={busy}
            onPress={() => void vote(1)}
            style={{
              minWidth: 44,
              minHeight: 44,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icon name="ArrowUp" size={20} color={value.myVote === 1 ? c.blue : c.muted} />
          </Pressable>
          <T
            variant="small"
            accessibilityLabel={`Score ${value.score}`}
            style={{
              color: value.myVote ? c.blue : c.ink,
              fontFamily: fonts.bold,
              minWidth: 18,
              textAlign: 'center',
            }}
          >
            {value.score}
          </T>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Downvote"
            aria-pressed={value.myVote === -1}
            accessibilityState={{ selected: value.myVote === -1, disabled: busy }}
            disabled={busy}
            onPress={() => void vote(-1)}
            style={{ minWidth: 44, minHeight: 44, alignItems: 'center', justifyContent: 'center' }}
          >
            <Icon name="ArrowDown" size={20} color={value.myVote === -1 ? c.blue : c.muted} />
          </Pressable>
        </View>
        {replies && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Open ${post.replyCount} replies`}
            onPress={() => router.push({ pathname: '/thread', params: { id: post.id } })}
            style={{
              flexDirection: 'row',
              gap: 7,
              alignItems: 'center',
              paddingHorizontal: 12,
              minHeight: 44,
            }}
          >
            <Icon name="MessageCircle" size={18} color={c.muted} />
            <T variant="small">
              {post.replyCount} {post.replyCount === 1 ? 'reply' : 'replies'}
            </T>
          </Pressable>
        )}
      </View>
      {!!error && <Notice tone="error">{error}</Notice>}
    </View>
  );
}
function ThreadPreview({ post }: { post: CommunityPost }) {
  return (
    <View style={{ gap: 13, paddingVertical: 23, borderBottomWidth: 1, borderBottomColor: c.line }}>
      <Identity post={post} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 7 }}>
        <T variant="small" style={{ color: c.blue, fontFamily: fonts.bold }}>
          {post.courseTitle || 'Campus life'}
        </T>
        <T variant="small">· {post.kind}</T>
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Open discussion: ${post.title}`}
        onPress={() => router.push({ pathname: '/thread', params: { id: post.id } })}
        style={({ pressed }) => ({ gap: 7, opacity: pressed ? 0.65 : 1 })}
      >
        <T variant="title">{post.title}</T>
        <T numberOfLines={3} style={{ color: c.muted }}>
          {post.body}
        </T>
      </Pressable>
      <Votes post={post} replies />
    </View>
  );
}
export function DiscussScreen() {
  const params = useLocalSearchParams<{ courseId?: string }>();
  return <DiscussionFeed key={params.courseId || ''} courseId={params.courseId || ''} />;
}
function DiscussionFeed({ courseId }: { courseId: string }) {
  const setCourse = (value: string) => router.setParams({ courseId: value });
  const [kind, setKind] = useState('');
  const [sort, setSort] = useState('recent');
  const [page, setPage] = useState(1);
  const courses = useResource<{ data: Course[] }>('/courses?limit=100');
  const resource = useResource<CommunityPage>(
    `/community/posts?sort=${sort}&page=${page}${courseId ? '&courseId=' + encodeURIComponent(courseId) : ''}${kind ? '&kind=' + encodeURIComponent(kind) : ''}`,
  );
  return (
    <Page onRefresh={resource.reload} refreshing={resource.loading}>
      <Header title="Your campus, together" back={false} />
      <View style={{ gap: 8 }}>
        <T variant="heading">{'Good questions.\nBetter together.'}</T>
        <T style={{ color: c.muted }}>A little help from people figuring it out too.</T>
      </View>
      <Button
        label="Start a conversation"
        icon="Plus"
        onPress={() => router.push({ pathname: '/compose', params: { courseId } })}
      />
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={{ flexGrow: 0 }}
        contentContainerStyle={{ gap: 8 }}
      >
        <Chip
          label="All campus"
          selected={!courseId}
          onPress={() => {
            setCourse('');
            setPage(1);
          }}
        />
        {(courses.data?.data || []).map((course) => (
          <Chip
            key={course.id}
            label={course.title}
            selected={courseId === course.id}
            onPress={() => {
              setCourse(course.id);
              setPage(1);
            }}
          />
        ))}
      </ScrollView>
      {!!courses.error && (
        <Notice>
          Course filters couldn’t load.{' '}
          <T onPress={courses.reload} style={{ color: c.blue }}>
            Try again
          </T>
        </Notice>
      )}
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <Chip
          label="Recent"
          selected={sort === 'recent'}
          onPress={() => {
            setSort('recent');
            setPage(1);
          }}
        />
        <Chip
          label="Top"
          selected={sort === 'top'}
          onPress={() => {
            setSort('top');
            setPage(1);
          }}
        />
        <View style={{ flex: 1 }} />
        <T variant="small">
          {resource.data
            ? `${resource.data.total} ${resource.data.total === 1 ? 'thread' : 'threads'}`
            : ''}
        </T>
      </View>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={{ flexGrow: 0 }}
        contentContainerStyle={{ gap: 8 }}
      >
        {['All posts', 'Question', 'Study tip', 'Discussion'].map((label) => (
          <Chip
            key={label}
            label={label}
            selected={kind === (label === 'All posts' ? '' : label)}
            onPress={() => {
              setKind(label === 'All posts' ? '' : label);
              setPage(1);
            }}
          />
        ))}
      </ScrollView>
      {resource.data?.data.some((post) => post.isSample) && (
        <View style={{ flexDirection: 'row', gap: 7, alignItems: 'center' }}>
          <Icon name="Users" size={16} color={c.muted} />
          <T variant="small" style={{ flex: 1 }}>
            Sample community · fictional students and posts.
          </T>
        </View>
      )}
      <ResourceState {...resource} retry={resource.reload} />
      {resource.data?.data.map((post) => (
        <ThreadPreview key={post.id} post={post} />
      ))}
      {resource.data?.total === 0 && (
        <Empty
          title="Be the first to ask."
          body="A small question can start a useful conversation. Share yours with campus."
        />
      )}
      {resource.data && resource.data.total > 15 && (
        <View style={{ gap: 10 }}>
          <Button
            label="Previous page"
            kind="secondary"
            disabled={page === 1}
            onPress={() => setPage((value) => value - 1)}
          />
          <T variant="small">{page}</T>
          <Button
            label="Next page"
            kind="secondary"
            disabled={page * 15 >= resource.data.total}
            onPress={() => setPage((value) => value + 1)}
          />
        </View>
      )}
    </Page>
  );
}
export function ThreadScreen() {
  const { id, replyPage, replyId } = useLocalSearchParams<{
    id: string;
    replyPage?: string;
    replyId?: string;
  }>();
  const [page, setPage] = useState(Math.min(100000, Math.max(1, Number(replyPage) || 1)));
  const [resolutionError, setResolutionError] = useState('');
  const thread = useResource<CommunityPost>(`/community/posts/${id}`);
  const replies = useResource<CommunityPage>(`/community/posts/${id}/replies?page=${page}`);
  return (
    <Page
      footer={
        thread.data && !thread.data.isDeleted ? (
          <Button
            label="Add your reply"
            icon="Reply"
            onPress={() => router.push({ pathname: '/compose', params: { parentId: id } })}
          />
        ) : undefined
      }
    >
      <Header title="The conversation" />
      <ResourceState {...thread} retry={thread.reload} />
      {thread.data && (
        <>
          <Identity post={thread.data} />
          <T variant="small" style={{ color: c.blue, fontFamily: fonts.bold }}>
            {thread.data.courseTitle || 'Campus life'} · {thread.data.kind}
          </T>
          <T variant="heading">{thread.data.title}</T>
          <T selectable style={{ lineHeight: 27 }}>
            {thread.data.body}
          </T>
          {!thread.data.isDeleted && <Votes post={thread.data} />}
          {thread.data.acceptedReplyId && (
            <Notice tone="success">The asker selected a helpful reply.</Notice>
          )}
          <PostControls post={thread.data} onChanged={thread.reload} />
          {!thread.data.isDeleted && (
            <>
              <View style={{ flexDirection: 'row', gap: 8 }}>
                <View style={{ flex: 1 }}>
                  <BookmarkControl kind="thread" id={id} compact />
                </View>
                <View style={{ flex: 1 }}>
                  <FollowControl id={id} />
                </View>
              </View>
            </>
          )}
          {!!resolutionError && <Notice tone="error">{resolutionError}</Notice>}
          {thread.data.isSample && (
            <T variant="small">
              This thread was started by a fictional student for the demo. Your replies are real.
            </T>
          )}
          <Section
            title={`${replies.data?.total ?? thread.data.replyCount} ${(replies.data?.total ?? thread.data.replyCount) === 1 ? 'reply' : 'replies'}`}
          />
          <ResourceState {...replies} retry={replies.reload} />
          {replies.data?.data.map((reply) => (
            <View
              key={reply.id}
              style={{
                gap: 12,
                paddingBottom: 20,
                paddingTop: 8,
                borderBottomWidth: 1,
                borderBottomColor: c.line,
              }}
            >
              <Identity post={reply} />
              <T selectable>{reply.body}</T>
              {!reply.isDeleted && <Votes post={reply} />}
              {reply.id === replyId && (
                <Notice>This is the reply you opened from your activity.</Notice>
              )}
              {thread.data?.acceptedReplyId === reply.id && (
                <Notice tone="success">Helpful reply · selected by the asker</Notice>
              )}
              <PostControls
                post={reply}
                onChanged={() => {
                  thread.reload();
                  replies.reload();
                }}
              />
              {thread.data?.isMine &&
                thread.data.kind === 'Question' &&
                !thread.data.isDeleted &&
                !reply.isDeleted && (
                  <Button
                    label={
                      thread.data.acceptedReplyId === reply.id
                        ? 'Unselect helpful reply'
                        : 'Mark as helpful reply'
                    }
                    kind="quiet"
                    onPress={() => {
                      setResolutionError('');
                      void api
                        .request(`/community/posts/${id}/resolution`, {
                          method: 'PUT',
                          body: JSON.stringify({
                            replyId: thread.data?.acceptedReplyId === reply.id ? null : reply.id,
                          }),
                        })
                        .then(thread.reload)
                        .catch((e) => setResolutionError(errorText(e)));
                    }}
                  />
                )}
            </View>
          ))}
          {replies.data?.total === 0 && (
            <Empty
              title="A fresh conversation."
              body="Got an explanation or a similar question? Add the first reply."
            />
          )}
          {replies.data && replies.data.total > 30 && (
            <View style={{ gap: 10 }}>
              <Button
                label="Previous replies"
                kind="secondary"
                disabled={page === 1}
                onPress={() => setPage((value) => value - 1)}
              />
              <T variant="small">Page {page}</T>
              <Button
                label="Next replies"
                kind="secondary"
                disabled={page * 30 >= replies.data.total}
                onPress={() => setPage((value) => value + 1)}
              />
            </View>
          )}
        </>
      )}
    </Page>
  );
}
export function ComposeScreen() {
  const params = useLocalSearchParams<{ courseId?: string; parentId?: string; editId?: string }>();
  const { profile } = useSession();
  const edit = useResource<CommunityPost>(
    params.editId ? `/community/posts/${params.editId}/entry` : null,
  );
  if (params.editId && (!edit.data || !edit.data.isMine || edit.data.isDeleted))
    return (
      <Page>
        <Header title="Edit contribution" />
        <ResourceState {...edit} retry={edit.reload} />
        {edit.data && <Notice>This contribution cannot be edited.</Notice>}
      </Page>
    );
  return (
    <Composer
      key={`${profile?.id}:${params.editId || params.parentId || params.courseId || 'new'}`}
      params={params}
      initial={edit.data || undefined}
    />
  );
}
function Composer({
  params,
  initial,
}: {
  params: { courseId?: string; parentId?: string; editId?: string };
  initial?: CommunityPost;
}) {
  const { profile } = useSession();
  const parentId = params.parentId || initial?.parentId;
  const replying = !!parentId;
  const scope = params.editId
    ? `edit:${params.editId}`
    : parentId
      ? `reply:${parentId}`
      : `new:${params.courseId || 'campus'}`;
  const generation = useRef(composerDrafts.generation());
  const closed = useRef(false);
  const [draft, setDraft] = useState<ComposerDraft>(() => ({
    title: initial?.title || '',
    body: initial?.body || '',
    kind: initial?.kind || 'Question',
    courseId: params.courseId || initial?.courseId || '',
    anonymous: initial?.isAnonymous || false,
    requestKey: newKey(),
    updatedAt: Date.now(),
  }));
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const courses = useResource<{ data: Course[] }>(
    !replying && !params.editId ? '/courses?limit=100' : null,
  );
  const parent = useResource<CommunityPost>(parentId ? `/community/posts/${parentId}` : null);
  useEffect(() => {
    let active = true;
    if (!profile) return;
    void composerDrafts
      .read(profile.id, scope, generation.current)
      .then((value) => {
        if (active) {
          if (value) setDraft(value);
          setLoaded(true);
        }
      })
      .catch(() => {
        if (active) {
          setError('Could not restore your draft. You can still write a contribution.');
          setLoaded(true);
        }
      });
    return () => {
      active = false;
    };
  }, [profile, scope]);
  useEffect(() => {
    if (!loaded || !profile || closed.current) return;
    void composerDrafts
      .save(profile.id, scope, { ...draft, updatedAt: Date.now() }, generation.current)
      .catch(() => setError('Could not save this draft on your device.'));
  }, [draft, loaded, profile, scope]);
  function change(value: Partial<ComposerDraft>) {
    setDraft((current) => ({ ...current, ...value }));
  }
  async function submit() {
    if (busy || !profile) return;
    setBusy(true);
    setError('');
    const body = JSON.stringify(
      params.editId
        ? { title: draft.title.trim(), body: draft.body.trim() }
        : {
            body: draft.body.trim(),
            isAnonymous: draft.anonymous,
            ...(!replying
              ? { title: draft.title.trim(), kind: draft.kind, courseId: draft.courseId || null }
              : {}),
          },
    );
    const key = draft.lastPayload && draft.lastPayload !== body ? newKey() : draft.requestKey;
    const sent = { ...draft, requestKey: key, lastPayload: body, updatedAt: Date.now() };
    setDraft(sent);
    try {
      await composerDrafts.save(profile.id, scope, sent, generation.current);
      const post = await api.request<CommunityPost>(
        params.editId
          ? `/community/posts/${params.editId}`
          : replying
            ? `/community/posts/${parentId}/replies`
            : '/community/posts',
        {
          method: params.editId ? 'PATCH' : 'POST',
          headers: { 'Idempotency-Key': key },
          body,
        },
      );
      closed.current = true;
      void composerDrafts.remove(profile.id, scope).catch(() => {});
      if (params.editId || replying) router.back();
      else router.replace({ pathname: '/thread', params: { id: post.id } });
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function discard() {
    if (!profile) return;
    try {
      await composerDrafts.remove(profile.id, scope);
      change({
        title: initial?.title || '',
        body: initial?.body || '',
        requestKey: newKey(),
        lastPayload: undefined,
      });
      setConfirmDiscard(false);
    } catch (e) {
      setError(errorText(e));
    }
  }
  if (!loaded)
    return (
      <Page>
        <Header title="Your draft" />
        <T>Restoring your draft…</T>
      </Page>
    );
  return (
    <Page
      footer={
        <Button
          label={params.editId ? 'Save changes' : replying ? 'Post reply' : 'Post to campus'}
          busy={busy}
          disabled={
            !draft.body.trim() ||
            (!replying && !draft.title.trim()) ||
            (replying && (!parent.data || parent.data.isDeleted))
          }
          icon="Send"
          onPress={() => void submit()}
        />
      }
    >
      <Header
        title={
          params.editId ? 'Edit contribution' : replying ? 'Your reply' : 'Start a conversation'
        }
      />
      <T variant="heading">
        {replying ? 'A little help goes a long way.' : 'What’s on your mind?'}
      </T>
      {replying && (
        <>
          <ResourceState {...parent} retry={parent.reload} />
          {parent.data && <T variant="label">Replying to: {parent.data.title}</T>}
        </>
      )}
      {!replying && (
        <Field
          label="Title"
          value={draft.title}
          onChangeText={(title) => change({ title })}
          maxLength={180}
        />
      )}
      {!replying && !params.editId && (
        <>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {(['Question', 'Study tip', 'Discussion'] as const).map((kind) => (
              <Chip
                key={kind}
                label={kind}
                selected={draft.kind === kind}
                onPress={() => change({ kind })}
              />
            ))}
          </View>
          <Section title="Where does it belong?" />
          <ResourceState {...courses} retry={courses.reload} />
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={{ flexGrow: 0 }}
            contentContainerStyle={{ gap: 8 }}
          >
            <Chip
              label="Campus life"
              selected={!draft.courseId}
              onPress={() => change({ courseId: '' })}
            />
            {courses.data?.data.map((course) => (
              <Chip
                key={course.id}
                label={course.title}
                selected={draft.courseId === course.id}
                onPress={() => change({ courseId: course.id })}
              />
            ))}
          </ScrollView>
        </>
      )}
      <Field
        label={replying ? 'Your reply' : 'Your message'}
        value={draft.body}
        onChangeText={(body) => change({ body })}
        multiline
        maxLength={8000}
        style={{ minHeight: 150, textAlignVertical: 'top' }}
      />
      <T variant="small">{draft.body.length} / 8000</T>
      {!params.editId ? (
        <Pressable
          accessibilityRole="switch"
          accessibilityLabel="Post anonymously"
          accessibilityState={{ checked: draft.anonymous }}
          aria-checked={draft.anonymous}
          onPress={() => change({ anonymous: !draft.anonymous })}
          style={{
            flexDirection: 'row',
            gap: 12,
            padding: 16,
            borderRadius: 16,
            backgroundColor: draft.anonymous ? c.blueSoft : c.canvas,
            minHeight: 64,
            alignItems: 'center',
          }}
        >
          <Icon name={draft.anonymous ? 'EyeOff' : 'UserRound'} color={c.blue} />
          <View style={{ flex: 1, gap: 4 }}>
            <T variant="label">Post anonymously</T>
            <T variant="small">
              {draft.anonymous
                ? 'Shown as Anonymous student.'
                : `Shown as ${profile?.displayName || 'your profile'}.`}
            </T>
          </View>
          <Icon name={draft.anonymous ? 'Check' : 'UserRound'} color={c.blue} />
        </Pressable>
      ) : (
        <Notice>
          Posting identity stays {initial?.isAnonymous ? 'anonymous' : 'with your profile'} when you
          edit.
        </Notice>
      )}
      {draft.anonymous && (
        <T variant="small">
          Your identity is hidden from other students. The app retains ownership for account
          security and moderation.
        </T>
      )}
      <T variant="small">
        Your draft is saved on this device. Going back keeps it; signing out clears drafts.
      </T>
      <Button label="Discard draft" kind="quiet" onPress={() => setConfirmDiscard(true)} />
      {confirmDiscard && (
        <>
          <Notice>Discard the text saved for this contribution?</Notice>
          <Button label="Confirm discard" kind="secondary" onPress={() => void discard()} />
          <Button label="Keep my draft" kind="quiet" onPress={() => setConfirmDiscard(false)} />
        </>
      )}
      {!!error && <Notice tone="error">{error}</Notice>}
    </Page>
  );
}
