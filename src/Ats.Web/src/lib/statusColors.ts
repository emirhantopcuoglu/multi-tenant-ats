import type {
  ApplicationStatus,
  FeedbackRecommendation,
  InterviewStatus,
  JobStatus,
} from '@/types/enums';

/* Visual tones a Badge can take, named after the prototype's pill kinds (Design System.dc.html). */
export type BadgeTone =
  | 'neutral'
  | 'gray'
  | 'accent'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'solidDanger'
  | 'solidSuccess';

/* Status → tone mapping, ported verbatim from the design's "Status color mapping" (the `sb(...)`
   calls in Design System.dc.html). Kept as exhaustive Record<Enum, BadgeTone> maps so adding a new
   enum value becomes a compile error here until its colour is decided — no silent default. */

export const jobStatusTone: Record<JobStatus, BadgeTone> = {
  Draft: 'neutral',
  Published: 'success',
  Closed: 'warning',
  Archived: 'gray',
};

export const applicationStatusTone: Record<ApplicationStatus, BadgeTone> = {
  Active: 'accent',
  Hired: 'success',
  Rejected: 'danger',
  Withdrawn: 'gray',
};

export const interviewStatusTone: Record<InterviewStatus, BadgeTone> = {
  Scheduled: 'accent',
  Completed: 'success',
  Cancelled: 'danger',
  NoShow: 'warning',
};

export const recommendationTone: Record<FeedbackRecommendation, BadgeTone> = {
  StrongNoHire: 'solidDanger',
  NoHire: 'danger',
  Hire: 'success',
  StrongHire: 'solidSuccess',
};
