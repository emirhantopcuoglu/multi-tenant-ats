import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type {
  ApplicationInterviewOutcome,
  CancelInterviewRequest,
  InterviewDetail,
  InterviewFeedbackSummary,
  InterviewListFilter,
  InterviewListItem,
  MarkNoShowRequest,
  ReassignInterviewersRequest,
  RescheduleRequest,
  ScheduleInterviewRequest,
  SubmitFeedbackRequest,
} from '@/types/interview';

const INTERVIEWS_BASE = `${API_V1}/interviews`;

export interface ListInterviewsParams {
  page: number;
  pageSize: number;
  /** ISO 8601 lower/upper bounds on scheduledAtUtc. */
  fromDate?: string;
  toDate?: string;
  interviewerId?: string;
  applicationId?: string;
  /** Lifecycle bucket; omitted means every interview regardless of state. */
  filter?: InterviewListFilter;
}

export async function listInterviews(
  params: ListInterviewsParams,
): Promise<PagedResult<InterviewListItem>> {
  const { data } = await apiClient.get<PagedResult<InterviewListItem>>(INTERVIEWS_BASE, {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      fromDate: params.fromDate,
      toDate: params.toDate,
      interviewerId: params.interviewerId,
      applicationId: params.applicationId,
      filter: params.filter,
    },
  });
  return data;
}

export async function getInterview(id: string): Promise<InterviewDetail> {
  const { data } = await apiClient.get<InterviewDetail>(`${INTERVIEWS_BASE}/${id}`);
  return data;
}

/* POST returns the new interview's id ({ id }); the caller navigates to its detail page. */
export async function scheduleInterview(body: ScheduleInterviewRequest): Promise<string> {
  const { data } = await apiClient.post<{ id: string }>(INTERVIEWS_BASE, body);
  return data.id;
}

export async function rescheduleInterview(id: string, body: RescheduleRequest): Promise<void> {
  await apiClient.put(`${INTERVIEWS_BASE}/${id}/reschedule`, body);
}

/* The three terminal lifecycle transitions. The backend gates each on CanManageInterviews and on
   the interview's own state — which now includes the clock, so a transition can be refused with 409
   even though the caller had a button for it (a stale page).

   Cancelling takes a reason: it is the only transition that emails the candidate, and the reason
   decides whether that email promises a new invitation. */
export async function cancelInterview(id: string, body: CancelInterviewRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/cancel`, body);
}

export async function completeInterview(id: string): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/complete`);
}

/* Takes which side failed to appear: "nobody came" is not a usable record when it cannot say
   whether that reflects on the candidate or on us. */
export async function markInterviewNoShow(id: string, body: MarkNoShowRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/no-show`, body);
}

/* Swaps the panel without touching the time — a separate endpoint from reschedule because they are
   different operations. Can fail with 409 interviewer_conflict if a new interviewer is already
   booked over this slot. */
export async function reassignInterviewers(
  id: string,
  body: ReassignInterviewersRequest,
): Promise<void> {
  await apiClient.put(`${INTERVIEWS_BASE}/${id}/interviewers`, body);
}

/* Submit feedback for an interview. The backend authorizes this twice: the caller's role (handled by
   the list/detail gating) and, resource-based, that the caller is one of the interview's assigned
   interviewers — so it can still fail with 403, or 409 (cancelled / already submitted) even when the
   UI shows the form. The interviewer identity comes from the JWT, never the body. */
export async function submitFeedback(id: string, body: SubmitFeedbackRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/feedback`, body);
}

/* Reads the panel's evaluations. The server decides what this caller may see — an interviewer who
   has not filed their own gets empty items and `isWithheld` — so the client renders the answer
   rather than reproducing the rule. */
export async function getInterviewFeedback(id: string): Promise<InterviewFeedbackSummary> {
  const { data } = await apiClient.get<InterviewFeedbackSummary>(
    `${INTERVIEWS_BASE}/${id}/feedback`,
  );
  return data;
}

/* Roll-up of one application's interviews, for the decision on the application screen. */
export async function getApplicationInterviewOutcome(
  applicationId: string,
): Promise<ApplicationInterviewOutcome> {
  const { data } = await apiClient.get<ApplicationInterviewOutcome>(`${INTERVIEWS_BASE}/outcome`, {
    params: { applicationId },
  });
  return data;
}
