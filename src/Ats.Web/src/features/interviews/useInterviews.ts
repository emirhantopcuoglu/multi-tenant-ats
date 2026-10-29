import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelInterview,
  completeInterview,
  getApplicationInterviewOutcome,
  getInterview,
  getInterviewFeedback,
  listInterviews,
  markInterviewNoShow,
  reassignInterviewers,
  rescheduleInterview,
  scheduleInterview,
  submitFeedback,
  type ListInterviewsParams,
} from './interviewsApi';
import type {
  CancelInterviewRequest,
  MarkNoShowRequest,
  ReassignInterviewersRequest,
  RescheduleRequest,
  SubmitFeedbackRequest,
} from '@/types/interview';

/* One invalidate refreshes every list and the open detail. */
const INTERVIEWS_KEY = ['interviews'] as const;
export const interviewsListKey = (params: ListInterviewsParams) =>
  [...INTERVIEWS_KEY, 'list', params] as const;
export const interviewDetailKey = (id: string) => [...INTERVIEWS_KEY, 'detail', id] as const;

export function useInterviews(params: ListInterviewsParams) {
  return useQuery({
    queryKey: interviewsListKey(params),
    queryFn: () => listInterviews(params),
    placeholderData: keepPreviousData,
  });
}

export function useInterview(id: string) {
  return useQuery({ queryKey: interviewDetailKey(id), queryFn: () => getInterview(id) });
}

export function useScheduleInterview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: scheduleInterview,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: INTERVIEWS_KEY }),
  });
}

/* Toasts are left to the caller. */
export function useInterviewActions(id: string) {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: INTERVIEWS_KEY });

  const reschedule = useMutation({
    mutationFn: (body: RescheduleRequest) => rescheduleInterview(id, body),
    onSuccess: invalidate,
  });
  const cancel = useMutation({
    mutationFn: (body: CancelInterviewRequest) => cancelInterview(id, body),
    onSuccess: invalidate,
  });
  const complete = useMutation({ mutationFn: () => completeInterview(id), onSuccess: invalidate });
  const noShow = useMutation({
    mutationFn: (body: MarkNoShowRequest) => markInterviewNoShow(id, body),
    onSuccess: invalidate,
  });
  const reassign = useMutation({
    mutationFn: (body: ReassignInterviewersRequest) => reassignInterviewers(id, body),
    onSuccess: invalidate,
  });

  return { reschedule, cancel, complete, noShow, reassign };
}

export const interviewOutcomeKey = (applicationId: string) =>
  [...INTERVIEWS_KEY, 'outcome', applicationId] as const;

export function useApplicationInterviewOutcome(applicationId: string) {
  return useQuery({
    queryKey: interviewOutcomeKey(applicationId),
    queryFn: () => getApplicationInterviewOutcome(applicationId),
  });
}

export const interviewFeedbackKey = (id: string) => [...INTERVIEWS_KEY, 'feedback', id] as const;

export function useInterviewFeedback(id: string) {
  return useQuery({
    queryKey: interviewFeedbackKey(id),
    queryFn: () => getInterviewFeedback(id),
  });
}

/* Also invalidates the feedback list: submitting un-withholds the panel's feedback for this user. */
export function useSubmitFeedback(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SubmitFeedbackRequest) => submitFeedback(id, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: interviewDetailKey(id) });
      queryClient.invalidateQueries({ queryKey: interviewFeedbackKey(id) });
    },
  });
}
