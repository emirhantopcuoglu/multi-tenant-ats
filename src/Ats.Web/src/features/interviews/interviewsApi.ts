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
  fromDate?: string;
  toDate?: string;
  interviewerId?: string;
  applicationId?: string;
  /* Omitted means all interviews. */
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

export async function scheduleInterview(body: ScheduleInterviewRequest): Promise<string> {
  const { data } = await apiClient.post<{ id: string }>(INTERVIEWS_BASE, body);
  return data.id;
}

export async function rescheduleInterview(id: string, body: RescheduleRequest): Promise<void> {
  await apiClient.put(`${INTERVIEWS_BASE}/${id}/reschedule`, body);
}

/* May return 409 when the page is stale. Cancel takes a reason because it emails the candidate. */
export async function cancelInterview(id: string, body: CancelInterviewRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/cancel`, body);
}

export async function completeInterview(id: string): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/complete`);
}

export async function markInterviewNoShow(id: string, body: MarkNoShowRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/no-show`, body);
}

/* May return 409 interviewer_conflict. */
export async function reassignInterviewers(
  id: string,
  body: ReassignInterviewersRequest,
): Promise<void> {
  await apiClient.put(`${INTERVIEWS_BASE}/${id}/interviewers`, body);
}

/* May still return 403 or 409 even when the form is shown. The interviewer comes from the token. */
export async function submitFeedback(id: string, body: SubmitFeedbackRequest): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/feedback`, body);
}

/* The server decides what is withheld. */
export async function getInterviewFeedback(id: string): Promise<InterviewFeedbackSummary> {
  const { data } = await apiClient.get<InterviewFeedbackSummary>(
    `${INTERVIEWS_BASE}/${id}/feedback`,
  );
  return data;
}

export async function getApplicationInterviewOutcome(
  applicationId: string,
): Promise<ApplicationInterviewOutcome> {
  const { data } = await apiClient.get<ApplicationInterviewOutcome>(`${INTERVIEWS_BASE}/outcome`, {
    params: { applicationId },
  });
  return data;
}
