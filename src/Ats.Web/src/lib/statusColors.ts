import type {
  ApplicationStatus,
  CvJobFitRating,
  FeedbackRecommendation,
  InterviewStatus,
  JobStatus,
  Role,
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

export const cvJobFitRatingTone: Record<CvJobFitRating, BadgeTone> = {
  Weak: 'danger',
  Moderate: 'warning',
  Strong: 'success',
};

/* Role → tone for the user directory badges. Admin is the privileged role, so it takes the accent
   tone; the rest are informational. Exhaustive like the maps above — a new role won't compile until
   its colour is chosen here. */
export const roleTone: Record<Role, BadgeTone> = {
  Admin: 'accent',
  Recruiter: 'info',
  HiringManager: 'neutral',
  ReadOnly: 'gray',
};
