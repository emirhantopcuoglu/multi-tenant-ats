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
  canReassignInterviewers: boolean;
  canReceiveFeedback: boolean;
  /** Outcome details; each null unless the interview actually reached that state. */
  cancellationReason: InterviewCancellationReason | null;
  cancellationNote: string | null;
  noShowParty: NoShowParty | null;
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

/* ApplicationRejected is set by the system when the application behind the interview is rejected.
   It is not in the list above because it must not be offered in the cancel dialog — rejecting an
   application is its own action, not a way to call off a meeting. */
export type SelectableInterviewCancellationReason =
  (typeof INTERVIEW_CANCELLATION_REASONS)[number];

export type InterviewCancellationReason =
  | SelectableInterviewCancellationReason
  | 'ApplicationRejected';

/* POST /api/v1/interviews/{id}/cancel body (InterviewsController.CancelInterviewBody). `note` is
   internal: it is stored on the interview and never reaches the candidate. */
export interface CancelInterviewRequest {
  reason: InterviewCancellationReason;
  note?: string;
}

/* Who failed to appear (Interviews.Domain.NoShowParty). Recorded separately from the NoShow status
   because a candidate who did not turn up is a signal about that candidate, while an interviewer
   who did not is the company's own failure — one record cannot serve both. */
export const NO_SHOW_PARTIES = ['Candidate', 'Interviewer'] as const;

export type NoShowParty = (typeof NO_SHOW_PARTIES)[number];

/* POST /api/v1/interviews/{id}/no-show body (InterviewsController.MarkNoShowBody). */
export interface MarkNoShowRequest {
  party: NoShowParty;
}

/* PUT /api/v1/interviews/{id}/interviewers body. The full replacement panel, not a delta. */
export interface ReassignInterviewersRequest {
  interviewerUserIds: string[];
}

/* GET /api/v1/interviews/outcome?applicationId= (ApplicationInterviewOutcomeDto). The roll-up a
   recruiter decides on: are the interviews finished, is all the feedback in, what did it say.
   `awaitingOutcomeCount` is interviews whose slot has passed with nothing recorded — deciding while
   those are outstanding means deciding on incomplete information. */
export interface ApplicationInterviewOutcome {
  totalCount: number;
  completedCount: number;
  awaitingOutcomeCount: number;
  feedbackCount: number;
  expectedFeedbackCount: number;
  averageRating: number | null;
  recommendationCounts: Partial<Record<FeedbackRecommendation, number>>;
}

/* POST /api/v1/interviews/{id}/feedback body (InterviewsController.SubmitFeedbackBody). */
export interface SubmitFeedbackRequest {
  rating: number;
  recommendation: FeedbackRecommendation;
  comments?: string;
}

/* One interviewer's evaluation (Interviews.Application.InterviewFeedbackDto). */
export interface InterviewFeedbackItem {
  id: string;
  interviewerUserId: string;
  rating: number;
  recommendation: FeedbackRecommendation;
  comments: string | null;
  submittedAtUtc: string;
}

/* GET /api/v1/interviews/{id}/feedback (InterviewFeedbackSummaryDto).

   `items` comes back empty with `isWithheld: true` when the caller is on the panel but has not
   filed their own evaluation yet — feedback is immutable by design, and reading the others first
   would just move the anchoring earlier instead of preventing it. The counts are still populated in
   that case, because "1 of 3 submitted" is progress information, not a leak. */
export interface InterviewFeedbackSummary {
  items: InterviewFeedbackItem[];
  submittedCount: number;
  expectedCount: number;
  averageRating: number | null;
  isWithheld: boolean;
  hasCallerSubmitted: boolean;
}
