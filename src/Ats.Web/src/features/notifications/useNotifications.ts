import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getUnreadNotificationCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from './notificationsApi';

/* All notification caches share this prefix, so a single invalidation after a mutation refreshes
   both the badge count and every cached list page. */
const NOTIFICATIONS_KEY = ['candidate', 'notifications'] as const;

/* Polling instead of a push channel: the project has no WebSocket/SSE infrastructure, and a badge
   that lags by up to 30s is fine. The backend exposes unread-count as a dedicated bare COUNT
   endpoint precisely so this poll stays cheap. */
const UNREAD_POLL_INTERVAL_MS = 30_000;

export function useUnreadNotificationCount() {
  return useQuery({
    queryKey: [...NOTIFICATIONS_KEY, 'unread-count'],
    queryFn: getUnreadNotificationCount,
    refetchInterval: UNREAD_POLL_INTERVAL_MS,
  });
}

/* `enabled` lets the bell dropdown defer fetching until it is actually opened. */
export function useNotifications(page: number, pageSize: number, enabled = true) {
  return useQuery({
    queryKey: [...NOTIFICATIONS_KEY, 'list', page, pageSize],
    queryFn: () => listNotifications(page, pageSize),
    enabled,
    placeholderData: (prev) => prev,
  });
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_KEY });
    },
  });
}

export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_KEY });
    },
  });
}
