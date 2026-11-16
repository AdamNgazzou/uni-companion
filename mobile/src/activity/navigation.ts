import { router } from 'expo-router';
import { api } from '../api/runtime';
import type { ActivityDestination } from '../api/types';
export async function openActivity(
  id: string,
  signal?: AbortSignal,
  current: () => boolean = () => true,
) {
  const destination = await api.request<ActivityDestination>(`/activity/${id}/destination`, {
    signal,
  });
  await api.request(`/activity/${id}/read`, { method: 'PUT', signal });
  if (signal?.aborted || !current()) return;
  if (destination.kind === 'revision') router.push('/revision');
  else if (destination.kind === 'study')
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
}
