import { QueryClient } from '@tanstack/react-query';

const STALE_TIME_MS = 30_000; // Treat data as fresh for 30s to avoid refetch storms on navigation.

/* Shared TanStack Query client with conservative defaults:
   - retry once: a 401 is already handled by the apiClient interceptor, and most other failures
     (404, validation) are not worth retrying; one retry covers transient blips.
   - no refetch on window focus: this is a data-entry tool, not a live dashboard, so aggressive
     refocus refetching would be noise. Screens that need freshness can opt in per-query. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: STALE_TIME_MS,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});
