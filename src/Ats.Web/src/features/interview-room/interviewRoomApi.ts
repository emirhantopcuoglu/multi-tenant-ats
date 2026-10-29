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

/* The backend checks the caller is a participant; this either succeeds or fails with 401/404. */
export async function getInterviewRoom(roomToken: string): Promise<InterviewRoomInfo> {
  const { data } = await apiClient.get<InterviewRoomInfo>(
    `/api/v1/interview-room/${encodeURIComponent(roomToken)}`,
  );
  return data;
}
