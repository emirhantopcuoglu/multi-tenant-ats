import type {
  ApplicationStatus,
  CvJobFitRating,
  FeedbackRecommendation,
  InterviewStatus,
  JobStatus,
  Role,
} from '@/types/enums';

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

/* Exhaustive Records, so a new enum value fails to compile until it has a colour. */

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

/* An elapsed interview with no outcome is still Scheduled in the database but shown as awaiting
   outcome. Warning, because someone needs to act. */
export type InterviewDisplayStatus = InterviewStatus | 'AwaitingOutcome';

export const interviewDisplayStatusTone: Record<InterviewDisplayStatus, BadgeTone> = {
  ...interviewStatusTone,
  AwaitingOutcome: 'warning',
};

export function interviewDisplayStatus(interview: {
  status: InterviewStatus;
  isAwaitingOutcome: boolean;
}): InterviewDisplayStatus {
  return interview.isAwaitingOutcome ? 'AwaitingOutcome' : interview.status;
}

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

/* Admin gets the accent tone. Exhaustive like the maps above. */
export const roleTone: Record<Role, BadgeTone> = {
  Admin: 'accent',
  Recruiter: 'info',
  HiringManager: 'neutral',
  ReadOnly: 'gray',
};
