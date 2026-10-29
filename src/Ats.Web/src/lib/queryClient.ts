import { QueryClient } from '@tanstack/react-query';

const STALE_TIME_MS = 30_000; // Treat data as fresh for 30s to avoid refetch storms on navigation.

/* Retry once: 401s are handled by the interceptor and most other errors aren't worth retrying.
   No refetch on window focus; queries can opt in. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: STALE_TIME_MS,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});
