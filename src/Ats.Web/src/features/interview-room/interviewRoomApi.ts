import { apiClient } from '@/lib/apiClient';
import type { InterviewType } from '@/types/enums';

export type InterviewRoomState = 'TooEarly' | 'Open' | 'Ended' | 'Unavailable';

export interface InterviewRoomInfo {
  interviewId: string;
  jobTitle: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  state: InterviewRoomState;
  opensAtUtc: string;
}

/* GET /api/v1/interview-room/{roomToken} — resolves the token to the interview behind it, for
   whichever participant kind is calling (candidate or company interviewer); the backend does the
   resource-based authorization, this call either succeeds or 401/404s. */
export async function getInterviewRoom(roomToken: string): Promise<InterviewRoomInfo> {
  const { data } = await apiClient.get<InterviewRoomInfo>(
    `/api/v1/interview-room/${encodeURIComponent(roomToken)}`,
  );
  return data;
}
