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
  /** Server-derived: still `Scheduled`, but its slot has passed with no outcome recorded. */
  isAwaitingOutcome: boolean;
}

/* GET /api/v1/interviews/{id} (Interviews.Application.InterviewDetailDto). Note it carries no
   candidate name — the detail screen resolves that from the application (applicationId).

   The can* flags come from the domain rather than being re-derived here from status + the browser
   clock. That duplication is what let the detail page offer "cancel" on an interview that had
   already happened, so the rule now lives in one place and travels with the row. */
export interface InterviewDetail {
  id: string;
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
  notes: string | null;
  interviewerUserIds: string[];
  isAwaitingOutcome: boolean;
  canReschedule: boolean;
  canCancel: boolean;
  canComplete: boolean;
  canMarkNoShow: boolean;
  canReceiveFeedback: boolean;
}

/* Server-side list buckets (Interviews.Application.InterviewListFilter). Upcoming and
   AwaitingOutcome are both slices of the Scheduled status split by the clock, which is why this is
   not simply InterviewStatus. */
export const INTERVIEW_LIST_FILTERS = [
  'Upcoming',
  'AwaitingOutcome',
  'Completed',
  'Cancelled',
  'NoShow',
] as const;

export type InterviewListFilter = (typeof INTERVIEW_LIST_FILTERS)[number];

export function isInterviewListFilter(value: unknown): value is InterviewListFilter {
  return INTERVIEW_LIST_FILTERS.includes(value as InterviewListFilter);
}

/* POST /api/v1/interviews body (InterviewsController.ScheduleInterviewBody). scheduledAtUtc is an ISO
   8601 UTC string — the form collects a local date + time and converts before sending. */
export interface ScheduleInterviewRequest {
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  interviewerUserIds: string[];
  notes?: string;
}

/* PUT /api/v1/interviews/{id}/reschedule body (InterviewsController.RescheduleBody). */
export interface RescheduleRequest {
  scheduledAtUtc: string;
  durationMinutes: number;
}

/* Why a scheduled interview was called off (Interviews.Domain.InterviewCancellationReason). The
   value picks the sentence the candidate's cancellation email leads with, so it is candidate-facing
   even though the recruiter never sees the email — `Rescheduling` is the only one that promises a
   follow-up invitation. */
export const INTERVIEW_CANCELLATION_REASONS = [
  'Rescheduling',
  'CandidateRequested',
  'CandidateWithdrew',
  'PositionClosed',
  'Other',
] as const;

export type InterviewCancellationReason = (typeof INTERVIEW_CANCELLATION_REASONS)[number];

/* POST /api/v1/interviews/{id}/cancel body (InterviewsController.CancelInterviewBody). `note` is
   internal: it is stored on the interview and never reaches the candidate. */
export interface CancelInterviewRequest {
  reason: InterviewCancellationReason;
  note?: string;
}

/* POST /api/v1/interviews/{id}/feedback body (InterviewsController.SubmitFeedbackBody). Submitting is
   built in PR-2; the type lives here so the whole interview contract sits in one file. */
export interface SubmitFeedbackRequest {
  rating: number;
  recommendation: FeedbackRecommendation;
  comments?: string;
}
