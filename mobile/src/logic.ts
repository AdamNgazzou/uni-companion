import type { Comment, Post } from './types';
export function nextVote(current: -1 | 0 | 1, direction: -1 | 1): -1 | 0 | 1 {
  return current === direction ? 0 : direction;
}
export function visibleAuthor(
  item: Pick<Comment, 'anonymous' | 'author' | 'ownerId'>,
  post?: Pick<Post, 'ownerId'>,
): string {
  if (!item.anonymous) return item.author;
  return post && item.ownerId === post.ownerId ? 'Anonymous author' : 'Anonymous';
}
export function quizResult(answers: { id: number; correct: boolean }[]) {
  return {
    correct: answers.filter((x) => x.correct).length,
    total: answers.length,
    missed: answers.filter((x) => !x.correct).map((x) => x.id),
  };
}
