import type { FeedbackRecommendation, InterviewStatus, InterviewType } from './enums';

/* List row (Interviews.Application.InterviewListItemDto), from GET /api/v1/interviews. The candidate
   name is joined server-side; interviewer ids are resolved to names on the client via /users. */
export interface InterviewListItem {
  id: string;
  applicationId: string;
  candidateName: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
  interviewerUserIds: string[];
}

/* GET /api/v1/interviews/{id} (Interviews.Application.InterviewDetailDto). Note it carries no
   candidate name — the detail screen resolves that from the application (applicationId). */
export interface InterviewDetail {
  id: string;
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  location: string | null;
  status: InterviewStatus;
  notes: string | null;
  interviewerUserIds: string[];
}

/* POST /api/v1/interviews body (InterviewsController.ScheduleInterviewBody). scheduledAtUtc is an ISO
   8601 UTC string — the form collects local date+time and converts before sending. */
export interface ScheduleInterviewRequest {
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  location?: string;
  interviewerUserIds: string[];
  notes?: string;
}

/* PUT /api/v1/interviews/{id}/reschedule body (InterviewsController.RescheduleBody). */
export interface RescheduleRequest {
  scheduledAtUtc: string;
  durationMinutes: number;
}

/* POST /api/v1/interviews/{id}/feedback body (InterviewsController.SubmitFeedbackBody). Submitting is
   built in PR-2; the type lives here so the whole interview contract sits in one file. */
export interface SubmitFeedbackRequest {
  rating: number;
  recommendation: FeedbackRecommendation;
  comments?: string;
}
