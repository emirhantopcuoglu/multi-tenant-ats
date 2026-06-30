import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelInterview,
  completeInterview,
  getInterview,
  listInterviews,
  markInterviewNoShow,
  rescheduleInterview,
  scheduleInterview,
  type ListInterviewsParams,
} from './interviewsApi';
import { listApplications } from '@/features/applications/applicationsApi';
import type { RescheduleRequest } from '@/types/interview';

/* Root key for every interviews query, so a single invalidate after any mutation refreshes all
   filtered list pages and the open detail. */
const INTERVIEWS_KEY = ['interviews'] as const;
export const interviewsListKey = (params: ListInterviewsParams) =>
  [...INTERVIEWS_KEY, 'list', params] as const;
export const interviewDetailKey = (id: string) => [...INTERVIEWS_KEY, 'detail', id] as const;

export function useInterviews(params: ListInterviewsParams) {
  return useQuery({
    queryKey: interviewsListKey(params),
    queryFn: () => listInterviews(params),
    // Keep the current page visible while the next loads (no empty flash on paging/filtering).
    placeholderData: keepPreviousData,
  });
}

export function useInterview(id: string) {
  return useQuery({ queryKey: interviewDetailKey(id), queryFn: () => getInterview(id) });
}

/* Schedule mutation. Invalidates the whole interviews cache on success so every list reflects the new
   row; the caller handles navigation and the toast. */
export function useScheduleInterview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: scheduleInterview,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: INTERVIEWS_KEY }),
  });
}

/* Reschedule + the three terminal transitions for the detail page. Each invalidates the interviews
   cache on success so the detail and the lists reflect the new schedule/status. Toasts are the
   caller's responsibility (it knows which action ran). */
export function useInterviewActions(id: string) {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: INTERVIEWS_KEY });

  const reschedule = useMutation({
    mutationFn: (body: RescheduleRequest) => rescheduleInterview(id, body),
    onSuccess: invalidate,
  });
  const cancel = useMutation({ mutationFn: () => cancelInterview(id), onSuccess: invalidate });
  const complete = useMutation({ mutationFn: () => completeInterview(id), onSuccess: invalidate });
  const noShow = useMutation({ mutationFn: () => markInterviewNoShow(id), onSuccess: invalidate });

  return { reschedule, cancel, complete, noShow };
}

// 100 is the backend's max page size — enough active applications to schedule against for an MVP
// tenant. Only Active applications can receive an interview, so the picker filters to them.
const APPLICATION_OPTIONS_PAGE_SIZE = 100;

/* Active applications for the standalone schedule modal's candidate picker. When the modal is opened
   from an application's detail page the id is already known, so this only runs on the list screen. */
export function useActiveApplicationOptions(enabled: boolean) {
  return useQuery({
    queryKey: ['applications', 'active-options'],
    queryFn: () => listApplications({ page: 1, pageSize: APPLICATION_OPTIONS_PAGE_SIZE, status: 'Active' }),
    enabled,
    staleTime: 30_000,
  });
}
