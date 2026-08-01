import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { NotificationChannel } from '../../api/types';
import { api } from '../../lib/api';
import { notifyError } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * One integer a minute. At fifteen to thirty users a socket costs more than it saves, and this keeps
 * working through a proxy that would have to be taught about websockets.
 */
const POLL_MS = 60_000;

export function useUnreadCount() {
  return useQuery({
    queryKey: keys.notifications.unreadCount,
    queryFn: () => api.notifications.unreadCount(),
    refetchInterval: POLL_MS,
    refetchOnWindowFocus: true,
  });
}

export function useNotifications(enabled: boolean) {
  return useQuery({
    queryKey: keys.notifications.list,
    queryFn: () => api.notifications.list(),
    enabled,
  });
}

export function useMarkRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ids?: string[]) => api.notifications.markRead(ids),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.notifications.all }),
    onError: notifyError,
  });
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: keys.notifications.preferences,
    queryFn: () => api.me.preferences(),
  });
}

export function useSavePreferences() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: { channel: NotificationChannel | null; types: Record<string, boolean> }) =>
      api.me.savePreferences(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.notifications.preferences }),
    onError: notifyError,
  });
}

export function useTelegramLink() {
  const queryClient = useQueryClient();

  const start = useMutation({
    mutationFn: () => api.me.startTelegramLink(),
    onError: notifyError,
  });

  const unlink = useMutation({
    mutationFn: () => api.me.unlinkTelegram(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.notifications.preferences }),
    onError: notifyError,
  });

  return { start, unlink };
}
