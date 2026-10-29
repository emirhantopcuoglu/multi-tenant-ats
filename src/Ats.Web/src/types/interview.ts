import type { FeedbackRecommendation, InterviewStatus, InterviewType } from './enums';

/* Interviewer ids are resolved to names on the client through /users. */
export interface InterviewListItem {
  id: string;
  applicationId: string;
  candidateName: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
  interviewerUserIds: string[];
  /* Still Scheduled, but the slot passed with no outcome. */
  isAwaitingOutcome: boolean;
}

/* No candidate name; the detail page resolves it from applicationId. The can* flags come from the
   server so the client doesn't re-derive them from the clock. */
export interface InterviewDetail {
  id: string;
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
  notes: string | null;
  interviewerUserIds: string[];
  /* Null for phone screens. Only panel members may use it; the join endpoint checks. */
  roomToken: string | null;
  isAwaitingOutcome: boolean;
  canReschedule: boolean;
  canCancel: boolean;
  canComplete: boolean;
  canMarkNoShow: boolean;
  canReassignInterviewers: boolean;
  canReceiveFeedback: boolean;
  cancellationReason: InterviewCancellationReason | null;
  cancellationNote: string | null;
  noShowParty: NoShowParty | null;
}

/* Upcoming and AwaitingOutcome are both Scheduled, split by the clock. */
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

/* scheduledAtUtc is an ISO UTC string. */
export interface ScheduleInterviewRequest {
  applicationId: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  interviewerUserIds: string[];
  notes?: string;
}

export interface RescheduleRequest {
  scheduledAtUtc: string;
  durationMinutes: number;
}

/* Candidate-facing: it picks the cancellation email's sentence. Only Rescheduling promises a new
   invitation. */
export const INTERVIEW_CANCELLATION_REASONS = [
  'Rescheduling',
  'CandidateRequested',
  'CandidateWithdrew',
  'PositionClosed',
  'Other',
] as const;

/* Set by the system on rejection, so it isn't offered in the cancel dialog. */
export type SelectableInterviewCancellationReason =
  (typeof INTERVIEW_CANCELLATION_REASONS)[number];

export type InterviewCancellationReason =
  | SelectableInterviewCancellationReason
  | 'ApplicationRejected';

/* `note` is internal and never reaches the candidate. */
export interface CancelInterviewRequest {
  reason: InterviewCancellationReason;
  note?: string;
}

/* A candidate no-show and an interviewer no-show mean different things. */
export const NO_SHOW_PARTIES = ['Candidate', 'Interviewer'] as const;

export type NoShowParty = (typeof NO_SHOW_PARTIES)[number];

export interface MarkNoShowRequest {
  party: NoShowParty;
}

/* The whole new panel, not a diff. */
export interface ReassignInterviewersRequest {
  interviewerUserIds: string[];
}

export interface ApplicationInterviewOutcome {
  totalCount: number;
  completedCount: number;
  awaitingOutcomeCount: number;
  feedbackCount: number;
  expectedFeedbackCount: number;
  averageRating: number | null;
  recommendationCounts: Partial<Record<FeedbackRecommendation, number>>;
}

export interface SubmitFeedbackRequest {
  rating: number;
  recommendation: FeedbackRecommendation;
  comments?: string;
}

export interface InterviewFeedbackItem {
  id: string;
  interviewerUserId: string;
  rating: number;
  recommendation: FeedbackRecommendation;
  comments: string | null;
  submittedAtUtc: string;
}

/* `items` is empty with isWithheld when the caller is on the panel and hasn't submitted yet. The
   counts are always filled in. */
export interface InterviewFeedbackSummary {
  items: InterviewFeedbackItem[];
  submittedCount: number;
  expectedCount: number;
  averageRating: number | null;
  isWithheld: boolean;
  hasCallerSubmitted: boolean;
}
