import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type {
  InterviewDetail,
  InterviewListItem,
  RescheduleRequest,
  ScheduleInterviewRequest,
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

/* The three terminal lifecycle transitions. Each is a parameterless POST that the backend gates on
   CanManageInterviews and the interview's current status (e.g. you cannot complete a cancelled one). */
export async function cancelInterview(id: string): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/cancel`);
}

export async function completeInterview(id: string): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/complete`);
}

export async function markInterviewNoShow(id: string): Promise<void> {
  await apiClient.post(`${INTERVIEWS_BASE}/${id}/no-show`);
}
