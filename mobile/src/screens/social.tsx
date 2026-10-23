import React, { useState } from 'react';
import { Pressable, ScrollView, Switch, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import {
  Button,
  Chip,
  Empty,
  Field,
  Header,
  Hero,
  Icon,
  IconButton,
  Notice,
  Page,
  SearchField,
  Section,
  T,
  layout,
  useNav,
} from '../ui';
import { colors as c, fonts } from '../theme';
import { uid, useSaved, useStore } from '../store';
import { Comment, Post, RootStack } from '../types';
import { nextVote, visibleAuthor } from '../logic';
function timeAgo(at: number) {
  const minutes = Math.max(1, Math.floor((Date.now() - at) / 60000));
  return minutes < 60
    ? `${minutes}m`
    : minutes < 1440
      ? `${Math.floor(minutes / 60)}h`
      : `${Math.floor(minutes / 1440)}d`;
}
export function VoteBar({
  id,
  score = 0,
  replies,
  onReply,
}: {
  id: string;
  score?: number;
  replies?: number;
  onReply?: () => void;
}) {
  const { data, update } = useStore();
  const vote = data.votes[id] ?? 0;
  const cast = (direction: -1 | 1) =>
    update((s) => ({ ...s, votes: { ...s.votes, [id]: nextVote(s.votes[id] ?? 0, direction) } }));
  return (
    <View style={[layout.inline, { gap: 0 }]}>
      <IconButton
        label="Upvote"
        icon="ArrowUp"
        color={vote === 1 ? '#B65B0B' : c.ink}
        selected={vote === 1}
        onPress={() => cast(1)}
      />
      <T
        variant="label"
        accessibilityLabel={`Score ${score + vote}`}
        style={{ minWidth: 20, textAlign: 'center', color: vote ? c.blue : c.ink }}
      >
        {score + vote}
      </T>
      <IconButton
        label="Downvote"
        icon="ArrowDown"
        color={vote === -1 ? c.blue : c.ink}
        selected={vote === -1}
        onPress={() => cast(-1)}
      />
      {onReply && (
        <Pressable
          accessibilityRole="button"
          onPress={onReply}
          style={{
            flexDirection: 'row',
            gap: 7,
            alignItems: 'center',
            paddingHorizontal: 10,
            minHeight: 48,
          }}
        >
          <Icon name="MessageCircle" size={19} color={c.muted} />
          <T variant="small">{replies === undefined ? 'Reply' : `${replies} replies`}</T>
        </Pressable>
      )}
    </View>
  );
}
export function Avatar({
  name,
  anonymous,
  size = 38,
}: {
  name: string;
  anonymous?: boolean;
  size?: number;
}) {
  return (
    <View
      style={{
        width: size,
        height: size,
        borderRadius: size * 0.38,
        backgroundColor: anonymous ? c.blueSoft : c.orangeSoft,
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {anonymous ? (
        <Icon name="UserRound" size={size * 0.5} color={c.blue} />
      ) : (
        <T variant="label" style={{ color: '#915210', fontSize: size * 0.36 }}>
          {name
            .split(' ')
            .map((x) => x[0])
            .slice(0, 2)
            .join('')}
        </T>
      )}
    </View>
  );
}
export function PostCard({ post }: { post: Post }) {
  const nav = useNav();
  const { data } = useStore();
  const course = data.courses.find((x) => x.id === post.courseId);
  const bookmark = useSaved(post.id);
  return (
    <View style={{ gap: 11, paddingVertical: 21, borderBottomWidth: 1, borderBottomColor: c.line }}>
      <View style={layout.inline}>
        <Avatar name={post.author} anonymous={post.anonymous} />
        <View style={{ flex: 1 }}>
          <T variant="label" style={{ fontSize: 14 }}>
            {visibleAuthor(post)}
          </T>
          <T variant="small">
            {course?.code} · {timeAgo(post.createdAt)} ago
          </T>
        </View>
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Open discussion: ${post.title}`}
        onPress={() => nav.navigate('Thread', { id: post.id })}
        style={{ gap: 8 }}
      >
        <T variant="title" style={{ fontSize: 20, lineHeight: 26 }}>
          {post.title}
        </T>
        <T numberOfLines={2} style={{ color: c.muted }}>
          {post.body}
        </T>
      </Pressable>
      <View
        style={{
          alignSelf: 'flex-start',
          backgroundColor: post.tag === 'Question' ? c.orangeSoft : c.blueSoft,
          borderRadius: 9,
          paddingHorizontal: 10,
          paddingVertical: 4,
        }}
      >
        <T
          variant="small"
          style={{ color: post.tag === 'Question' ? '#865010' : c.blue, fontFamily: fonts.bold }}
        >
          {post.tag}
        </T>
      </View>
      <View style={[layout.inline, { justifyContent: 'space-between', gap: 0 }]}>
        <VoteBar
          id={post.id}
          score={post.score}
          replies={post.comments.filter((x) => !data.blocked.includes(x.ownerId)).length}
          onReply={() => nav.navigate('Thread', { id: post.id })}
        />
        <IconButton
          icon="Bookmark"
          color={c.blue}
          label={bookmark.saved ? 'Unsave discussion' : 'Save discussion'}
          selected={bookmark.saved}
          onPress={bookmark.toggle}
        />
      </View>
    </View>
  );
}
export function CommunityScreen() {
  const nav = useNav();
  const { data } = useStore();
  const [filter, setFilter] = useState('all');
  const [sort, setSort] = useState<'Latest' | 'Top'>('Latest');
  const [search, setSearch] = useState('');
  const posts = data.posts
    .filter(
      (x) =>
        !data.blocked.includes(x.ownerId) &&
        (filter === 'all' ? data.joined.includes(x.courseId) : x.courseId === filter) &&
        `${x.title} ${x.body}`.toLowerCase().includes(search.toLowerCase()),
    )
    .sort((a, b) =>
      sort === 'Latest'
        ? b.createdAt - a.createdAt
        : b.score + (data.votes[b.id] ?? 0) - (a.score + (data.votes[a.id] ?? 0)),
    );
  return (
    <Page>
      <View style={[layout.inline, { justifyContent: 'space-between', paddingTop: 6 }]}>
        <T variant="title">Community</T>
        <IconButton
          icon="Pencil"
          label="Create discussion"
          color={c.blue}
          onPress={() => nav.navigate('Compose')}
        />
      </View>
      <Hero title="Your course crowd." subtitle="Ask. Share. Learn together." />
      <SearchField value={search} onChangeText={setSearch} placeholder="Search discussions…" />
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ gap: 8 }}
      >
        <Chip label="My courses" selected={filter === 'all'} onPress={() => setFilter('all')} />
        {data.courses
          .filter((x) => data.joined.includes(x.id))
          .map((course) => (
            <Chip
              key={course.id}
              label={course.code}
              selected={filter === course.id}
              onPress={() => setFilter(course.id)}
            />
          ))}
      </ScrollView>
      <View style={layout.inline}>
        <Chip label="Latest" selected={sort === 'Latest'} onPress={() => setSort('Latest')} />
        <Chip label="Top" selected={sort === 'Top'} onPress={() => setSort('Top')} />
      </View>
      <View>
        {posts.map((post) => (
          <PostCard key={post.id} post={post} />
        ))}
      </View>
      {!posts.length && (
        <Empty
          title={search ? 'Nothing matched that.' : 'Someone has to go first.'}
          body={
            search
              ? 'Try a shorter phrase or another course.'
              : 'Ask a question or share a study tip with your course crowd.'
          }
          action="Start a discussion"
          onPress={() => nav.navigate('Compose')}
        />
      )}
    </Page>
  );
}
export function ComposeScreen({ route }: NativeStackScreenProps<RootStack, 'Compose'>) {
  const nav = useNav();
  const { data, update } = useStore();
  const editing = data.posts.find((x) => x.id === route.params?.postId && x.ownerId === 'me');
  const [courseId, setCourseId] = useState(
    editing?.courseId ?? route.params?.courseId ?? data.joined[0] ?? data.courses[0]?.id ?? '',
  );
  const [tag, setTag] = useState<Post['tag']>(editing?.tag ?? 'Question');
  const [title, setTitle] = useState(editing?.title ?? route.params?.question ?? '');
  const [body, setBody] = useState(editing?.body ?? '');
  const [anonymous, setAnonymous] = useState(editing?.anonymous ?? false);
  const canPost = title.trim().length >= 5 && body.trim().length >= 5 && !!courseId;
  function publish() {
    if (!canPost) return;
    const id = editing?.id ?? uid();
    update((s) => {
      const item: Post = {
        id,
        title: title.trim(),
        body: body.trim(),
        courseId,
        anonymous,
        author: s.profile.name,
        ownerId: 'me',
        tag,
        createdAt: editing?.createdAt ?? Date.now(),
        score: editing?.score ?? 0,
        comments: editing?.comments ?? [],
      };
      return {
        ...s,
        posts: editing ? s.posts.map((p) => (p.id === id ? item : p)) : [item, ...s.posts],
        joined: s.joined.includes(courseId) ? s.joined : [...s.joined, courseId],
      };
    });
    nav.replace('Thread', { id });
  }
  return (
    <Page
      footer={
        <Button
          label={editing ? 'Save changes' : 'Post discussion'}
          icon="Send"
          disabled={!canPost}
          onPress={publish}
        />
      }
    >
      <Header title={editing ? 'Edit discussion' : 'New discussion'} />
      <T variant="label">Course</T>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ gap: 8 }}
      >
        {data.courses.map((course) => (
          <Chip
            key={course.id}
            label={course.code}
            selected={courseId === course.id}
            onPress={() => setCourseId(course.id)}
          />
        ))}
      </ScrollView>
      <T variant="label">Post type</T>
      <View style={[layout.inline, { flexWrap: 'wrap', gap: 7 }]}>
        {(['Question', 'Study tip', 'Discussion'] as const).map((value) => (
          <Chip key={value} label={value} selected={tag === value} onPress={() => setTag(value)} />
        ))}
      </View>
      <Field
        label="Title"
        value={title}
        onChangeText={setTitle}
        placeholder="What’s on your mind?"
        maxLength={120}
      />
      <T variant="small" style={{ alignSelf: 'flex-end', marginTop: -10 }}>
        {title.length}/120
      </T>
      <Field
        label="Details"
        value={body}
        onChangeText={setBody}
        multiline
        placeholder="A little context helps your classmates help you."
        style={{ minHeight: 155, textAlignVertical: 'top' }}
        maxLength={2000}
      />
      <View style={[layout.card, { gap: 10 }]}>
        <View style={layout.inline}>
          <Icon name={anonymous ? 'LockKeyhole' : 'UserRound'} color={c.blue} />
          <T variant="label" style={{ flex: 1 }}>
            {anonymous ? 'Post as Anonymous' : `Post as ${data.profile.name}`}
          </T>
          <Switch
            accessibilityLabel="Post anonymously"
            value={anonymous}
            onValueChange={setAnonymous}
            trackColor={{ false: c.line, true: c.blue }}
            thumbColor={c.white}
          />
        </View>
        <T variant="small">
          {anonymous
            ? 'Classmates won’t see your profile. In the future service, authorised moderators can identify your account.'
            : 'Your display name will appear with this post.'}
        </T>
      </View>
    </Page>
  );
}
export function ThreadScreen({ route }: NativeStackScreenProps<RootStack, 'Thread'>) {
  const nav = useNav();
  const { data, update } = useStore();
  const post = data.posts.find((x) => x.id === route.params.id);
  const bookmark = useSaved(route.params.id);
  const [reply, setReply] = useState('');
  const [anonymous, setAnonymous] = useState(false);
  const [replyTo, setReplyTo] = useState<Comment | null>(null);
  const [message, setMessage] = useState('');
  const [menu, setMenu] = useState(false);
  if (!post || data.blocked.includes(post.ownerId))
    return (
      <Page>
        <Header title="Discussion" />
        <Empty
          title="This discussion is unavailable."
          body="Return to the community to find another conversation."
        />
      </Page>
    );
  const course = data.courses.find((x) => x.id === post.courseId);
  const comments = post.comments.filter((x) => !data.blocked.includes(x.ownerId));
  function sendReply() {
    if (!reply.trim()) return;
    const item: Comment = {
      id: uid(),
      author: data.profile.name,
      ownerId: 'me',
      anonymous,
      body: reply.trim(),
      parentId: replyTo?.parentId ?? replyTo?.id,
    };
    update((s) => ({
      ...s,
      posts: s.posts.map((p) =>
        p.id === route.params.id ? { ...p, comments: [...p.comments, item] } : p,
      ),
    }));
    setReply('');
    setReplyTo(null);
  }
  function renderComment(comment: Comment, nested = false): React.ReactNode {
    return (
      <View
        key={comment.id}
        style={{
          gap: 9,
          paddingVertical: 14,
          marginLeft: nested ? 25 : 0,
          paddingLeft: nested ? 13 : 0,
          borderLeftWidth: nested ? 1 : 0,
          borderLeftColor: c.line,
        }}
      >
        <View style={layout.inline}>
          <Avatar name={comment.author} anonymous={comment.anonymous} size={32} />
          <T variant="label" style={{ fontSize: 14, flex: 1 }}>
            {visibleAuthor(comment, post)}
            {comment.ownerId === 'me' ? ' · you' : ''}
          </T>
        </View>
        <T>{comment.body}</T>
        <VoteBar id={`comment:${comment.id}`} onReply={() => setReplyTo(comment)} />
      </View>
    );
  }
  return (
    <Page
      footer={
        <View style={{ gap: 8 }}>
          {replyTo && (
            <View style={layout.inline}>
              <T variant="small" style={{ flex: 1 }}>
                Replying to {visibleAuthor(replyTo, post)}
              </T>
              <IconButton icon="X" label="Cancel reply" onPress={() => setReplyTo(null)} />
            </View>
          )}
          <View style={layout.inline}>
            <View style={{ flex: 1 }}>
              <Field
                label="Your reply"
                value={reply}
                onChangeText={setReply}
                placeholder="Add to the discussion…"
                multiline
                maxLength={2000}
              />
            </View>
            <IconButton
              icon="Send"
              label="Send reply"
              color={reply.trim() ? c.blue : c.muted}
              onPress={sendReply}
            />
          </View>
          <View style={[layout.inline, { justifyContent: 'space-between' }]}>
            <T variant="small">Reply anonymously</T>
            <Switch
              accessibilityLabel="Reply anonymously"
              value={anonymous}
              onValueChange={setAnonymous}
              trackColor={{ false: c.line, true: c.blue }}
              thumbColor={c.white}
            />
          </View>
        </View>
      }
    >
      <Header
        title={course?.code ?? 'Discussion'}
        right={
          <IconButton
            icon="MoreHorizontal"
            label="Discussion options"
            onPress={() => setMenu(!menu)}
          />
        }
      />
      {menu && (
        <View style={layout.card}>
          {post.ownerId === 'me' ? (
            <Button
              label="Edit discussion"
              kind="secondary"
              onPress={() => nav.navigate('Compose', { postId: post.id })}
            />
          ) : (
            <>
              <Button
                label="Report discussion"
                kind="secondary"
                icon="Flag"
                onPress={() => {
                  setMessage('Report preview: no report is sent without the backend.');
                  setMenu(false);
                }}
              />
              <Button
                label="Block this author locally"
                kind="danger"
                onPress={() => {
                  update((s) => ({ ...s, blocked: [...s.blocked, post.ownerId] }));
                  nav.goBack();
                }}
              />
            </>
          )}
        </View>
      )}
      {message && <Notice>{message}</Notice>}
      <View style={layout.inline}>
        <Avatar name={post.author} anonymous={post.anonymous} />
        <View>
          <T variant="label">
            {visibleAuthor(post)}
            {post.ownerId === 'me' ? ' · you' : ''}
          </T>
          <T variant="small">
            {course?.code} · {timeAgo(post.createdAt)} ago
          </T>
        </View>
      </View>
      <T variant="heading">{post.title}</T>
      <T>{post.body}</T>
      <View style={[layout.inline, { justifyContent: 'space-between', gap: 0 }]}>
        <VoteBar
          id={post.id}
          score={post.score}
          replies={comments.length}
          onReply={() => setReplyTo(null)}
        />
        <IconButton
          icon="Bookmark"
          label={bookmark.saved ? 'Unsave discussion' : 'Save discussion'}
          color={c.blue}
          selected={bookmark.saved}
          onPress={bookmark.toggle}
        />
      </View>
      <View style={layout.rule} />
      <Section title={`Replies (${comments.length})`} />
      {comments
        .filter((x) => !x.parentId || !comments.some((p) => p.id === x.parentId))
        .map((comment) => (
          <View key={comment.id}>
            {renderComment(comment)}
            {comments
              .filter((x) => x.parentId === comment.id)
              .map((child) => renderComment(child, true))}
          </View>
        ))}
      {!comments.length && (
        <T style={{ color: c.muted }}>A helpful answer can make someone’s day.</T>
      )}
    </Page>
  );
}
